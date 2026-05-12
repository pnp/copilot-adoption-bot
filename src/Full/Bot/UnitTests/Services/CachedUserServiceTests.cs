using Engine.Models;
using Engine.Services;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.Fakes;

namespace UnitTests.Services;

/// <summary>
/// Pure unit tests for <see cref="CachedUserService"/> using in-memory fakes for the
/// cache manager and external (Graph) service.
/// </summary>
[TestClass]
public class CachedUserServiceTests
{
    private static EnrichedUserInfo MakeUser(string upn, string? department = null) => new()
    {
        Id = upn,
        UserPrincipalName = upn,
        DisplayName = upn,
        Department = department
    };

    [TestMethod]
    public async Task GetAllUsersWithMetadataAsync_UsesCacheManager_AndRespectsMaxUsers()
    {
        var cache = new FakeUserCacheManager(new[]
        {
            MakeUser("a@contoso.com"),
            MakeUser("b@contoso.com"),
            MakeUser("c@contoso.com")
        });
        var external = new FakeExternalUserService();
        var service = new CachedUserService(cache, external, NullLogger<CachedUserService>.Instance);

        var result = await service.GetAllUsersWithMetadataAsync(maxUsers: 2);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(1, cache.GetAllCachedUsersCallCount);
        Assert.AreEqual(0, external.GetAllUsersCallCount, "Should not have hit external service.");
    }

    [TestMethod]
    public async Task GetAllUsersWithMetadataAsync_PassesForceRefreshThroughToCacheManager()
    {
        var cache = new FakeUserCacheManager(new[] { MakeUser("a@contoso.com") });
        var external = new FakeExternalUserService();
        var service = new CachedUserService(cache, external, NullLogger<CachedUserService>.Instance);

        await service.GetAllUsersWithMetadataAsync(forceRefresh: true);

        Assert.AreEqual(true, cache.LastForceRefresh);
    }

    [TestMethod]
    public async Task GetUserWithMetadataAsync_WhenInCache_DoesNotCallExternalService()
    {
        var cache = new FakeUserCacheManager(new[] { MakeUser("a@contoso.com") });
        var external = new FakeExternalUserService(new[] { MakeUser("a@contoso.com") });
        var service = new CachedUserService(cache, external, NullLogger<CachedUserService>.Instance);

        var user = await service.GetUserWithMetadataAsync("a@contoso.com");

        Assert.IsNotNull(user);
        Assert.AreEqual(0, external.GetUserCallCount, "Cache hit should not call external service.");
    }

    [TestMethod]
    public async Task GetUserWithMetadataAsync_WhenNotInCache_FallsBackToExternalService()
    {
        var cache = new FakeUserCacheManager();
        var external = new FakeExternalUserService(new[] { MakeUser("a@contoso.com") });
        var service = new CachedUserService(cache, external, NullLogger<CachedUserService>.Instance);

        var user = await service.GetUserWithMetadataAsync("a@contoso.com");

        Assert.IsNotNull(user);
        Assert.AreEqual("a@contoso.com", user!.UserPrincipalName);
        Assert.AreEqual(1, external.GetUserCallCount);
    }

    [TestMethod]
    public async Task GetUserWithMetadataAsync_WhenNeitherSourceHasUser_ReturnsNull()
    {
        var cache = new FakeUserCacheManager();
        var external = new FakeExternalUserService();
        var service = new CachedUserService(cache, external, NullLogger<CachedUserService>.Instance);

        var user = await service.GetUserWithMetadataAsync("ghost@contoso.com");

        Assert.IsNull(user);
        Assert.AreEqual(1, external.GetUserCallCount);
    }

    [TestMethod]
    public async Task GetUsersByDepartmentAsync_BypassesCache()
    {
        var cache = new FakeUserCacheManager(new[] { MakeUser("a@contoso.com", "Sales") });
        var external = new FakeExternalUserService(new[]
        {
            MakeUser("a@contoso.com", "Sales"),
            MakeUser("b@contoso.com", "Engineering"),
            MakeUser("c@contoso.com", "Sales")
        });
        var service = new CachedUserService(cache, external, NullLogger<CachedUserService>.Instance);

        var sales = await service.GetUsersByDepartmentAsync("Sales");

        Assert.AreEqual(2, sales.Count);
        Assert.AreEqual(1, external.GetUsersByDepartmentCallCount);
        Assert.AreEqual(0, cache.GetAllCachedUsersCallCount, "Department lookup should not use cache.");
    }

    [TestMethod]
    public async Task EnrichUsersWithManagersAsync_DelegatesToExternalService()
    {
        var cache = new FakeUserCacheManager();
        var external = new FakeExternalUserService();
        var service = new CachedUserService(cache, external, NullLogger<CachedUserService>.Instance);

        await service.EnrichUsersWithManagersAsync(new List<EnrichedUserInfo>());

        Assert.AreEqual(1, external.EnrichManagersCallCount);
    }

    [TestMethod]
    public async Task EnrichUsersWithLicenseInfoAsync_DelegatesToExternalService()
    {
        var cache = new FakeUserCacheManager();
        var external = new FakeExternalUserService();
        var service = new CachedUserService(cache, external, NullLogger<CachedUserService>.Instance);

        await service.EnrichUsersWithLicenseInfoAsync(new List<EnrichedUserInfo>());

        Assert.AreEqual(1, external.EnrichLicenseCallCount);
    }

    [TestMethod]
    public async Task UpdateCopilotStatsAndLicensesAsync_DelegatesToCacheManager()
    {
        var cache = new FakeUserCacheManager();
        var external = new FakeExternalUserService();
        var service = new CachedUserService(cache, external, NullLogger<CachedUserService>.Instance);

        await service.UpdateCopilotStatsAndLicensesAsync();

        Assert.AreEqual(1, cache.UpdateCopilotStatsCallCount);
    }

    [TestMethod]
    public async Task GetAllUsersDirectFromGraphAsync_AlwaysUsesExternalService()
    {
        var cache = new FakeUserCacheManager(new[] { MakeUser("cached@contoso.com") });
        var external = new FakeExternalUserService(new[]
        {
            MakeUser("a@contoso.com"),
            MakeUser("b@contoso.com")
        });
        var service = new CachedUserService(cache, external, NullLogger<CachedUserService>.Instance);

        var users = await service.GetAllUsersDirectFromGraphAsync();

        Assert.AreEqual(2, users.Count);
        Assert.AreEqual(1, external.GetAllUsersCallCount);
        Assert.AreEqual(0, cache.GetAllCachedUsersCallCount);
    }
}
