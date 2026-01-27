using Common.Engine.Storage;
using Microsoft.Extensions.Logging;

namespace Common.Engine.Services;

/// <summary>
/// Service for managing message templates and logs
/// </summary>
public class MessageTemplateService
{
    private readonly MessageTemplateStorageManager _storageManager;
    private readonly BatchQueueService _queueService;
    private readonly ILogger<MessageTemplateService> _logger;

    public MessageTemplateService(
        MessageTemplateStorageManager storageManager, 
        BatchQueueService queueService,
        ILogger<MessageTemplateService> logger)
    {
        _storageManager = storageManager;
        _queueService = queueService;
        _logger = logger;
    }

    public async Task<MessageTemplateDto> CreateTemplate(string templateName, string jsonPayload, string createdByUpn)
    {
        _logger.LogInformation($"Creating template '{templateName}' by {createdByUpn}");
        var entity = await _storageManager.SaveTemplate(templateName, jsonPayload, createdByUpn);
        return MapToDto(entity);
    }

    public async Task<List<MessageTemplateDto>> GetAllTemplates()
    {
        var entities = await _storageManager.GetAllTemplates();
        return entities.Select(MapToDto).ToList();
    }

    public async Task<MessageTemplateDto?> GetTemplate(string templateId)
    {
        var entity = await _storageManager.GetTemplate(templateId);
        return entity != null ? MapToDto(entity) : null;
    }

    public async Task<string> GetTemplateJson(string templateId)
    {
        return await _storageManager.GetTemplateJson(templateId);
    }

    public async Task<MessageTemplateDto> UpdateTemplate(string templateId, string templateName, string jsonPayload)
    {
        _logger.LogInformation($"Updating template {templateId}");
        var entity = await _storageManager.UpdateTemplate(templateId, templateName, jsonPayload);
        return MapToDto(entity);
    }

    public async Task DeleteTemplate(string templateId)
    {
        _logger.LogInformation($"Deleting template {templateId}");
        await _storageManager.DeleteTemplate(templateId);
    }

    public async Task<MessageBatchDto> CreateBatch(string batchName, string templateId, string senderUpn)
    {
        _logger.LogInformation($"Creating batch '{batchName}' for template {templateId}");
        var entity = await _storageManager.CreateBatch(batchName, templateId, senderUpn);
        return MapBatchToDto(entity);
    }

    public async Task<List<MessageBatchDto>> GetAllBatches()
    {
        var entities = await _storageManager.GetAllBatches();
        return entities.Select(MapBatchToDto).ToList();
    }

    public async Task<MessageBatchDto?> GetBatch(string batchId)
    {
        var entity = await _storageManager.GetBatch(batchId);
        return entity != null ? MapBatchToDto(entity) : null;
    }

    public async Task DeleteBatch(string batchId)
    {
        _logger.LogInformation($"Deleting batch {batchId}");
        await _storageManager.DeleteBatch(batchId);
    }

    public async Task<MessageLogDto> LogMessageSend(string messageBatchId, string? recipientUpn, string status, string? lastError = null)
    {
        var entity = await _storageManager.LogMessageSend(messageBatchId, recipientUpn, status, lastError);
        return MapLogToDto(entity);
    }

    public async Task<List<MessageLogDto>> LogBatchMessages(string messageBatchId, List<string> recipientUpns)
    {
        _logger.LogInformation("Creating message logs for batch {BatchId} with {RecipientCount} recipients", 
            messageBatchId, recipientUpns.Count);
        
        var entities = await _storageManager.LogBatchMessages(messageBatchId, recipientUpns);
        
        _logger.LogInformation("Created {LogCount} message log entries for batch {BatchId}", 
            entities.Count, messageBatchId);
        
        // Get batch to retrieve template ID
        var batch = await _storageManager.GetBatch(messageBatchId);
        if (batch == null)
        {
            _logger.LogError("Batch {BatchId} not found when trying to enqueue messages", messageBatchId);
            throw new InvalidOperationException($"Batch {messageBatchId} not found");
        }

        _logger.LogDebug("Retrieved batch {BatchId} with template {TemplateId} for enqueueing", 
            messageBatchId, batch.TemplateId);

        // Enqueue messages for asynchronous processing
        var queueMessages = entities.Select(entity => new BatchQueueMessage
        {
            BatchId = messageBatchId,
            MessageLogId = entity.RowKey,
            RecipientUpn = entity.RecipientUpn ?? string.Empty,
            TemplateId = batch.TemplateId
        }).ToList();

        _logger.LogInformation("Enqueueing {MessageCount} messages for batch {BatchId} to queue", 
            queueMessages.Count, messageBatchId);

        await _queueService.EnqueueBatchMessagesAsync(queueMessages);
        
        _logger.LogInformation("Successfully completed batch message logging and enqueueing for batch {BatchId}", 
            messageBatchId);
        
        return entities.Select(MapLogToDto).ToList();
    }

    public async Task UpdateMessageLogStatus(string logId, string status, string? lastError = null)
    {
        await _storageManager.UpdateMessageLogStatus(logId, status, lastError);
    }

    public async Task<List<MessageLogDto>> GetAllMessageLogs()
    {
        var entities = await _storageManager.GetAllMessageLogs();
        return entities.Select(MapLogToDto).ToList();
    }

    public async Task<List<MessageLogDto>> GetMessageLogsByBatch(string batchId)
    {
        var entities = await _storageManager.GetMessageLogsByBatch(batchId);
        return entities.Select(MapLogToDto).ToList();
    }

    public async Task<List<MessageLogDto>> GetMessageLogsByTemplate(string templateId)
    {
        var entities = await _storageManager.GetMessageLogsByTemplate(templateId);
        return entities.Select(MapLogToDto).ToList();
    }

    private MessageTemplateDto MapToDto(MessageTemplateTableEntity entity)
    {
        return new MessageTemplateDto
        {
            Id = entity.RowKey,
            TemplateName = entity.TemplateName,
            BlobUrl = entity.BlobUrl,
            CreatedByUpn = entity.CreatedByUpn,
            CreatedDate = entity.CreatedDate
        };
    }

    private MessageBatchDto MapBatchToDto(MessageBatchTableEntity entity)
    {
        return new MessageBatchDto
        {
            Id = entity.RowKey,
            BatchName = entity.BatchName,
            TemplateId = entity.TemplateId,
            SenderUpn = entity.SenderUpn,
            CreatedDate = entity.CreatedDate
        };
    }

    private MessageLogDto MapLogToDto(MessageLogTableEntity entity)
    {
        return new MessageLogDto
        {
            Id = entity.RowKey,
            MessageBatchId = entity.MessageBatchId,
            SentDate = entity.SentDate,
            RecipientUpn = entity.RecipientUpn,
            Status = entity.Status,
            LastError = entity.LastError
        };
    }
}

public class MessageTemplateDto
{
    public string Id { get; set; } = null!;
    public string TemplateName { get; set; } = null!;
    public string BlobUrl { get; set; } = null!;
    public string CreatedByUpn { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
}

public class MessageBatchDto
{
    public string Id { get; set; } = null!;
    public string BatchName { get; set; } = null!;
    public string TemplateId { get; set; } = null!;
    public string SenderUpn { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
}

public class MessageLogDto
{
    public string Id { get; set; } = null!;
    public string MessageBatchId { get; set; } = null!;
    public DateTime SentDate { get; set; }
    public string? RecipientUpn { get; set; }
    public string Status { get; set; } = null!;
    public string? LastError { get; set; }
}
