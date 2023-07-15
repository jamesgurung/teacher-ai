using Azure.Data.Tables;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Globalization;
using System.Text.Json;

namespace TeacherAI;

public static class Api
{
  public static void MapApiPaths(this WebApplication app)
  {
    var group = app.MapGroup("/api").ValidateAntiforgery();

    group.MapPost("/chat", [Authorize] async (ChatRequest chatRequest, HttpContext context, IHttpClientFactory httpClientFactory, IHubContext<ChatHub, IChatClient> hubContext) =>
    {
      var ticks = DateTime.UtcNow.Ticks;
      var nameParts = context.User.Identity.Name.Split('@');
      var service = new TableService(nameParts[1]);

      var remainingCredits = Organisation.Instance.UserCreditsPerWeek - await service.CalculateUsageAsync(nameParts[0]);
      if (remainingCredits <= 0) return Results.BadRequest("Insufficient credits.");

      chatRequest.Messages.Insert(0, new() {
        Role = "system",
        Content = $"You are a friendly and professional assistant, supporting teachers in a UK secondary school. Use British English. " +
        $"It is {DateTime.UtcNow:d MMM yyyy}."
      });
      ChatGPTCompletion response = null;
      int promptTokens = ChatGPT.CountPromptTokens(chatRequest.Messages);
      int completionTokens = 0;
      try
      {
        var chat = new ChatGPT(httpClientFactory.CreateClient(chatRequest.Model), chatRequest.Model, hubContext.Clients, chatRequest.ConnectionId);
        response = await chat.SendGptRequestStreamingAsync(chatRequest.Messages, chatRequest.Temperature, 0.95m, $"{ticks}");
        if (response.FinishReason != "prompt_filter" && response.FinishReason != "error") {
          completionTokens = ChatGPT.CountCompletionTokens(response.Content);
          chatRequest.Messages.Add(new() { Role = "assistant", Content = response.Content });
        }
      } finally {
        #if !DEBUG
        await service.LogChatAsync(nameParts[0], chatRequest, ticks, promptTokens, completionTokens, GetFilterReason(response.FinishReason));
        #endif
      }
      var model = OpenAIModel.Dictionary[chatRequest.Model];
      var thisUsage = promptTokens * model.CostPerPromptToken + completionTokens * model.CostPerCompletionToken;

      var data = new ChatResponse {
        Response = response.Content,
        FinishReason = response.FinishReason,
        RemainingCredits = Math.Max((int)Math.Round(remainingCredits - thisUsage, 0, MidpointRounding.AwayFromZero), 0),
        Stop = promptTokens + completionTokens > 3200 || response.FinishReason != "stop"
      };
      return Results.Ok(data);
    });

    group.MapPost("/feedback", async (FeedbackRequest feedbackRequest, HttpContext context, IHttpClientFactory httpClientFactory, IHubContext<ChatHub, IChatClient> hubContext, IWebHostEnvironment env) =>
    {
      var nameParts = context.User.Identity.Name.Split('@');
      var service = new TableService(nameParts[1]);

      var remainingCredits = Organisation.Instance.UserCreditsPerWeek - await service.CalculateUsageAsync(nameParts[0]);
      if (remainingCredits <= 0) return Results.BadRequest("Insufficient credits.");

      var spreadsheet = new Spreadsheet(feedbackRequest.Url);

      try
      {
        await spreadsheet.OpenAsync();
      }
      catch (SpreadsheetSetupException exc)
      {
        return Results.Problem(exc.Message);
      }

      var chat = new ChatGPT(httpClientFactory.CreateClient("gpt-4"), "gpt-4");
      var fallbackModel = OpenAIModel.Dictionary["gpt-4"].Fallback;
      var fallbackChat = fallbackModel is null ? null : new ChatGPT(httpClientFactory.CreateClient(fallbackModel), "gpt-4");

      var systemPrompt = new ChatGPTMessage
      {
        Role = "system",
        Content =
@"You are a teacher who marks student work. Evaluate it against all the points in the grading criteria, using the same headings that are written at the start of each bullet point.

Output format:
{
  ""evaluation"": ""* Criterion heading - Yes, evidence: ... (or) No, reasoning: ...\n* Criterion heading - Yes, evidence: ... (or) No, reasoning: ..."",
  ""mark"": N,
  ""feedback"": ""Paragraph of feedback to the student (in the second person) including at least two strengths and a development area"",
  ""task"": ""Task for the student to add or rewrite a paragraph to reach a higher mark or elevate their response if it already achieved full marks"",
  ""spag"": ""Comment on spelling, punctuation, and grammar; say 'All correct' or identify all the errors and how to fix them""
}"
      };

      var total = spreadsheet.Responses.Count(o => o.Mark is null);
      var index = 0;

      for (var i = 0; i < spreadsheet.Responses.Count; i++)
      {
        var studentWork = spreadsheet.Responses[i];
        if (studentWork.Mark is not null) continue;

        await hubContext.Clients.Client(feedbackRequest.ConnectionId).Feedback(studentWork.Name, index++, total);

        var userPrompt = new ChatGPTMessage
        {
          Role = "user",
          Content = $"# Question\n\"\"\"\n{spreadsheet.Question}\n\"\"\"\n\n# Grading criteria:\n\"\"\"\n{spreadsheet.MarkScheme}\n\"\"\"\n\n" +
            $"# Student answer:\n\"\"\"\n{studentWork.Response}\n\"\"\""
        };
        var prompts = new List<ChatGPTMessage>() { systemPrompt, userPrompt };
        ChatGPTCompletion response = null;
        int promptTokens = ChatGPT.CountPromptTokens(prompts);
        int completionTokens = 0;
        var ticks = DateTime.UtcNow.Ticks;

        for (var attempt = 0; attempt < 2; attempt++)
        {
          try
          {
            if (attempt == 1 && fallbackChat is null) break;
            response = await (attempt == 0 ? chat : fallbackChat).SendGptRequestAsync(prompts, 0.0m, 0.0m, $"{ticks}");
            if (response.FinishReason != "prompt_filter" && response.FinishReason != "error")
            {
              completionTokens = ChatGPT.CountCompletionTokens(response.Content);
              prompts.Add(new() { Role = "assistant", Content = response.Content });
            }
            if (response.FinishReason == "stop") break;
          }
          finally
          {
            if (!env.IsDevelopment())
            {
              var chatRequest = new ChatRequest
              {
                Messages = prompts,
                Temperature = 0.0m,
                ConversationId = feedbackRequest.ConversationId,
                Model = "gpt-4",
                TemplateId = "feedback-spreadsheet"
              };
              await service.LogChatAsync(nameParts[0], chatRequest, ticks, promptTokens, completionTokens, GetFilterReason(response.FinishReason));
            }
          }
        }

        FeedbackResponse feedback;
        try
        {
          feedback = response.FinishReason == "stop"
            ? JsonSerializer.Deserialize<FeedbackResponse>(response.Content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            : new() { Mark = -1, Evaluation = response.FinishReason == "error" ? "Error: marking failed" : "Error: content filter triggered" };
        } catch (JsonException) {
          feedback = new() { Mark = -1, Evaluation = "Error: unable to parse output" };
        }

        await spreadsheet.WriteFeedbackAsync(i, feedback);
      }

      return Results.Ok();
    });

    group.MapPost("/admin", [Authorize(Roles = Roles.Admin)] async (AdminRequest adminRequest, HttpContext context) =>
    {
      var nameParts = context.User.Identity.Name.Split('@');
      var service = new TableService(nameParts[1]);
      var parts = adminRequest.Command.ToLowerInvariant().Split(' ');
      var command = parts[0];
      switch (command)
      {
        case "spend":
          if (parts.Length != 2 || !int.TryParse(parts[1], out var days)) return Results.Ok("Usage: spend {days}");
          var spend = await service.CalculateTotalSpendAsync(days);
          return Results.Ok($"Total spend for the past {days} {(days == 1 ? "day" : "days")}: {spend} credits");
        case "credit":
          if (parts.Length != 3 || !int.TryParse(parts[2], out var credits)) return Results.Ok("Usage: credit {username} {credits}");
          await service.AddCreditsAsync(parts[1], credits);
          return Results.Ok($"{credits} credits added to user {parts[1]}");
        case "recent":
          if (parts.Length != 2 || !int.TryParse(parts[1], out var n)) return Results.Ok("Usage: recent {n}");
          var recent = await service.GetRecentChatsAsync(n);
          if (recent.Count == 0) return Results.Ok("No conversations in the past 3 days.");
          var intro = recent.Count == n ? $"{n} most recent conversations" : $"All {recent.Count} conversations from the past 3 days";
          var output = $"{intro}:\n\n" + string.Join("\n\n",
            recent.Select(o => $"# {o.First().RowKey.Split('_')[0]} - {new DateTime(long.Parse(o.First().RowKey.Split('_')[1], CultureInfo.InvariantCulture)):ddd d MMM 'at' HH:mm} " +
            $"- {o.First().Template}\n\n{string.Join("\n\n", o.Select(c => $"**{o.First().RowKey.Split('_')[0]}**:\n\n{c.UserPrompt}\n\n**{c.Model}**:\n\n{c.Completion}"))}\n\n"));
          return Results.Ok(output);
        case "leaderboard":
          if (parts.Length > 1) return Results.Ok("Usage: leaderboard");
          var leaderboard = await service.GetLeaderboardAsync();
          return Results.Ok("# This week's leaderboard\n\n| User | Words | Chats | Credits |\n| :-- | :-: | :-: | :-: |\n" + string.Join('\n', leaderboard));
        default:
          return Results.Ok("Unknown command.");
      }
    });
  }

  private static string GetFilterReason(string finishReason) => finishReason switch
  {
    "prompt_filter" => "Harmful prompt",
    "content_filter" => "Harmful completion",
    _ => "OK"
  };
}

public static class AntiForgeryExtensions
{
  public static TBuilder ValidateAntiforgery<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
  {
    return builder.AddEndpointFilter(async (context, next) =>
    {
      try
      {
        var antiForgeryService = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
        await antiForgeryService.ValidateRequestAsync(context.HttpContext);
      }
      catch (AntiforgeryValidationException)
      {
        return Results.BadRequest("Antiforgery token validation failed.");
      }

      return await next(context);
    });
  }
}

public class ChatRequest {
  public IList<ChatGPTMessage> Messages { get; set; }
  public string Model { get; set; }
  public decimal Temperature { get; set; }
  public string TemplateId { get; set; }
  public string ConversationId { get; set; }
  public string ConnectionId { get; set; }
}

public class ChatResponse {
  public string Response { get; set; }
  public int RemainingCredits { get; set; }
  public bool Stop { get; set; }
  public string FinishReason { get; set; }
}

public class AdminRequest {
  public string Command { get; set; }
}

public class FeedbackRequest {
  public string Url { get; set; }
  public string ConversationId { get; set; }
  public string ConnectionId { get; set; }
}

public class FeedbackResponse {
  public string Evaluation { get; set; }
  public int Mark { get; set; }
  public string Feedback { get; set; }
  public string Task { get; set; }
  public string SPaG { get; set; }
}