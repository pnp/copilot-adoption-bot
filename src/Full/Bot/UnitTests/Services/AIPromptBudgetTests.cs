using Engine.Services;

namespace UnitTests.Services;

/// <summary>
/// Pure unit tests for the AI prompt budgeting rules - no AI Foundry endpoint required.
/// </summary>
[TestClass]
public class AIPromptBudgetTests
{
    private const string SampleCard = """
    {
      "type": "AdaptiveCard",
      "version": "1.5",
      "body": [
        { "type": "TextBlock", "text": "Try Copilot in Word", "size": "Large" },
        { "type": "Image", "url": "data:image/png;base64,AAAABBBBCCCCDDDD" },
        { "type": "TextBlock", "text": "Summarise a long document in one click." }
      ],
      "actions": [ { "type": "Action.OpenUrl", "title": "Learn more", "url": "https://example.com" } ]
    }
    """;

    [TestMethod]
    public void SummariseCard_NullOrEmpty_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, AIPromptBudget.SummariseCard(null, 100));
        Assert.AreEqual(string.Empty, AIPromptBudget.SummariseCard("   ", 100));
    }

    [TestMethod]
    public void SummariseCard_ExtractsVisibleText()
    {
        var summary = AIPromptBudget.SummariseCard(SampleCard, 1000);

        StringAssert.Contains(summary, "Try Copilot in Word");
        StringAssert.Contains(summary, "Summarise a long document");
        StringAssert.Contains(summary, "Learn more", "Action titles are visible copy too");
    }

    [TestMethod]
    public void SummariseCard_ExcludesImageDataAndUrls()
    {
        var summary = AIPromptBudget.SummariseCard(SampleCard, 1000);

        // Embedded base64 images are the reason intro cards reach ~94 KB; they must never
        // reach the prompt.
        Assert.IsFalse(summary.Contains("base64"), "Image payloads must not be included");
        Assert.IsFalse(summary.Contains("AAAABBBB"), "Image payloads must not be included");
        Assert.IsFalse(summary.Contains("https://example.com"), "URLs are not prompt context");
    }

    [TestMethod]
    public void SummariseCard_RespectsCharacterBudget()
    {
        var summary = AIPromptBudget.SummariseCard(SampleCard, 10);
        Assert.IsTrue(summary.Length <= 10);
    }

    [TestMethod]
    public void SummariseCard_LargeCard_IsBounded()
    {
        // Simulates an intro card with a big embedded image.
        var big = "{\"type\":\"AdaptiveCard\",\"body\":[{\"type\":\"Image\",\"url\":\"data:image/png;base64,"
                  + new string('A', 90_000) + "\"}]}";

        var summary = AIPromptBudget.SummariseCard(big, 1000);

        Assert.IsTrue(summary.Length <= 1000, $"Expected <= 1000 chars, got {summary.Length}");
    }

    [TestMethod]
    public void SummariseCard_InvalidJson_FallsBackButStillBounded()
    {
        var summary = AIPromptBudget.SummariseCard(new string('x', 5000), 100);
        Assert.AreEqual(100, summary.Length);
    }

    [TestMethod]
    public void TrimHistory_NullOrEmpty_ReturnsEmpty()
    {
        Assert.AreEqual(0, AIPromptBudget.TrimHistory(null, 100).Count);
        Assert.AreEqual(0, AIPromptBudget.TrimHistory(Array.Empty<(string, string)>(), 100).Count);
    }

    [TestMethod]
    public void TrimHistory_WithinBudget_KeepsEverythingInOrder()
    {
        var history = new[]
        {
            ("user", "hello"),
            ("assistant", "hi there"),
            ("user", "thanks")
        };

        var trimmed = AIPromptBudget.TrimHistory(history, 1000);

        CollectionAssert.AreEqual(
            history.Select(h => h.Item2).ToArray(),
            trimmed.Select(t => t.message).ToArray());
    }

    [TestMethod]
    public void TrimHistory_OverBudget_KeepsMostRecentAndPreservesOrder()
    {
        var history = new[]
        {
            ("user", new string('a', 100)),
            ("assistant", new string('b', 100)),
            ("user", new string('c', 100))
        };

        // Budget only fits the last two entries.
        var trimmed = AIPromptBudget.TrimHistory(history, 200);

        Assert.AreEqual(2, trimmed.Count);
        Assert.AreEqual(new string('b', 100), trimmed[0].message, "Oldest kept entry first");
        Assert.AreEqual(new string('c', 100), trimmed[1].message, "Newest entry last");
    }

    [TestMethod]
    public void TrimHistory_SingleEntryLargerThanBudget_ReturnsEmpty()
    {
        var history = new[] { ("user", new string('a', 5000)) };

        var trimmed = AIPromptBudget.TrimHistory(history, 100);

        Assert.AreEqual(0, trimmed.Count);
    }

    [TestMethod]
    public void TrimHistory_TwentyLongTurns_IsBoundedBySizeNotCount()
    {
        // The dialog caps history at 20 entries; 20 long turns is still a large prompt, so
        // the budget must be applied by size as well.
        var history = Enumerable.Range(0, 20)
            .Select(i => (i % 2 == 0 ? "user" : "assistant", new string('x', 800)))
            .ToArray();

        var trimmed = AIPromptBudget.TrimHistory(history, AIFoundryServiceBudgets.MaxHistoryChars);

        Assert.IsTrue(trimmed.Sum(t => t.message.Length) <= AIFoundryServiceBudgets.MaxHistoryChars);
        Assert.IsTrue(trimmed.Count < 20, "A size budget must drop some of the 20 entries");
    }
}

/// <summary>
/// Mirrors the budget constants so tests don't depend on internals visibility.
/// </summary>
internal static class AIFoundryServiceBudgets
{
    public const int MaxHistoryChars = 4000;
}
