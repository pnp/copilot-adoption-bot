using Engine.Services.UserCache;

namespace UnitTests.Services;

/// <summary>
/// Pure unit tests for <see cref="CopilotUsageCsvParser"/> - no Azure / Graph dependencies.
/// </summary>
[TestClass]
public class CopilotUsageCsvParserTests
{
    private const string FullHeader =
        "User Principal Name," +
        "Last Activity Date," +
        "Copilot Chat Last Activity Date," +
        "Microsoft Teams Copilot Last Activity Date," +
        "Word Copilot Last Activity Date," +
        "Excel Copilot Last Activity Date," +
        "PowerPoint Copilot Last Activity Date," +
        "Outlook Copilot Last Activity Date," +
        "OneNote Copilot Last Activity Date," +
        "Loop Copilot Last Activity Date";

    [TestMethod]
    public void Parse_NullOrEmpty_ReturnsEmptyList()
    {
        Assert.AreEqual(0, CopilotUsageCsvParser.Parse(null).Count);
        Assert.AreEqual(0, CopilotUsageCsvParser.Parse(string.Empty).Count);
        Assert.AreEqual(0, CopilotUsageCsvParser.Parse("   ").Count);
    }

    [TestMethod]
    public void Parse_HeaderOnly_ReturnsEmptyList()
    {
        var result = CopilotUsageCsvParser.Parse(FullHeader);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void Parse_MissingUpnColumn_ReturnsEmptyList()
    {
        var csv = "Foo,Bar\nval1,val2";
        var result = CopilotUsageCsvParser.Parse(csv);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void Parse_SingleRow_ReturnsRecordWithUtcDates()
    {
        var csv = FullHeader + "\n" +
                  "user1@contoso.com,2024-01-15,2024-01-14,2024-01-13,2024-01-12,2024-01-11,2024-01-10,2024-01-09,2024-01-08,2024-01-07";

        var result = CopilotUsageCsvParser.Parse(csv);

        Assert.AreEqual(1, result.Count);
        var rec = result[0];
        Assert.AreEqual("user1@contoso.com", rec.UserPrincipalName);
        Assert.AreEqual(new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc), rec.LastActivityDate);
        Assert.AreEqual(DateTimeKind.Utc, rec.LastActivityDate!.Value.Kind);
        Assert.AreEqual(new DateTime(2024, 1, 14, 0, 0, 0, DateTimeKind.Utc), rec.CopilotChatLastActivityDate);
        Assert.AreEqual(new DateTime(2024, 1, 13, 0, 0, 0, DateTimeKind.Utc), rec.TeamsCopilotLastActivityDate);
        Assert.AreEqual(new DateTime(2024, 1, 12, 0, 0, 0, DateTimeKind.Utc), rec.WordCopilotLastActivityDate);
        Assert.AreEqual(new DateTime(2024, 1, 11, 0, 0, 0, DateTimeKind.Utc), rec.ExcelCopilotLastActivityDate);
        Assert.AreEqual(new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc), rec.PowerPointCopilotLastActivityDate);
        Assert.AreEqual(new DateTime(2024, 1, 9, 0, 0, 0, DateTimeKind.Utc), rec.OutlookCopilotLastActivityDate);
        Assert.AreEqual(new DateTime(2024, 1, 8, 0, 0, 0, DateTimeKind.Utc), rec.OneNoteCopilotLastActivityDate);
        Assert.AreEqual(new DateTime(2024, 1, 7, 0, 0, 0, DateTimeKind.Utc), rec.LoopCopilotLastActivityDate);
    }

    [TestMethod]
    public void Parse_RowWithBlankUpn_IsSkipped()
    {
        var csv = FullHeader + "\n" +
                  ",2024-01-15,,,,,,,,";
        var result = CopilotUsageCsvParser.Parse(csv);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void Parse_HandlesCrLfLineEndings()
    {
        var csv = FullHeader + "\r\n" + "u@x.com,2024-01-15,,,,,,,,";
        var result = CopilotUsageCsvParser.Parse(csv);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("u@x.com", result[0].UserPrincipalName);
    }

    [TestMethod]
    public void Parse_MissingDateValues_ReturnsNullDates()
    {
        var csv = FullHeader + "\n" + "u@x.com,,,,,,,,,";
        var result = CopilotUsageCsvParser.Parse(csv);
        Assert.AreEqual(1, result.Count);
        var r = result[0];
        Assert.IsNull(r.LastActivityDate);
        Assert.IsNull(r.CopilotChatLastActivityDate);
        Assert.IsNull(r.TeamsCopilotLastActivityDate);
        Assert.IsNull(r.WordCopilotLastActivityDate);
        Assert.IsNull(r.ExcelCopilotLastActivityDate);
        Assert.IsNull(r.PowerPointCopilotLastActivityDate);
        Assert.IsNull(r.OutlookCopilotLastActivityDate);
        Assert.IsNull(r.OneNoteCopilotLastActivityDate);
        Assert.IsNull(r.LoopCopilotLastActivityDate);
    }

    [TestMethod]
    public void Parse_InvalidDate_ReturnsNullForThatField()
    {
        var csv = FullHeader + "\n" + "u@x.com,not-a-date,2024-01-14,,,,,,,";
        var result = CopilotUsageCsvParser.Parse(csv);
        Assert.AreEqual(1, result.Count);
        Assert.IsNull(result[0].LastActivityDate);
        Assert.AreEqual(new DateTime(2024, 1, 14, 0, 0, 0, DateTimeKind.Utc), result[0].CopilotChatLastActivityDate);
    }

    [TestMethod]
    public void Parse_PartialHeader_StillReturnsRowsWithNullForMissingColumns()
    {
        var csv = "User Principal Name,Last Activity Date\n" + "u@x.com,2024-02-01";
        var result = CopilotUsageCsvParser.Parse(csv);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc), result[0].LastActivityDate);
        Assert.IsNull(result[0].CopilotChatLastActivityDate);
        Assert.IsNull(result[0].TeamsCopilotLastActivityDate);
    }

    [TestMethod]
    public void Parse_MultipleRows_ReturnsAll()
    {
        var csv = FullHeader + "\n" +
                  "a@x.com,2024-01-10,,,,,,,,\n" +
                  "b@x.com,2024-01-11,,,,,,,,\n" +
                  "c@x.com,2024-01-12,,,,,,,,";
        var result = CopilotUsageCsvParser.Parse(csv);
        Assert.AreEqual(3, result.Count);
        CollectionAssert.AreEqual(
            new[] { "a@x.com", "b@x.com", "c@x.com" },
            result.Select(r => r.UserPrincipalName).ToArray());
    }

    [TestMethod]
    public void Parse_TooFewColumnsForUpnRow_IsSkipped()
    {
        // Row has only one column even though header has 10
        var csv = FullHeader + "\n" + "single";
        var result = CopilotUsageCsvParser.Parse(csv);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("single", result[0].UserPrincipalName);
        Assert.IsNull(result[0].LastActivityDate);
    }

    [TestMethod]
    public void ParseHeader_NullHeader_ReturnsAllNegativeIndices()
    {
        var idx = CopilotUsageCsvParser.ParseHeader(null!);
        Assert.AreEqual(-1, idx.UpnIndex);
        Assert.AreEqual(-1, idx.LastActivityIndex);
        Assert.AreEqual(-1, idx.CopilotChatIndex);
        Assert.AreEqual(-1, idx.TeamsIndex);
        Assert.AreEqual(-1, idx.WordIndex);
        Assert.AreEqual(-1, idx.ExcelIndex);
        Assert.AreEqual(-1, idx.PowerPointIndex);
        Assert.AreEqual(-1, idx.OutlookIndex);
        Assert.AreEqual(-1, idx.OneNoteIndex);
        Assert.AreEqual(-1, idx.LoopIndex);
    }

    [TestMethod]
    public void ParseHeader_FullHeader_ReturnsExpectedIndices()
    {
        var idx = CopilotUsageCsvParser.ParseHeader(FullHeader);
        Assert.AreEqual(0, idx.UpnIndex);
        Assert.AreEqual(1, idx.LastActivityIndex);
        Assert.AreEqual(2, idx.CopilotChatIndex);
        Assert.AreEqual(3, idx.TeamsIndex);
        Assert.AreEqual(4, idx.WordIndex);
        Assert.AreEqual(5, idx.ExcelIndex);
        Assert.AreEqual(6, idx.PowerPointIndex);
        Assert.AreEqual(7, idx.OutlookIndex);
        Assert.AreEqual(8, idx.OneNoteIndex);
        Assert.AreEqual(9, idx.LoopIndex);
    }

    [TestMethod]
    public void ParseDate_OutOfRangeOrEmpty_ReturnsNull()
    {
        Assert.IsNull(CopilotUsageCsvParser.ParseDate(new string[] { "v" }, -1));
        Assert.IsNull(CopilotUsageCsvParser.ParseDate(new string[] { "v" }, 5));
        Assert.IsNull(CopilotUsageCsvParser.ParseDate(new string[] { "" }, 0));
        Assert.IsNull(CopilotUsageCsvParser.ParseDate(new string[] { "   " }, 0));
        Assert.IsNull(CopilotUsageCsvParser.ParseDate(new string[] { "garbage" }, 0));
    }

    [TestMethod]
    public void ParseDate_ValidValue_ReturnsUtcDate()
    {
        var d = CopilotUsageCsvParser.ParseDate(new[] { "2024-03-04" }, 0);
        Assert.IsNotNull(d);
        Assert.AreEqual(new DateTime(2024, 3, 4, 0, 0, 0, DateTimeKind.Utc), d);
        Assert.AreEqual(DateTimeKind.Utc, d!.Value.Kind);
    }
}
