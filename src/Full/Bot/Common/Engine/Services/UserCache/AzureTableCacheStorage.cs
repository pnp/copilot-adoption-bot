using Azure.Data.Tables;
using Common.Engine.Config;
using Common.Engine.Models;
using Common.Engine.Storage;
using Microsoft.Extensions.Logging;

namespace Common.Engine.Services.UserCache;

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
        string storageConnectionString,
        ILogger<AzureTableCacheStorage> logger,
        UserCacheConfig config)
    {
        _storageManager = new ConcreteTableStorageManager(storageConnectionString);
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

        await tableClient.UpsertEntityAsync(cachedUser, TableUpdateMode.Replace);
    }

    public async Task UpsertUsersAsync(IEnumerable<EnrichedUserInfo> users)
    {
        var tableClient = await _storageManager.GetTableClient(_userCacheTableName);

        foreach (var user in users)
        {
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

            await tableClient.UpsertEntityAsync(cachedUser, TableUpdateMode.Replace);
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

        foreach (var user in users)
        {
            await tableClient.DeleteEntityAsync(user.PartitionKey, user.RowKey);
        }

        // Clear sync metadata including delta token to force a full sync
        var metadata = new CacheSyncMetadata();
        await UpdateSyncMetadataAsync(metadata);

        return users.Count;
    }

    public async Task<int> UpdateUsersWithCopilotStatsAsync(Dictionary<string, CopilotUserStats> stats)
    {
        var tableClient = await _storageManager.GetTableClient(_userCacheTableName);
        var updateCount = 0;

        foreach (var (upn, userStats) in stats)
        {
            try
            {
                var response = await tableClient.GetEntityAsync<UserCacheTableEntity>(
                    UserCacheTableEntity.PartitionKeyVal,
                    upn);

                if (response.Value != null && !response.Value.IsDeleted)
                {
                    var user = response.Value;
                    
                    // Update Copilot stats
                    user.CopilotLastActivityDate = userStats.LastActivityDate;
                    user.CopilotChatLastActivityDate = userStats.CopilotChatLastActivityDate;
                    user.TeamscopilotLastActivityDate = userStats.TeamsCopilotLastActivityDate;
                    user.WordCopilotLastActivityDate = userStats.WordCopilotLastActivityDate;
                    user.ExcelCopilotLastActivityDate = userStats.ExcelCopilotLastActivityDate;
                    user.PowerPointCopilotLastActivityDate = userStats.PowerPointCopilotLastActivityDate;
                    user.OutlookCopilotLastActivityDate = userStats.OutlookCopilotLastActivityDate;
                    user.OneNoteCopilotLastActivityDate = userStats.OneNoteCopilotLastActivityDate;
                    user.LoopCopilotLastActivityDate = userStats.LoopCopilotLastActivityDate;
                    user.LastCopilotStatsUpdate = DateTime.UtcNow;

                    await tableClient.UpdateEntityAsync(user, user.ETag, TableUpdateMode.Replace);
                    updateCount++;
                }
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogDebug($"User {upn} not found in cache, skipping Copilot stats update");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to update Copilot stats for user {upn}");
            }
        }

        _logger.LogInformation($"Updated Copilot stats for {updateCount} of {stats.Count} users");
        return updateCount;
    }

    public async Task<int> UpdateUsersWithLicenseInfoAsync(Dictionary<string, bool> licenseInfo)
    {
        var tableClient = await _storageManager.GetTableClient(_userCacheTableName);
        var updateCount = 0;

        foreach (var (upn, hasCopilotLicense) in licenseInfo)
        {
            try
            {
                var response = await tableClient.GetEntityAsync<UserCacheTableEntity>(
                    UserCacheTableEntity.PartitionKeyVal,
                    upn);

                if (response.Value != null && !response.Value.IsDeleted)
                {
                    var user = response.Value;
                    
                    // Update license info
                    user.HasCopilotLicense = hasCopilotLicense;

                    await tableClient.UpdateEntityAsync(user, user.ETag, TableUpdateMode.Replace);
                    updateCount++;
                }
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogDebug($"User {upn} not found in cache, skipping license info update");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to update license info for user {upn}");
            }
        }

        _logger.LogInformation($"Updated license info for {updateCount} of {licenseInfo.Count} users");
        return updateCount;
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
