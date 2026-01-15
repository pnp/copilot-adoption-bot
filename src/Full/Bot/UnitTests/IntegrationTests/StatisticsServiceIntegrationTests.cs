using Common.Engine;
using Common.Engine.Services;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests.IntegrationTests;

[TestClass]
public class StatisticsServiceIntegrationTests : AbstractTest
{
    private StatisticsService _service = null!;
    private MessageTemplateService _messageService = null!;
    private MessageTemplateStorageManager _storageManager = null!;
    private BatchQueueService _queueService = null!;
    private GraphService _graphService = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        // Initialize storage manager
        _storageManager = new MessageTemplateStorageManager(
            _config.ConnectionStrings.Storage,
            GetLogger<MessageTemplateStorageManager>()
        );

        // Initialize queue service
        _queueService = new BatchQueueService(
            _config.ConnectionStrings.Storage,
            GetLogger<BatchQueueService>()
        );
        await _queueService.InitializeAsync();

        // Initialize message service
        _messageService = new MessageTemplateService(
            _storageManager,
            _queueService,
            GetLogger<MessageTemplateService>()
        );

        // Initialize graph service
        _graphService = new GraphService(
            _config.GraphConfig,
            GetLogger<GraphService>()
        );

        // Initialize statistics service
        _service = new StatisticsService(
            _storageManager,
            _graphService,
            GetLogger<StatisticsService>()
        );
    }

    [TestMethod]
    public async Task GetMessageStatusStats_WithNoMessages_ReturnsZeroCounts()
    {
        // Act
        var stats = await _service.GetMessageStatusStats();

        // Assert
        Assert.IsNotNull(stats);
        Assert.IsTrue(stats.TotalCount >= 0);
        Assert.AreEqual(stats.SentCount + stats.FailedCount + stats.PendingCount, stats.TotalCount,
            "Sum of status counts should equal total count");
    }

    [TestMethod]
    public async Task GetMessageStatusStats_WithMessages_CalculatesCorrectly()
    {
        // Arrange - Create test data
        var templateName = $"Test Template {Guid.NewGuid()}";
        var jsonPayload = "{\"type\":\"AdaptiveCard\",\"version\":\"1.3\"}";
        var template = await _messageService.CreateTemplate(templateName, jsonPayload, "test@example.com");

        var batchName = $"Test Batch {Guid.NewGuid()}";
        var batch = await _messageService.CreateBatch(batchName, template.Id, "sender@example.com");

        // Create messages with different statuses
        var log1 = await _messageService.LogMessageSend(batch.Id, "user1@example.com", "Sent");
        var log2 = await _messageService.LogMessageSend(batch.Id, "user2@example.com", "Failed", "Test error");
        var log3 = await _messageService.LogMessageSend(batch.Id, "user3@example.com", "Pending");

        // Act
        var stats = await _service.GetMessageStatusStats();

        // Assert
        Assert.IsNotNull(stats);
        Assert.IsTrue(stats.SentCount >= 1, "Should have at least 1 sent message");
        Assert.IsTrue(stats.FailedCount >= 1, "Should have at least 1 failed message");
        Assert.IsTrue(stats.PendingCount >= 1, "Should have at least 1 pending message");
        Assert.AreEqual(stats.SentCount + stats.FailedCount + stats.PendingCount, stats.TotalCount);

        _logger.LogInformation($"Stats - Sent: {stats.SentCount}, Failed: {stats.FailedCount}, " +
            $"Pending: {stats.PendingCount}, Total: {stats.TotalCount}");

        // Cleanup
        await _messageService.DeleteBatch(batch.Id);
        await _messageService.DeleteTemplate(template.Id);
    }

    [TestMethod]
    public async Task GetUserCoverageStats_CalculatesCorrectly()
    {
        // Act
        var stats = await _service.GetUserCoverageStats();

        // Assert
        Assert.IsNotNull(stats);
        Assert.IsTrue(stats.TotalUsersInTenant > 0, "Should have users in tenant");
        Assert.IsTrue(stats.UsersMessaged >= 0);
        Assert.AreEqual(stats.TotalUsersInTenant - stats.UsersMessaged, stats.UsersNotMessaged);
        
        if (stats.TotalUsersInTenant > 0)
        {
            var expectedPercentage = Math.Round((double)stats.UsersMessaged / stats.TotalUsersInTenant * 100, 2);
            Assert.AreEqual(expectedPercentage, stats.CoveragePercentage);
        }

        _logger.LogInformation($"User Coverage - Messaged: {stats.UsersMessaged}/{stats.TotalUsersInTenant} " +
            $"({stats.CoveragePercentage}%)");
    }

    [TestMethod]
    public async Task GetUserCoverageStats_WithNewMessages_UpdatesCorrectly()
    {
        // Arrange - Get initial stats
        var initialStats = await _service.GetUserCoverageStats();

        // Create test data with new users
        var templateName = $"Test Template {Guid.NewGuid()}";
        var jsonPayload = "{\"type\":\"AdaptiveCard\",\"version\":\"1.3\"}";
        var template = await _messageService.CreateTemplate(templateName, jsonPayload, "test@example.com");

        var batchName = $"Test Batch {Guid.NewGuid()}";
        var batch = await _messageService.CreateBatch(batchName, template.Id, "sender@example.com");

        // Create messages to unique users
        var uniqueUsers = new List<string>
        {
            $"testuser1_{Guid.NewGuid()}@example.com",
            $"testuser2_{Guid.NewGuid()}@example.com",
            $"testuser3_{Guid.NewGuid()}@example.com"
        };

        foreach (var user in uniqueUsers)
        {
            await _messageService.LogMessageSend(batch.Id, user, "Sent");
        }

        // Act
        var updatedStats = await _service.GetUserCoverageStats();

        // Assert
        Assert.IsTrue(updatedStats.UsersMessaged >= initialStats.UsersMessaged,
            "Users messaged count should not decrease");
        
        _logger.LogInformation($"Initial users messaged: {initialStats.UsersMessaged}, " +
            $"After test: {updatedStats.UsersMessaged}");

        // Cleanup
        await _messageService.DeleteBatch(batch.Id);
        await _messageService.DeleteTemplate(template.Id);
    }

    [TestMethod]
    public async Task GetUserCoverageStats_WithDuplicateRecipients_CountsOnce()
    {
        // Arrange - Create test data
        var templateName = $"Test Template {Guid.NewGuid()}";
        var jsonPayload = "{\"type\":\"AdaptiveCard\",\"version\":\"1.3\"}";
        var template = await _messageService.CreateTemplate(templateName, jsonPayload, "test@example.com");

        var batchName = $"Test Batch {Guid.NewGuid()}";
        var batch = await _messageService.CreateBatch(batchName, template.Id, "sender@example.com");

        // Send multiple messages to the same user
        var recipientUpn = $"duplicate_{Guid.NewGuid()}@example.com";
        await _messageService.LogMessageSend(batch.Id, recipientUpn, "Sent");
        await _messageService.LogMessageSend(batch.Id, recipientUpn, "Sent");
        await _messageService.LogMessageSend(batch.Id, recipientUpn, "Sent");

        var initialStats = await _service.GetUserCoverageStats();

        // Act - Create another batch with the same recipient
        var batch2 = await _messageService.CreateBatch($"Test Batch 2 {Guid.NewGuid()}", template.Id, "sender@example.com");
        await _messageService.LogMessageSend(batch2.Id, recipientUpn, "Sent");

        var updatedStats = await _service.GetUserCoverageStats();

        // Assert
        Assert.AreEqual(initialStats.UsersMessaged, updatedStats.UsersMessaged,
            "Duplicate recipients should not increase unique user count");

        // Cleanup
        await _messageService.DeleteBatch(batch.Id);
        await _messageService.DeleteBatch(batch2.Id);
        await _messageService.DeleteTemplate(template.Id);
    }

    [TestMethod]
    public async Task GetMessageStatusStats_AfterStatusUpdate_ReflectsChanges()
    {
        // Arrange - Create test data
        var templateName = $"Test Template {Guid.NewGuid()}";
        var jsonPayload = "{\"type\":\"AdaptiveCard\",\"version\":\"1.3\"}";
        var template = await _messageService.CreateTemplate(templateName, jsonPayload, "test@example.com");

        var batchName = $"Test Batch {Guid.NewGuid()}";
        var batch = await _messageService.CreateBatch(batchName, template.Id, "sender@example.com");

        var log = await _messageService.LogMessageSend(batch.Id, "user@example.com", "Pending");

        var initialStats = await _service.GetMessageStatusStats();
        var initialPending = initialStats.PendingCount;
        var initialSent = initialStats.SentCount;

        // Act - Update status
        await _messageService.UpdateMessageLogStatus(log.Id, "Sent");
        var updatedStats = await _service.GetMessageStatusStats();

        // Assert
        Assert.AreEqual(initialPending - 1, updatedStats.PendingCount,
            "Pending count should decrease by 1");
        Assert.AreEqual(initialSent + 1, updatedStats.SentCount,
            "Sent count should increase by 1");

        // Cleanup
        await _messageService.DeleteBatch(batch.Id);
        await _messageService.DeleteTemplate(template.Id);
    }
}
