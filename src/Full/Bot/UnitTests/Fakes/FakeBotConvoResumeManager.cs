using Engine.Notifications;

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
    public List<(string Upn, string BatchId, string TemplateId)> ResumeCalls { get; } = new();

    public Task<ConversationResumeResult> ResumeConversation(string upn, string batchId, string templateId)
    {
        ResumedUpns.Add(upn);
        ResumeCalls.Add((upn, batchId, templateId));

        if (ThrowOnResume != null)
        {
            throw ThrowOnResume;
        }

        return Task.FromResult(Result ?? ConversationResumeResult.MessageSent(upn));
    }
}
