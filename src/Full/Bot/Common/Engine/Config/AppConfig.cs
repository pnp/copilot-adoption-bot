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

    [ConfigSection(Optional = true)]
    public StorageAuthConfig? StorageAuthConfig { get; set; }
}

public class AppConnectionStrings : PropertyBoundConfig
{
    public AppConnectionStrings() : base()
    {
    }

    public AppConnectionStrings(IConfigurationSection config) : base(config) { }

    [ConfigValue(true)]
    public string Storage { get; set; } = null!;
}
