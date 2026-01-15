using Common.Engine;
using Common.Engine.Config;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Server.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class SettingsController : ControllerBase
{
    private readonly SettingsStorageManager _settingsManager;
    private readonly TeamsAppConfig _config;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(
        SettingsStorageManager settingsManager,
        TeamsAppConfig config,
        ILogger<SettingsController> logger)
    {
        _settingsManager = settingsManager;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Get current application settings
    /// </summary>
    [HttpGet("Get")]
    public async Task<IActionResult> Get()
    {
        try
        {
            var settings = await _settingsManager.GetSettings();
            return Ok(new AppSettingsDto
            {
                FollowUpChatSystemPrompt = settings.FollowUpChatSystemPrompt,
                DefaultFollowUpChatSystemPrompt = SettingsStorageManager.DefaultFollowUpChatSystemPrompt,
                LastModifiedDate = settings.LastModifiedDate,
                LastModifiedByUpn = settings.LastModifiedByUpn
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting settings");
            return StatusCode(500, "Error getting settings");
        }
    }

    /// <summary>
    /// Update application settings
    /// </summary>
    [HttpPut("Update")]
    public async Task<IActionResult> Update([FromBody] UpdateSettingsRequest request)
    {
        try
        {
            var userUpn = User.Identity?.Name ?? "unknown";
            
            // Allow null/empty to reset to default
            var settings = await _settingsManager.UpdateSettings(
                string.IsNullOrWhiteSpace(request.FollowUpChatSystemPrompt) ? null : request.FollowUpChatSystemPrompt.Trim(),
                userUpn);
            
            return Ok(new AppSettingsDto
            {
                FollowUpChatSystemPrompt = settings.FollowUpChatSystemPrompt,
                DefaultFollowUpChatSystemPrompt = SettingsStorageManager.DefaultFollowUpChatSystemPrompt,
                LastModifiedDate = settings.LastModifiedDate,
                LastModifiedByUpn = settings.LastModifiedByUpn
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating settings");
            return StatusCode(500, "Error updating settings");
        }
    }

    /// <summary>
    /// Reset settings to defaults
    /// </summary>
    [HttpPost("ResetToDefaults")]
    public async Task<IActionResult> ResetToDefaults()
    {
        try
        {
            var userUpn = User.Identity?.Name ?? "unknown";
            
            var settings = await _settingsManager.UpdateSettings(null, userUpn);
            
            return Ok(new AppSettingsDto
            {
                FollowUpChatSystemPrompt = null,
                DefaultFollowUpChatSystemPrompt = SettingsStorageManager.DefaultFollowUpChatSystemPrompt,
                LastModifiedDate = settings.LastModifiedDate,
                LastModifiedByUpn = settings.LastModifiedByUpn
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting settings");
            return StatusCode(500, "Error resetting settings");
        }
    }
}

#region Request/Response DTOs

public class AppSettingsDto
{
    /// <summary>
    /// Custom follow-up chat system prompt (null means default is used)
    /// </summary>
    public string? FollowUpChatSystemPrompt { get; set; }
    
    /// <summary>
    /// The default follow-up chat system prompt for reference
    /// </summary>
    public string DefaultFollowUpChatSystemPrompt { get; set; } = null!;
    
    /// <summary>
    /// When settings were last modified
    /// </summary>
    public DateTime? LastModifiedDate { get; set; }
    
    /// <summary>
    /// Who last modified the settings
    /// </summary>
    public string? LastModifiedByUpn { get; set; }
}

public class UpdateSettingsRequest
{
    /// <summary>
    /// Custom follow-up chat system prompt. Pass null or empty to use default.
    /// </summary>
    public string? FollowUpChatSystemPrompt { get; set; }
}

#endregion
