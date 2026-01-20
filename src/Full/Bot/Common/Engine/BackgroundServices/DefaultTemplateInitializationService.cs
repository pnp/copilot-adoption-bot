using Common.Engine.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Common.Engine.BackgroundServices;

/// <summary>
/// Background service that initializes default nudge templates on application startup
/// if no templates exist in storage.
/// </summary>
public class DefaultTemplateInitializationService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DefaultTemplateInitializationService> _logger;

    public DefaultTemplateInitializationService(
        IServiceProvider serviceProvider,
        ILogger<DefaultTemplateInitializationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Checking for default nudge templates...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var templateService = scope.ServiceProvider.GetRequiredService<MessageTemplateService>();
            
            var existingTemplates = await templateService.GetAllTemplates();
            
            // Only create default templates if storage is completely empty
            if (existingTemplates.Count == 0)
            {
                _logger.LogInformation("No templates found. Creating default nudge templates...");
                await CreateDefaultTemplates(templateService, cancellationToken);
                _logger.LogInformation("Default templates created successfully.");
            }
            else
            {
                _logger.LogInformation($"Found {existingTemplates.Count} existing template(s). Skipping default template creation.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing default templates. The application will continue but no default templates were created.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task CreateDefaultTemplates(MessageTemplateService templateService, CancellationToken cancellationToken)
    {
        const string systemCreator = "system@initialization";

        // Copilot Chat (Beginner)
        var copilotChatBeginner = await ReadEmbeddedResourceAsync("Engine.BackgroundServices.Templates.copilot-chat-tips.json");
        await templateService.CreateTemplate(
            "Copilot Chat - Tips (Beginner)",
            copilotChatBeginner,
            systemCreator);

        _logger.LogInformation("Created default template: Copilot Chat - Tips (Beginner)");

        // Copilot Chat (Advanced)
        var copilotChatAdvanced = await ReadEmbeddedResourceAsync("Engine.BackgroundServices.Templates.copilot-chat-tips-advanced.json");
        await templateService.CreateTemplate(
            "Copilot Chat - Tips (Advanced)",
            copilotChatAdvanced,
            systemCreator);

        _logger.LogInformation("Created default template: Copilot Chat - Tips (Advanced)");

        // Microsoft 365 Copilot (Beginner)
        var m365CopilotBeginner = await ReadEmbeddedResourceAsync("Engine.BackgroundServices.Templates.m365-copilot-tips.json");
        await templateService.CreateTemplate(
            "Microsoft 365 Copilot - Tips (Beginner)",
            m365CopilotBeginner,
            systemCreator);

        _logger.LogInformation("Created default template: Microsoft 365 Copilot - Tips (Beginner)");

        // Microsoft 365 Copilot (Advanced)
        var m365CopilotAdvanced = await ReadEmbeddedResourceAsync("Engine.BackgroundServices.Templates.m365-copilot-tips-advanced.json");
        await templateService.CreateTemplate(
            "Microsoft 365 Copilot - Tips (Advanced)",
            m365CopilotAdvanced,
            systemCreator);

        _logger.LogInformation("Created default template: Microsoft 365 Copilot - Tips (Advanced)");
    }

    private static async Task<string> ReadEmbeddedResourceAsync(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        
        await using var stream = assembly.GetManifestResourceStream(resourceName);
        
        if (stream == null)
        {
            throw new FileNotFoundException($"Embedded resource '{resourceName}' not found.");
        }

        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
