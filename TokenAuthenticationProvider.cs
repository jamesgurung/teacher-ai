using Microsoft.Graph;
using System.Net.Http.Headers;
using System.Text.Json;

namespace TeacherAI;

public class TokenAuthenticationProvider : IAuthenticationProvider
{
  public static string AuthRedirectUrl =>
    $"https://login.microsoftonline.com/{_tenantId}/oauth2/v2.0/authorize?client_id={_clientId}&prompt=consent&redirect_uri={Uri.EscapeDataString(_redirectUri)}&response_type=code" +
    $"&scope=https%3A%2F%2Fgraph.microsoft.com%2Fuser.read%20https%3A%2F%2Fgraph.microsoft.com%2Ffiles.readwrite.all";
  
  private static string _accessToken;
  private static DateTime _expirationTime;

  private static string _tenantId;
  private static string _clientId;
  private static string _clientSecret;
  private static string _redirectUri;
  private static string _refreshToken;

  private static readonly SemaphoreSlim _semaphore = new(1);

  public static void Configure(string tenantId, string clientId, string clientSecret, string refreshToken)
  {
    _tenantId = tenantId;
    _clientId = clientId;
    _clientSecret = clientSecret;
    _redirectUri = $"https://{Organisation.Instance.AppWebsite}/auth/authorise-service-account/done";
    _refreshToken = refreshToken;
  }

  public async Task AuthenticateRequestAsync(HttpRequestMessage request)
  {
    ArgumentNullException.ThrowIfNull(request);
    await _semaphore.WaitAsync();
    try
    {
      if (DateTime.UtcNow.AddMinutes(5) >= _expirationTime) await GetNewAccessTokenAsync();
    }
    finally
    {
      _semaphore.Release();
    }
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
  }

  private static async Task GetNewAccessTokenAsync()
  {
    using var client = new HttpClient();

    var postData = new List<KeyValuePair<string, string>>()
      {
          new("client_id", _clientId),
          new("scope", "Files.ReadWrite.All offline_access"),
          new("refresh_token", _refreshToken),
          new("grant_type", "refresh_token"),
          new("client_secret", _clientSecret),
          new("redirect_uri", _redirectUri)
      };

    using var content = new FormUrlEncodedContent(postData);

    var response = await client.PostAsync($"https://login.microsoftonline.com/{_tenantId}/oauth2/v2.0/token", content);

    using var dataStream = await response.Content.ReadAsStreamAsync();
    using var reader = new StreamReader(dataStream);
    var responseFromServer = await reader.ReadToEndAsync();
    var jsonDoc = JsonDocument.Parse(responseFromServer);
    _accessToken = jsonDoc.RootElement.GetProperty("access_token").GetString();
    _expirationTime = DateTime.UtcNow.AddSeconds(jsonDoc.RootElement.GetProperty("expires_in").GetInt32());
  }


  public static async Task<string> GetRefreshTokenAsync(string code)
  {
    using var client = new HttpClient();

    var postData = new List<KeyValuePair<string, string>>()
      {
          new("client_id", _clientId),
          new("scope", "Files.ReadWrite.All offline_access"),
          new("code", code),
          new("grant_type", "authorization_code"),
          new("client_secret", _clientSecret),
          new("redirect_uri", _redirectUri)
      };

    using var content = new FormUrlEncodedContent(postData);

    var response = await client.PostAsync($"https://login.microsoftonline.com/{_tenantId}/oauth2/v2.0/token", content);

    using var dataStream = await response.Content.ReadAsStreamAsync();
    using var reader = new StreamReader(dataStream);
    var responseFromServer = await reader.ReadToEndAsync();
    var jsonDoc = JsonDocument.Parse(responseFromServer);
    return jsonDoc.RootElement.GetProperty("refresh_token").GetString();
  }
}
