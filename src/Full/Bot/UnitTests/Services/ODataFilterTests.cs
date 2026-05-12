using Engine.Storage;

namespace UnitTests.Services;

[TestClass]
public class ODataFilterTests
{
    [TestMethod]
    public void EscapeLiteral_NullOrEmpty_ReturnsEmptyString()
    {
        Assert.AreEqual(string.Empty, ODataFilter.EscapeLiteral(null));
        Assert.AreEqual(string.Empty, ODataFilter.EscapeLiteral(""));
    }

    [TestMethod]
    public void EscapeLiteral_NoQuote_ReturnsAsIs()
    {
        Assert.AreEqual("alice@contoso.com", ODataFilter.EscapeLiteral("alice@contoso.com"));
    }

    [TestMethod]
    public void EscapeLiteral_SingleQuote_IsDoubled()
    {
        Assert.AreEqual("o''connor@contoso.com", ODataFilter.EscapeLiteral("o'connor@contoso.com"));
    }

    [TestMethod]
    public void EscapeLiteral_MultipleQuotes_AllDoubled()
    {
        Assert.AreEqual("a''b''c", ODataFilter.EscapeLiteral("a'b'c"));
    }

    [TestMethod]
    public void EscapeLiteral_InjectionAttempt_IsNeutralized()
    {
        // A naive filter "RecipientUpn eq '{upn}'" with this value would break out
        // of the literal and inject extra OData clauses. After escaping the value
        // is a single string literal again.
        var raw = "x' or PartitionKey eq 'y";
        var escaped = ODataFilter.EscapeLiteral(raw);
        Assert.AreEqual("x'' or PartitionKey eq ''y", escaped);
    }
}
