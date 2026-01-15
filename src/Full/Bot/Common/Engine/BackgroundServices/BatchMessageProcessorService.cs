using Common.Engine.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Common.Engine.BackgroundServices;

/// <summary>
/// Background service that processes queued batch messages
/// </summary>
public class BatchMessageProcessorService : BackgroundService
{
    private readonly BatchQueueService _queueService;
    private readonly MessageSenderService _senderService;
    private readonly ILogger<BatchMessageProcessorService> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var (message, queueMessage) = await _queueService.DequeueMessageAsync();

                if (message != null && queueMessage != null)
                {
                    _logger.LogInformation($"Processing message for recipient {message.RecipientUpn}");

                    // Send the message
                    var result = await _senderService.SendMessageAsync(message);

                    if (result.Success)
                    {
                        _logger.LogInformation($"Successfully processed message {message.MessageLogId}");
                    }
                    else
                    {
                        _logger.LogWarning($"Failed to process message {message.MessageLogId}: {result.ErrorMessage}");
                    }

                    // Delete the message from the queue
                    await _queueService.DeleteMessageAsync(queueMessage);
                }
                else
                {
                    // No messages in queue, wait before polling again
                    await Task.Delay(_pollInterval, stoppingToken);
                }
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
