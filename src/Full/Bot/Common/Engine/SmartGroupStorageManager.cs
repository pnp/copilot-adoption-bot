using Azure.Data.Tables;
using Engine.Config;
using Engine.Storage;
using Microsoft.Extensions.Logging;

namespace Engine;

/// <summary>
/// Manages smart groups in Azure Table Storage
/// </summary>
public class SmartGroupStorageManager : TableStorageManager
{
    private readonly ILogger _logger;
    private const string SMART_GROUPS_TABLE_NAME = "smartgroups";
    private const string SMART_GROUP_MEMBERS_TABLE_NAME = "smartgroupmembers";

    public SmartGroupStorageManager(StorageAuthConfig storageAuthConfig, ILogger logger)
        : base(storageAuthConfig, logger)
    {
        _logger = logger;
    }

    #region Smart Group Management

    /// <summary>
    /// Create a new smart group
    /// </summary>
    public async Task<SmartGroupTableEntity> CreateSmartGroup(string name, string description, string createdByUpn)
    {
        var groupId = Guid.NewGuid().ToString();

        var entity = new SmartGroupTableEntity
        {
            RowKey = groupId,
            Name = name,
            Description = description,
            CreatedByUpn = createdByUpn,
            CreatedDate = DateTime.UtcNow
        };

        var tableClient = await GetTableClient(SMART_GROUPS_TABLE_NAME);
        await tableClient.AddEntityAsync(entity);

        _logger.LogInformation($"Created smart group '{name}' with ID {groupId}");
        return entity;
    }

    /// <summary>
    /// Get all smart groups
    /// </summary>
    public async Task<List<SmartGroupTableEntity>> GetAllSmartGroups()
    {
        var tableClient = await GetTableClient(SMART_GROUPS_TABLE_NAME);
        var groups = new List<SmartGroupTableEntity>();

        await foreach (var entity in tableClient.QueryAsync<SmartGroupTableEntity>(
            filter: $"PartitionKey eq '{SmartGroupTableEntity.PartitionKeyVal}'"))
        {
            groups.Add(entity);
        }

        return groups;
    }

    /// <summary>
    /// Get a specific smart group by ID
    /// </summary>
    public async Task<SmartGroupTableEntity?> GetSmartGroup(string groupId)
    {
        var tableClient = await GetTableClient(SMART_GROUPS_TABLE_NAME);
        try
        {
            var response = await tableClient.GetEntityAsync<SmartGroupTableEntity>(
                SmartGroupTableEntity.PartitionKeyVal, groupId);
            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    /// <summary>
    /// Update a smart group
    /// </summary>
    public async Task<SmartGroupTableEntity> UpdateSmartGroup(string groupId, string name, string description)
    {
        var group = await GetSmartGroup(groupId);
        if (group == null)
        {
            throw new InvalidOperationException($"Smart group {groupId} not found");
        }

        group.Name = name;
        group.Description = description;

        var tableClient = await GetTableClient(SMART_GROUPS_TABLE_NAME);
        await tableClient.UpdateEntityAsync(group, group.ETag, TableUpdateMode.Replace);

        _logger.LogInformation($"Updated smart group {groupId}");
        return group;
    }

    /// <summary>
    /// Update smart group resolution info
    /// </summary>
    public async Task UpdateSmartGroupResolution(string groupId, int memberCount)
    {
        var group = await GetSmartGroup(groupId);
        if (group == null)
        {
            throw new InvalidOperationException($"Smart group {groupId} not found");
        }

        group.LastResolvedDate = DateTime.UtcNow;
        group.LastResolvedMemberCount = memberCount;

        var tableClient = await GetTableClient(SMART_GROUPS_TABLE_NAME);
        await tableClient.UpdateEntityAsync(group, group.ETag, TableUpdateMode.Replace);

        _logger.LogInformation($"Updated smart group {groupId} resolution: {memberCount} members");
    }

    /// <summary>
    /// Delete a smart group and its cached members
    /// </summary>
    public async Task DeleteSmartGroup(string groupId)
    {
        var group = await GetSmartGroup(groupId);
        if (group == null)
        {
            throw new InvalidOperationException($"Smart group {groupId} not found");
        }

        // Delete cached members
        await ClearSmartGroupMemberCache(groupId);

        // Delete the smart group
        var tableClient = await GetTableClient(SMART_GROUPS_TABLE_NAME);
        await tableClient.DeleteEntityAsync(SmartGroupTableEntity.PartitionKeyVal, groupId);

        _logger.LogInformation($"Deleted smart group {groupId}");
    }

    #endregion

    #region Smart Group Member Cache

    /// <summary>
    /// Cache resolved smart group members. All members for a group share the same
    /// partition key (the groupId), so we can submit inserts as transactional batches
    /// (up to 100 ops per batch) - O(N/100) round trips instead of O(N).
    /// </summary>
    public async Task CacheSmartGroupMembers(string groupId, List<SmartGroupMemberCacheEntity> members)
    {
        // Clear existing cache first
        await ClearSmartGroupMemberCache(groupId);

        if (members.Count == 0) return;

        var tableClient = await GetTableClient(SMART_GROUP_MEMBERS_TABLE_NAME);

        var now = DateTime.UtcNow;
        foreach (var member in members)
        {
            member.PartitionKey = groupId;
            member.CachedDate = now;
        }

        var ops = members.Select(m => new TableTransactionAction(TableTransactionActionType.Add, m));
        try
        {
            await TableBatch.SubmitInBatchesAsync(tableClient, ops);
        }
        catch (Azure.RequestFailedException ex)
        {
            _logger.LogWarning(ex, "Batched smart-group member insert failed for {GroupId}; falling back to per-entity inserts", groupId);
            foreach (var member in members)
            {
                try { await tableClient.AddEntityAsync(member); }
                catch (Azure.RequestFailedException innerEx) when (innerEx.Status == 409)
                {
                    // Already present (race). Ignore.
                }
            }
        }

        _logger.LogInformation($"Cached {members.Count} members for smart group {groupId}");
    }

    /// <summary>
    /// Get cached members for a smart group
    /// </summary>
    public async Task<List<SmartGroupMemberCacheEntity>> GetCachedSmartGroupMembers(string groupId)
    {
        var tableClient = await GetTableClient(SMART_GROUP_MEMBERS_TABLE_NAME);
        var members = new List<SmartGroupMemberCacheEntity>();
        var safeGroupId = ODataFilter.EscapeLiteral(groupId);

        await foreach (var entity in tableClient.QueryAsync<SmartGroupMemberCacheEntity>(
            filter: $"PartitionKey eq '{safeGroupId}'"))
        {
            members.Add(entity);
        }

        return members;
    }

    /// <summary>
    /// Clear the member cache for a smart group. Uses transactional batches because all
    /// members share the groupId partition key.
    /// </summary>
    public async Task ClearSmartGroupMemberCache(string groupId)
    {
        var tableClient = await GetTableClient(SMART_GROUP_MEMBERS_TABLE_NAME);
        var existingMembers = await GetCachedSmartGroupMembers(groupId);

        if (existingMembers.Count == 0)
        {
            _logger.LogDebug("No cached members to clear for smart group {GroupId}", groupId);
            return;
        }

        var ops = existingMembers.Select(m => new TableTransactionAction(TableTransactionActionType.Delete, m));
        try
        {
            await TableBatch.SubmitInBatchesAsync(tableClient, ops);
        }
        catch (Azure.RequestFailedException ex)
        {
            _logger.LogWarning(ex, "Batched smart-group member delete failed for {GroupId}; falling back to per-entity deletes", groupId);
            foreach (var member in existingMembers)
            {
                try { await tableClient.DeleteEntityAsync(member.PartitionKey, member.RowKey); }
                catch (Azure.RequestFailedException innerEx) when (innerEx.Status == 404)
                {
                    // Already gone. Ignore.
                }
            }
        }

        _logger.LogInformation($"Cleared member cache for smart group {groupId}");
    }

    #endregion
}
