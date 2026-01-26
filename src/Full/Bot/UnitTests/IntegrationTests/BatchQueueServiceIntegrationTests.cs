using Common.Engine.Services;
using Common.Engine.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests.IntegrationTests;

[TestClass]
public class BatchQueueServiceIntegrationTests : AbstractTest
{
    private BatchQueueService _service = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        _service = new BatchQueueService(
            GetStorageAuthConfig(),
            GetLogger<BatchQueueService>()
        );
        await _service.InitializeAsync();
    }

    [TestMethod]
    public async Task EnqueueAndDequeueMessage_Success()
    {
        // Arrange
        var message = new BatchQueueMessage
        {
            BatchId = Guid.NewGuid().ToString(),
            MessageLogId = Guid.NewGuid().ToString(),
            RecipientUpn = "test@example.com",
            TemplateId = Guid.NewGuid().ToString()
        };

        // Act
        await _service.EnqueueMessageAsync(message);
        
        // Wait a moment for the message to be available
        await Task.Delay(500);
        
        var (dequeuedMessage, queueMessage) = await _service.DequeueMessageAsync();

        // Assert
        Assert.IsNotNull(dequeuedMessage);
        Assert.IsNotNull(queueMessage);
        Assert.AreEqual(message.BatchId, dequeuedMessage.BatchId);
        Assert.AreEqual(message.MessageLogId, dequeuedMessage.MessageLogId);
        Assert.AreEqual(message.RecipientUpn, dequeuedMessage.RecipientUpn);
        Assert.AreEqual(message.TemplateId, dequeuedMessage.TemplateId);

        // Cleanup
        await _service.DeleteMessageAsync(queueMessage);
    }

    [TestMethod]
    public async Task EnqueueBatchMessages_Success()
    {
        // Arrange
        var messages = new List<BatchQueueMessage>
        {
            new BatchQueueMessage
            {
                BatchId = Guid.NewGuid().ToString(),
                MessageLogId = Guid.NewGuid().ToString(),
                RecipientUpn = "test1@example.com",
                TemplateId = Guid.NewGuid().ToString()
            },
            new BatchQueueMessage
            {
                BatchId = Guid.NewGuid().ToString(),
                MessageLogId = Guid.NewGuid().ToString(),
                RecipientUpn = "test2@example.com",
                TemplateId = Guid.NewGuid().ToString()
            },
            new BatchQueueMessage
            {
                BatchId = Guid.NewGuid().ToString(),
                MessageLogId = Guid.NewGuid().ToString(),
                RecipientUpn = "test3@example.com",
                TemplateId = Guid.NewGuid().ToString()
            }
        };

        var initialLength = await _service.GetQueueLengthAsync();

        // Act
        await _service.EnqueueBatchMessagesAsync(messages);
        
        // Wait for messages to be available
        await Task.Delay(1000);
        
        var finalLength = await _service.GetQueueLengthAsync();

        // Assert
        Assert.IsTrue(finalLength >= initialLength + 3,
            $"Expected at least 3 more messages. Initial: {initialLength}, Final: {finalLength}");

        // Cleanup - dequeue the messages we added
        for (int i = 0; i < 3; i++)
        {
            var (_, queueMessage) = await _service.DequeueMessageAsync();
            if (queueMessage != null)
            {
                await _service.DeleteMessageAsync(queueMessage);
            }
        }
    }

    [TestMethod]
    public async Task GetQueueLength_ReturnsCorrectCount()
    {
        // Arrange
        var initialLength = await _service.GetQueueLengthAsync();

        var message = new BatchQueueMessage
        {
            BatchId = Guid.NewGuid().ToString(),
            MessageLogId = Guid.NewGuid().ToString(),
            RecipientUpn = "test@example.com",
            TemplateId = Guid.NewGuid().ToString()
        };

        // Act
        await _service.EnqueueMessageAsync(message);
        await Task.Delay(500);
        var lengthAfterEnqueue = await _service.GetQueueLengthAsync();

        var (_, queueMessage) = await _service.DequeueMessageAsync();
        Assert.IsNotNull(queueMessage);
        await _service.DeleteMessageAsync(queueMessage);
        
        await Task.Delay(500);
        var lengthAfterDequeue = await _service.GetQueueLengthAsync();

        // Assert
        Assert.IsTrue(lengthAfterEnqueue > initialLength, 
            $"Queue length should increase after enqueue. Initial: {initialLength}, After: {lengthAfterEnqueue}");
        Assert.IsTrue(lengthAfterDequeue < lengthAfterEnqueue, 
            $"Queue length should decrease after dequeue. Before: {lengthAfterEnqueue}, After: {lengthAfterDequeue}");
    }

    [TestMethod]
    public async Task DequeueMessage_EmptyQueue_ReturnsNull()
    {
        // Arrange - Clear the queue first
        while (true)
        {
            var (_, queueMessage) = await _service.DequeueMessageAsync();
            if (queueMessage == null) break;
            await _service.DeleteMessageAsync(queueMessage);
        }

        // Act
        var (dequeuedMessage, retrievedQueueMessage) = await _service.DequeueMessageAsync();

        // Assert
        Assert.IsNull(dequeuedMessage);
        Assert.IsNull(retrievedQueueMessage);
    }

    [TestMethod]
    public async Task DeleteMessage_RemovesFromQueue()
    {
        // Arrange
        var message = new BatchQueueMessage
        {
            BatchId = Guid.NewGuid().ToString(),
            MessageLogId = Guid.NewGuid().ToString(),
            RecipientUpn = "test@example.com",
            TemplateId = Guid.NewGuid().ToString()
        };

        await _service.EnqueueMessageAsync(message);
        await Task.Delay(500);
        
        var initialLength = await _service.GetQueueLengthAsync();
        var (_, queueMessage) = await _service.DequeueMessageAsync();
        Assert.IsNotNull(queueMessage);

        // Act
        await _service.DeleteMessageAsync(queueMessage);
        await Task.Delay(500);
        
        var finalLength = await _service.GetQueueLengthAsync();

        // Assert
        Assert.IsTrue(finalLength < initialLength,
            $"Queue length should decrease after delete. Before: {initialLength}, After: {finalLength}");
    }
}
