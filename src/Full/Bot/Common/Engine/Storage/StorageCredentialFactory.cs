using Azure.Core;
using Azure.Identity;
using Common.Engine.Config;
using Microsoft.Extensions.Logging;

namespace Common.Engine.Storage;

/// <summary>
/// Factory for creating Azure Storage credentials supporting both RBAC and connection string authentication.
/// </summary>
public class StorageCredentialFactory
{
    private readonly AppConfig _config;
    private readonly ILogger _logger;
    private static TokenCredential? _cachedCredential;

    public StorageCredentialFactory(AppConfig config, ILogger logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Gets the appropriate credential for storage access.
    /// Returns null if using connection string authentication.
    /// </summary>
    public TokenCredential? GetCredential()
    {
        if (!_config.EnableRBAC)
        {
            _logger.LogInformation("Using connection string authentication for Azure Storage");
            return null;
        }

        if (_cachedCredential == null)
        {
            _cachedCredential = CreateCredential();
        }

        return _cachedCredential;
    }

    /// <summary>
    /// Creates the appropriate credential based on configuration.
    /// Uses StorageAuthConfig if provided, otherwise falls back to DefaultAzureCredential.
    /// </summary>
    private TokenCredential CreateCredential()
    {
        // If explicit storage credentials are configured, use them
        if (_config.StorageAuthConfig != null && 
            !string.IsNullOrEmpty(_config.StorageAuthConfig.ClientId) &&
            !string.IsNullOrEmpty(_config.StorageAuthConfig.ClientSecret) &&
            !string.IsNullOrEmpty(_config.StorageAuthConfig.TenantId))
        {
            _logger.LogInformation("Using service principal authentication for Azure Storage (ClientId: {ClientId})", 
                _config.StorageAuthConfig.ClientId);
            
            return new ClientSecretCredential(
                _config.StorageAuthConfig.TenantId,
                _config.StorageAuthConfig.ClientId,
                _config.StorageAuthConfig.ClientSecret);
        }

        // Otherwise use DefaultAzureCredential (Managed Identity or local development credentials)
        _logger.LogInformation("Using DefaultAzureCredential for Azure Storage (Managed Identity or Visual Studio)");
        return CreateDefaultAzureCredential();
    }

    /// <summary>
    /// Creates a DefaultAzureCredential that works both locally (Visual Studio) and in Azure.
    /// In development, it will use Visual Studio authentication or environment variables.
    /// In Azure, it will use Managed Identity.
    /// </summary>
    private DefaultAzureCredential CreateDefaultAzureCredential()
    {
        var options = new DefaultAzureCredentialOptions
        {
            // Exclude credentials that are unlikely to work and add noise to logs
            ExcludeSharedTokenCacheCredential = true,
            ExcludeAzurePowerShellCredential = true
        };

        return new DefaultAzureCredential(options);
    }

    /// <summary>
    /// Gets the connection string if not using RBAC, otherwise returns null.
    /// </summary>
    public string? GetConnectionString()
    {
        return _config.EnableRBAC ? null : _config.ConnectionStrings.Storage;
    }

    /// <summary>
    /// Gets the storage account name for RBAC authentication.
    /// Extracts from connection string if not explicitly configured.
    /// </summary>
    public string GetStorageAccountName()
    {
        if (!string.IsNullOrEmpty(_config.StorageAccountName))
        {
            return _config.StorageAccountName;
        }

        // Try to extract from connection string
        if (!string.IsNullOrEmpty(_config.ConnectionStrings.Storage))
        {
            var accountNamePrefix = "AccountName=";
            var startIndex = _config.ConnectionStrings.Storage.IndexOf(accountNamePrefix);
            if (startIndex >= 0)
            {
                startIndex += accountNamePrefix.Length;
                var endIndex = _config.ConnectionStrings.Storage.IndexOf(';', startIndex);
                if (endIndex >= 0)
                {
                    return _config.ConnectionStrings.Storage.Substring(startIndex, endIndex - startIndex);
                }
                else
                {
                    return _config.ConnectionStrings.Storage.Substring(startIndex);
                }
            }
        }

        throw new InvalidOperationException(
            "StorageAccountName must be configured when using RBAC authentication, or a valid connection string must be provided");
    }

    /// <summary>
    /// Checks if RBAC is enabled.
    /// </summary>
    public bool IsRBACEnabled => _config.EnableRBAC;
}
