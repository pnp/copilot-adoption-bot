using Microsoft.Graph.Models;

namespace Common.Engine.Models;

/// <summary>
/// Result container for delta query operations.
/// </summary>
internal class DeltaQueryResult
{
    public List<User> Users { get; set; } = new();
    public string? DeltaLink { get; set; }
}

/// <summary>
/// CSV column indices for parsing Copilot usage reports.
/// </summary>
public class CsvColumnIndices
{
    public int UpnIndex { get; set; }
    public int LastActivityIndex { get; set; }
    public int CopilotChatIndex { get; set; }
    public int TeamsIndex { get; set; }
    public int WordIndex { get; set; }
    public int ExcelIndex { get; set; }
    public int PowerPointIndex { get; set; }
    public int OutlookIndex { get; set; }
    public int OneNoteIndex { get; set; }
    public int LoopIndex { get; set; }
}

/// <summary>
/// Represents Copilot usage statistics for a single user.
/// </summary>
public class CopilotUsageRecord
{
    public string UserPrincipalName { get; set; } = null!;
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
/// Result of fetching Copilot usage statistics.
/// </summary>
public class CopilotStatsResult
{
    /// <summary>
    /// The list of Copilot usage records. Empty if fetch failed.
    /// </summary>
    public List<CopilotUsageRecord> Records { get; set; } = new();

    /// <summary>
    /// Whether the fetch was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// HTTP status code from the API response, if applicable.
    /// </summary>
    public int? StatusCode { get; set; }

    /// <summary>
    /// Error message if the fetch failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
