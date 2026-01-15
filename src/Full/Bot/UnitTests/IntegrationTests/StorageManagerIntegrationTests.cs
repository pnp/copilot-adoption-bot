using Common.Engine;
using Common.Engine.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests.IntegrationTests;

[TestClass]
public class StorageManagerIntegrationTests : AbstractTest
{
    private MessageTemplateStorageManager _messageStorage = null!;
    private SmartGroupStorageManager _smartGroupStorage = null!;

    [TestInitialize]
    public void Initialize()
    {
        _messageStorage = new MessageTemplateStorageManager(
            _config.ConnectionStrings.Storage,
            GetLogger<MessageTemplateStorageManager>()
        );

        _smartGroupStorage = new SmartGroupStorageManager(
            _config.ConnectionStrings.Storage,
            GetLogger<SmartGroupStorageManager>()
        );
    }

    #region MessageTemplateStorageManager Tests

    [TestMethod]
    public async Task MessageTemplateStorage_SaveAndGetTemplate_Success()
    {
        // Arrange
        var templateName = $"Test Template {Guid.NewGuid()}";
        var jsonPayload = "{\"type\":\"AdaptiveCard\",\"version\":\"1.3\",\"body\":[]}";
        var createdBy = "test@example.com";

        // Act
        var saved = await _messageStorage.SaveTemplate(templateName, jsonPayload, createdBy);
        var retrieved = await _messageStorage.GetTemplate(saved.RowKey);

        // Assert
        Assert.IsNotNull(retrieved);
        Assert.AreEqual(templateName, retrieved.TemplateName);
        Assert.AreEqual(createdBy, retrieved.CreatedByUpn);
        Assert.IsFalse(string.IsNullOrEmpty(retrieved.BlobUrl));

        // Cleanup
        await _messageStorage.DeleteTemplate(saved.RowKey);
    }

    [TestMethod]
    public async Task MessageTemplateStorage_GetTemplateJson_Success()
    {
        // Arrange
        var templateName = $"Test Template {Guid.NewGuid()}";
        var jsonPayload = "{\"type\":\"AdaptiveCard\",\"version\":\"1.3\",\"body\":[{\"type\":\"TextBlock\",\"text\":\"Test\"}]}";
        var createdBy = "test@example.com";
        var saved = await _messageStorage.SaveTemplate(templateName, jsonPayload, createdBy);

        // Act
        var retrievedJson = await _messageStorage.GetTemplateJson(saved.RowKey);

        // Assert
        Assert.IsNotNull(retrievedJson);
        Assert.IsTrue(retrievedJson.Contains("AdaptiveCard"));
        Assert.IsTrue(retrievedJson.Contains("TextBlock"));

        // Cleanup
        await _messageStorage.DeleteTemplate(saved.RowKey);
    }

    [TestMethod]
    public async Task MessageTemplateStorage_UpdateTemplate_Success()
    {
        // Arrange
        var templateName = $"Test Template {Guid.NewGuid()}";
        var jsonPayload = "{\"type\":\"AdaptiveCard\",\"version\":\"1.3\"}";
        var saved = await _messageStorage.SaveTemplate(templateName, jsonPayload, "test@example.com");

        var newName = $"Updated Template {Guid.NewGuid()}";
        var newJson = "{\"type\":\"AdaptiveCard\",\"version\":\"1.4\"}";

        // Act
        var updated = await _messageStorage.UpdateTemplate(saved.RowKey, newName, newJson);
        var retrievedJson = await _messageStorage.GetTemplateJson(saved.RowKey);

        // Assert
        Assert.AreEqual(newName, updated.TemplateName);
        Assert.IsTrue(retrievedJson.Contains("1.4"));

        // Cleanup
        await _messageStorage.DeleteTemplate(saved.RowKey);
    }

    [TestMethod]
    public async Task MessageTemplateStorage_CreateAndGetBatch_Success()
    {
        // Arrange
        var template = await _messageStorage.SaveTemplate($"Template {Guid.NewGuid()}", "{}", "test@example.com");
        var batchName = $"Batch {Guid.NewGuid()}";
        var senderUpn = "sender@example.com";

        // Act
        var batch = await _messageStorage.CreateBatch(batchName, template.RowKey, senderUpn);
        var retrieved = await _messageStorage.GetBatch(batch.RowKey);

        // Assert
        Assert.IsNotNull(retrieved);
        Assert.AreEqual(batchName, retrieved.BatchName);
        Assert.AreEqual(template.RowKey, retrieved.TemplateId);
        Assert.AreEqual(senderUpn, retrieved.SenderUpn);

        // Cleanup
        await _messageStorage.DeleteBatch(batch.RowKey);
        await _messageStorage.DeleteTemplate(template.RowKey);
    }

    [TestMethod]
    public async Task MessageTemplateStorage_LogAndGetMessageSend_Success()
    {
        // Arrange
        var template = await _messageStorage.SaveTemplate($"Template {Guid.NewGuid()}", "{}", "test@example.com");
        var batch = await _messageStorage.CreateBatch($"Batch {Guid.NewGuid()}", template.RowKey, "sender@example.com");
        var recipientUpn = "recipient@example.com";
        var status = "Pending";

        // Act
        var log = await _messageStorage.LogMessageSend(batch.RowKey, recipientUpn, status);
        var logs = await _messageStorage.GetMessageLogsByBatch(batch.RowKey);

        // Assert
        Assert.IsTrue(logs.Any(l => l.RowKey == log.RowKey));
        var retrievedLog = logs.First(l => l.RowKey == log.RowKey);
        Assert.AreEqual(batch.RowKey, retrievedLog.MessageBatchId);
        Assert.AreEqual(recipientUpn, retrievedLog.RecipientUpn);
        Assert.AreEqual(status, retrievedLog.Status);

        // Cleanup
        await _messageStorage.DeleteBatch(batch.RowKey);
        await _messageStorage.DeleteTemplate(template.RowKey);
    }

    [TestMethod]
    public async Task MessageTemplateStorage_LogBatchMessages_Success()
    {
        // Arrange
        var template = await _messageStorage.SaveTemplate($"Template {Guid.NewGuid()}", "{}", "test@example.com");
        var batch = await _messageStorage.CreateBatch($"Batch {Guid.NewGuid()}", template.RowKey, "sender@example.com");
        var recipients = new List<string>
        {
            "user1@example.com",
            "user2@example.com",
            "user3@example.com"
        };

        // Act
        var logs = await _messageStorage.LogBatchMessages(batch.RowKey, recipients);

        // Assert
        Assert.AreEqual(3, logs.Count);
        Assert.IsTrue(logs.All(l => l.MessageBatchId == batch.RowKey));
        Assert.IsTrue(logs.All(l => recipients.Contains(l.RecipientUpn ?? "")));

        // Cleanup
        await _messageStorage.DeleteBatch(batch.RowKey);
        await _messageStorage.DeleteTemplate(template.RowKey);
    }

    [TestMethod]
    public async Task MessageTemplateStorage_UpdateMessageLogStatus_Success()
    {
        // Arrange
        var template = await _messageStorage.SaveTemplate($"Template {Guid.NewGuid()}", "{}", "test@example.com");
        var batch = await _messageStorage.CreateBatch($"Batch {Guid.NewGuid()}", template.RowKey, "sender@example.com");
        var log = await _messageStorage.LogMessageSend(batch.RowKey, "user@example.com", "Pending");

        // Act
        await _messageStorage.UpdateMessageLogStatus(log.RowKey, "Sent", null);
        var logs = await _messageStorage.GetMessageLogsByBatch(batch.RowKey);
        var updated = logs.First(l => l.RowKey == log.RowKey);

        // Assert
        Assert.AreEqual("Sent", updated.Status);
        Assert.IsNull(updated.LastError);

        // Cleanup
        await _messageStorage.DeleteBatch(batch.RowKey);
        await _messageStorage.DeleteTemplate(template.RowKey);
    }

    [TestMethod]
    public async Task MessageTemplateStorage_UpdateMessageLogWithError_Success()
    {
        // Arrange
        var template = await _messageStorage.SaveTemplate($"Template {Guid.NewGuid()}", "{}", "test@example.com");
        var batch = await _messageStorage.CreateBatch($"Batch {Guid.NewGuid()}", template.RowKey, "sender@example.com");
        var log = await _messageStorage.LogMessageSend(batch.RowKey, "user@example.com", "Pending");
        var errorMessage = "Test error message";

        // Act
        await _messageStorage.UpdateMessageLogStatus(log.RowKey, "Failed", errorMessage);
        var logs = await _messageStorage.GetMessageLogsByBatch(batch.RowKey);
        var updated = logs.First(l => l.RowKey == log.RowKey);

        // Assert
        Assert.AreEqual("Failed", updated.Status);
        Assert.AreEqual(errorMessage, updated.LastError);

        // Cleanup
        await _messageStorage.DeleteBatch(batch.RowKey);
        await _messageStorage.DeleteTemplate(template.RowKey);
    }

    [TestMethod]
    public async Task MessageTemplateStorage_GetMessageLogsByTemplate_Success()
    {
        // Arrange
        var template = await _messageStorage.SaveTemplate($"Template {Guid.NewGuid()}", "{}", "test@example.com");
        var batch1 = await _messageStorage.CreateBatch($"Batch 1 {Guid.NewGuid()}", template.RowKey, "sender@example.com");
        var batch2 = await _messageStorage.CreateBatch($"Batch 2 {Guid.NewGuid()}", template.RowKey, "sender@example.com");

        await _messageStorage.LogMessageSend(batch1.RowKey, "user1@example.com", "Sent");
        await _messageStorage.LogMessageSend(batch2.RowKey, "user2@example.com", "Sent");

        // Act
        var logs = await _messageStorage.GetMessageLogsByTemplate(template.RowKey);

        // Assert
        Assert.IsTrue(logs.Count >= 2);

        // Cleanup
        await _messageStorage.DeleteBatch(batch1.RowKey);
        await _messageStorage.DeleteBatch(batch2.RowKey);
        await _messageStorage.DeleteTemplate(template.RowKey);
    }

    #endregion

    #region SmartGroupStorageManager Tests

    [TestMethod]
    public async Task SmartGroupStorage_CreateAndGetSmartGroup_Success()
    {
        // Arrange
        var groupName = $"Test Group {Guid.NewGuid()}";
        var description = "Users in Sales department";
        var createdBy = "test@example.com";

        // Act
        var created = await _smartGroupStorage.CreateSmartGroup(groupName, description, createdBy);
        var retrieved = await _smartGroupStorage.GetSmartGroup(created.RowKey);

        // Assert
        Assert.IsNotNull(retrieved);
        Assert.AreEqual(groupName, retrieved.Name);
        Assert.AreEqual(description, retrieved.Description);
        Assert.AreEqual(createdBy, retrieved.CreatedByUpn);

        // Cleanup
        await _smartGroupStorage.DeleteSmartGroup(created.RowKey);
    }

    [TestMethod]
    public async Task SmartGroupStorage_UpdateSmartGroup_Success()
    {
        // Arrange
        var groupName = $"Test Group {Guid.NewGuid()}";
        var created = await _smartGroupStorage.CreateSmartGroup(groupName, "Original desc", "test@example.com");

        var newName = $"Updated Group {Guid.NewGuid()}";
        var newDescription = "Updated description";

        // Act
        var updated = await _smartGroupStorage.UpdateSmartGroup(created.RowKey, newName, newDescription);
        var retrieved = await _smartGroupStorage.GetSmartGroup(created.RowKey);

        // Assert
        Assert.AreEqual(newName, retrieved!.Name);
        Assert.AreEqual(newDescription, retrieved.Description);

        // Cleanup
        await _smartGroupStorage.DeleteSmartGroup(created.RowKey);
    }

    [TestMethod]
    public async Task SmartGroupStorage_CacheAndGetSmartGroupMembers_Success()
    {
        // Arrange
        var group = await _smartGroupStorage.CreateSmartGroup($"Group {Guid.NewGuid()}", "Test", "test@example.com");
        
        var members = new List<SmartGroupMemberCacheEntity>
        {
            new SmartGroupMemberCacheEntity
            {
                RowKey = "user1@example.com",
                DisplayName = "User One",
                Department = "Sales",
                JobTitle = "Sales Manager",
                ConfidenceScore = 0.95
            },
            new SmartGroupMemberCacheEntity
            {
                RowKey = "user2@example.com",
                DisplayName = "User Two",
                Department = "Sales",
                JobTitle = "Sales Rep",
                ConfidenceScore = 0.87
            }
        };

        // Act
        await _smartGroupStorage.CacheSmartGroupMembers(group.RowKey, members);
        var cached = await _smartGroupStorage.GetCachedSmartGroupMembers(group.RowKey);

        // Assert
        Assert.AreEqual(2, cached.Count);
        Assert.IsTrue(cached.Any(m => m.RowKey == "user1@example.com"));
        Assert.IsTrue(cached.Any(m => m.RowKey == "user2@example.com"));

        var user1 = cached.First(m => m.RowKey == "user1@example.com");
        Assert.AreEqual("User One", user1.DisplayName);
        Assert.AreEqual(0.95, user1.ConfidenceScore);

        // Cleanup
        await _smartGroupStorage.DeleteSmartGroup(group.RowKey);
    }

    [TestMethod]
    public async Task SmartGroupStorage_UpdateSmartGroupResolution_Success()
    {
        // Arrange
        var group = await _smartGroupStorage.CreateSmartGroup($"Group {Guid.NewGuid()}", "Test", "test@example.com");
        var memberCount = 5;

        // Act
        await _smartGroupStorage.UpdateSmartGroupResolution(group.RowKey, memberCount);
        var retrieved = await _smartGroupStorage.GetSmartGroup(group.RowKey);

        // Assert
        Assert.IsNotNull(retrieved!.LastResolvedDate);
        Assert.AreEqual(memberCount, retrieved.LastResolvedMemberCount);
        Assert.IsTrue((DateTime.UtcNow - retrieved.LastResolvedDate.Value).TotalMinutes < 1);

        // Cleanup
        await _smartGroupStorage.DeleteSmartGroup(group.RowKey);
    }

    [TestMethod]
    public async Task SmartGroupStorage_GetAllSmartGroups_Success()
    {
        // Arrange
        var group1 = await _smartGroupStorage.CreateSmartGroup($"Group 1 {Guid.NewGuid()}", "Test 1", "test@example.com");
        var group2 = await _smartGroupStorage.CreateSmartGroup($"Group 2 {Guid.NewGuid()}", "Test 2", "test@example.com");

        // Act
        var all = await _smartGroupStorage.GetAllSmartGroups();

        // Assert
        Assert.IsTrue(all.Count >= 2);
        Assert.IsTrue(all.Any(g => g.RowKey == group1.RowKey));
        Assert.IsTrue(all.Any(g => g.RowKey == group2.RowKey));

        // Cleanup
        await _smartGroupStorage.DeleteSmartGroup(group1.RowKey);
        await _smartGroupStorage.DeleteSmartGroup(group2.RowKey);
    }

    [TestMethod]
    public async Task SmartGroupStorage_DeleteSmartGroup_RemovesGroup()
    {
        // Arrange
        var group = await _smartGroupStorage.CreateSmartGroup($"Group {Guid.NewGuid()}", "Test", "test@example.com");

        // Act
        await _smartGroupStorage.DeleteSmartGroup(group.RowKey);
        var retrieved = await _smartGroupStorage.GetSmartGroup(group.RowKey);

        // Assert
        Assert.IsNull(retrieved);
    }

    #endregion
}
