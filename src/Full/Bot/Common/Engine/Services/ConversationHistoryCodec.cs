using System.Text.Json;

namespace Engine.Services;

/// <summary>
/// Pure helper that serializes / deserializes the trimmed (role, message) conversation
/// history used by AI follow-up so it can be persisted as a single string column on
/// <c>CachedUserAndConversationData</c>. Kept out of <see cref="BotConversationCache"/>
/// so the JSON shape can be unit-tested in isolation, per the repo convention of
/// extracting pure helpers from I/O classes.
/// </summary>
public static class ConversationHistoryCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Wire format. Properties are short to keep the persisted column small.
    /// </summary>
    public record HistoryEntry(string Role, string Message);

    public static string Serialize(IEnumerable<(string role, string message)> history)
    {
        ArgumentNullException.ThrowIfNull(history);
        var entries = history.Select(t => new HistoryEntry(t.role ?? string.Empty, t.message ?? string.Empty)).ToList();
        return JsonSerializer.Serialize(entries, Options);
    }

    /// <summary>
    /// Deserialize a persisted history string. Returns an empty list for null / blank
    /// / corrupted input so a bad row never breaks a live conversation.
    /// </summary>
    public static List<(string role, string message)> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<(string role, string message)>();
        }

        try
        {
            var entries = JsonSerializer.Deserialize<List<HistoryEntry>>(json, Options);
            if (entries == null)
            {
                return new List<(string role, string message)>();
            }
            return entries.Select(e => (e.Role ?? string.Empty, e.Message ?? string.Empty)).ToList();
        }
        catch (JsonException)
        {
            return new List<(string role, string message)>();
        }
    }
}
