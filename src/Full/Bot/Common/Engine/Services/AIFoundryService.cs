using Azure.AI.OpenAI;
using Azure.Identity;
using Engine.Config;
using Engine.Models;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using System.ClientModel;
using System.Text.Json;

namespace Engine.Services;

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

    /// <summary>
    /// Maximum follow-up chat completions in flight at once.
    ///
    /// <para>
    /// Without a gate, a nudge blast to 150,000 users produces a reply burst that maps
    /// one-to-one onto concurrent model calls: a 10% reply rate over five minutes is ~50
    /// requests/second, which at ~2s latency is ~100 concurrent completions on a single-core
    /// worker that is also running the dispatcher. The smart-group path was already bounded;
    /// this path was not.
    /// </para>
    /// </summary>
    private static readonly SemaphoreSlim FollowUpGate = new(8, 8);

    /// <summary>
    /// Wall-clock budget for a single follow-up completion. Without this a hung call holds a
    /// Bot Framework turn, a thread and a queue slot indefinitely.
    /// </summary>
    private static readonly TimeSpan FollowUpTimeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Maximum characters accepted from a user message. Protects both the token bill and the
    /// 64 KB Azure Table property limit that the persisted history has to fit inside.
    /// </summary>
    internal const int MaxUserMessageChars = 2000;

    /// <summary>
    /// Maximum characters of conversation history replayed as context. The dialog also caps
    /// history by entry count, but 20 long turns can still be tens of thousands of tokens.
    /// </summary>
    internal const int MaxHistoryChars = 4000;

    /// <summary>
    /// Maximum characters of card context injected into the system prompt.
    /// </summary>
    internal const int MaxCardContextChars = 1000;

    public AIFoundryService(AIFoundryConfig config, ILogger<AIFoundryService> logger, SettingsStorageManager? settingsManager = null)
    {
        _config = config;
        _logger = logger;
        _settingsManager = settingsManager;

        _logger.LogDebug("Creating AzureOpenAIClient using RBAC authentication for endpoint {Endpoint}", config.Endpoint);
        var credential = GetCredential(config);
        var azureClient = new AzureOpenAIClient(new Uri(config.Endpoint), credential);
        _logger.LogInformation("Successfully created AzureOpenAIClient using RBAC for {Endpoint}", config.Endpoint);

        _chatClient = azureClient.GetChatClient(config.DeploymentName);
    }

    /// <summary>
    /// Gets the appropriate Azure credential based on the configuration.
    /// Uses RBACOverrideCredentials if provided, otherwise uses DefaultAzureCredential.
    /// </summary>
    private Azure.Core.TokenCredential GetCredential(AIFoundryConfig config)
    {
        if (config.RBACOverrideCredentials != null)
        {
            _logger.LogDebug("Using ClientSecretCredential with override credentials for tenant {TenantId}",
                config.RBACOverrideCredentials.TenantId);
            return new ClientSecretCredential(
                config.RBACOverrideCredentials.TenantId,
                config.RBACOverrideCredentials.ClientId,
                config.RBACOverrideCredentials.ClientSecret);
        }

        _logger.LogDebug("Using DefaultAzureCredential (Managed Identity, Azure CLI, Environment Variables, etc.)");
        return new DefaultAzureCredential();
    }

    /// <summary>
    /// Maximum number of users sent to the AI in a single chat completion request.
    /// Each chunk produces its own JSON response and the results are merged. Keeps prompts
    /// well within model token limits and lets us run chunks in parallel.
    /// </summary>
    internal const int SmartGroupResolutionChunkSize = 100;

    /// <summary>
    /// Maximum number of chunks that can be in-flight against the model at once.
    /// </summary>
    internal const int SmartGroupResolutionMaxParallelism = 4;

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

        if (availableUsers.Count == 0)
        {
            _logger.LogWarning("No users provided for smart group resolution");
            return new List<AIUserMatchResult>();
        }

        // Page the user list. Sending an entire tenant in a single prompt would blow past
        // the model's input-token limit and is expensive even when it fits.
        var chunks = ChunkUsers(availableUsers, SmartGroupResolutionChunkSize);
        _logger.LogInformation(
            "Smart-group resolution will run {ChunkCount} AI request(s) of up to {ChunkSize} users each",
            chunks.Count, SmartGroupResolutionChunkSize);

        using var throttler = new SemaphoreSlim(SmartGroupResolutionMaxParallelism);
        var perChunkResults = new System.Collections.Concurrent.ConcurrentBag<List<AIUserMatchResult>>();

        var tasks = chunks.Select(async chunk =>
        {
            await throttler.WaitAsync();
            try
            {
                var chunkResults = await ResolveSmartGroupChunkAsync(groupDescription, chunk);
                perChunkResults.Add(chunkResults);
            }
            finally
            {
                throttler.Release();
            }
        });

        await Task.WhenAll(tasks);

        // Merge results. If the same UPN appears in multiple chunks (shouldn't, but defensively
        // handle it), keep the highest confidence.
        var merged = new Dictionary<string, AIUserMatchResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var chunkResult in perChunkResults)
        {
            foreach (var r in chunkResult)
            {
                if (!merged.TryGetValue(r.UserPrincipalName, out var existing) || existing.ConfidenceScore < r.ConfidenceScore)
                {
                    merged[r.UserPrincipalName] = r;
                }
            }
        }

        _logger.LogInformation($"AI matched {merged.Count} users for smart group across {chunks.Count} chunk(s)");
        return merged.Values.ToList();
    }

    /// <summary>
    /// Pure helper: chunk the user list. Internal for unit testing.
    /// </summary>
    internal static List<List<EnrichedUserInfo>> ChunkUsers(List<EnrichedUserInfo> users, int chunkSize)
    {
        if (chunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSize));
        var chunks = new List<List<EnrichedUserInfo>>();
        for (var i = 0; i < users.Count; i += chunkSize)
        {
            chunks.Add(users.GetRange(i, Math.Min(chunkSize, users.Count - i)));
        }
        return chunks;
    }

    private async Task<List<AIUserMatchResult>> ResolveSmartGroupChunkAsync(
        string groupDescription,
        List<EnrichedUserInfo> chunkUsers)
    {
        var userSummaries = chunkUsers.Select((u, idx) => $"{idx + 1}. {u.ToAISummary()}").ToList();
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

                return ParseUserMatchResponse(responseText, chunkUsers);
            }
        }
        catch (ClientResultException ex)
        {
            // Surface the actual HTTP response from Azure OpenAI (status code + body) so we can
            // diagnose throttling (429), auth (401/403), wrong deployment (404), payload size (413),
            // content-filter blocks, etc. The default ToString only includes the message.
            string? responseBody = null;
            try
            {
                responseBody = ex.GetRawResponse()?.Content?.ToString();
            }
            catch
            {
                // Ignore - response body may not be readable.
            }

            _logger.LogError(
                ex,
                "Azure OpenAI request failed for smart group resolution chunk (Status={Status}, Users={UserCount}, Deployment={Deployment}). Response body: {ResponseBody}",
                ex.Status,
                chunkUsers.Count,
                _config.DeploymentName,
                responseBody);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling AI Foundry for smart group resolution chunk (Users={UserCount}, Deployment={Deployment})", chunkUsers.Count, _config.DeploymentName);
            throw;
        }

        return new List<AIUserMatchResult>();
    }

    /// <summary>
    /// Reduce card JSON to a short text digest for prompt context.
    /// </summary>
    private static string SummariseCardContext(string? cardJson) =>
        AIPromptBudget.SummariseCard(cardJson, MaxCardContextChars);

    /// <summary>
    /// Keep the most recent history turns that fit the character budget.
    /// </summary>
    private static List<(string role, string message)> TrimHistory(
        List<(string role, string message)>? history, int maxChars) =>
        AIPromptBudget.TrimHistory(history, maxChars);

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
        List<(string role, string message)>? conversationHistory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userMessage);

        // Truncate before doing anything else: an unbounded paste would otherwise be billed
        // as tokens and then fail to persist against the 64 KB table property limit.
        if (userMessage.Length > MaxUserMessageChars)
        {
            _logger.LogInformation("Truncating {Length}-char message from {Upn} to {Max}",
                userMessage.Length, userUpn, MaxUserMessageChars);
            userMessage = userMessage[..MaxUserMessageChars];
        }

        _logger.LogDebug("Handling follow-up chat from {Upn}", userUpn);

        // Get the configurable system prompt
        var systemPrompt = await GetFollowUpChatSystemPromptAsync();

        // Summarise rather than embedding the raw card. Nudge templates are 7-8 KB and the
        // intro cards are ~94 KB (embedded base64 images), which would be ~24k tokens in a
        // single system prompt - and card text is not trusted input for system-level content.
        var cardSummary = SummariseCardContext(originalNudgeContext);
        if (!string.IsNullOrEmpty(cardSummary))
        {
            systemPrompt += $"\n\nThe original nudge message context was about: {cardSummary}";
        }

        try
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt)
            };

            // Add conversation history if available, trimmed to a character budget.
            foreach (var (role, message) in TrimHistory(conversationHistory, MaxHistoryChars))
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

            messages.Add(new UserChatMessage(userMessage));

            var options = new ChatCompletionOptions
            {
                MaxOutputTokenCount = 500, // Keep responses concise for chat
                Temperature = 0.7f
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(FollowUpTimeout);

            await FollowUpGate.WaitAsync(cts.Token);
            ChatCompletion? completion;
            try
            {
                var response = await _chatClient.CompleteChatAsync(messages, options, cts.Token);
                completion = response?.Value;
            }
            finally
            {
                FollowUpGate.Release();
            }

            if (completion?.Content != null && completion.Content.Count > 0)
            {
                var responseText = completion.Content[0].Text;

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
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Follow-up chat for {Upn} timed out after {Timeout}", userUpn, FollowUpTimeout);
            return new AIFollowUpResponse
            {
                Response = "Sorry, that took longer than expected. Please try again.",
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
            // Clean up the response - remove markdown code blocks if present.
            // Span-based: avoid Split → List<string> → Join allocations on every AI response.
            var cleaned = responseText.AsSpan().Trim();
            if (cleaned.StartsWith("```"))
            {
                // Drop the opening fence line (```json or ```).
                var firstNewline = cleaned.IndexOf('\n');
                if (firstNewline >= 0)
                {
                    cleaned = cleaned.Slice(firstNewline + 1);
                }
                else
                {
                    cleaned = default;
                }

                // Drop a trailing fence line if present (last line starting with ```).
                if (!cleaned.IsEmpty)
                {
                    // Strip trailing newline(s) so we can locate the final line cleanly.
                    var trimmedEnd = cleaned.TrimEnd();
                    var lastNewline = trimmedEnd.LastIndexOf('\n');
                    var lastLineStart = lastNewline + 1;
                    var lastLine = trimmedEnd.Slice(lastLineStart);
                    if (lastLine.TrimStart().StartsWith("```"))
                    {
                        cleaned = trimmedEnd.Slice(0, lastNewline < 0 ? 0 : lastNewline);
                    }
                    else
                    {
                        cleaned = trimmedEnd;
                    }
                }
            }

            var cleanedResponse = cleaned.ToString();

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
