using Engine;
using Microsoft.Bot.Schema;

namespace UnitTests.Services;

/// <summary>
/// Pure unit tests for <see cref="BotUserUtils.ParseBotUserInfo(ChannelAccount)"/>.
/// </summary>
[TestClass]
public class BotUserUtilsTests
{
    [TestMethod]
    public void ParseBotUserInfo_WithAadObjectId_UsesAadIdAndFlagsTrue()
    {
        var channel = new ChannelAccount
        {
            Id = "29:teamsChannelId",
            AadObjectId = "11111111-2222-3333-4444-555555555555"
        };

        var user = channel.ParseBotUserInfo();

        Assert.IsTrue(user.IsAzureAdUserId);
        Assert.AreEqual("11111111-2222-3333-4444-555555555555", user.UserId);
    }

    [TestMethod]
    public void ParseBotUserInfo_WithoutAadObjectId_FallsBackToChannelIdAndFlagsFalse()
    {
        var channel = new ChannelAccount
        {
            Id = "29:teamsChannelId",
            AadObjectId = null
        };

        var user = channel.ParseBotUserInfo();

        Assert.IsFalse(user.IsAzureAdUserId);
        Assert.AreEqual("29:teamsChannelId", user.UserId);
    }

    [TestMethod]
    public void ParseBotUserInfo_WithEmptyAadObjectId_FallsBackToChannelId()
    {
        var channel = new ChannelAccount
        {
            Id = "29:teamsChannelId",
            AadObjectId = string.Empty
        };

        var user = channel.ParseBotUserInfo();

        Assert.IsFalse(user.IsAzureAdUserId);
        Assert.AreEqual("29:teamsChannelId", user.UserId);
    }
}
