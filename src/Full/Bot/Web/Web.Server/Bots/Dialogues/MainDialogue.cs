using Engine;
using Engine.Config;
using Engine.Services;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Web.Server.Bots.Dialogues.Abstract;

namespace Web.Server.Bots.Dialogues;


/// <summary>
/// Entrypoint to all new conversations
/// </summary>
public class MainDialogue : CommonBotDialogue
{
    private readonly UserState _userState;
    private readonly AIFoundryService? _aiFoundryService;
    private readonly PendingCardLookupService? _pendingCardLookupService;
    private readonly ILogger<MainDialogue> _logger;

    const string CACHE_NAME_CONVO_STATE = "CACHE_NAME_CONVO_STATE";

    /// <summary>
    /// Setup dialogue flow
    /// </summary>
    public MainDialogue(BotConfig configuration, BotConversationCache botConversationCache, ILogger<MainDialogue> logger,
        BotActionsHelper botActionsHelper,
        UserState userState,
        AIFoundryService? aiFoundryService = null,
        PendingCardLookupService? pendingCardLookupService = null)
        : base(nameof(MainDialogue), botConversationCache, configuration)
    {
        _userState = userState;
        _aiFoundryService = aiFoundryService;
        _pendingCardLookupService = pendingCardLookupService;
        _logger = logger;

        AddDialog(new TextPrompt(nameof(TextPrompt)));

        AddDialog(new WaterfallDialog(nameof(WaterfallDialog),
        [
            NewChat
        ]));
        AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
        InitialDialogId = nameof(WaterfallDialog);
    }

    /// <summary>
    /// Main entry-point for bot new chat. User is either responding to the intro card or has said something to the bot.
    /// </summary>
    private async Task<DialogTurnResult> NewChat(WaterfallStepContext stepContext, CancellationToken cancellationToken)
    {
        // Get/set state
        var convoState = await GetConvoStateAsync(stepContext.Context);
        var userMessage = stepContext.Context.Activity.Text;
        var aadObjectId = stepContext.Context.Activity?.From?.AadObjectId;

        // Check if Copilot Connected mode is enabled for AI follow-up
        if (_aiFoundryService != null && !string.IsNullOrEmpty(userMessage))
        {
            try
            {
                _logger.LogInformation($"Processing follow-up chat via AI Foundry: {userMessage.Substring(0, Math.Min(50, userMessage.Length))}...");

                // The dialogue/UserState live in MemoryStorage and are wiped on app restart
                // or scale-out. Both the card JSON and the conversation history are
                // mirrored to BotConversationCache (table-backed) so AI follow-up keeps
                // context after a restart.
                var lastCardJson = convoState.LastCardSent?.CardJson;
                var conversationHistory = convoState.ConversationHistory;

                if ((string.IsNullOrEmpty(lastCardJson) || conversationHistory == null)
                    && !string.IsNullOrWhiteSpace(aadObjectId))
                {
                    var persisted = await _botConversationCache.GetCachedUserAsync(aadObjectId);
                    if (persisted != null)
                    {
                        // Rehydrate card context from the template reference rather than from a
                        // per-user copy of the rendered card. The card is identical for every
                        // recipient of a batch, so storing it per user duplicated the same few KB
                        // across the whole audience - and across bot state as well.
                        var templateId = convoState.LastCardSent?.TemplateId ?? persisted.LastCardTemplateId;
                        if (string.IsNullOrEmpty(lastCardJson) && !string.IsNullOrEmpty(templateId))
                        {
                            lastCardJson = await LoadTemplateJsonAsync(templateId);
                            convoState.LastCardSent = new LastCardInfo
                            {
                                TemplateName = convoState.LastCardSent?.TemplateName ?? persisted.LastCardTemplateName,
                                TemplateId = templateId,
                                CardJson = lastCardJson,
                                SentDate = convoState.LastCardSent?.SentDate ?? persisted.LastCardSentUtc
                            };
                            _logger.LogDebug("Resolved card context for {AadObjectId} from template {TemplateId}",
                                aadObjectId, templateId);
                        }

                        if (conversationHistory == null && !string.IsNullOrEmpty(persisted.ConversationHistoryJson))
                        {
                            conversationHistory = ConversationHistoryCodec.Deserialize(persisted.ConversationHistoryJson);
                            convoState.ConversationHistory = conversationHistory;
                            _logger.LogDebug("Rehydrated ConversationHistory ({Count} entries) for {AadObjectId} from BotConversationCache", conversationHistory.Count, aadObjectId);
                        }
                    }
                }

                conversationHistory ??= new List<(string role, string message)>();

                // Get AI response
                var aiResponse = await _aiFoundryService.HandleFollowUpChatAsync(
                    stepContext.Context.Activity?.From?.Id ?? string.Empty,
                    userMessage,
                    lastCardJson,
                    conversationHistory
                );

                // Update conversation history
                conversationHistory.Add(("user", userMessage));
                conversationHistory.Add(("assistant", aiResponse.Response));

                // Keep only last 10 exchanges to avoid token limits
                if (conversationHistory.Count > 20)
                {
                    conversationHistory = conversationHistory.Skip(conversationHistory.Count - 20).ToList();
                }
                convoState.ConversationHistory = conversationHistory;

                // Send AI response
                await SendMsg(stepContext.Context, aiResponse.Response);

                if (aiResponse.ShouldEndConversation)
                {
                    // Clear conversation history on natural end
                    convoState.ConversationHistory = null;
                }

                // Persist the (possibly cleared) history to table storage so it survives
                // restart / scale-out. UserState alone (MemoryStorage) is volatile.
                if (!string.IsNullOrWhiteSpace(aadObjectId))
                {
                    try
                    {
                        await _botConversationCache.SetConversationHistoryAsync(aadObjectId, convoState.ConversationHistory);
                    }
                    catch (Exception persistEx)
                    {
                        _logger.LogWarning(persistEx, "Failed to persist conversation history for {AadObjectId}", aadObjectId);
                    }
                }

                return await stepContext.EndDialogAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing AI follow-up chat");
                // Fall back to default response on error
            }
        }

        // Default response when AI is not enabled or no message
        await SendMsg(stepContext.Context!,
                        "Hi! I'm the Office Adoption Bot. I deliver important messages and tips to help you stay productive. " +
                        "If you have questions about a message I sent, feel free to reply!"
                     );
        return await stepContext.EndDialogAsync();
    }

    /// <summary>
    /// Load a template's JSON for AI context. Cached per template by
    /// <see cref="PendingCardLookupService"/>, so a batch's recipients share one blob read.
    /// </summary>
    private async Task<string?> LoadTemplateJsonAsync(string templateId)
    {
        if (_pendingCardLookupService == null) return null;

        try
        {
            var card = await _pendingCardLookupService.GetDeliveryCardAsync(string.Empty, string.Empty, templateId);
            return card?.CardJson;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load template {TemplateId} for AI context", templateId);
            return null;
        }
    }

    async Task<MainDialogueConvoState> GetConvoStateAsync(ITurnContext context)    {
        var convoStateProp = _userState.CreateProperty<MainDialogueConvoState>(CACHE_NAME_CONVO_STATE);
        var convoState = await convoStateProp.GetAsync(context);
        if (convoState == null)
        {
            convoState = new MainDialogueConvoState();
            await convoStateProp.SetAsync(context, convoState);
        }
        return convoState;
    }

}

internal class MainDialogueConvoState
{
    /// <summary>
    /// Conversation history for AI follow-up (role, message pairs)
    /// </summary>
    public List<(string role, string message)>? ConversationHistory { get; set; }

    /// <summary>
    /// Information about the last card sent to this user
    /// </summary>
    public LastCardInfo? LastCardSent { get; set; }
}

public class LastCardInfo
{
    public string? TemplateName { get; set; }
    public string? TemplateId { get; set; }

    /// <summary>
    /// Rendered card JSON. Deliberately <b>not</b> persisted into bot state - it is resolved
    /// on demand from <see cref="TemplateId"/> (cached per template), so the per-user state
    /// stays a few hundred bytes rather than several KB of duplicated card payload.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public string? CardJson { get; set; }

    public DateTime? SentDate { get; set; }
}
