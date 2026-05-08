using Common.Engine.Models;

namespace Common.Engine.Services.UserCache;

/// <summary>
/// Pure CSV parser for Microsoft 365 Copilot Usage User Detail reports.
/// Extracted for testability - no external dependencies.
/// </summary>
public static class CopilotUsageCsvParser
{
    /// <summary>
    /// Parses CSV content from the Copilot usage report into a list of <see cref="CopilotUsageRecord"/>.
    /// Returns an empty list when content is null/empty or contains only a header.
    /// </summary>
    public static List<CopilotUsageRecord> Parse(string? csvContent)
    {
        var records = new List<CopilotUsageRecord>();

        if (string.IsNullOrWhiteSpace(csvContent))
        {
            return records;
        }

        // Normalize line endings then split. Skip empty lines.
        var lines = csvContent.Replace("\r\n", "\n").Replace("\r", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length < 2)
        {
            return records;
        }

        var columnIndices = ParseHeader(lines[0]);

        // No UPN column means the report is malformed - cannot proceed.
        if (columnIndices.UpnIndex < 0)
        {
            return records;
        }

        for (int i = 1; i < lines.Length; i++)
        {
            var values = lines[i].Split(',');
            if (values.Length <= columnIndices.UpnIndex)
                continue;

            var upn = values[columnIndices.UpnIndex]?.Trim();
            if (string.IsNullOrWhiteSpace(upn))
                continue;

            var record = new CopilotUsageRecord
            {
                UserPrincipalName = upn,
                LastActivityDate = ParseDate(values, columnIndices.LastActivityIndex),
                CopilotChatLastActivityDate = ParseDate(values, columnIndices.CopilotChatIndex),
                TeamsCopilotLastActivityDate = ParseDate(values, columnIndices.TeamsIndex),
                WordCopilotLastActivityDate = ParseDate(values, columnIndices.WordIndex),
                ExcelCopilotLastActivityDate = ParseDate(values, columnIndices.ExcelIndex),
                PowerPointCopilotLastActivityDate = ParseDate(values, columnIndices.PowerPointIndex),
                OutlookCopilotLastActivityDate = ParseDate(values, columnIndices.OutlookIndex),
                OneNoteCopilotLastActivityDate = ParseDate(values, columnIndices.OneNoteIndex),
                LoopCopilotLastActivityDate = ParseDate(values, columnIndices.LoopIndex)
            };

            records.Add(record);
        }

        return records;
    }

    /// <summary>
    /// Parses the Copilot CSV header to map known columns to their index.
    /// Unknown columns are returned as -1.
    /// </summary>
    public static CsvColumnIndices ParseHeader(string headerLine)
    {
        if (headerLine == null)
        {
            return new CsvColumnIndices
            {
                UpnIndex = -1,
                LastActivityIndex = -1,
                CopilotChatIndex = -1,
                TeamsIndex = -1,
                WordIndex = -1,
                ExcelIndex = -1,
                PowerPointIndex = -1,
                OutlookIndex = -1,
                OneNoteIndex = -1,
                LoopIndex = -1
            };
        }

        var headers = headerLine.Split(',').Select(h => h.Trim()).ToArray();

        return new CsvColumnIndices
        {
            UpnIndex = Array.IndexOf(headers, "User Principal Name"),
            LastActivityIndex = Array.IndexOf(headers, "Last Activity Date"),
            CopilotChatIndex = Array.IndexOf(headers, "Copilot Chat Last Activity Date"),
            TeamsIndex = Array.IndexOf(headers, "Microsoft Teams Copilot Last Activity Date"),
            WordIndex = Array.IndexOf(headers, "Word Copilot Last Activity Date"),
            ExcelIndex = Array.IndexOf(headers, "Excel Copilot Last Activity Date"),
            PowerPointIndex = Array.IndexOf(headers, "PowerPoint Copilot Last Activity Date"),
            OutlookIndex = Array.IndexOf(headers, "Outlook Copilot Last Activity Date"),
            OneNoteIndex = Array.IndexOf(headers, "OneNote Copilot Last Activity Date"),
            LoopIndex = Array.IndexOf(headers, "Loop Copilot Last Activity Date")
        };
    }

    /// <summary>
    /// Safely parse a date value at <paramref name="index"/>, returning null when the index is missing
    /// or the value cannot be parsed.
    /// </summary>
    public static DateTime? ParseDate(string[] values, int index)
    {
        if (index < 0 || index >= values.Length)
            return null;

        var value = values[index].Trim();
        if (string.IsNullOrEmpty(value))
            return null;

        if (DateTime.TryParse(value, out var date))
        {
            return DateTime.SpecifyKind(date, DateTimeKind.Utc);
        }

        return null;
    }
}
