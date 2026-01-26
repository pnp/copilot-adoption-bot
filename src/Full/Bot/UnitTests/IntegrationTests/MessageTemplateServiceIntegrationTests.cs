using Common.Engine;
using Common.Engine.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests.IntegrationTests;

[TestClass]
public class MessageTemplateServiceIntegrationTests : AbstractTest
{
    private MessageTemplateService _service = null!;
    private MessageTemplateStorageManager _storageManager = null!;
    private BatchQueueService _queueService = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        // Initialize storage manager
        _storageManager = new MessageTemplateStorageManager(
            GetStorageAuthConfig(),
            GetLogger<MessageTemplateStorageManager>()
        );

        // Initialize queue service
        _queueService = new BatchQueueService(
            GetStorageAuthConfig(),
            GetLogger<BatchQueueService>()
        );
        await _queueService.InitializeAsync();

        // Initialize service
        _service = new MessageTemplateService(
            _storageManager,
            _queueService,
            GetLogger<MessageTemplateService>()
        );
    }

    [TestMethod]
    public async Task CreateAndGetTemplate_Success()
    {
        // Arrange
        var templateName = $"Test Template {Guid.NewGuid()}";
        var jsonPayload = "{\"type\":\"AdaptiveCard\",\"version\":\"1.3\"}";
        var createdBy = "test@example.com";

        // Act
        var createdTemplate = await _service.CreateTemplate(templateName, jsonPayload, createdBy);
        var retrievedTemplate = await _service.GetTemplate(createdTemplate.Id);

        // Assert
        Assert.IsNotNull(retrievedTemplate);
        Assert.AreEqual(templateName, retrievedTemplate.TemplateName);
        Assert.AreEqual(createdBy, retrievedTemplate.CreatedByUpn);
        Assert.IsNotNull(retrievedTemplate.BlobUrl);

        // Cleanup
        await _service.DeleteTemplate(createdTemplate.Id);
    }

    [TestMethod]
    public async Task GetTemplateJson_Success()
    {
        // Arrange
        var templateName = $"Test Template {Guid.NewGuid()}";
        var jsonPayload = "{\"type\":\"AdaptiveCard\",\"version\":\"1.3\",\"body\":[]}";
        var createdBy = "test@example.com";

        var createdTemplate = await _service.CreateTemplate(templateName, jsonPayload, createdBy);

        // Act
        var retrievedJson = await _service.GetTemplateJson(createdTemplate.Id);

        // Assert
        Assert.IsNotNull(retrievedJson);
        Assert.IsTrue(retrievedJson.Contains("AdaptiveCard"));

        // Cleanup
        await _service.DeleteTemplate(createdTemplate.Id);
    }

    [TestMethod]
    public async Task UpdateTemplate_Success()
    {
        // Arrange
        var templateName = $"Test Template {Guid.NewGuid()}";
        var jsonPayload = "{\"type\":\"AdaptiveCard\",\"version\":\"1.3\"}";
        var createdBy = "test@example.com";

        var createdTemplate = await _service.CreateTemplate(templateName, jsonPayload, createdBy);

        var newTemplateName = $"Updated Template {Guid.NewGuid()}";
        var newJsonPayload = "{\"type\":\"AdaptiveCard\",\"version\":\"1.4\"}";

        // Act
        var updatedTemplate = await _service.UpdateTemplate(
            createdTemplate.Id, 
            newTemplateName, 
            newJsonPayload
        );

        // Assert
        Assert.IsNotNull(updatedTemplate);
        Assert.AreEqual(newTemplateName, updatedTemplate.TemplateName);

        // Cleanup
        await _service.DeleteTemplate(createdTemplate.Id);
    }

    [TestMethod]
    public async Task GetAllTemplates_Success()
    {
        // Arrange
        var templateName1 = $"Test Template 1 {Guid.NewGuid()}";
        var templateName2 = $"Test Template 2 {Guid.NewGuid()}";
        var jsonPayload = "{\"type\":\"AdaptiveCard\",\"version\":\"1.3\"}";
        var createdBy = "test@example.com";

        var template1 = await _service.CreateTemplate(templateName1, jsonPayload, createdBy);
        var template2 = await _service.CreateTemplate(templateName2, jsonPayload, createdBy);

        // Act
        var allTemplates = await _service.GetAllTemplates();

        // Assert
        Assert.IsTrue(allTemplates.Count >= 2);
        Assert.IsTrue(allTemplates.Any(t => t.Id == template1.Id));
        Assert.IsTrue(allTemplates.Any(t => t.Id == template2.Id));

        // Cleanup
        await _service.DeleteTemplate(template1.Id);
        await _service.DeleteTemplate(template2.Id);
    }

    [TestMethod]
    public async Task CreateAndGetBatch_Success()
    {
        // Arrange
        var templateName = $"Test Template {Guid.NewGuid()}";
        var jsonPayload = "{\"type\":\"AdaptiveCard\",\"version\":\"1.3\"}";
        var template = await _service.CreateTemplate(templateName, jsonPayload, "test@example.com");

        var batchName = $"Test Batch {Guid.NewGuid()}";
        var senderUpn = "sender@example.com";

        // Act
        var createdBatch = await _service.CreateBatch(batchName, template.Id, senderUpn);
        var retrievedBatch = await _service.GetBatch(createdBatch.Id);

        // Assert
        Assert.IsNotNull(retrievedBatch);
        Assert.AreEqual(batchName, retrievedBatch.BatchName);
        Assert.AreEqual(template.Id, retrievedBatch.TemplateId);
        Assert.AreEqual(senderUpn, retrievedBatch.SenderUpn);

        // Cleanup
        await _service.DeleteBatch(createdBatch.Id);
        await _service.DeleteTemplate(template.Id);
    }

    [TestMethod]
    public async Task GetAllBatches_Success()
    {
        // Arrange
        var templateName = $"Test Template {Guid.NewGuid()}";
        var jsonPayload = "{\"type\":\"AdaptiveCard\",\"version\":\"1.3\"}";
        var template = await _service.CreateTemplate(templateName, jsonPayload, "test@example.com");

        var batchName1 = $"Test Batch 1 {Guid.NewGuid()}";
        var batchName2 = $"Test Batch 2 {Guid.NewGuid()}";
        var senderUpn = "sender@example.com";

        var batch1 = await _service.CreateBatch(batchName1, template.Id, senderUpn);
        var batch2 = await _service.CreateBatch(batchName2, template.Id, senderUpn);

        // Act
        var allBatches = await _service.GetAllBatches();

        // Assert
        Assert.IsTrue(allBatches.Count >= 2);
        Assert.IsTrue(allBatches.Any(b => b.Id == batch1.Id));
        Assert.IsTrue(allBatches.Any(b => b.Id == batch2.Id));

        // Cleanup
        await _service.DeleteBatch(batch1.Id);
        await _service.DeleteBatch(batch2.Id);
        await _service.DeleteTemplate(template.Id);
    }

    [TestMethod]
    public async Task LogMessageSend_Success()
    {
        // Arrange
        var templateName = $"Test Template {Guid.NewGuid()}";
        var jsonPayload = "{\"type\":\"AdaptiveCard\",\"version\":\"1.3\"}";
        var template = await _service.CreateTemplate(templateName, jsonPayload, "test@example.com");

        var batchName = $"Test Batch {Guid.NewGuid()}";
        var batch = await _service.CreateBatch(batchName, template.Id, "sender@example.com");

        var recipientUpn = "recipient@example.com";
        var status = "Pending";

        // Act
        var messageLog = await _service.LogMessageSend(batch.Id, recipientUpn, status);

        // Assert
        Assert.IsNotNull(messageLog);
        Assert.AreEqual(batch.Id, messageLog.MessageBatchId);
        Assert.AreEqual(recipientUpn, messageLog.RecipientUpn);
        Assert.AreEqual(status, messageLog.Status);

        // Cleanup
        await _service.DeleteBatch(batch.Id);
        await _service.DeleteTemplate(template.Id);
    }

    [TestMethod]
    public async Task LogBatchMessages_EnqueuesMessages()
    {
        // Arrange
        var templateName = $"Test Template {Guid.NewGuid()}";
        var jsonPayload = "{\"type\":\"AdaptiveCard\",\"version\":\"1.3\"}";
        var template = await _service.CreateTemplate(templateName, jsonPayload, "test@example.com");

        var batchName = $"Test Batch {Guid.NewGuid()}";
        var batch = await _service.CreateBatch(batchName, template.Id, "sender@example.com");

        var recipientUpns = new List<string>
        {
            "recipient1@example.com",
            "recipient2@example.com",
            "recipient3@example.com"
        };

        var initialQueueLength = await _queueService.GetQueueLengthAsync();

        // Act
        var messageLogs = await _service.LogBatchMessages(batch.Id, recipientUpns);

        // Wait a moment for queue operations to complete
        await Task.Delay(1000);
        var finalQueueLength = await _queueService.GetQueueLengthAsync();

        // Assert
        Assert.AreEqual(3, messageLogs.Count);
        Assert.IsTrue(finalQueueLength >= initialQueueLength + 3, 
            $"Expected queue length to increase by at least 3. Initial: {initialQueueLength}, Final: {finalQueueLength}");

        // Cleanup
        await _service.DeleteBatch(batch.Id);
        await _service.DeleteTemplate(template.Id);
    }

    [TestMethod]
    public async Task GetMessageLogsByBatch_Success()
    {
        // Arrange
        var templateName = $"Test Template {Guid.NewGuid()}";
        var jsonPayload = "{\"type\":\"AdaptiveCard\",\"version\":\"1.3\"}";
        var template = await _service.CreateTemplate(templateName, jsonPayload, "test@example.com");

        var batchName = $"Test Batch {Guid.NewGuid()}";
        var batch = await _service.CreateBatch(batchName, template.Id, "sender@example.com");

        var recipientUpns = new List<string>
        {
            "recipient1@example.com",
            "recipient2@example.com"
        };

        await _service.LogBatchMessages(batch.Id, recipientUpns);

        // Act
        var batchLogs = await _service.GetMessageLogsByBatch(batch.Id);

        // Assert
        Assert.AreEqual(2, batchLogs.Count);
        Assert.IsTrue(batchLogs.All(log => log.MessageBatchId == batch.Id));

        // Cleanup
        await _service.DeleteBatch(batch.Id);
        await _service.DeleteTemplate(template.Id);
    }

    [TestMethod]
    public async Task UpdateMessageLogStatus_Success()
    {
        // Arrange
        var templateName = $"Test Template {Guid.NewGuid()}";
        var jsonPayload = "{\"type\":\"AdaptiveCard\",\"version\":\"1.3\"}";
        var template = await _service.CreateTemplate(templateName, jsonPayload, "test@example.com");

        var batchName = $"Test Batch {Guid.NewGuid()}";
        var batch = await _service.CreateBatch(batchName, template.Id, "sender@example.com");

        var messageLog = await _service.LogMessageSend(batch.Id, "recipient@example.com", "Pending");

        // Act
        await _service.UpdateMessageLogStatus(messageLog.Id, "Sent", null);
        var logs = await _service.GetMessageLogsByBatch(batch.Id);
        var updatedLog = logs.FirstOrDefault(l => l.Id == messageLog.Id);

        // Assert
        Assert.IsNotNull(updatedLog);
        Assert.AreEqual("Sent", updatedLog.Status);

        // Cleanup
        await _service.DeleteBatch(batch.Id);
        await _service.DeleteTemplate(template.Id);
    }

    [TestMethod]
    public async Task DeleteTemplate_RemovesTemplate()
    {
        // Arrange
        var templateName = $"Test Template {Guid.NewGuid()}";
        var jsonPayload = "{\"type\":\"AdaptiveCard\",\"version\":\"1.3\"}";
        var template = await _service.CreateTemplate(templateName, jsonPayload, "test@example.com");

        // Act
        await _service.DeleteTemplate(template.Id);
        var retrievedTemplate = await _service.GetTemplate(template.Id);

        // Assert
        Assert.IsNull(retrievedTemplate);
    }

    [TestMethod]
    public async Task DeleteBatch_RemovesBatch()
    {
        // Arrange
        var templateName = $"Test Template {Guid.NewGuid()}";
        var jsonPayload = "{\"type\":\"AdaptiveCard\",\"version\":\"1.3\"}";
        var template = await _service.CreateTemplate(templateName, jsonPayload, "test@example.com");

        var batchName = $"Test Batch {Guid.NewGuid()}";
        var batch = await _service.CreateBatch(batchName, template.Id, "sender@example.com");

        // Act
        await _service.DeleteBatch(batch.Id);
        var retrievedBatch = await _service.GetBatch(batch.Id);

        // Assert
        Assert.IsNull(retrievedBatch);

        // Cleanup
        await _service.DeleteTemplate(template.Id);
    }
}
