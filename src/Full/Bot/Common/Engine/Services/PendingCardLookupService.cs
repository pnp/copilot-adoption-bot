using Azure.Data.Tables;
using Common.Engine.Storage;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Common.Engine.Services;

/// <summary>
/// Service for looking up pending cards to send to users from Azure Table Storage
/// </summary>
public class PendingCardLookupService
{
    private readonly MessageTemplateStorageManager _storageManager;
    private readonly ILogger<PendingCardLookupService> _logger;
    private const string LOGS_TABLE_NAME = "messagelogs";

    public PendingCardLookupService(
        MessageTemplateStorageManager storageManager,
        ILogger<PendingCardLookupService> logger)
    {
        _storageManager = storageManager;
        _logger = logger;
    }

    /// <summary>
    /// Finds the latest pending card for a specific user by UPN
    /// </summary>
    /// <param name="upn">User Principal Name</param>
    /// <returns>Pending card info if found, null otherwise</returns>
    public async Task<PendingCardInfo?> GetLatestPendingCardByUpn(string upn)
    {
        try
        {
            _logger.LogInformation($"Looking for pending cards for user {upn}");

            var tableClient = await _storageManager.GetTableClient(LOGS_TABLE_NAME);

            // Query for pending messages for this UPN, ordered by SentDate descending
            var filter = $"PartitionKey eq '{MessageLogTableEntity.PartitionKeyVal}' and RecipientUpn eq '{upn}' and Status eq 'Pending'";
            
            var query = tableClient.QueryAsync<MessageLogTableEntity>(
                filter: filter,
                maxPerPage: 100);

            var pendingLogs = new List<MessageLogTableEntity>();
            await foreach (var log in query)
            {
                pendingLogs.Add(log);
            }

            if (!pendingLogs.Any())
            {
                _logger.LogInformation($"No pending cards found for user {upn}");
                return null;
            }

            // Get the latest pending message (most recent SentDate)
            var latestLog = pendingLogs.OrderByDescending(l => l.SentDate).First();

            _logger.LogInformation($"Found pending card for user {upn}: Log ID {latestLog.RowKey}, Batch ID {latestLog.MessageBatchId}");

            // Get the batch to retrieve template ID
            var batch = await _storageManager.GetBatch(latestLog.MessageBatchId);
            if (batch == null)
            {
                _logger.LogWarning($"Batch {latestLog.MessageBatchId} not found for pending card");
                return null;
            }

            // Get the template
            var template = await _storageManager.GetTemplate(batch.TemplateId);
            if (template == null)
            {
                _logger.LogWarning($"Template {batch.TemplateId} not found for pending card");
                return null;
            }

            // Get the template JSON
            var templateJson = await _storageManager.GetTemplateJson(batch.TemplateId);

            // Parse JSON into an Attachment
            var cardAttachment = CreateCardAttachment(templateJson);

            return new PendingCardInfo
            {
                MessageLogId = latestLog.RowKey,
                BatchId = latestLog.MessageBatchId,
                TemplateId = batch.TemplateId,
                TemplateName = template.TemplateName,
                CardJson = templateJson,
                CardAttachment = cardAttachment,
                SentDate = latestLog.SentDate,
                RecipientUpn = latestLog.RecipientUpn ?? upn
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error looking up pending card for user {upn}");
            return null;
        }
    }

    /// <summary>
    /// Gets all pending cards for a specific user by UPN
    /// </summary>
    /// <param name="upn">User Principal Name</param>
    /// <returns>List of pending cards</returns>
    public async Task<List<PendingCardInfo>> GetAllPendingCardsByUpn(string upn)
    {
        try
        {
            _logger.LogInformation($"Looking for all pending cards for user {upn}");

            var tableClient = await _storageManager.GetTableClient(LOGS_TABLE_NAME);

            var filter = $"PartitionKey eq '{MessageLogTableEntity.PartitionKeyVal}' and RecipientUpn eq '{upn}' and Status eq 'Pending'";
            
            var query = tableClient.QueryAsync<MessageLogTableEntity>(
                filter: filter);

            var pendingLogs = new List<MessageLogTableEntity>();
            await foreach (var log in query)
            {
                pendingLogs.Add(log);
            }

            if (!pendingLogs.Any())
            {
                _logger.LogInformation($"No pending cards found for user {upn}");
                return new List<PendingCardInfo>();
            }

            // Get all pending cards with their templates
            var pendingCards = new List<PendingCardInfo>();
            foreach (var log in pendingLogs.OrderByDescending(l => l.SentDate))
            {
                try
                {
                    var batch = await _storageManager.GetBatch(log.MessageBatchId);
                    if (batch == null) continue;

                    var template = await _storageManager.GetTemplate(batch.TemplateId);
                    if (template == null) continue;

                    var templateJson = await _storageManager.GetTemplateJson(batch.TemplateId);
                    var cardAttachment = CreateCardAttachment(templateJson);

                    pendingCards.Add(new PendingCardInfo
                    {
                        MessageLogId = log.RowKey,
                        BatchId = log.MessageBatchId,
                        TemplateId = batch.TemplateId,
                        TemplateName = template.TemplateName,
                        CardJson = templateJson,
                        CardAttachment = cardAttachment,
                        SentDate = log.SentDate,
                        RecipientUpn = log.RecipientUpn ?? upn
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Error processing pending card for log {log.RowKey}");
                }
            }

            _logger.LogInformation($"Found {pendingCards.Count} pending cards for user {upn}");
            return pendingCards;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error looking up pending cards for user {upn}");
            return new List<PendingCardInfo>();
        }
    }

    /// <summary>
    /// Creates a Bot Framework Attachment from adaptive card JSON
    /// </summary>
    private Attachment CreateCardAttachment(string cardJson)
    {
        return new Attachment
        {
            ContentType = "application/vnd.microsoft.card.adaptive",
            Content = JsonConvert.DeserializeObject(cardJson)
        };
    }
}

/// <summary>
/// Information about a pending card
/// </summary>
public class PendingCardInfo
{
    public string MessageLogId { get; set; } = null!;
    public string BatchId { get; set; } = null!;
    public string TemplateId { get; set; } = null!;
    public string TemplateName { get; set; } = null!;
    public string CardJson { get; set; } = null!;
    public Attachment CardAttachment { get; set; } = null!;
    public DateTime SentDate { get; set; }
    public string RecipientUpn { get; set; } = null!;
}
