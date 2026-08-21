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

    /// <summary>
    /// Process-wide send rate limit.
    ///
    /// <para>
    /// Teams throttles proactive messaging at roughly 1,800 operations per bot per tenant per
    /// 30 seconds. Shaping to 1,500/30s leaves headroom for the reply path and the app-install
    /// calls, which draw on the same budget. Without this the dispatcher saturated the limit
    /// within seconds of a send starting and took sustained 429s for the rest of the run.
    /// </para>
    /// </summary>
    private static readonly TokenBucketRateLimiter SendLimiter =
        new(permits: 1_500, perPeriod: TimeSpan.FromSeconds(30));

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

                // Tally outcomes per batch and flush once per dequeue cycle. Azure Tables has
                // no atomic increment, so writing a counter per delivery would mean an
                // ETag compare-and-swap per message on a single row - contention plus two
                // extra round trips each. Aggregating first makes it one write per ~32 sends.
                var tally = new System.Collections.Concurrent.ConcurrentDictionary<string, int[]>();

                var tasks = batch.Select(async pair =>
                {
                    var (message, queueMessage) = pair;
                    await throttler.WaitAsync(stoppingToken);
                    try
                    {
                        // Honour batch lifecycle before doing any work: a cancelled or paused
                        // batch must not keep sending. This is what makes 150,000 queued
                        // messages recallable.
                        var gate = await _senderService.GetBatchGateAsync(message.BatchId);
                        if (gate == BatchGate.Drop)
                        {
                            _logger.LogDebug("Dropping delivery for {Recipient}: batch {BatchId} is cancelled",
                                message.RecipientUpn, message.BatchId);
                            await _queueService.DeleteMessageAsync(queueMessage);
                            return;
                        }
                        if (gate == BatchGate.Defer)
                        {
                            // Paused or not yet due - leave queued and let it redeliver.
                            return;
                        }

                        // Shape the send rate to stay under the Teams proactive-messaging limit.
                        await SendLimiter.WaitAsync(stoppingToken);

                        var result = await _senderService.SendMessageAsync(message);

                        if (result.Disposition == SendDisposition.TransientFailure && result.RetryAfter.HasValue)
                        {
                            // Slow the whole process down, not just this caller.
                            SendLimiter.Penalise(result.RetryAfter.Value);
                        }

                        var counts = tally.GetOrAdd(message.BatchId, _ => new int[2]);
                        if (result.Disposition == SendDisposition.Delivered)
                        {
                            Interlocked.Increment(ref counts[0]);
                        }
                        else if (result.Disposition == SendDisposition.PermanentFailure)
                        {
                            Interlocked.Increment(ref counts[1]);
                        }

                        switch (result.Disposition)
                        {
                            case SendDisposition.Delivered:
                            case SendDisposition.AwaitingInstall:
                            case SendDisposition.PermanentFailure:
                                // Terminal outcome: the delivery row records what happened, so
                                // the queue message can be removed.
                                await _queueService.DeleteMessageAsync(queueMessage);
                                break;

                            case SendDisposition.TransientFailure:
                                // Deliberately NOT deleted. Previously every failure - including
                                // throttling - deleted the message, permanently dropping that
                                // user's nudge with no retry. Letting the visibility timeout
                                // expire redelivers it; the send is idempotent on the delivery key.
                                if (queueMessage.DequeueCount >= MaxDequeueCount)
                                {
                                    _logger.LogError(
                                        "Delivery to {Recipient} in batch {BatchId} exceeded MaxDequeueCount ({MaxDequeueCount}); recording as failed",
                                        message.RecipientUpn, message.BatchId, MaxDequeueCount);

                                    await _senderService.RecordExhaustedAsync(message, result.ErrorMessage);
                                    await _queueService.DeleteMessageAsync(queueMessage);
                                }
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing delivery to {Recipient} in batch {BatchId} (dequeue count {DequeueCount})",
                            message.RecipientUpn, message.BatchId, queueMessage.DequeueCount);

                        // Poison-message guard: after MaxDequeueCount unhandled failures, drop the
                        // message so it doesn't redeliver forever and block the queue.
                        if (queueMessage.DequeueCount >= MaxDequeueCount)
                        {
                            _logger.LogError("Delivery to {Recipient} exceeded MaxDequeueCount ({MaxDequeueCount}); deleting as poison",
                                message.RecipientUpn, MaxDequeueCount);
                            try
                            {
                                await _senderService.RecordExhaustedAsync(message, ex.Message);
                                await _queueService.DeleteMessageAsync(queueMessage);
                            }
                            catch (Exception delEx)
                            {
                                _logger.LogError(delEx, "Failed to delete poison message for {Recipient}", message.RecipientUpn);
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

                // Flush the aggregated counters so dashboards can read progress without
                // touching delivery rows.
                foreach (var (batchId, counts) in tally)
                {
                    if (counts[0] == 0 && counts[1] == 0) continue;
                    try
                    {
                        await _senderService.FlushBatchCountersAsync(batchId, counts[0], counts[1]);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to flush counters for batch {BatchId}", batchId);
                    }
                }
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
