using Engine.Config;
using Engine.Models;
using Engine.Services;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

namespace Engine.Notifications;

public class BotConvoResumeManager(ILogger<BotConvoResumeManager> loggerBotConvoResumeManager,
    ILogger<BotAppInstallHelper> loggerBotAppInstallHelper,
    BotConversationCache botConversationCache,
    IServiceProvider serviceProvider,
    GraphServiceClient graphServiceClient, TeamsAppConfig config, IBotFrameworkHttpAdapter adapter,
    CachedUserService cachedUserService) : IBotConvoResumeManager
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
    /// <param name="batchId">Batch the delivery belongs to, so the exact card is loaded.</param>
    /// <param name="templateId">Template to render for this delivery.</param>
    /// <returns>A result object indicating success status and an operation message</returns>
    public async Task<ConversationResumeResult> ResumeConversation(string upn, string batchId, string templateId)
    {
        // Resolve the recipient's AAD object id. Prefer the user cache: the mapping is already
        // stored there keyed by UPN, so hitting Graph for it once per send was 150,000 avoidable
        // calls a day on the most throttle-sensitive path in the system.
        string? userId = null;
        try
        {
            var cached = await cachedUserService.GetUserWithMetadataAsync(upn);
            userId = cached?.Id;
        }
        catch (Exception ex)
        {
            _loggerBotConvoResumeManager.LogDebug(ex, "User cache lookup failed for {Upn}; falling back to Graph", upn);
        }

        if (string.IsNullOrEmpty(userId))
        {
            // Cache miss - a user synced since the last delta, or an unsynced tenant.
            User? graphUser = null;
            try
            {
                graphUser = await graphServiceClient.Users[upn].GetAsync(op => op.QueryParameters.Select = ["Id"]);
            }
            catch (ODataError ex) when (IsTransient(ex))
            {
                var transientMessage = $"Transient Graph error resolving '{upn}' - {ex.Message}";
                _loggerBotConvoResumeManager.LogWarning(ex, transientMessage);
                return ConversationResumeResult.Transient(transientMessage, ex);
            }
            catch (ODataError ex)
            {
                var message = $"Couldn't get user by UPN '{upn}' - {ex.Message}";
                _loggerBotConvoResumeManager.LogWarning(ex, message);
                return ConversationResumeResult.Failed(message, ex);
            }

            userId = graphUser?.Id;
        }

        if (string.IsNullOrEmpty(userId))
        {
            var message = $"User {upn} not found or has no ID";
            _loggerBotConvoResumeManager.LogWarning(message);
            return ConversationResumeResult.Failed(message);
        }

        // Do we have a conversation with this user yet? This must consult durable storage,
        // not just an in-memory cache: on a cold start the cache is empty, and treating every
        // user as "never seen" sends them all down the app-install path and silently drops
        // their nudge.
        var cachedUser = await botConversationCache.GetCachedUserAsync(userId);
        if (cachedUser != null)
        {
            return await SendMessageToExistingConversation(cachedUser, userId, upn, batchId, templateId);
        }

        return await InstallBotAndQueueMessage(userId, upn);
    }

    /// <summary>
    /// Graph/transport failures that should be retried rather than recorded as a permanent
    /// delivery failure.
    /// </summary>
    private static bool IsTransient(ODataError ex) =>
        ex.ResponseStatusCode == 429 ||
        ex.ResponseStatusCode == 503 ||
        ex.ResponseStatusCode == 504 ||
        ex.ResponseStatusCode == 500;

    /// <summary>
    /// Sends a message to an existing conversation
    /// </summary>
    private async Task<ConversationResumeResult> SendMessageToExistingConversation(
        CachedUserAndConversationData cachedUser, string userId, string upn, string batchId, string templateId)
    {
        var previousConversationReference = CreateConversationReference(cachedUser);

        try
        {
            // Create a scope to resolve scoped services (like PendingCardLookupService)
            using var scope = serviceProvider.CreateScope();
            var conversationResumeHandler = scope.ServiceProvider.GetRequiredService<IConversationResumeHandler<PendingCardInfo>>();

            // Load the exact delivery identified by the queue message - never "newest pending
            // for this UPN", which would send the wrong card to a user with several queued.
            var (data, card) = await conversationResumeHandler.LoadDeliveryAsync(upn, batchId, templateId);
            var resumeActivity = MessageFactory.Attachment(card);

            await ((CloudAdapter)adapter)
                .ContinueConversationAsync(config.GraphConfig.ClientId, previousConversationReference,
                async (turnContext, cancellationToken) =>
                    await turnContext.SendActivityAsync(resumeActivity, cancellationToken), CancellationToken.None);

            // Persist the template reference for AI follow-up context. Only after the send
            // succeeded, so we never record context for a card the user never received.
            if (data != null)
            {
                try
                {
                    await botConversationCache.SetLastCardAsync(
                        userId,
                        data.TemplateId,
                        data.TemplateName,
                        data.CardJson,
                        data.SentDate);
                }
                catch (Exception persistEx)
                {
                    _loggerBotConvoResumeManager.LogWarning(persistEx,
                        "Failed to persist last card for {UserId} after proactive send", userId);
                }
            }

            var result = ConversationResumeResult.MessageSent(upn);
            _loggerBotConvoResumeManager.LogDebug("Conversation resume result: {Status} for user {Upn}", result.Status, upn);
            return result;
        }
        catch (Exception ex)
        {
            // Transport-level failures here are typically throttling or a transient Bot
            // Framework error, so keep the delivery queued rather than dropping the nudge.
            var message = $"Error sending message to {upn}: {ex.Message}";
            _loggerBotConvoResumeManager.LogError(ex, message);
            return ConversationResumeResult.Transient(message, ex);
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
            // IMPORTANT: Also make sure the bot endpoint is configured correctly in the Azure Bot registration
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
    public abstract Task<ConversationResumeResult> ResumeConversation(string upn, string batchId, string templateId);
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
    /// Operation failed permanently (user not found, not licensed, app not installable).
    /// Retrying will not help.
    /// </summary>
    Failed,

    /// <summary>
    /// Operation failed for a transient reason (throttling, timeout, transport error).
    /// The delivery should be retried rather than recorded as failed.
    /// </summary>
    TransientFailure
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
    /// Creates a result for a permanent failure
    /// </summary>
    public static ConversationResumeResult Failed(string message, Exception? exception = null) =>
        new() { Status = ConversationResumeStatus.Failed, Message = message, Exception = exception };

    /// <summary>
    /// Creates a result for a transient failure that should be retried.
    /// </summary>
    public static ConversationResumeResult Transient(string message, Exception? exception = null) =>
        new() { Status = ConversationResumeStatus.TransientFailure, Message = message, Exception = exception };
}
