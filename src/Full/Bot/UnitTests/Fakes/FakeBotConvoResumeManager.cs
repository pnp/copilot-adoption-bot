using Common.Engine.Notifications;

namespace UnitTests.Fakes;

/// <summary>
/// Test double for <see cref="IBotConvoResumeManager"/> that returns a configurable
/// <see cref="ConversationResumeResult"/> or throws a configured exception.
/// </summary>
public class FakeBotConvoResumeManager : IBotConvoResumeManager
{
    public ConversationResumeResult? Result { get; set; }
    public Exception? ThrowOnResume { get; set; }
    public List<string> ResumedUpns { get; } = new();

    public Task<ConversationResumeResult> ResumeConversation(string upn)
    {
        ResumedUpns.Add(upn);

        if (ThrowOnResume != null)
        {
            throw ThrowOnResume;
        }

        return Task.FromResult(Result ?? ConversationResumeResult.MessageSent(upn));
    }
}
