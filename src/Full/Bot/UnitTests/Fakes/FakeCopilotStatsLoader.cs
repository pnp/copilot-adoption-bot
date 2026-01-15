using Common.Engine.Models;
using Common.Engine.Services.UserCache;

namespace UnitTests.Fakes;

/// <summary>
/// Fake Copilot stats loader for testing purposes.
/// Returns pre-configured test data instead of calling Microsoft Graph.
/// </summary>
public class FakeCopilotStatsLoader : ICopilotStatsLoader
{
    private readonly List<CopilotUsageRecord> _stats;

    public FakeCopilotStatsLoader(List<CopilotUsageRecord>? stats = null)
    {
        _stats = stats ?? CreateDefaultTestStats();
    }

    public Task<CopilotStatsResult> GetCopilotUsageStatsAsync()
    {
        var result = new CopilotStatsResult
        {
            Records = _stats,
            Success = true,
            StatusCode = 200
        };
        return Task.FromResult(result);
    }

    private static List<CopilotUsageRecord> CreateDefaultTestStats()
    {
        return
        [
            new CopilotUsageRecord
            {
                UserPrincipalName = "test1@contoso.com",
                LastActivityDate = DateTime.UtcNow.AddDays(-1),
                CopilotChatLastActivityDate = DateTime.UtcNow.AddDays(-2),
                TeamsCopilotLastActivityDate = DateTime.UtcNow.AddDays(-3)
            },
            new CopilotUsageRecord
            {
                UserPrincipalName = "test2@contoso.com",
                LastActivityDate = DateTime.UtcNow.AddDays(-5),
                WordCopilotLastActivityDate = DateTime.UtcNow.AddDays(-7)
            }
        ];
    }
}
