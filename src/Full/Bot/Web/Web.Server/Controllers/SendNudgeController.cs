using Common.Engine.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;

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
                    var trimmedLine = line.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmedLine))
                    {
                        // Handle CSV - take first column if comma-separated
                        var upn = trimmedLine.Split(',')[0].Trim();
                        if (!string.IsNullOrWhiteSpace(upn))
                        {
                            upns.Add(upn);
                        }
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

            // Collect all recipient UPNs
            var allRecipientUpns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Add directly specified UPNs
            if (hasRecipientUpns)
            {
                foreach (var upn in request.RecipientUpns!)
                {
                    allRecipientUpns.Add(upn);
                }
            }

            // Resolve smart groups and add their members
            if (hasSmartGroups)
            {
                var failedSmartGroups = new List<string>();
                foreach (var smartGroupId in request.SmartGroupIds!)
                {
                    try
                    {
                        var smartGroupUpns = await _smartGroupService.GetSmartGroupUpns(smartGroupId);
                        foreach (var upn in smartGroupUpns)
                        {
                            allRecipientUpns.Add(upn);
                        }
                        _logger.LogInformation($"Resolved smart group {smartGroupId} to {smartGroupUpns.Count} UPNs");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to resolve smart group {smartGroupId}: {ex.Message}");
                        failedSmartGroups.Add(smartGroupId);
                    }
                }

                // If all smart groups failed and there are no direct recipients, return a more informative error
                if (failedSmartGroups.Count > 0 && allRecipientUpns.Count == 0)
                {
                    return BadRequest($"Failed to resolve {failedSmartGroups.Count} smart group(s). " +
                        "This may be because Copilot Connected mode is not properly configured or the groups have not been resolved yet. " +
                        "Try resolving the smart groups first by viewing their members in the Smart Groups page.");
                }
            }

            if (allRecipientUpns.Count == 0)
            {
                return BadRequest("No valid recipients found after resolving smart groups");
            }

            // Create batch
            var batch = await _templateService.CreateBatch(request.BatchName, request.TemplateId, senderUpn);

            // Create message log entries for each recipient
            var logs = await _templateService.LogBatchMessages(batch.Id, allRecipientUpns.ToList());

            _logger.LogInformation($"Created batch {batch.Id} with {logs.Count} messages (from {request.RecipientUpns?.Count ?? 0} direct UPNs and {request.SmartGroupIds?.Count ?? 0} smart groups)");

            return Ok(new
            {
                batch,
                messageCount = logs.Count,
                logs,
                smartGroupsResolved = request.SmartGroupIds?.Count ?? 0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating batch and sending messages");
            return StatusCode(500, "Error creating batch and sending messages");
        }
    }

    // PUT: api/SendNudge/UpdateLogStatus/{logId}
    [HttpPut("UpdateLogStatus/{logId}")]
    public async Task<IActionResult> UpdateLogStatus(string logId, [FromBody] UpdateLogStatusRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Status))
        {
            return BadRequest("Status is required");
        }

        try
        {
            await _templateService.UpdateMessageLogStatus(logId, request.Status, request.LastError);
            _logger.LogInformation($"Updated message log {logId} to status {request.Status}");
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating message log status");
            return StatusCode(500, "Error updating message log status");
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
}

public class UpdateLogStatusRequest
{
    public string Status { get; set; } = null!;
    public string? LastError { get; set; }
}
