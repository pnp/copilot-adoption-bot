using Common.Engine.Models;
using Common.Engine.Services.UserCache;
using System.Collections.Concurrent;

namespace UnitTests.Fakes;

/// <summary>
/// Stores cached user data in memory (for testing).
/// Data is lost when application restarts.
/// </summary>
public class InMemoryCacheStorage : ICacheStorage
{
    private readonly ConcurrentDictionary<string, EnrichedUserInfo> _userCache = new();
    private CacheSyncMetadata _metadata = new();

    public Task<List<EnrichedUserInfo>> GetAllUsersAsync()
    {
        var users = _userCache.Values
            .Where(u => !u.IsDeleted)
            .ToList();

        return Task.FromResult(users);
    }

    public Task<EnrichedUserInfo?> GetUserByUpnAsync(string upn)
    {
        if (_userCache.TryGetValue(upn, out var user) && !user.IsDeleted)
        {
            return Task.FromResult<EnrichedUserInfo?>(user);
        }

        return Task.FromResult<EnrichedUserInfo?>(null);
    }

    public Task UpsertUserAsync(EnrichedUserInfo user)
    {
        _userCache.AddOrUpdate(user.UserPrincipalName, user, (key, existing) => user);
        return Task.CompletedTask;
    }

    public Task UpsertUsersAsync(IEnumerable<EnrichedUserInfo> users)
    {
        foreach (var user in users)
        {
            _userCache.AddOrUpdate(user.UserPrincipalName, user, (key, existing) => user);
        }

        return Task.CompletedTask;
    }

    public Task<int> ClearAllUsersAsync()
    {
        var count = _userCache.Count;
        _userCache.Clear();
        
        _metadata = new CacheSyncMetadata();
        
        return Task.FromResult(count);
    }

    public Task<int> UpdateUsersWithCopilotStatsAsync(Dictionary<string, CopilotUserStats> stats)
    {
        var updateCount = 0;

        foreach (var (upn, userStats) in stats)
        {
            if (_userCache.TryGetValue(upn, out var user) && !user.IsDeleted)
            {
                user.CopilotLastActivityDate = userStats.LastActivityDate;
                user.CopilotChatLastActivityDate = userStats.CopilotChatLastActivityDate;
                user.TeamsCopilotLastActivityDate = userStats.TeamsCopilotLastActivityDate;
                user.WordCopilotLastActivityDate = userStats.WordCopilotLastActivityDate;
                user.ExcelCopilotLastActivityDate = userStats.ExcelCopilotLastActivityDate;
                user.PowerPointCopilotLastActivityDate = userStats.PowerPointCopilotLastActivityDate;
                user.OutlookCopilotLastActivityDate = userStats.OutlookCopilotLastActivityDate;
                user.OneNoteCopilotLastActivityDate = userStats.OneNoteCopilotLastActivityDate;
                user.LoopCopilotLastActivityDate = userStats.LoopCopilotLastActivityDate;
                user.LastCopilotStatsUpdate = DateTime.UtcNow;

                updateCount++;
            }
        }

        return Task.FromResult(updateCount);
    }

    public Task<CacheSyncMetadata> GetSyncMetadataAsync()
    {
        return Task.FromResult(_metadata);
    }

    public Task UpdateSyncMetadataAsync(CacheSyncMetadata metadata)
    {
        _metadata = metadata;
        return Task.CompletedTask;
    }
}
