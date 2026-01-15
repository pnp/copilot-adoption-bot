using Common.DataUtils.Config;
using Common.Engine.Config;
using Microsoft.Extensions.Configuration;

namespace UnitTests;

public class TestsConfig : AppConfig
{
    public TestsConfig(IConfiguration config) : base(config)
    {
    }

    /// <summary>
    /// Optional AI Foundry configuration for testing AI-powered features.
    /// </summary>
    [ConfigSection(Optional = true)]
    public AIFoundryConfig? AIFoundryConfig { get; set; } = null;
}

