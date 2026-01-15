using Common.Engine.Config;
using Common.Engine.Models;
using Common.Engine.Services;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

namespace Common.Engine.Notifications;

public class BotConvoResumeManager(ILogger<BotConvoResumeManager> loggerBotConvoResumeManager,
    ILogger<BotAppInstallHelper> loggerBotAppInstallHelper,
    BotConversationCache botConversationCache,
    IServiceProvider serviceProvider,
    GraphServiceClient graphServiceClient, TeamsAppConfig config, IBotFrameworkHttpAdapter adapter) : IBotConvoResumeManager
{
    private const string TeamsBotFrameworkChannelId = "msteams";
    private const string BotIdPrefix = "28:";

    private readonly ILogger _loggerBotConvoResumeManager = loggerBotConvoResumeManager;

    /// <summary>
    /// Resumes a conversation with the specified user in Microsoft Teams, installing the bot app for the user if
    /// necessary.
    /// </summary>
    /// <remarks>If a conversation with the specified user already exists, this method resumes it by sending a
    /// message. If no conversation exists, the bot app is installed for the user and a new conversation is initiated.
    /// The method logs warnings if the user cannot be found or if the bot app cannot be installed. The user must be
    /// licensed for Microsoft Teams for the operation to succeed.</remarks>
    /// <param name="upn">The user principal name (UPN) of the user with whom to resume the conversation. Cannot be null or empty.</param>
    /// <returns>A result object indicating success status and an operation message</returns>
    public async Task<ConversationResumeResult> ResumeConversation(string upn)
    {
        // Get AAD user ID from Graph by looking up user by email
        User? graphUser = null;
        try
        {
            graphUser = await graphServiceClient.Users[upn].GetAsync(op => op.QueryParameters.Select = ["Id"]);
        }
        catch (ODataError ex)
        {
            var message = $"Couldn't get user by UPN '{upn}' - {ex.Message}";
            _loggerBotConvoResumeManager.LogWarning(ex, message);
            return ConversationResumeResult.Failed(message, ex);
        }
        
        if (graphUser?.Id == null)
        {
            var message = $"User {upn} not found or has no ID";
            _loggerBotConvoResumeManager.LogWarning(message);
            return ConversationResumeResult.Failed(message);
        }

        // Do we have a conversation with this user yet?
        if (botConversationCache.ContainsUserId(graphUser.Id))
        {
            return await SendMessageToExistingConversation(graphUser.Id, upn);
        }
        else
        {
            return await InstallBotAndQueueMessage(graphUser.Id, upn);
        }
    }

    /// <summary>
    /// Sends a message to an existing conversation
    /// </summary>
    private async Task<ConversationResumeResult> SendMessageToExistingConversation(string userId, string upn)
    {
        var cachedUser = botConversationCache.GetCachedUser(userId)!;
        var previousConversationReference = CreateConversationReference(cachedUser);

        try
        {
            // Create a scope to resolve scoped services (like PendingCardLookupService)
            using var scope = serviceProvider.CreateScope();
            var conversationResumeHandler = scope.ServiceProvider.GetRequiredService<IConversationResumeHandler<PendingCardInfo>>();
            
            // Continue conversation with the registered "resume conversation" service
            var (data, card) = await conversationResumeHandler.LoadDataAndResumeConversation(upn);
            var resumeActivity = MessageFactory.Attachment(card);

            await ((CloudAdapter)adapter)
                .ContinueConversationAsync(config.GraphConfig.ClientId, previousConversationReference,
                async (turnContext, cancellationToken) =>
                    await turnContext.SendActivityAsync(resumeActivity, cancellationToken), CancellationToken.None);
            
            var result = ConversationResumeResult.MessageSent(upn);
            _loggerBotConvoResumeManager.LogInformation("Conversation resume result: {Status} for user {Upn}", result.Status, upn);
            return result;
        }
        catch (Exception ex)
        {
            var message = $"Error sending message to {upn}: {ex.Message}";
            _loggerBotConvoResumeManager.LogError(ex, message);
            return ConversationResumeResult.Failed(message, ex);
        }
    }

    /// <summary>
    /// Installs the bot app for the user and queues the message for when they open Teams
    /// </summary>
    private async Task<ConversationResumeResult> InstallBotAndQueueMessage(string userId, string upn)
    {
        if (string.IsNullOrEmpty(config.AppCatalogTeamAppId))
        {
            var message = $"Can't install Teams app for bot - no {nameof(config.AppCatalogTeamAppId)} found in configuration";
            _loggerBotConvoResumeManager.LogError(message);
            return ConversationResumeResult.Failed(message);
        }

        var installManager = new BotAppInstallHelper(loggerBotAppInstallHelper, graphServiceClient);
        try
        {
            // Install app and if already installed, trigger a new conversation update.
            // This will then be picked up by the bot and the conversation ID then cached for this user.
            await installManager.InstallBotForUser(userId, config.AppCatalogTeamAppId,
                () => TriggerUserConversationUpdate(userId, config.AppCatalogTeamAppId, installManager));
            
            var result = ConversationResumeResult.AppInstalled(upn);
            _loggerBotConvoResumeManager.LogInformation("Conversation resume result: {Status} for user {Upn}", result.Status, upn);
            return result;
        }
        catch (ODataError ex)
        {
            var message = $"Couldn't install Teams app for user '{userId}' - {ex.Message} - is user licensed for Teams?";
            _loggerBotConvoResumeManager.LogWarning(ex, message);
            return ConversationResumeResult.Failed(message, ex);
        }
    }

    /// <summary>
    /// Creates a conversation reference for resuming a conversation
    /// </summary>
    private ConversationReference CreateConversationReference(CachedUserAndConversationData cachedUser)
    {
        return new ConversationReference()
        {
            ChannelId = TeamsBotFrameworkChannelId,
            Bot = new ChannelAccount() { Id = $"{BotIdPrefix}{config.AppCatalogTeamAppId}" },
            ServiceUrl = cachedUser.ServiceUrl,
            Conversation = new ConversationAccount() { Id = cachedUser.ConversationId },
        };
    }

    async Task TriggerUserConversationUpdate(string userid, string appId, BotAppInstallHelper installManager)
    {
        _loggerBotConvoResumeManager.LogInformation("Triggering new conversation with bot {AppId} for user {UserId}", appId, userid);

        // Docs here: https://docs.microsoft.com/en-us/microsoftteams/platform/graph-api/proactive-bots-and-messages/graph-proactive-bots-and-messages#-retrieve-the-conversation-chatid
        var installedApp = await installManager.GetUserInstalledApp(userid, appId);
        try
        {
            // Calling this will trigger a "conversationUpdate" activity to the bot, assuming the correct callback URL is configured
            // You need to have either NGROK or a public endpoint for this to work
            // When the callback is received, the bot should cache the conversation ID for this user, and then send whatever card or message is needed
            var chat = await graphServiceClient.Users[userid].Teamwork.InstalledApps[installedApp.Id].Chat.GetAsync();
        }
        catch (ODataError ex)
        {
            _loggerBotConvoResumeManager.LogWarning(ex, "Couldn't get chat for user '{UserId}'", userid);
        }
    }
}

public interface IBotConvoResumeManager
{
    public abstract Task<ConversationResumeResult> ResumeConversation(string upn);
}

/// <summary>
/// Result status of a conversation resume operation
/// </summary>
public enum ConversationResumeStatus
{
    /// <summary>
    /// Message was sent successfully to the user
    /// </summary>
    MessageSent,
    
    /// <summary>
    /// Bot app was installed; message will be sent when user opens Teams
    /// </summary>
    AppInstalledPending,
    
    /// <summary>
    /// Operation failed due to an error
    /// </summary>
    Failed
}

/// <summary>
/// Result of a conversation resume operation
/// </summary>
public class ConversationResumeResult
{
    public required ConversationResumeStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }

    /// <summary>
    /// Creates a result for a successfully sent message
    /// </summary>
    public static ConversationResumeResult MessageSent(string upn) =>
        new() { Status = ConversationResumeStatus.MessageSent, Message = $"Message sent successfully to {upn}" };
    
    /// <summary>
    /// Creates a result for when the bot app was installed and message is pending
    /// </summary>
    public static ConversationResumeResult AppInstalled(string upn) =>
        new() { Status = ConversationResumeStatus.AppInstalledPending, Message = $"Bot app installed for {upn}. Message will be sent when user opens the app." };
    
    /// <summary>
    /// Creates a result for a failed operation
    /// </summary>
    public static ConversationResumeResult Failed(string message, Exception? exception = null) =>
        new() { Status = ConversationResumeStatus.Failed, Message = message, Exception = exception };
}
