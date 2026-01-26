using Common.Engine.Models;

namespace Common.Engine.Services.UserCache;

/// <summary>
/// Result from a user data load operation.
/// </summary>
public class UserLoadResult
{
    public List<EnrichedUserInfo> Users { get; set; } = new();
    public string? DeltaToken { get; set; }
}

/// <summary>
/// Copilot usage statistics for a user.
/// </summary>
public class CopilotUserStats
{
    public DateTime? LastActivityDate { get; set; }
    public DateTime? CopilotChatLastActivityDate { get; set; }
    public DateTime? TeamsCopilotLastActivityDate { get; set; }
    public DateTime? WordCopilotLastActivityDate { get; set; }
    public DateTime? ExcelCopilotLastActivityDate { get; set; }
    public DateTime? PowerPointCopilotLastActivityDate { get; set; }
    public DateTime? OutlookCopilotLastActivityDate { get; set; }
    public DateTime? OneNoteCopilotLastActivityDate { get; set; }
    public DateTime? LoopCopilotLastActivityDate { get; set; }
}

/// <summary>
/// Adapter interface for loading user data from an external source (e.g., Microsoft Graph).
/// </summary>
public interface IUserDataLoader
{
    /// <summary>
    /// Load all users from the data source.
    /// </summary>
    /// <returns>All users and a delta token for incremental updates.</returns>
    Task<UserLoadResult> LoadAllUsersAsync();

    /// <summary>
    /// Load only changed users since the last sync using a delta token.
    /// </summary>
    /// <param name="deltaToken">Token from previous sync to get only changes.</param>
    /// <returns>Changed users and new delta token.</returns>
    Task<UserLoadResult> LoadDeltaChangesAsync(string deltaToken);

    /// <summary>
    /// Get Copilot usage statistics for users (optional feature).
    /// </summary>
    /// <returns>Dictionary of UPN to Copilot stats.</returns>
    Task<Dictionary<string, CopilotUserStats>> GetCopilotStatsAsync();

    /// <summary>
    /// Get license information for users.
    /// </summary>
    /// <returns>Dictionary of UPN to whether user has Copilot license.</returns>
    Task<Dictionary<string, bool>> GetLicenseInfoAsync();
}
