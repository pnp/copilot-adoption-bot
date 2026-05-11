using Common.Engine.Models;
using Common.Engine.Services;

namespace UnitTests.Fakes;

/// <summary>
/// In-memory fake for <see cref="IExternalUserService"/> used by pure unit tests so
/// <see cref="CachedUserService"/> can be exercised without Microsoft Graph.
/// </summary>
public class FakeExternalUserService : IExternalUserService
{
    private readonly Dictionary<string, EnrichedUserInfo> _users;

    public int GetAllUsersCallCount { get; private set; }
    public int GetUserCallCount { get; private set; }
    public int GetUsersByDepartmentCallCount { get; private set; }
    public int EnrichManagersCallCount { get; private set; }
    public int EnrichLicenseCallCount { get; private set; }

    public FakeExternalUserService(IEnumerable<EnrichedUserInfo>? users = null)
    {
        _users = (users ?? Array.Empty<EnrichedUserInfo>())
            .ToDictionary(u => u.UserPrincipalName, StringComparer.OrdinalIgnoreCase);
    }

    public Task<List<EnrichedUserInfo>> GetAllUsersAsync(int maxUsers = 999)
    {
        GetAllUsersCallCount++;
        return Task.FromResult(_users.Values.Take(maxUsers).ToList());
    }

    public Task<EnrichedUserInfo?> GetUserAsync(string upn)
    {
        GetUserCallCount++;
        _users.TryGetValue(upn, out var user);
        return Task.FromResult(user);
    }

    public Task<List<EnrichedUserInfo>> GetUsersByDepartmentAsync(string department)
    {
        GetUsersByDepartmentCallCount++;
        var matches = _users.Values
            .Where(u => string.Equals(u.Department, department, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult(matches);
    }

    public Task EnrichUsersWithManagersAsync(List<EnrichedUserInfo> users)
    {
        EnrichManagersCallCount++;
        return Task.CompletedTask;
    }

    public Task EnrichUsersWithLicenseInfoAsync(List<EnrichedUserInfo> users)
    {
        EnrichLicenseCallCount++;
        return Task.CompletedTask;
    }
}
