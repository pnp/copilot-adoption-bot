using System.ClientModel;
using System.Text.Json;
using Azure.AI.OpenAI;
using Common.Engine.Config;
using Common.Engine.Models;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Common.Engine.Services;

/// <summary>
/// Result of AI-based user matching
/// </summary>
public class AIUserMatchResult
{
    public string UserPrincipalName { get; set; } = null!;
    public double ConfidenceScore { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Result of a follow-up chat interaction
/// </summary>
public class AIFollowUpResponse
{
    public string Response { get; set; } = null!;
    public bool ShouldEndConversation { get; set; }
    public Dictionary<string, string>? ExtractedData { get; set; }
}

/// <summary>
/// Service for interacting with Azure AI Foundry for smart group resolution and follow-up chats.
/// </summary>
public class AIFoundryService
{
    private readonly AIFoundryConfig _config;
    private readonly ILogger<AIFoundryService> _logger;
    private readonly ChatClient _chatClient;
    private readonly SettingsStorageManager? _settingsManager;

    public AIFoundryService(AIFoundryConfig config, ILogger<AIFoundryService> logger, SettingsStorageManager? settingsManager = null)
    {
        _config = config;
        _logger = logger;
        _settingsManager = settingsManager;

        var azureClient = new AzureOpenAIClient(
            new Uri(config.Endpoint),
            new ApiKeyCredential(config.ApiKey));

        _chatClient = azureClient.GetChatClient(config.DeploymentName);
    }

    /// <summary>
    /// Resolve a smart group description to matching users using AI.
    /// </summary>
    /// <param name="groupDescription">Natural language description of the target users</param>
    /// <param name="availableUsers">List of available users with their metadata</param>
    /// <returns>List of matching users with confidence scores</returns>
    public async Task<List<AIUserMatchResult>> ResolveSmartGroupMembersAsync(
        string groupDescription,
        List<EnrichedUserInfo> availableUsers)
    {
        _logger.LogInformation($"Resolving smart group: '{groupDescription}' against {availableUsers.Count} users");

        var results = new List<AIUserMatchResult>();

        if (availableUsers.Count == 0)
        {
            _logger.LogWarning("No users provided for smart group resolution");
            return results;
        }

        // Build the user list for the AI prompt
        var userSummaries = availableUsers.Select((u, idx) => $"{idx + 1}. {u.ToAISummary()}").ToList();
        var userListText = string.Join("\n", userSummaries);

        var systemPrompt = $@"You are an AI assistant that helps match users to group criteria based on their profile and activity data.

Current date: {DateTime.UtcNow:yyyy-MM-dd}

You will receive:
1. A description of the target user group
2. A list of users with their metadata including:
   - Profile information (name, department, job title, location, etc.)
   - Has Copilot License (Yes/No) - indicates if the user has a Microsoft 365 Copilot license assigned
   - Copilot Activity data showing the last activity date for various Microsoft 365 Copilot features (format: YYYY-MM-DD)
     - Overall: Last activity across any Copilot feature
     - Chat: Copilot Chat activity
     - Teams: Teams Copilot activity
     - Word: Word Copilot activity
     - Excel: Excel Copilot activity
     - PowerPoint: PowerPoint Copilot activity
     - Outlook: Outlook Copilot activity
     - OneNote: OneNote Copilot activity
     - Loop: Loop Copilot activity

Your task is to identify which users match the group description and return them with confidence scores.

IMPORTANT DATE HANDLING:
- When criteria mention time periods (e.g., ""last 30 days"", ""last week"", ""recently""), calculate the date range from today's date
- Compare activity dates to determine if they fall within the specified time period
- A user matches if they have activity within the requested timeframe
- If a user has no activity date for a specific Copilot feature, they don't match criteria requiring that feature

MATCHING RULES:
- Only include users that genuinely match ALL specified criteria
- Confidence score should be between 0.0 and 1.0 (1.0 = perfect match)
- Include a brief reason explaining why the user matches (reference specific dates or attributes)
- If no users match, return an empty array

Return your response as a JSON array in this exact format:
[
  {{""upn"": ""user@example.com"", ""confidence"": 0.95, ""reason"": ""Matches because...""}},
  ...
]

Only return the JSON array, no other text.";

        var userPrompt = $@"Group Description: {groupDescription}

Available Users:
{userListText}

Which users match the group description? Return as JSON array.";

        try
        {
            // Log the prompts for debugging
            _logger.LogDebug($"System Prompt: {systemPrompt}");
            _logger.LogDebug($"User Prompt: {userPrompt}");
            
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userPrompt)
            };

            var options = new ChatCompletionOptions
            {
                MaxOutputTokenCount = _config.MaxTokens,
                Temperature = _config.GetTemperature()
            };

            var response = await _chatClient.CompleteChatAsync(messages, options);

            if (response?.Value?.Content != null && response.Value.Content.Count > 0)
            {
                var responseText = response.Value.Content[0].Text;
                _logger.LogDebug($"AI Response: {responseText}");

                // Parse the JSON response
                results = ParseUserMatchResponse(responseText, availableUsers);
                _logger.LogInformation($"AI matched {results.Count} users for smart group");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling AI Foundry for smart group resolution");
            throw;
        }

        return results;
    }

    /// <summary>
    /// Handle a follow-up chat message from a user.
    /// </summary>
    /// <param name="userUpn">The UPN of the user sending the message</param>
    /// <param name="userMessage">The user's message</param>
    /// <param name="originalNudgeContext">Context about the original nudge that was sent</param>
    /// <param name="conversationHistory">Previous messages in the conversation</param>
    /// <returns>AI response and metadata</returns>
    public async Task<AIFollowUpResponse> HandleFollowUpChatAsync(
        string userUpn,
        string userMessage,
        string? originalNudgeContext,
        List<(string role, string message)>? conversationHistory = null)
    {
        _logger.LogInformation($"Handling follow-up chat from {userUpn}: {userMessage.Substring(0, Math.Min(50, userMessage.Length))}...");

        // Get the configurable system prompt
        var systemPrompt = await GetFollowUpChatSystemPromptAsync();

        if (!string.IsNullOrEmpty(originalNudgeContext))
        {
            systemPrompt += $"\n\nThe original nudge message context was about: {originalNudgeContext}";
        }

        try
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt)
            };

            // Add conversation history if available
            if (conversationHistory != null)
            {
                foreach (var (role, message) in conversationHistory)
                {
                    if (role.Equals("user", StringComparison.OrdinalIgnoreCase))
                    {
                        messages.Add(new UserChatMessage(message));
                    }
                    else if (role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                    {
                        messages.Add(new AssistantChatMessage(message));
                    }
                }
            }

            messages.Add(new UserChatMessage(userMessage));

            var options = new ChatCompletionOptions
            {
                MaxOutputTokenCount = 500, // Keep responses concise for chat
                Temperature = 0.7f
            };

            var response = await _chatClient.CompleteChatAsync(messages, options);

            if (response?.Value?.Content != null && response.Value.Content.Count > 0)
            {
                var responseText = response.Value.Content[0].Text;

                return new AIFollowUpResponse
                {
                    Response = responseText,
                    ShouldEndConversation = DetectConversationEnd(userMessage, responseText)
                };
            }

            return new AIFollowUpResponse
            {
                Response = "I'm sorry, I couldn't process your message. Please try again.",
                ShouldEndConversation = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling follow-up chat via AI Foundry");
            return new AIFollowUpResponse
            {
                Response = "I apologize, but I'm having trouble responding right now. Please try again later.",
                ShouldEndConversation = true
            };
        }
    }

    private List<AIUserMatchResult> ParseUserMatchResponse(string responseText, List<EnrichedUserInfo> availableUsers)
    {
        var results = new List<AIUserMatchResult>();

        try
        {
            // Clean up the response - remove markdown code blocks if present
            var cleanedResponse = responseText.Trim();
            if (cleanedResponse.StartsWith("```"))
            {
                var lines = cleanedResponse.Split('\n').ToList();
                lines.RemoveAt(0); // Remove opening ```json or ```
                if (lines.Count > 0 && lines[^1].StartsWith("```"))
                {
                    lines.RemoveAt(lines.Count - 1); // Remove closing ```
                }
                cleanedResponse = string.Join("\n", lines);
            }

            var jsonResults = JsonSerializer.Deserialize<List<JsonElement>>(cleanedResponse);
            
            if (jsonResults != null)
            {
                // Use GroupBy to handle duplicate UPNs - take the first occurrence
                var upnLookup = availableUsers
                    .GroupBy(u => u.UserPrincipalName.ToLowerInvariant())
                    .ToDictionary(g => g.Key, g => g.First());

                foreach (var item in jsonResults)
                {
                    var upn = item.GetProperty("upn").GetString();
                    if (upn != null && upnLookup.TryGetValue(upn.ToLowerInvariant(), out var user))
                    {
                        var result = new AIUserMatchResult
                        {
                            UserPrincipalName = user.UserPrincipalName, // Use correct casing
                            ConfidenceScore = item.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : 0.5,
                            Reason = item.TryGetProperty("reason", out var reason) ? reason.GetString() : null
                        };
                        results.Add(result);
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, $"Failed to parse AI response as JSON: {responseText}");
        }

        return results;
    }

    private bool DetectConversationEnd(string userMessage, string aiResponse)
        {
            var endIndicators = new[]
            {
                "thank", "thanks", "got it", "ok", "okay", "understood",
                "bye", "goodbye", "cheers", "perfect", "great", "awesome"
            };

            var userLower = userMessage.ToLowerInvariant();
            return endIndicators.Any(indicator => userLower.Contains(indicator) && userMessage.Length < 50);
        }

        /// <summary>
        /// Get the effective follow-up chat system prompt (custom or default)
        /// </summary>
        private async Task<string> GetFollowUpChatSystemPromptAsync()
        {
            if (_settingsManager != null)
            {
                try
                {
                    return await _settingsManager.GetEffectiveFollowUpChatSystemPrompt();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load custom system prompt, using default");
                }
            }
        
            return SettingsStorageManager.DefaultFollowUpChatSystemPrompt;
        }
    }
