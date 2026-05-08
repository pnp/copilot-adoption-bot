using Common.Engine.Services;
using Common.Engine.Storage;
using Microsoft.Bot.Schema;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests.Services;

/// <summary>
/// Pure unit tests for <see cref="PendingCardMaterializer"/>. Exercises the dedup behaviour and
/// the per-log error handling path without requiring Azure Storage.
/// </summary>
[TestClass]
public class PendingCardMaterializerTests
{
    private static MessageLogTableEntity Log(string id, string batchId, DateTime sentDate, string? recipient = null) =>
        new()
        {
            RowKey = id,
            MessageBatchId = batchId,
            SentDate = sentDate,
            RecipientUpn = recipient,
            Status = "Pending"
        };

    private static MessageBatchTableEntity Batch(string id, string templateId) =>
        new()
        {
            RowKey = id,
            BatchName = "b",
            TemplateId = templateId,
            SenderUpn = "sender@x.com",
            CreatedDate = DateTime.UtcNow
        };

    private static MessageTemplateTableEntity Template(string id, string name = "tpl") =>
        new()
        {
            RowKey = id,
            TemplateName = name,
            BlobUrl = "about:blank",
            CreatedByUpn = "creator@x.com",
            CreatedDate = DateTime.UtcNow
        };

    private static Attachment Attach(string json) => new()
    {
        ContentType = "application/vnd.microsoft.card.adaptive",
        Content = json
    };

    [TestMethod]
    public async Task Materialize_NullLogs_Throws()
    {
        await Assert.ThrowsExceptionAsync<ArgumentNullException>(() =>
            PendingCardMaterializer.MaterializeAsync(
                "u@x.com", null!,
                _ => Task.FromResult<MessageBatchTableEntity?>(null),
                _ => Task.FromResult<MessageTemplateTableEntity?>(null),
                _ => Task.FromResult(""),
                _ => Attach("")));
    }

    [TestMethod]
    public async Task Materialize_EmptyLogs_ReturnsEmpty()
    {
        var result = await PendingCardMaterializer.MaterializeAsync(
            "u@x.com", Array.Empty<MessageLogTableEntity>(),
            _ => Task.FromResult<MessageBatchTableEntity?>(null),
            _ => Task.FromResult<MessageTemplateTableEntity?>(null),
            _ => Task.FromResult(""),
            _ => Attach(""));
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task Materialize_DeduplicatesLookups()
    {
        var logs = new[]
        {
            Log("l1", "b1", new DateTime(2024, 1, 3)),
            Log("l2", "b1", new DateTime(2024, 1, 2)),
            Log("l3", "b1", new DateTime(2024, 1, 1)),
            Log("l4", "b2", new DateTime(2024, 1, 4))
        };

        int batchCalls = 0;
        int templateCalls = 0;
        int jsonCalls = 0;
        int attachCalls = 0;

        var result = await PendingCardMaterializer.MaterializeAsync(
            "u@x.com",
            logs,
            id => { batchCalls++; return Task.FromResult<MessageBatchTableEntity?>(Batch(id, "tpl-1")); },
            id => { templateCalls++; return Task.FromResult<MessageTemplateTableEntity?>(Template(id)); },
            id => { jsonCalls++; return Task.FromResult("{\"json\":\"" + id + "\"}"); },
            json => { attachCalls++; return Attach(json); });

        Assert.AreEqual(4, result.Count);
        // batch is keyed by message batch id, only 2 unique
        Assert.AreEqual(2, batchCalls);
        // template is keyed by template id, only 1 unique
        Assert.AreEqual(1, templateCalls);
        Assert.AreEqual(1, jsonCalls);
        // attachment is created for each log though (cheap, in-memory)
        Assert.AreEqual(4, attachCalls);

        // Newest-first ordering preserved
        CollectionAssert.AreEqual(
            new[] { "l4", "l1", "l2", "l3" },
            result.Select(r => r.MessageLogId).ToArray());
    }

    [TestMethod]
    public async Task Materialize_BatchNotFound_SkipsLogAndCachesNullResult()
    {
        var logs = new[]
        {
            Log("l1", "missing", DateTime.UtcNow.AddMinutes(-1)),
            Log("l2", "missing", DateTime.UtcNow.AddMinutes(-2)),
            Log("l3", "real", DateTime.UtcNow.AddMinutes(-3))
        };

        int batchCalls = 0;
        int templateCalls = 0;

        var result = await PendingCardMaterializer.MaterializeAsync(
            "u@x.com",
            logs,
            id =>
            {
                batchCalls++;
                return Task.FromResult<MessageBatchTableEntity?>(
                    id == "real" ? Batch(id, "t") : null);
            },
            id => { templateCalls++; return Task.FromResult<MessageTemplateTableEntity?>(Template(id)); },
            _ => Task.FromResult("{}"),
            Attach);

        // 2 unique batch ids -> 2 batch calls (null result also cached)
        Assert.AreEqual(2, batchCalls);
        Assert.AreEqual(1, templateCalls);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("l3", result[0].MessageLogId);
    }

    [TestMethod]
    public async Task Materialize_TemplateNotFound_SkipsLog()
    {
        var logs = new[] { Log("l1", "b", DateTime.UtcNow) };

        var result = await PendingCardMaterializer.MaterializeAsync(
            "u@x.com",
            logs,
            id => Task.FromResult<MessageBatchTableEntity?>(Batch(id, "t")),
            _ => Task.FromResult<MessageTemplateTableEntity?>(null),
            _ => Task.FromResult("{}"),
            Attach);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task Materialize_FallsBackToUpnWhenLogHasNoRecipient()
    {
        var logs = new[]
        {
            Log("l1", "b", DateTime.UtcNow, recipient: null),
            Log("l2", "b", DateTime.UtcNow.AddSeconds(-1), recipient: "explicit@x.com")
        };

        var result = await PendingCardMaterializer.MaterializeAsync(
            "fallback@x.com",
            logs,
            id => Task.FromResult<MessageBatchTableEntity?>(Batch(id, "t")),
            id => Task.FromResult<MessageTemplateTableEntity?>(Template(id)),
            _ => Task.FromResult("{}"),
            Attach);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("fallback@x.com", result[0].RecipientUpn);
        Assert.AreEqual("explicit@x.com", result[1].RecipientUpn);
    }

    [TestMethod]
    public async Task Materialize_PerLogException_IsCaught_AndOtherLogsContinue()
    {
        var logs = new[]
        {
            Log("good", "b1", DateTime.UtcNow),
            Log("bad", "b2", DateTime.UtcNow.AddSeconds(-1))
        };

        var captured = new List<(string id, string ex)>();

        var result = await PendingCardMaterializer.MaterializeAsync(
            "u@x.com",
            logs,
            id => id == "b2"
                ? throw new InvalidOperationException("boom")
                : Task.FromResult<MessageBatchTableEntity?>(Batch(id, "t")),
            id => Task.FromResult<MessageTemplateTableEntity?>(Template(id)),
            _ => Task.FromResult("{}"),
            Attach,
            (log, ex) => captured.Add((log.RowKey, ex.Message)));

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("good", result[0].MessageLogId);
        Assert.AreEqual(1, captured.Count);
        Assert.AreEqual("bad", captured[0].id);
        Assert.AreEqual("boom", captured[0].ex);
    }
}
