using Common.Engine.Notifications;
using Common.Engine.Services;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;

namespace Web.Server.Bots;

/// <summary>
/// Conversation resume handler that sends pending cards from Azure Table Storage
/// </summary>
public class PendingCardConversationResumeHandler(
    PendingCardLookupService pendingCardLookupService,
    MessageTemplateService messageTemplateService,
    ILogger<PendingCardConversationResumeHandler> logger) : IConversationResumeHandler<PendingCardInfo>
{
    public async Task<(PendingCardInfo?, Attachment)> LoadDataAndResumeConversation(string chatUserUpn)
    {
        logger.LogInformation($"Looking for pending card for user {chatUserUpn}");

        var pendingCard = await pendingCardLookupService.GetLatestPendingCardByUpn(chatUserUpn);

        if (pendingCard != null)
        {
            logger.LogInformation($"Found pending card '{pendingCard.TemplateName}' for user {chatUserUpn}");
            
            // Update the message log status to Success since we're about to send it
            await messageTemplateService.UpdateMessageLogStatus(pendingCard.MessageLogId, "Success");
            logger.LogInformation($"Updated message log {pendingCard.MessageLogId} to Success status");
            
            return (pendingCard, pendingCard.CardAttachment);
        }
        else
        {
            logger.LogInformation($"No pending cards found for user {chatUserUpn}, sending default welcome message");
            
            // Return a default welcome card if no pending card exists
            var defaultCard = new HeroCard
            {
                Title = "Welcome!",
                Text = $"Hello {chatUserUpn}, you have no pending messages at this time."
            }.ToAttachment();

            return (null, defaultCard);
        }
    }
}
