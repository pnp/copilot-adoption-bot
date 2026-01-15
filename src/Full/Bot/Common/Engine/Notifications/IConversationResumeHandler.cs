using Microsoft.Bot.Schema;

namespace Common.Engine.Notifications;

public interface IConversationResumeHandler<T>
{
    Task<(T?, Attachment)> LoadDataAndResumeConversation(string chatUserUpn);
}
