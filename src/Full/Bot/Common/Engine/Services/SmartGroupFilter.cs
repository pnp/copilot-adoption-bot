using System.Text.Json;
using System.Text.Json.Serialization;
using Engine.Models;

namespace Engine.Services;

/// <summary>
/// A structured, deterministic predicate over <see cref="EnrichedUserInfo"/>.
///
/// <para>
/// Smart groups previously matched members by sending <em>every user in the tenant</em> to the
/// model in chunks of 100 and asking it to classify each one. At 150,000 users that is ~1,500
/// chat completions and tens of millions of input tokens <em>per resolution</em>, recurring on
/// a 1-hour cache TTL, and it gave no guarantee that the same description produced the same
/// membership twice.
/// </para>
///
/// <para>
/// Instead the model is asked <b>once</b> to translate the natural-language description into
/// one of these predicates, which the application then evaluates locally over the cached user
/// directory. That is one completion instead of 1,500, membership is deterministic and
/// auditable, and an admin can review the generated rule before it targets 150,000 people.
/// </para>
/// </summary>
public sealed class SmartGroupFilter
{
    /// <summary>All conditions must match.</summary>
    [JsonPropertyName("all")]
    public List<SmartGroupCondition> All { get; set; } = new();

    /// <summary>At least one condition must match (ignored when empty).</summary>
    [JsonPropertyName("any")]
    public List<SmartGroupCondition> Any { get; set; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Fields a filter may reference. Kept explicit so a model cannot invent a field name and
    /// silently produce a filter that matches nobody.
    /// </summary>
    public static readonly string[] SupportedFields =
    [
        "Department", "JobTitle", "OfficeLocation", "City", "Country", "State",
        "CompanyName", "EmployeeType", "DisplayName", "UserPrincipalName",
        "ManagerUpn", "ManagerDisplayName",
        "HasCopilotLicense", "HireDate",
        "CopilotLastActivityDate", "CopilotChatLastActivityDate", "TeamsCopilotLastActivityDate",
        "WordCopilotLastActivityDate", "ExcelCopilotLastActivityDate", "PowerPointCopilotLastActivityDate",
        "OutlookCopilotLastActivityDate", "OneNoteCopilotLastActivityDate", "LoopCopilotLastActivityDate"
    ];

    /// <summary>
    /// Operators a filter may use.
    /// </summary>
    public static readonly string[] SupportedOperators =
    [
        "eq", "neq", "contains", "startsWith", "in",
        "isNull", "isNotNull",
        "olderThanDays", "withinLastDays"
    ];

    public bool IsEmpty => All.Count == 0 && Any.Count == 0;

    /// <summary>
    /// Parse a model-produced filter. Returns null when the payload is unusable, so callers can
    /// fall back rather than silently resolving a group to everybody or nobody.
    /// </summary>
    public static SmartGroupFilter? TryParse(string? json, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Empty filter payload";
            return null;
        }

        var cleaned = StripCodeFence(json);

        SmartGroupFilter? filter;
        try
        {
            filter = JsonSerializer.Deserialize<SmartGroupFilter>(cleaned, JsonOptions);
        }
        catch (JsonException ex)
        {
            error = $"Filter was not valid JSON: {ex.Message}";
            return null;
        }

        if (filter == null || filter.IsEmpty)
        {
            error = "Filter contained no conditions";
            return null;
        }

        foreach (var condition in filter.All.Concat(filter.Any))
        {
            if (!SupportedFields.Contains(condition.Field, StringComparer.OrdinalIgnoreCase))
            {
                error = $"Unsupported field '{condition.Field}'";
                return null;
            }

            if (!SupportedOperators.Contains(condition.Op, StringComparer.OrdinalIgnoreCase))
            {
                error = $"Unsupported operator '{condition.Op}'";
                return null;
            }
        }

        return filter;
    }

    /// <summary>
    /// Models frequently wrap JSON in a markdown code fence.
    /// </summary>
    private static string StripCodeFence(string value)
    {
        var span = value.AsSpan().Trim();
        if (!span.StartsWith("```")) return span.ToString();

        var firstNewline = span.IndexOf('\n');
        if (firstNewline < 0) return span.ToString();

        span = span[(firstNewline + 1)..];
        var fenceEnd = span.LastIndexOf("```".AsSpan());
        if (fenceEnd >= 0) span = span[..fenceEnd];

        return span.Trim().ToString();
    }

    /// <summary>
    /// Evaluate the filter against a user.
    /// </summary>
    public bool Matches(EnrichedUserInfo user, DateTime utcNow)
    {
        foreach (var condition in All)
        {
            if (!condition.Matches(user, utcNow)) return false;
        }

        if (Any.Count > 0)
        {
            var anyMatched = false;
            foreach (var condition in Any)
            {
                if (condition.Matches(user, utcNow)) { anyMatched = true; break; }
            }
            if (!anyMatched) return false;
        }

        return true;
    }

    /// <summary>
    /// Human-readable rendering, so an admin can review what will be targeted.
    /// </summary>
    public string Describe()
    {
        var parts = new List<string>();
        if (All.Count > 0) parts.Add("ALL of: " + string.Join(" AND ", All.Select(c => c.Describe())));
        if (Any.Count > 0) parts.Add("ANY of: " + string.Join(" OR ", Any.Select(c => c.Describe())));
        return string.Join("; ", parts);
    }
}

/// <summary>
/// A single field/operator/value condition.
/// </summary>
public sealed class SmartGroupCondition
{
    [JsonPropertyName("field")]
    public string Field { get; set; } = string.Empty;

    [JsonPropertyName("op")]
    public string Op { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public JsonElement? Value { get; set; }

    public bool Matches(EnrichedUserInfo user, DateTime utcNow)
    {
        var actual = UserFieldAccessor.Get(user, Field);

        return Op.ToLowerInvariant() switch
        {
            "isnull" => actual is null,
            "isnotnull" => actual is not null,
            "eq" => CompareEquals(actual),
            "neq" => !CompareEquals(actual),
            "contains" => AsString(actual)?.Contains(ValueAsString() ?? string.Empty, StringComparison.OrdinalIgnoreCase) == true,
            "startswith" => AsString(actual)?.StartsWith(ValueAsString() ?? string.Empty, StringComparison.OrdinalIgnoreCase) == true,
            "in" => ValueAsList().Any(v => string.Equals(v, AsString(actual), StringComparison.OrdinalIgnoreCase)),
            "olderthandays" => IsOlderThanDays(actual, utcNow),
            "withinlastdays" => IsWithinLastDays(actual, utcNow),
            _ => false
        };
    }

    private bool CompareEquals(object? actual)
    {
        if (actual is bool b)
        {
            var expected = Value?.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => bool.TryParse(Value.Value.GetString(), out var parsed) && parsed,
                _ => (bool?)null
            };
            return expected.HasValue && expected.Value == b;
        }

        return string.Equals(AsString(actual), ValueAsString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True when the date is absent or older than N days. "Hasn't used Copilot in 30 days"
    /// must include users who have never used it at all.
    /// </summary>
    private bool IsOlderThanDays(object? actual, DateTime utcNow)
    {
        if (!TryGetDays(out var days)) return false;
        if (actual is not DateTime date) return actual is null;
        return date < utcNow.AddDays(-days);
    }

    private bool IsWithinLastDays(object? actual, DateTime utcNow)
    {
        if (!TryGetDays(out var days)) return false;
        if (actual is not DateTime date) return false;
        return date >= utcNow.AddDays(-days);
    }

    private bool TryGetDays(out int days)
    {
        days = 0;
        if (Value is not { } v) return false;

        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out days)) return true;
        if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out days)) return true;
        return false;
    }

    private static string? AsString(object? value) => value switch
    {
        null => null,
        string s => s,
        DateTime d => d.ToString("yyyy-MM-dd"),
        bool b => b.ToString(),
        _ => value.ToString()
    };

    private string? ValueAsString() => Value?.ValueKind switch
    {
        JsonValueKind.String => Value.Value.GetString(),
        JsonValueKind.Number => Value.Value.ToString(),
        JsonValueKind.True => "True",
        JsonValueKind.False => "False",
        _ => null
    };

    private List<string> ValueAsList()
    {
        var results = new List<string>();
        if (Value is not { ValueKind: JsonValueKind.Array } array) return results;

        foreach (var item in array.EnumerateArray())
        {
            var s = item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString();
            if (!string.IsNullOrEmpty(s)) results.Add(s);
        }

        return results;
    }

    public string Describe() => $"{Field} {Op} {ValueAsString() ?? (Value?.ToString() ?? string.Empty)}".TrimEnd();
}

/// <summary>
/// Maps a supported field name onto the corresponding <see cref="EnrichedUserInfo"/> value.
/// An explicit switch rather than reflection, so only whitelisted fields are reachable.
/// </summary>
internal static class UserFieldAccessor
{
    public static object? Get(EnrichedUserInfo u, string field) => field.ToLowerInvariant() switch
    {
        "department" => u.Department,
        "jobtitle" => u.JobTitle,
        "officelocation" => u.OfficeLocation,
        "city" => u.City,
        "country" => u.Country,
        "state" => u.State,
        "companyname" => u.CompanyName,
        "employeetype" => u.EmployeeType,
        "displayname" => u.DisplayName,
        "userprincipalname" => u.UserPrincipalName,
        "managerupn" => u.ManagerUpn,
        "managerdisplayname" => u.ManagerDisplayName,
        "hascopilotlicense" => u.HasCopilotLicense,
        "hiredate" => u.HireDate?.UtcDateTime,
        "copilotlastactivitydate" => u.CopilotLastActivityDate,
        "copilotchatlastactivitydate" => u.CopilotChatLastActivityDate,
        "teamscopilotlastactivitydate" => u.TeamsCopilotLastActivityDate,
        "wordcopilotlastactivitydate" => u.WordCopilotLastActivityDate,
        "excelcopilotlastactivitydate" => u.ExcelCopilotLastActivityDate,
        "powerpointcopilotlastactivitydate" => u.PowerPointCopilotLastActivityDate,
        "outlookcopilotlastactivitydate" => u.OutlookCopilotLastActivityDate,
        "onenotecopilotlastactivitydate" => u.OneNoteCopilotLastActivityDate,
        "loopcopilotlastactivitydate" => u.LoopCopilotLastActivityDate,
        _ => null
    };
}
