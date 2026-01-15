using Common.Engine.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Server.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class MessageTemplateController : ControllerBase
{
    private readonly MessageTemplateService _templateService;
    private readonly ILogger<MessageTemplateController> _logger;

    public MessageTemplateController(MessageTemplateService templateService, ILogger<MessageTemplateController> logger)
    {
        _templateService = templateService;
        _logger = logger;
    }

    // GET: api/MessageTemplate/GetAll
    [HttpGet(nameof(GetAll))]
    public async Task<IActionResult> GetAll()
    {
        _logger.LogInformation("Getting all message templates");
        var templates = await _templateService.GetAllTemplates();
        return Ok(templates);
    }

    // GET: api/MessageTemplate/Get/{id}
    [HttpGet("Get/{id}")]
    public async Task<IActionResult> Get(string id)
    {
        _logger.LogInformation($"Getting template {id}");
        var template = await _templateService.GetTemplate(id);
        
        if (template == null)
        {
            return NotFound();
        }

        return Ok(template);
    }

    // GET: api/MessageTemplate/GetJson/{id}
    [HttpGet("GetJson/{id}")]
    public async Task<IActionResult> GetJson(string id)
    {
        _logger.LogInformation($"Getting JSON for template {id}");
        try
        {
            var json = await _templateService.GetTemplateJson(id);
            return Ok(new { json });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    // POST: api/MessageTemplate/Create
    [HttpPost(nameof(Create))]
    public async Task<IActionResult> Create([FromBody] CreateTemplateRequest request)
    {
        _logger.LogInformation($"Creating template '{request.TemplateName}'");
        
        if (string.IsNullOrWhiteSpace(request.TemplateName) || string.IsNullOrWhiteSpace(request.JsonPayload))
        {
            return BadRequest("TemplateName and JsonPayload are required");
        }

        // Get user principal name from claims
        var upn = User.Identity?.Name ?? "unknown";

        try
        {
            var template = await _templateService.CreateTemplate(request.TemplateName, request.JsonPayload, upn);
            return Ok(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating template");
            return StatusCode(500, "Error creating template");
        }
    }

    // PUT: api/MessageTemplate/Update/{id}
    [HttpPut("Update/{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateTemplateRequest request)
    {
        _logger.LogInformation($"Updating template {id}");
        
        if (string.IsNullOrWhiteSpace(request.TemplateName) || string.IsNullOrWhiteSpace(request.JsonPayload))
        {
            return BadRequest("TemplateName and JsonPayload are required");
        }

        try
        {
            var template = await _templateService.UpdateTemplate(id, request.TemplateName, request.JsonPayload);
            return Ok(template);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating template");
            return StatusCode(500, "Error updating template");
        }
    }

    // DELETE: api/MessageTemplate/Delete/{id}
    [HttpDelete("Delete/{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        _logger.LogInformation($"Deleting template {id}");
        
        try
        {
            await _templateService.DeleteTemplate(id);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting template");
            return StatusCode(500, "Error deleting template");
        }
    }

    // GET: api/MessageTemplate/GetLogs
    [HttpGet(nameof(GetLogs))]
    public async Task<IActionResult> GetLogs()
    {
        _logger.LogInformation("Getting all message logs");
        var logs = await _templateService.GetAllMessageLogs();
        return Ok(logs);
    }

    // GET: api/MessageTemplate/GetLogsByTemplate/{templateId}
    [HttpGet("GetLogsByTemplate/{templateId}")]
    public async Task<IActionResult> GetLogsByTemplate(string templateId)
    {
        _logger.LogInformation($"Getting logs for template {templateId}");
        var logs = await _templateService.GetMessageLogsByTemplate(templateId);
        return Ok(logs);
    }

    // GET: api/MessageTemplate/GetBatches
    [HttpGet(nameof(GetBatches))]
    public async Task<IActionResult> GetBatches()
    {
        _logger.LogInformation("Getting all message batches");
        var batches = await _templateService.GetAllBatches();
        return Ok(batches);
    }

    // GET: api/MessageTemplate/GetBatch/{id}
    [HttpGet("GetBatch/{id}")]
    public async Task<IActionResult> GetBatch(string id)
    {
        _logger.LogInformation($"Getting batch {id}");
        var batch = await _templateService.GetBatch(id);
        
        if (batch == null)
        {
            return NotFound();
        }

        return Ok(batch);
    }

    // GET: api/MessageTemplate/GetLogsByBatch/{batchId}
    [HttpGet("GetLogsByBatch/{batchId}")]
    public async Task<IActionResult> GetLogsByBatch(string batchId)
    {
        _logger.LogInformation($"Getting logs for batch {batchId}");
        var logs = await _templateService.GetMessageLogsByBatch(batchId);
        return Ok(logs);
    }

    // DELETE: api/MessageTemplate/DeleteBatch/{id}
    [HttpDelete("DeleteBatch/{id}")]
    public async Task<IActionResult> DeleteBatch(string id)
    {
        _logger.LogInformation($"Deleting batch {id}");
        
        try
        {
            await _templateService.DeleteBatch(id);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting batch");
            return StatusCode(500, "Error deleting batch");
        }
    }
}

public class CreateTemplateRequest
{
    public string TemplateName { get; set; } = null!;
    public string JsonPayload { get; set; } = null!;
}

public class UpdateTemplateRequest
{
    public string TemplateName { get; set; } = null!;
    public string JsonPayload { get; set; } = null!;
}
