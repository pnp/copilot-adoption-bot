using Engine.Config;
using Engine.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Server.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class SmartGroupController : ControllerBase
{
    private readonly SmartGroupService _smartGroupService;
    private readonly SmartGroupResolutionJobTracker _jobTracker;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TeamsAppConfig _config;
    private readonly ILogger<SmartGroupController> _logger;

    public SmartGroupController(
        SmartGroupService smartGroupService,
        SmartGroupResolutionJobTracker jobTracker,
        IServiceScopeFactory scopeFactory,
        TeamsAppConfig config,
        ILogger<SmartGroupController> logger)
    {
        _smartGroupService = smartGroupService;
        _jobTracker = jobTracker;
        _scopeFactory = scopeFactory;
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
    /// Per-group cache status (without triggering resolution). Use this for the UI
    /// to render staleness badges without paying the cold-path cost.
    /// </summary>
    [HttpGet("Status")]
    public async Task<IActionResult> GetStatus()
    {
        if (!_config.IsCopilotConnectedEnabled)
        {
            return BadRequest("Copilot Connected mode is not enabled. Configure AI Foundry to use smart groups.");
        }

        try
        {
            var groups = await _smartGroupService.GetAllSmartGroups();
            var ttl = SmartGroupService.ResolutionCacheTtl;
            var now = DateTime.UtcNow;

            var items = groups.Select(g => new SmartGroupStatusItemDto
            {
                Id = g.Id,
                Name = g.Name,
                LastResolvedDate = g.LastResolvedDate,
                LastResolvedMemberCount = g.LastResolvedMemberCount,
                IsResolved = g.LastResolvedDate.HasValue,
                IsStale = !g.LastResolvedDate.HasValue || (now - g.LastResolvedDate.Value) >= ttl
            }).ToList();

            return Ok(new SmartGroupStatusDto
            {
                TtlSeconds = (int)ttl.TotalSeconds,
                Groups = items
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting smart group status");
            return StatusCode(500, "Error getting smart group status");
        }
    }

    /// <summary>
    /// Kick off smart group resolution asynchronously. Returns 202 + a job id the
    /// caller can poll via <see cref="ResolveStatus"/>. Avoids long-running HTTP
    /// requests for the AI-Foundry path which can take 30s+ on a cold cache.
    /// </summary>
    [HttpPost("ResolveMembersAsync/{id}")]
    public async Task<IActionResult> ResolveMembersAsync(string id, [FromQuery] bool forceRefresh = false)
    {
        if (!_config.IsCopilotConnectedEnabled)
        {
            return BadRequest("Copilot Connected mode is not enabled.");
        }

        var existing = await _smartGroupService.GetSmartGroup(id);
        if (existing == null)
        {
            return NotFound($"Smart group {id} not found");
        }

        var job = _jobTracker.CreateJob(id);

        // Fire-and-forget on a worker thread with its own DI scope. SmartGroupService is scoped
        // so we cannot reuse the request's scope after returning the response.
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<SmartGroupService>();

            try
            {
                _jobTracker.MarkRunning(job.JobId, forceRefresh ? "Resolving members via AI" : "Loading members");
                var result = await svc.ResolveSmartGroupMembers(id, forceRefresh);
                _jobTracker.MarkSucceeded(job.JobId, result.Members.Count, result.FromCache);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background resolution of smart group {GroupId} failed (job {JobId})", id, job.JobId);
                _jobTracker.MarkFailed(job.JobId, ex.Message);
            }
        });

        return Accepted(new ResolveJobAcceptedDto
        {
            JobId = job.JobId,
            SmartGroupId = id,
            StatusUrl = Url.Action(nameof(ResolveStatus), new { jobId = job.JobId }) ?? string.Empty
        });
    }

    /// <summary>
    /// Poll the state of an async resolution job kicked off by <see cref="ResolveMembersAsync"/>.
    /// </summary>
    [HttpGet("ResolveStatus/{jobId}")]
    public async Task<IActionResult> ResolveStatus(string jobId)
    {
        var job = _jobTracker.GetJob(jobId);
        if (job == null)
        {
            return NotFound(new { message = "Job not found or has expired", jobId });
        }

        // If the job is done and the caller wants the result, serve the cached members too.
        List<SmartGroupMemberDto>? members = null;
        DateTime? resolvedAt = null;
        if (job.State == SmartGroupResolutionJobState.Succeeded)
        {
            try
            {
                var result = await _smartGroupService.ResolveSmartGroupMembers(job.SmartGroupId, forceRefresh: false);
                members = result.Members;
                resolvedAt = result.ResolvedAt;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load cached members for job {JobId}", jobId);
            }
        }

        return Ok(new ResolveJobStatusDto
        {
            JobId = job.JobId,
            SmartGroupId = job.SmartGroupId,
            State = job.State.ToString(),
            CurrentStep = job.CurrentStep,
            Error = job.Error,
            MemberCount = job.MemberCount,
            FromCache = job.FromCache,
            CreatedAt = job.CreatedAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            ResolvedAt = resolvedAt,
            Members = members
        });
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

public class SmartGroupStatusItemDto
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public DateTime? LastResolvedDate { get; set; }
    public int? LastResolvedMemberCount { get; set; }
    public bool IsResolved { get; set; }
    public bool IsStale { get; set; }
}

public class SmartGroupStatusDto
{
    public int TtlSeconds { get; set; }
    public List<SmartGroupStatusItemDto> Groups { get; set; } = [];
}

public class ResolveJobAcceptedDto
{
    public string JobId { get; set; } = null!;
    public string SmartGroupId { get; set; } = null!;
    public string StatusUrl { get; set; } = null!;
}

public class ResolveJobStatusDto
{
    public string JobId { get; set; } = null!;
    public string SmartGroupId { get; set; } = null!;
    public string State { get; set; } = null!;
    public string? CurrentStep { get; set; }
    public string? Error { get; set; }
    public int? MemberCount { get; set; }
    public bool FromCache { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public List<SmartGroupMemberDto>? Members { get; set; }
}

#endregion
