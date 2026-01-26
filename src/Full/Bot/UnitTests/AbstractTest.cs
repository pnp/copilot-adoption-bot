



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
}
