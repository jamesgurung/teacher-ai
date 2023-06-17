using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Net;
using System.Security.Claims;

namespace TeacherAI;

public static class AuthConfig
{
  public static void ConfigureAuth(this WebApplicationBuilder builder)
  {
    ArgumentNullException.ThrowIfNull(builder);
    builder.Services
      .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
      .AddCookie(o =>
      {
        o.LoginPath = "/auth/login";
        o.LogoutPath = "/auth/logout";
        o.ExpireTimeSpan = TimeSpan.FromDays(90);
        o.SlidingExpiration = true;
        o.ReturnUrlParameter = "path";
        o.Events = new()
        {
          OnRedirectToAccessDenied = context =>
          {
            context.Response.StatusCode = 403;
            return Task.CompletedTask;
          }
        };
      })
      .AddOpenIdConnect("Microsoft", "Microsoft", o =>
      {
        o.Authority = $"https://login.microsoftonline.com/{builder.Configuration["Azure:TenantId"]}/v2.0/";
        o.ClientId = builder.Configuration["Azure:ClientId"];
        o.ClientSecret = builder.Configuration["Azure:ClientSecret"];
        o.ResponseType = OpenIdConnectResponseType.IdToken;
        o.Events = new()
        {
          OnTicketReceived = context =>
          {
            var email = context.Principal.Claims.FirstOrDefault(c => c.Type == "preferred_username").Value.ToLowerInvariant();
            var emailParts = email.Split('@');
            if (!string.Equals(emailParts[1], Organisation.Instance.Domain, StringComparison.OrdinalIgnoreCase) || char.IsDigit(emailParts[0][0]))
            {
              context.Response.Redirect("/auth/denied");
              context.HandleResponse();
              return Task.CompletedTask;
            }
            var isAdmin = string.Equals(email, Organisation.Instance.AdminEmail, StringComparison.OrdinalIgnoreCase);
            var identity = context.Principal.Identity as ClaimsIdentity;
            for (var i = identity.Claims.Count() - 1; i >= 0; i--)
            {
              identity.RemoveClaim(identity.Claims.ElementAt(i));
            }
            identity.AddClaim(new Claim(ClaimTypes.Name, email));
            if (isAdmin) {
              identity.AddClaim(new Claim(ClaimTypes.Role, Roles.Admin));
            }
            identity.AddClaim(new Claim(ClaimTypes.Role, Roles.Staff));
            return Task.CompletedTask;
          }
        };
      });

    builder.Services.AddAuthorizationBuilder().SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
  }

  private static readonly string[] authenticationSchemes = new[] { "Microsoft" };

  public static void MapAuthPaths(this WebApplication app)
  {
    app.MapGet("/auth/login/challenge", [AllowAnonymous] ([FromQuery] string path) =>
      Results.Challenge(
        new AuthenticationProperties { RedirectUri = path is null ? "/" : WebUtility.UrlDecode(path), AllowRefresh = true, IsPersistent = true },
        authenticationSchemes
      )
    );

    app.MapGet("/auth/logout", (HttpContext context) =>
    {
      context.SignOutAsync();
      return Results.Redirect("/auth/login");
    });

    app.MapGet("/auth/authorise-service-account", [Authorize(Roles = Roles.Admin)] () => Results.Redirect(TokenAuthenticationProvider.AuthRedirectUrl));
    app.MapGet("/auth/authorise-service-account/done", async ([FromQuery] string code) => {
      var refreshToken = await TokenAuthenticationProvider.GetRefreshTokenAsync(code);
      return Results.Ok(new { RefreshToken = refreshToken });
    });
  }

}

public static class Roles
{
  public const string Staff = nameof(Staff);
  public const string Student = nameof(Student);
  public const string Admin = nameof(Admin);
}