using Common.Engine.Config;
using Common.Engine.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Server.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class SmartGroupController : ControllerBase
{
    private readonly SmartGroupService _smartGroupService;
    private readonly TeamsAppConfig _config;
    private readonly ILogger<SmartGroupController> _logger;

    public SmartGroupController(
        SmartGroupService smartGroupService,
        TeamsAppConfig config,
        ILogger<SmartGroupController> logger)
    {
        _smartGroupService = smartGroupService;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Get Copilot Connected status (whether AI Foundry is configured)
    /// </summary>
    [HttpGet("CopilotConnectedStatus")]
    public IActionResult GetCopilotConnectedStatus()
    {
        return Ok(new CopilotConnectedStatusDto
        {
            IsEnabled = _config.IsCopilotConnectedEnabled,
            HasAIFoundryConfig = _config.AIFoundryConfig != null
        });
    }

    /// <summary>
    /// Get all smart groups
    /// </summary>
    [HttpGet("GetAll")]
    public async Task<IActionResult> GetAll()
    {
        if (!_config.IsCopilotConnectedEnabled)
        {
            return BadRequest("Copilot Connected mode is not enabled. Configure AI Foundry to use smart groups.");
        }

        try
        {
            var groups = await _smartGroupService.GetAllSmartGroups();
            return Ok(groups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting smart groups");
            return StatusCode(500, "Error getting smart groups");
        }
    }

    /// <summary>
    /// Get a specific smart group
    /// </summary>
    [HttpGet("Get/{id}")]
    public async Task<IActionResult> Get(string id)
    {
        if (!_config.IsCopilotConnectedEnabled)
        {
            return BadRequest("Copilot Connected mode is not enabled.");
        }

        try
        {
            var group = await _smartGroupService.GetSmartGroup(id);
            if (group == null)
            {
                return NotFound($"Smart group {id} not found");
            }
            return Ok(group);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting smart group {id}");
            return StatusCode(500, "Error getting smart group");
        }
    }

    /// <summary>
    /// Create a new smart group
    /// </summary>
    [HttpPost("Create")]
    public async Task<IActionResult> Create([FromBody] CreateSmartGroupRequest request)
    {
        if (!_config.IsCopilotConnectedEnabled)
        {
            return BadRequest("Copilot Connected mode is not enabled. Configure AI Foundry to use smart groups.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Name is required");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest("Description is required");
        }

        try
        {
            var senderUpn = User.Identity?.Name ?? "unknown";
            var group = await _smartGroupService.CreateSmartGroup(request.Name, request.Description, senderUpn);
            return Ok(group);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating smart group");
            return StatusCode(500, "Error creating smart group");
        }
    }

    /// <summary>
    /// Update a smart group
    /// </summary>
    [HttpPut("Update/{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateSmartGroupRequest request)
    {
        if (!_config.IsCopilotConnectedEnabled)
        {
            return BadRequest("Copilot Connected mode is not enabled.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Name is required");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest("Description is required");
        }

        try
        {
            var group = await _smartGroupService.UpdateSmartGroup(id, request.Name, request.Description);
            return Ok(group);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating smart group {id}");
            return StatusCode(500, "Error updating smart group");
        }
    }

    /// <summary>
    /// Delete a smart group
    /// </summary>
    [HttpDelete("Delete/{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!_config.IsCopilotConnectedEnabled)
        {
            return BadRequest("Copilot Connected mode is not enabled.");
        }

        try
        {
            await _smartGroupService.DeleteSmartGroup(id);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting smart group {id}");
            return StatusCode(500, "Error deleting smart group");
        }
    }

    /// <summary>
    /// Resolve smart group members using AI
    /// </summary>
    [HttpPost("ResolveMembers/{id}")]
    public async Task<IActionResult> ResolveMembers(string id, [FromQuery] bool forceRefresh = false)
    {
        if (!_config.IsCopilotConnectedEnabled)
        {
            return BadRequest("Copilot Connected mode is not enabled.");
        }

        try
        {
            var result = await _smartGroupService.ResolveSmartGroupMembers(id, forceRefresh);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error resolving members for smart group {id}");
            return StatusCode(500, "Error resolving smart group members");
        }
    }

    /// <summary>
    /// Preview smart group resolution (without caching)
    /// </summary>
    [HttpPost("Preview")]
    public async Task<IActionResult> Preview([FromBody] PreviewSmartGroupRequest request)
    {
        if (!_config.IsCopilotConnectedEnabled)
        {
            return BadRequest("Copilot Connected mode is not enabled.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest("Description is required");
        }

        try
        {
            var members = await _smartGroupService.PreviewSmartGroupMembers(request.Description, request.MaxUsers);
            return Ok(new { members, count = members.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error previewing smart group");
            return StatusCode(500, "Error previewing smart group");
        }
    }

    /// <summary>
    /// Get UPNs for a smart group (for sending nudges)
    /// </summary>
    [HttpGet("GetUpns/{id}")]
    public async Task<IActionResult> GetUpns(string id)
    {
        if (!_config.IsCopilotConnectedEnabled)
        {
            return BadRequest("Copilot Connected mode is not enabled.");
        }

        try
        {
            var upns = await _smartGroupService.GetSmartGroupUpns(id);
            return Ok(new { upns, count = upns.Count });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting UPNs for smart group {id}");
            return StatusCode(500, "Error getting smart group UPNs");
        }
    }
}

#region Request/Response DTOs

public class CopilotConnectedStatusDto
{
    public bool IsEnabled { get; set; }
    public bool HasAIFoundryConfig { get; set; }
}

public class CreateSmartGroupRequest
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
}

public class UpdateSmartGroupRequest
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
}

public class PreviewSmartGroupRequest
{
    public string Description { get; set; } = null!;
    public int MaxUsers { get; set; } = 100;
}

#endregion
