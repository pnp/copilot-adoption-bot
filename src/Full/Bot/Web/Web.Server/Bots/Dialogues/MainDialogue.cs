using Common.Engine;
using Common.Engine.Config;
using Common.Engine.Services;
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
    private readonly ILogger<MainDialogue> _logger;

    const string CACHE_NAME_CONVO_STATE = "CACHE_NAME_CONVO_STATE";

    /// <summary>
    /// Setup dialogue flow
    /// </summary>
    public MainDialogue(BotConfig configuration, BotConversationCache botConversationCache, ILogger<MainDialogue> logger,
        BotActionsHelper botActionsHelper,
        UserState userState,
        AIFoundryService? aiFoundryService = null)
        : base(nameof(MainDialogue), botConversationCache, configuration)
    {
        _userState = userState;
        _aiFoundryService = aiFoundryService;
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

        // Check if Copilot Connected mode is enabled for AI follow-up
        if (_aiFoundryService != null && !string.IsNullOrEmpty(userMessage))
        {
            try
            {
                _logger.LogInformation($"Processing follow-up chat via AI Foundry: {userMessage.Substring(0, Math.Min(50, userMessage.Length))}...");
                
                // Build conversation history from state
                var conversationHistory = convoState.ConversationHistory ?? new List<(string role, string message)>();
                
                // Get AI response
                var aiResponse = await _aiFoundryService.HandleFollowUpChatAsync(
                    stepContext.Context.Activity.From.Id,
                    userMessage,
                    convoState.LastNudgeContext,
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
                        "Hi! I'm the Office Nudge bot. I deliver important messages and tips to help you stay productive. " +
                        "If you have questions about a message I sent, feel free to reply!"
                     );
        return await stepContext.EndDialogAsync();
    }

    async Task<MainDialogueConvoState> GetConvoStateAsync(ITurnContext context)
    {
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
    public string RandomStateVal { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Context about the last nudge sent to this user (for AI follow-up)
    /// </summary>
    public string? LastNudgeContext { get; set; }
    
    /// <summary>
    /// Conversation history for AI follow-up (role, message pairs)
    /// </summary>
    public List<(string role, string message)>? ConversationHistory { get; set; }
}
