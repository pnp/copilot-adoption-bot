using Common.Engine;
using Common.Engine.Config;
using Common.Engine.Notifications;
using Common.Engine.Services;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Graph;

namespace Web.Server.Bots;

public class TeamsBot<T>(ConversationState conversationState, UserState userState, T dialog, ILogger<DialogueBot<T>> logger, BotActionsHelper helper, GraphServiceClient graphServiceClient,
    BotConfig configuration, BotConversationCache botConversationCache, IConversationResumeHandler<PendingCardInfo> conversationResumeHandler)
    : DialogueBot<T>(conversationState, userState, dialog, logger) where T : Dialog
{
    public readonly BotConfig _configuration = configuration;

    /// <summary>
    /// New thread with bot. 
    /// </summary>
    protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
    {
        foreach (var member in membersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                var userIdentity = await BotUserUtils.GetBotUserAsync(member, _configuration, graphServiceClient);

                // Is this an Azure AD user?
                if (!userIdentity.IsAzureAdUserId)
                    await turnContext.SendActivityAsync(MessageFactory.Text($"Hi, anonynous user. I only work with Azure AD users in Teams normally..."));

                // Have we spoken before?
                await botConversationCache.PopulateMemCacheIfEmpty();
                var cachedUser = botConversationCache.GetCachedUser(userIdentity.UserId);
                if (cachedUser?.UserPrincipalName == null)
                {
                    // Add current user to conversation reference cache.
                    await botConversationCache.AddConversationReferenceToCache((Activity)turnContext.Activity, userIdentity);

                    cachedUser = botConversationCache.GetCachedUser(userIdentity.UserId);
                    if (cachedUser?.UserPrincipalName == null)
                    {
                        Logger.LogError($"Failed to add new user ID {userIdentity.UserId} to conversation cache.");
                        continue;
                    }

                    // First time meeting a user (new thread). Can be because we've just installed the app. Introduce bot and start a new dialog.
                    await helper.SendBotFirstIntro(turnContext, cancellationToken);
                }
                else
                {
                    Logger.LogDebug($"User {userIdentity.UserId} found in conversation cache.");
                }

                // Resume conversation - get next card to send.
                var (card, nextCardAttachmentToSend) = await conversationResumeHandler.LoadDataAndResumeConversation(cachedUser.UserPrincipalName);
                if (card != null && nextCardAttachmentToSend != null)
                {
                    Logger.LogInformation($"Resuming conversation with user {userIdentity.UserId} by sending next card (card {card.TemplateName}).");
                    var resumeActivity = MessageFactory.Attachment(nextCardAttachmentToSend);
                    await turnContext.SendActivityAsync(resumeActivity, cancellationToken);
                }
                else
                {
                    Logger.LogInformation($"No conversation to resume with user {userIdentity.UserId}.");
                }
            }
        }
    }

    protected override async Task OnTeamsSigninVerifyStateAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Running dialog with signin/verifystate from an Invoke Activity.");

        // The OAuth Prompt needs to see the Invoke Activity in order to complete the login process.

        // Run the Dialog with the new Invoke Activity.
        await Dialog.RunAsync(turnContext, ConversationState.CreateProperty<DialogState>(nameof(DialogState)), cancellationToken);
    }
}
