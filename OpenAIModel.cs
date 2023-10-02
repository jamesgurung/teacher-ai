namespace TeacherAI;

public class OpenAIModel
{
  public static Dictionary<string, OpenAIModel> Dictionary { get; set; }

  public string Name { get; init; }
  public decimal CostPerPromptToken { get; init; }
  public decimal CostPerCompletionToken { get; init; }
}