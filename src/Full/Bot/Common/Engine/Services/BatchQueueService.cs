using Azure.Identity;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Common.Engine.Config;
using Common.Engine.Storage;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Common.Engine.Services;

/// <summary>
/// Service for managing Azure Storage Queue operations for batch message processing
/// </summary>
public class BatchQueueService
{
    private readonly QueueClient _queueClient;
    private readonly ILogger<BatchQueueService> _logger;
    private const string QUEUE_NAME = "batch-messages";

    /// <summary>
    /// Legacy constructor using connection string authentication
    /// </summary>
    public BatchQueueService(string storageConnectionString, ILogger<BatchQueueService> logger)
    {
        _logger = logger;
        _queueClient = new QueueClient(storageConnectionString, QUEUE_NAME);
    }

    /// <summary>
    /// Constructor supporting both connection string and RBAC authentication
    /// </summary>
    public BatchQueueService(StorageAuthConfig storageAuthConfig, ILogger<BatchQueueService> logger)
    {
        _logger = logger;

        if (storageAuthConfig.UseRBAC)
        {
            if (string.IsNullOrEmpty(storageAuthConfig.StorageAccountName))
            {
                throw new InvalidOperationException("StorageAccountName is required when UseRBAC is true");
            }

            var queueServiceEndpoint = new Uri($"https://{storageAuthConfig.StorageAccountName}.queue.core.windows.net");

            // Use override credentials if provided, otherwise use DefaultAzureCredential
            if (storageAuthConfig.RBACOverrideCredentials != null)
            {
                var credential = new ClientSecretCredential(
                    storageAuthConfig.RBACOverrideCredentials.TenantId,
                    storageAuthConfig.RBACOverrideCredentials.ClientId,
                    storageAuthConfig.RBACOverrideCredentials.ClientSecret);
                var queueServiceClient = new QueueServiceClient(queueServiceEndpoint, credential);
                _queueClient = queueServiceClient.GetQueueClient(QUEUE_NAME);
            }
            else
            {
                // Use DefaultAzureCredential (Managed Identity, Azure CLI, etc.)
                var queueServiceClient = new QueueServiceClient(queueServiceEndpoint, new DefaultAzureCredential());
                _queueClient = queueServiceClient.GetQueueClient(QUEUE_NAME);
            }
        }
        else
        {
            if (string.IsNullOrEmpty(storageAuthConfig.ConnectionString))
            {
                throw new InvalidOperationException("ConnectionString is required when UseRBAC is false");
            }
            _queueClient = new QueueClient(storageAuthConfig.ConnectionString, QUEUE_NAME);
        }
    }

    /// <summary>
    /// Initialize the queue (create if not exists)
    /// </summary>
    public async Task InitializeAsync()
    {
        await _queueClient.CreateIfNotExistsAsync();
        _logger.LogInformation($"Queue '{QUEUE_NAME}' initialized");
    }

    /// <summary>
    /// Enqueue a message for processing
    /// </summary>
    public async Task EnqueueMessageAsync(BatchQueueMessage message)
    {
        var json = JsonSerializer.Serialize(message);
        await _queueClient.SendMessageAsync(json);
        _logger.LogInformation($"Enqueued message for log {message.MessageLogId}");
    }

    /// <summary>
    /// Enqueue multiple messages for batch processing
    /// </summary>
    public async Task EnqueueBatchMessagesAsync(List<BatchQueueMessage> messages)
    {
        foreach (var message in messages)
        {
            await EnqueueMessageAsync(message);
        }
        _logger.LogInformation($"Enqueued {messages.Count} messages for processing");
    }

    /// <summary>
    /// Dequeue a message for processing
    /// </summary>
    public async Task<(BatchQueueMessage? Message, QueueMessage? QueueMessage)> DequeueMessageAsync()
    {
        QueueMessage[] messages = await _queueClient.ReceiveMessagesAsync(maxMessages: 1);
        
        if (messages.Length == 0)
        {
            return (null, null);
        }

        var queueMessage = messages[0];
        var batchMessage = JsonSerializer.Deserialize<BatchQueueMessage>(queueMessage.MessageText);
        
        return (batchMessage, queueMessage);
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
        return properties.Value.ApproximateMessagesCount;
    }
}
