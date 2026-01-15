using AdaptiveCards;
using Common.DataUtils;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using System.Reflection;

namespace Common.Engine.Bot.Cards;

/// <summary>
/// Base implementation for any of the adaptive cards sent
/// </summary>
public abstract class BaseAdaptiveCard
{

    public abstract string GetCardContent();

    protected string ReadResource(string resourcePath)
    {
        return ResourceUtils.ReadResource(Assembly.GetExecutingAssembly(), resourcePath);
    }
    public Attachment GetCardAttachment()
    {
        dynamic cardJson = JsonConvert.DeserializeObject(GetCardContent()) ?? new { };

        return new Attachment
        {
            ContentType = AdaptiveCard.ContentType,
            Content = cardJson,
        };
    }
}
