using Engine.Services;
using Engine.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Engine.BackgroundServices;

/// <summary>
/// Expands a batch's recipient sources into delivery rows and enqueues them.
///
/// <para>
/// This work used to happen inline in the HTTP request that created the batch. At 150,000
/// recipients that was ~250-310 seconds against App Service's hard 230-second request timeout,
/// and a timeout left a half-created batch with no record of which recipients had already been
/// enqueued - so retrying double-sent to everyone already queued.
/// </para>
///
/// <para>
/// Expansion now runs here in checkpointed chunks, recording progress on the batch row so an
/// interrupted run resumes rather than restarting. That matters because the worker is unloaded
/// whenever it goes idle, which makes interruption the norm rather than the exception.
/// </para>
/// </summary>
public class BatchExpansionService : BackgroundService
{
    private readonly BatchQueueService _queueService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BatchExpansionService> _logger;

    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Recipients written and enqueued per checkpoint. Small enough that an interruption loses
    /// little work; large enough that checkpointing isn't itself the bottleneck.
    /// </summary>
    private const int ChunkSize = 5_000;

    public BatchExpansionService(
        BatchQueueService queueService,
        IServiceScopeFactory scopeFactory,
        ILogger<BatchExpansionService> logger)
    {
        _queueService = queueService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Batch expansion service is starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var (message, queueMessage) = await _queueService.DequeueControlMessageAsync();

                if (message == null || queueMessage == null)
                {
                    await Task.Delay(_pollInterval, stoppingToken);
                    continue;
                }

                await ExpandAsync(message, stoppingToken);
                await _queueService.DeleteControlMessageAsync(queueMessage);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Leave the control message queued so expansion resumes after redelivery.
                _logger.LogError(ex, "Error expanding batch; will retry after visibility timeout");
                await Task.Delay(_pollInterval, stoppingToken);
            }
        }

        _logger.LogInformation("Batch expansion service is stopping");
    }

    private async Task ExpandAsync(BatchControlMessage message, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var templateService = scope.ServiceProvider.GetRequiredService<MessageTemplateService>();
        var smartGroupService = scope.ServiceProvider.GetRequiredService<SmartGroupService>();
        var storage = scope.ServiceProvider.GetRequiredService<MessageTemplateStorageManager>();

        var batch = await storage.GetBatch(message.BatchId);
        if (batch == null)
        {
            _logger.LogWarning("Batch {BatchId} no longer exists; dropping expansion", message.BatchId);
            return;
        }

        if (string.Equals(batch.Status, BatchStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Batch {BatchId} was cancelled before expansion; dropping", message.BatchId);
            return;
        }

        await storage.SetBatchExpansionProgressAsync(message.BatchId, batch.ExpandedCount, BatchStatus.Expanding);

        // Resolve recipients from both sources, de-duplicated case-insensitively.
        var recipients = new HashSet<string>(message.RecipientUpns, StringComparer.OrdinalIgnoreCase);

        foreach (var smartGroupId in message.SmartGroupIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var upns = await smartGroupService.GetSmartGroupUpns(smartGroupId);
                foreach (var upn in upns) recipients.Add(upn);
                _logger.LogInformation("Smart group {GroupId} contributed {Count} recipients to batch {BatchId}",
                    smartGroupId, upns.Count, message.BatchId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve smart group {GroupId} for batch {BatchId}",
                    smartGroupId, message.BatchId);
            }
        }

        var ordered = recipients.OrderBy(u => u, StringComparer.Ordinal).ToList();

        if (ordered.Count == 0)
        {
            _logger.LogWarning("Batch {BatchId} expanded to zero recipients", message.BatchId);
            await storage.SetBatchStatusAsync(message.BatchId, BatchStatus.Complete);
            return;
        }

        // Resume from the last checkpoint. Ordering is deterministic, so a resumed run skips
        // exactly the recipients already written - and because delivery rows use the recipient
        // as their natural key, re-processing a chunk is an upsert rather than a duplicate.
        var startIndex = Math.Min(batch.ExpandedCount, ordered.Count);
        if (startIndex > 0)
        {
            _logger.LogInformation("Resuming expansion of batch {BatchId} from recipient {Index} of {Total}",
                message.BatchId, startIndex, ordered.Count);
        }

        for (var i = startIndex; i < ordered.Count; i += ChunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chunk = ordered.Skip(i).Take(ChunkSize).ToList();
            await templateService.LogBatchMessages(message.BatchId, chunk);

            var processed = Math.Min(i + ChunkSize, ordered.Count);
            await storage.SetBatchExpansionProgressAsync(message.BatchId, processed, BatchStatus.Expanding);

            _logger.LogInformation("Batch {BatchId}: expanded {Processed}/{Total} recipients",
                message.BatchId, processed, ordered.Count);
        }

        await storage.SetBatchTotalCountAsync(message.BatchId, ordered.Count);
        await storage.SetBatchStatusAsync(message.BatchId, BatchStatus.Running);

        _logger.LogInformation("Batch {BatchId} expansion complete: {Total} recipients", message.BatchId, ordered.Count);
    }
}
