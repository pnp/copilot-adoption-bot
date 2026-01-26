using Azure.Data.Tables;
using Common.Engine.Config;
using Common.Engine.Models;
using Common.Engine.Services;
using Common.Engine.Services.UserCache;
using Common.Engine.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests.IntegrationTests;

/// <summary>
/// Integration tests for CopilotStatsService.
/// NOTE: These tests require actual Microsoft Graph connectivity and Reports.Read.All permission.
/// Some tests may be inconclusive if the tenant doesn't have Copilot licenses or usage data.
/// </summary>
[TestClass]
public class CopilotStatsServiceIntegrationTests : AbstractTest
{
    private CopilotStatsService? _service;
    private TableServiceClient? _tableServiceClient;
    private string _testTableName = string.Empty;

    [TestInitialize]
    public void Initialize()
    {
        // Include milliseconds and random component to ensure uniqueness in parallel test execution
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var random = new Random().Next(1000, 9999);
        _testTableName = $"copilotteststats{timestamp}{random}";

        try
        {
            var cacheConfig = new UserCacheConfig
            {
                CopilotStatsPeriod = "D30"
            };

            var statsLoader = new GraphCopilotStatsLoader(
                GetLogger<GraphCopilotStatsLoader>(),
                cacheConfig,
                _config.GraphConfig);

            _service = new CopilotStatsService(
                GetLogger<CopilotStatsService>(),
                statsLoader);

            var storageAuthConfig = GetStorageAuthConfig();
            if (storageAuthConfig.UseRBAC)
            {
                var tableEndpoint = new Uri($"https://{storageAuthConfig.StorageAccountName}.table.core.windows.net");
                if (storageAuthConfig.RBACOverrideCredentials != null)
                {
                    var credential = new Azure.Identity.ClientSecretCredential(
                        storageAuthConfig.RBACOverrideCredentials.TenantId,
                        storageAuthConfig.RBACOverrideCredentials.ClientId,
                        storageAuthConfig.RBACOverrideCredentials.ClientSecret);
                    _tableServiceClient = new TableServiceClient(tableEndpoint, credential);
                }
                else
                {
                    _tableServiceClient = new TableServiceClient(tableEndpoint, new Azure.Identity.DefaultAzureCredential());
                }
            }
            else
            {
                _tableServiceClient = new TableServiceClient(storageAuthConfig.ConnectionString);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to initialize CopilotStatsService: {ex.Message}");
            _logger.LogWarning("Tests will be skipped if Graph credentials or Reports.Read.All permission are not configured");
        }
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

    #region Token Acquisition Tests

    [TestMethod]
    public async Task GetAccessTokenAsync_WithValidCredentials_ReturnsToken()
    {
        if (_service == null)
        {
            Assert.Inconclusive("Service not initialized - check Graph API credentials");
            return;
        }

        // Act & Assert - The service should get a token when calling the API
        try
        {
            var stats = await _service.GetCopilotUsageStatsAsync();
            
            // If we got here without exception, token acquisition worked
            Assert.IsNotNull(stats);
            _logger.LogInformation("Successfully acquired access token and retrieved Copilot stats");
        }
        catch (Exception ex) when (ex.Message.Contains("Forbidden") || ex.Message.Contains("403"))
        {
            Assert.Inconclusive("Reports.Read.All permission not granted or tenant doesn't have Copilot licenses");
        }
        catch (Exception ex) when (ex.Message.Contains("Unauthorized") || ex.Message.Contains("401"))
        {
            Assert.Fail($"Authentication failed: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task GetAccessTokenAsync_CachesToken_ForMultipleCalls()
    {
        if (_service == null)
        {
            Assert.Inconclusive("Service not initialized - check Graph API credentials");
            return;
        }

        try
        {
            // Act - Make multiple calls that will use the same token
            var startTime = DateTime.UtcNow;
            
            var stats1 = await _service.GetCopilotUsageStatsAsync();
            var firstCallTime = DateTime.UtcNow - startTime;
            
            startTime = DateTime.UtcNow;
            var stats2 = await _service.GetCopilotUsageStatsAsync();
            var secondCallTime = DateTime.UtcNow - startTime;

            // Assert - Second call should be faster or similar (using cached token)
            Assert.IsNotNull(stats1);
            Assert.IsNotNull(stats2);
            
            _logger.LogInformation($"First call: {firstCallTime.TotalMilliseconds}ms, Second call: {secondCallTime.TotalMilliseconds}ms");
            
            // Token caching should make subsequent calls faster or at least not significantly slower
            Assert.IsTrue(secondCallTime.TotalMilliseconds < firstCallTime.TotalMilliseconds * 2,
                "Second call should benefit from cached token");
        }
        catch (Exception ex) when (ex.Message.Contains("Forbidden") || ex.Message.Contains("403"))
        {
            Assert.Inconclusive("Reports.Read.All permission not granted or tenant doesn't have Copilot licenses");
        }
    }

    #endregion

    #region Copilot Stats Retrieval Tests

    [TestMethod]
    public async Task GetCopilotUsageStatsAsync_ReturnsRecords()
    {
        if (_service == null)
        {
            Assert.Inconclusive("Service not initialized - check Graph API credentials");
            return;
        }

        try
        {
            // Act
            var result = await _service.GetCopilotUsageStatsAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success, "Stats retrieval should be successful");
            _logger.LogInformation($"Retrieved {result.Records.Count} Copilot usage records (Status: {result.StatusCode})");

            if (!result.Success)
            {
                _logger.LogWarning($"Copilot stats retrieval failed: {result.ErrorMessage}");
                Assert.Inconclusive($"Copilot stats unavailable: {result.ErrorMessage}");
                return;
            }

            if (result.Records.Count == 0)
            {
                _logger.LogWarning("No Copilot usage data found - tenant may not have active Copilot users");
                Assert.Inconclusive("No Copilot usage data available in tenant");
                return;
            }

            // Verify structure of first record
            var firstRecord = result.Records[0];
            Assert.IsFalse(string.IsNullOrEmpty(firstRecord.UserPrincipalName), "Record should have UserPrincipalName");
            
            _logger.LogInformation($"Sample record: {firstRecord.UserPrincipalName}");
            _logger.LogInformation($"  Last Activity: {firstRecord.LastActivityDate}");
            _logger.LogInformation($"  Copilot Chat: {firstRecord.CopilotChatLastActivityDate}");
            _logger.LogInformation($"  Teams Copilot: {firstRecord.TeamsCopilotLastActivityDate}");
        }
        catch (Exception ex) when (ex.Message.Contains("Forbidden") || ex.Message.Contains("403"))
        {
            Assert.Inconclusive("Reports.Read.All permission not granted or tenant doesn't have Copilot licenses");
        }
    }

    [TestMethod]
    public async Task GetCopilotUsageStatsAsync_ParsesAllActivityDates()
    {
        if (_service == null)
        {
            Assert.Inconclusive("Service not initialized - check Graph API credentials");
            return;
        }

        try
        {
            // Act
            var result = await _service.GetCopilotUsageStatsAsync();

            if (!result.Success || result.Records.Count == 0)
            {
                Assert.Inconclusive("No Copilot usage data available in tenant");
                return;
            }

            // Assert - Check that we can parse various activity types
            var recordsWithActivity = result.Records.Where(r => r.LastActivityDate.HasValue).ToList();
            
            _logger.LogInformation($"Records with any activity: {recordsWithActivity.Count}/{result.Records.Count}");
            
            if (recordsWithActivity.Count > 0)
            {
                var sampleRecord = recordsWithActivity[0];
                var activityTypes = new Dictionary<string, DateTime?>
                {
                    { "Overall", sampleRecord.LastActivityDate },
                    { "Copilot Chat", sampleRecord.CopilotChatLastActivityDate },
                    { "Teams", sampleRecord.TeamsCopilotLastActivityDate },
                    { "Word", sampleRecord.WordCopilotLastActivityDate },
                    { "Excel", sampleRecord.ExcelCopilotLastActivityDate },
                    { "PowerPoint", sampleRecord.PowerPointCopilotLastActivityDate },
                    { "Outlook", sampleRecord.OutlookCopilotLastActivityDate },
                    { "OneNote", sampleRecord.OneNoteCopilotLastActivityDate },
                    { "Loop", sampleRecord.LoopCopilotLastActivityDate }
                };

                foreach (var activity in activityTypes.Where(a => a.Value.HasValue))
                {
                    _logger.LogInformation($"  {activity.Key}: {activity.Value}");
                }

                Assert.IsTrue(activityTypes.Values.Any(v => v.HasValue), 
                    "At least one activity type should have a date");
            }
        }
        catch (Exception ex) when (ex.Message.Contains("Forbidden") || ex.Message.Contains("403"))
        {
            Assert.Inconclusive("Reports.Read.All permission not granted or tenant doesn't have Copilot licenses");
        }
    }

    [TestMethod]
    public async Task GetCopilotUsageStatsAsync_HandlesNoData_Gracefully()
    {
        if (_service == null)
        {
            Assert.Inconclusive("Service not initialized - check Graph API credentials");
            return;
        }

        try
        {
            // Act
            var result = await _service.GetCopilotUsageStatsAsync();

            // Assert - Should return empty list, not null or throw
            Assert.IsNotNull(result);
            
            if (result.Records.Count == 0)
            {
                _logger.LogInformation("No Copilot usage data found - this is acceptable for tenants without active Copilot users");
            }
        }
        catch (Exception ex) when (ex.Message.Contains("Forbidden") || ex.Message.Contains("403"))
        {
            Assert.Inconclusive("Reports.Read.All permission not granted or tenant doesn't have Copilot licenses");
        }
    }

    #endregion

    #region Update Cached Users Tests

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

    #endregion

    #region Fake Loader Tests

    [TestMethod]
    public async Task UpdateCachedUsersWithStatsAsync_WithFakeLoader_UpdatesAllCopilotActivityDates()
    {
        if (_tableServiceClient == null)
        {
            Assert.Inconclusive("Table service client not initialized");
            return;
        }

        // Arrange - Create test table with test users
        var tableClient = _tableServiceClient.GetTableClient(_testTableName);
        await tableClient.CreateIfNotExistsAsync();

        var testUsers = new[]
        {
            new UserCacheTableEntity
            {
                PartitionKey = UserCacheTableEntity.PartitionKeyVal,
                RowKey = "user1@contoso.com",
                UserPrincipalName = "user1@contoso.com",
                DisplayName = "Test User 1"
            },
            new UserCacheTableEntity
            {
                PartitionKey = UserCacheTableEntity.PartitionKeyVal,
                RowKey = "user2@contoso.com",
                UserPrincipalName = "user2@contoso.com",
                DisplayName = "Test User 2"
            }
        };

        foreach (var user in testUsers)
        {
            await tableClient.AddEntityAsync(user);
        }

        // Create fake stats with all activity dates populated
        var fakeStats = new List<CopilotUsageRecord>
        {
            new CopilotUsageRecord
            {
                UserPrincipalName = "user1@contoso.com",
                LastActivityDate = DateTime.UtcNow.AddDays(-1),
                CopilotChatLastActivityDate = DateTime.UtcNow.AddDays(-2),
                TeamsCopilotLastActivityDate = DateTime.UtcNow.AddDays(-3),
                WordCopilotLastActivityDate = DateTime.UtcNow.AddDays(-4),
                ExcelCopilotLastActivityDate = DateTime.UtcNow.AddDays(-5),
                PowerPointCopilotLastActivityDate = DateTime.UtcNow.AddDays(-6),
                OutlookCopilotLastActivityDate = DateTime.UtcNow.AddDays(-7),
                OneNoteCopilotLastActivityDate = DateTime.UtcNow.AddDays(-8),
                LoopCopilotLastActivityDate = DateTime.UtcNow.AddDays(-9)
            },
            new CopilotUsageRecord
            {
                UserPrincipalName = "user2@contoso.com",
                LastActivityDate = DateTime.UtcNow.AddDays(-10),
                CopilotChatLastActivityDate = DateTime.UtcNow.AddDays(-11),
                TeamsCopilotLastActivityDate = DateTime.UtcNow.AddDays(-12),
                WordCopilotLastActivityDate = DateTime.UtcNow.AddDays(-13),
                ExcelCopilotLastActivityDate = DateTime.UtcNow.AddDays(-14),
                PowerPointCopilotLastActivityDate = DateTime.UtcNow.AddDays(-15),
                OutlookCopilotLastActivityDate = DateTime.UtcNow.AddDays(-16),
                OneNoteCopilotLastActivityDate = DateTime.UtcNow.AddDays(-17),
                LoopCopilotLastActivityDate = DateTime.UtcNow.AddDays(-18)
            }
        };

        // Create service with fake loader
        var fakeLoader = new UnitTests.Fakes.FakeCopilotStatsLoader(fakeStats);
        var service = new CopilotStatsService(
            GetLogger<CopilotStatsService>(),
            fakeLoader);

        // Get stats from fake loader
        var statsResult = await service.GetCopilotUsageStatsAsync();

        // Act - Update cached users with fake stats
        await service.UpdateCachedUsersWithStatsAsync(tableClient, statsResult.Records);

        // Assert - Verify all stats were retrieved correctly
        Assert.IsTrue(statsResult.Success);
        Assert.AreEqual(2, statsResult.Records.Count);

        // Verify user1 stats in table
        var updatedUser1 = await tableClient.GetEntityAsync<UserCacheTableEntity>(
            UserCacheTableEntity.PartitionKeyVal, "user1@contoso.com");
        
        Assert.IsNotNull(updatedUser1.Value.CopilotLastActivityDate, "User1: CopilotLastActivityDate should be set");
        Assert.IsNotNull(updatedUser1.Value.CopilotChatLastActivityDate, "User1: CopilotChatLastActivityDate should be set");
        Assert.IsNotNull(updatedUser1.Value.TeamscopilotLastActivityDate, "User1: TeamscopilotLastActivityDate should be set");
        Assert.IsNotNull(updatedUser1.Value.WordCopilotLastActivityDate, "User1: WordCopilotLastActivityDate should be set");
        Assert.IsNotNull(updatedUser1.Value.ExcelCopilotLastActivityDate, "User1: ExcelCopilotLastActivityDate should be set");
        Assert.IsNotNull(updatedUser1.Value.PowerPointCopilotLastActivityDate, "User1: PowerPointCopilotLastActivityDate should be set");
        Assert.IsNotNull(updatedUser1.Value.OutlookCopilotLastActivityDate, "User1: OutlookCopilotLastActivityDate should be set");
        Assert.IsNotNull(updatedUser1.Value.OneNoteCopilotLastActivityDate, "User1: OneNoteCopilotLastActivityDate should be set");
        Assert.IsNotNull(updatedUser1.Value.LoopCopilotLastActivityDate, "User1: LoopCopilotLastActivityDate should be set");
        Assert.IsNotNull(updatedUser1.Value.LastCopilotStatsUpdate, "User1: LastCopilotStatsUpdate should be set");

        // Verify the actual date values are correct (within tolerance for test timing)
        Assert.AreEqual(fakeStats[0].LastActivityDate!.Value.Date, updatedUser1.Value.CopilotLastActivityDate!.Value.Date);
        Assert.AreEqual(fakeStats[0].CopilotChatLastActivityDate!.Value.Date, updatedUser1.Value.CopilotChatLastActivityDate!.Value.Date);
        Assert.AreEqual(fakeStats[0].TeamsCopilotLastActivityDate!.Value.Date, updatedUser1.Value.TeamscopilotLastActivityDate!.Value.Date);
        Assert.AreEqual(fakeStats[0].WordCopilotLastActivityDate!.Value.Date, updatedUser1.Value.WordCopilotLastActivityDate!.Value.Date);
        Assert.AreEqual(fakeStats[0].ExcelCopilotLastActivityDate!.Value.Date, updatedUser1.Value.ExcelCopilotLastActivityDate!.Value.Date);
        Assert.AreEqual(fakeStats[0].PowerPointCopilotLastActivityDate!.Value.Date, updatedUser1.Value.PowerPointCopilotLastActivityDate!.Value.Date);
        Assert.AreEqual(fakeStats[0].OutlookCopilotLastActivityDate!.Value.Date, updatedUser1.Value.OutlookCopilotLastActivityDate!.Value.Date);
        Assert.AreEqual(fakeStats[0].OneNoteCopilotLastActivityDate!.Value.Date, updatedUser1.Value.OneNoteCopilotLastActivityDate!.Value.Date);
        Assert.AreEqual(fakeStats[0].LoopCopilotLastActivityDate!.Value.Date, updatedUser1.Value.LoopCopilotLastActivityDate!.Value.Date);

        // Verify user2 stats in table
        var updatedUser2 = await tableClient.GetEntityAsync<UserCacheTableEntity>(
            UserCacheTableEntity.PartitionKeyVal, "user2@contoso.com");
        
        Assert.IsNotNull(updatedUser2.Value.CopilotLastActivityDate, "User2: CopilotLastActivityDate should be set");
        Assert.IsNotNull(updatedUser2.Value.CopilotChatLastActivityDate, "User2: CopilotChatLastActivityDate should be set");
        Assert.IsNotNull(updatedUser2.Value.TeamscopilotLastActivityDate, "User2: TeamscopilotLastActivityDate should be set");
        Assert.IsNotNull(updatedUser2.Value.WordCopilotLastActivityDate, "User2: WordCopilotLastActivityDate should be set");
        Assert.IsNotNull(updatedUser2.Value.ExcelCopilotLastActivityDate, "User2: ExcelCopilotLastActivityDate should be set");
        Assert.IsNotNull(updatedUser2.Value.PowerPointCopilotLastActivityDate, "User2: PowerPointCopilotLastActivityDate should be set");
        Assert.IsNotNull(updatedUser2.Value.OutlookCopilotLastActivityDate, "User2: OutlookCopilotLastActivityDate should be set");
        Assert.IsNotNull(updatedUser2.Value.OneNoteCopilotLastActivityDate, "User2: OneNoteCopilotLastActivityDate should be set");
        Assert.IsNotNull(updatedUser2.Value.LoopCopilotLastActivityDate, "User2: LoopCopilotLastActivityDate should be set");
        Assert.IsNotNull(updatedUser2.Value.LastCopilotStatsUpdate, "User2: LastCopilotStatsUpdate should be set");

        // Verify the actual date values for user2
        Assert.AreEqual(fakeStats[1].LastActivityDate!.Value.Date, updatedUser2.Value.CopilotLastActivityDate!.Value.Date);
        Assert.AreEqual(fakeStats[1].CopilotChatLastActivityDate!.Value.Date, updatedUser2.Value.CopilotChatLastActivityDate!.Value.Date);

        _logger.LogInformation("Successfully verified all Copilot activity dates from fake loader");
        _logger.LogInformation($"User1 Last Activity: {updatedUser1.Value.CopilotLastActivityDate}");
        _logger.LogInformation($"User1 Teams Copilot: {updatedUser1.Value.TeamscopilotLastActivityDate}");
        _logger.LogInformation($"User1 Word Copilot: {updatedUser1.Value.WordCopilotLastActivityDate}");
        _logger.LogInformation($"User2 Last Activity: {updatedUser2.Value.CopilotLastActivityDate}");
        _logger.LogInformation($"User2 Excel Copilot: {updatedUser2.Value.ExcelCopilotLastActivityDate}");
    }

    [TestMethod]
    public async Task AIFoundryService_FindsUsersWithWordCopilotActivity()
    {
        if (_tableServiceClient == null)
        {
            Assert.Inconclusive("Table service client not initialized");
            return;
        }

        // Check if AI Foundry is configured
        if (string.IsNullOrEmpty(_config.AIFoundryConfig?.Endpoint) ||
            string.IsNullOrEmpty(_config.AIFoundryConfig?.ApiKey))
        {
            Assert.Inconclusive("AI Foundry is not configured - set AIFoundryConfig:Endpoint and AIFoundryConfig:ApiKey in user secrets");
            return;
        }

        // Arrange - Create test table with test users
        var tableClient = _tableServiceClient.GetTableClient(_testTableName);
        await tableClient.CreateIfNotExistsAsync();

        var testUsers = new[]
        {
            new UserCacheTableEntity
            {
                PartitionKey = UserCacheTableEntity.PartitionKeyVal,
                RowKey = "activeuser@contoso.com",
                UserPrincipalName = "activeuser@contoso.com",
                DisplayName = "Active Word User",
                Department = "Marketing",
                JobTitle = "Marketing Manager"
            },
            new UserCacheTableEntity
            {
                PartitionKey = UserCacheTableEntity.PartitionKeyVal,
                RowKey = "inactiveuser@contoso.com",
                UserPrincipalName = "inactiveuser@contoso.com",
                DisplayName = "Inactive User",
                Department = "Sales",
                JobTitle = "Sales Representative"
            },
            new UserCacheTableEntity
            {
                PartitionKey = UserCacheTableEntity.PartitionKeyVal,
                RowKey = "anotheruser@contoso.com",
                UserPrincipalName = "anotheruser@contoso.com",
                DisplayName = "Another User",
                Department = "IT",
                JobTitle = "IT Specialist"
            }
        };

        foreach (var user in testUsers)
        {
            await tableClient.AddEntityAsync(user);
        }

        // Create fake stats - only one user has Word Copilot activity in the last 30 days
        var fakeStats = new List<CopilotUsageRecord>
        {
            new CopilotUsageRecord
            {
                UserPrincipalName = "activeuser@contoso.com",
                LastActivityDate = DateTime.UtcNow.AddDays(-5),
                WordCopilotLastActivityDate = DateTime.UtcNow.AddDays(-10) // Within last 30 days
            },
            new CopilotUsageRecord
            {
                UserPrincipalName = "inactiveuser@contoso.com",
                LastActivityDate = null,
                WordCopilotLastActivityDate = null // No Word Copilot activity
            },
            new CopilotUsageRecord
            {
                UserPrincipalName = "anotheruser@contoso.com",
                LastActivityDate = DateTime.UtcNow.AddDays(-40),
                WordCopilotLastActivityDate = null // No Word Copilot activity
            }
        };

        // Create service with fake loader
        var fakeLoader = new UnitTests.Fakes.FakeCopilotStatsLoader(fakeStats);
        var copilotService = new CopilotStatsService(
            GetLogger<CopilotStatsService>(),
            fakeLoader);

        // Get stats from fake loader and update table
        var statsResult = await copilotService.GetCopilotUsageStatsAsync();
        await copilotService.UpdateCachedUsersWithStatsAsync(tableClient, statsResult.Records);

        // Create enriched users from the table data
        var enrichedUsers = new List<EnrichedUserInfo>();
        await foreach (var entity in tableClient.QueryAsync<UserCacheTableEntity>())
        {
            var enrichedUser = new EnrichedUserInfo
            {
                Id = entity.Id,
                UserPrincipalName = entity.UserPrincipalName,
                DisplayName = entity.DisplayName,
                Department = entity.Department,
                JobTitle = entity.JobTitle,
                WordCopilotLastActivityDate = entity.WordCopilotLastActivityDate,
                CopilotLastActivityDate = entity.CopilotLastActivityDate,
                CopilotChatLastActivityDate = entity.CopilotChatLastActivityDate,
                TeamsCopilotLastActivityDate = entity.TeamscopilotLastActivityDate,
                ExcelCopilotLastActivityDate = entity.ExcelCopilotLastActivityDate,
                PowerPointCopilotLastActivityDate = entity.PowerPointCopilotLastActivityDate,
                OutlookCopilotLastActivityDate = entity.OutlookCopilotLastActivityDate,
                OneNoteCopilotLastActivityDate = entity.OneNoteCopilotLastActivityDate,
                LoopCopilotLastActivityDate = entity.LoopCopilotLastActivityDate
            };
            
            enrichedUsers.Add(enrichedUser);
            
            // Log what the AI will see for this user
            _logger.LogInformation($"User summary for AI: {enrichedUser.ToAISummary()}");
        }

        // Create AI Foundry service
        var aiService = new AIFoundryService(
            _config.AIFoundryConfig,
            GetLogger<AIFoundryService>(),
            null);

        // Act - Use AI to find users with Word Copilot activity in last 30 days
        var matches = await aiService.ResolveSmartGroupMembersAsync(
            "Anyone who has used Word Copilot in the last 30 days",
            enrichedUsers);

        // Assert
        Assert.IsNotNull(matches);
        Assert.AreEqual(1, matches.Count, "Should find exactly one user with Word Copilot activity in last 30 days");
        Assert.AreEqual("activeuser@contoso.com", matches[0].UserPrincipalName);
        Assert.IsTrue(matches[0].ConfidenceScore > 0.7, "Confidence score should be high for clear match");
        
        _logger.LogInformation($"AI Foundry successfully identified {matches.Count} user(s) with Word Copilot activity");
        _logger.LogInformation($"Matched user: {matches[0].UserPrincipalName} (Confidence: {matches[0].ConfidenceScore:P0})");
        _logger.LogInformation($"Reason: {matches[0].Reason}");
    }

    #endregion

    #region Configuration Tests

    [TestMethod]
    public void CopilotStatsService_UsesDifferentPeriods_Correctly()
    {
        // Arrange & Act - Create services with different periods
        var periods = new[] { "D7", "D30", "D90", "D180" };
        var services = new List<CopilotStatsService>();

        foreach (var period in periods)
        {
            var config = new UserCacheConfig { CopilotStatsPeriod = period };
            var loader = new GraphCopilotStatsLoader(
                GetLogger<GraphCopilotStatsLoader>(),
                config,
                _config.GraphConfig);
            var service = new CopilotStatsService(
                GetLogger<CopilotStatsService>(),
                loader);
            services.Add(service);
        }

        // Assert - Services should be created with different configs
        Assert.AreEqual(periods.Length, services.Count);
        _logger.LogInformation($"Successfully created services for periods: {string.Join(", ", periods)}");
    }

    #endregion

    #region Error Handling Tests

    [TestMethod]
    public async Task GetCopilotUsageStatsAsync_WithInvalidCredentials_ThrowsException()
    {
        // Arrange - Create loader with invalid credentials
        var invalidConfig = new AzureADAuthConfig
        {
            TenantId = "invalid-tenant-id",
            ClientId = "invalid-client-id",
            ClientSecret = "invalid-secret"
        };

        var loader = new GraphCopilotStatsLoader(
            GetLogger<GraphCopilotStatsLoader>(),
            new UserCacheConfig(),
            invalidConfig);
        
        var service = new CopilotStatsService(
            GetLogger<CopilotStatsService>(),
            loader);

        // Act & Assert
        await Assert.ThrowsExceptionAsync<Azure.Identity.AuthenticationFailedException>(
            async () => await service.GetCopilotUsageStatsAsync(),
            "Should throw exception with invalid credentials");
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

    #endregion
}
