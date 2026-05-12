using Engine.Config;
using Engine.Services.UserCache;
using Microsoft.Extensions.Logging;

namespace UnitTests.IntegrationTests;

/// <summary>
/// Base class for <see cref="GraphCopilotStatsLoader"/> integration tests.
/// Centralises construction of a default loader configured for a 30-day reporting period.
/// </summary>
public abstract class GraphCopilotStatsLoaderIntegrationTestBase : AbstractTest
{
    protected GraphCopilotStatsLoader? _loader;

    [TestInitialize]
    public void BaseInitialize()
    {
        try
        {
            var cacheConfig = new UserCacheConfig
            {
                CopilotStatsPeriod = "D30"
            };

            _loader = new GraphCopilotStatsLoader(
                GetLogger<GraphCopilotStatsLoader>(),
                cacheConfig,
                _config.GraphConfig);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to initialize GraphCopilotStatsLoader: {ex.Message}");
            _logger.LogWarning("Tests will be skipped if Graph credentials or Reports.Read.All permission are not configured");
        }
    }
}
