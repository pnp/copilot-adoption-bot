# Usage Guide

This guide covers how to use the Copilot Adoption Bot to create and send adaptive cards to users.

## Table of Contents

- [Managing Adaptive Card Templates](#managing-adaptive-card-templates)
- [Sending Messages to Users](#sending-messages-to-users)
- [Viewing Message Logs](#viewing-message-logs)
- [API Endpoints](#api-endpoints)

## Managing Adaptive Card Templates

### Creating a Template

1. **Access the Application**: Navigate to the web application and authenticate
2. **Navigate to Templates**: Go to the "Message Templates" page
3. **Create a Template**:
   - Click "New Template"
   - Enter a template name
   - Paste your adaptive card JSON payload
   - Click "Create"

### Example Adaptive Card JSON

```json
{
  "type": "AdaptiveCard",
  "version": "1.3",
  "body": [
    {
      "type": "TextBlock",
      "text": "💡 Copilot Tip: Use Specific Prompts",
      "size": "Large",
      "weight": "Bolder"
    },
    {
      "type": "TextBlock",
      "text": "Get better results from Copilot by being specific in your requests. Instead of 'summarize this', try 'create a 3-bullet executive summary focusing on budget and timeline'.",
      "wrap": true
    },
    {
      "type": "ActionSet",
      "actions": [
        {
          "type": "Action.OpenUrl",
          "title": "Learn More",
          "url": "https://aka.ms/copilot-prompts"
        }
      ]
    }
  ]
}
```

### Editing Templates

1. Navigate to the "Message Templates" page
2. Find the template you want to edit
3. Click the edit icon (pencil)
4. Modify the template name or JSON payload
5. Click "Save"

### Deleting Templates

1. Navigate to the "Message Templates" page
2. Find the template you want to delete
3. Click the delete icon (trash can)
4. Confirm the deletion

### Viewing Template JSON

1. Navigate to the "Message Templates" page
2. Click the eye icon next to the template
3. The JSON payload will be displayed in a modal

## Sending Messages to Users

Messages are sent via the Teams bot using the stored templates. 

### How It Works

When a message is sent, the bot:
1. Retrieves the template metadata from Azure Table Storage
2. Downloads the JSON payload from Azure Blob Storage
3. Parses the adaptive card JSON
4. Sends the card to specified users or channels
5. Logs the delivery status

### Storage Architecture

- **Template metadata** (name, creator, dates) is stored in Azure Table Storage for fast queries
- **JSON payloads** are stored in Azure Blob Storage (`message-templates` container) to handle large adaptive cards
- Each template's blob is named `{templateId}.json`
- Table storage references the blob URL for retrieval

### Sending to Individual Users

To send a nudge to specific users:
1. Navigate to the "Send Nudge" page
2. Select a template from the dropdown
3. Choose target users or groups
4. Click "Send Nudge"

### Sending to Groups

The bot supports several targeting options:
- **Individual users**: Select specific users by name
- **Smart groups**: AI-powered dynamic groups based on natural language (requires AI Foundry)
- **Copilot usage groups**: Target users based on their Copilot activity
- **All users**: Send to everyone in your tenant

## Viewing Message Logs

Navigate to the message logs section to view detailed delivery information.

### Log Information

Each log entry includes:
- **Timestamp**: When the message was sent
- **Template**: Which template was used
- **Recipient**: User who received the message
- **Status**: Delivery status (Sent, Failed, Pending)
- **Error details**: If delivery failed, error information is included

### Filtering Logs

Filter logs by:
- Date range
- Template
- Recipient
- Status

### Exporting Logs

1. Navigate to the message logs page
2. Apply desired filters
3. Click "Export to CSV"
4. The filtered logs will be downloaded

## API Endpoints

All API endpoints require authentication. See the [Setup Guide](SETUP.md) for authentication configuration.

### Message Template Management

#### Get All Templates
```http
GET /api/MessageTemplate/GetAll
```

Returns all templates with metadata (without JSON payloads).

**Response:**
```json
[
  {
    "id": "template-id",
    "name": "Welcome Message",
    "created": "2024-01-15T10:30:00Z",
    "createdBy": "user@domain.com",
    "modified": "2024-01-20T14:45:00Z",
    "modifiedBy": "user@domain.com"
  }
]
```

#### Get Specific Template
```http
GET /api/MessageTemplate/Get/{id}
```

Returns template metadata for a specific template.

#### Get Template JSON
```http
GET /api/MessageTemplate/GetJson/{id}
```

Returns the adaptive card JSON payload for a template.

**Response:**
```json
{
  "type": "AdaptiveCard",
  "version": "1.3",
  "body": [...]
}
```

#### Create Template
```http
POST /api/MessageTemplate/Create
Content-Type: application/json

{
  "name": "New Template",
  "jsonPayload": "{\"type\":\"AdaptiveCard\"...}"
}
```

Creates a new template and stores it in Azure Storage.

#### Update Template
```http
PUT /api/MessageTemplate/Update/{id}
Content-Type: application/json

{
  "name": "Updated Template",
  "jsonPayload": "{\"type\":\"AdaptiveCard\"...}"
}
```

Updates an existing template.

#### Delete Template
```http
DELETE /api/MessageTemplate/Delete/{id}
```

Deletes a template from Azure Storage (both metadata and JSON payload).

### Message Logging

#### Log Send Event
```http
POST /api/MessageTemplate/LogSend
Content-Type: application/json

{
  "templateId": "template-id",
  "recipientId": "user-id",
  "recipientName": "User Name",
  "status": "Sent"
}
```

Logs a message send event.

#### Get All Logs
```http
GET /api/MessageTemplate/GetLogs?startDate=2024-01-01&endDate=2024-01-31
```

Returns all message logs, optionally filtered by date range.

#### Get Logs by Template
```http
GET /api/MessageTemplate/GetLogsByTemplate/{templateId}
```

Returns all logs for a specific template.

### User Cache Management

#### Update User Cache
```http
POST /api/UserCache/Update
```

Triggers a full user cache update from Microsoft Graph.

#### Get Cached Users
```http
GET /api/UserCache/GetUsers
```

Returns all cached users with their metadata.

#### Update Copilot Stats
```http
POST /api/UserCache/UpdateCopilotStats
```

Updates Copilot usage statistics for all users (requires `Reports.Read.All` permission).

For detailed information on Copilot usage statistics, see [FEATURES.md](FEATURES.md#copilot-usage-statistics).

## Best Practices

### Template Design

1. **Keep it concise**: Users receive these in Teams chat - make them scannable
2. **Use clear CTAs**: Include action buttons with clear purpose
3. **Brand consistently**: Use your organization's colors and tone
4. **Test thoroughly**: Preview cards in the Adaptive Cards Designer before deploying

### Targeting

1. **Start small**: Test with a small group before rolling out widely
2. **Respect frequency**: Don't overwhelm users with too many nudges
3. **Use smart groups**: Leverage AI-powered targeting for relevant messages
4. **Track engagement**: Monitor logs to see what resonates

### Content Strategy

1. **Progressive learning**: Start with basics, build to advanced topics
2. **Contextual tips**: Send tips related to what users are working on
3. **Regular cadence**: Establish a predictable schedule (e.g., weekly tips)
4. **Mix content types**: Combine tips, prompts, announcements, and resources

## Adaptive Cards Resources

- [Adaptive Cards Designer](https://adaptivecards.io/designer/) - Visual designer for creating cards
- [Adaptive Cards Documentation](https://docs.microsoft.com/adaptive-cards/) - Official documentation
- [Adaptive Cards Samples](https://adaptivecards.io/samples/) - Example cards for inspiration
- [Teams Adaptive Cards](https://docs.microsoft.com/microsoftteams/platform/task-modules-and-cards/cards/cards-reference#adaptive-card) - Teams-specific features

## Next Steps

- **Features**: Learn about all features in [FEATURES.md](FEATURES.md)
- **Security**: Review security best practices in [SECURITY.md](SECURITY.md)
- **Troubleshooting**: Get help with [TROUBLESHOOTING.md](TROUBLESHOOTING.md)
