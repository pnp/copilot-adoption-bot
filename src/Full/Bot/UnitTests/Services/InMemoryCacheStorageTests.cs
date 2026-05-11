using Common.Engine.Models;
using Common.Engine.Services.UserCache;
using UnitTests.Fakes;

namespace UnitTests.Services;

/// <summary>
/// Pure unit tests for <see cref="InMemoryCacheStorage"/>. This fake is the lynchpin of
/// the other pure unit tests in the suite, so its behavior must be pinned down explicitly.
/// </summary>
[TestClass]
public class InMemoryCacheStorageTests
{
    private static EnrichedUserInfo MakeUser(string upn, bool deleted = false) => new()
    {
        Id = upn,
        UserPrincipalName = upn,
        DisplayName = upn,
        IsDeleted = deleted
    };

    [TestMethod]
    public async Task GetAllUsersAsync_FiltersDeletedUsers()
    {
        var storage = new InMemoryCacheStorage();
        await storage.UpsertUsersAsync(new[]
        {
            MakeUser("a@contoso.com"),
            MakeUser("b@contoso.com", deleted: true),
            MakeUser("c@contoso.com")
        });

        var users = await storage.GetAllUsersAsync();

        CollectionAssert.AreEquivalent(
            new[] { "a@contoso.com", "c@contoso.com" },
            users.Select(u => u.UserPrincipalName).ToList());
    }

    [TestMethod]
    public async Task GetUserByUpnAsync_DeletedUser_ReturnsNull()
    {
        var storage = new InMemoryCacheStorage();
        await storage.UpsertUserAsync(MakeUser("gone@contoso.com", deleted: true));

        var user = await storage.GetUserByUpnAsync("gone@contoso.com");

        Assert.IsNull(user);
    }

    [TestMethod]
    public async Task UpsertUserAsync_DuplicateUpn_ReplacesExisting()
    {
        var storage = new InMemoryCacheStorage();
        await storage.UpsertUserAsync(new EnrichedUserInfo
        {
            Id = "a", UserPrincipalName = "a@contoso.com", DisplayName = "Original"
        });
        await storage.UpsertUserAsync(new EnrichedUserInfo
        {
            Id = "a", UserPrincipalName = "a@contoso.com", DisplayName = "Updated"
        });

        var user = await storage.GetUserByUpnAsync("a@contoso.com");

        Assert.IsNotNull(user);
        Assert.AreEqual("Updated", user!.DisplayName);
    }

    [TestMethod]
    public async Task UpdateUsersWithCopilotStatsAsync_OnlyTouchesKnownUsers()
    {
        var storage = new InMemoryCacheStorage();
        await storage.UpsertUserAsync(MakeUser("known@contoso.com"));

        var stats = new Dictionary<string, CopilotUserStats>
        {
            ["known@contoso.com"] = new CopilotUserStats { LastActivityDate = DateTime.UtcNow.AddDays(-1) },
            ["unknown@contoso.com"] = new CopilotUserStats { LastActivityDate = DateTime.UtcNow.AddDays(-1) }
        };

        var updated = await storage.UpdateUsersWithCopilotStatsAsync(stats);

        Assert.AreEqual(1, updated, "Should only update users that exist in the cache.");
        var user = await storage.GetUserByUpnAsync("known@contoso.com");
        Assert.IsNotNull(user!.CopilotLastActivityDate);
        Assert.IsNotNull(user.LastCopilotStatsUpdate);
    }

    [TestMethod]
    public async Task UpdateUsersWithCopilotStatsAsync_DeletedUser_IsSkipped()
    {
        var storage = new InMemoryCacheStorage();
        await storage.UpsertUserAsync(MakeUser("gone@contoso.com", deleted: true));

        var updated = await storage.UpdateUsersWithCopilotStatsAsync(new()
        {
            ["gone@contoso.com"] = new CopilotUserStats { LastActivityDate = DateTime.UtcNow }
        });

        Assert.AreEqual(0, updated);
    }

    [TestMethod]
    public async Task UpdateUsersWithLicenseInfoAsync_AppliesValueAndCount()
    {
        var storage = new InMemoryCacheStorage();
        await storage.UpsertUsersAsync(new[]
        {
            MakeUser("a@contoso.com"),
            MakeUser("b@contoso.com")
        });

        var updated = await storage.UpdateUsersWithLicenseInfoAsync(new()
        {
            ["a@contoso.com"] = true,
            ["b@contoso.com"] = false,
            ["c@contoso.com"] = true // not in cache
        });

        Assert.AreEqual(2, updated);
        Assert.IsTrue((await storage.GetUserByUpnAsync("a@contoso.com"))!.HasCopilotLicense);
        Assert.IsFalse((await storage.GetUserByUpnAsync("b@contoso.com"))!.HasCopilotLicense);
    }

    [TestMethod]
    public async Task ClearAllUsersAsync_ResetsUsersAndMetadata()
    {
        var storage = new InMemoryCacheStorage();
        await storage.UpsertUserAsync(MakeUser("a@contoso.com"));
        await storage.UpdateSyncMetadataAsync(new CacheSyncMetadata
        {
            DeltaToken = "tok",
            LastFullSyncDate = DateTime.UtcNow,
            LastSyncStatus = "Success"
        });

        var removed = await storage.ClearAllUsersAsync();

        Assert.AreEqual(1, removed);
        Assert.AreEqual(0, (await storage.GetAllUsersAsync()).Count);
        var metadata = await storage.GetSyncMetadataAsync();
        Assert.IsNull(metadata.DeltaToken);
        Assert.IsNull(metadata.LastFullSyncDate);
        Assert.IsNull(metadata.LastSyncStatus);
    }

    [TestMethod]
    public async Task SyncMetadata_RoundTripsThroughStorage()
    {
        var storage = new InMemoryCacheStorage();
        var metadata = new CacheSyncMetadata
        {
            DeltaToken = "abc",
            LastFullSyncDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            LastSyncStatus = "Success",
            LastSyncUserCount = 42
        };

        await storage.UpdateSyncMetadataAsync(metadata);
        var roundTripped = await storage.GetSyncMetadataAsync();

        Assert.AreEqual("abc", roundTripped.DeltaToken);
        Assert.AreEqual("Success", roundTripped.LastSyncStatus);
        Assert.AreEqual(42, roundTripped.LastSyncUserCount);
    }
}
