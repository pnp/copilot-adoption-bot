using Common.DataUtils.Config;
using Microsoft.Extensions.Configuration;

namespace Common.Engine.Config;

/// <summary>
/// Configuration for Azure Storage authentication.
/// Supports both connection string (key-based) and RBAC (role-based access control) authentication.
/// </summary>
public class StorageAuthConfig : PropertyBoundConfig
{
    public StorageAuthConfig() : base()
    {
    }

    public StorageAuthConfig(IConfigurationSection config) : base(config)
    {
    }

    /// <summary>
    /// If true, use Azure RBAC authentication (DefaultAzureCredential or override credentials).
    /// If false, use connection string with access key.
    /// </summary>
    [ConfigValue]
    public bool UseRBAC { get; set; } = false;

    /// <summary>
    /// Optional: Override the default Azure credentials with specific service principal credentials.
    /// Only used when UseRBAC is true.
    /// If not provided, DefaultAzureCredential will be used (Managed Identity, Azure CLI, etc.)
    /// </summary>
    [ConfigSection()]
    public AzureADAuthConfig? RBACOverrideCredentials { get; set; }

    /// <summary>
    /// Azure Storage account name. Required when UseRBAC is true.
    /// Used to construct storage endpoints (e.g., https://{accountName}.blob.core.windows.net)
    /// </summary>
    [ConfigValue(true)]
    public string? StorageAccountName { get; set; }

    /// <summary>
    /// Storage connection string (includes account key).
    /// Required when UseRBAC is false. Ignored when UseRBAC is true.
    /// </summary>
    [ConfigValue(true)]
    public string? ConnectionString { get; set; }
}
