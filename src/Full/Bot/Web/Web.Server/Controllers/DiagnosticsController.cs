using Common.Engine.Config;
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
    private readonly AppConfig _appConfig;
    private readonly ILogger<DiagnosticsController> _logger;

    public DiagnosticsController(
        GraphService graphService, 
        BatchQueueService queueService,
        AppConfig appConfig,
        ILogger<DiagnosticsController> logger)
    {
        _graphService = graphService;
        _queueService = queueService;
        _appConfig = appConfig;
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

    // GET: api/Diagnostics/StorageConfig
    [HttpGet(nameof(StorageConfig))]
    public IActionResult StorageConfig()
    {
        _logger.LogInformation("Getting storage configuration");
        
        try
        {
            var storageConfig = _appConfig.StorageAuthConfig;
            
            // Check if we're using fallback to legacy ConnectionStrings.Storage
            var isUsingLegacy = storageConfig == null || 
                                (!storageConfig.UseRBAC && string.IsNullOrEmpty(storageConfig.ConnectionString));
            
            if (isUsingLegacy)
            {
                // Using legacy configuration
                return Ok(new
                {
                    useRBAC = false,
                    storageAccountName = (string?)null,
                    hasConnectionString = !string.IsNullOrEmpty(_appConfig.ConnectionStrings?.Storage),
                    hasOverrideCredentials = false,
                    overrideTenantId = (string?)null,
                    overrideClientId = (string?)null,
                    effectiveAuthMethod = "Connection String (Legacy)",
                    configurationSource = "ConnectionStrings:Storage"
                });
            }
            
            // Using StorageAuthConfig
            var authMethod = storageConfig!.UseRBAC
                ? (storageConfig.RBACOverrideCredentials != null 
                    ? "RBAC with Service Principal" 
                    : "RBAC with DefaultAzureCredential")
                : "Connection String";
            
            return Ok(new
            {
                useRBAC = storageConfig.UseRBAC,
                storageAccountName = storageConfig.StorageAccountName,
                hasConnectionString = !string.IsNullOrEmpty(storageConfig.ConnectionString),
                hasOverrideCredentials = storageConfig.RBACOverrideCredentials != null,
                overrideTenantId = storageConfig.RBACOverrideCredentials?.TenantId,
                overrideClientId = storageConfig.RBACOverrideCredentials?.ClientId,
                effectiveAuthMethod = authMethod,
                configurationSource = "StorageAuthConfig"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting storage configuration");
            return Ok(new
            {
                useRBAC = false,
                storageAccountName = (string?)null,
                hasConnectionString = false,
                hasOverrideCredentials = false,
                overrideTenantId = (string?)null,
                overrideClientId = (string?)null,
                effectiveAuthMethod = "Error",
                configurationSource = "Unknown",
                error = ex.Message
            });
        }
    }
}
