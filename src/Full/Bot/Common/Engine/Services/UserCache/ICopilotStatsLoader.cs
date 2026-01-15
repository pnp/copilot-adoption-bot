using Common.Engine.Models;

namespace Common.Engine.Services.UserCache;

/// <summary>
/// Adapter interface for loading Copilot usage statistics from an external source.
/// </summary>
public interface ICopilotStatsLoader
{
    /// <summary>
    /// Fetch Copilot usage statistics for all users.
    /// </summary>
    /// <returns>Result containing stats records and status information.</returns>
    Task<CopilotStatsResult> GetCopilotUsageStatsAsync();
}
