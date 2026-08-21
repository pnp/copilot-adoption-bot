using System.Text;
using System.Text.Json;

namespace Engine.Services;

/// <summary>
/// Pure helpers that bound what gets sent to the model on the AI follow-up path.
/// Extracted from <see cref="AIFoundryService"/> so the budgeting rules can be unit-tested
/// without an Azure AI Foundry endpoint, per the repo's pure-helper convention.
/// </summary>
public static class AIPromptBudget
{
    /// <summary>
    /// Reduce an adaptive card to a short plain-text digest suitable for prompt context.
    ///
    /// <para>
    /// Nudge templates are 7-8 KB of JSON and the bundled intro cards are ~94 KB because they
    /// embed base64 images. Passing the raw card as system-prompt context is both a large
    /// per-turn token cost and a prompt-injection surface, so only the card's visible text is
    /// extracted, then truncated to <paramref name="maxChars"/>.
    /// </para>
    /// </summary>
    public static string SummariseCard(string? cardJson, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(cardJson)) return string.Empty;
        if (maxChars <= 0) return string.Empty;

        var text = new StringBuilder();

        try
        {
            using var doc = JsonDocument.Parse(cardJson);
            CollectText(doc.RootElement, text, maxChars);
        }
        catch (JsonException)
        {
            // Not valid JSON - fall back to the raw string, still budgeted.
            return Truncate(cardJson.Trim(), maxChars);
        }

        return Truncate(text.ToString().Trim(), maxChars);
    }

    /// <summary>
    /// Walk the card looking only for human-readable text, ignoring images, styling and
    /// structural noise.
    /// </summary>
    private static void CollectText(JsonElement element, StringBuilder text, int maxChars)
    {
        if (text.Length >= maxChars) return;

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    // "text" and "title" carry the card's visible copy; "url"/"data" carry
                    // base64 images and payloads we never want in a prompt.
                    if (property.NameEquals("text") || property.NameEquals("title"))
                    {
                        if (property.Value.ValueKind == JsonValueKind.String)
                        {
                            var value = property.Value.GetString();
                            if (!string.IsNullOrWhiteSpace(value))
                            {
                                if (text.Length > 0) text.Append(' ');
                                text.Append(value.Trim());
                                if (text.Length >= maxChars) return;
                            }
                            continue;
                        }
                    }

                    CollectText(property.Value, text, maxChars);
                    if (text.Length >= maxChars) return;
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectText(item, text, maxChars);
                    if (text.Length >= maxChars) return;
                }
                break;
        }
    }

    /// <summary>
    /// Keep the most recent turns that fit inside a character budget, preserving order.
    /// The dialog caps history at 20 entries, but 20 long turns can still be tens of
    /// thousands of tokens, so the budget is applied by size as well as by count.
    /// </summary>
    public static List<(string role, string message)> TrimHistory(
        IReadOnlyList<(string role, string message)>? history, int maxChars)
    {
        var result = new List<(string role, string message)>();
        if (history == null || history.Count == 0 || maxChars <= 0) return result;

        var budget = 0;

        // Walk backwards so the newest exchanges are the ones retained.
        for (var i = history.Count - 1; i >= 0; i--)
        {
            var (role, message) = history[i];
            var length = message?.Length ?? 0;

            if (budget + length > maxChars) break;

            budget += length;
            result.Add((role, message ?? string.Empty));
        }

        result.Reverse();
        return result;
    }

    private static string Truncate(string value, int maxChars) =>
        value.Length <= maxChars ? value : value[..maxChars];
}
