using Common.Engine.Models;

namespace Common.Engine.Services.UserCache;

/// <summary>
/// Pure CSV parser for Microsoft 365 Copilot Usage User Detail reports.
/// Extracted for testability - no external dependencies.
/// </summary>
public static class CopilotUsageCsvParser
{
    // Header column names matched in a single pass over the header line.
    private static ReadOnlySpan<char> HeaderUpn => "User Principal Name";
    private static ReadOnlySpan<char> HeaderLastActivity => "Last Activity Date";
    private static ReadOnlySpan<char> HeaderCopilotChat => "Copilot Chat Last Activity Date";
    private static ReadOnlySpan<char> HeaderTeams => "Microsoft Teams Copilot Last Activity Date";
    private static ReadOnlySpan<char> HeaderWord => "Word Copilot Last Activity Date";
    private static ReadOnlySpan<char> HeaderExcel => "Excel Copilot Last Activity Date";
    private static ReadOnlySpan<char> HeaderPowerPoint => "PowerPoint Copilot Last Activity Date";
    private static ReadOnlySpan<char> HeaderOutlook => "Outlook Copilot Last Activity Date";
    private static ReadOnlySpan<char> HeaderOneNote => "OneNote Copilot Last Activity Date";
    private static ReadOnlySpan<char> HeaderLoop => "Loop Copilot Last Activity Date";

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

        var content = csvContent.AsSpan();
        var contentPos = 0;

        // Pull the first non-empty line as the header.
        int headerStart, headerLength;
        if (!TryTakeNextNonEmptyLine(content, ref contentPos, out headerStart, out headerLength))
        {
            return records;
        }

        var columnIndices = ParseHeaderCore(content.Slice(headerStart, headerLength));

        // No UPN column means the report is malformed - cannot proceed.
        if (columnIndices.UpnIndex < 0)
        {
            return records;
        }

        var maxKnownIndex = MaxKnownIndex(columnIndices);

        // Per-row field offsets (start, length) into `content`. -1 length = absent.
        Span<int> fieldStarts = stackalloc int[10];
        Span<int> fieldLengths = stackalloc int[10];
        Span<int> targets = stackalloc int[10]
        {
            columnIndices.UpnIndex,
            columnIndices.LastActivityIndex,
            columnIndices.CopilotChatIndex,
            columnIndices.TeamsIndex,
            columnIndices.WordIndex,
            columnIndices.ExcelIndex,
            columnIndices.PowerPointIndex,
            columnIndices.OutlookIndex,
            columnIndices.OneNoteIndex,
            columnIndices.LoopIndex
        };

        while (contentPos < content.Length)
        {
            if (!TryTakeNextNonEmptyLine(content, ref contentPos, out var lineStart, out var lineLength))
            {
                break;
            }

            // Reset row state. lengths < 0 means "not captured".
            for (int i = 0; i < fieldLengths.Length; i++)
            {
                fieldLengths[i] = -1;
            }

            var lineEnd = lineStart + lineLength;
            var fieldStart = lineStart;
            var fieldIndex = 0;
            var upnSeen = false;
            for (int pos = lineStart; pos <= lineEnd; pos++)
            {
                var atEnd = pos == lineEnd;
                if (atEnd || content[pos] == ',')
                {
                    var len = pos - fieldStart;
                    for (int t = 0; t < targets.Length; t++)
                    {
                        if (targets[t] == fieldIndex)
                        {
                            fieldStarts[t] = fieldStart;
                            fieldLengths[t] = len;
                            if (t == 0)
                            {
                                upnSeen = true;
                            }
                        }
                    }

                    fieldIndex++;
                    fieldStart = pos + 1;

                    if (atEnd)
                    {
                        break;
                    }

                    // Stop early once we've captured everything we care about.
                    if (upnSeen && fieldIndex > maxKnownIndex)
                    {
                        break;
                    }
                }
            }

            // Original behavior: if the row didn't have enough columns to reach UPN, skip.
            if (!upnSeen)
            {
                continue;
            }

            var trimmedUpn = content.Slice(fieldStarts[0], fieldLengths[0]).Trim();
            if (trimmedUpn.IsEmpty)
            {
                continue;
            }

            records.Add(new CopilotUsageRecord
            {
                UserPrincipalName = trimmedUpn.ToString(),
                LastActivityDate = ParseDateSpanAt(content, fieldStarts, fieldLengths, 1, columnIndices.LastActivityIndex),
                CopilotChatLastActivityDate = ParseDateSpanAt(content, fieldStarts, fieldLengths, 2, columnIndices.CopilotChatIndex),
                TeamsCopilotLastActivityDate = ParseDateSpanAt(content, fieldStarts, fieldLengths, 3, columnIndices.TeamsIndex),
                WordCopilotLastActivityDate = ParseDateSpanAt(content, fieldStarts, fieldLengths, 4, columnIndices.WordIndex),
                ExcelCopilotLastActivityDate = ParseDateSpanAt(content, fieldStarts, fieldLengths, 5, columnIndices.ExcelIndex),
                PowerPointCopilotLastActivityDate = ParseDateSpanAt(content, fieldStarts, fieldLengths, 6, columnIndices.PowerPointIndex),
                OutlookCopilotLastActivityDate = ParseDateSpanAt(content, fieldStarts, fieldLengths, 7, columnIndices.OutlookIndex),
                OneNoteCopilotLastActivityDate = ParseDateSpanAt(content, fieldStarts, fieldLengths, 8, columnIndices.OneNoteIndex),
                LoopCopilotLastActivityDate = ParseDateSpanAt(content, fieldStarts, fieldLengths, 9, columnIndices.LoopIndex)
            });
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
            return CreateEmptyIndices();
        }

        return ParseHeaderCore(headerLine.AsSpan());
    }

    /// <summary>
    /// Safely parse a date value at <paramref name="index"/>, returning null when the index is missing
    /// or the value cannot be parsed.
    /// </summary>
    public static DateTime? ParseDate(string[] values, int index)
    {
        if (index < 0 || index >= values.Length)
        {
            return null;
        }

        return ParseDateSpan(values[index].AsSpan(), index);
    }

    private static CsvColumnIndices ParseHeaderCore(ReadOnlySpan<char> headerLine)
    {
        var indices = CreateEmptyIndices();

        var fieldStart = 0;
        var fieldIndex = 0;
        for (int pos = 0; pos <= headerLine.Length; pos++)
        {
            var atEnd = pos == headerLine.Length;
            if (atEnd || headerLine[pos] == ',')
            {
                var name = headerLine.Slice(fieldStart, pos - fieldStart).Trim();
                if (name.SequenceEqual(HeaderUpn)) indices.UpnIndex = fieldIndex;
                else if (name.SequenceEqual(HeaderLastActivity)) indices.LastActivityIndex = fieldIndex;
                else if (name.SequenceEqual(HeaderCopilotChat)) indices.CopilotChatIndex = fieldIndex;
                else if (name.SequenceEqual(HeaderTeams)) indices.TeamsIndex = fieldIndex;
                else if (name.SequenceEqual(HeaderWord)) indices.WordIndex = fieldIndex;
                else if (name.SequenceEqual(HeaderExcel)) indices.ExcelIndex = fieldIndex;
                else if (name.SequenceEqual(HeaderPowerPoint)) indices.PowerPointIndex = fieldIndex;
                else if (name.SequenceEqual(HeaderOutlook)) indices.OutlookIndex = fieldIndex;
                else if (name.SequenceEqual(HeaderOneNote)) indices.OneNoteIndex = fieldIndex;
                else if (name.SequenceEqual(HeaderLoop)) indices.LoopIndex = fieldIndex;

                fieldIndex++;
                fieldStart = pos + 1;
            }
        }

        return indices;
    }

    private static DateTime? ParseDateSpan(ReadOnlySpan<char> value, int index)
    {
        if (index < 0)
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.IsEmpty)
        {
            return null;
        }

        if (DateTime.TryParse(trimmed, out var date))
        {
            return DateTime.SpecifyKind(date, DateTimeKind.Utc);
        }

        return null;
    }

    private static DateTime? ParseDateSpanAt(
        ReadOnlySpan<char> content,
        ReadOnlySpan<int> starts,
        ReadOnlySpan<int> lengths,
        int slot,
        int index)
    {
        if (index < 0 || lengths[slot] < 0)
        {
            return null;
        }

        return ParseDateSpan(content.Slice(starts[slot], lengths[slot]), index);
    }

    /// <summary>
    /// Advances <paramref name="position"/> past the next line terminator and reports the line's
    /// start offset and length within <paramref name="content"/>. Treats \r\n, \n and \r as line
    /// terminators. Skips empty lines (zero length) to mirror the previous
    /// Split('\n', RemoveEmptyEntries) behavior. Returns false when no more lines are available.
    /// </summary>
    private static bool TryTakeNextNonEmptyLine(ReadOnlySpan<char> content, ref int position, out int lineStart, out int lineLength)
    {
        while (position < content.Length)
        {
            var remaining = content.Slice(position);
            var next = remaining.IndexOfAny('\r', '\n');
            int start = position;
            int length;
            if (next < 0)
            {
                length = remaining.Length;
                position = content.Length;
            }
            else
            {
                length = next;
                var skip = 1;
                if (remaining[next] == '\r' && next + 1 < remaining.Length && remaining[next + 1] == '\n')
                {
                    skip = 2;
                }
                position += next + skip;
            }

            if (length > 0)
            {
                lineStart = start;
                lineLength = length;
                return true;
            }
        }

        lineStart = 0;
        lineLength = 0;
        return false;
    }

    private static CsvColumnIndices CreateEmptyIndices() => new CsvColumnIndices
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

    private static int MaxKnownIndex(CsvColumnIndices c)
    {
        var m = c.UpnIndex;
        if (c.LastActivityIndex > m) m = c.LastActivityIndex;
        if (c.CopilotChatIndex > m) m = c.CopilotChatIndex;
        if (c.TeamsIndex > m) m = c.TeamsIndex;
        if (c.WordIndex > m) m = c.WordIndex;
        if (c.ExcelIndex > m) m = c.ExcelIndex;
        if (c.PowerPointIndex > m) m = c.PowerPointIndex;
        if (c.OutlookIndex > m) m = c.OutlookIndex;
        if (c.OneNoteIndex > m) m = c.OneNoteIndex;
        if (c.LoopIndex > m) m = c.LoopIndex;
        return m;
    }
}

