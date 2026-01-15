using Common.Engine.Config;
using Common.Engine.Models;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Graph;

namespace Common.Engine;

/// <summary>
/// Utility methods for working with bot users.
/// </summary>
public static class BotUserUtils
{
    public static BotUser ParseBotUserInfo(this ChannelAccount user)
    {
        return string.IsNullOrEmpty(user.AadObjectId) 
            ? new BotUser { IsAzureAdUserId = false, UserId = user.Id } 
            : new BotUser { IsAzureAdUserId = true, UserId = user.AadObjectId };
    }

    public static async Task<BotUser> GetBotUserAsync(ITurnContext context, BotConfig botConfig, GraphServiceClient graphServiceClient)
    {
        return await GetBotUserAsync(context.Activity.From, botConfig, graphServiceClient);
    }
    
    public static async Task<BotUser> GetBotUserAsync(ChannelAccount channelUser, BotConfig botConfig, GraphServiceClient graphServiceClient)
    {
        BotUser botUser = ParseBotUserInfo(channelUser);
        return botUser;
    }
}

