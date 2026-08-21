using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Engine.Config;
using Engine.Storage;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Engine.Services;

/// <summary>
/// Service for managing Azure Storage Queue operations for batch message processing
/// </summary>
public class BatchQueueService
{
    private readonly QueueClient _queueClient;
    private readonly ILogger<BatchQueueService> _logger;
    private readonly string _queueName;
    private readonly QueueServiceClient? _queueServiceClient;
    private const string DEFAULT_QUEUE_NAME = "batch-messages";

    /// <summary>
    /// Legacy constructor using connection string authentication
    /// </summary>
    public BatchQueueService(string storageConnectionString, ILogger<BatchQueueService> logger, string? queueName = null)
    {
        _logger = logger;
        _queueName = queueName ?? DEFAULT_QUEUE_NAME;
        _queueClient = new QueueClient(storageConnectionString, _queueName);
    }

    /// <summary>
    /// Constructor supporting both connection string and RBAC authentication
    /// </summary>
    public BatchQueueService(StorageAuthConfig storageAuthConfig, ILogger<BatchQueueService> logger, string? queueName = null)
    {
        _logger = logger;
        _queueName = queueName ?? DEFAULT_QUEUE_NAME;

        _queueServiceClient = AzureStorageClientFactory.CreateQueueServiceClient(storageAuthConfig, logger);
        _queueClient = _queueServiceClient.GetQueueClient(_queueName);
    }

    /// <summary>
    /// Initialize the queue (create if not exists)
    /// </summary>
    public async Task InitializeAsync()
    {
        await _queueClient.CreateIfNotExistsAsync();
        _logger.LogInformation($"Queue '{_queueName}' initialized");
    }

    /// <summary>
    /// Enqueue a message for processing
    /// </summary>
    public async Task EnqueueMessageAsync(BatchQueueMessage message)
    {
        var json = JsonSerializer.Serialize(message);
        await _queueClient.SendMessageAsync(json);
        _logger.LogInformation($"Enqueued message for {message.RecipientUpn} in batch {message.BatchId}");
    }

    /// <summary>
    /// Enqueue multiple messages for batch processing. Sends are issued in parallel (bounded)
    /// to reduce wall-clock time on large batches.
    /// </summary>
    public async Task EnqueueBatchMessagesAsync(List<BatchQueueMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        if (messages.Count == 0) return;

        const int maxParallelism = 16;
        using var throttler = new SemaphoreSlim(maxParallelism);

        var tasks = messages.Select(async message =>
        {
            await throttler.WaitAsync();
            try
            {
                var json = JsonSerializer.Serialize(message);
                await _queueClient.SendMessageAsync(json);
            }
            finally
            {
                throttler.Release();
            }
        });

        await Task.WhenAll(tasks);
        _logger.LogInformation($"Enqueued {messages.Count} messages for processing");
    }

    /// <summary>
    /// Dequeue a single message for processing.
    /// </summary>
    public async Task<(BatchQueueMessage? Message, QueueMessage? QueueMessage)> DequeueMessageAsync()
    {
        var batch = await DequeueMessagesAsync(maxMessages: 1);
        return batch.Count == 0 ? (null, null) : batch[0];
    }

    /// <summary>
    /// Dequeue up to <paramref name="maxMessages"/> messages from the queue. Azure Storage Queue
    /// allows up to 32 messages per receive call; values outside [1, 32] are clamped.
    /// A visibility timeout is set explicitly so slow per-message processing (Graph + Bot Framework)
    /// won't redeliver messages mid-flight. Messages that fail JSON deserialization or contain a
    /// null payload are treated as poison and deleted after logging.
    /// </summary>
    public async Task<List<(BatchQueueMessage Message, QueueMessage QueueMessage)>> DequeueMessagesAsync(int maxMessages = 32)
    {
        if (maxMessages < 1) maxMessages = 1;
        if (maxMessages > 32) maxMessages = 32;

        // 5 minutes gives the processor plenty of time to send the message via Graph/Bot Framework
        // even under throttling. Messages still redeliver on hard failure because we only call
        // DeleteMessageAsync on success.
        var visibilityTimeout = TimeSpan.FromMinutes(5);

        QueueMessage[] messages = await _queueClient.ReceiveMessagesAsync(maxMessages: maxMessages, visibilityTimeout: visibilityTimeout);
        var result = new List<(BatchQueueMessage, QueueMessage)>(messages.Length);

        foreach (var queueMessage in messages)
        {
            BatchQueueMessage? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<BatchQueueMessage>(queueMessage.MessageText);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize queue message {MessageId}; deleting poison message", queueMessage.MessageId);
                await DeleteMessageAsync(queueMessage);
                continue;
            }

            if (parsed == null)
            {
                _logger.LogWarning("Queue message {MessageId} deserialized to null payload; deleting poison message", queueMessage.MessageId);
                await DeleteMessageAsync(queueMessage);
                continue;
            }

            result.Add((parsed, queueMessage));
        }

        return result;
    }

    /// <summary>
    /// Delete a message from the queue after successful processing
    /// </summary>
    public async Task DeleteMessageAsync(QueueMessage queueMessage)
    {
        await _queueClient.DeleteMessageAsync(queueMessage.MessageId, queueMessage.PopReceipt);
    }

    /// <summary>
    /// Get approximate queue length
    /// </summary>
    public async Task<int> GetQueueLengthAsync()
    {
        var properties = await _queueClient.GetPropertiesAsync();
        var count = properties.Value.ApproximateMessagesCount;

        _logger.LogDebug("Queue '{QueueName}' has approximately {MessageCount} messages", _queueName, count);

        return count;
    }

    /// <summary>
    /// Delete the entire queue. Used primarily for test cleanup.
    /// </summary>
    public async Task DeleteQueueAsync()
    {
        await _queueClient.DeleteIfExistsAsync();
        _logger.LogInformation($"Queue '{_queueName}' deleted");
    }

    #region Control queue

    private const string CONTROL_QUEUE_NAME = "batch-control";
    private QueueClient? _controlQueueClient;

    private QueueClient ControlQueue =>
        _controlQueueClient ??= _queueServiceClient is not null
            ? _queueServiceClient.GetQueueClient(CONTROL_QUEUE_NAME)
            : throw new InvalidOperationException(
                "Control queue requires the StorageAuthConfig constructor.");

    /// <summary>
    /// Enqueue a batch-expansion instruction. Kept on a separate queue so a long-running
    /// expansion can't be starved behind 150,000 delivery messages.
    /// </summary>
    public async Task EnqueueControlMessageAsync(BatchControlMessage message)
    {
        await ControlQueue.CreateIfNotExistsAsync();
        await ControlQueue.SendMessageAsync(JsonSerializer.Serialize(message));
        _logger.LogInformation("Enqueued expansion for batch {BatchId}", message.BatchId);
    }

    /// <summary>
    /// Dequeue a batch-expansion instruction. The visibility timeout is generous because
    /// expanding a large batch legitimately takes minutes.
    /// </summary>
    public async Task<(BatchControlMessage? Message, QueueMessage? QueueMessage)> DequeueControlMessageAsync()
    {
        await ControlQueue.CreateIfNotExistsAsync();

        var messages = await ControlQueue.ReceiveMessagesAsync(
            maxMessages: 1, visibilityTimeout: TimeSpan.FromMinutes(30));

        var queueMessage = messages.Value.FirstOrDefault();
        if (queueMessage == null) return (null, null);

        try
        {
            var parsed = JsonSerializer.Deserialize<BatchControlMessage>(queueMessage.MessageText);
            if (parsed != null) return (parsed, queueMessage);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Poison control message {MessageId}; deleting", queueMessage.MessageId);
        }

        await ControlQueue.DeleteMessageAsync(queueMessage.MessageId, queueMessage.PopReceipt);
        return (null, null);
    }

    /// <summary>
    /// Remove a control message once its expansion has completed.
    /// </summary>
    public async Task DeleteControlMessageAsync(QueueMessage queueMessage)
    {
        await ControlQueue.DeleteMessageAsync(queueMessage.MessageId, queueMessage.PopReceipt);
    }

    #endregion
}
