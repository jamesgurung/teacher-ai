namespace TeacherAI;

public class Organisation
{
  public static Organisation Instance { get; set; }

  public string Name { get; init; }
  public string Domain { get; init; }
  public string AppWebsite { get; init; }
  public string AdminName { get; set; }
  public string AdminEmail { get; set; }
  public string ServiceAccountEmail { get; set; }
  public int UserCreditsPerWeek { get; init; }
}