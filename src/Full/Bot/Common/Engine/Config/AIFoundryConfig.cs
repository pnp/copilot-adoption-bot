using DataUtils.Config;
using Microsoft.Extensions.Configuration;

namespace Engine.Config;

/// <summary>
/// Configuration for Azure AI Foundry integration (Copilot Connected mode).
/// When configured, enables smart groups and AI-powered follow-up conversations.
/// </summary>
public class AIFoundryConfig : PropertyBoundConfig
{
    public AIFoundryConfig() : base()
    {
    }

    public AIFoundryConfig(IConfigurationSection config) : base(config)
    {
    }

    /// <summary>
    /// The Azure AI Foundry endpoint URL.
    /// Example: https://your-project.openai.azure.com/
    /// </summary>
    [ConfigValue]
    public string Endpoint { get; set; } = null!;

    /// <summary>
    /// The deployment name for the AI model.
    /// </summary>
    [ConfigValue]
    public string DeploymentName { get; set; } = null!;

    /// <summary>
    /// Optional: Override the default Azure credentials with specific service principal credentials.
    /// If not provided, DefaultAzureCredential will be used (Managed Identity, Azure CLI, etc.).
    /// Authentication to AI Foundry always uses Azure RBAC; API key authentication is not supported.
    /// </summary>
    [ConfigSection(Optional = true)]
    public AzureADAuthConfig? RBACOverrideCredentials { get; set; }

    /// <summary>
    /// Optional: Maximum tokens for AI responses.
    /// </summary>
    [ConfigValue(true)]
    public int MaxTokens { get; set; } = 2000;

    /// <summary>
    /// Optional: Temperature for AI responses (0.0-1.0).
    /// </summary>
    [ConfigValue(true)]
    public string? Temperature { get; set; } = "0.7";

    /// <summary>
    /// Gets the parsed temperature value.
    /// </summary>
    public float GetTemperature()
    {
        if (float.TryParse(Temperature, out var temp))
        {
            return Math.Clamp(temp, 0f, 1f);
        }
        return 0.7f;
    }
}
