using Common.Engine.Notifications;
using Microsoft.Bot.Schema;

namespace Web.Server.Bots;

/// <summary>
/// Default implementation of conversation resume handler that sends a simple welcome back message.
/// </summary>
public class DefaultConversationResumeHandler : IConversationResumeHandler<string>
{
    public Task<(string?, Attachment)> LoadDataAndResumeConversation(string chatUserUpn)
    {
        var card = new HeroCard
        {
            Title = "Welcome Back!",
            Text = $"Hello {chatUserUpn}, how can I help you today?"
        }.ToAttachment();

        return Task.FromResult<(string?, Attachment)>((chatUserUpn, card));
    }
}
