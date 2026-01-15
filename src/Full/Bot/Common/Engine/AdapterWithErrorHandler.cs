using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Builder.TraceExtensions;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.Logging;

namespace Common.Engine;

public class AdapterWithErrorHandler : CloudAdapter
{
    public AdapterWithErrorHandler(BotFrameworkAuthentication auth, ILogger<IBotFrameworkHttpAdapter> logger, ConversationState? conversationState = null)
        : base(auth, logger)
    {
        OnTurnError = async (turnContext, exception) =>
        {
            // Log any leaked exception from the application.
            // NOTE: In production environment, you should consider logging this to
            // Azure Application Insights. Visit https://aka.ms/bottelemetry to see how
            // to add telemetry capture to your bot.
            logger.LogError(exception, $"[OnTurnError] unhandled error : {exception.Message}");

            // Send a message to the user
            try
            {
#if DEBUG
                await turnContext.SendActivityAsync($"Oops, something unexpected happened - {exception.Message}. Here's some debug info, seeing as this is a debug build:");
                await turnContext.SendActivityAsync(exception.StackTrace);
#else
                await turnContext.SendActivityAsync("Oops, something unexpected happened and I hit a problem.");
                await turnContext.SendActivityAsync("Please check the error logged and try again.");
#endif
            }
            catch (Exception sendException)
            {
                // If we can't send the error message to the user (e.g., due to authentication issues),
                // just log it and continue. This prevents recursive errors in the error handler.
                logger.LogError(sendException, $"[OnTurnError] Failed to send error message to user: {sendException.Message}");
            }

            if (conversationState != null)
            {
                try
                {
                    // Delete the conversationState for the current conversation to prevent the
                    // bot from getting stuck in a error-loop caused by being in a bad state.
                    // ConversationState should be thought of as similar to "cookie-state" in a Web pages.
                    await conversationState.DeleteAsync(turnContext);
                }
                catch (Exception e)
                {
                    logger.LogError(e, $"Exception caught on attempting to Delete ConversationState : {e.Message}");
                }
            }

            // Send a trace activity, which will be displayed in the Bot Framework Emulator
            try
            {
                await turnContext.TraceActivityAsync("OnTurnError Trace", exception.Message, "https://www.botframework.com/schemas/error", "TurnError");
            }
            catch (Exception traceException)
            {
                // If we can't send the trace activity, just log it
                logger.LogError(traceException, $"[OnTurnError] Failed to send trace activity: {traceException.Message}");
            }
        };
    }
}
