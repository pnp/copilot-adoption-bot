using Engine.Config;
using Engine.Services.UserCache;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;

namespace UnitTests.IntegrationTests;

/// <summary>
/// Base class for <see cref="UserCacheManager"/> integration tests.
/// Provides shared initialization that creates a Graph client, data loader and storage backed
/// by unique Azure Table names so tests can run in parallel without conflicts.
/// </summary>
public abstract class UserCacheManagerIntegrationTestBase : AbstractTest
{
    protected UserCacheManager? _cacheManager;
    protected AzureTableCacheStorage? _storage;
    protected GraphServiceClient? _graphClient;
    protected UserCacheConfig? _cacheConfig;
    protected string _testTablePrefix = string.Empty;
    protected bool _testPassed = false;

    [TestInitialize]
    public void BaseInitialize()
    {
        // Use unique table names for each test run to avoid conflicts
        // Include milliseconds and random component to ensure uniqueness even in parallel test execution
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var random = new Random().Next(1000, 9999);
        _testTablePrefix = $"test{timestamp}{random}";

        _cacheConfig = new UserCacheConfig
        {
            CacheExpiration = TimeSpan.FromMinutes(5),
            FullSyncInterval = TimeSpan.FromDays(1),
            UserCacheTableName = $"{_testTablePrefix}usercache",
            SyncMetadataTableName = $"{_testTablePrefix}syncmeta"
        };

        try
        {
            // Create Graph client for tests
            var options = new Azure.Identity.TokenCredentialOptions
            {
                AuthorityHost = Azure.Identity.AzureAuthorityHosts.AzurePublicCloud
            };
            var scopes = new[] { "https://graph.microsoft.com/.default" };
            var clientSecretCredential = new Azure.Identity.ClientSecretCredential(
                _config.GraphConfig.TenantId,
                _config.GraphConfig.ClientId,
                _config.GraphConfig.ClientSecret,
                options);

            _graphClient = new GraphServiceClient(clientSecretCredential, scopes);

            // Create adapters
            var copilotStatsLoader = new GraphCopilotStatsLoader(
                GetLogger<GraphCopilotStatsLoader>(),
                _cacheConfig,
                _config.GraphConfig);
            var dataLoader = new GraphUserDataLoader(_graphClient, GetLogger<GraphUserDataLoader>(), copilotStatsLoader, _cacheConfig);
            _storage = new AzureTableCacheStorage(GetStorageAuthConfig(), GetLogger<AzureTableCacheStorage>(), _cacheConfig);

            _cacheManager = new UserCacheManager(dataLoader, _storage, _cacheConfig, GetLogger<UserCacheManager>());
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to initialize cache manager: {ex.Message}");
            _logger.LogWarning("Tests will be skipped if Graph credentials are not configured");
        }
    }

    [TestCleanup]
    public async Task BaseCleanup()
    {
        if (_cacheManager != null && _storage != null)
        {
            try
            {
                if (_testPassed)
                {
                    // Test passed - delete the temporary tables
                    await _storage.DeleteTablesAsync();
                    _logger.LogInformation($"Deleted test tables with prefix: {_testTablePrefix}");
                }
                else
                {
                    // Test failed - keep tables for debugging but clear data
                    await _cacheManager.ClearCacheAsync();
                    _logger.LogWarning($"Test failed - kept tables with prefix: {_testTablePrefix} for debugging");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error during cleanup: {ex.Message}");
            }
        }

        // Reset for next test
        _testPassed = false;
    }
}
