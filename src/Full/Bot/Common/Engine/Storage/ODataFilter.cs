namespace Common.Engine.Storage;

/// <summary>
/// Helpers for safely composing OData filter clauses used against Azure Table Storage.
/// Azure Table OData uses single quotes for string literals; an unescaped apostrophe inside
/// a value (for example UPN <c>o'connor@contoso.com</c>) breaks the filter and is a query
/// injection risk. <see cref="EscapeLiteral"/> doubles single quotes per the OData spec.
/// </summary>
public static class ODataFilter
{
    /// <summary>
    /// Escapes a string for safe inclusion in a single-quoted OData literal.
    /// </summary>
    /// <param name="value">Raw string value (may contain apostrophes).</param>
    /// <returns>Value with every <c>'</c> replaced by <c>''</c>.</returns>
    public static string EscapeLiteral(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Replace("'", "''");
    }
}
