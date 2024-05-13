using Azure;
using Azure.Data.Tables;
using System.Globalization;
using System.Text.Json;

namespace TeacherAI;

public class TableService(string domain)
{
  public static void Configure(string connectionString)
  {
    client = new TableServiceClient(connectionString);
  }

  private static TableServiceClient client;

  private static readonly JsonSerializerOptions _jsonOptions = new() {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true
  };

  public async Task LogChatAsync(string username, ChatRequest chatRequest, long ticks, int promptTokens, int completionTokens, string contentFilter)
  {
    ArgumentNullException.ThrowIfNull(chatRequest);
    var table = client.GetTableClient("chatlog");
    var entry = new ChatLog {
      PartitionKey = domain,
      RowKey = $"{username}_{ticks}",
      Chat = JsonSerializer.Serialize(chatRequest.Messages, _jsonOptions),
      Template = chatRequest.TemplateId,
      Model = chatRequest.Model,
      Temperature = chatRequest.Temperature,
      UserPrompt = chatRequest.Messages.LastOrDefault(o => o.Role == "user")?.Content,
      Completion = chatRequest.Messages.Last().Role == "assistant" ? chatRequest.Messages.Last().Content : null,
      PromptTokens = promptTokens,
      CompletionTokens = completionTokens,
      ConversationId = chatRequest.ConversationId,
      ContentFilter = contentFilter
    };
    await table.AddEntityAsync(entry);
  }

  public async Task<decimal> CalculateUsageAsync(string username) {
    var table = client.GetTableClient("chatlog");
    var lastMonday = DateTime.UtcNow.Date.AddDays(-(((int)DateTime.UtcNow.DayOfWeek + 6) % 7));
    var start = $"{username}_{lastMonday.Ticks}";
    var end = $"{username}_{DateTime.UtcNow.AddDays(1).Ticks}";
    var results = await table.QueryAsync<ChatLog>(
      filter: o => o.PartitionKey == domain && o.RowKey.CompareTo(start) >= 0 && o.RowKey.CompareTo(end) < 0,
      select: [nameof(ChatLog.PromptTokens), nameof(ChatLog.CompletionTokens), nameof(ChatLog.Model)]
    ).ToListAsync();
    return results
      .Select(o => new { Chat = o, Model = OpenAIModel.Dictionary[o.Model] })
      .Sum(o => o.Chat.PromptTokens * o.Model.CostPerPromptToken + (o.Chat.CompletionTokens ?? 0) * o.Model.CostPerCompletionToken);
  }

  public async Task<decimal> CalculateTotalSpendAsync(int days)
  {
    var table = client.GetTableClient("chatlog");
    var start = DateTime.UtcNow.AddDays(-days);
    var results = await table.QueryAsync<ChatLog>(
      filter: o => o.PartitionKey == domain && o.Timestamp >= start && o.Model != "credits",
      select: [nameof(ChatLog.RowKey), nameof(ChatLog.PromptTokens), nameof(ChatLog.CompletionTokens), nameof(ChatLog.Model)]
    ).ToListAsync();
    return results
      .Select(o => new { Chat = o, Ticks = long.Parse(o.RowKey.Split('_')[1], CultureInfo.InvariantCulture), Model = OpenAIModel.Dictionary[o.Model] })
      .Where(o => o.Ticks >= start.Ticks)
      .Sum(o => o.Chat.PromptTokens * o.Model.CostPerPromptToken + (o.Chat.CompletionTokens ?? 0) * o.Model.CostPerCompletionToken);
  }

  public async Task AddCreditsAsync(string username, int credits) {
    var table = client.GetTableClient("chatlog");
    var entry = new ChatLog
    {
      PartitionKey = domain,
      RowKey = $"{username}_{DateTime.UtcNow.Ticks}",
      Model = "credits",
      PromptTokens = credits
    };
    await table.AddEntityAsync(entry);
  }

  public async Task<List<List<ChatLog>>> GetRecentChatsAsync(int n)
  {
    var table = client.GetTableClient("chatlog");
    var start = DateTime.UtcNow.AddDays(-3);
    var results = await table.QueryAsync<ChatLog>(
      filter: o => o.PartitionKey == domain && o.Timestamp >= start && o.Model != "credits"
    ).ToListAsync();
    return results.Select(o => new { Chat = o, Ticks = long.Parse(o.RowKey.Split('_')[1], CultureInfo.InvariantCulture) }).Where(o => o.Ticks >= start.Ticks)
      .OrderByDescending(o => o.Ticks)
      .GroupBy(o => o.Chat.ConversationId).Select(o => o.Reverse().Select(o => o.Chat).ToList()).Take(n).ToList();
  }

  public async Task<List<string>> GetLeaderboardAsync()
  {
    var table = client.GetTableClient("chatlog");
    var lastMonday = DateTime.UtcNow.Date.AddDays(-(((int)DateTime.UtcNow.DayOfWeek + 6) % 7));
    var items = await table.QueryAsync<ChatLog>(
      filter: o => o.PartitionKey == domain && o.Timestamp >= lastMonday && o.Model != "credits"
    ).ToListAsync();
    var results = items.Select(o => new {
        Chat = o,
        Username = o.RowKey.Split('_')[0],
        Ticks = long.Parse(o.RowKey.Split('_')[1], CultureInfo.InvariantCulture),
        Model = OpenAIModel.Dictionary[o.Model]
      })
      .Where(o => o.Ticks >= lastMonday.Ticks)
      .GroupBy(o => o.Username).Select(o => new {
        User = o.Key,
        Words = o.Sum(c => CountWords(c.Chat.Completion)),
        Chats = o.DistinctBy(o => o.Chat.ConversationId).Count(),
        Credits = o.Sum(c => c.Chat.PromptTokens * c.Model.CostPerPromptToken + (c.Chat.CompletionTokens ?? 0) * c.Model.CostPerCompletionToken)
       })
      .OrderByDescending(o => o.Words).ToList();
    var totals = $"| *Total* | *{results.Sum(o => o.Words)}* | *{results.Sum(o => o.Chats)}* | *{Math.Round(results.Sum(o => o.Credits), 0, MidpointRounding.AwayFromZero)}* |";
    return results.Select(o => $"| {o.User} | {o.Words} | {o.Chats} | {Math.Round(o.Credits, 0, MidpointRounding.AwayFromZero)} |").Append(totals).ToList();
  }

  private static readonly char[] separators = [' ', ',', '.', ';', ':', '-', '\n', '\r', '\t'];
  private static int CountWords(string text) => text?.Split(separators, StringSplitOptions.RemoveEmptyEntries).Length ?? 0;
}

public class ChatLog : ITableEntity
{
  public string PartitionKey { get; set; } // Domain
  public string RowKey { get; set; }       // Username_Ticks
  public DateTimeOffset? Timestamp { get; set; }
  public ETag ETag { get; set; }

  public string Chat { get; set; }
  public string Template { get; set; }
  public string Model { get; set; }
  public decimal? Temperature { get; set; }
  public string UserPrompt { get; set; }
  public string Completion { get; set; }
  public int PromptTokens { get; set; }
  public int? CompletionTokens { get; set; }
  public string ConversationId { get; set; }
  public string ContentFilter { get; set; }
}

public static class QueryExtensions
{
  public static async Task<List<T>> ToListAsync<T>(this AsyncPageable<T> query)
  {
    ArgumentNullException.ThrowIfNull(query);
    var list = new List<T>();
    await foreach (var item in query)
    {
      list.Add(item);
    }
    return list;
  }
}