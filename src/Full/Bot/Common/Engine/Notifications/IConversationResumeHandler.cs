using Microsoft.Bot.Schema;

namespace Engine.Notifications;

public interface IConversationResumeHandler<T>
{
    /// <summary>
    /// Load a specific queued delivery by its batch and template.
    ///
    /// <para>
    /// Used by the queue-driven send path, where the exact delivery is already known. This
    /// must never fall back to "newest pending card for this UPN": a user with more than one
    /// pending delivery would then receive the wrong card.
    /// </para>
    ///
    /// <para>
    /// Implementations are side-effect free - they load only. Delivery status is written by
    /// the caller <em>after</em> the card has actually been sent.
    /// </para>
    /// </summary>
    Task<(T?, Attachment)> LoadDeliveryAsync(string chatUserUpn, string batchId, string templateId);

    /// <summary>
    /// Load the newest pending delivery for a user, for the case where the user opens Teams
    /// after the bot app was installed for them and no queue message is in hand.
    /// Served by the per-user pending index, not by scanning delivery rows.
    /// </summary>
    Task<(T?, Attachment)> LoadNewestPendingAsync(string chatUserUpn);
}
