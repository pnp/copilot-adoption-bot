using Azure.Data.Tables;
using Engine.Config;
using Engine.Models;
using Engine.Storage;
using Microsoft.Extensions.Logging;

namespace Engine.Services.UserCache;

/// <summary>
/// Stores cached user data in Azure Table Storage.
/// </summary>
public class AzureTableCacheStorage : ICacheStorage
{
    private readonly TableStorageManager _storageManager;
    private readonly ILogger _logger;
    private readonly string _userCacheTableName;
    private readonly string _syncMetadataTableName;

    public AzureTableCacheStorage(
        StorageAuthConfig storageAuthConfig,
        ILogger<AzureTableCacheStorage> logger,
        UserCacheConfig config)
    {
        _storageManager = new ConcreteTableStorageManager(storageAuthConfig, logger);
        _logger = logger;
        _userCacheTableName = config.UserCacheTableName;
        _syncMetadataTableName = config.SyncMetadataTableName;
    }

    public async Task<List<EnrichedUserInfo>> GetAllUsersAsync()
    {
        var tableClient = await _storageManager.GetTableClient(_userCacheTableName);
        var users = new List<EnrichedUserInfo>();

        await foreach (var entity in tableClient.QueryAsync<UserCacheTableEntity>(
            filter: $"PartitionKey eq '{UserCacheTableEntity.PartitionKeyVal}' and IsDeleted eq false"))
        {
            users.Add(MapToEnrichedUser(entity));
        }

        return users;
    }

    public async Task<EnrichedUserInfo?> GetUserByUpnAsync(string upn)
    {
        try
        {
            var tableClient = await _storageManager.GetTableClient(_userCacheTableName);
            var response = await tableClient.GetEntityAsync<UserCacheTableEntity>(
                UserCacheTableEntity.PartitionKeyVal,
                upn);

            if (response.Value != null && !response.Value.IsDeleted)
            {
                return MapToEnrichedUser(response.Value);
            }

            return null;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task UpsertUserAsync(EnrichedUserInfo user)
    {
        var tableClient = await _storageManager.GetTableClient(_userCacheTableName);

        var cachedUser = new UserCacheTableEntity
        {
            RowKey = user.UserPrincipalName,
            Id = user.Id,
            UserPrincipalName = user.UserPrincipalName,
            DisplayName = user.DisplayName,
            GivenName = user.GivenName,
            Surname = user.Surname,
            Mail = user.Mail,
            Department = user.Department,
            JobTitle = user.JobTitle,
            OfficeLocation = user.OfficeLocation,
            City = user.City,
            Country = user.Country,
            State = user.State,
            CompanyName = user.CompanyName,
            EmployeeType = user.EmployeeType,
            EmployeeHireDate = user.HireDate?.DateTime,
            LastSyncedDate = DateTime.UtcNow,
            IsDeleted = user.IsDeleted,
            HasCopilotLicense = user.HasCopilotLicense,
            ManagerUpn = user.ManagerUpn,
            ManagerDisplayName = user.ManagerDisplayName
        };

        // Merge for the same reason as UpsertUsersAsync: this projection does not carry the
        // Copilot activity columns, and Replace would delete them.
        await tableClient.UpsertEntityAsync(cachedUser, TableUpdateMode.Merge);
    }

    public async Task UpsertUsersAsync(IEnumerable<EnrichedUserInfo> users)
    {
        var tableClient = await _storageManager.GetTableClient(_userCacheTableName);
        var now = DateTime.UtcNow;

        var entities = users.Select(user => new UserCacheTableEntity
        {
            RowKey = user.UserPrincipalName,
            Id = user.Id,
            UserPrincipalName = user.UserPrincipalName,
            DisplayName = user.DisplayName,
            GivenName = user.GivenName,
            Surname = user.Surname,
            Mail = user.Mail,
            Department = user.Department,
            JobTitle = user.JobTitle,
            OfficeLocation = user.OfficeLocation,
            City = user.City,
            Country = user.Country,
            State = user.State,
            CompanyName = user.CompanyName,
            EmployeeType = user.EmployeeType,
            EmployeeHireDate = user.HireDate?.DateTime,
            LastSyncedDate = now,
            IsDeleted = user.IsDeleted,
            HasCopilotLicense = user.HasCopilotLicense,
            ManagerUpn = user.ManagerUpn,
            ManagerDisplayName = user.ManagerDisplayName
        }).ToList();

        if (entities.Count == 0) return;

        // Merge, not Replace: this projection deliberately omits the Copilot activity columns
        // (they're owned by the stats refresh, not by directory sync). Replace semantics drop
        // any property the entity doesn't carry, so a Replace here silently erased every
        // user's Copilot usage data on each delta sync - and all 150k on each full sync.
        var ops = entities.Select(e =>
            new TableTransactionAction(TableTransactionActionType.UpsertMerge, e));
        try
        {
            await TableBatch.SubmitInBatchesAsync(tableClient, ops);
        }
        catch (Azure.RequestFailedException ex)
        {
            _logger.LogWarning(ex, "Batched user-cache upsert failed; falling back to per-entity upserts");
            foreach (var entity in entities)
            {
                try
                {
                    await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge);
                }
                catch (Exception innerEx)
                {
                    _logger.LogError(innerEx, "Failed to upsert user {Upn}", entity.UserPrincipalName);
                }
            }
        }
    }

    public async Task<int> ClearAllUsersAsync()
    {
        var tableClient = await _storageManager.GetTableClient(_userCacheTableName);
        var users = new List<UserCacheTableEntity>();

        await foreach (var user in tableClient.QueryAsync<UserCacheTableEntity>(
            filter: $"PartitionKey eq '{UserCacheTableEntity.PartitionKeyVal}'"))
        {
            users.Add(user);
        }

        // Submit deletes in transactional batches (all users share the same partition key).
        var ops = users.Select(u => new TableTransactionAction(TableTransactionActionType.Delete, u));
        try
        {
            await TableBatch.SubmitInBatchesAsync(tableClient, ops);
        }
        catch (Azure.RequestFailedException ex)
        {
            _logger.LogWarning(ex, "Batched user-cache clear failed; falling back to per-entity deletes");
            foreach (var user in users)
            {
                try
                {
                    await tableClient.DeleteEntityAsync(user.PartitionKey, user.RowKey);
                }
                catch (Azure.RequestFailedException innerEx) when (innerEx.Status == 404)
                {
                    // Already gone.
                }
            }
        }

        // Clear sync metadata including delta token to force a full sync
        var metadata = new CacheSyncMetadata();
        await UpdateSyncMetadataAsync(metadata);

        return users.Count;
    }

    /// <summary>
    /// Apply Copilot usage stats to cached users.
    ///
    /// <para>
    /// Uses batched sparse merges: no read-before-write, and only the stats columns are sent.
    /// The previous implementation did a GetEntity + full Replace per user - 300,000
    /// serialized round trips for a 150,000-user tenant, roughly 50-100 minutes single
    /// threaded, on a worker that is unloaded when idle.
    /// </para>
    /// </summary>
    public async Task<int> UpdateUsersWithCopilotStatsAsync(Dictionary<string, CopilotUserStats> stats)
    {
        if (stats.Count == 0) return 0;

        var tableClient = await _storageManager.GetTableClient(_userCacheTableName);
        var now = DateTime.UtcNow;

        var ops = stats.Select(kv => new TableTransactionAction(
            TableTransactionActionType.UpsertMerge,
            new TableEntity(UserCacheTableEntity.PartitionKeyVal, kv.Key)
            {
                { nameof(UserCacheTableEntity.CopilotLastActivityDate), kv.Value.LastActivityDate },
                { nameof(UserCacheTableEntity.CopilotChatLastActivityDate), kv.Value.CopilotChatLastActivityDate },
                { nameof(UserCacheTableEntity.TeamscopilotLastActivityDate), kv.Value.TeamsCopilotLastActivityDate },
                { nameof(UserCacheTableEntity.WordCopilotLastActivityDate), kv.Value.WordCopilotLastActivityDate },
                { nameof(UserCacheTableEntity.ExcelCopilotLastActivityDate), kv.Value.ExcelCopilotLastActivityDate },
                { nameof(UserCacheTableEntity.PowerPointCopilotLastActivityDate), kv.Value.PowerPointCopilotLastActivityDate },
                { nameof(UserCacheTableEntity.OutlookCopilotLastActivityDate), kv.Value.OutlookCopilotLastActivityDate },
                { nameof(UserCacheTableEntity.OneNoteCopilotLastActivityDate), kv.Value.OneNoteCopilotLastActivityDate },
                { nameof(UserCacheTableEntity.LoopCopilotLastActivityDate), kv.Value.LoopCopilotLastActivityDate },
                { nameof(UserCacheTableEntity.LastCopilotStatsUpdate), now },
            }));

        var failures = await SubmitMergesAsync(tableClient, ops, "Copilot stats");
        var updateCount = stats.Count - failures;

        _logger.LogInformation($"Updated Copilot stats for {updateCount} of {stats.Count} users");
        return updateCount;
    }

    /// <summary>
    /// Apply Copilot license state to cached users, using the same batched sparse merge.
    /// </summary>
    public async Task<int> UpdateUsersWithLicenseInfoAsync(Dictionary<string, bool> licenseInfo)
    {
        if (licenseInfo.Count == 0) return 0;

        var tableClient = await _storageManager.GetTableClient(_userCacheTableName);

        var ops = licenseInfo.Select(kv => new TableTransactionAction(
            TableTransactionActionType.UpsertMerge,
            new TableEntity(UserCacheTableEntity.PartitionKeyVal, kv.Key)
            {
                { nameof(UserCacheTableEntity.HasCopilotLicense), kv.Value },
            }));

        var failures = await SubmitMergesAsync(tableClient, ops, "license info");
        var updateCount = licenseInfo.Count - failures;

        _logger.LogInformation($"Updated license info for {updateCount} of {licenseInfo.Count} users");
        return updateCount;
    }

    /// <summary>
    /// Submit merge operations in transactional batches, falling back to per-entity writes
    /// only for the chunk that failed.
    /// </summary>
    private async Task<int> SubmitMergesAsync(
        TableClient tableClient, IEnumerable<TableTransactionAction> ops, string what)
    {
        var failures = 0;

        foreach (var chunk in TableBatch.Chunk(ops))
        {
            try
            {
                await tableClient.SubmitTransactionAsync(chunk);
            }
            catch (Azure.RequestFailedException ex)
            {
                _logger.LogWarning(ex, "Batched {What} merge failed; retrying that chunk per entity", what);

                foreach (var op in chunk)
                {
                    try
                    {
                        await tableClient.UpsertEntityAsync(op.Entity, TableUpdateMode.Merge);
                    }
                    catch (Exception innerEx)
                    {
                        failures++;
                        _logger.LogWarning(innerEx, "Failed to update {What} for {Upn}", what, op.Entity.RowKey);
                    }
                }
            }
        }

        return failures;
    }

    public async Task<CacheSyncMetadata> GetSyncMetadataAsync()
    {
        var tableClient = await _storageManager.GetTableClient(_syncMetadataTableName);

        try
        {
            var response = await tableClient.GetEntityAsync<UserSyncMetadataEntity>(
                UserSyncMetadataEntity.PartitionKeyVal,
                UserSyncMetadataEntity.SingletonRowKey);

            var entity = response.Value;
            return new CacheSyncMetadata
            {
                DeltaToken = entity.DeltaLink,
                LastFullSyncDate = entity.LastFullSyncDate,
                LastDeltaSyncDate = entity.LastDeltaSyncDate,
                LastCopilotStatsUpdate = entity.LastCopilotStatsUpdate,
                LastSyncStatus = entity.LastSyncStatus,
                LastSyncError = entity.LastSyncError,
                LastSyncUserCount = entity.LastSyncUserCount
            };
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return new CacheSyncMetadata();
        }
    }

    public async Task UpdateSyncMetadataAsync(CacheSyncMetadata metadata)
    {
        var tableClient = await _storageManager.GetTableClient(_syncMetadataTableName);

        var entity = new UserSyncMetadataEntity
        {
            DeltaLink = metadata.DeltaToken,
            LastFullSyncDate = metadata.LastFullSyncDate,
            LastDeltaSyncDate = metadata.LastDeltaSyncDate,
            LastCopilotStatsUpdate = metadata.LastCopilotStatsUpdate,
            LastSyncStatus = metadata.LastSyncStatus,
            LastSyncError = metadata.LastSyncError,
            LastSyncUserCount = metadata.LastSyncUserCount
        };

        await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace);
    }

    /// <summary>
    /// Delete cache tables from Azure Table Storage. Used primarily for test cleanup.
    /// </summary>
    public async Task DeleteTablesAsync()
    {
        await _storageManager.DeleteTable(_userCacheTableName);
        await _storageManager.DeleteTable(_syncMetadataTableName);
    }

    private static EnrichedUserInfo MapToEnrichedUser(UserCacheTableEntity entity)
    {
        return new EnrichedUserInfo
        {
            Id = entity.Id,
            UserPrincipalName = entity.UserPrincipalName,
            DisplayName = entity.DisplayName,
            GivenName = entity.GivenName,
            Surname = entity.Surname,
            Mail = entity.Mail,
            Department = entity.Department,
            JobTitle = entity.JobTitle,
            OfficeLocation = entity.OfficeLocation,
            City = entity.City,
            Country = entity.Country,
            State = entity.State,
            CompanyName = entity.CompanyName,
            EmployeeType = entity.EmployeeType,
            HireDate = entity.EmployeeHireDate.HasValue ? new DateTimeOffset(entity.EmployeeHireDate.Value) : null,
            ManagerUpn = entity.ManagerUpn,
            ManagerDisplayName = entity.ManagerDisplayName,
            IsDeleted = entity.IsDeleted,
            HasCopilotLicense = entity.HasCopilotLicense,
            CopilotLastActivityDate = entity.CopilotLastActivityDate,
            CopilotChatLastActivityDate = entity.CopilotChatLastActivityDate,
            TeamsCopilotLastActivityDate = entity.TeamscopilotLastActivityDate,
            WordCopilotLastActivityDate = entity.WordCopilotLastActivityDate,
            ExcelCopilotLastActivityDate = entity.ExcelCopilotLastActivityDate,
            PowerPointCopilotLastActivityDate = entity.PowerPointCopilotLastActivityDate,
            OutlookCopilotLastActivityDate = entity.OutlookCopilotLastActivityDate,
            OneNoteCopilotLastActivityDate = entity.OneNoteCopilotLastActivityDate,
            LoopCopilotLastActivityDate = entity.LoopCopilotLastActivityDate,
            LastCopilotStatsUpdate = entity.LastCopilotStatsUpdate
        };
    }
}
