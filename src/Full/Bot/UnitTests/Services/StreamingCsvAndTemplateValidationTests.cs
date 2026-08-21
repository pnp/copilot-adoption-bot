using Engine;
using Engine.Services.UserCache;

namespace UnitTests.Services;

/// <summary>
/// Tests for the streaming CSV parse path and card-template validation.
/// </summary>
[TestClass]
public class StreamingCsvAndTemplateValidationTests
{
    private const string Header =
        "Report Refresh Date,User Principal Name,Last Activity Date,Copilot Chat Last Activity Date," +
        "Microsoft Teams Copilot Last Activity Date,Word Copilot Last Activity Date";

    private static StringReader Reader(params string[] lines) =>
        new(string.Join("\r\n", lines));

    [TestMethod]
    public async Task ParseAsync_EmptyInput_ReturnsEmpty()
    {
        var records = await CopilotUsageCsvParser.ParseAsync(new StringReader(string.Empty));
        Assert.AreEqual(0, records.Count);
    }

    [TestMethod]
    public async Task ParseAsync_HeaderOnly_ReturnsEmpty()
    {
        var records = await CopilotUsageCsvParser.ParseAsync(Reader(Header));
        Assert.AreEqual(0, records.Count);
    }

    [TestMethod]
    public async Task ParseAsync_MissingUpnColumn_ReturnsEmpty()
    {
        var records = await CopilotUsageCsvParser.ParseAsync(Reader("A,B,C", "1,2,3"));
        Assert.AreEqual(0, records.Count);
    }

    [TestMethod]
    public async Task ParseAsync_ReadsRowsAndDates()
    {
        var records = await CopilotUsageCsvParser.ParseAsync(Reader(
            Header,
            "2026-06-01,alice@contoso.com,2026-05-30,2026-05-29,,2026-05-28",
            "2026-06-01,bob@contoso.com,,,,"));

        Assert.AreEqual(2, records.Count);

        Assert.AreEqual("alice@contoso.com", records[0].UserPrincipalName);
        Assert.AreEqual(new DateTime(2026, 5, 30), records[0].LastActivityDate);
        Assert.AreEqual(new DateTime(2026, 5, 29), records[0].CopilotChatLastActivityDate);
        Assert.IsNull(records[0].TeamsCopilotLastActivityDate);
        Assert.AreEqual(new DateTime(2026, 5, 28), records[0].WordCopilotLastActivityDate);

        Assert.AreEqual("bob@contoso.com", records[1].UserPrincipalName);
        Assert.IsNull(records[1].LastActivityDate);
    }

    [TestMethod]
    public async Task ParseAsync_SkipsBlankLinesAndRowsWithoutUpn()
    {
        var records = await CopilotUsageCsvParser.ParseAsync(Reader(
            Header,
            "",
            "2026-06-01,  ,2026-05-30,,,",   // blank UPN
            "2026-06-01,carol@contoso.com,2026-05-30,,,",
            ""));

        Assert.AreEqual(1, records.Count);
        Assert.AreEqual("carol@contoso.com", records[0].UserPrincipalName);
    }

    [TestMethod]
    public async Task ParseAsync_MatchesWholeStringParser()
    {
        // The streaming path must not diverge from the well-tested string path.
        var lines = new List<string> { Header };
        for (var i = 0; i < 500; i++)
        {
            lines.Add($"2026-06-01,user{i}@contoso.com,2026-05-{(i % 28) + 1:D2},,,");
        }
        var csv = string.Join("\r\n", lines);

        var fromString = CopilotUsageCsvParser.Parse(csv);
        var fromStream = await CopilotUsageCsvParser.ParseAsync(new StringReader(csv));

        Assert.AreEqual(fromString.Count, fromStream.Count);
        CollectionAssert.AreEqual(
            fromString.Select(r => r.UserPrincipalName).ToArray(),
            fromStream.Select(r => r.UserPrincipalName).ToArray());
        CollectionAssert.AreEqual(
            fromString.Select(r => r.LastActivityDate).ToArray(),
            fromStream.Select(r => r.LastActivityDate).ToArray());
    }

    [TestMethod]
    public void ValidateTemplatePayload_AcceptsNormalNudgeTemplate()
    {
        // Bundled nudge templates are 7-8 KB; these must keep working.
        var body = new string('a', 6_000);
        var json = $"{{\"type\":\"AdaptiveCard\",\"body\":[{{\"type\":\"TextBlock\",\"text\":\"{body}\"}}]}}";

        MessageTemplateStorageManager.ValidateTemplatePayload(json);
    }

    [TestMethod]
    public void ValidateTemplatePayload_RejectsOversizedTemplate()
    {
        // Simulates a card with an embedded base64 image - the bundled intro cards are ~94 KB
        // for exactly this reason, which breaches the 64 KB table property limit downstream.
        var big = "{\"type\":\"AdaptiveCard\",\"body\":[{\"type\":\"Image\",\"url\":\"data:image/png;base64,"
                  + new string('A', 90_000) + "\"}]}";

        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => MessageTemplateStorageManager.ValidateTemplatePayload(big));

        StringAssert.Contains(ex.Message, "base64");
    }

    [TestMethod]
    public void ValidateTemplatePayload_RejectsInvalidJson()
    {
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => MessageTemplateStorageManager.ValidateTemplatePayload("{ not json"));

        StringAssert.Contains(ex.Message, "not valid JSON");
    }

    [TestMethod]
    public void ValidateTemplatePayload_RejectsEmpty()
    {
        Assert.ThrowsException<ArgumentException>(
            () => MessageTemplateStorageManager.ValidateTemplatePayload("   "));
    }
}
