using Microsoft.Graph;
using System.Text;
using System.Text.Json;

namespace TeacherAI;

public class Spreadsheet
{
  private readonly string _encodedUrl;
  private readonly GraphServiceClient _client;

  private IWorkbookWorksheetsCollectionRequestBuilder _worksheets;
  private string _sheetId;
  private Dictionary<string, int> _columnIndex;
  private string _firstColumnLetter;
  private string _lastColumnLetter;

  public IReadOnlyList<StudentResponse> Responses { get; private set; }
  public string Question { get; private set; }
  public string MarkScheme { get; private set; }

  public Spreadsheet(string shareUrl)
  {
    ArgumentNullException.ThrowIfNull(shareUrl);
    var base64Value = Convert.ToBase64String(Encoding.UTF8.GetBytes(shareUrl.Split('?')[0]));
    _encodedUrl = "u!" + base64Value.TrimEnd('=').Replace('/', '_').Replace('+', '-');
    var authProvider = new TokenAuthenticationProvider();
    _client = new GraphServiceClient(authProvider);
  }

  public async Task OpenAsync()
  {
    DriveItem sharedDriveItem;
    try
    {
      sharedDriveItem = await _client.Shares[_encodedUrl].DriveItem.Request().GetAsync();
    }
    catch (ServiceException)
    {
      throw new SpreadsheetSetupException("Unable to access spreadsheet at this URL.");
    }
    _worksheets = _client.Drives[sharedDriveItem.ParentReference.DriveId].Items[sharedDriveItem.Id].Workbook.Worksheets;
    _sheetId = (await _worksheets.Request().GetAsync()).First().Id;
    var range = await _worksheets[_sheetId].UsedRange(true).Request().GetAsync();
    var array = range.Values.RootElement.EnumerateArray()
      .Select(r => r.EnumerateArray().Select(c => c.ToString()).ToArray()).ToArray();

    var titles = new[] { "Name", "Response", "Mark", "Evaluation", "Feedback", "T Task", "SPaG" };
    _columnIndex = titles.ToDictionary(t => t, t => Array.IndexOf(array[0].Select(o => o.ToLowerInvariant()).ToArray(), t.ToLowerInvariant()));

    if (_columnIndex.Values.Any(o => o == -1))
      throw new SpreadsheetSetupException("One or more required columns could not be found.");

    var firstColumnIndex = _columnIndex.Values.Skip(2).Min();
    var lastColumnIndex = _columnIndex.Values.Skip(2).Max();
    if (lastColumnIndex - firstColumnIndex != 4)
      throw new SpreadsheetSetupException("The \"Mark\", \"Evaluation\", \"Feedback\", \"T Task\", and \"SPaG\" columns must be adjacent.");

    _firstColumnLetter = ColumnLetterFromIndex(firstColumnIndex);
    _lastColumnLetter = ColumnLetterFromIndex(lastColumnIndex);

    if (array.Length < 4)
      throw new SpreadsheetSetupException("There must be at least one student response.");

    if (!string.Equals(array[1][_columnIndex["Name"]], "question", StringComparison.OrdinalIgnoreCase))
      throw new SpreadsheetSetupException("The second row must contain the question.");

    if (!string.Equals(array[2][_columnIndex["Name"]], "mark scheme", StringComparison.OrdinalIgnoreCase))
      throw new SpreadsheetSetupException("The third row must contain the mark scheme.");

    Question = array[1][_columnIndex["Response"]];
    MarkScheme = array[2][_columnIndex["Response"]];

    if (string.IsNullOrWhiteSpace(Question))
      throw new SpreadsheetSetupException("The question cannot be empty.");

    if (string.IsNullOrWhiteSpace(MarkScheme))
      throw new SpreadsheetSetupException("The mark scheme cannot be empty.");

    Responses = array.Skip(3).Select(r => new StudentResponse
    {
      Name = r[_columnIndex["Name"]],
      Response = r[_columnIndex["Response"]],
      Mark = int.TryParse(r[_columnIndex["Mark"]], out var mark) ? mark : null,
      Evaluation = r[_columnIndex["Evaluation"]],
      Feedback = r[_columnIndex["Feedback"]],
      Task = r[_columnIndex["T Task"]],
      SPaG = r[_columnIndex["SPaG"]]
    }).ToList();

    if (!Responses.Any(o => o.Mark is null))
      throw new SpreadsheetSetupException("All questions are already marked.");
  }

  public Task WriteFeedbackAsync(int i, FeedbackResponse feedback)
  {
    ArgumentNullException.ThrowIfNull(feedback);
    var range = _worksheets[_sheetId].Range($"{_firstColumnLetter}{i + 4}:{_lastColumnLetter}{i + 4}");
    var row = new (object Value, int Order)[] {
      (feedback.Mark, _columnIndex["Mark"]),
      (feedback.Evaluation, _columnIndex["Evaluation"]),
      (feedback.Feedback, _columnIndex["Feedback"]),
      (feedback.Task, _columnIndex["T Task"]),
      (feedback.SPaG, _columnIndex["SPaG"])
    }.OrderBy(o => o.Order).Select(o => o.Value).ToArray();
    var json = JsonSerializer.Serialize(new object[] { row });
    var body = new WorkbookRange { Values = JsonDocument.Parse(json) };
    return range.Request().PatchAsync(body);
  }

  private static string ColumnLetterFromIndex(int index)
  {
    var sb = new StringBuilder();
    while (index >= 0)
    {
      sb.Insert(0, (char)('A' + (index % 26)));
      index /= 26;
      index--;
    }
    return sb.ToString();
  }
}

public class StudentResponse
{
  public string Name { get; init; }
  public string Response { get; init; }
  public int? Mark { get; set; }
  public string Evaluation { get; set; }
  public string Feedback { get; set; }
  public string Task { get; set; }
  public string SPaG { get; set; }
}

public class SpreadsheetSetupException : Exception
{
  public SpreadsheetSetupException() { }
  public SpreadsheetSetupException(string message) : base(message) { }
  public SpreadsheetSetupException(string message, Exception innerException) : base(message, innerException) { }
}