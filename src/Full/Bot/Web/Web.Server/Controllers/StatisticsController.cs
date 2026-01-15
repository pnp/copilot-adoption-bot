using Common.Engine.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Server.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class StatisticsController : ControllerBase
{
    private readonly StatisticsService _statisticsService;
    private readonly ILogger<StatisticsController> _logger;

    public StatisticsController(StatisticsService statisticsService, ILogger<StatisticsController> logger)
    {
        _statisticsService = statisticsService;
        _logger = logger;
    }

    // GET: api/Statistics/GetMessageStatusStats
    [HttpGet(nameof(GetMessageStatusStats))]
    public async Task<IActionResult> GetMessageStatusStats()
    {
        _logger.LogInformation("Getting message status statistics");
        
        try
        {
            var stats = await _statisticsService.GetMessageStatusStats();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting message status statistics");
            return StatusCode(500, "Error getting message status statistics");
        }
    }

    // GET: api/Statistics/GetUserCoverageStats
    [HttpGet(nameof(GetUserCoverageStats))]
    public async Task<IActionResult> GetUserCoverageStats()
    {
        _logger.LogInformation("Getting user coverage statistics");
        
        try
        {
            var stats = await _statisticsService.GetUserCoverageStats();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user coverage statistics");
            return StatusCode(500, "Error getting user coverage statistics");
        }
    }
}
