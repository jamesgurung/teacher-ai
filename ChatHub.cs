using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TeacherAI;

public interface IChatClient
{
  Task Type(string token);
  Task Feedback(string student, int index, int total);
}

[Authorize]
public class ChatHub : Hub<IChatClient> {}