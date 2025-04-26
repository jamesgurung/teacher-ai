using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace OrgAI;

public interface IChatClient
{
  Task Append(string chunk, string instanceId);
}

[Authorize]
public class ChatHub : Hub<IChatClient> { }