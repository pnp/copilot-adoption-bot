using Common.Engine.Config;
using Common.Engine.Models;
using Common.Engine.Services.UserCache;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.Fakes;

namespace UnitTests.Services;

/// <summary>
/// Pure unit tests for <see cref="UserCacheManager"/>. These exercise the orchestration
/// branches (full vs delta sync, cache expiration, error handling, stats refresh) without
/// touching Azure Table Storage or Microsoft Graph.
/// </summary>
[TestClass]
public class UserCacheManagerTests
{
    private static UserCacheManager CreateManager(
        out FakeUserDataLoader loader,
        out InMemoryCacheStorage storage,
        UserCacheConfig? config = null,
        List<EnrichedUserInfo>? users = null,
        Dictionary<string, CopilotUserStats>? stats = null)
    {
        loader = new FakeUserDataLoader(users, stats);
        storage = new InMemoryCacheStorage();
        var cfg = config ?? new UserCacheConfig();
        return new UserCacheManager(loader, storage, cfg, NullLogger<UserCacheManager>.Instance);
    }

    [TestMethod]
    public async Task SyncUsersAsync_OnEmptyCache_PerformsFullSyncAndStoresMetadata()
    {
        var manager = CreateManager(out var loader, out var storage);

        await manager.SyncUsersAsync();

        var metadata = await storage.GetSyncMetadataAsync();
        var cached = await storage.GetAllUsersAsync();

        Assert.IsTrue(cached.Count > 0, "Expected users from the fake loader to be persisted.");
        Assert.AreEqual("Success", metadata.LastSyncStatus);
        Assert.IsNotNull(metadata.LastFullSyncDate);
        Assert.IsNotNull(metadata.LastDeltaSyncDate);
        Assert.IsNotNull(metadata.DeltaToken);
        Assert.AreEqual(cached.Count, metadata.LastSyncUserCount);
    }

    [TestMethod]
    public async Task SyncUsersAsync_WithRecentFullSyncAndDeltaToken_PerformsDeltaSync()
    {
        var manager = CreateManager(out var loader, out var storage);

        // First sync is full
        await manager.SyncUsersAsync();
        var afterFull = await storage.GetSyncMetadataAsync();
        var firstFullDate = afterFull.LastFullSyncDate;

        // Second sync should be a delta sync (full not due, delta token present)
        await manager.SyncUsersAsync();

        var afterDelta = await storage.GetSyncMetadataAsync();
        Assert.AreEqual(firstFullDate, afterDelta.LastFullSyncDate, "Full sync date should not change on a delta run.");
        Assert.AreEqual("Success", afterDelta.LastSyncStatus);
    }

    [TestMethod]
    public async Task SyncUsersAsync_WhenFullSyncIsDue_RunsFullSync()
    {
        var config = new UserCacheConfig { FullSyncInterval = TimeSpan.FromMilliseconds(1) };
        var manager = CreateManager(out var loader, out var storage, config);

        await manager.SyncUsersAsync();
        var firstFull = (await storage.GetSyncMetadataAsync()).LastFullSyncDate;

        await Task.Delay(20); // ensure the interval has elapsed
        await manager.SyncUsersAsync();

        var secondFull = (await storage.GetSyncMetadataAsync()).LastFullSyncDate;
        Assert.IsNotNull(firstFull);
        Assert.IsNotNull(secondFull);
        Assert.IsTrue(secondFull > firstFull, "Full sync should run again once FullSyncInterval has elapsed.");
    }

    [TestMethod]
    public async Task SyncUsersAsync_WhenLoaderThrows_RecordsFailureAndRethrows()
    {
        var storage = new InMemoryCacheStorage();
        var loader = new ThrowingUserDataLoader();
        var manager = new UserCacheManager(loader, storage, new UserCacheConfig(), NullLogger<UserCacheManager>.Instance);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => manager.SyncUsersAsync());

        var metadata = await storage.GetSyncMetadataAsync();
        Assert.AreEqual("Failed", metadata.LastSyncStatus);
        Assert.IsFalse(string.IsNullOrEmpty(metadata.LastSyncError));
    }

    [TestMethod]
    public async Task GetAllCachedUsersAsync_AutoSyncsWhenExpired()
    {
        var config = new UserCacheConfig { CacheExpiration = TimeSpan.FromMilliseconds(1) };
        var manager = CreateManager(out var loader, out var storage, config);

        var users = await manager.GetAllCachedUsersAsync();

        Assert.IsTrue(users.Count > 0, "First call should auto-sync because cache is empty.");
        Assert.AreEqual("Success", (await storage.GetSyncMetadataAsync()).LastSyncStatus);
    }

    [TestMethod]
    public async Task GetAllCachedUsersAsync_SkipAutoSync_DoesNotPopulateEmptyCache()
    {
        var manager = CreateManager(out var loader, out var storage);

        var users = await manager.GetAllCachedUsersAsync(skipAutoSync: true);

        Assert.AreEqual(0, users.Count);
        Assert.IsNull((await storage.GetSyncMetadataAsync()).LastFullSyncDate);
    }

    [TestMethod]
    public async Task GetAllCachedUsersAsync_ForceRefresh_TriggersSyncEvenWithFreshMetadata()
    {
        var manager = CreateManager(out var loader, out var storage);
        await manager.SyncUsersAsync(); // initial populate

        var loadCountBefore = (await storage.GetSyncMetadataAsync()).LastFullSyncDate;

        await manager.GetAllCachedUsersAsync(forceRefresh: true);

        var metadata = await storage.GetSyncMetadataAsync();
        Assert.AreEqual("Success", metadata.LastSyncStatus);
        Assert.IsNotNull(metadata.LastFullSyncDate);
    }

    [TestMethod]
    public async Task ClearCacheAsync_RemovesUsersAndResetsMetadata()
    {
        var manager = CreateManager(out var loader, out var storage);
        await manager.SyncUsersAsync();
        Assert.IsTrue((await storage.GetAllUsersAsync()).Count > 0);

        await manager.ClearCacheAsync();

        Assert.AreEqual(0, (await storage.GetAllUsersAsync()).Count);
        var metadata = await storage.GetSyncMetadataAsync();
        Assert.IsNull(metadata.DeltaToken);
        Assert.IsNull(metadata.LastFullSyncDate);
    }

    [TestMethod]
    public async Task UpdateCopilotStatsAsync_AppliesStatsToCachedUsers()
    {
        var stats = new Dictionary<string, CopilotUserStats>
        {
            ["test1@contoso.com"] = new CopilotUserStats
            {
                LastActivityDate = DateTime.UtcNow.AddDays(-1),
                WordCopilotLastActivityDate = DateTime.UtcNow.AddDays(-2)
            }
        };

        var manager = CreateManager(out var loader, out var storage, stats: stats);
        await manager.SyncUsersAsync();

        await manager.UpdateCopilotStatsAsync();

        var cached = await storage.GetUserByUpnAsync("test1@contoso.com");
        Assert.IsNotNull(cached);
        Assert.IsNotNull(cached!.CopilotLastActivityDate);
        Assert.IsNotNull(cached.WordCopilotLastActivityDate);
        Assert.IsNotNull((await storage.GetSyncMetadataAsync()).LastCopilotStatsUpdate);
    }

    [TestMethod]
    public async Task UpdateCopilotStatsAsync_WhenStillFresh_DoesNothing()
    {
        var manager = CreateManager(out var loader, out var storage,
            new UserCacheConfig { CopilotStatsRefreshInterval = TimeSpan.FromHours(1) });
        await manager.SyncUsersAsync();

        var metadata = await storage.GetSyncMetadataAsync();
        metadata.LastCopilotStatsUpdate = DateTime.UtcNow; // mark as just refreshed
        await storage.UpdateSyncMetadataAsync(metadata);

        var stampBefore = metadata.LastCopilotStatsUpdate;

        await manager.UpdateCopilotStatsAsync();

        var stampAfter = (await storage.GetSyncMetadataAsync()).LastCopilotStatsUpdate;
        Assert.AreEqual(stampBefore, stampAfter, "Stats should not be refreshed inside the refresh interval.");
    }

    [TestMethod]
    public async Task UpdateCopilotStatsAsync_WithEmptyStats_StillUpdatesMetadataTimestamp()
    {
        var manager = CreateManager(out var loader, out var storage); // no stats configured
        await manager.SyncUsersAsync();

        await manager.UpdateCopilotStatsAsync();

        Assert.IsNotNull((await storage.GetSyncMetadataAsync()).LastCopilotStatsUpdate);
    }

    private sealed class ThrowingUserDataLoader : IUserDataLoader
    {
        public Task<UserLoadResult> LoadAllUsersAsync() => throw new InvalidOperationException("loader failed");
        public Task<UserLoadResult> LoadDeltaChangesAsync(string deltaToken) => throw new InvalidOperationException("loader failed");
        public Task<Dictionary<string, CopilotUserStats>> GetCopilotStatsAsync() => Task.FromResult(new Dictionary<string, CopilotUserStats>());
        public Task<Dictionary<string, bool>> GetLicenseInfoAsync() => Task.FromResult(new Dictionary<string, bool>());
    }
}
