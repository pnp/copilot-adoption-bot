using Common.Engine.Notifications;

namespace UnitTests.Services;

/// <summary>
/// Pure unit tests for the <see cref="ConversationResumeResult"/> factory helpers.
/// </summary>
[TestClass]
public class ConversationResumeResultTests
{
    [TestMethod]
    public void MessageSent_SetsStatusAndUpn()
    {
        var result = ConversationResumeResult.MessageSent("user@contoso.com");

        Assert.AreEqual(ConversationResumeStatus.MessageSent, result.Status);
        Assert.IsTrue(result.Message.Contains("user@contoso.com"));
        Assert.IsNull(result.Exception);
    }

    [TestMethod]
    public void AppInstalled_SetsStatusAndUpn()
    {
        var result = ConversationResumeResult.AppInstalled("user@contoso.com");

        Assert.AreEqual(ConversationResumeStatus.AppInstalledPending, result.Status);
        Assert.IsTrue(result.Message.Contains("user@contoso.com"));
        Assert.IsNull(result.Exception);
    }

    [TestMethod]
    public void Failed_WithoutException_SetsStatusAndMessage()
    {
        var result = ConversationResumeResult.Failed("Boom");

        Assert.AreEqual(ConversationResumeStatus.Failed, result.Status);
        Assert.AreEqual("Boom", result.Message);
        Assert.IsNull(result.Exception);
    }

    [TestMethod]
    public void Failed_WithException_PreservesException()
    {
        var ex = new InvalidOperationException("nope");

        var result = ConversationResumeResult.Failed("Boom", ex);

        Assert.AreEqual(ConversationResumeStatus.Failed, result.Status);
        Assert.AreEqual("Boom", result.Message);
        Assert.AreSame(ex, result.Exception);
    }
}
