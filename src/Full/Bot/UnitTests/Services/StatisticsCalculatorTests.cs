using Engine.Models;
using Engine.Services;
using Engine.Storage;

namespace UnitTests.Services;

/// <summary>
/// Pure unit tests for <see cref="StatisticsCalculator"/> - no Azure / Graph dependencies.
/// </summary>
[TestClass]
public class StatisticsCalculatorTests
{
    private static MessageLogTableEntity CreateLog(string status, string? recipient = null) =>
        new()
        {
            RowKey = Guid.NewGuid().ToString(),
            MessageBatchId = "batch-1",
            SentDate = DateTime.UtcNow,
            RecipientUpn = recipient,
            Status = status
        };

    [TestMethod]
    public void ComputeMessageStatusStats_NullLogs_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => StatisticsCalculator.ComputeMessageStatusStats(null!));
    }

    [TestMethod]
    public void ComputeMessageStatusStats_EmptyLogs_ReturnsZeroes()
    {
        var stats = StatisticsCalculator.ComputeMessageStatusStats(Array.Empty<MessageLogTableEntity>());
        Assert.AreEqual(0, stats.SentCount);
        Assert.AreEqual(0, stats.FailedCount);
        Assert.AreEqual(0, stats.PendingCount);
        Assert.AreEqual(0, stats.TotalCount);
    }

    [TestMethod]
    public void ComputeMessageStatusStats_CountsSentAndSuccessAsSent()
    {
        var logs = new[]
        {
            CreateLog("Sent"),
            CreateLog("Success"),
            CreateLog("sent"),     // case-insensitive
            CreateLog("SUCCESS"),  // case-insensitive
            CreateLog("Failed"),
            CreateLog("Pending"),
            CreateLog("Unknown")   // not counted in any bucket
        };

        var stats = StatisticsCalculator.ComputeMessageStatusStats(logs);
        Assert.AreEqual(4, stats.SentCount);
        Assert.AreEqual(1, stats.FailedCount);
        Assert.AreEqual(1, stats.PendingCount);
        Assert.AreEqual(7, stats.TotalCount);
    }

    [TestMethod]
    public void ComputeMessageStatusStats_NullStatus_DoesNotCountInAnyBucket()
    {
        var logs = new[]
        {
            new MessageLogTableEntity
            {
                RowKey = "x",
                MessageBatchId = "b",
                SentDate = DateTime.UtcNow,
                Status = null!
            },
            CreateLog("Sent")
        };

        var stats = StatisticsCalculator.ComputeMessageStatusStats(logs);
        Assert.AreEqual(1, stats.SentCount);
        Assert.AreEqual(0, stats.FailedCount);
        Assert.AreEqual(0, stats.PendingCount);
        Assert.AreEqual(2, stats.TotalCount);
    }

    [TestMethod]
    public void ComputeUserCoverageStats_NullLogs_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => StatisticsCalculator.ComputeUserCoverageStats(null!, 10));
    }

    [TestMethod]
    public void ComputeUserCoverageStats_NoTenantUsers_PercentageIsZero()
    {
        var stats = StatisticsCalculator.ComputeUserCoverageStats(
            Array.Empty<MessageLogTableEntity>(), totalUsersInTenant: 0);

        Assert.AreEqual(0, stats.UsersMessaged);
        Assert.AreEqual(0, stats.TotalUsersInTenant);
        Assert.AreEqual(0, stats.UsersNotMessaged);
        Assert.AreEqual(0d, stats.CoveragePercentage);
    }

    [TestMethod]
    public void ComputeUserCoverageStats_DistinctRecipients_CountedOnce()
    {
        var logs = new[]
        {
            CreateLog("Sent", "alice@x.com"),
            CreateLog("Sent", "alice@x.com"),
            CreateLog("Sent", "ALICE@x.com"), // distinct ignoring case
            CreateLog("Sent", "bob@x.com"),
            CreateLog("Sent", "  "),           // whitespace - skipped
            CreateLog("Sent", null)            // null - skipped
        };

        var stats = StatisticsCalculator.ComputeUserCoverageStats(logs, totalUsersInTenant: 10);

        Assert.AreEqual(2, stats.UsersMessaged);
        Assert.AreEqual(10, stats.TotalUsersInTenant);
        Assert.AreEqual(8, stats.UsersNotMessaged);
        Assert.AreEqual(20d, stats.CoveragePercentage);
    }

    [TestMethod]
    public void ComputeUserCoverageStats_MoreMessagedThanTenantUsers_NotMessagedClampsToZero()
    {
        var logs = new[]
        {
            CreateLog("Sent", "a@x.com"),
            CreateLog("Sent", "b@x.com"),
            CreateLog("Sent", "c@x.com")
        };

        var stats = StatisticsCalculator.ComputeUserCoverageStats(logs, totalUsersInTenant: 1);

        Assert.AreEqual(3, stats.UsersMessaged);
        Assert.AreEqual(1, stats.TotalUsersInTenant);
        Assert.AreEqual(0, stats.UsersNotMessaged);
        Assert.AreEqual(300d, stats.CoveragePercentage);
    }

    [TestMethod]
    public void ComputeUserCoverageStats_PercentageRoundedToTwoDecimals()
    {
        var logs = new[]
        {
            CreateLog("Sent", "a@x.com")
        };

        var stats = StatisticsCalculator.ComputeUserCoverageStats(logs, totalUsersInTenant: 3);

        // 1 / 3 * 100 = 33.3333... -> rounded to 33.33
        Assert.AreEqual(33.33d, stats.CoveragePercentage);
    }

    private static CachedUserAndConversationData CachedUser(string id, DateTime? lastInteraction = null) =>
        new()
        {
            RowKey = id,
            ConversationId = $"conv-{id}",
            UserPrincipalName = $"{id}@x.com",
            ServiceUrl = "https://example",
            LastInteractionUtc = lastInteraction
        };

    [TestMethod]
    public void ComputeBotInteractionStats_NullThrows()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => StatisticsCalculator.ComputeBotInteractionStats(null!));
    }

    [TestMethod]
    public void ComputeBotInteractionStats_Empty_ReturnsZeroes()
    {
        var stats = StatisticsCalculator.ComputeBotInteractionStats(Array.Empty<CachedUserAndConversationData>());

        Assert.AreEqual(0, stats.UsersWithConversation);
        Assert.AreEqual(0, stats.UsersInteracted);
        Assert.AreEqual(0, stats.UsersNotInteracted);
        Assert.AreEqual(0d, stats.InteractionRatePercentage);
        Assert.IsNull(stats.LastInteractionUtc);
    }

    [TestMethod]
    public void ComputeBotInteractionStats_CountsAndMostRecent()
    {
        var latest = DateTime.UtcNow.AddMinutes(-1);
        var older = DateTime.UtcNow.AddDays(-3);

        var users = new[]
        {
            CachedUser("a", older),
            CachedUser("b", latest),
            CachedUser("c"),
            CachedUser("d")
        };

        var stats = StatisticsCalculator.ComputeBotInteractionStats(users);

        Assert.AreEqual(4, stats.UsersWithConversation);
        Assert.AreEqual(2, stats.UsersInteracted);
        Assert.AreEqual(2, stats.UsersNotInteracted);
        Assert.AreEqual(50d, stats.InteractionRatePercentage);
        Assert.AreEqual(latest, stats.LastInteractionUtc);
    }

    [TestMethod]
    public void ComputeBotInteractionStats_AllInteracted_HundredPercent()
    {
        var users = new[]
        {
            CachedUser("a", DateTime.UtcNow),
            CachedUser("b", DateTime.UtcNow.AddMinutes(-30))
        };

        var stats = StatisticsCalculator.ComputeBotInteractionStats(users);

        Assert.AreEqual(2, stats.UsersInteracted);
        Assert.AreEqual(100d, stats.InteractionRatePercentage);
    }
}
