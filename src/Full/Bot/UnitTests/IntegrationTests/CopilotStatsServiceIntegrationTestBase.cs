using Azure.Data.Tables;
using Engine.Config;
using Engine.Services.UserCache;
using Microsoft.Extensions.Logging;

namespace UnitTests.IntegrationTests;

/// <summary>
/// Base test class for CopilotStatsService integration tests. Provides shared initialization
/// for the service instance and a unique Azure Table for assertions, and a cleanup hook that
/// deletes the test table after each test.
/// </summary>
public abstract class CopilotStatsServiceIntegrationTestBase : AbstractTest
{
    protected CopilotStatsService? _service;
    protected TableServiceClient? _tableServiceClient;
    protected string _testTableName = string.Empty;

    [TestInitialize]
    public void BaseInitialize()
    {
        // Include milliseconds and random component to ensure uniqueness in parallel test execution
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var random = new Random().Next(1000, 9999);
        _testTableName = $"copilotteststats{timestamp}{random}";

        try
        {
            var cacheConfig = new UserCacheConfig
            {
                CopilotStatsPeriod = "D30"
            };

            var statsLoader = new GraphCopilotStatsLoader(
                GetLogger<GraphCopilotStatsLoader>(),
                cacheConfig,
                _config.GraphConfig);

            _service = new CopilotStatsService(
                GetLogger<CopilotStatsService>(),
                statsLoader);

            _tableServiceClient = CreateTableServiceClient();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to initialize CopilotStatsService: {ex.Message}");
            _logger.LogWarning("Tests will be skipped if Graph credentials or Reports.Read.All permission are not configured");
        }
    }

    [TestCleanup]
    public async Task BaseCleanup()
    {
        if (_tableServiceClient != null)
        {
            try
            {
                await _tableServiceClient.DeleteTableAsync(_testTableName);
                _logger.LogInformation($"Cleaned up test table: {_testTableName}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error during cleanup: {ex.Message}");
            }
        }
    }

    private TableServiceClient CreateTableServiceClient()
    {
        var storageAuthConfig = GetStorageAuthConfig();
        if (storageAuthConfig.UseRBAC)
        {
            var tableEndpoint = new Uri($"https://{storageAuthConfig.StorageAccountName}.table.core.windows.net");
            if (storageAuthConfig.RBACOverrideCredentials != null)
            {
                var credential = new Azure.Identity.ClientSecretCredential(
                    storageAuthConfig.RBACOverrideCredentials.TenantId,
                    storageAuthConfig.RBACOverrideCredentials.ClientId,
                    storageAuthConfig.RBACOverrideCredentials.ClientSecret);
                return new TableServiceClient(tableEndpoint, credential);
            }

            return new TableServiceClient(tableEndpoint, new Azure.Identity.DefaultAzureCredential());
        }

        return new TableServiceClient(storageAuthConfig.ConnectionString);
    }
}
