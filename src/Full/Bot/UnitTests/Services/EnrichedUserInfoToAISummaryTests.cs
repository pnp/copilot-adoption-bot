using Common.Engine.Models;

namespace UnitTests.Services;

/// <summary>
/// Pure unit tests for <see cref="EnrichedUserInfo.ToAISummary"/>. This string drives the
/// smart-group resolution prompt sent to Azure OpenAI, so its shape and contents matter.
/// </summary>
[TestClass]
public class EnrichedUserInfoToAISummaryTests
{
    [TestMethod]
    public void ToAISummary_MinimalUser_IncludesUpnAndUnknownName()
    {
        var user = new EnrichedUserInfo { Id = "1", UserPrincipalName = "a@contoso.com" };

        var summary = user.ToAISummary();

        StringAssert.Contains(summary, "UPN: a@contoso.com");
        StringAssert.Contains(summary, "Name: Unknown");
        StringAssert.Contains(summary, "Has Copilot License: No");
    }

    [TestMethod]
    public void ToAISummary_IncludesOnlyPopulatedProfileFields()
    {
        var user = new EnrichedUserInfo
        {
            Id = "1",
            UserPrincipalName = "a@contoso.com",
            DisplayName = "Alice",
            JobTitle = "Engineer",
            Department = "R&D"
            // intentionally leave Office/City/State/Country/Company/Manager/EmployeeType empty
        };

        var summary = user.ToAISummary();

        StringAssert.Contains(summary, "Job Title: Engineer");
        StringAssert.Contains(summary, "Department: R&D");
        Assert.IsFalse(summary.Contains("Office:"));
        Assert.IsFalse(summary.Contains("City:"));
        Assert.IsFalse(summary.Contains("Manager:"));
        Assert.IsFalse(summary.Contains("Employee Type:"));
    }

    [TestMethod]
    public void ToAISummary_WithCopilotLicense_ReportsYes()
    {
        var user = new EnrichedUserInfo
        {
            Id = "1",
            UserPrincipalName = "a@contoso.com",
            HasCopilotLicense = true
        };

        var summary = user.ToAISummary();

        StringAssert.Contains(summary, "Has Copilot License: Yes");
    }

    [TestMethod]
    public void ToAISummary_OmitsCopilotActivityBlockWhenNoDatesSet()
    {
        var user = new EnrichedUserInfo
        {
            Id = "1",
            UserPrincipalName = "a@contoso.com"
        };

        var summary = user.ToAISummary();

        Assert.IsFalse(summary.Contains("Copilot Activity:"),
            "Activity block should be omitted when no dates are populated.");
    }

    [TestMethod]
    public void ToAISummary_IncludesIso8601DatesForEachPopulatedCopilotActivity()
    {
        var user = new EnrichedUserInfo
        {
            Id = "1",
            UserPrincipalName = "a@contoso.com",
            CopilotLastActivityDate = new DateTime(2024, 5, 1),
            CopilotChatLastActivityDate = new DateTime(2024, 5, 2),
            TeamsCopilotLastActivityDate = new DateTime(2024, 5, 3),
            WordCopilotLastActivityDate = new DateTime(2024, 5, 4),
            ExcelCopilotLastActivityDate = new DateTime(2024, 5, 5),
            PowerPointCopilotLastActivityDate = new DateTime(2024, 5, 6),
            OutlookCopilotLastActivityDate = new DateTime(2024, 5, 7),
            OneNoteCopilotLastActivityDate = new DateTime(2024, 5, 8),
            LoopCopilotLastActivityDate = new DateTime(2024, 5, 9)
        };

        var summary = user.ToAISummary();

        StringAssert.Contains(summary, "Copilot Activity:");
        StringAssert.Contains(summary, "Overall: 2024-05-01");
        StringAssert.Contains(summary, "Chat: 2024-05-02");
        StringAssert.Contains(summary, "Teams: 2024-05-03");
        StringAssert.Contains(summary, "Word: 2024-05-04");
        StringAssert.Contains(summary, "Excel: 2024-05-05");
        StringAssert.Contains(summary, "PowerPoint: 2024-05-06");
        StringAssert.Contains(summary, "Outlook: 2024-05-07");
        StringAssert.Contains(summary, "OneNote: 2024-05-08");
        StringAssert.Contains(summary, "Loop: 2024-05-09");
    }

    [TestMethod]
    public void ToAISummary_SeparatesPartsWithPipe()
    {
        var user = new EnrichedUserInfo
        {
            Id = "1",
            UserPrincipalName = "a@contoso.com",
            DisplayName = "Alice"
        };

        var summary = user.ToAISummary();

        Assert.IsTrue(summary.Contains(" | "), "Parts should be pipe-separated for prompt readability.");
    }
}
