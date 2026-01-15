using Common.DataUtils.Config;
using Microsoft.Extensions.Configuration;

namespace Common.Engine.Config;

public class TeamsAppConfig : AppConfig
{
    public TeamsAppConfig() : base()
    {
    }
    public TeamsAppConfig(IConfiguration config) : base(config)
    {
    }

    /// <summary>
    /// Identity for accessing the web interface.
    /// </summary>
    [ConfigSection()]
    public AzureADAuthConfig WebAuthConfig { get; set; } = null!;

    [ConfigValue(true)]
    public string? AppCatalogTeamAppId { get; set; } = null!;

    /// <summary>
    /// Optional AI Foundry configuration for Copilot Connected mode.
    /// When configured, enables smart groups and AI-powered follow-up conversations.
    /// </summary>
    [ConfigSection(Optional = true)]
    public AIFoundryConfig? AIFoundryConfig { get; set; } = null;

    /// <summary>
    /// Returns true if Copilot Connected mode is enabled (AI Foundry is configured).
    /// </summary>
    public bool IsCopilotConnectedEnabled => AIFoundryConfig != null;
}

/// <summary>
/// Configuration for the Teams bot
/// </summary>
public class BotConfig : TeamsAppConfig
{
    public BotConfig() : base()
    {
    }

    public BotConfig(IConfiguration config) : base(config)
    {
    }

    // Leaving these as default names for now so as to not mess with bot framework defaults
    [ConfigValue(backingPropertyName: "MicrosoftAppId")] public string BotAppId { get; set; } = null!;
    [ConfigValue(backingPropertyName: "MicrosoftAppPassword")] public string BotAppSecret { get; set; } = null!;

    /// <summary>
    /// Hack for dev testing
    /// </summary>
    [ConfigValue(true)]
    public string TestUPN { get; set; } = null!;
}
