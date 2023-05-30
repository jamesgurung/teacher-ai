namespace TeacherAI;

public class OpenAIModel
{
  public static Dictionary<string, OpenAIModel> Dictionary { get; set; }

  public string Name { get; init; }
  public string Endpoint { get; init; }
  public string Key { get; init; }
  public decimal CostPerPromptToken { get; init; }
  public decimal CostPerCompletionToken { get; init; }
  public string Fallback { get; init; }
}