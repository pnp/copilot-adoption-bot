using Engine.Notifications;
using Engine.Services;
using Engine.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.Fakes;

namespace UnitTests.Services;

[TestClass]
public class MessageSenderServiceTests
{
    private const string BatchId = "batch-1";
    private const string Upn = "Alice@Contoso.com";

    private static BatchQueueMessage QueueMessage() => new()
    {
        BatchId = BatchId,
        DeliveryPartitionKey = DeliveryKey.PartitionFor(BatchId, Upn),
        DeliveryRowKey = DeliveryKey.RowKeyFor(Upn),
        RecipientUpn = Upn,
        TemplateId = "template-1"
    };

    private static MessageSenderService CreateService(
        FakeBotConvoResumeManager resume, FakeMessageLogStatusWriter writer) =>
        new(resume, writer, NullLogger<MessageSenderService>.Instance);

    [TestMethod]
    public async Task SendMessageAsync_PassesBatchAndTemplateSoTheRightCardIsSent()
    {
        var resume = new FakeBotConvoResumeManager();
        var writer = new FakeMessageLogStatusWriter();

        await CreateService(resume, writer).SendMessageAsync(QueueMessage());

        // Regression guard: the send path must address the exact queued delivery. Looking up
        // "newest pending for this UPN" sends the wrong card when a user has several queued.
        var call = resume.ResumeCalls.Single();
        Assert.AreEqual(Upn, call.Upn);
        Assert.AreEqual(BatchId, call.BatchId);
        Assert.AreEqual("template-1", call.TemplateId);
    }

    [TestMethod]
    public async Task SendMessageAsync_OnDelivery_MarksExactDeliveryKeyAndClearsPending()
    {
        var resume = new FakeBotConvoResumeManager { Result = ConversationResumeResult.MessageSent(Upn) };
        var writer = new FakeMessageLogStatusWriter();
        var msg = QueueMessage();

        var result = await CreateService(resume, writer).SendMessageAsync(msg);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(SendDisposition.Delivered, result.Disposition);

        var update = writer.Updates.Single();
        Assert.AreEqual(msg.DeliveryPartitionKey, update.PartitionKey);
        Assert.AreEqual(msg.DeliveryRowKey, update.RowKey);
        Assert.AreEqual("Success", update.Status);

        // Must be removed from the pending index or the user gets it again next time they
        // open Teams.
        Assert.AreEqual((Upn, BatchId), writer.ClearedPending.Single());
    }

    [TestMethod]
    public async Task SendMessageAsync_AppInstalled_RecordsAwaitingInstallNotSuccess()
    {
        var resume = new FakeBotConvoResumeManager { Result = ConversationResumeResult.AppInstalled(Upn) };
        var writer = new FakeMessageLogStatusWriter();

        var result = await CreateService(resume, writer).SendMessageAsync(QueueMessage());

        Assert.AreEqual(SendDisposition.AwaitingInstall, result.Disposition);
        Assert.AreEqual("AwaitingInstall", writer.Updates.Single().Status);

        // Still pending delivery, so it must stay in the index.
        Assert.AreEqual(0, writer.ClearedPending.Count);
    }

    [TestMethod]
    public async Task SendMessageAsync_PermanentFailure_RecordsFailed()
    {
        var resume = new FakeBotConvoResumeManager { Result = ConversationResumeResult.Failed("user not licensed") };
        var writer = new FakeMessageLogStatusWriter();

        var result = await CreateService(resume, writer).SendMessageAsync(QueueMessage());

        Assert.IsFalse(result.Success);
        Assert.AreEqual(SendDisposition.PermanentFailure, result.Disposition);
        Assert.AreEqual("Failed", writer.Updates.Single().Status);
        Assert.AreEqual("user not licensed", writer.Updates.Single().LastError);
    }

    [TestMethod]
    public async Task SendMessageAsync_TransientFailure_DoesNotWriteTerminalStatus()
    {
        var resume = new FakeBotConvoResumeManager { Result = ConversationResumeResult.Transient("429 throttled") };
        var writer = new FakeMessageLogStatusWriter();

        var result = await CreateService(resume, writer).SendMessageAsync(QueueMessage());

        Assert.IsFalse(result.Success);
        Assert.AreEqual(SendDisposition.TransientFailure, result.Disposition);

        // A throttle must not be recorded as a permanent failure - the delivery stays queued
        // for retry rather than being silently dropped.
        Assert.AreEqual(0, writer.Updates.Count);
        Assert.AreEqual(0, writer.ClearedPending.Count);
    }

    [TestMethod]
    public async Task SendMessageAsync_UnhandledException_TreatedAsTransient()
    {
        var resume = new FakeBotConvoResumeManager { ThrowOnResume = new HttpRequestException("socket closed") };
        var writer = new FakeMessageLogStatusWriter();

        var result = await CreateService(resume, writer).SendMessageAsync(QueueMessage());

        Assert.AreEqual(SendDisposition.TransientFailure, result.Disposition);
        Assert.AreEqual(0, writer.Updates.Count, "Unhandled errors must be retried, not recorded as failed");
    }

    [TestMethod]
    public async Task RecordExhaustedAsync_MarksFailedAndIncrementsCounter()
    {
        var resume = new FakeBotConvoResumeManager();
        var writer = new FakeMessageLogStatusWriter();
        var msg = QueueMessage();

        await CreateService(resume, writer).RecordExhaustedAsync(msg, "gave up");

        Assert.AreEqual("Failed", writer.Updates.Single().Status);
        Assert.AreEqual(new FakeMessageLogStatusWriter.CounterUpdate(BatchId, 0, 1), writer.CounterUpdates.Single());
    }

    [TestMethod]
    public async Task DeliveryKey_IsCaseInsensitiveForRecipient()
    {
        var resume = new FakeBotConvoResumeManager { Result = ConversationResumeResult.MessageSent(Upn) };
        var writer = new FakeMessageLogStatusWriter();

        await CreateService(resume, writer).SendMessageAsync(QueueMessage());

        // Table keys are case-sensitive but UPNs are not; the row key must be normalised so
        // Alice@... and alice@... are one delivery, not two.
        Assert.AreEqual("alice@contoso.com", writer.Updates.Single().RowKey);
    }
}
