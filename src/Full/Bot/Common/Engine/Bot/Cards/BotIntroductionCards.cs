namespace Common.Engine.Bot.Cards;

public abstract class BaseBotIntroductionCard : BaseAdaptiveCard
{
    public BaseBotIntroductionCard(string botName)
    {
        BotName = botName;
    }

    public string BotName { get; set; }


    public override string GetCardContent()
    {
        var json = ReadResource(BotConstants.BotFirstIntroduction);

        json = json.Replace(BotConstants.FIELD_NAME_BOT_NAME, BotName);

        return json;
    }

    public string ReplaceCommonFields(string json)
    {
        return json.Replace(BotConstants.FIELD_NAME_BOT_NAME, BotName);
    }
}

public class BotFirstIntroduction : BaseBotIntroductionCard
{
    public BotFirstIntroduction(string botName) : base(botName)
    {
    }

    public override string GetCardContent()
    {
        var json = ReplaceCommonFields(ReadResource(BotConstants.BotFirstIntroduction));
        return json;
    }
}

public class BotResumeConversationIntroduction : BaseBotIntroductionCard
{
    public BotResumeConversationIntroduction(string botName) : base(botName)
    {
    }

    public override string GetCardContent()
    {
        var json = ReplaceCommonFields(ReadResource(BotConstants.BotResumeConversationIntro));
        return json;
    }
}
