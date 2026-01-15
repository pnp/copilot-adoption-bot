using Azure.Data.Tables;
using Common.Engine.Models;
using Common.Engine.Services.UserCache;
using Common.Engine.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTests.Fakes;

namespace UnitTests.Services;

/// <summary>
/// Unit tests for CopilotStatsService using fake loader.
/// </summary>
[TestClass]
public class CopilotStatsServiceTests : AbstractTest
{
    private CopilotStatsService? _service;
    private FakeCopilotStatsLoader? _fakeLoader;
    private TableServiceClient? _tableServiceClient;
    private string _testTableName = string.Empty;

    [TestInitialize]
    public void Initialize()
    {
        _testTableName = $"copilotunittest{DateTime.UtcNow:yyyyMMddHHmmss}";
        
        var testStats = new List<CopilotUsageRecord>
        {
            new CopilotUsageRecord
            {
                UserPrincipalName = "testuser1@contoso.com",
                LastActivityDate = DateTime.UtcNow.AddDays(-1),
                CopilotChatLastActivityDate = DateTime.UtcNow.AddDays(-2),
                TeamsCopilotLastActivityDate = DateTime.UtcNow.AddDays(-3)
            },
            new CopilotUsageRecord
            {
                UserPrincipalName = "testuser2@contoso.com",
                LastActivityDate = DateTime.UtcNow.AddDays(-5),
                WordCopilotLastActivityDate = DateTime.UtcNow.AddDays(-7),
                ExcelCopilotLastActivityDate = DateTime.UtcNow.AddDays(-8)
            }
        };

        _fakeLoader = new FakeCopilotStatsLoader(testStats);
        _service = new CopilotStatsService(
            GetLogger<CopilotStatsService>(),
            _fakeLoader);

        _tableServiceClient = new TableServiceClient(_config.ConnectionStrings.Storage);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        if (_tableServiceClient != null)
        {
            try
            {
                await _tableServiceClient.DeleteTableAsync(_testTableName);
                _logger.LogInformation($"Cleaned up test table: {_testTableName}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error during cleanup: {ex.Message}");
            }
        }
    }

    [TestMethod]
    public async Task GetCopilotUsageStatsAsync_UsesFakeLoader_ReturnsTestData()
    {
        // Act
        var result = await _service!.GetCopilotUsageStatsAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Success);
        Assert.AreEqual(2, result.Records.Count);
        Assert.AreEqual("testuser1@contoso.com", result.Records[0].UserPrincipalName);
        Assert.AreEqual("testuser2@contoso.com", result.Records[1].UserPrincipalName);
    }

    [TestMethod]
    public async Task UpdateCachedUsersWithStatsAsync_UpdatesExistingUsers()
    {
        // Arrange
        var tableClient = _tableServiceClient!.GetTableClient(_testTableName);
        await tableClient.CreateIfNotExistsAsync();

        var testUser = new UserCacheTableEntity
        {
            PartitionKey = UserCacheTableEntity.PartitionKeyVal,
            RowKey = "testuser1@contoso.com",
            UserPrincipalName = "testuser1@contoso.com",
            DisplayName = "Test User 1"
        };
        await tableClient.AddEntityAsync(testUser);

        var result = await _service!.GetCopilotUsageStatsAsync();

        // Act
        await _service.UpdateCachedUsersWithStatsAsync(tableClient, result.Records);

        // Assert
        var updatedUser = await tableClient.GetEntityAsync<UserCacheTableEntity>(
            UserCacheTableEntity.PartitionKeyVal, "testuser1@contoso.com");
        
        Assert.IsNotNull(updatedUser.Value.CopilotLastActivityDate);
        Assert.IsNotNull(updatedUser.Value.CopilotChatLastActivityDate);
        Assert.IsNotNull(updatedUser.Value.TeamscopilotLastActivityDate);
        Assert.IsNotNull(updatedUser.Value.LastCopilotStatsUpdate);
        
        _logger.LogInformation("Successfully updated user with Copilot stats from fake loader");
    }

    [TestMethod]
    public async Task UpdateCachedUsersWithStatsAsync_SkipsNonExistentUsers()
    {
        // Arrange
        var tableClient = _tableServiceClient!.GetTableClient(_testTableName);
        await tableClient.CreateIfNotExistsAsync();

        var result = await _service!.GetCopilotUsageStatsAsync();

        // Act - Should not throw even though users don't exist
        await _service.UpdateCachedUsersWithStatsAsync(tableClient, result.Records);

        // Assert - Table should still be empty
        var entities = tableClient.QueryAsync<UserCacheTableEntity>();
        var count = 0;
        await foreach (var entity in entities)
        {
            count++;
        }

        Assert.AreEqual(0, count, "Should not create new users");
        _logger.LogInformation("Correctly skipped non-existent users");
    }

    [TestMethod]
    public async Task UpdateCachedUsersWithStatsAsync_UpdatesAllCopilotActivityTypes()
    {
        // Arrange
        var tableClient = _tableServiceClient!.GetTableClient(_testTableName);
        await tableClient.CreateIfNotExistsAsync();

        var testUser = new UserCacheTableEntity
        {
            PartitionKey = UserCacheTableEntity.PartitionKeyVal,
            RowKey = "testuser2@contoso.com",
            UserPrincipalName = "testuser2@contoso.com",
            DisplayName = "Test User 2"
        };
        await tableClient.AddEntityAsync(testUser);

        var result = await _service!.GetCopilotUsageStatsAsync();

        // Act
        await _service.UpdateCachedUsersWithStatsAsync(tableClient, result.Records);

        // Assert
        var updatedUser = await tableClient.GetEntityAsync<UserCacheTableEntity>(
            UserCacheTableEntity.PartitionKeyVal, "testuser2@contoso.com");
        
        Assert.IsNotNull(updatedUser.Value.CopilotLastActivityDate);
        Assert.IsNotNull(updatedUser.Value.WordCopilotLastActivityDate);
        Assert.IsNotNull(updatedUser.Value.ExcelCopilotLastActivityDate);
        Assert.IsNotNull(updatedUser.Value.LastCopilotStatsUpdate);

        _logger.LogInformation("Successfully updated all Copilot activity types");
    }

    [TestMethod]
    public async Task UpdateCachedUsersWithStatsAsync_WithEmptyStatsList_CompletesSuccessfully()
    {
        // Arrange
        var tableClient = _tableServiceClient!.GetTableClient(_testTableName);
        await tableClient.CreateIfNotExistsAsync();

        var emptyLoader = new FakeCopilotStatsLoader(new List<CopilotUsageRecord>());
        var service = new CopilotStatsService(
            GetLogger<CopilotStatsService>(),
            emptyLoader);

        var result = await service.GetCopilotUsageStatsAsync();

        // Act - Should not throw
        await service.UpdateCachedUsersWithStatsAsync(tableClient, result.Records);

        // Assert
        Assert.AreEqual(0, result.Records.Count);
        _logger.LogInformation("Successfully handled empty stats list");
    }

    [TestMethod]
    public async Task UpdateCachedUsersWithStatsAsync_UpdatesMultipleUsers()
    {
        // Arrange
        var tableClient = _tableServiceClient!.GetTableClient(_testTableName);
        await tableClient.CreateIfNotExistsAsync();

        var users = new[]
        {
            new UserCacheTableEntity
            {
                PartitionKey = UserCacheTableEntity.PartitionKeyVal,
                RowKey = "testuser1@contoso.com",
                UserPrincipalName = "testuser1@contoso.com"
            },
            new UserCacheTableEntity
            {
                PartitionKey = UserCacheTableEntity.PartitionKeyVal,
                RowKey = "testuser2@contoso.com",
                UserPrincipalName = "testuser2@contoso.com"
            }
        };

        foreach (var user in users)
        {
            await tableClient.AddEntityAsync(user);
        }

        var result = await _service!.GetCopilotUsageStatsAsync();

        // Act
        await _service.UpdateCachedUsersWithStatsAsync(tableClient, result.Records);

        // Assert - Both users should be updated
        foreach (var stat in result.Records)
        {
            var updatedUser = await tableClient.GetEntityAsync<UserCacheTableEntity>(
                UserCacheTableEntity.PartitionKeyVal, stat.UserPrincipalName);
            
            Assert.IsNotNull(updatedUser.Value.CopilotLastActivityDate);
            Assert.IsNotNull(updatedUser.Value.LastCopilotStatsUpdate);
        }

        _logger.LogInformation($"Successfully updated {result.Records.Count} users with Copilot stats");
    }
}
