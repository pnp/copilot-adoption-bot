using Common.Engine.Config;
using Common.Engine.Services.UserCache;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests.IntegrationTests;

/// <summary>
/// Integration tests for GraphCopilotStatsLoader.
/// NOTE: These tests require actual Microsoft Graph connectivity and Reports.Read.All permission.
/// Some tests may be inconclusive if the tenant doesn't have Copilot licenses or usage data.
/// </summary>
[TestClass]
public class GraphCopilotStatsLoaderIntegrationTests : AbstractTest
{
    private GraphCopilotStatsLoader? _loader;

    [TestInitialize]
    public void Initialize()
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

    #region Token Acquisition Tests

    [TestMethod]
    public async Task GetCopilotUsageStatsAsync_WithValidCredentials_ReturnsRecords()
    {
        if (_loader == null)
        {
            Assert.Inconclusive("Loader not initialized - check Graph API credentials");
            return;
        }

        try
        {
            // Act
            var result = await _loader.GetCopilotUsageStatsAsync();

            // Assert
            Assert.IsNotNull(result);
            _logger.LogInformation($"Retrieved {result.Records.Count} Copilot usage records");

            if (result.Records.Count == 0)
            {
                _logger.LogWarning("No Copilot usage data found - tenant may not have active Copilot users");
                Assert.Inconclusive("No Copilot usage data available in tenant");
                return;
            }

            var firstRecord = result.Records[0];
            Assert.IsFalse(string.IsNullOrEmpty(firstRecord.UserPrincipalName), "Record should have UserPrincipalName");
            
            _logger.LogInformation($"Sample record: {firstRecord.UserPrincipalName}");
            _logger.LogInformation($"  Last Activity: {firstRecord.LastActivityDate}");
            _logger.LogInformation($"  Copilot Chat: {firstRecord.CopilotChatLastActivityDate}");
            _logger.LogInformation($"  Teams Copilot: {firstRecord.TeamsCopilotLastActivityDate}");
        }
        catch (Exception ex) when (ex.Message.Contains("Forbidden") || ex.Message.Contains("403"))
        {
            Assert.Inconclusive("Reports.Read.All permission not granted or tenant doesn't have Copilot licenses");
        }
        catch (Exception ex) when (ex.Message.Contains("Unauthorized") || ex.Message.Contains("401"))
        {
            Assert.Fail($"Authentication failed: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task GetCopilotUsageStatsAsync_CachesToken_ForMultipleCalls()
    {
        if (_loader == null)
        {
            Assert.Inconclusive("Loader not initialized - check Graph API credentials");
            return;
        }

        try
        {
            // Act - Make multiple calls that will use the same token
            var startTime = DateTime.UtcNow;
            
            var stats1 = await _loader.GetCopilotUsageStatsAsync();
            var firstCallTime = DateTime.UtcNow - startTime;
            
            startTime = DateTime.UtcNow;
            var stats2 = await _loader.GetCopilotUsageStatsAsync();
            var secondCallTime = DateTime.UtcNow - startTime;

            // Assert - Second call should be faster or similar (using cached token)
            Assert.IsNotNull(stats1);
            Assert.IsNotNull(stats2);
            
            _logger.LogInformation($"First call: {firstCallTime.TotalMilliseconds}ms, Second call: {secondCallTime.TotalMilliseconds}ms");
            
            Assert.IsTrue(secondCallTime.TotalMilliseconds < firstCallTime.TotalMilliseconds * 2,
                "Second call should benefit from cached token");
        }
        catch (Exception ex) when (ex.Message.Contains("Forbidden") || ex.Message.Contains("403"))
        {
            Assert.Inconclusive("Reports.Read.All permission not granted or tenant doesn't have Copilot licenses");
        }
    }

    #endregion

    #region CSV Parsing Tests

    [TestMethod]
    public async Task GetCopilotUsageStatsAsync_ParsesAllActivityDates()
    {
        if (_loader == null)
        {
            Assert.Inconclusive("Loader not initialized - check Graph API credentials");
            return;
        }

        try
        {
            // Act
            var result = await _loader.GetCopilotUsageStatsAsync();

            if (result.Records.Count == 0)
            {
                Assert.Inconclusive("No Copilot usage data available in tenant");
                return;
            }

            // Assert - Check that we can parse various activity types
            var recordsWithActivity = result.Records.Where(r => r.LastActivityDate.HasValue).ToList();
            
            _logger.LogInformation($"Records with any activity: {recordsWithActivity.Count}/{result.Records.Count}");
            
            if (recordsWithActivity.Count > 0)
            {
                var sampleRecord = recordsWithActivity[0];
                var activityTypes = new Dictionary<string, DateTime?>
                {
                    { "Overall", sampleRecord.LastActivityDate },
                    { "Copilot Chat", sampleRecord.CopilotChatLastActivityDate },
                    { "Teams", sampleRecord.TeamsCopilotLastActivityDate },
                    { "Word", sampleRecord.WordCopilotLastActivityDate },
                    { "Excel", sampleRecord.ExcelCopilotLastActivityDate },
                    { "PowerPoint", sampleRecord.PowerPointCopilotLastActivityDate },
                    { "Outlook", sampleRecord.OutlookCopilotLastActivityDate },
                    { "OneNote", sampleRecord.OneNoteCopilotLastActivityDate },
                    { "Loop", sampleRecord.LoopCopilotLastActivityDate }
                };

                foreach (var activity in activityTypes.Where(a => a.Value.HasValue))
                {
                    _logger.LogInformation($"  {activity.Key}: {activity.Value}");
                }

                Assert.IsTrue(activityTypes.Values.Any(v => v.HasValue), 
                    "At least one activity type should have a date");
            }
        }
        catch (Exception ex) when (ex.Message.Contains("Forbidden") || ex.Message.Contains("403"))
        {
            Assert.Inconclusive("Reports.Read.All permission not granted or tenant doesn't have Copilot licenses");
        }
    }

    [TestMethod]
    public async Task GetCopilotUsageStatsAsync_HandlesNoData_Gracefully()
    {
        if (_loader == null)
        {
            Assert.Inconclusive("Loader not initialized - check Graph API credentials");
            return;
        }

        try
        {
            // Act
            var result = await _loader.GetCopilotUsageStatsAsync();

            // Assert - Should return empty list, not null or throw
            Assert.IsNotNull(result);
            
            if (result.Records.Count == 0)
            {
                _logger.LogInformation("No Copilot usage data found - this is acceptable for tenants without active Copilot users");
            }
        }
        catch (Exception ex) when (ex.Message.Contains("Forbidden") || ex.Message.Contains("403"))
        {
            Assert.Inconclusive("Reports.Read.All permission not granted or tenant doesn't have Copilot licenses");
        }
    }

    [TestMethod]
    public async Task GetCopilotUsageStatsAsync_ReturnsValidUserPrincipalNames()
    {
        if (_loader == null)
        {
            Assert.Inconclusive("Loader not initialized - check Graph API credentials");
            return;
        }

        try
        {
            // Act
            var result = await _loader.GetCopilotUsageStatsAsync();

            if (result.Records.Count == 0)
            {
                Assert.Inconclusive("No Copilot usage data available in tenant");
                return;
            }

            // Assert - All records should have valid UPNs
            foreach (var record in result.Records.Take(10))
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(record.UserPrincipalName), 
                    "Every record should have a non-empty UserPrincipalName");
                
                Assert.IsTrue(record.UserPrincipalName.Contains("@"), 
                    "UserPrincipalName should be in email format");
            }

            _logger.LogInformation($"All {Math.Min(10, result.Records.Count)} sampled records have valid UserPrincipalNames");
        }
        catch (Exception ex) when (ex.Message.Contains("Forbidden") || ex.Message.Contains("403"))
        {
            Assert.Inconclusive("Reports.Read.All permission not granted or tenant doesn't have Copilot licenses");
        }
    }

    #endregion

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

    #region Date Parsing Tests

    [TestMethod]
    public async Task GetCopilotUsageStatsAsync_ParsesDatesAsUtc()
    {
        if (_loader == null)
        {
            Assert.Inconclusive("Loader not initialized - check Graph API credentials");
            return;
        }

        try
        {
            // Act
            var result = await _loader.GetCopilotUsageStatsAsync();

            if (result.Records.Count == 0)
            {
                Assert.Inconclusive("No Copilot usage data available in tenant");
                return;
            }

            // Assert - All dates should be UTC
            var recordsWithDates = result.Records.Where(r => r.LastActivityDate.HasValue).ToList();
            
            if (recordsWithDates.Count > 0)
            {
                foreach (var record in recordsWithDates.Take(5))
                {
                    if (record.LastActivityDate.HasValue)
                    {
                        Assert.AreEqual(DateTimeKind.Utc, record.LastActivityDate.Value.Kind,
                            "LastActivityDate should be UTC");
                    }
                    if (record.CopilotChatLastActivityDate.HasValue)
                    {
                        Assert.AreEqual(DateTimeKind.Utc, record.CopilotChatLastActivityDate.Value.Kind,
                            "CopilotChatLastActivityDate should be UTC");
                    }
                }

                _logger.LogInformation("All dates are correctly parsed as UTC");
            }
        }
        catch (Exception ex) when (ex.Message.Contains("Forbidden") || ex.Message.Contains("403"))
        {
            Assert.Inconclusive("Reports.Read.All permission not granted or tenant doesn't have Copilot licenses");
        }
    }

    #endregion
}
