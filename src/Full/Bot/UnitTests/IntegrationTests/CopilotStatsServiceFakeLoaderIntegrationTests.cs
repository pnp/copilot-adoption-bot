using Engine.Models;
using Engine.Services;
using Engine.Services.UserCache;
using Engine.Storage;
using Microsoft.Extensions.Logging;

namespace UnitTests.IntegrationTests;

/// <summary>
/// Integration tests that exercise <see cref="CopilotStatsService"/> using a
/// <see cref="Fakes.FakeCopilotStatsLoader"/> so they don't require Microsoft Graph credentials,
/// but still verify behavior end-to-end against Azure Table Storage and optionally AI Foundry.
/// </summary>
[TestClass]
public class CopilotStatsServiceFakeLoaderIntegrationTests : CopilotStatsServiceIntegrationTestBase
{
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

        // Check if AI Foundry is configured. AI Foundry only supports Azure RBAC authentication,
        // so we just need an endpoint and a deployment name. DefaultAzureCredential (or the
        // optional service principal override) handles the actual auth.
        var aiConfig = _config.AIFoundryConfig;
        var hasEndpoint = !string.IsNullOrEmpty(aiConfig?.Endpoint);
        var hasDeployment = !string.IsNullOrEmpty(aiConfig?.DeploymentName);
        if (!hasEndpoint || !hasDeployment)
        {
            Assert.Inconclusive("AI Foundry is not configured - set AIFoundryConfig:Endpoint and AIFoundryConfig:DeploymentName in user secrets (RBAC is used automatically)");
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
            aiConfig!,
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
}
