using Azure;
using Engine.Config;
using Engine.Models;
using Engine.Services;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using System.Collections.Concurrent;

namespace Engine;


public class BotConversationCache : TableStorageManager, IBotInteractionSource
{
    const string TABLE_NAME = "ConversationCache";
    private readonly GraphServiceClient _graphServiceClient;
    private readonly ILogger<BotConversationCache> _logger;
    private ConcurrentDictionary<string, CachedUserAndConversationData> _userIdConversationCache = new();

    public BotConversationCache(GraphServiceClient graphServiceClient, AppConfig appConfig, ILogger<BotConversationCache> logger) : base(appConfig.StorageAuthConfig ?? throw new ArgumentNullException(nameof(appConfig.StorageAuthConfig)), logger)
    {
        _graphServiceClient = graphServiceClient;
        _logger = logger;
        // Dev only: make sure the Azure Storage emulator is running or this will fail
    }

    public async Task PopulateMemCacheIfEmpty()
    {
        if (_userIdConversationCache.Count > 0) return;

        _logger.LogInformation("Populating conversation cache from table storage");
        var client = await base.GetTableClient(TABLE_NAME);

        int count = 0;
        await foreach (var qEntity in client.QueryAsync<CachedUserAndConversationData>(
            filter: $"PartitionKey eq '{CachedUserAndConversationData.PartitionKeyVal}'"))
        {
            _userIdConversationCache.AddOrUpdate(qEntity.RowKey, qEntity, (key, newValue) => qEntity);
            count++;
        }
        _logger.LogInformation("Populated conversation cache with {Count} entries", count);
    }

    public async Task RemoveFromCache(string aadObjectId)
    {
        _userIdConversationCache.TryRemove(aadObjectId, out _);
        var client = await base.GetTableClient(TABLE_NAME);
        try
        {
            await client.DeleteEntityAsync(CachedUserAndConversationData.PartitionKeyVal, aadObjectId);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Already gone - ignore.
        }
    }

    /// <summary>
    /// App installed for user &amp; now we have a conversation reference to cache for future chat threads.
    /// </summary>
    public async Task AddConversationReferenceToCache(Activity activity, BotUser botUser)
    {
        var conversationReference = activity.GetConversationReference();
        await AddOrUpdateUserAndConversationId(conversationReference, botUser, activity.ServiceUrl, _graphServiceClient);
    }

    internal async Task AddOrUpdateUserAndConversationId(ConversationReference conversationReference, BotUser botUser, string serviceUrl, GraphServiceClient graphClient)
    {
        CachedUserAndConversationData? u;
        var client = await base.GetTableClient(TABLE_NAME);

        if (!_userIdConversationCache.TryGetValue(botUser.UserId, out u))
        {
            // Have not got in memory cache - check table storage (async).
            try
            {
                var entityResponse = await client.GetEntityAsync<CachedUserAndConversationData>(
                    CachedUserAndConversationData.PartitionKeyVal, botUser.UserId);
                u = entityResponse.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                u = null;
            }

            if (u == null)
            {
                string? upn = null;
                if (botUser.IsAzureAdUserId)
                {
                    // Get UPN from Graph
                    var user = await graphClient.Users[botUser.UserId].GetAsync(op => op.QueryParameters.Select = ["userPrincipalName"]);
                    upn = user?.UserPrincipalName ?? throw new ArgumentNullException($"No userPrincipalName for {nameof(conversationReference.User.AadObjectId)} '{conversationReference.User.AadObjectId}'");
                }

                u = new CachedUserAndConversationData
                {
                    RowKey = botUser.UserId,
                    ServiceUrl = serviceUrl,
                    UserPrincipalName = upn,
                    ConversationId = conversationReference.Conversation.Id
                };

                try
                {
                    await client.AddEntityAsync(u);
                }
                catch (RequestFailedException ex) when (ex.Status == 409)
                {
                    // Concurrent add - re-read and use the existing entity.
                    var refreshed = await client.GetEntityAsync<CachedUserAndConversationData>(
                        CachedUserAndConversationData.PartitionKeyVal, botUser.UserId);
                    u = refreshed.Value;
                }
            }
        }

        // Update memory cache
        _userIdConversationCache.AddOrUpdate(botUser.UserId, u, (key, newValue) => u);
    }


    public async Task<List<CachedUserAndConversationData>> GetCachedUsers()
    {
        await PopulateMemCacheIfEmpty();
        return _userIdConversationCache.Values.ToList();
    }

    /// <inheritdoc />
    public Task<List<CachedUserAndConversationData>> GetCachedUsersAsync() => GetCachedUsers();

    public CachedUserAndConversationData? GetCachedUser(string aadObjectId)
    {
        // Use direct dictionary lookup (O(1)) instead of a linear scan over Values.
        return _userIdConversationCache.TryGetValue(aadObjectId, out var u) ? u : null;
    }

    public bool ContainsUserId(string aadId)
    {
        return _userIdConversationCache.ContainsKey(aadId);
    }

    /// <summary>
    /// Records that the user with the given AAD object id has sent a message to the bot.
    /// Updates the entity's <see cref="CachedUserAndConversationData.LastInteractionUtc"/>
    /// in both table storage and the in-memory cache. No-op if the user is not yet cached
    /// (the welcome flow caches them first).
    /// </summary>
    public async Task RecordUserInteractionAsync(string aadObjectId)
    {
        if (string.IsNullOrWhiteSpace(aadObjectId)) return;

        await PopulateMemCacheIfEmpty();

        if (!_userIdConversationCache.TryGetValue(aadObjectId, out var existing) || existing == null)
        {
            // User isn't cached yet (e.g. a message before the welcome flow finished). Skip.
            return;
        }

        existing.LastInteractionUtc = DateTime.UtcNow;

        try
        {
            var client = await base.GetTableClient(TABLE_NAME);
            await client.UpdateEntityAsync(existing, ETag.All, Azure.Data.Tables.TableUpdateMode.Merge);
            _userIdConversationCache.AddOrUpdate(aadObjectId, existing, (_, _) => existing);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogWarning(ex, "Failed to record user interaction for {AadObjectId}", aadObjectId);
        }
    }

    /// <summary>
    /// Persists the JSON of the most recent adaptive card the bot sent to this user.
    /// Only the latest card is kept (overwritten on every call) so the row stays small
    /// and the AI follow-up context remains bounded and scalable. Survives app restarts
    /// and scale-out unlike the in-memory <c>UserState</c>.
    /// </summary>
    /// <param name="aadObjectId">AAD object id of the recipient.</param>
    /// <param name="templateId">Template id of the card that was sent.</param>
    /// <param name="templateName">Template display name of the card that was sent.</param>
    /// <param name="cardJson">Raw adaptive-card JSON that was sent.</param>
    /// <param name="sentUtc">UTC timestamp at which the card was sent.</param>
    public async Task SetLastCardAsync(string aadObjectId, string templateId, string templateName, string cardJson, DateTime sentUtc)
    {
        if (string.IsNullOrWhiteSpace(aadObjectId)) return;
        ArgumentException.ThrowIfNullOrEmpty(cardJson);

        await PopulateMemCacheIfEmpty();

        if (!_userIdConversationCache.TryGetValue(aadObjectId, out var existing) || existing == null)
        {
            // No conversation reference yet - the welcome flow hasn't run. Without a
            // ConversationId we can't address this user from a hosted service anyway,
            // so skip persisting the card until they've been cached.
            _logger.LogDebug("Skipped SetLastCardAsync for {AadObjectId}: user not yet in conversation cache", aadObjectId);
            return;
        }

        existing.LastCardJson = cardJson;
        existing.LastCardTemplateId = templateId;
        existing.LastCardTemplateName = templateName;
        existing.LastCardSentUtc = sentUtc;

        try
        {
            var client = await base.GetTableClient(TABLE_NAME);
            await client.UpdateEntityAsync(existing, ETag.All, Azure.Data.Tables.TableUpdateMode.Merge);
            _userIdConversationCache.AddOrUpdate(aadObjectId, existing, (_, _) => existing);
        }
        catch (RequestFailedException ex)
        {
            // Persistence failure must never break the user-facing send path; log and
            // keep the updated in-memory copy so the current process at least benefits.
            _logger.LogWarning(ex, "Failed to persist last card for {AadObjectId}", aadObjectId);
            _userIdConversationCache.AddOrUpdate(aadObjectId, existing, (_, _) => existing);
        }
    }

    /// <summary>
    /// Persists the trimmed conversation history (role, message pairs) used as LLM
    /// context for AI follow-up. Survives app restarts / scale-out so users keep their
    /// thread continuity. The history is expected to already be bounded by the caller
    /// (the dialog caps it at 20 entries) to keep the row small and scalable.
    /// </summary>
    /// <param name="aadObjectId">AAD object id of the recipient.</param>
    /// <param name="history">
    /// Trimmed conversation history. Null or empty clears the persisted value.
    /// </param>
    public async Task SetConversationHistoryAsync(string aadObjectId, IEnumerable<(string role, string message)>? history)
    {
        if (string.IsNullOrWhiteSpace(aadObjectId)) return;

        await PopulateMemCacheIfEmpty();

        if (!_userIdConversationCache.TryGetValue(aadObjectId, out var existing) || existing == null)
        {
            // No conversation reference yet - the welcome flow hasn't cached this user.
            _logger.LogDebug("Skipped SetConversationHistoryAsync for {AadObjectId}: user not yet in conversation cache", aadObjectId);
            return;
        }

        existing.ConversationHistoryJson = history == null || !history.Any()
            ? null
            : ConversationHistoryCodec.Serialize(history);

        try
        {
            var client = await base.GetTableClient(TABLE_NAME);
            await client.UpdateEntityAsync(existing, ETag.All, Azure.Data.Tables.TableUpdateMode.Merge);
            _userIdConversationCache.AddOrUpdate(aadObjectId, existing, (_, _) => existing);
        }
        catch (RequestFailedException ex)
        {
            // Persistence failure must never break the chat; log and keep the updated
            // in-memory copy so the current process at least benefits.
            _logger.LogWarning(ex, "Failed to persist conversation history for {AadObjectId}", aadObjectId);
            _userIdConversationCache.AddOrUpdate(aadObjectId, existing, (_, _) => existing);
        }
    }
}

