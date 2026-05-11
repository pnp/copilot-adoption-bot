using Common.Engine.Notifications;
using Common.Engine.Services;
using Common.Engine.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTests.Fakes;

namespace UnitTests.Services;

[TestClass]
public class MessageSenderServiceTests
{
    private static BatchQueueMessage NewQueueMessage(string logId = "log-1", string upn = "user@contoso.com")
        => new()
        {
            BatchId = "batch-1",
            MessageLogId = logId,
            RecipientUpn = upn,
            TemplateId = "template-1"
        };

    private static MessageSenderService BuildService(
        FakeBotConvoResumeManager resume,
        FakeMessageLogStatusWriter writer)
        => new(resume, writer, NullLogger<MessageSenderService>.Instance);

    [TestMethod]
    public async Task SendMessageAsync_MessageSent_UpdatesLogToSuccess_AndReturnsSuccess()
    {
        var resume = new FakeBotConvoResumeManager
        {
            Result = ConversationResumeResult.MessageSent("user@contoso.com")
        };
        var writer = new FakeMessageLogStatusWriter();
        var service = BuildService(resume, writer);

        var result = await service.SendMessageAsync(NewQueueMessage());

        Assert.IsTrue(result.Success);
        Assert.AreEqual("log-1", result.MessageLogId);
        Assert.AreEqual("user@contoso.com", result.RecipientUpn);
        Assert.IsNull(result.ErrorMessage);

        Assert.AreEqual(1, writer.Updates.Count);
        Assert.AreEqual("log-1", writer.Updates[0].LogId);
        Assert.AreEqual("Success", writer.Updates[0].Status);
        Assert.IsNull(writer.Updates[0].LastError);
    }

    [TestMethod]
    public async Task SendMessageAsync_AppInstalledPending_DoesNotUpdateLog_AndReturnsSuccess()
    {
        var resume = new FakeBotConvoResumeManager
        {
            Result = ConversationResumeResult.AppInstalled("user@contoso.com")
        };
        var writer = new FakeMessageLogStatusWriter();
        var service = BuildService(resume, writer);

        var result = await service.SendMessageAsync(NewQueueMessage());

        // Treated as success at the queue level; status stays Pending.
        Assert.IsTrue(result.Success);
        Assert.AreEqual(0, writer.Updates.Count, "Pending status must not be overwritten when the app is freshly installed");
    }

    [TestMethod]
    public async Task SendMessageAsync_Failed_UpdatesLogToFailed_AndReturnsFailure()
    {
        var resume = new FakeBotConvoResumeManager
        {
            Result = ConversationResumeResult.Failed("user not in tenant")
        };
        var writer = new FakeMessageLogStatusWriter();
        var service = BuildService(resume, writer);

        var result = await service.SendMessageAsync(NewQueueMessage());

        Assert.IsFalse(result.Success);
        Assert.AreEqual("user not in tenant", result.ErrorMessage);

        Assert.AreEqual(1, writer.Updates.Count);
        Assert.AreEqual("Failed", writer.Updates[0].Status);
        Assert.AreEqual("user not in tenant", writer.Updates[0].LastError);
    }

    [TestMethod]
    public async Task SendMessageAsync_ResumeThrows_UpdatesLogToFailed_AndReturnsFailureWithExceptionMessage()
    {
        var resume = new FakeBotConvoResumeManager
        {
            ThrowOnResume = new InvalidOperationException("graph blew up")
        };
        var writer = new FakeMessageLogStatusWriter();
        var service = BuildService(resume, writer);

        var result = await service.SendMessageAsync(NewQueueMessage());

        Assert.IsFalse(result.Success);
        Assert.AreEqual("graph blew up", result.ErrorMessage);
        Assert.AreEqual(1, writer.Updates.Count);
        Assert.AreEqual("Failed", writer.Updates[0].Status);
        Assert.AreEqual("graph blew up", writer.Updates[0].LastError);
    }

    [TestMethod]
    public async Task SendMessageAsync_ResumeThrows_AndStatusWriteAlsoThrows_StillReturnsOriginalErrorWithoutThrowing()
    {
        var resume = new FakeBotConvoResumeManager
        {
            ThrowOnResume = new InvalidOperationException("graph blew up")
        };
        var writer = new FakeMessageLogStatusWriter
        {
            ThrowOnUpdate = new InvalidOperationException("storage blew up")
        };
        var service = BuildService(resume, writer);

        var result = await service.SendMessageAsync(NewQueueMessage());

        Assert.IsFalse(result.Success);
        Assert.AreEqual("graph blew up", result.ErrorMessage,
            "Secondary failure to persist Failed status must not mask the original send error.");
    }
}
