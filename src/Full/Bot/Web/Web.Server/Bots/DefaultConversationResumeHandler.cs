using Engine.Notifications;
using Microsoft.Bot.Schema;

namespace Web.Server.Bots;

/// <summary>
/// Default implementation of conversation resume handler that sends a simple welcome back message.
/// </summary>
public class DefaultConversationResumeHandler : IConversationResumeHandler<string>
{
    public Task<(string?, Attachment)> LoadDeliveryAsync(string chatUserUpn, string batchId, string templateId) =>
        WelcomeBack(chatUserUpn);

    public Task<(string?, Attachment)> LoadNewestPendingAsync(string chatUserUpn) =>
        WelcomeBack(chatUserUpn);

    private static Task<(string?, Attachment)> WelcomeBack(string chatUserUpn)
    {
        var card = new HeroCard
        {
            Title = "Welcome Back!",
            Text = "Hello, how can I help you today?"
        }.ToAttachment();

        return Task.FromResult<(string?, Attachment)>((chatUserUpn, card));
    }
}
