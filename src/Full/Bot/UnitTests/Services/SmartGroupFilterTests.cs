using System.Text.Json;
using Engine.Models;
using Engine.Services;

namespace UnitTests.Services;

/// <summary>
/// Pure unit tests for the structured smart-group filter that replaced per-user AI
/// classification. No AI Foundry endpoint required - which is the point: membership is now
/// deterministic and testable rather than being whatever the model decided that run.
/// </summary>
[TestClass]
public class SmartGroupFilterTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    private static EnrichedUserInfo User(
        string upn = "a@contoso.com",
        string? department = null,
        string? country = null,
        string? jobTitle = null,
        bool hasLicence = false,
        DateTime? copilotLastActivity = null) => new()
        {
            Id = upn,
            UserPrincipalName = upn,
            DisplayName = upn,
            Department = department,
            Country = country,
            JobTitle = jobTitle,
            HasCopilotLicense = hasLicence,
            CopilotLastActivityDate = copilotLastActivity
        };

    private static SmartGroupFilter Parse(string json)
    {
        var filter = SmartGroupFilter.TryParse(json, out var error);
        Assert.IsNotNull(filter, $"Expected a valid filter, got error: {error}");
        return filter!;
    }

    [TestMethod]
    public void TryParse_NullOrEmpty_Fails()
    {
        Assert.IsNull(SmartGroupFilter.TryParse(null, out _));
        Assert.IsNull(SmartGroupFilter.TryParse("   ", out _));
    }

    [TestMethod]
    public void TryParse_InvalidJson_FailsWithoutThrowing()
    {
        var filter = SmartGroupFilter.TryParse("not json at all", out var error);
        Assert.IsNull(filter);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public void TryParse_NoConditions_Fails()
    {
        // A filter with no conditions would match the entire tenant - never accept it.
        Assert.IsNull(SmartGroupFilter.TryParse("""{"all":[]}""", out var error));
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public void TryParse_UnknownField_Rejected()
    {
        var filter = SmartGroupFilter.TryParse("""{"all":[{"field":"Salary","op":"eq","value":"100"}]}""", out var error);
        Assert.IsNull(filter);
        StringAssert.Contains(error!, "Salary");
    }

    [TestMethod]
    public void TryParse_UnknownOperator_Rejected()
    {
        var filter = SmartGroupFilter.TryParse("""{"all":[{"field":"Department","op":"regex","value":"x"}]}""", out var error);
        Assert.IsNull(filter);
        StringAssert.Contains(error!, "regex");
    }

    [TestMethod]
    public void TryParse_StripsMarkdownCodeFence()
    {
        var fenced = "```json\n{\"all\":[{\"field\":\"Department\",\"op\":\"eq\",\"value\":\"Finance\"}]}\n```";
        var filter = Parse(fenced);

        Assert.IsTrue(filter.Matches(User(department: "Finance"), Now));
    }

    [TestMethod]
    public void Eq_IsCaseInsensitive()
    {
        var filter = Parse("""{"all":[{"field":"Department","op":"eq","value":"finance"}]}""");

        Assert.IsTrue(filter.Matches(User(department: "Finance"), Now));
        Assert.IsFalse(filter.Matches(User(department: "Legal"), Now));
    }

    [TestMethod]
    public void Neq_ExcludesMatches()
    {
        var filter = Parse("""{"all":[{"field":"Department","op":"neq","value":"Finance"}]}""");

        Assert.IsFalse(filter.Matches(User(department: "Finance"), Now));
        Assert.IsTrue(filter.Matches(User(department: "Legal"), Now));
    }

    [TestMethod]
    public void Contains_And_StartsWith()
    {
        var contains = Parse("""{"all":[{"field":"JobTitle","op":"contains","value":"engineer"}]}""");
        Assert.IsTrue(contains.Matches(User(jobTitle: "Senior Engineer"), Now));
        Assert.IsFalse(contains.Matches(User(jobTitle: "Accountant"), Now));

        var startsWith = Parse("""{"all":[{"field":"JobTitle","op":"startsWith","value":"Senior"}]}""");
        Assert.IsTrue(startsWith.Matches(User(jobTitle: "Senior Engineer"), Now));
        Assert.IsFalse(startsWith.Matches(User(jobTitle: "Junior Engineer"), Now));
    }

    [TestMethod]
    public void In_MatchesAnyOfList()
    {
        var filter = Parse("""{"all":[{"field":"Country","op":"in","value":["UK","Ireland"]}]}""");

        Assert.IsTrue(filter.Matches(User(country: "UK"), Now));
        Assert.IsTrue(filter.Matches(User(country: "ireland"), Now));
        Assert.IsFalse(filter.Matches(User(country: "France"), Now));
    }

    [TestMethod]
    public void BooleanField_MatchesTrueAndFalse()
    {
        var licensed = Parse("""{"all":[{"field":"HasCopilotLicense","op":"eq","value":true}]}""");
        Assert.IsTrue(licensed.Matches(User(hasLicence: true), Now));
        Assert.IsFalse(licensed.Matches(User(hasLicence: false), Now));

        var unlicensed = Parse("""{"all":[{"field":"HasCopilotLicense","op":"eq","value":false}]}""");
        Assert.IsTrue(unlicensed.Matches(User(hasLicence: false), Now));
    }

    [TestMethod]
    public void IsNull_And_IsNotNull()
    {
        var never = Parse("""{"all":[{"field":"CopilotLastActivityDate","op":"isNull"}]}""");
        Assert.IsTrue(never.Matches(User(), Now));
        Assert.IsFalse(never.Matches(User(copilotLastActivity: Now.AddDays(-1)), Now));

        var ever = Parse("""{"all":[{"field":"CopilotLastActivityDate","op":"isNotNull"}]}""");
        Assert.IsTrue(ever.Matches(User(copilotLastActivity: Now.AddDays(-1)), Now));
    }

    [TestMethod]
    public void OlderThanDays_IncludesUsersWithNoActivityAtAll()
    {
        // "Hasn't used Copilot in 30 days" must include people who have never used it - they are
        // the primary audience for an adoption nudge. Treating null as "no match" would silently
        // exclude exactly the users the campaign targets.
        var filter = Parse("""{"all":[{"field":"CopilotLastActivityDate","op":"olderThanDays","value":30}]}""");

        Assert.IsTrue(filter.Matches(User(), Now), "Never-active users must match");
        Assert.IsTrue(filter.Matches(User(copilotLastActivity: Now.AddDays(-31)), Now));
        Assert.IsFalse(filter.Matches(User(copilotLastActivity: Now.AddDays(-2)), Now));
    }

    [TestMethod]
    public void WithinLastDays_ExcludesNeverActive()
    {
        var filter = Parse("""{"all":[{"field":"CopilotLastActivityDate","op":"withinLastDays","value":7}]}""");

        Assert.IsTrue(filter.Matches(User(copilotLastActivity: Now.AddDays(-1)), Now));
        Assert.IsFalse(filter.Matches(User(copilotLastActivity: Now.AddDays(-30)), Now));
        Assert.IsFalse(filter.Matches(User(), Now), "Never-active users are not recently active");
    }

    [TestMethod]
    public void All_RequiresEveryCondition()
    {
        var filter = Parse("""
        {"all":[
          {"field":"Department","op":"eq","value":"Finance"},
          {"field":"HasCopilotLicense","op":"eq","value":true}
        ]}
        """);

        Assert.IsTrue(filter.Matches(User(department: "Finance", hasLicence: true), Now));
        Assert.IsFalse(filter.Matches(User(department: "Finance", hasLicence: false), Now));
        Assert.IsFalse(filter.Matches(User(department: "Legal", hasLicence: true), Now));
    }

    [TestMethod]
    public void Any_RequiresAtLeastOneCondition()
    {
        var filter = Parse("""
        {"any":[
          {"field":"Country","op":"eq","value":"UK"},
          {"field":"Country","op":"eq","value":"Ireland"}
        ]}
        """);

        Assert.IsTrue(filter.Matches(User(country: "UK"), Now));
        Assert.IsTrue(filter.Matches(User(country: "Ireland"), Now));
        Assert.IsFalse(filter.Matches(User(country: "France"), Now));
    }

    [TestMethod]
    public void AllAndAny_CombineAsAndOfOr()
    {
        var filter = Parse("""
        {
          "all":[{"field":"HasCopilotLicense","op":"eq","value":true}],
          "any":[
            {"field":"Country","op":"eq","value":"UK"},
            {"field":"Country","op":"eq","value":"Ireland"}
          ]
        }
        """);

        Assert.IsTrue(filter.Matches(User(country: "UK", hasLicence: true), Now));
        Assert.IsFalse(filter.Matches(User(country: "UK", hasLicence: false), Now));
        Assert.IsFalse(filter.Matches(User(country: "France", hasLicence: true), Now));
    }

    [TestMethod]
    public void Describe_IsHumanReadableForAdminReview()
    {
        var filter = Parse("""
        {"all":[
          {"field":"Department","op":"eq","value":"Finance"},
          {"field":"CopilotLastActivityDate","op":"olderThanDays","value":30}
        ]}
        """);

        var description = filter.Describe();

        StringAssert.Contains(description, "Department");
        StringAssert.Contains(description, "Finance");
        StringAssert.Contains(description, "olderThanDays");
    }

    [TestMethod]
    public void Evaluation_IsDeterministicAcrossRuns()
    {
        // The core benefit over per-user model classification: the same description always
        // produces the same membership.
        var filter = Parse("""{"all":[{"field":"Department","op":"eq","value":"Finance"}]}""");
        var users = Enumerable.Range(0, 1000)
            .Select(i => User($"u{i}@contoso.com", department: i % 3 == 0 ? "Finance" : "Legal"))
            .ToList();

        var first = users.Where(u => filter.Matches(u, Now)).Select(u => u.UserPrincipalName).ToList();
        var second = users.Where(u => filter.Matches(u, Now)).Select(u => u.UserPrincipalName).ToList();

        CollectionAssert.AreEqual(first, second);
        Assert.AreEqual(334, first.Count);
    }
}
