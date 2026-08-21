using Engine.Notifications;
using Engine.Services;
using Microsoft.Bot.Schema;

namespace Web.Server.Bots;

/// <summary>
/// Conversation resume handler that loads cards from Azure Table Storage.
///
/// <para>
/// Deliberately side-effect free. The previous implementation marked the delivery
/// <c>Success</c> here — <em>before</em> the card had been sent — so any failure between
/// this call and <c>SendActivityAsync</c> (a throttle, a transport error, or the worker
/// being unloaded) permanently recorded an undelivered message as delivered. Status is now
/// written by the caller, after the send actually succeeds.
/// </para>
/// </summary>
public class PendingCardConversationResumeHandler(
    PendingCardLookupService pendingCardLookupService,
    ILogger<PendingCardConversationResumeHandler> logger) : IConversationResumeHandler<PendingCardInfo>
{
    public async Task<(PendingCardInfo?, Attachment)> LoadDeliveryAsync(string chatUserUpn, string batchId, string templateId)
    {
        var card = await pendingCardLookupService.GetDeliveryCardAsync(chatUserUpn, batchId, templateId);

        if (card != null)
        {
            logger.LogDebug("Loaded card '{TemplateName}' for {Upn} in batch {BatchId}",
                card.TemplateName, chatUserUpn, batchId);
            return (card, card.CardAttachment);
        }

        logger.LogWarning("Could not load template {TemplateId} for {Upn} in batch {BatchId}",
            templateId, chatUserUpn, batchId);
        return (null, DefaultWelcomeCard(chatUserUpn));
    }

    public async Task<(PendingCardInfo?, Attachment)> LoadNewestPendingAsync(string chatUserUpn)
    {
        var pendingCard = await pendingCardLookupService.GetLatestPendingCardByUpn(chatUserUpn);

        if (pendingCard != null)
        {
            logger.LogInformation("Found pending card '{TemplateName}' for user {Upn}",
                pendingCard.TemplateName, chatUserUpn);
            return (pendingCard, pendingCard.CardAttachment);
        }

        logger.LogInformation("No pending cards found for user {Upn}, sending default welcome message", chatUserUpn);
        return (null, DefaultWelcomeCard(chatUserUpn));
    }

    private static Attachment DefaultWelcomeCard(string chatUserUpn) =>
        new HeroCard
        {
            Title = "Welcome!",
            Text = "Hello, you have no pending messages at this time."
        }.ToAttachment();
}
