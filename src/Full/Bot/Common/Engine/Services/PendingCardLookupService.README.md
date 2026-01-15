# PendingCardLookupService

## Overview

The `PendingCardLookupService` is a service class that queries Azure Table Storage to find pending adaptive cards that need to be sent to users. This service is particularly useful for bot conversation resume scenarios where you want to send users their pending messages when they interact with the bot.

## Location

- **Namespace**: `Common.Engine.Services`
- **File**: `Common.Engine\Services\PendingCardLookupService.cs`

## Features

- **Find Latest Pending Card**: Retrieves the most recent pending card for a specific user
- **Find All Pending Cards**: Retrieves all pending cards for a specific user
- **Automatic Template Resolution**: Automatically resolves batch and template information
- **Bot Framework Integration**: Returns cards as Bot Framework `Attachment` objects ready to send

## Dependencies

The service requires:
- `MessageTemplateStorageManager` - For accessing Azure Table Storage
- `ILogger<PendingCardLookupService>` - For logging

## Registration

The service is automatically registered when you call `AddMessageTemplateServices`:

```csharp
// In Program.cs or Startup.cs
services.AddMessageTemplateServices(config);
```

This registers `PendingCardLookupService` as a scoped service.

## Usage

### Basic Usage

```csharp
public class MyService
{
    private readonly PendingCardLookupService _pendingCardLookup;

    public MyService(PendingCardLookupService pendingCardLookup)
    {
        _pendingCardLookup = pendingCardLookup;
    }

    public async Task ProcessUserAsync(string userUpn)
    {
        // Get the latest pending card for this user
        var pendingCard = await _pendingCardLookup.GetLatestPendingCardByUpn(userUpn);

        if (pendingCard != null)
        {
            Console.WriteLine($"Found pending card: {pendingCard.TemplateName}");
            Console.WriteLine($"Batch ID: {pendingCard.BatchId}");
            Console.WriteLine($"Sent Date: {pendingCard.SentDate}");
            
            // The card is ready to send as a Bot Framework attachment
            // var attachment = pendingCard.CardAttachment;
        }
        else
        {
            Console.WriteLine("No pending cards found for this user");
        }
    }
}
```

### Bot Conversation Resume Handler

The most common use case is in a conversation resume handler. See `PendingCardConversationResumeHandler` for a complete example:

```csharp
public class PendingCardConversationResumeHandler : IConversationResumeHandler<PendingCardInfo>
{
    private readonly PendingCardLookupService _pendingCardLookupService;

    public PendingCardConversationResumeHandler(PendingCardLookupService pendingCardLookupService)
    {
        _pendingCardLookupService = pendingCardLookupService;
    }

    public async Task<(PendingCardInfo?, Attachment)> LoadDataAndResumeConversation(string chatUserUpn)
    {
        var pendingCard = await _pendingCardLookupService.GetLatestPendingCardByUpn(chatUserUpn);

        if (pendingCard != null)
        {
            // Return the pending card
            return (pendingCard, pendingCard.CardAttachment);
        }
        else
        {
            // Return a default card
            var defaultCard = new HeroCard
            {
                Title = "Welcome!",
                Text = $"Hello {chatUserUpn}, you have no pending messages."
            }.ToAttachment();

            return (null, defaultCard);
        }
    }
}
```

### Getting All Pending Cards

If you need to retrieve all pending cards (not just the latest):

```csharp
var allPendingCards = await _pendingCardLookup.GetAllPendingCardsByUpn(userUpn);

Console.WriteLine($"Found {allPendingCards.Count} pending cards");

foreach (var card in allPendingCards)
{
    Console.WriteLine($"- {card.TemplateName} (sent {card.SentDate})");
}
```

## PendingCardInfo Object

The service returns `PendingCardInfo` objects with the following properties:

| Property | Type | Description |
|----------|------|-------------|
| `MessageLogId` | `string` | Unique ID of the message log entry |
| `BatchId` | `string` | ID of the batch this message belongs to |
| `TemplateId` | `string` | ID of the adaptive card template |
| `TemplateName` | `string` | Display name of the template |
| `CardJson` | `string` | Raw JSON of the adaptive card |
| `CardAttachment` | `Attachment` | Bot Framework attachment ready to send |
| `SentDate` | `DateTime` | When the message was queued |
| `RecipientUpn` | `string` | UPN of the recipient |

## Integration with TeamsBot

To use this in your Teams bot, update the `OnMembersAddedAsync` method:

```csharp
protected override async Task OnMembersAddedAsync(
    IList<ChannelAccount> membersAdded, 
    ITurnContext<IConversationUpdateActivity> turnContext, 
    CancellationToken cancellationToken)
{
    foreach (var member in membersAdded)
    {
        if (member.Id != turnContext.Activity.Recipient.Id)
        {
            var userIdentity = await BotUserUtils.GetBotUserAsync(member, _configuration, graphServiceClient);

            // Check for pending cards
            var pendingCard = await _pendingCardLookupService.GetLatestPendingCardByUpn(userIdentity.UserPrincipalName);
            
            if (pendingCard != null)
            {
                // Send the pending card
                var activity = MessageFactory.Attachment(pendingCard.CardAttachment);
                await turnContext.SendActivityAsync(activity, cancellationToken);
            }
            else
            {
                // Send default introduction
                await helper.SendBotFirstIntro(turnContext, cancellationToken);
            }
        }
    }
}
```

## Query Performance

The service queries Azure Table Storage with the following filter:
```
PartitionKey eq 'MessageLogs' AND RecipientUpn eq '{upn}' AND Status eq 'Pending'
```

For optimal performance:
- The query is indexed on `PartitionKey`, `RecipientUpn`, and `Status`
- Results are ordered by `SentDate` in descending order (most recent first)
- Only records with status "Pending" are returned

## Error Handling

The service includes comprehensive error handling:
- Returns `null` if no pending cards are found
- Logs warnings if batches or templates are missing
- Returns empty list for `GetAllPendingCardsByUpn` on errors
- All exceptions are logged for debugging

## Logging

The service logs the following information:
- `Information`: Searches, results found
- `Warning`: Missing batches/templates, processing errors
- `Error`: Query failures, unexpected exceptions

## Related Classes

- **PendingCardConversationResumeHandler** (`Web.Server\Bots\PendingCardConversationResumeHandler.cs`) - Example conversation resume handler
- **MessageTemplateStorageManager** (`Common.Engine\MessageTemplateStorageManager.cs`) - Underlying storage manager
- **MessageLogTableEntity** (`Common.Engine\Storage\MessageStorageEntities.cs`) - Table storage entity for message logs

## Testing

Example unit test:

```csharp
[Fact]
public async Task GetLatestPendingCardByUpn_ReturnsPendingCard_WhenExists()
{
    // Arrange
    var upn = "user@example.com";
    var storageManager = new MessageTemplateStorageManager(connectionString, logger);
    var service = new PendingCardLookupService(storageManager, logger);

    // Create test data
    var template = await storageManager.SaveTemplate("Test Template", cardJson, "admin@example.com");
    var batch = await storageManager.CreateBatch("Test Batch", template.RowKey, "admin@example.com");
    await storageManager.LogMessageSend(batch.RowKey, upn, "Pending");

    // Act
    var result = await service.GetLatestPendingCardByUpn(upn);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(upn, result.RecipientUpn);
    Assert.Equal("Test Template", result.TemplateName);
}
```

## See Also

- [Message Template Service](MessageTemplateService.cs) - For managing templates and batches
- [Bot Conversation Resume Manager](../Notifications/BotConvoResumeManager.cs) - For resuming conversations
- [Azure Table Storage Documentation](https://docs.microsoft.com/en-us/azure/storage/tables/)
