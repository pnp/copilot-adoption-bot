using Engine.Services;

namespace UnitTests.Services;

/// <summary>
/// Pure unit tests for <see cref="ConversationHistoryCodec"/>. Verifies the round-trip
/// serialization shape and that malformed / empty input is tolerated, so a bad row
/// in <c>ConversationCache</c> never breaks a live chat.
/// </summary>
[TestClass]
public class ConversationHistoryCodecTests
{
    [TestMethod]
    public void Roundtrip_PreservesOrderAndContent()
    {
        var history = new List<(string role, string message)>
        {
            ("user", "Hi there"),
            ("assistant", "Hello, how can I help?"),
            ("user", "Tell me about Copilot")
        };

        var json = ConversationHistoryCodec.Serialize(history);
        var roundtripped = ConversationHistoryCodec.Deserialize(json);

        CollectionAssert.AreEqual(history, roundtripped);
    }

    [TestMethod]
    public void Deserialize_NullOrBlank_ReturnsEmpty()
    {
        Assert.AreEqual(0, ConversationHistoryCodec.Deserialize(null).Count);
        Assert.AreEqual(0, ConversationHistoryCodec.Deserialize(string.Empty).Count);
        Assert.AreEqual(0, ConversationHistoryCodec.Deserialize("   ").Count);
    }

    [TestMethod]
    public void Deserialize_Malformed_ReturnsEmpty()
    {
        // Should never throw on bad input - a corrupted column must not break a live chat.
        Assert.AreEqual(0, ConversationHistoryCodec.Deserialize("{not json").Count);
        Assert.AreEqual(0, ConversationHistoryCodec.Deserialize("[\"oops\"]").Count);
    }

    [TestMethod]
    public void Serialize_EmptyList_ProducesValidJsonRoundtrip()
    {
        var json = ConversationHistoryCodec.Serialize(new List<(string role, string message)>());
        Assert.IsFalse(string.IsNullOrWhiteSpace(json));

        var roundtripped = ConversationHistoryCodec.Deserialize(json);
        Assert.AreEqual(0, roundtripped.Count);
    }

    [TestMethod]
    public void Serialize_NullThrows()
    {
        Assert.ThrowsException<ArgumentNullException>(() => ConversationHistoryCodec.Serialize(null!));
    }

    [TestMethod]
    public void Roundtrip_HandlesUnicodeAndQuotes()
    {
        var history = new List<(string role, string message)>
        {
            ("user", "What about o'connor@contoso.com?"),
            ("assistant", "Здравствуйте — \"quoted\" \\ slashes work too")
        };

        var roundtripped = ConversationHistoryCodec.Deserialize(ConversationHistoryCodec.Serialize(history));
        CollectionAssert.AreEqual(history, roundtripped);
    }
}
