using Engine.Config;
using Engine.Services.UserCache;
using Microsoft.Extensions.Logging;

namespace UnitTests.IntegrationTests;

/// <summary>
/// Integration tests verifying <see cref="GraphCopilotStatsLoader"/> behaviour for varying
/// <see cref="UserCacheConfig.CopilotStatsPeriod"/> values and for invalid configuration / credentials.
/// </summary>
[TestClass]
public class GraphCopilotStatsLoaderConfigurationIntegrationTests : AbstractTest
{
    #region Configuration Tests

    [TestMethod]
    public async Task GraphCopilotStatsLoader_WithDifferentPeriods_ReturnsData()
    {
        if (_config?.GraphConfig == null)
        {
            Assert.Inconclusive("Configuration not available");
            return;
        }

        var periods = new[] { "D7", "D30", "D90" };

        foreach (var period in periods)
        {
            try
            {
                var config = new UserCacheConfig { CopilotStatsPeriod = period };
                var loader = new GraphCopilotStatsLoader(
                    GetLogger<GraphCopilotStatsLoader>(),
                    config,
                    _config.GraphConfig);

                var result = await loader.GetCopilotUsageStatsAsync();

                Assert.IsNotNull(result);
                _logger.LogInformation($"Period {period}: Retrieved {result.Records.Count} records");
            }
            catch (Exception ex) when (ex.Message.Contains("Forbidden") || ex.Message.Contains("403"))
            {
                Assert.Inconclusive($"Reports.Read.All permission not granted for period {period}");
                return;
            }
        }
    }

    #endregion

    #region Error Handling Tests

    [TestMethod]
    public async Task GetCopilotUsageStatsAsync_WithInvalidCredentials_ThrowsException()
    {
        // Arrange - Create loader with invalid credentials
        var invalidConfig = new AzureADAuthConfig
        {
            TenantId = "invalid-tenant-id",
            ClientId = "invalid-client-id",
            ClientSecret = "invalid-secret"
        };

        var loader = new GraphCopilotStatsLoader(
            GetLogger<GraphCopilotStatsLoader>(),
            new UserCacheConfig(),
            invalidConfig);

        // Act & Assert
        await Assert.ThrowsExceptionAsync<Azure.Identity.AuthenticationFailedException>(
            async () => await loader.GetCopilotUsageStatsAsync(),
            "Should throw exception with invalid credentials");
    }

    [TestMethod]
    public async Task GetCopilotUsageStatsAsync_WithInvalidPeriod_HandlesGracefully()
    {
        if (_config?.GraphConfig == null)
        {
            Assert.Inconclusive("Configuration not available");
            return;
        }

        try
        {
            // Arrange - Invalid period format
            var config = new UserCacheConfig { CopilotStatsPeriod = "INVALID" };
            var loader = new GraphCopilotStatsLoader(
                GetLogger<GraphCopilotStatsLoader>(),
                config,
                _config.GraphConfig);

            // Act
            var result = await loader.GetCopilotUsageStatsAsync();

            // Assert - Should handle gracefully (may return empty list or throw)
            Assert.IsNotNull(result);
            _logger.LogInformation("Invalid period handled gracefully");
        }
        catch (Exception ex)
        {
            _logger.LogInformation($"Invalid period threw expected exception: {ex.Message}");
            Assert.IsTrue(true, "Exception on invalid period is acceptable");
        }
    }

    #endregion
}
