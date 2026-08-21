using Engine.Services;
using Engine.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Server.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class SendNudgeController : ControllerBase
{
    private readonly MessageTemplateService _templateService;
    private readonly SmartGroupService _smartGroupService;
    private readonly ILogger<SendNudgeController> _logger;

    public SendNudgeController(
        MessageTemplateService templateService,
        SmartGroupService smartGroupService,
        ILogger<SendNudgeController> logger)
    {
        _templateService = templateService;
        _smartGroupService = smartGroupService;
        _logger = logger;
    }

    // POST: api/SendNudge/ParseFile
    [HttpPost(nameof(ParseFile))]
    public async Task<IActionResult> ParseFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded");
        }

        try
        {
            var upns = new List<string>();

            using (var stream = file.OpenReadStream())
            using (var reader = new StreamReader(stream))
            {
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    var trimmedLine = line.AsSpan().Trim();
                    if (trimmedLine.IsEmpty)
                    {
                        continue;
                    }

                    // Handle CSV - take first column if comma-separated. Span-based to avoid
                    // allocating a string[] per line just to read index 0.
                    var comma = trimmedLine.IndexOf(',');
                    var upn = (comma < 0 ? trimmedLine : trimmedLine.Slice(0, comma)).Trim();
                    if (!upn.IsEmpty)
                    {
                        upns.Add(upn.ToString());
                    }
                }
            }

            _logger.LogInformation($"Parsed {upns.Count} UPNs from file {file.FileName}");
            return Ok(new { upns });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing file");
            return StatusCode(500, "Error parsing file");
        }
    }

    // POST: api/SendNudge/CreateBatchAndSend
    [HttpPost(nameof(CreateBatchAndSend))]
    public async Task<IActionResult> CreateBatchAndSend([FromBody] CreateBatchAndSendRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BatchName))
        {
            return BadRequest("BatchName is required");
        }

        if (string.IsNullOrWhiteSpace(request.TemplateId))
        {
            return BadRequest("TemplateId is required");
        }

        // Must have either recipient UPNs or smart group IDs
        var hasRecipientUpns = request.RecipientUpns != null && request.RecipientUpns.Any();
        var hasSmartGroups = request.SmartGroupIds != null && request.SmartGroupIds.Any();

        if (!hasRecipientUpns && !hasSmartGroups)
        {
            return BadRequest("At least one recipient UPN or smart group is required");
        }

        try
        {
            // Get user principal name from claims
            var senderUpn = User.Identity?.Name ?? "unknown";

            // Verify template exists
            var template = await _templateService.GetTemplate(request.TemplateId);
            if (template == null)
            {
                return NotFound($"Template {request.TemplateId} not found");
            }

            // Create the batch in a Queued state and hand expansion to the background worker.
            // Expanding 150,000 recipients inline took ~250-310s against App Service's hard
            // 230s request timeout, and a timeout left a half-created batch with no way to tell
            // which recipients had been enqueued - so a retry double-sent to everyone already
            // queued.
            var batch = await _templateService.CreateBatch(
                request.BatchName, request.TemplateId, senderUpn, request.ScheduledSendUtc);

            await _templateService.EnqueueBatchExpansionAsync(
                batch.Id,
                request.RecipientUpns ?? new List<string>(),
                request.SmartGroupIds ?? new List<string>());

            _logger.LogInformation("Accepted batch {BatchId}; expansion queued", batch.Id);

            return Accepted(new
            {
                batch,
                status = BatchStatus.Queued,
                message = "Batch accepted. Recipients are being resolved in the background; poll the batch for progress."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating batch and sending messages");
            return StatusCode(500, "Error creating batch and sending messages");
        }
    }

    // POST: api/SendNudge/CancelBatch/{batchId}
    /// <summary>
    /// Stop an in-flight batch. Remaining queued deliveries are dropped by the dispatcher
    /// rather than being deleted from storage, so the audit trail of what was already sent
    /// stays intact.
    /// </summary>
    [HttpPost("CancelBatch/{batchId}")]
    public async Task<IActionResult> CancelBatch(string batchId)
    {
        try
        {
            await _templateService.SetBatchStatusAsync(batchId, BatchStatus.Cancelled);
            _logger.LogInformation("Batch {BatchId} cancelled by {User}", batchId, User.Identity?.Name);
            return Ok(new { batchId, status = BatchStatus.Cancelled });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling batch {BatchId}", batchId);
            return StatusCode(500, "Error cancelling batch");
        }
    }

    // POST: api/SendNudge/PauseBatch/{batchId}
    [HttpPost("PauseBatch/{batchId}")]
    public async Task<IActionResult> PauseBatch(string batchId, [FromQuery] bool resume = false)
    {
        try
        {
            var status = resume ? BatchStatus.Running : BatchStatus.Paused;
            await _templateService.SetBatchStatusAsync(batchId, status);
            return Ok(new { batchId, status });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating batch {BatchId}", batchId);
            return StatusCode(500, "Error updating batch");
        }
    }
}


public class CreateBatchAndSendRequest
{
    public string BatchName { get; set; } = null!;
    public string TemplateId { get; set; } = null!;

    /// <summary>
    /// Direct list of recipient UPNs
    /// </summary>
    public List<string>? RecipientUpns { get; set; }

    /// <summary>
    /// Smart group IDs to resolve and include as recipients (requires Copilot Connected mode)
    /// </summary>
    public List<string>? SmartGroupIds { get; set; }

    /// <summary>
    /// Earliest UTC time this batch may start sending. Null sends as soon as expansion completes.
    /// </summary>
    public DateTime? ScheduledSendUtc { get; set; }
}
