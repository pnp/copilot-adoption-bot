using Common.Engine;
using Common.Engine.Bot.Cards;
using Common.Engine.Config;
using Common.Engine.Models;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;

namespace Web.Server.Bots.Dialogues.Abstract;

public abstract class CommonBotDialogue : ComponentDialog
{
    protected readonly BotConversationCache _botConversationCache;
    protected readonly BotConfig _botConfig;

    public CommonBotDialogue(string id, BotConversationCache botConversationCache, BotConfig botConfig)
        : base(id)
    {
        _botConversationCache = botConversationCache;
        _botConfig = botConfig;
    }

    protected async Task<CachedUserAndConversationData?> GetCachedUser(BotUser botUser)
    {
        await _botConversationCache.PopulateMemCacheIfEmpty();

        var chatUser = _botConversationCache.GetCachedUser(botUser.UserId);
        return chatUser;
    }

    protected async Task<DialogTurnResult> PromptWithCard(WaterfallStepContext stepContext, BaseAdaptiveCard card)
    {
        return await PromptWithCard(stepContext, card.GetCardAttachment());
    }
    protected async Task<DialogTurnResult> PromptWithCard(WaterfallStepContext stepContext, Attachment attachment)
    {
        var opts = new PromptOptions { Prompt = new Activity { Attachments = new List<Attachment>() { attachment }, Type = ActivityTypes.Message } };
        return await stepContext.PromptAsync(nameof(TextPrompt), opts);
    }

    protected async Task SendMsg(ITurnContext context, string msg)
    {
        await context.SendActivityAsync(BuildMsg(msg));
    }
    protected Activity BuildMsg(string msg)
    {
        return MessageFactory.Text(msg, msg, InputHints.ExpectingInput);
    }
}
