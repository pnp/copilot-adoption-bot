





using Azure;
using Azure.Data.Tables;
using Common.Engine.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace UnitTests;

public abstract class AbstractTest
{
    protected ILogger _logger;
    protected TestsConfig _config;

    public AbstractTest()
    {
        _logger = LoggerFactory.Create(config =>
        {
            config.AddConsole();
        }).CreateLogger("Tests");

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddUserSecrets(System.Reflection.Assembly.GetExecutingAssembly())
            .AddJsonFile("appsettings.json", true);

        var configCollection = builder.Build();
        _config = new TestsConfig(configCollection);

    }

    protected ILogger<T> GetLogger<T>()
    {
        return LoggerFactory.Create(config =>
        {
            config.AddConsole();
        }).CreateLogger<T>();
    }

    /// <summary>
    /// Helper method to get StorageAuthConfig with fallback to legacy configuration.
    /// Use this in all integration tests to support both RBAC and connection string authentication.
    /// </summary>
    protected StorageAuthConfig GetStorageAuthConfig()
    {
        // If StorageAuthConfig is properly configured, use it
        if (_config.StorageAuthConfig != null &&
            (_config.StorageAuthConfig.UseRBAC || !string.IsNullOrEmpty(_config.StorageAuthConfig.ConnectionString)))
        {
            return _config.StorageAuthConfig;
        }

        // Fallback to legacy ConnectionStrings.Storage
        return new StorageAuthConfig
        {
            UseRBAC = false,
            ConnectionString = _config.ConnectionStrings.Storage
        };
    }

    /// <summary>
    /// Helper method to create a table with retry logic for "TableBeingDeleted" errors.
    /// Use this in tests instead of calling CreateIfNotExistsAsync directly.
    /// </summary>
    protected async Task CreateTableWithRetryAsync(TableClient tableClient)
    {
        int maxRetries = 10;
        int retryDelayMs = 2000; // Start with 2 seconds for cloud environments
        
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                await tableClient.CreateIfNotExistsAsync();
                return; // Success
            }
            catch (RequestFailedException ex) when (ex.ErrorCode == "TableAlreadyExists")
            {
                // Table already exists, that's fine
                return;
            }
            catch (RequestFailedException ex) when (ex.ErrorCode == "TableBeingDeleted")
            {
                if (attempt == maxRetries)
                {
                    // Final attempt failed, rethrow with more context
                    throw new InvalidOperationException(
                        $"Table '{tableClient.Name}' is being deleted and did not become available after {maxRetries} retry attempts. " +
                        "This may indicate a naming collision in parallel test execution.", ex);
                }
                
                _logger.LogWarning($"Table '{tableClient.Name}' is being deleted. Retry attempt {attempt + 1} of {maxRetries}...");
                
                // Wait with exponential backoff before retrying
                await Task.Delay(retryDelayMs);
                retryDelayMs *= 2; // Double the delay for next attempt
            }
        }
    }
}
