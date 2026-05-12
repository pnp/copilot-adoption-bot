using Azure;
using Engine.Config;
using Engine.Models;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using System.Collections.Concurrent;

namespace Engine;


public class BotConversationCache : TableStorageManager
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

    public CachedUserAndConversationData? GetCachedUser(string aadObjectId)
    {
        // Use direct dictionary lookup (O(1)) instead of a linear scan over Values.
        return _userIdConversationCache.TryGetValue(aadObjectId, out var u) ? u : null;
    }

    public bool ContainsUserId(string aadId)
    {
        return _userIdConversationCache.ContainsKey(aadId);
    }
}

