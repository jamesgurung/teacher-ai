using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Net;
using System.Security.Claims;

namespace OrgAI;

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
        o.ExpireTimeSpan = TimeSpan.FromDays(60);
        o.SlidingExpiration = true;
        o.ReturnUrlParameter = "path";
        o.Events = new()
        {
          OnRedirectToAccessDenied = context =>
          {
            context.Response.StatusCode = 403;
            return Task.CompletedTask;
          },
          OnValidatePrincipal = async context =>
          {
            var issued = context.Properties.IssuedUtc;
            if (issued.HasValue && issued.Value > DateTimeOffset.UtcNow.AddDays(-1))
            {
              return;
            }
            var email = context.Principal.Identity.Name;
            var identity = new ClaimsIdentity(context.Principal.Identity.AuthenticationType);
            if (RefreshIdentity(identity, email))
            {
              context.ReplacePrincipal(new ClaimsPrincipal(identity));
              context.ShouldRenew = true;
            }
            else
            {
              context.RejectPrincipal();
              await context.HttpContext.SignOutAsync();
            }
            ;
          }
        };
      })
      .AddOpenIdConnect("Microsoft", "Microsoft", o =>
      {
        o.Authority = $"https://login.microsoftonline.com/{builder.Configuration["Azure:TenantId"]}/v2.0/";
        o.ClientId = builder.Configuration["Azure:ClientId"];
        o.ResponseType = OpenIdConnectResponseType.IdToken;
        o.Scope.Add("profile");
        o.Events = new()
        {
          OnTicketReceived = context =>
          {
            var email = context.Principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Upn)?.Value.ToLowerInvariant();
            if (!RefreshIdentity((ClaimsIdentity)context.Principal.Identity, email))
            {
              context.Response.Redirect("/auth/denied");
              context.HandleResponse();
            }
            return Task.CompletedTask;
          }
        };
      });

    builder.Services.AddAuthorizationBuilder().SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
  }

  private static bool RefreshIdentity(ClaimsIdentity identity, string email)
  {
    if (email is null || !UserGroup.GroupNameByUserEmail.ContainsKey(email))
    {
      return false;
    }
    for (var i = identity.Claims.Count() - 1; i >= 0; i--)
    {
      identity.RemoveClaim(identity.Claims.ElementAt(i));
    }
    identity.AddClaim(new Claim(ClaimTypes.Name, email));
    return true;
  }

  private static readonly string[] authenticationSchemes = ["Microsoft"];

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
  }
}