namespace OrgAI;

public class Organisation
{
  public static Organisation Instance { get; set; }

  public string Name { get; init; }
  public string AppWebsite { get; init; }
  public decimal UserMaxWeeklySpend { get; init; }
  public IList<string> Reviewers { get; init; }
  public string CountryCode { get; init; }
  public string City { get; init; }
  public string Timezone { get; init; }
}