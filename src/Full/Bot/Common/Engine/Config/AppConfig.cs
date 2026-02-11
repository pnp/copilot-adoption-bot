using Common.DataUtils.Config;
using Microsoft.Extensions.Configuration;

namespace Common.Engine.Config;

public class AppConfig : PropertyBoundConfig
{
    public AppConfig() : base()
    {
        ConnectionStrings = new AppConnectionStrings();
        GraphConfig = new AzureADAuthConfig();
    }

    public AppConfig(IConfiguration config) : base(config)
    {
    }

    [ConfigSection()]
    public AzureADAuthConfig GraphConfig { get; set; } = null!;


    [ConfigValue(true, "APPLICATIONINSIGHTS_CONNECTION_STRING")]
    public string? AppInsightsConnectionString { get; set; }

    [ConfigValue(true)]
    public bool DevMode { get; set; } = false;

    [ConfigSection()]
    public AppConnectionStrings ConnectionStrings { get; set; } = null!;

    /// <summary>
    /// When true (default), use Azure RBAC for storage authentication instead of connection strings.
    /// Set to false to use connection string authentication (e.g., for local development with Azurite).
    /// </summary>
    [ConfigValue(true)]
    public bool EnableRBAC { get; set; } = true;

    /// <summary>
    /// Storage account name for RBAC authentication. Required when EnableRBAC is true and not using a full connection string.
    /// </summary>
    [ConfigValue(true)]
    public string? StorageAccountName { get; set; }

    /// <summary>
    /// Optional Azure AD credentials for storage RBAC authentication.
    /// If configured, uses these credentials instead of DefaultAzureCredential.
    /// Useful for service principal authentication or when DefaultAzureCredential doesn't work.
    /// </summary>
    [ConfigSection(Optional = true)]
    public AzureADAuthConfig? StorageAuthConfig { get; set; }
}

public class AppConnectionStrings : PropertyBoundConfig
{
    public AppConnectionStrings() : base()
    {
    }

    public AppConnectionStrings(IConfigurationSection config) : base(config) { }

    [ConfigValue]
    public string Storage { get; set; } = null!;
}
