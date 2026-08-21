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
    private static MessageBatchTableEntity CreateBatch(int total, int sent, int failed, string id = "batch-1") =>
        new()
        {
            RowKey = id,
            BatchName = id,
            TemplateId = "template-1",
            SenderUpn = "admin@x.com",
            CreatedDate = DateTime.UtcNow,
            TotalCount = total,
            SentCount = sent,
            FailedCount = failed
        };

    [TestMethod]
    public void ComputeMessageStatusStats_NullBatches_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => StatisticsCalculator.ComputeMessageStatusStats(null!));
    }

    [TestMethod]
    public void ComputeMessageStatusStats_EmptyBatches_ReturnsZeroes()
    {
        var stats = StatisticsCalculator.ComputeMessageStatusStats(Array.Empty<MessageBatchTableEntity>());

        Assert.AreEqual(0, stats.SentCount);
        Assert.AreEqual(0, stats.FailedCount);
        Assert.AreEqual(0, stats.PendingCount);
        Assert.AreEqual(0, stats.TotalCount);
    }

    [TestMethod]
    public void ComputeMessageStatusStats_SumsCountersAcrossBatches()
    {
        var batches = new[]
        {
            CreateBatch(total: 10, sent: 6, failed: 2, id: "b1"),
            CreateBatch(total: 5, sent: 5, failed: 0, id: "b2")
        };

        var stats = StatisticsCalculator.ComputeMessageStatusStats(batches);

        Assert.AreEqual(11, stats.SentCount);
        Assert.AreEqual(2, stats.FailedCount);
        Assert.AreEqual(15, stats.TotalCount);
        Assert.AreEqual(2, stats.PendingCount, "total - sent - failed");
    }

    [TestMethod]
    public void ComputeMessageStatusStats_PendingNeverNegative()
    {
        // Counters could over-report if a delivery were somehow counted twice; the dashboard
        // must not render a negative "pending".
        var batches = new[] { CreateBatch(total: 2, sent: 3, failed: 1) };

        var stats = StatisticsCalculator.ComputeMessageStatusStats(batches);

        Assert.AreEqual(0, stats.PendingCount);
    }

    [TestMethod]
    public void ComputeUserCoverageStats_UsesSuppliedReachCount()
    {
        var stats = StatisticsCalculator.ComputeUserCoverageStats(usersMessaged: 2, totalUsersInTenant: 10);

        Assert.AreEqual(2, stats.UsersMessaged);
        Assert.AreEqual(10, stats.TotalUsersInTenant);
        Assert.AreEqual(8, stats.UsersNotMessaged);
        Assert.AreEqual(20d, stats.CoveragePercentage);
    }

    [TestMethod]
    public void ComputeUserCoverageStats_MoreMessagedThanTenantUsers_NotMessagedClampsToZero()
    {
        var stats = StatisticsCalculator.ComputeUserCoverageStats(usersMessaged: 3, totalUsersInTenant: 1);

        Assert.AreEqual(3, stats.UsersMessaged);
        Assert.AreEqual(1, stats.TotalUsersInTenant);
        Assert.AreEqual(0, stats.UsersNotMessaged);
        Assert.AreEqual(300d, stats.CoveragePercentage);
    }

    [TestMethod]
    public void ComputeUserCoverageStats_NegativeReach_ClampsToZero()
    {
        var stats = StatisticsCalculator.ComputeUserCoverageStats(usersMessaged: -5, totalUsersInTenant: 10);

        Assert.AreEqual(0, stats.UsersMessaged);
        Assert.AreEqual(10, stats.UsersNotMessaged);
    }

    [TestMethod]
    public void ComputeUserCoverageStats_ZeroTenantUsers_NoDivideByZero()
    {
        var stats = StatisticsCalculator.ComputeUserCoverageStats(usersMessaged: 5, totalUsersInTenant: 0);

        Assert.AreEqual(0d, stats.CoveragePercentage);
    }

    [TestMethod]
    public void ComputeUserCoverageStats_PercentageRoundedToTwoDecimals()
    {
        // 1 / 3 * 100 = 33.3333... -> rounded to 33.33
        var stats = StatisticsCalculator.ComputeUserCoverageStats(usersMessaged: 1, totalUsersInTenant: 3);

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
