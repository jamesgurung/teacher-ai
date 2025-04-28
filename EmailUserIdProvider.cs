using Microsoft.AspNetCore.SignalR;

namespace OrgAI;

public class EmailUserIdProvider : IUserIdProvider
{
  public string GetUserId(HubConnectionContext connection)
  {
    return connection?.User?.Identity?.Name;
  }
}