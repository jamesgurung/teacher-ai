using Microsoft.AspNetCore.SignalR;
using Microsoft.DeepDev;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TeacherAI;

public class ChatGPT(HttpClient client, string model, IHubClients<IChatClient> hub = null, string chatId = null)
{
  public static async Task CreateTokenizerAsync() {
    _tokenizer = await TokenizerBuilder.CreateByEncoderNameAsync("cl100k_base");
  }  

  private static ITokenizer _tokenizer;

  public async Task<ChatGPTCompletion> SendGptRequestStreamingAsync(IList<ChatGPTMessage> prompts, decimal temperature, decimal topP, string identifier) {
    var request = new ChatGPTRequest
    {
      User = identifier,
      Temperature = temperature,
      TopP = topP,
      Choices = 1,
      Stream = true,
      Messages = prompts
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
    while (!reader.EndOfStream)
    {
      var line = await reader.ReadLineAsync();
      if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ", StringComparison.Ordinal)) continue;
      if (line == "data: [DONE]") break;
      var chunk = JsonSerializer.Deserialize<ChatGPTResponseChunk>(line[6..]);
      if (chunk?.FinishReason is not null) finishReason = chunk.FinishReason;
      if (chunk?.Value is null) continue;
      content.Append(chunk.Value);
      if (hub is not null) await hub.Client(chatId).Type(chunk.Value);
    }
    return new() { Content = content.ToString(), FinishReason = finishReason };
  }

  public async Task<ChatGPTCompletion> SendGptRequestAsync(IList<ChatGPTMessage> prompts, decimal temperature, decimal topP, string identifier)
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

  private static readonly string[] _emptyArray = Array.Empty<string>();
  public static int CountPromptTokens(IList<ChatGPTMessage> prompts) => prompts.Sum(o => _tokenizer.Encode(o.Content, _emptyArray).Count + 5) + 3;
  public static int CountCompletionTokens(string completion) => _tokenizer.Encode(completion, _emptyArray).Count + 1;
}

public class ChatGPTRequest
{
  [JsonPropertyName("messages")]
  public IList<ChatGPTMessage> Messages { get; set; }
  [JsonPropertyName("user")]
  public string User { get; set; }
  [JsonPropertyName("temperature")]
  public decimal Temperature { get; set; } 
  [JsonPropertyName("top_p")]
  public decimal TopP { get; set; }
  [JsonPropertyName("n")]
  public decimal Choices { get; set; }
  [JsonPropertyName("stream")]
  public bool Stream { get; set; }
  [JsonPropertyName("model")]
  public string Model { get; set; }
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
  public string Value => Choices?[0].Delta?.Content;

  [JsonIgnore]
  public string FinishReason => Choices?[0].FinishReason;
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

public class ChatGPTCompletion {
  public string Content { get; set; }
  public string FinishReason { get; set; }
}