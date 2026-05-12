using Microsoft.Bot.Schema;

namespace Engine.Notifications;

public interface IConversationResumeHandler<T>
{
    Task<(T?, Attachment)> LoadDataAndResumeConversation(string chatUserUpn);
}
