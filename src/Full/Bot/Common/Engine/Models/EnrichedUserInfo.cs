namespace Common.Engine.Models;

/// <summary>
/// Extended user information with metadata for AI-driven user matching.
/// </summary>
public class EnrichedUserInfo
{
    public string Id { get; set; } = null!;
    public string UserPrincipalName { get; set; } = null!;
    public string? DisplayName { get; set; }
    public string? GivenName { get; set; }
    public string? Surname { get; set; }
    public string? Mail { get; set; }
    public string? Department { get; set; }
    public string? JobTitle { get; set; }
    public string? OfficeLocation { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? State { get; set; }
    public string? CompanyName { get; set; }
    public string? ManagerUpn { get; set; }
    public string? ManagerDisplayName { get; set; }
    public string? EmployeeType { get; set; }
    public DateTimeOffset? HireDate { get; set; }

    /// <summary>
    /// Indicates if the user has been deleted (used in delta queries).
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Indicates if the user has a Microsoft 365 Copilot license assigned.
    /// </summary>
    public bool HasCopilotLicense { get; set; }

    // Copilot usage statistics
    public DateTime? CopilotLastActivityDate { get; set; }
    public DateTime? CopilotChatLastActivityDate { get; set; }
    public DateTime? TeamsCopilotLastActivityDate { get; set; }
    public DateTime? WordCopilotLastActivityDate { get; set; }
    public DateTime? ExcelCopilotLastActivityDate { get; set; }
    public DateTime? PowerPointCopilotLastActivityDate { get; set; }
    public DateTime? OutlookCopilotLastActivityDate { get; set; }
    public DateTime? OneNoteCopilotLastActivityDate { get; set; }
    public DateTime? LoopCopilotLastActivityDate { get; set; }
    public DateTime? LastCopilotStatsUpdate { get; set; }

    /// <summary>
    /// Creates a summary string for AI processing.
    /// </summary>
    public string ToAISummary()
    {
        var parts = new List<string>
        {
            $"UPN: {UserPrincipalName}",
            $"Name: {DisplayName ?? "Unknown"}"
        };

        if (!string.IsNullOrEmpty(JobTitle))
            parts.Add($"Job Title: {JobTitle}");
        if (!string.IsNullOrEmpty(Department))
            parts.Add($"Department: {Department}");
        if (!string.IsNullOrEmpty(OfficeLocation))
            parts.Add($"Office: {OfficeLocation}");
        if (!string.IsNullOrEmpty(City))
            parts.Add($"City: {City}");
        if (!string.IsNullOrEmpty(State))
            parts.Add($"State: {State}");
        if (!string.IsNullOrEmpty(Country))
            parts.Add($"Country: {Country}");
        if (!string.IsNullOrEmpty(CompanyName))
            parts.Add($"Company: {CompanyName}");
        if (!string.IsNullOrEmpty(ManagerDisplayName))
            parts.Add($"Manager: {ManagerDisplayName}");
        if (!string.IsNullOrEmpty(EmployeeType))
            parts.Add($"Employee Type: {EmployeeType}");

        // Add Copilot license information
        parts.Add($"Has Copilot License: {(HasCopilotLicense ? "Yes" : "No")}");

        // Add Copilot activity information
        var copilotActivities = new List<string>();
        if (CopilotLastActivityDate.HasValue)
            copilotActivities.Add($"Overall: {CopilotLastActivityDate.Value:yyyy-MM-dd}");
        if (CopilotChatLastActivityDate.HasValue)
            copilotActivities.Add($"Chat: {CopilotChatLastActivityDate.Value:yyyy-MM-dd}");
        if (TeamsCopilotLastActivityDate.HasValue)
            copilotActivities.Add($"Teams: {TeamsCopilotLastActivityDate.Value:yyyy-MM-dd}");
        if (WordCopilotLastActivityDate.HasValue)
            copilotActivities.Add($"Word: {WordCopilotLastActivityDate.Value:yyyy-MM-dd}");
        if (ExcelCopilotLastActivityDate.HasValue)
            copilotActivities.Add($"Excel: {ExcelCopilotLastActivityDate.Value:yyyy-MM-dd}");
        if (PowerPointCopilotLastActivityDate.HasValue)
            copilotActivities.Add($"PowerPoint: {PowerPointCopilotLastActivityDate.Value:yyyy-MM-dd}");
        if (OutlookCopilotLastActivityDate.HasValue)
            copilotActivities.Add($"Outlook: {OutlookCopilotLastActivityDate.Value:yyyy-MM-dd}");
        if (OneNoteCopilotLastActivityDate.HasValue)
            copilotActivities.Add($"OneNote: {OneNoteCopilotLastActivityDate.Value:yyyy-MM-dd}");
        if (LoopCopilotLastActivityDate.HasValue)
            copilotActivities.Add($"Loop: {LoopCopilotLastActivityDate.Value:yyyy-MM-dd}");

        if (copilotActivities.Count > 0)
        {
            parts.Add($"Copilot Activity: [{string.Join(", ", copilotActivities)}]");
        }

        return string.Join(" | ", parts);
    }
}
