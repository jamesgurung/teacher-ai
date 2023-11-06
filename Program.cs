using Microsoft.AspNetCore.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using TeacherAI;

var builder = WebApplication.CreateBuilder(args);

var storageAccountName = builder.Configuration["Azure:StorageAccountName"];
var storageAccountKey = builder.Configuration["Azure:StorageAccountKey"];
var connectionString = $"DefaultEndpointsProtocol=https;AccountName={storageAccountName};AccountKey={storageAccountKey};EndpointSuffix=core.windows.net";
TableService.Configure(connectionString);

builder.ConfigureAuth();
builder.Services.AddResponseCompression();
builder.Services.AddAntiforgery(options => { options.HeaderName = "X-XSRF-TOKEN"; });
builder.Services.Configure<RouteOptions>(options => { options.LowercaseUrls = true; });
builder.Services.Configure<JsonOptions>(options => { options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase; });
builder.Services.AddRazorPages(options => { options.Conventions.AllowAnonymousToFolder("/auth"); });

Organisation.Instance = builder.Configuration.GetSection("Organisation").Get<Organisation>();
var models = builder.Configuration.GetSection("OpenAI:Models").Get<List<OpenAIModel>>();
OpenAIModel.Dictionary = models.ToDictionary(model => model.Key);
TokenAuthenticationProvider.Configure(builder.Configuration["Azure:TenantId"], builder.Configuration["Azure:ClientId"],
  builder.Configuration["Azure:ClientSecret"], builder.Configuration["Azure:RefreshToken"]);
await ChatGPT.CreateTokenizerAsync();

builder.Services.AddHttpClient("OpenAI", options => {
  options.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", builder.Configuration["OpenAI:Key"]);
  options.BaseAddress = new Uri("https://api.openai.com/v1/chat/completions");
  options.Timeout = TimeSpan.FromMinutes(10);
});
OpenAIModel.Dictionary.Add("credits", new() { Name = "credits", CostPerPromptToken = -1.0m });

builder.Services.AddSignalR();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
  app.UseHsts();
  app.Use(async (context, next) =>
  {
    if (context.Request.Path.Value == "/" && context.Request.Headers.UserAgent.ToString().Equals("alwayson", StringComparison.OrdinalIgnoreCase))
    {
      context.Response.StatusCode = 200;
    }
    else if (!context.Request.Host.Host.Equals(Organisation.Instance.AppWebsite, StringComparison.OrdinalIgnoreCase))
    {
      context.Response.Redirect($"https://{Organisation.Instance.AppWebsite}{context.Request.Path.Value}{context.Request.QueryString}", true);
    }
    else
    {
      await next();
    }
  });
}

app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<ChatHub>("/chat");
app.MapRazorPages();
app.MapAuthPaths();
app.MapApiPaths();

await app.RunAsync();
