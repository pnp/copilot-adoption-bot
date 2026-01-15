using Common.DataUtils.Config;
using Microsoft.Extensions.Configuration;

namespace Common.Engine.Config;

public class AzureADAuthConfig : PropertyBoundConfig
{
    public AzureADAuthConfig() : base()
    {
    }

    public AzureADAuthConfig(IConfiguration config) : base(config)
    {
    }

    [ConfigValue]
    public string ClientId { get; set; } = null!;

    [ConfigValue]
    public string ClientSecret { get; set; } = null!;

    [ConfigValue]
    public string TenantId { get; set; } = null!;

    [ConfigValue(true)]
    public string Authority { get; set; } = "https://login.microsoftonline.com/organizations";

    [ConfigValue(true)]
    public string? ApiAudience { get; set; } = null!;
}
