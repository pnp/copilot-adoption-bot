using Azure;
using Azure.Data.Tables;

namespace Engine.Models;

/// <summary>
/// Table storage entity for cached user and conversation data.
/// </summary>
public class CachedUserAndConversationData : ITableEntity
{
    public static string PartitionKeyVal => "Users";
    public string PartitionKey { get => PartitionKeyVal; set { return; } }

    /// <summary>
    /// Azure AD ID.
    /// </summary>
    public string RowKey { get; set; } = null!;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    /// <summary>
    /// Gets or sets service URL.
    /// </summary>
    public string ServiceUrl { get; set; } = null!;

    public string ConversationId { get; set; } = null!;
    public string? UserPrincipalName { get; set; } = null;

    /// <summary>
    /// UTC timestamp of the last user-initiated message to the bot, or null if the user
    /// has never replied. Used to compute "engaged users" statistics.
    /// </summary>
    public DateTime? LastInteractionUtc { get; set; }

    /// <summary>
    /// Raw adaptive-card JSON of the most recent card the bot sent to this user.
    /// Persisted so that AI follow-up has the card as context even after an app restart
    /// or scale-out, when the in-memory <c>UserState</c> has been lost. Only the latest
    /// card is kept to keep the row small and scalable.
    /// </summary>
    public string? LastCardJson { get; set; }

    /// <summary>
    /// Template id of the card stored in <see cref="LastCardJson"/>.
    /// </summary>
    public string? LastCardTemplateId { get; set; }

    /// <summary>
    /// Template display name of the card stored in <see cref="LastCardJson"/>.
    /// </summary>
    public string? LastCardTemplateName { get; set; }

    /// <summary>
    /// UTC timestamp at which the card stored in <see cref="LastCardJson"/> was sent.
    /// </summary>
    public DateTime? LastCardSentUtc { get; set; }

    /// <summary>
    /// JSON-serialized trimmed conversation history (user/assistant turn pairs) used as
    /// LLM context by AI follow-up. Persisted so that thread continuity survives an app
    /// restart or scale-out. Capped at the same 20-entry budget enforced by the dialog
    /// to keep the row small and scalable. Null if the user has had no AI exchanges yet.
    /// </summary>
    public string? ConversationHistoryJson { get; set; }
}
