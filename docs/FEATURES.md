# Features

Detailed documentation of all Copilot Adoption Bot features.

## Table of Contents

- [Core Features](#core-features)
- [Copilot Usage Statistics](#copilot-usage-statistics)
- [Smart Groups](#smart-groups)
- [AI-Powered Conversations](#ai-powered-conversations)
- [User Cache Management](#user-cache-management)

## Core Features

### Template Management

Create and manage adaptive card templates for sending to users.

**Features:**
- Create templates with JSON adaptive card payloads
- Edit existing templates
- Delete templates
- Preview templates before sending
- Store large payloads in Azure Blob Storage (no 64KB limit)

**Storage Architecture:**
- Template metadata stored in Azure Table Storage
- JSON payloads stored in Azure Blob Storage
- Automatic blob container creation on first run

### Teams Bot Integration

Send adaptive cards directly to users via Teams bot conversations.

**Features:**
- Proactive messaging to users
- Interactive adaptive cards
- Bot installation management
- Activity feed notifications

**Requirements:**
- Teams bot registered in Teams Developer Portal
- `TeamsAppInstallation.ReadWriteForUser.All` permission
- Bot messaging endpoint configured

### Message Logging

Track message delivery status and recipients.

**Features:**
- Log every message send event
- Track delivery status (Sent, Failed, Pending)
- Record recipient information
- Timestamp all events
- Query logs by template, date, or recipient

**Storage:**
- Logs stored in Azure Table Storage
- Efficient queries with partition keys
- Automatic cleanup options

### User Cache with Delta Queries

Efficient user data caching with incremental synchronization from Microsoft Graph.

**Features:**
- Full user cache from Microsoft Graph
- Delta query support for incremental updates
- Cached metadata: department, manager, location
- Configurable refresh intervals
- Manual cache refresh option

**Benefits:**
- Reduces API calls to Microsoft Graph
- Faster user lookups
- Supports offline scenarios
- Tracks user changes over time

See [User Cache Management](#user-cache-management) for details.

### Authentication

Flexible authentication options for different scenarios.

**Supported Methods:**
- **Teams SSO**: Seamless authentication within Teams
- **MSAL Browser**: Standard Azure AD authentication for web
- **Service Principal**: Backend service authentication

**Features:**
- Single sign-on experience
- Token caching
- Automatic token refresh
- Multi-tenant support

### Modern UI

React-based web interface with Fluent UI components.

**Features:**
- Responsive design
- Light/dark theme support
- Teams theme integration
- Accessible components
- Real-time updates

**Technology:**
- React 18
- TypeScript
- Fluent UI v9
- Vite for fast builds

## Copilot Usage Statistics

Track and cache Microsoft 365 Copilot usage statistics for users in your tenant.

### Overview

The Copilot stats feature helps identify active Copilot users and enables targeted adoption campaigns based on actual usage data.

### Features

**Per-user activity tracking** across all Microsoft 365 Copilot surfaces:
- Microsoft 365 Copilot Chat
- Microsoft Teams Copilot
- Word Copilot
- Excel Copilot
- PowerPoint Copilot
- Outlook Copilot
- OneNote Copilot
- Loop Copilot

**Cached statistics** to reduce API calls and improve performance:
- Activity dates stored with user cache
- Configurable refresh intervals
- Manual update option

### Requirements

1. **Microsoft Graph Permission**: `Reports.Read.All` application permission with admin consent
2. **Copilot Licenses**: Microsoft 365 Copilot licenses assigned to users
3. **Active Usage**: The Microsoft Graph reports API only returns data for users with recent activity

### Configuration

Copilot stats are automatically enabled if the `Reports.Read.All` permission is granted.

**Configurable Settings:**

| Setting | Default | Description |
|---------|---------|-------------|
| `CopilotStatsPeriod` | D30 | Reporting period: D7, D30, D90, or D180 days |
| `CopilotStatsRefreshInterval` | 24 hours | How often to refresh statistics from Microsoft Graph |
| `CacheExpiration` | 1 hour | How long user cache remains valid before resync |

### Usage

#### Via Settings Page

1. Navigate to the Settings page in the web interface
2. In the "User Cache Management" section, click "Update Copilot Stats"
3. The system fetches the latest Copilot usage data from Microsoft Graph
4. Statistics are stored in Azure Table Storage with the user cache

**Clearing Stats (Force Refresh):**
1. Navigate to the Settings page
2. Click "Clear Copilot Stats"
3. This clears the last update timestamp, forcing a fresh data pull on the next update

#### Via API

```http
# Update Copilot stats
POST /api/UserCache/UpdateCopilotStats

# Clear stats metadata (force refresh)
POST /api/UserCache/ClearCopilotStats
```

#### Programmatically

```csharp
// Update stats
await userCacheManager.UpdateCopilotStatsAsync();

// Clear stats metadata to force refresh
var metadata = await userCacheManager.GetSyncMetadataAsync();
metadata.LastCopilotStatsUpdate = null;
await userCacheManager.UpdateSyncMetadataAsync(metadata);
```

### Use Cases

**Targeted Campaigns:**
- Send advanced tips to active users
- Send getting-started guides to new or inactive users
- Celebrate milestones with power users

**Adoption Insights:**
- Identify departments with low adoption
- Track adoption trends over time
- Measure campaign effectiveness

**Smart Targeting:**
- Combine with Smart Groups for AI-powered targeting
- Filter users by app-specific usage
- Target users based on usage patterns

### Data Privacy

- Statistics only include **activity dates**, not content or prompts
- Data retrieved from Microsoft's official reporting APIs
- Stored in your Azure Storage account under your control
- Same privacy and compliance policies as other Microsoft 365 reports

### Limitations

1. **Tenant Requirements**: Only available for tenants with Microsoft 365 Copilot licenses
2. **Data Delay**: Microsoft Graph reports typically have a 24-48 hour delay
3. **Permission Errors**: Without `Reports.Read.All` permission, the feature logs errors but doesn't impact core functionality
4. **Regional Availability**: Must be available in your tenant's region

### Troubleshooting

**"Reports.Read.All permission not granted" errors:**
1. Verify the permission is added in Azure Portal
2. Ensure admin consent has been granted
3. Wait 5-10 minutes for permission changes to propagate
4. Try calling the API again

**No data returned:**
- Verify your tenant has active Copilot licenses
- Check that users have actually used Copilot features
- Remember reports have a 24-48 hour data delay
- Verify the reporting period (D7, D30, etc.) has usage

**API rate limiting:**
- The service caches tokens and reuses them
- Default refresh interval is 24 hours to minimize API calls
- Consider increasing `CopilotStatsRefreshInterval` if needed

## Smart Groups

AI-powered dynamic user groups based on natural language descriptions.

### Overview

Smart Groups use Azure AI Foundry to create dynamic user groups using natural language. Instead of manually selecting users, describe who you want to target and let AI figure it out.

### Requirements

- Azure AI Foundry resource with a deployed model (e.g., GPT-4o)
- `AIFoundryConfig` configured in application settings

### Example Queries

- "All users in the Sales department"
- "Users who haven't used Copilot in the last 30 days"
- "Managers in the Engineering organization"
- "New employees hired in the last 60 days"

### How It Works

1. You enter a natural language description
2. AI analyzes your cached user data
3. AI returns matching users with reasoning
4. You review and confirm the selection
5. Send your message to the group

### Configuration

```json
{
  "AIFoundryConfig": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "DeploymentName": "gpt-4o-mini",
    "ApiKey": "your-api-key",
    "MaxTokens": 2000,
    "Temperature": 0.7
  }
}
```

### Benefits

- **Natural language**: No complex filter syntax
- **Flexible**: Combine multiple criteria easily
- **Exploratory**: Try different targeting approaches
- **Time-saving**: No manual user selection

### Limitations

- Requires AI Foundry subscription (additional cost)
- Accuracy depends on user cache metadata quality
- May require iteration to get desired results
- Currently supports only single-language queries

## AI-Powered Conversations

Optional Azure AI Foundry integration for intelligent bot responses.

### Overview

When enabled, the bot can engage in natural conversations with users about Copilot adoption, answer questions, and provide contextual guidance.

### Requirements

- Azure AI Foundry resource with a deployed model
- `AIFoundryConfig` configured in application settings

### Features

- **Conversational support**: Answer user questions about Copilot
- **Contextual help**: Provide guidance based on user's role and usage
- **Proactive tips**: Suggest relevant tips based on conversation
- **Feedback collection**: Gather user feedback through natural conversation

### Configuration

Same as Smart Groups - uses the same AI Foundry configuration.

### Use Cases

- **Help desk deflection**: Answer common Copilot questions
- **Onboarding**: Guide new users through Copilot features
- **Feedback**: Collect qualitative feedback from users
- **Discovery**: Help users discover relevant Copilot features

## User Cache Management

Efficient caching of user data from Microsoft Graph with delta query support.

### Overview

The user cache stores user information from Microsoft Graph in Azure Table Storage for fast lookups and reduced API calls.

### Features

**Full Cache:**
- All users from Microsoft Graph
- User metadata: display name, email, department, manager, location
- Job title and other profile information

**Delta Queries:**
- Incremental updates using Microsoft Graph delta queries
- Only syncs changed users
- Tracks delta tokens in sync metadata

**Automatic Refresh:**
- Configurable refresh intervals
- Manual refresh option
- Stale cache detection

### Cached Data

For each user, the cache stores:
- User principal name (UPN)
- Display name
- Email address
- Department
- Manager (UPN and name)
- Office location
- Job title
- Copilot usage statistics (if enabled)

### Storage

- Users stored in Azure Table Storage
- Partition key: First letter of UPN
- Row key: Full UPN
- Supports efficient queries

### API Endpoints

```http
# Full cache update
POST /api/UserCache/Update

# Get all cached users
GET /api/UserCache/GetUsers

# Get sync metadata (last sync time, delta token)
GET /api/UserCache/GetMetadata

# Clear cache
DELETE /api/UserCache/Clear
```

### Configuration

```json
{
  "UserCacheConfig": {
    "RefreshInterval": "01:00:00",  // 1 hour
    "EnableDeltaQueries": true,
    "CacheExpiration": "24:00:00"   // 24 hours
  }
}
```

### Benefits

- **Fast**: No API calls for cached data
- **Efficient**: Delta queries minimize data transfer
- **Reliable**: Works even if Graph API is temporarily unavailable
- **Cost-effective**: Reduces Microsoft Graph API calls

### Maintenance

**Manual Refresh:**
1. Navigate to Settings ? User Cache Management
2. Click "Refresh User Cache"
3. Wait for completion notification

**Automatic Refresh:**
- Runs on configurable schedule
- Checks cache age before refresh
- Uses delta queries when possible

**Clear Cache:**
- Deletes all cached users
- Resets delta token
- Next refresh will be full sync

## Next Steps

- **Usage**: Learn how to use these features in [USAGE.md](USAGE.md)
- **Setup**: Configure features in [SETUP.md](SETUP.md)
- **Security**: Review security implications in [SECURITY.md](SECURITY.md)
- **Troubleshooting**: Get help with [TROUBLESHOOTING.md](TROUBLESHOOTING.md)
