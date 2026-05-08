using Common.Engine.Storage;
using Microsoft.Bot.Schema;

namespace Common.Engine.Services;

/// <summary>
/// Pure helper that turns a batch of pending message logs into <see cref="PendingCardInfo"/>
/// while deduplicating batch / template / template-JSON lookups.
/// Extracted from <see cref="PendingCardLookupService"/> for testability.
/// </summary>
public static class PendingCardMaterializer
{
    /// <summary>
    /// Materialize pending cards from logs. Each unique batch / template id is fetched at most once.
    /// </summary>
    /// <param name="upn">Fallback UPN used when a log has no recipient.</param>
    /// <param name="logs">Pending message logs (will be processed newest-first).</param>
    /// <param name="getBatch">Async delegate to load a <see cref="MessageBatchTableEntity"/> by id.</param>
    /// <param name="getTemplate">Async delegate to load a <see cref="MessageTemplateTableEntity"/> by id.</param>
    /// <param name="getTemplateJson">Async delegate to load template JSON by template id.</param>
    /// <param name="createAttachment">Factory that creates a Bot Framework attachment from card JSON.</param>
    /// <param name="onLogError">Optional callback invoked for per-log exceptions (for logging).</param>
    public static async Task<List<PendingCardInfo>> MaterializeAsync(
        string upn,
        IEnumerable<MessageLogTableEntity> logs,
        Func<string, Task<MessageBatchTableEntity?>> getBatch,
        Func<string, Task<MessageTemplateTableEntity?>> getTemplate,
        Func<string, Task<string>> getTemplateJson,
        Func<string, Attachment> createAttachment,
        Action<MessageLogTableEntity, Exception>? onLogError = null)
    {
        ArgumentNullException.ThrowIfNull(logs);
        ArgumentNullException.ThrowIfNull(getBatch);
        ArgumentNullException.ThrowIfNull(getTemplate);
        ArgumentNullException.ThrowIfNull(getTemplateJson);
        ArgumentNullException.ThrowIfNull(createAttachment);

        var pendingCards = new List<PendingCardInfo>();
        var batchCache = new Dictionary<string, MessageBatchTableEntity?>(StringComparer.Ordinal);
        var templateCache = new Dictionary<string, MessageTemplateTableEntity?>(StringComparer.Ordinal);
        var templateJsonCache = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var log in logs.OrderByDescending(l => l.SentDate))
        {
            try
            {
                if (!batchCache.TryGetValue(log.MessageBatchId, out var batch))
                {
                    batch = await getBatch(log.MessageBatchId);
                    batchCache[log.MessageBatchId] = batch;
                }
                if (batch == null) continue;

                if (!templateCache.TryGetValue(batch.TemplateId, out var template))
                {
                    template = await getTemplate(batch.TemplateId);
                    templateCache[batch.TemplateId] = template;
                }
                if (template == null) continue;

                if (!templateJsonCache.TryGetValue(batch.TemplateId, out var templateJson))
                {
                    templateJson = await getTemplateJson(batch.TemplateId);
                    templateJsonCache[batch.TemplateId] = templateJson;
                }

                pendingCards.Add(new PendingCardInfo
                {
                    MessageLogId = log.RowKey,
                    BatchId = log.MessageBatchId,
                    TemplateId = batch.TemplateId,
                    TemplateName = template.TemplateName,
                    CardJson = templateJson,
                    CardAttachment = createAttachment(templateJson),
                    SentDate = log.SentDate,
                    RecipientUpn = log.RecipientUpn ?? upn
                });
            }
            catch (Exception ex)
            {
                onLogError?.Invoke(log, ex);
            }
        }

        return pendingCards;
    }
}
