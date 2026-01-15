using Common.Engine.Services.UserCache;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Server.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class UserCacheController : ControllerBase
{
    private readonly IUserCacheManager _cacheManager;
    private readonly ILogger<UserCacheController> _logger;

    public UserCacheController(
        IUserCacheManager cacheManager,
        ILogger<UserCacheController> logger)
    {
        _cacheManager = cacheManager;
        _logger = logger;
    }

    /// <summary>
    /// Get cached users (without triggering auto-sync)
    /// </summary>
    [HttpGet("GetCachedUsers")]
    public async Task<IActionResult> GetCachedUsers()
    {
        try
        {
            var users = await _cacheManager.GetAllCachedUsersAsync(forceRefresh: false, skipAutoSync: true);
            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cached users");
            return StatusCode(500, "Error getting cached users");
        }
    }

    /// <summary>
    /// Clear the user cache and force a full resync on next access
    /// </summary>
    [HttpPost("Clear")]
    public async Task<IActionResult> ClearCache()
    {
        try
        {
            _logger.LogInformation("User cache clear requested by {User}", User.Identity?.Name);
            await _cacheManager.ClearCacheAsync();
            return Ok(new { message = "User cache cleared successfully. A full sync will occur on next access." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing user cache");
            return StatusCode(500, "Error clearing user cache");
        }
    }

    /// <summary>
    /// Force synchronization of users from Microsoft Graph
    /// </summary>
    [HttpPost("Sync")]
    public async Task<IActionResult> SyncUsers()
    {
        try
        {
            _logger.LogInformation("User cache sync requested by {User}", User.Identity?.Name);
            await _cacheManager.SyncUsersAsync();
            return Ok(new { message = "User cache synchronized successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing user cache");
            return StatusCode(500, "Error syncing user cache");
        }
    }

    /// <summary>
    /// Update Copilot usage statistics for all cached users
    /// </summary>
    [HttpPost("UpdateCopilotStats")]
    public async Task<IActionResult> UpdateCopilotStats()
    {
        try
        {
            _logger.LogInformation("Copilot stats update requested by {User}", User.Identity?.Name);
            await _cacheManager.UpdateCopilotStatsAsync();
            
            // Get the updated metadata to return status information
            var metadata = await _cacheManager.GetSyncMetadataAsync();
            
            return Ok(new 
            { 
                message = "Copilot statistics updated successfully for cached users.",
                lastUpdate = metadata.LastCopilotStatsUpdate,
                success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Copilot stats");
            
            // Return detailed error information
            return StatusCode(500, new
            {
                message = "Error updating Copilot stats",
                error = ex.Message,
                success = false
            });
        }
    }

    /// <summary>
    /// Clear Copilot statistics metadata to force a fresh update on next refresh
    /// </summary>
    [HttpPost("ClearCopilotStats")]
    public async Task<IActionResult> ClearCopilotStats()
    {
        try
        {
            _logger.LogInformation("Copilot stats clear requested by {User}", User.Identity?.Name);
            
            // Get current metadata and clear the last update timestamp
            var metadata = await _cacheManager.GetSyncMetadataAsync();
            metadata.LastCopilotStatsUpdate = null;
            await _cacheManager.UpdateSyncMetadataAsync(metadata);
            
            return Ok(new { message = "Copilot statistics cleared. Next update will fetch fresh data." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing Copilot stats");
            return StatusCode(500, $"Error clearing Copilot stats: {ex.Message}");
        }
    }
}
