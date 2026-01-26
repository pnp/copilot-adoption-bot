using Common.Engine.Models;
using Common.Engine.Services.UserCache;

namespace UnitTests.Fakes;

/// <summary>
/// Fake user data loader for testing purposes.
/// Returns pre-configured test data instead of calling Microsoft Graph.
/// </summary>
public class FakeUserDataLoader : IUserDataLoader
{
    private readonly List<EnrichedUserInfo> _users;
    private readonly Dictionary<string, CopilotUserStats> _stats;
    private int _loadCount = 0;

    public FakeUserDataLoader(List<EnrichedUserInfo>? users = null, Dictionary<string, CopilotUserStats>? stats = null)
    {
        _users = users ?? CreateDefaultTestUsers();
        _stats = stats ?? [];
    }

    public Task<UserLoadResult> LoadAllUsersAsync()
    {
        _loadCount++;

        return Task.FromResult(new UserLoadResult
        {
            Users = _users,
            DeltaToken = $"fake-delta-token-{_loadCount}"
        });
    }

    public Task<UserLoadResult> LoadDeltaChangesAsync(string deltaToken)
    {
        _loadCount++;

        return Task.FromResult(new UserLoadResult
        {
            Users = [],
            DeltaToken = $"fake-delta-token-{_loadCount}"
        });
    }

    public Task<Dictionary<string, CopilotUserStats>> GetCopilotStatsAsync()
    {
        return Task.FromResult(_stats);
    }

    public Task<Dictionary<string, bool>> GetLicenseInfoAsync()
    {
        // Return empty dictionary by default for tests
        return Task.FromResult(new Dictionary<string, bool>());
    }

    private static List<EnrichedUserInfo> CreateDefaultTestUsers()
    {
        return
        [
            new EnrichedUserInfo
            {
                Id = "1",
                UserPrincipalName = "test1@contoso.com",
                DisplayName = "Test User 1",
                GivenName = "Test",
                Surname = "User1",
                Department = "Engineering",
                JobTitle = "Software Engineer"
            },
            new EnrichedUserInfo
            {
                Id = "2",
                UserPrincipalName = "test2@contoso.com",
                DisplayName = "Test User 2",
                GivenName = "Test",
                Surname = "User2",
                Department = "Sales",
                JobTitle = "Sales Manager"
            }
        ];
    }
}
