using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Engine.Config;
using Engine.Services;
using Engine.Storage;
using Microsoft.Extensions.Logging;

namespace Engine;

/// <summary>
/// Manages message templates in Azure Storage (Table + Blob)
/// </summary>
public class MessageTemplateStorageManager : TableStorageManager, IBatchStatsSource
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger _logger;
    private const string TEMPLATES_TABLE_NAME = "messagetemplates";
    private const string BATCHES_TABLE_NAME = "messagebatches";
    private const string LOGS_TABLE_NAME = "messagelogs";
    private const string PENDING_TABLE_NAME = "pendingdeliveries";
    private const string BLOB_CONTAINER_NAME = "message-templates";

    public MessageTemplateStorageManager(StorageAuthConfig storageAuthConfig, ILogger logger)
        : base(storageAuthConfig, logger)
    {
        _logger = logger;
        _blobServiceClient = AzureStorageClientFactory.CreateBlobServiceClient(storageAuthConfig, logger);
    }

    #region Template Management

    /// <summary>
    /// Save a message template to blob storage and create table entry
    /// </summary>
    public async Task<MessageTemplateTableEntity> SaveTemplate(string templateName, string jsonPayload, string createdByUpn)
    {
        var templateId = Guid.NewGuid().ToString();

        // Save JSON to blob storage
        var blobUrl = await SaveTemplateToBlobStorage(templateId, jsonPayload);

        // Create table entry with blob reference
        var tableEntity = new MessageTemplateTableEntity
        {
            RowKey = templateId,
            TemplateName = templateName,
            BlobUrl = blobUrl,
            CreatedByUpn = createdByUpn,
            CreatedDate = DateTime.UtcNow
        };

        var tableClient = await GetTableClient(TEMPLATES_TABLE_NAME);
        await tableClient.AddEntityAsync(tableEntity);

        _logger.LogInformation($"Saved template '{templateName}' with ID {templateId}");
        return tableEntity;
    }

    /// <summary>
    /// Get all message templates
    /// </summary>
    public async Task<List<MessageTemplateTableEntity>> GetAllTemplates()
    {
        var tableClient = await GetTableClient(TEMPLATES_TABLE_NAME);
        var templates = new List<MessageTemplateTableEntity>();

        await foreach (var entity in tableClient.QueryAsync<MessageTemplateTableEntity>(
            filter: $"PartitionKey eq '{MessageTemplateTableEntity.PartitionKeyVal}'"))
        {
            templates.Add(entity);
        }

        return templates;
    }

    /// <summary>
    /// Get a specific template by ID
    /// </summary>
    public async Task<MessageTemplateTableEntity?> GetTemplate(string templateId)
    {
        var tableClient = await GetTableClient(TEMPLATES_TABLE_NAME);
        try
        {
            var response = await tableClient.GetEntityAsync<MessageTemplateTableEntity>(
                MessageTemplateTableEntity.PartitionKeyVal, templateId);
            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    /// <summary>
    /// Get the JSON content from blob storage
    /// </summary>
    public async Task<string> GetTemplateJson(string templateId)
    {
        var template = await GetTemplate(templateId);
        if (template == null)
        {
            throw new InvalidOperationException($"Template {templateId} not found");
        }

        var containerClient = _blobServiceClient.GetBlobContainerClient(BLOB_CONTAINER_NAME);
        var blobName = $"{templateId}.json";
        var blobClient = containerClient.GetBlobClient(blobName);

        var response = await blobClient.DownloadContentAsync();
        return response.Value.Content.ToString();
    }

    /// <summary>
    /// Update a template
    /// </summary>
    public async Task<MessageTemplateTableEntity> UpdateTemplate(string templateId, string templateName, string jsonPayload)
    {
        var template = await GetTemplate(templateId);
        if (template == null)
        {
            throw new InvalidOperationException($"Template {templateId} not found");
        }

        // Update blob content
        var blobUrl = await SaveTemplateToBlobStorage(templateId, jsonPayload);

        // Update table entry
        template.TemplateName = templateName;
        template.BlobUrl = blobUrl;

        var tableClient = await GetTableClient(TEMPLATES_TABLE_NAME);
        await tableClient.UpdateEntityAsync(template, template.ETag, TableUpdateMode.Replace);

        _logger.LogInformation($"Updated template {templateId}");
        return template;
    }

    /// <summary>
    /// Delete a template
    /// </summary>
    public async Task DeleteTemplate(string templateId)
    {
        var template = await GetTemplate(templateId);
        if (template == null)
        {
            throw new InvalidOperationException($"Template {templateId} not found");
        }

        // Delete blob
        var containerClient = _blobServiceClient.GetBlobContainerClient(BLOB_CONTAINER_NAME);
        var blobName = $"{templateId}.json";
        var blobClient = containerClient.GetBlobClient(blobName);
        await blobClient.DeleteIfExistsAsync();

        // Delete table entry
        var tableClient = await GetTableClient(TEMPLATES_TABLE_NAME);
        await tableClient.DeleteEntityAsync(MessageTemplateTableEntity.PartitionKeyVal, templateId);

        _logger.LogInformation($"Deleted template {templateId}");
    }

    private async Task<string> SaveTemplateToBlobStorage(string templateId, string jsonPayload)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(BLOB_CONTAINER_NAME);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

        var blobName = $"{templateId}.json";
        var blobClient = containerClient.GetBlobClient(blobName);

        // Explicitly use UTF-8 encoding to preserve emojis and special characters
        var utf8Bytes = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
        var content = new BinaryData(utf8Bytes);

        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = "application/json; charset=utf-8"
            }
        };

        await blobClient.UploadAsync(content, overwrite: true);

        return blobClient.Uri.ToString();
    }

    #endregion

    #region Batch Management

    /// <summary>
    /// Create a new message batch
    /// </summary>
    public async Task<MessageBatchTableEntity> CreateBatch(string batchName, string templateId, string senderUpn, DateTime? scheduledSendUtc = null)
    {
        var batchId = Guid.NewGuid().ToString();

        var batchEntity = new MessageBatchTableEntity
        {
            RowKey = batchId,
            BatchName = batchName,
            TemplateId = templateId,
            SenderUpn = senderUpn,
            CreatedDate = DateTime.UtcNow,
            Status = BatchStatus.Queued,
            ScheduledSendUtc = scheduledSendUtc
        };

        var tableClient = await GetTableClient(BATCHES_TABLE_NAME);
        await tableClient.AddEntityAsync(batchEntity);

        _logger.LogInformation($"Created batch '{batchName}' with ID {batchId}");
        return batchEntity;
    }

    /// <summary>
    /// Set a batch's lifecycle state via a sparse merge.
    /// </summary>
    public async Task SetBatchStatusAsync(string batchId, string status)
    {
        var tableClient = await GetTableClient(BATCHES_TABLE_NAME);
        var patch = new TableEntity(MessageBatchTableEntity.PartitionKeyVal, batchId)
        {
            { nameof(MessageBatchTableEntity.Status), status },
            { nameof(MessageBatchTableEntity.LastProgressUtc), DateTime.UtcNow }
        };

        await tableClient.UpdateEntityAsync(patch, ETag.All, TableUpdateMode.Merge);
        _logger.LogInformation("Batch {BatchId} status set to {Status}", batchId, status);
    }

    /// <summary>
    /// Record expansion progress so an interrupted run resumes instead of restarting. Necessary
    /// because the worker is unloaded whenever it goes idle.
    /// </summary>
    public async Task SetBatchExpansionProgressAsync(string batchId, int expandedCount, string status)
    {
        var tableClient = await GetTableClient(BATCHES_TABLE_NAME);
        var patch = new TableEntity(MessageBatchTableEntity.PartitionKeyVal, batchId)
        {
            { nameof(MessageBatchTableEntity.ExpandedCount), expandedCount },
            { nameof(MessageBatchTableEntity.Status), status },
            { nameof(MessageBatchTableEntity.LastProgressUtc), DateTime.UtcNow }
        };

        await tableClient.UpdateEntityAsync(patch, ETag.All, TableUpdateMode.Merge);
    }

    /// <summary>
    /// Get all message batches
    /// </summary>
    public async Task<List<MessageBatchTableEntity>> GetAllBatches()
    {
        var tableClient = await GetTableClient(BATCHES_TABLE_NAME);
        var batches = new List<MessageBatchTableEntity>();

        await foreach (var entity in tableClient.QueryAsync<MessageBatchTableEntity>(
            filter: $"PartitionKey eq '{MessageBatchTableEntity.PartitionKeyVal}'"))
        {
            batches.Add(entity);
        }

        return batches;
    }

    /// <summary>
    /// Get a specific batch by ID
    /// </summary>
    public async Task<MessageBatchTableEntity?> GetBatch(string batchId)
    {
        var tableClient = await GetTableClient(BATCHES_TABLE_NAME);
        try
        {
            var response = await tableClient.GetEntityAsync<MessageBatchTableEntity>(
                MessageBatchTableEntity.PartitionKeyVal, batchId);
            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    /// <summary>
    /// Delete a batch, all its delivery rows and any pending-delivery index entries.
    /// </summary>
    public async Task DeleteBatch(string batchId)
    {
        var batch = await GetBatch(batchId);
        if (batch == null)
        {
            throw new InvalidOperationException($"Batch {batchId} not found");
        }

        var logs = await GetMessageLogsByBatch(batchId);
        var logsTableClient = await GetTableClient(LOGS_TABLE_NAME);

        var deleteOps = logs.Select(log =>
            new TableTransactionAction(TableTransactionActionType.Delete, log));

        var failures = await TableBatch.SubmitInBatchesAsync(
            logsTableClient,
            deleteOps,
            onOperationFailed: (op, ex) =>
                _logger.LogWarning(ex, "Failed to delete delivery {PartitionKey}/{RowKey} for batch {BatchId}",
                    op.Entity.PartitionKey, op.Entity.RowKey, batchId));

        if (failures > 0)
        {
            _logger.LogWarning("{FailureCount} delivery rows could not be deleted for batch {BatchId}", failures, batchId);
        }

        // Remove the pending index entries so a cancelled batch can't later resurface as
        // "the newest pending card" for a user.
        await DeletePendingIndexEntriesAsync(logs, batchId);

        var batchesTableClient = await GetTableClient(BATCHES_TABLE_NAME);
        await batchesTableClient.DeleteEntityAsync(MessageBatchTableEntity.PartitionKeyVal, batchId);

        _logger.LogInformation($"Deleted batch {batchId} and {logs.Count} associated message logs");
    }

    #endregion

    #region Message Logs

    /// <summary>
    /// Log a single message send event.
    /// </summary>
    public async Task<MessageLogTableEntity> LogMessageSend(string messageBatchId, string? recipientUpn, string status, string? lastError = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientUpn);

        var logEntity = new MessageLogTableEntity
        {
            PartitionKey = DeliveryKey.PartitionFor(messageBatchId, recipientUpn),
            RowKey = DeliveryKey.RowKeyFor(recipientUpn),
            MessageBatchId = messageBatchId,
            SentDate = DateTime.UtcNow,
            RecipientUpn = recipientUpn,
            Status = status,
            LastError = lastError
        };

        var tableClient = await GetTableClient(LOGS_TABLE_NAME);
        // Upsert rather than Add: the natural key makes this idempotent, so a retry
        // updates the existing delivery instead of creating a duplicate.
        await tableClient.UpsertEntityAsync(logEntity, TableUpdateMode.Merge);

        _logger.LogInformation($"Logged message send for batch {messageBatchId}");
        return logEntity;
    }

    /// <summary>
    /// Create delivery rows for every recipient in a batch, plus the per-user pending index
    /// entries used when a user opens Teams after the app was installed for them.
    ///
    /// <para>
    /// Writes are idempotent: the row key is the normalised recipient UPN, so re-running a
    /// batch (or retrying a failed chunk) upserts the same row rather than inserting a
    /// duplicate. Duplicate UPNs in <paramref name="recipientUpns"/> are collapsed.
    /// </para>
    /// </summary>
    public async Task<List<MessageLogTableEntity>> LogBatchMessages(string messageBatchId, List<string> recipientUpns)
    {
        ArgumentNullException.ThrowIfNull(recipientUpns);

        _logger.LogInformation("Creating {RecipientCount} message log entries in storage for batch {BatchId}",
            recipientUpns.Count, messageBatchId);

        var tableClient = await GetTableClient(LOGS_TABLE_NAME);
        var now = DateTime.UtcNow;

        // Collapse duplicates up front - two rows with the same natural key would otherwise
        // collide inside a single transaction.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var logEntities = new List<MessageLogTableEntity>(recipientUpns.Count);
        var operations = new List<TableTransactionAction>(recipientUpns.Count);

        foreach (var recipientUpn in recipientUpns)
        {
            if (string.IsNullOrWhiteSpace(recipientUpn)) continue;

            var rowKey = DeliveryKey.RowKeyFor(recipientUpn);
            if (!seen.Add(rowKey)) continue;

            var logEntity = new MessageLogTableEntity
            {
                PartitionKey = DeliveryKey.PartitionFor(messageBatchId, recipientUpn),
                RowKey = rowKey,
                MessageBatchId = messageBatchId,
                SentDate = now,
                RecipientUpn = recipientUpn,
                Status = "Pending",
                LastError = null
            };
            logEntities.Add(logEntity);
            operations.Add(new TableTransactionAction(TableTransactionActionType.UpsertMerge, logEntity));
        }

        var failureCount = await TableBatch.SubmitInBatchesAsync(
            tableClient,
            operations,
            onOperationFailed: (op, ex) =>
                _logger.LogError(ex, "Failed to create delivery {PartitionKey}/{RowKey} in batch {BatchId}",
                    op.Entity.PartitionKey, op.Entity.RowKey, messageBatchId));

        var successCount = logEntities.Count - failureCount;

        if (failureCount > 0)
        {
            _logger.LogWarning("Created {SuccessCount}/{TotalCount} message logs for batch {BatchId}. {FailureCount} failed",
                successCount, logEntities.Count, messageBatchId, failureCount);
        }
        else
        {
            _logger.LogInformation("Successfully created all {LogCount} message log entries for batch {BatchId}",
                logEntities.Count, messageBatchId);
        }

        await WritePendingIndexEntriesAsync(messageBatchId, logEntities, now);
        await SetBatchTotalCountAsync(messageBatchId, logEntities.Count);

        return logEntities;
    }

    /// <summary>
    /// Populate the per-user pending index for a batch. Best-effort: a failure here only
    /// affects the "user opened Teams later" path, never the primary queue-driven send.
    /// </summary>
    private async Task WritePendingIndexEntriesAsync(
        string messageBatchId, List<MessageLogTableEntity> logEntities, DateTime createdUtc)
    {
        if (logEntities.Count == 0) return;

        var batch = await GetBatch(messageBatchId);
        if (batch == null) return;

        var pendingTable = await GetTableClient(PENDING_TABLE_NAME);
        var rowKey = DeliveryKey.PendingRowKey(createdUtc, messageBatchId);

        var ops = logEntities.Select(log => new TableTransactionAction(
            TableTransactionActionType.UpsertMerge,
            new PendingDeliveryTableEntity
            {
                PartitionKey = log.RowKey,   // already the normalised UPN
                RowKey = rowKey,
                BatchId = messageBatchId,
                TemplateId = batch.TemplateId,
                RecipientUpn = log.RecipientUpn ?? log.RowKey,
                CreatedUtc = createdUtc
            }));

        var failures = await TableBatch.SubmitInBatchesAsync(
            pendingTable,
            ops,
            onOperationFailed: (op, ex) =>
                _logger.LogWarning(ex, "Failed to write pending index entry for {PartitionKey}", op.Entity.PartitionKey));

        if (failures > 0)
        {
            _logger.LogWarning("{FailureCount} pending index entries could not be written for batch {BatchId}",
                failures, messageBatchId);
        }
    }

    /// <summary>
    /// Remove pending index entries for a batch (on send completion or batch deletion).
    /// </summary>
    private async Task DeletePendingIndexEntriesAsync(List<MessageLogTableEntity> logs, string batchId)
    {
        if (logs.Count == 0) return;

        var pendingTable = await GetTableClient(PENDING_TABLE_NAME);

        // The pending row key embeds the batch's creation time, which we don't have per-log
        // here, so locate entries by scanning each user's (small) partition for this batch.
        foreach (var log in logs)
        {
            try
            {
                var safeBatchId = ODataFilter.EscapeLiteral(batchId);
                var filter = $"PartitionKey eq '{ODataFilter.EscapeLiteral(log.RowKey)}' and BatchId eq '{safeBatchId}'";

                await foreach (var entry in pendingTable.QueryAsync<PendingDeliveryTableEntity>(filter: filter))
                {
                    await pendingTable.DeleteEntityAsync(entry.PartitionKey, entry.RowKey, ETag.All);
                }
            }
            catch (Azure.RequestFailedException ex)
            {
                _logger.LogWarning(ex, "Failed to clear pending index for {Upn} in batch {BatchId}", log.RowKey, batchId);
            }
        }
    }

    /// <summary>
    /// Remove a single pending index entry once its delivery has been sent.
    /// </summary>
    public async Task ClearPendingDeliveryAsync(string recipientUpn, string batchId)
    {
        if (string.IsNullOrWhiteSpace(recipientUpn) || string.IsNullOrWhiteSpace(batchId)) return;

        try
        {
            var pendingTable = await GetTableClient(PENDING_TABLE_NAME);
            var partitionKey = DeliveryKey.NormaliseUpn(recipientUpn);
            var filter = $"PartitionKey eq '{ODataFilter.EscapeLiteral(partitionKey)}' and BatchId eq '{ODataFilter.EscapeLiteral(batchId)}'";

            await foreach (var entry in pendingTable.QueryAsync<PendingDeliveryTableEntity>(filter: filter))
            {
                await pendingTable.DeleteEntityAsync(entry.PartitionKey, entry.RowKey, ETag.All);
            }
        }
        catch (Azure.RequestFailedException ex)
        {
            _logger.LogWarning(ex, "Failed to clear pending delivery for {Upn} in batch {BatchId}", recipientUpn, batchId);
        }
    }

    /// <summary>
    /// Record the recipient count on the batch row so dashboards never need to count rows.
    /// </summary>
    public async Task SetBatchTotalCountAsync(string messageBatchId, int totalCount)
    {
        try
        {
            var batchesTable = await GetTableClient(BATCHES_TABLE_NAME);
            var patch = new TableEntity(MessageBatchTableEntity.PartitionKeyVal, messageBatchId)
            {
                { nameof(MessageBatchTableEntity.TotalCount), totalCount },
                { nameof(MessageBatchTableEntity.LastProgressUtc), DateTime.UtcNow }
            };
            await batchesTable.UpdateEntityAsync(patch, ETag.All, TableUpdateMode.Merge);
        }
        catch (Azure.RequestFailedException ex)
        {
            _logger.LogWarning(ex, "Failed to record TotalCount for batch {BatchId}", messageBatchId);
        }
    }

    /// <summary>
    /// Update a delivery's status by its exact key.
    ///
    /// <para>
    /// A sparse merge patch: no read-before-write, and only the changed columns are sent.
    /// The previous implementation did a GET followed by a full Replace, costing two round
    /// trips per delivery (300,000/day at target scale) and re-uploading every column.
    /// </para>
    /// </summary>
    public async Task UpdateMessageLogStatus(string partitionKey, string rowKey, string status, string? lastError = null)
    {
        var tableClient = await GetTableClient(LOGS_TABLE_NAME);

        var patch = new TableEntity(partitionKey, rowKey)
        {
            { nameof(MessageLogTableEntity.Status), status },
            { nameof(MessageLogTableEntity.LastError), lastError }
        };

        try
        {
            await tableClient.UpdateEntityAsync(patch, ETag.All, TableUpdateMode.Merge);
            _logger.LogInformation("Updated delivery {PartitionKey}/{RowKey} to status {Status}", partitionKey, rowKey, status);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Delivery {PartitionKey}/{RowKey} not found", partitionKey, rowKey);
        }
    }

    /// <summary>
    /// Apply a delta to a batch's running success/failure counters.
    ///
    /// <para>
    /// Azure Table Storage has no atomic increment, so this uses an ETag compare-and-swap
    /// with bounded retries. Callers are expected to aggregate outcomes in memory and flush
    /// periodically rather than calling this once per delivery, which keeps contention on
    /// the single batch row negligible.
    /// </para>
    /// </summary>
    public async Task IncrementBatchCountersAsync(string batchId, int sentDelta, int failedDelta, int maxRetries = 5)
    {
        if (sentDelta == 0 && failedDelta == 0) return;

        var batchesTable = await GetTableClient(BATCHES_TABLE_NAME);

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var response = await batchesTable.GetEntityAsync<MessageBatchTableEntity>(
                    MessageBatchTableEntity.PartitionKeyVal, batchId);
                var batch = response.Value;

                batch.SentCount += sentDelta;
                batch.FailedCount += failedDelta;
                batch.LastProgressUtc = DateTime.UtcNow;

                await batchesTable.UpdateEntityAsync(batch, batch.ETag, TableUpdateMode.Merge);
                return;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 412)
            {
                // Lost the race - re-read and reapply.
                await Task.Delay(TimeSpan.FromMilliseconds(20 * (attempt + 1)));
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogWarning("Batch {BatchId} not found when updating counters", batchId);
                return;
            }
        }

        _logger.LogWarning("Gave up updating counters for batch {BatchId} after {MaxRetries} attempts", batchId, maxRetries);
    }

    /// <summary>
    /// Get a single delivery by its exact key.
    /// </summary>
    public async Task<MessageLogTableEntity?> GetMessageLog(string partitionKey, string rowKey)
    {
        var tableClient = await GetTableClient(LOGS_TABLE_NAME);
        try
        {
            var response = await tableClient.GetEntityAsync<MessageLogTableEntity>(partitionKey, rowKey);
            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    /// <summary>
    /// Get the newest pending delivery for a user, using the per-user pending index.
    /// Reads the first row of one small partition instead of scanning every delivery.
    /// </summary>
    public async Task<PendingDeliveryTableEntity?> GetNewestPendingDeliveryAsync(string recipientUpn)
    {
        if (string.IsNullOrWhiteSpace(recipientUpn)) return null;

        var pendingTable = await GetTableClient(PENDING_TABLE_NAME);
        var partitionKey = DeliveryKey.NormaliseUpn(recipientUpn);
        var filter = $"PartitionKey eq '{ODataFilter.EscapeLiteral(partitionKey)}'";

        // Row keys embed an inverted tick count, so the first row is the newest.
        await foreach (var entry in pendingTable.QueryAsync<PendingDeliveryTableEntity>(filter: filter, maxPerPage: 1))
        {
            return entry;
        }

        return null;
    }

    /// <summary>
    /// Get every pending delivery for a user (newest first).
    /// </summary>
    public async Task<List<PendingDeliveryTableEntity>> GetPendingDeliveriesAsync(string recipientUpn)
    {
        var results = new List<PendingDeliveryTableEntity>();
        if (string.IsNullOrWhiteSpace(recipientUpn)) return results;

        var pendingTable = await GetTableClient(PENDING_TABLE_NAME);
        var partitionKey = DeliveryKey.NormaliseUpn(recipientUpn);
        var filter = $"PartitionKey eq '{ODataFilter.EscapeLiteral(partitionKey)}'";

        await foreach (var entry in pendingTable.QueryAsync<PendingDeliveryTableEntity>(filter: filter))
        {
            results.Add(entry);
        }

        return results;
    }

    /// <summary>
    /// Get deliveries for a specific batch.
    ///
    /// <para>
    /// A bounded partition-key range scan over just this batch's shards. Previously this
    /// filtered on <c>MessageBatchId</c>, which is not a key, so it scanned every delivery
    /// row ever written (tens of millions after a year in production).
    /// </para>
    /// </summary>
    public async Task<List<MessageLogTableEntity>> GetMessageLogsByBatch(string batchId)
    {
        var tableClient = await GetTableClient(LOGS_TABLE_NAME);
        var logs = new List<MessageLogTableEntity>();

        var start = ODataFilter.EscapeLiteral(DeliveryKey.PartitionRangeStartInclusive(batchId));
        var end = ODataFilter.EscapeLiteral(DeliveryKey.PartitionRangeEndExclusive(batchId));

        await foreach (var entity in tableClient.QueryAsync<MessageLogTableEntity>(
            filter: $"PartitionKey ge '{start}' and PartitionKey lt '{end}'"))
        {
            logs.Add(entity);
        }

        return logs;
    }

    /// <summary>
    /// Get deliveries for a specific template, by querying only the batches that use it.
    /// </summary>
    public async Task<List<MessageLogTableEntity>> GetMessageLogsByTemplate(string templateId)
    {
        var batches = await GetAllBatches();
        var templateBatchIds = batches
            .Where(b => b.TemplateId == templateId)
            .Select(b => b.RowKey)
            .ToHashSet(StringComparer.Ordinal);

        if (templateBatchIds.Count == 0)
        {
            return new List<MessageLogTableEntity>();
        }

        // Query each owning batch's partition range. Bounded by the number of batches that
        // use this template, instead of scanning every delivery row ever written.
        var results = new List<MessageLogTableEntity>();
        foreach (var batchId in templateBatchIds)
        {
            results.AddRange(await GetMessageLogsByBatch(batchId));
        }

        return results;
    }

    #endregion
}
