using Engine.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Engine.BackgroundServices;

/// <summary>
/// Background service that processes queued batch messages
/// </summary>
public class BatchMessageProcessorService : BackgroundService
{
    private readonly BatchQueueService _queueService;
    private readonly MessageSenderService _senderService;
    private readonly ILogger<BatchMessageProcessorService> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

    // Drop messages that keep failing after this many unhandled redeliveries so a poison
    // message can't block the queue forever.
    private const int MaxDequeueCount = 5;

    public BatchMessageProcessorService(
        BatchQueueService queueService,
        MessageSenderService senderService,
        ILogger<BatchMessageProcessorService> logger)
    {
        _queueService = queueService;
        _senderService = senderService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Batch Message Processor Service is starting");

        // Initialize the queue
        await _queueService.InitializeAsync();

        // Bound parallel processing so we don't overwhelm Graph / Bot Framework throttling limits.
        const int maxParallelism = 8;
        using var throttler = new SemaphoreSlim(maxParallelism);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var batch = await _queueService.DequeueMessagesAsync(maxMessages: 32);

                if (batch.Count == 0)
                {
                    // No messages in queue, wait before polling again
                    await Task.Delay(_pollInterval, stoppingToken);
                    continue;
                }

                var tasks = batch.Select(async pair =>
                {
                    var (message, queueMessage) = pair;
                    await throttler.WaitAsync(stoppingToken);
                    try
                    {
                        _logger.LogInformation("Processing message for recipient {Recipient}", message.RecipientUpn);

                        var result = await _senderService.SendMessageAsync(message);
                        if (result.Success)
                        {
                            _logger.LogInformation("Successfully processed message {LogId}", message.MessageLogId);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to process message {LogId}: {Error}", message.MessageLogId, result.ErrorMessage);
                        }

                        // SendMessageAsync persists the outcome (Success/Failed) to the message log,
                        // so we always remove the queue message after it returns - whether the send
                        // succeeded or recorded a permanent failure. We only rely on redelivery for
                        // *unhandled* exceptions below.
                        await _queueService.DeleteMessageAsync(queueMessage);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing queued message {LogId} (dequeue count {DequeueCount})",
                            message.MessageLogId, queueMessage.DequeueCount);

                        // Poison-message guard: after MaxDequeueCount unhandled failures, drop the
                        // message so it doesn't redeliver forever and block the queue.
                        if (queueMessage.DequeueCount >= MaxDequeueCount)
                        {
                            _logger.LogError("Message {LogId} exceeded MaxDequeueCount ({MaxDequeueCount}); deleting as poison",
                                message.MessageLogId, MaxDequeueCount);
                            try
                            {
                                await _queueService.DeleteMessageAsync(queueMessage);
                            }
                            catch (Exception delEx)
                            {
                                _logger.LogError(delEx, "Failed to delete poison message {LogId}", message.MessageLogId);
                            }
                        }
                        // Otherwise let Azure Storage Queue redeliver after visibility timeout.
                    }
                    finally
                    {
                        throttler.Release();
                    }
                });

                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown.
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing batch message");
                await Task.Delay(_pollInterval, stoppingToken);
            }
        }

        _logger.LogInformation("Batch Message Processor Service is stopping");
    }
}
