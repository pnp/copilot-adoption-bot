using Azure.Data.Tables;
using Common.Engine.Config;
using Common.Engine.Storage;
using Microsoft.Extensions.Logging;

namespace Common.Engine;

/// <summary>
/// Manages application settings in Azure Table Storage
/// </summary>
public class SettingsStorageManager : TableStorageManager
{
    private readonly ILogger _logger;
    private const string SETTINGS_TABLE_NAME = "appsettings";

    /// <summary>
    /// Default system prompt for follow-up chat conversations
    /// </summary>
    public const string DefaultFollowUpChatSystemPrompt = @"You are a helpful assistant for a Microsoft Teams bot called Office Adoption Bot. 
Users receive nudge messages (tips, reminders, notifications) and may reply with questions or feedback.

Your role:
1. Answer questions about the nudge content helpfully
2. Provide additional information or clarification when asked
3. Be concise and professional
4. If the user seems done with the conversation, indicate that in your response

Keep responses brief and suitable for a Teams chat. Use markdown formatting sparingly.";

public SettingsStorageManager(StorageAuthConfig storageAuthConfig, ILogger logger)
    : base(storageAuthConfig, logger)
{
    _logger = logger;
}

    /// <summary>
    /// Get current application settings
    /// </summary>
    public async Task<AppSettingsTableEntity> GetSettings()
    {
        var tableClient = await GetTableClient(SETTINGS_TABLE_NAME);
        
        try
        {
            var response = await tableClient.GetEntityAsync<AppSettingsTableEntity>(
                AppSettingsTableEntity.PartitionKeyVal, 
                AppSettingsTableEntity.SingletonRowKey);
            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            // Return default settings if none exist
            return new AppSettingsTableEntity
            {
                FollowUpChatSystemPrompt = null // Will use default
            };
        }
    }

    /// <summary>
    /// Update application settings
    /// </summary>
    public async Task<AppSettingsTableEntity> UpdateSettings(string? followUpChatSystemPrompt, string modifiedByUpn)
    {
        var tableClient = await GetTableClient(SETTINGS_TABLE_NAME);
        
        AppSettingsTableEntity entity;
        
        try
        {
            var response = await tableClient.GetEntityAsync<AppSettingsTableEntity>(
                AppSettingsTableEntity.PartitionKeyVal, 
                AppSettingsTableEntity.SingletonRowKey);
            entity = response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            entity = new AppSettingsTableEntity();
        }

        entity.FollowUpChatSystemPrompt = followUpChatSystemPrompt;
        entity.LastModifiedDate = DateTime.UtcNow;
        entity.LastModifiedByUpn = modifiedByUpn;

        await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace);

        _logger.LogInformation($"Updated app settings by {modifiedByUpn}");
        return entity;
    }

    /// <summary>
    /// Get the effective follow-up chat system prompt (custom or default)
    /// </summary>
    public async Task<string> GetEffectiveFollowUpChatSystemPrompt()
    {
        var settings = await GetSettings();
        
        if (!string.IsNullOrWhiteSpace(settings.FollowUpChatSystemPrompt))
        {
            return settings.FollowUpChatSystemPrompt;
        }
        
        return DefaultFollowUpChatSystemPrompt;
    }
}
