using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;

namespace TeacherAI;

public class ChatGPT(HttpClient client, string model, IHubClients<IChatClient> hub = null, string chatId = null)
{
  public async Task<ChatGPTCompletion> SendGptRequestStreamingAsync(IList<ChatGPTMessage> prompts, double temperature, double topP, string identifier)
  {
    var request = new ChatGPTRequest
    {
      User = identifier,
      Temperature = temperature,
      TopP = topP,
      Choices = 1,
      Stream = true,
      StreamOptions = new() { IncludeUsage = true },
      Messages = prompts,
      Model = model
    };

    using var body = JsonContent.Create(request);
    using var message = new HttpRequestMessage(HttpMethod.Post, string.Empty) { Content = body };
    using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead);
    if (response.StatusCode == HttpStatusCode.BadRequest) return new() { Content = "Request rejected.", FinishReason = "prompt_filter" };
    if (!response.IsSuccessStatusCode) return new() { Content = "Request failed.", FinishReason = "error" };
    using var stream = await response.Content.ReadAsStreamAsync();
    using var reader = new StreamReader(stream);
    var content = new StringBuilder();
    string finishReason = null;
    var promptTokens = 0;
    var completionTokens = 0;
    while (!reader.EndOfStream)
    {
      var line = await reader.ReadLineAsync();
      if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ", StringComparison.Ordinal)) continue;
      if (line == "data: [DONE]") break;
      var chunk = JsonSerializer.Deserialize<ChatGPTResponseChunk>(line[6..]);
      if (chunk?.Usage is not null)
      {
        promptTokens = chunk.Usage.PromptTokens;
        completionTokens = chunk.Usage.CompletionTokens;
      }
      if (chunk?.FinishReason is not null) finishReason = chunk.FinishReason;
      if (chunk?.Value is null) continue;
      content.Append(chunk.Value);
      if (hub is not null) await hub.Client(chatId).Type(chunk.Value);
    }
    return new() { Content = content.ToString(), FinishReason = finishReason, PromptTokens = promptTokens, CompletionTokens = completionTokens };
  }

  public async Task<ChatGPTCompletion> SendGptRequestAsync(IList<ChatGPTMessage> prompts, double temperature, double topP, string identifier)
  {
    var request = new ChatGPTRequest
    {
      User = identifier,
      Temperature = temperature,
      TopP = topP,
      Choices = 1,
      Messages = prompts,
      Model = model
    };

    using var body = JsonContent.Create(request);
    using var response = await client.PostAsync(string.Empty, body);
    if (response.StatusCode == HttpStatusCode.BadRequest) return new() { Content = "Request rejected.", FinishReason = "prompt_filter" };
    if (!response.IsSuccessStatusCode) return new() { Content = "Request failed.", FinishReason = "error" };
    var data = JsonSerializer.Deserialize<ChatGPTResponse>(await response.Content.ReadAsStringAsync());
    return new() { Content = data.Value, FinishReason = data.FinishReason };
  }
}

public class ChatGPTRequest
{
  [JsonPropertyName("messages")]
  public IList<ChatGPTMessage> Messages { get; set; }
  [JsonPropertyName("user")]
  public string User { get; set; }
  [JsonPropertyName("temperature")]
  public double Temperature { get; set; }
  [JsonPropertyName("top_p")]
  public double TopP { get; set; }
  [JsonPropertyName("n")]
  public double Choices { get; set; }
  [JsonPropertyName("stream")]
  public bool Stream { get; set; }
  [JsonPropertyName("model")]
  public string Model { get; set; }
  [JsonPropertyName("stream_options")]
  public StreamOptions StreamOptions { get; set; }
}

public class StreamOptions
{
  [JsonPropertyName("include_usage")]
  public bool IncludeUsage { get; set; }
}

public class ChatGPTMessage
{
  [JsonPropertyName("role")]
  public string Role { get; set; }
  [JsonPropertyName("content")]
  public string Content { get; set; }
}

public class ChatGPTResponse
{
  [JsonPropertyName("choices")]
  public IList<ChatGPTResponseChoice> Choices { get; set; }

  [JsonIgnore]
  public string Value => Choices?[0].Message?.Content;

  [JsonIgnore]
  public string FinishReason => Choices?[0].FinishReason;
}

public class ChatGPTResponseChoice
{
  [JsonPropertyName("message")]
  public ChatGPTResponseMessage Message { get; set; }

  [JsonPropertyName("finish_reason")]
  public string FinishReason { get; set; }
}

public class ChatGPTResponseChunk
{
  [JsonPropertyName("choices")]
  public IList<ChatGPTResponseChunkChoice> Choices { get; set; }

  [JsonIgnore]
  public string Value => Choices is null || Choices.Count == 0 ? null : Choices[0].Delta?.Content;

  [JsonIgnore]
  public string FinishReason => Choices is null || Choices.Count == 0 ? null : Choices[0].FinishReason;

  [JsonPropertyName("usage")]
  public UsageData Usage { get; set; }
}

public class UsageData
{
  [JsonPropertyName("prompt_tokens")]
  public int PromptTokens { get; set; }
  [JsonPropertyName("completion_tokens")]
  public int CompletionTokens { get; set; }
}

public class ChatGPTResponseChunkChoice
{
  [JsonPropertyName("delta")]
  public ChatGPTResponseMessage Delta { get; set; }

  [JsonPropertyName("finish_reason")]
  public string FinishReason { get; set; }
}

public class ChatGPTResponseMessage
{
  [JsonPropertyName("content")]
  public string Content { get; set; }
}

public class ChatGPTCompletion
{
  public string Content { get; set; }
  public string FinishReason { get; set; }
  public int PromptTokens { get; set; }
  public int CompletionTokens { get; set; }
}