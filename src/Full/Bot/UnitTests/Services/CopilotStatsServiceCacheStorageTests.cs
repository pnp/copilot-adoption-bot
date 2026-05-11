using Common.Engine.Models;
using Common.Engine.Services.UserCache;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTests.Fakes;

namespace UnitTests.Services;

/// <summary>
/// Pure unit tests for the new <see cref="CopilotStatsService.UpdateCachedUsersWithStatsAsync(ICacheStorage, List{CopilotUsageRecord})"/>
/// overload that flows through <see cref="ICacheStorage"/> instead of a real <see cref="Azure.Data.Tables.TableClient"/>.
/// </summary>
[TestClass]
public class CopilotStatsServiceCacheStorageTests
{
    private static CopilotStatsService BuildService(FakeCopilotStatsLoader? loader = null)
        => new(NullLogger<CopilotStatsService>.Instance, loader ?? new FakeCopilotStatsLoader(new List<CopilotUsageRecord>()));

    private static InMemoryCacheStorage SeedStorage(params string[] upns)
    {
        var storage = new InMemoryCacheStorage();
        foreach (var upn in upns)
        {
            storage.UpsertUserAsync(new EnrichedUserInfo
            {
                UserPrincipalName = upn,
                DisplayName = upn
            }).GetAwaiter().GetResult();
        }
        return storage;
    }

    [TestMethod]
    public async Task UpdateCachedUsersWithStatsAsync_UpdatesExistingUsers()
    {
        var storage = SeedStorage("a@contoso.com", "b@contoso.com");
        var service = BuildService();

        var stats = new List<CopilotUsageRecord>
        {
            new() { UserPrincipalName = "a@contoso.com", LastActivityDate = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc), TeamsCopilotLastActivityDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new() { UserPrincipalName = "b@contoso.com", LastActivityDate = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc) }
        };

        var updated = await service.UpdateCachedUsersWithStatsAsync(storage, stats);

        Assert.AreEqual(2, updated);

        var a = await storage.GetUserByUpnAsync("a@contoso.com");
        Assert.IsNotNull(a);
        Assert.AreEqual(new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc), a!.CopilotLastActivityDate);
        Assert.AreEqual(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), a.TeamsCopilotLastActivityDate);
        Assert.IsNotNull(a.LastCopilotStatsUpdate);
    }

    [TestMethod]
    public async Task UpdateCachedUsersWithStatsAsync_SkipsNonExistentUsers()
    {
        var storage = SeedStorage("a@contoso.com");
        var service = BuildService();

        var stats = new List<CopilotUsageRecord>
        {
            new() { UserPrincipalName = "a@contoso.com", LastActivityDate = DateTime.UtcNow },
            new() { UserPrincipalName = "ghost@contoso.com", LastActivityDate = DateTime.UtcNow }
        };

        var updated = await service.UpdateCachedUsersWithStatsAsync(storage, stats);

        Assert.AreEqual(1, updated, "Only existing users should be updated");
        Assert.IsNull(await storage.GetUserByUpnAsync("ghost@contoso.com"));
    }

    [TestMethod]
    public async Task UpdateCachedUsersWithStatsAsync_EmptyList_DoesNothing()
    {
        var storage = SeedStorage("a@contoso.com");
        var service = BuildService();

        var updated = await service.UpdateCachedUsersWithStatsAsync(storage, new List<CopilotUsageRecord>());

        Assert.AreEqual(0, updated);

        var a = await storage.GetUserByUpnAsync("a@contoso.com");
        Assert.IsNotNull(a);
        Assert.IsNull(a!.LastCopilotStatsUpdate);
    }

    [TestMethod]
    public async Task UpdateCachedUsersWithStatsAsync_NullCacheStorage_Throws()
    {
        var service = BuildService();
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() =>
            service.UpdateCachedUsersWithStatsAsync((ICacheStorage)null!, new List<CopilotUsageRecord>()));
    }

    [TestMethod]
    public async Task UpdateCachedUsersWithStatsAsync_NullStats_Throws()
    {
        var storage = new InMemoryCacheStorage();
        var service = BuildService();
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() =>
            service.UpdateCachedUsersWithStatsAsync(storage, null!));
    }

    [TestMethod]
    public async Task UpdateCachedUsersWithStatsAsync_IgnoresRecordsWithBlankUpn()
    {
        var storage = SeedStorage("a@contoso.com");
        var service = BuildService();

        var stats = new List<CopilotUsageRecord>
        {
            new() { UserPrincipalName = "a@contoso.com", LastActivityDate = DateTime.UtcNow },
            new() { UserPrincipalName = "", LastActivityDate = DateTime.UtcNow },
            new() { UserPrincipalName = "   ", LastActivityDate = DateTime.UtcNow }
        };

        var updated = await service.UpdateCachedUsersWithStatsAsync(storage, stats);
        Assert.AreEqual(1, updated);
    }
}
