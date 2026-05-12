using Engine.Models;
using Engine.Services.UserCache;

namespace UnitTests.Fakes;

/// <summary>
/// In-memory fake for <see cref="IUserCacheManager"/> for pure unit tests of
/// <see cref="Engine.Services.CachedUserService"/>.
/// </summary>
public class FakeUserCacheManager : IUserCacheManager
{
    private readonly Dictionary<string, EnrichedUserInfo> _users;
    private CacheSyncMetadata _metadata = new();

    public int GetAllCachedUsersCallCount { get; private set; }
    public int GetCachedUserCallCount { get; private set; }
    public int SyncCallCount { get; private set; }
    public int ClearCallCount { get; private set; }
    public int UpdateCopilotStatsCallCount { get; private set; }
    public bool? LastForceRefresh { get; private set; }
    public bool? LastSkipAutoSync { get; private set; }

    public FakeUserCacheManager(IEnumerable<EnrichedUserInfo>? users = null)
    {
        _users = (users ?? Array.Empty<EnrichedUserInfo>())
            .ToDictionary(u => u.UserPrincipalName, StringComparer.OrdinalIgnoreCase);
    }

    public Task<List<EnrichedUserInfo>> GetAllCachedUsersAsync(bool forceRefresh = false, bool skipAutoSync = false)
    {
        GetAllCachedUsersCallCount++;
        LastForceRefresh = forceRefresh;
        LastSkipAutoSync = skipAutoSync;
        return Task.FromResult(_users.Values.ToList());
    }

    public Task<EnrichedUserInfo?> GetCachedUserAsync(string upn)
    {
        GetCachedUserCallCount++;
        _users.TryGetValue(upn, out var user);
        return Task.FromResult(user);
    }

    public Task ClearCacheAsync()
    {
        ClearCallCount++;
        _users.Clear();
        _metadata = new CacheSyncMetadata();
        return Task.CompletedTask;
    }

    public Task SyncUsersAsync()
    {
        SyncCallCount++;
        return Task.CompletedTask;
    }

    public Task UpdateCopilotStatsAsync()
    {
        UpdateCopilotStatsCallCount++;
        return Task.CompletedTask;
    }

    public Task<CacheSyncMetadata> GetSyncMetadataAsync() => Task.FromResult(_metadata);

    public Task UpdateSyncMetadataAsync(CacheSyncMetadata metadata)
    {
        _metadata = metadata;
        return Task.CompletedTask;
    }
}
