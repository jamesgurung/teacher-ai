using Azure;
using Azure.Data.Tables;
using System.Globalization;
using System.Text.Json.Serialization;

namespace OrgAI;

public static class TableService
{
  private static TableClient conversationsClient;
  private static TableClient spendClient;
  private static TableClient reviewClient;

  public static void Configure(string connectionString)
  {
    conversationsClient = new TableServiceClient(connectionString).GetTableClient("conversations");
    spendClient = new TableServiceClient(connectionString).GetTableClient("spend");
    reviewClient = new TableServiceClient(connectionString).GetTableClient("review");
  }

  public static async Task WarmUpAsync()
  {
    var nonExistentKey = "warmup";
    await conversationsClient.QueryAsync<TableEntity>(o => o.PartitionKey == nonExistentKey).ToListAsync();
    await spendClient.QueryAsync<TableEntity>(o => o.PartitionKey == nonExistentKey).ToListAsync();
    await reviewClient.QueryAsync<TableEntity>(o => o.PartitionKey == nonExistentKey).ToListAsync();
  }

  public static async Task UpsertConversationAsync(ConversationEntity conversation)
  {
    ArgumentNullException.ThrowIfNull(conversation);
    var table = conversationsClient;
    await table.UpsertEntityAsync(conversation, TableUpdateMode.Replace);
  }

  public static async Task<bool> ConversationExistsAsync(string userEmail, string conversationId)
  {
    ArgumentException.ThrowIfNullOrEmpty(userEmail);
    ArgumentException.ThrowIfNullOrEmpty(conversationId);
    var entity = await conversationsClient.GetEntityIfExistsAsync<ConversationEntity>(userEmail, conversationId, select: ["RowKey"]);
    return entity.HasValue;
  }

  public static async Task<ConversationEntity> GetConversationAsync(string userEmail, string conversationId)
  {
    ArgumentNullException.ThrowIfNull(userEmail);
    ArgumentNullException.ThrowIfNull(conversationId);
    var entity = await conversationsClient.GetEntityIfExistsAsync<ConversationEntity>(userEmail, conversationId);
    return !entity.HasValue || entity.Value.IsDeleted ? throw new InvalidOperationException("Conversation not found") : entity.Value;
  }

  public static async Task<List<ConversationEntity>> GetConversationsAsync(string userEmail, bool basicDataOnly)
  {
    ArgumentNullException.ThrowIfNull(userEmail);
    var query = conversationsClient.QueryAsync<ConversationEntity>(c => c.PartitionKey == userEmail && !c.IsDeleted,
      select: basicDataOnly ? ["RowKey", "Title", "Timestamp"] : null);
    var conversations = await query.ToListAsync();
    return conversations.OrderByDescending(o => o.Timestamp).ToList();
  }

  public static async Task<List<ReviewEntity>> GetReviewEntitiesAsync()
  {
    var query = reviewClient.QueryAsync<ReviewEntity>(select: ["RowKey", "User", "Title", "Timestamp"]);
    var reviewEntities = await query.ToListAsync();
    return reviewEntities.OrderByDescending(o => o.Timestamp).ToList();
  }

  public static async Task UpsertReviewEntityAsync(ConversationEntity conversation)
  {
    ArgumentNullException.ThrowIfNull(conversation);
    var reviewItem = new ReviewEntity
    {
      PartitionKey = nameof(ReviewEntity),
      RowKey = conversation.RowKey,
      User = conversation.PartitionKey,
      Title = conversation.Title
    };
    await reviewClient.UpsertEntityAsync(reviewItem, TableUpdateMode.Replace);
  }

  public static async Task DeleteReviewEntityAsync(string conversationId)
  {
    ArgumentNullException.ThrowIfNull(conversationId);
    await reviewClient.DeleteEntityAsync(nameof(ReviewEntity), conversationId);
  }

  public static async Task DeleteConversationAsync(string userEmail, string conversationId)
  {
    ArgumentNullException.ThrowIfNull(userEmail);
    ArgumentNullException.ThrowIfNull(conversationId);
    await conversationsClient.DeleteEntityAsync(userEmail, conversationId);
  }

  public static async Task<decimal> RecordSpendAsync(string userEmail, decimal amount)
  {
    ArgumentNullException.ThrowIfNull(userEmail);
    var weekStart = GetCurrentWeekStart();
    var userSpend = await spendClient.GetEntityIfExistsAsync<SpendEntity>(weekStart, userEmail);
    if (userSpend.HasValue)
    {
      var existingSpend = decimal.Parse(userSpend.Value.Spent, CultureInfo.InvariantCulture);
      var newSpend = existingSpend + amount;
      userSpend.Value.Spent = newSpend.ToString(CultureInfo.InvariantCulture);
      await spendClient.UpdateEntityAsync(userSpend.Value, userSpend.Value.ETag);
      return newSpend;
    }
    else
    {
      var newSpend = new SpendEntity { PartitionKey = weekStart, RowKey = userEmail, Spent = amount.ToString(CultureInfo.InvariantCulture) };
      await spendClient.AddEntityAsync(newSpend);
      return amount;
    }
  }

  public static async Task<decimal> GetSpendAsync(string userEmail)
  {
    ArgumentNullException.ThrowIfNull(userEmail);
    var weekStart = GetCurrentWeekStart();
    var userSpend = await spendClient.GetEntityIfExistsAsync<SpendEntity>(weekStart, userEmail);
    return userSpend.HasValue ? decimal.Parse(userSpend.Value.Spent, CultureInfo.InvariantCulture) : 0m;
  }

  private static string GetCurrentWeekStart()
  {
    var now = DateTime.UtcNow;
    var startOfWeek = now.AddDays(-(int)now.DayOfWeek);
    return startOfWeek.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
  }
}

public class ConversationEntity : ITableEntity
{
  [JsonIgnore]
  public string PartitionKey { get; set; }
  [JsonPropertyName("id")]
  public string RowKey { get; set; }
  [JsonIgnore]
  public DateTimeOffset? Timestamp { get; set; }
  [JsonIgnore]
  public ETag ETag { get; set; }

  [JsonPropertyName("title")]
  public string Title { get; set; }
  [JsonIgnore]
  public string Cost { get; set; }
  [JsonIgnore]
  public bool IsDeleted { get; set; }

  public ConversationEntity() { }

  public ConversationEntity(string userEmail, string conversationId, string title, decimal cost)
  {
    PartitionKey = userEmail;
    RowKey = conversationId;
    Title = title;
    Cost = cost.ToString(CultureInfo.InvariantCulture);
  }
}

public class SpendEntity : ITableEntity
{
  public string PartitionKey { get; set; }
  public string RowKey { get; set; }
  public DateTimeOffset? Timestamp { get; set; }
  public ETag ETag { get; set; }

  public string Spent { get; set; }
}

public class ReviewEntity : ITableEntity
{
  [JsonIgnore]
  public string PartitionKey { get; set; }
  [JsonPropertyName("id")]
  public string RowKey { get; set; }
  [JsonIgnore]
  public DateTimeOffset? Timestamp { get; set; }
  [JsonIgnore]
  public ETag ETag { get; set; }

  [JsonPropertyName("user")]
  public string User { get; set; }
  [JsonPropertyName("title")]
  public string Title { get; set; }
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