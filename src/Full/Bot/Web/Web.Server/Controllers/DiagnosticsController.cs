using Common.Engine.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Server.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class DiagnosticsController : ControllerBase
{
    private readonly GraphService _graphService;
    private readonly BatchQueueService _queueService;
    private readonly ILogger<DiagnosticsController> _logger;

    public DiagnosticsController(
        GraphService graphService, 
        BatchQueueService queueService,
        ILogger<DiagnosticsController> logger)
    {
        _graphService = graphService;
        _queueService = queueService;
        _logger = logger;
    }

    // GET: api/Diagnostics/TestGraphConnection
    [HttpGet(nameof(TestGraphConnection))]
    public async Task<IActionResult> TestGraphConnection()
    {
        _logger.LogInformation("Testing Graph API connection");
        
        try
        {
            var userCount = await _graphService.GetTotalUserCount();
            return Ok(new
            {
                success = true,
                message = $"Successfully connected to Graph API",
                userCount = userCount,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Graph connection");
            return Ok(new
            {
                success = false,
                message = ex.Message,
                details = ex.InnerException?.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    // GET: api/Diagnostics/QueueStatus
    [HttpGet(nameof(QueueStatus))]
    public async Task<IActionResult> QueueStatus()
    {
        _logger.LogInformation("Checking queue status");
        
        try
        {
            var queueLength = await _queueService.GetQueueLengthAsync();
            return Ok(new
            {
                success = true,
                queueLength = queueLength,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking queue status");
            return Ok(new
            {
                success = false,
                message = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }
}
