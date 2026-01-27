using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Common.Engine.Config;
using Common.Engine.Storage;
using Microsoft.Extensions.Logging;

namespace Common.Engine;

/// <summary>
/// Manages message templates in Azure Storage (Table + Blob)
/// </summary>
public class MessageTemplateStorageManager : TableStorageManager
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger _logger;
    private const string TEMPLATES_TABLE_NAME = "messagetemplates";
    private const string BATCHES_TABLE_NAME = "messagebatches";
    private const string LOGS_TABLE_NAME = "messagelogs";
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
    public async Task<MessageBatchTableEntity> CreateBatch(string batchName, string templateId, string senderUpn)
    {
        var batchId = Guid.NewGuid().ToString();
        
        var batchEntity = new MessageBatchTableEntity
        {
            RowKey = batchId,
            BatchName = batchName,
            TemplateId = templateId,
            SenderUpn = senderUpn,
            CreatedDate = DateTime.UtcNow
        };

        var tableClient = await GetTableClient(BATCHES_TABLE_NAME);
        await tableClient.AddEntityAsync(batchEntity);

        _logger.LogInformation($"Created batch '{batchName}' with ID {batchId}");
        return batchEntity;
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
    /// Delete a batch and all its associated message logs
    /// </summary>
    public async Task DeleteBatch(string batchId)
    {
        var batch = await GetBatch(batchId);
        if (batch == null)
        {
            throw new InvalidOperationException($"Batch {batchId} not found");
        }

        // Delete all message logs associated with this batch
        var logs = await GetMessageLogsByBatch(batchId);
        var logsTableClient = await GetTableClient(LOGS_TABLE_NAME);
        
        foreach (var log in logs)
        {
            await logsTableClient.DeleteEntityAsync(MessageLogTableEntity.PartitionKeyVal, log.RowKey);
        }

        // Delete the batch
        var batchesTableClient = await GetTableClient(BATCHES_TABLE_NAME);
        await batchesTableClient.DeleteEntityAsync(MessageBatchTableEntity.PartitionKeyVal, batchId);

        _logger.LogInformation($"Deleted batch {batchId} and {logs.Count} associated message logs");
    }

    #endregion

    #region Message Logs

    /// <summary>
    /// Log a message send event
    /// </summary>
    public async Task<MessageLogTableEntity> LogMessageSend(string messageBatchId, string? recipientUpn, string status, string? lastError = null)
    {
        var logEntity = new MessageLogTableEntity
        {
            RowKey = Guid.NewGuid().ToString(),
            MessageBatchId = messageBatchId,
            SentDate = DateTime.UtcNow,
            RecipientUpn = recipientUpn,
            Status = status,
            LastError = lastError
        };

        var tableClient = await GetTableClient(LOGS_TABLE_NAME);
        await tableClient.AddEntityAsync(logEntity);

        _logger.LogInformation($"Logged message send for batch {messageBatchId}");
        return logEntity;
    }

    /// <summary>
    /// Create multiple message log entries for a batch
    /// </summary>
    public async Task<List<MessageLogTableEntity>> LogBatchMessages(string messageBatchId, List<string> recipientUpns)
    {
        var tableClient = await GetTableClient(LOGS_TABLE_NAME);
        var logEntities = new List<MessageLogTableEntity>();

        foreach (var recipientUpn in recipientUpns)
        {
            var logEntity = new MessageLogTableEntity
            {
                RowKey = Guid.NewGuid().ToString(),
                MessageBatchId = messageBatchId,
                SentDate = DateTime.UtcNow,
                RecipientUpn = recipientUpn,
                Status = "Pending",
                LastError = null
            };

            await tableClient.AddEntityAsync(logEntity);
            logEntities.Add(logEntity);
        }

        _logger.LogInformation($"Created {logEntities.Count} message logs for batch {messageBatchId}");
        return logEntities;
    }

    /// <summary>
    /// Update a message log status
    /// </summary>
    public async Task UpdateMessageLogStatus(string logId, string status, string? lastError = null)
    {
        var tableClient = await GetTableClient(LOGS_TABLE_NAME);
        
        try
        {
            var response = await tableClient.GetEntityAsync<MessageLogTableEntity>(
                MessageLogTableEntity.PartitionKeyVal, logId);
            var logEntity = response.Value;

            logEntity.Status = status;
            logEntity.LastError = lastError;

            await tableClient.UpdateEntityAsync(logEntity, logEntity.ETag, TableUpdateMode.Replace);
            _logger.LogInformation($"Updated message log {logId} to status {status}");
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning($"Message log {logId} not found");
        }
    }

    /// <summary>
    /// Get all message logs
    /// </summary>
    public async Task<List<MessageLogTableEntity>> GetAllMessageLogs()
    {
        var tableClient = await GetTableClient(LOGS_TABLE_NAME);
        var logs = new List<MessageLogTableEntity>();

        await foreach (var entity in tableClient.QueryAsync<MessageLogTableEntity>(
            filter: $"PartitionKey eq '{MessageLogTableEntity.PartitionKeyVal}'"))
        {
            logs.Add(entity);
        }

        return logs;
    }

    /// <summary>
    /// Get message logs for a specific batch
    /// </summary>
    public async Task<List<MessageLogTableEntity>> GetMessageLogsByBatch(string batchId)
    {
        var tableClient = await GetTableClient(LOGS_TABLE_NAME);
        var logs = new List<MessageLogTableEntity>();

        await foreach (var entity in tableClient.QueryAsync<MessageLogTableEntity>(
            filter: $"PartitionKey eq '{MessageLogTableEntity.PartitionKeyVal}' and MessageBatchId eq '{batchId}'"))
        {
            logs.Add(entity);
        }

        return logs;
    }

    /// <summary>
    /// Get message logs for a specific template (via batches)
    /// </summary>
    public async Task<List<MessageLogTableEntity>> GetMessageLogsByTemplate(string templateId)
    {
        var batches = await GetAllBatches();
        var templateBatches = batches.Where(b => b.TemplateId == templateId).ToList();
        
        var logs = new List<MessageLogTableEntity>();
        foreach (var batch in templateBatches)
        {
            var batchLogs = await GetMessageLogsByBatch(batch.RowKey);
            logs.AddRange(batchLogs);
        }

        return logs;
    }

    #endregion
}
