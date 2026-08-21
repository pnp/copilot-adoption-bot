using Engine.Storage;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Engine.Services;

/// <summary>
/// Resolves the adaptive card to deliver to a user.
///
/// <para>
/// The queue-driven path addresses a delivery by <em>batch + template</em>, which is a point
/// read. The previous implementation instead searched the whole delivery table for
/// "newest Pending row with this RecipientUpn" - a filter on two non-key properties, so a
/// full scan of every row ever written, on every single send - and then delivered whichever
/// row happened to be newest, which could be a different delivery from the one being
/// processed.
/// </para>
///
/// <para>
/// Template JSON is cached per template id, so a 150,000-recipient batch downloads the
/// blob once rather than once per recipient.
/// </para>
/// </summary>
public class PendingCardLookupService
{
    private readonly MessageTemplateStorageManager _storageManager;
    private readonly ILogger<PendingCardLookupService> _logger;

    /// <summary>
    /// Process-wide cache of rendered template JSON keyed by template id. Templates are
    /// immutable per id for the lifetime of a batch, and a batch shares one template across
    /// every recipient.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Task<string>> TemplateJsonCache = new();

    public PendingCardLookupService(
        MessageTemplateStorageManager storageManager,
        ILogger<PendingCardLookupService> logger)
    {
        _storageManager = storageManager;
        _logger = logger;
    }

    /// <summary>
    /// Load a specific delivery by batch + template. No search, no scan.
    /// </summary>
    public async Task<PendingCardInfo?> GetDeliveryCardAsync(string upn, string batchId, string templateId)
    {
        try
        {
            var template = await _storageManager.GetTemplate(templateId);
            if (template == null)
            {
                _logger.LogWarning("Template {TemplateId} not found for batch {BatchId}", templateId, batchId);
                return null;
            }

            var templateJson = await GetTemplateJsonCachedAsync(templateId);

            return new PendingCardInfo
            {
                BatchId = batchId,
                TemplateId = templateId,
                TemplateName = template.TemplateName,
                CardJson = templateJson,
                CardAttachment = CreateCardAttachment(templateJson),
                SentDate = DateTime.UtcNow,
                RecipientUpn = upn
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading delivery card for {Upn} in batch {BatchId}", upn, batchId);
            return null;
        }
    }

    /// <summary>
    /// Find the newest not-yet-delivered card for a user via the per-user pending index.
    /// Reads the first row of one small partition rather than scanning delivery history.
    /// </summary>
    public async Task<PendingCardInfo?> GetLatestPendingCardByUpn(string upn)
    {
        try
        {
            var pending = await _storageManager.GetNewestPendingDeliveryAsync(upn);
            if (pending == null)
            {
                _logger.LogInformation("No pending cards found for user {Upn}", upn);
                return null;
            }

            var card = await GetDeliveryCardAsync(upn, pending.BatchId, pending.TemplateId);
            if (card != null)
            {
                card.SentDate = pending.CreatedUtc;
            }
            return card;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up pending card for user {Upn}", upn);
            return null;
        }
    }

    /// <summary>
    /// Gets all pending cards for a user, newest first.
    /// </summary>
    public async Task<List<PendingCardInfo>> GetAllPendingCardsByUpn(string upn)
    {
        var results = new List<PendingCardInfo>();

        try
        {
            var pendingEntries = await _storageManager.GetPendingDeliveriesAsync(upn);

            foreach (var entry in pendingEntries)
            {
                var card = await GetDeliveryCardAsync(upn, entry.BatchId, entry.TemplateId);
                if (card != null)
                {
                    card.SentDate = entry.CreatedUtc;
                    results.Add(card);
                }
            }

            _logger.LogInformation("Found {Count} pending cards for user {Upn}", results.Count, upn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up pending cards for user {Upn}", upn);
        }

        return results;
    }

    /// <summary>
    /// Cache template JSON per template id. Uses a <see cref="Task{T}"/> value so concurrent
    /// callers share one blob download rather than racing to fetch the same template.
    /// </summary>
    private Task<string> GetTemplateJsonCachedAsync(string templateId) =>
        TemplateJsonCache.GetOrAdd(templateId, id => _storageManager.GetTemplateJson(id));

    /// <summary>
    /// Drop a cached template so an edited template is picked up.
    /// </summary>
    public static void InvalidateTemplateCache(string templateId) =>
        TemplateJsonCache.TryRemove(templateId, out _);

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
/// Information about a card to deliver.
/// </summary>
public class PendingCardInfo
{
    public string BatchId { get; set; } = null!;
    public string TemplateId { get; set; } = null!;
    public string TemplateName { get; set; } = null!;
    public string CardJson { get; set; } = null!;
    public Attachment CardAttachment { get; set; } = null!;
    public DateTime SentDate { get; set; }
    public string RecipientUpn { get; set; } = null!;
}
