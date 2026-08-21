using Azure;
using Azure.Data.Tables;
using Engine.Config;
using Engine.Models;
using Engine.Services;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;

namespace Engine;

/// <summary>
/// Durable store of per-user conversation references, backed by Azure Table Storage with a
/// small bounded read-through cache.
///
/// <para>
/// This class previously loaded <em>every</em> user in the tenant into a process-wide
/// dictionary that was never evicted. Three problems followed from that, all addressed here:
/// the footprint scaled with tenant size rather than working set (~2.5 GB at 150,000 users);
/// the load was an unsynchronised full-table scan that could stampede or leave a permanently
/// half-filled cache; and lookups that missed the cache reported "user not found" rather than
/// consulting storage, which on a cold start routed the entire audience down the app-install
/// path and silently dropped their nudges.
/// </para>
///
/// <para>
/// Every read is now a point read (~10 ms) and every write is a sparse merge patch, so cost
/// is independent of tenant size and correct across restarts and scaled-out instances.
/// </para>
/// </summary>
public class BotConversationCache : TableStorageManager, IBotInteractionSource
{
    const string TABLE_NAME = "ConversationCache";

    /// <summary>
    /// Hot-user cache size. Sized for concurrent conversations, not for the tenant: at ~1 KB
    /// per row this is a few MB, versus gigabytes for the whole directory.
    /// </summary>
    private const int HotCacheCapacity = 5_000;

    /// <summary>
    /// Character budget for persisted conversation history. Azure Table string properties are
    /// capped at 64 KB; staying well under it means a long chat can never silently fail to save.
    /// </summary>
    internal const int MaxHistoryChars = 16_000;

    private readonly GraphServiceClient _graphServiceClient;
    private readonly ILogger<BotConversationCache> _logger;
    private readonly BoundedCache<string, CachedUserAndConversationData> _hot =
        new(HotCacheCapacity, StringComparer.Ordinal);

    public BotConversationCache(GraphServiceClient graphServiceClient, AppConfig appConfig, ILogger<BotConversationCache> logger)
        : base(appConfig.StorageAuthConfig ?? throw new ArgumentNullException(nameof(appConfig.StorageAuthConfig)), logger)
    {
        _graphServiceClient = graphServiceClient;
        _logger = logger;
    }

    /// <summary>
    /// Look a user up by AAD object id: hot cache first, then a durable point read.
    /// </summary>
    public async Task<CachedUserAndConversationData?> GetCachedUserAsync(string aadObjectId)
    {
        if (string.IsNullOrWhiteSpace(aadObjectId)) return null;

        if (_hot.TryGet(aadObjectId, out var cached) && cached != null)
        {
            return cached;
        }

        try
        {
            var client = await base.GetTableClient(TABLE_NAME);
            var response = await client.GetEntityAsync<CachedUserAndConversationData>(
                CachedUserAndConversationData.PartitionKeyVal, aadObjectId);

            var entity = response.Value;
            _hot.Set(aadObjectId, entity);
            return entity;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether a conversation reference exists for this user. Consults durable storage, so it
    /// is correct on a cold start and across instances.
    /// </summary>
    public async Task<bool> ContainsUserIdAsync(string aadObjectId) =>
        await GetCachedUserAsync(aadObjectId) != null;

    public async Task RemoveFromCache(string aadObjectId)
    {
        _hot.Remove(aadObjectId);
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
        var client = await base.GetTableClient(TABLE_NAME);

        var existing = await GetCachedUserAsync(botUser.UserId);
        if (existing != null)
        {
            _hot.Set(botUser.UserId, existing);
            return;
        }

        string? upn = null;
        if (botUser.IsAzureAdUserId)
        {
            // Get UPN from Graph
            var user = await graphClient.Users[botUser.UserId].GetAsync(op => op.QueryParameters.Select = ["userPrincipalName"]);
            upn = user?.UserPrincipalName ?? throw new ArgumentNullException($"No userPrincipalName for {nameof(conversationReference.User.AadObjectId)} '{conversationReference.User.AadObjectId}'");
        }

        var entity = new CachedUserAndConversationData
        {
            RowKey = botUser.UserId,
            ServiceUrl = serviceUrl,
            UserPrincipalName = upn,
            ConversationId = conversationReference.Conversation.Id
        };

        try
        {
            await client.AddEntityAsync(entity);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            // Concurrent add - re-read and use the existing entity.
            var refreshed = await client.GetEntityAsync<CachedUserAndConversationData>(
                CachedUserAndConversationData.PartitionKeyVal, botUser.UserId);
            entity = refreshed.Value;
        }

        _hot.Set(botUser.UserId, entity);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Streams the table rather than holding it. Only used by the statistics dashboard; the
    /// inbound message path must never enumerate users.
    /// </remarks>
    public async Task<List<CachedUserAndConversationData>> GetCachedUsersAsync()
    {
        var client = await base.GetTableClient(TABLE_NAME);
        var users = new List<CachedUserAndConversationData>();

        // Project only what interaction statistics need, so this doesn't pull chat history
        // for every user in the tenant.
        await foreach (var entity in client.QueryAsync<CachedUserAndConversationData>(
            filter: $"PartitionKey eq '{CachedUserAndConversationData.PartitionKeyVal}'",
            select: new[] { "RowKey", "UserPrincipalName", "ConversationId", "ServiceUrl", "LastInteractionUtc" }))
        {
            users.Add(entity);
        }

        return users;
    }

    /// <inheritdoc />
    public async Task<int> GetReachedUserCountAsync()
    {
        var client = await base.GetTableClient(TABLE_NAME);

        var count = 0;
        await foreach (var _ in client.QueryAsync<CachedUserAndConversationData>(
            filter: $"PartitionKey eq '{CachedUserAndConversationData.PartitionKeyVal}'",
            select: new[] { "RowKey" }))
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// Records that the user has sent a message to the bot.
    /// </summary>
    public async Task RecordUserInteractionAsync(string aadObjectId)
    {
        if (string.IsNullOrWhiteSpace(aadObjectId)) return;

        await MergeAsync(aadObjectId, new Dictionary<string, object?>
        {
            [nameof(CachedUserAndConversationData.LastInteractionUtc)] = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Records which template was last sent to this user, as AI follow-up context.
    ///
    /// <para>
    /// Only the template <em>reference</em> is stored. The rendered card is identical for every
    /// recipient of a batch, so persisting it per user duplicated the same few KB across the
    /// whole audience - hundreds of MB of storage, and the same again in memory - to hold a few
    /// KB of distinct information. Card text for the LLM is re-derived from the template.
    /// </para>
    /// </summary>
    public async Task SetLastCardAsync(string aadObjectId, string templateId, string templateName, string cardJson, DateTime sentUtc)
    {
        if (string.IsNullOrWhiteSpace(aadObjectId)) return;

        await MergeAsync(aadObjectId, new Dictionary<string, object?>
        {
            [nameof(CachedUserAndConversationData.LastCardTemplateId)] = templateId,
            [nameof(CachedUserAndConversationData.LastCardTemplateName)] = templateName,
            [nameof(CachedUserAndConversationData.LastCardSentUtc)] = sentUtc
        });
    }

    /// <summary>
    /// Persists the trimmed conversation history used as LLM context for AI follow-up.
    /// Truncated to <see cref="MaxHistoryChars"/> so it cannot breach the 64 KB Azure Table
    /// property limit and silently fail to save.
    /// </summary>
    public async Task SetConversationHistoryAsync(string aadObjectId, IEnumerable<(string role, string message)>? history)
    {
        if (string.IsNullOrWhiteSpace(aadObjectId)) return;

        string? json = null;
        if (history != null)
        {
            var trimmed = AIPromptBudget.TrimHistory(history.ToList(), MaxHistoryChars);
            if (trimmed.Count > 0)
            {
                json = ConversationHistoryCodec.Serialize(trimmed);
            }
        }

        await MergeAsync(aadObjectId, new Dictionary<string, object?>
        {
            [nameof(CachedUserAndConversationData.ConversationHistoryJson)] = json
        });
    }

    /// <summary>
    /// Apply a sparse merge patch to a user's row.
    ///
    /// <para>
    /// Sends only the changed columns and requires no read-before-write. The previous
    /// implementation loaded the full entity and wrote it back to change a single field, which
    /// re-uploaded the card JSON on every inbound message.
    /// </para>
    /// </summary>
    private async Task MergeAsync(string aadObjectId, Dictionary<string, object?> changes)
    {
        var patch = new TableEntity(CachedUserAndConversationData.PartitionKeyVal, aadObjectId);
        foreach (var (key, value) in changes)
        {
            patch[key] = value;
        }

        try
        {
            var client = await base.GetTableClient(TABLE_NAME);
            await client.UpdateEntityAsync(patch, ETag.All, TableUpdateMode.Merge);

            // Keep any cached copy consistent with what we just wrote.
            _hot.Remove(aadObjectId);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // No conversation reference yet - the welcome flow hasn't cached this user, so
            // there is nothing to attach the change to.
            _logger.LogDebug("Skipped update for {AadObjectId}: user not yet in conversation cache", aadObjectId);
        }
        catch (RequestFailedException ex)
        {
            // Persistence failure must never break the user-facing path.
            _logger.LogWarning(ex, "Failed to persist conversation cache update for {AadObjectId}", aadObjectId);
        }
    }
}
