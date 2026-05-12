using Azure.Data.Tables;
using Engine.Models;
using Engine.Services.UserCache;
using Engine.Storage;
using Microsoft.Extensions.Logging;

namespace UnitTests.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="CopilotStatsService.UpdateCachedUsersWithStatsAsync"/>.
/// These tests require Azure Table Storage connectivity.
/// </summary>
[TestClass]
public class CopilotStatsServiceUpdateUsersIntegrationTests : CopilotStatsServiceIntegrationTestBase
{
    [TestMethod]
    public async Task UpdateCachedUsersWithStatsAsync_UpdatesExistingUsers()
    {
        if (_service == null || _tableServiceClient == null)
        {
            Assert.Inconclusive("Service not initialized - check configuration");
            return;
        }

        // Arrange - Create test table and add test user
        var tableClient = _tableServiceClient.GetTableClient(_testTableName);
        await tableClient.CreateIfNotExistsAsync();

        var testUser = new UserCacheTableEntity
        {
            PartitionKey = UserCacheTableEntity.PartitionKeyVal,
            RowKey = "testuser@contoso.com",
            UserPrincipalName = "testuser@contoso.com",
            DisplayName = "Test User"
        };
        await tableClient.AddEntityAsync(testUser);

        var stats = new List<CopilotUsageRecord>
        {
            new CopilotUsageRecord
            {
                UserPrincipalName = "testuser@contoso.com",
                LastActivityDate = DateTime.UtcNow.AddDays(-1),
                CopilotChatLastActivityDate = DateTime.UtcNow.AddDays(-2),
                TeamsCopilotLastActivityDate = DateTime.UtcNow.AddDays(-3)
            }
        };

        // Act
        await _service.UpdateCachedUsersWithStatsAsync(tableClient, stats);

        // Assert
        var updatedUser = await tableClient.GetEntityAsync<UserCacheTableEntity>(
            UserCacheTableEntity.PartitionKeyVal, "testuser@contoso.com");

        Assert.IsNotNull(updatedUser.Value.CopilotLastActivityDate);
        Assert.IsNotNull(updatedUser.Value.CopilotChatLastActivityDate);
        Assert.IsNotNull(updatedUser.Value.TeamscopilotLastActivityDate);
        Assert.IsNotNull(updatedUser.Value.LastCopilotStatsUpdate);

        _logger.LogInformation($"Successfully updated user with Copilot stats");
        _logger.LogInformation($"  Last Activity: {updatedUser.Value.CopilotLastActivityDate}");
        _logger.LogInformation($"  Last Stats Update: {updatedUser.Value.LastCopilotStatsUpdate}");
    }

    [TestMethod]
    public async Task UpdateCachedUsersWithStatsAsync_SkipsNonExistentUsers()
    {
        if (_service == null || _tableServiceClient == null)
        {
            Assert.Inconclusive("Service not initialized - check configuration");
            return;
        }

        // Arrange - Create empty test table
        var tableClient = _tableServiceClient.GetTableClient(_testTableName);
        await tableClient.CreateIfNotExistsAsync();

        var stats = new List<CopilotUsageRecord>
        {
            new CopilotUsageRecord
            {
                UserPrincipalName = "nonexistent@contoso.com",
                LastActivityDate = DateTime.UtcNow
            }
        };

        // Act - Should not throw
        await _service.UpdateCachedUsersWithStatsAsync(tableClient, stats);

        // Assert - Table should still be empty
        var entities = tableClient.QueryAsync<UserCacheTableEntity>();
        var count = 0;
        await foreach (var entity in entities)
        {
            count++;
        }

        Assert.AreEqual(0, count, "Should not create new users, only update existing ones");
        _logger.LogInformation("Correctly skipped non-existent user");
    }

    [TestMethod]
    public async Task UpdateCachedUsersWithStatsAsync_UpdatesMultipleUsers()
    {
        if (_service == null || _tableServiceClient == null)
        {
            Assert.Inconclusive("Service not initialized - check configuration");
            return;
        }

        // Arrange - Create test table with multiple users
        var tableClient = _tableServiceClient.GetTableClient(_testTableName);
        await tableClient.CreateIfNotExistsAsync();

        var users = new[]
        {
            new UserCacheTableEntity
            {
                PartitionKey = UserCacheTableEntity.PartitionKeyVal,
                RowKey = "user1@contoso.com",
                UserPrincipalName = "user1@contoso.com"
            },
            new UserCacheTableEntity
            {
                PartitionKey = UserCacheTableEntity.PartitionKeyVal,
                RowKey = "user2@contoso.com",
                UserPrincipalName = "user2@contoso.com"
            },
            new UserCacheTableEntity
            {
                PartitionKey = UserCacheTableEntity.PartitionKeyVal,
                RowKey = "user3@contoso.com",
                UserPrincipalName = "user3@contoso.com"
            }
        };

        foreach (var user in users)
        {
            await tableClient.AddEntityAsync(user);
        }

        var stats = new List<CopilotUsageRecord>
        {
            new CopilotUsageRecord { UserPrincipalName = "user1@contoso.com", LastActivityDate = DateTime.UtcNow.AddDays(-1) },
            new CopilotUsageRecord { UserPrincipalName = "user2@contoso.com", LastActivityDate = DateTime.UtcNow.AddDays(-2) },
            new CopilotUsageRecord { UserPrincipalName = "user3@contoso.com", LastActivityDate = DateTime.UtcNow.AddDays(-3) }
        };

        // Act
        await _service.UpdateCachedUsersWithStatsAsync(tableClient, stats);

        // Assert - All users should be updated
        foreach (var stat in stats)
        {
            var updatedUser = await tableClient.GetEntityAsync<UserCacheTableEntity>(
                UserCacheTableEntity.PartitionKeyVal, stat.UserPrincipalName);

            Assert.IsNotNull(updatedUser.Value.CopilotLastActivityDate);
            Assert.IsNotNull(updatedUser.Value.LastCopilotStatsUpdate);
        }

        _logger.LogInformation($"Successfully updated {stats.Count} users with Copilot stats");
    }

    [TestMethod]
    public async Task UpdateCachedUsersWithStatsAsync_UpdatesAllCopilotActivityTypes()
    {
        if (_service == null || _tableServiceClient == null)
        {
            Assert.Inconclusive("Service not initialized - check configuration");
            return;
        }

        // Arrange
        var tableClient = _tableServiceClient.GetTableClient(_testTableName);
        await tableClient.CreateIfNotExistsAsync();

        var testUser = new UserCacheTableEntity
        {
            PartitionKey = UserCacheTableEntity.PartitionKeyVal,
            RowKey = "testuser@contoso.com",
            UserPrincipalName = "testuser@contoso.com"
        };
        await tableClient.AddEntityAsync(testUser);

        var stats = new List<CopilotUsageRecord>
        {
            new CopilotUsageRecord
            {
                UserPrincipalName = "testuser@contoso.com",
                LastActivityDate = DateTime.UtcNow.AddDays(-1),
                CopilotChatLastActivityDate = DateTime.UtcNow.AddDays(-2),
                TeamsCopilotLastActivityDate = DateTime.UtcNow.AddDays(-3),
                WordCopilotLastActivityDate = DateTime.UtcNow.AddDays(-4),
                ExcelCopilotLastActivityDate = DateTime.UtcNow.AddDays(-5),
                PowerPointCopilotLastActivityDate = DateTime.UtcNow.AddDays(-6),
                OutlookCopilotLastActivityDate = DateTime.UtcNow.AddDays(-7),
                OneNoteCopilotLastActivityDate = DateTime.UtcNow.AddDays(-8),
                LoopCopilotLastActivityDate = DateTime.UtcNow.AddDays(-9)
            }
        };

        // Act
        await _service.UpdateCachedUsersWithStatsAsync(tableClient, stats);

        // Assert
        var updatedUser = await tableClient.GetEntityAsync<UserCacheTableEntity>(
            UserCacheTableEntity.PartitionKeyVal, "testuser@contoso.com");

        Assert.IsNotNull(updatedUser.Value.CopilotLastActivityDate);
        Assert.IsNotNull(updatedUser.Value.CopilotChatLastActivityDate);
        Assert.IsNotNull(updatedUser.Value.TeamscopilotLastActivityDate);
        Assert.IsNotNull(updatedUser.Value.WordCopilotLastActivityDate);
        Assert.IsNotNull(updatedUser.Value.ExcelCopilotLastActivityDate);
        Assert.IsNotNull(updatedUser.Value.PowerPointCopilotLastActivityDate);
        Assert.IsNotNull(updatedUser.Value.OutlookCopilotLastActivityDate);
        Assert.IsNotNull(updatedUser.Value.OneNoteCopilotLastActivityDate);
        Assert.IsNotNull(updatedUser.Value.LoopCopilotLastActivityDate);

        _logger.LogInformation("Successfully updated all Copilot activity types");
    }

    [TestMethod]
    public async Task UpdateCachedUsersWithStatsAsync_WithEmptyStatsList_CompletesSuccessfully()
    {
        if (_service == null || _tableServiceClient == null)
        {
            Assert.Inconclusive("Service not initialized - check configuration");
            return;
        }

        // Arrange
        var tableClient = _tableServiceClient.GetTableClient(_testTableName);
        await tableClient.CreateIfNotExistsAsync();

        var emptyStats = new List<CopilotUsageRecord>();

        // Act - Should not throw
        await _service.UpdateCachedUsersWithStatsAsync(tableClient, emptyStats);

        // Assert
        _logger.LogInformation("Successfully handled empty stats list");
    }
}
