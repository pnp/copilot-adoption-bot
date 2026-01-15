
# Send Teams Adaptive Card to a UPN (PowerShell)

This repository/script lets you send an **Adaptive Card** to a **specific Microsoft Teams user (by UPN/email)** using **Microsoft Graph (app-only)** from PowerShell. It creates (or reuses) a **1:1 chat** with the target user and posts the card as an attachment.

> **Note**: Messages are sent **as the app** (service principal), not as an end user.

---

## Contents
- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [Required API Permissions](#required-api-permissions)
- [Setup](#setup)
- [Usage](#usage)
  - [Send a default card](#send-a-default-card)
  - [Send a custom card from JSON](#send-a-custom-card-from-json)
- [Adaptive Card schema notes](#adaptive-card-schema-notes)
- [Chat ID caching](#chat-id-caching)
- [Troubleshooting](#troubleshooting)
- [Security recommendations](#security-recommendations)
- [FAQ](#faq)
- [Example adaptive card JSON](#example-adaptive-card-json)

---

## Overview
The script uses **Microsoft Graph** with **application permissions** to:
1. Resolve the target **user by UPN**.
2. **Create** (first time) or **reuse** (subsequently) a **one-on-one chat** with the user.
3. Send a **Teams message** with an **Adaptive Card** attachment.

This is ideal for **notifications**, **build/deployment status**, **alerts**, and other automation scenarios where you need to reach individuals directly in Teams.

## Prerequisites
- A **Microsoft 365 tenant** with Teams.
- **Entra ID (Azure AD) app registration** with a **client secret** (or certificate; the script shows secret use for simplicity).
- **Admin consent** for the Graph **application permissions** listed below.
- **PowerShell 7+** recommended (Windows PowerShell 5.1 also works) and the **Microsoft Graph PowerShell SDK** (the script installs it if missing).

## Required API Permissions
Grant the following **Application** permissions to your app registration and **Admin consent** them:

- `Chat.Create`
- `Chat.ReadWrite`
- `ChatMessage.Send`

> Depending on your tenant policies, you might also choose to grant `Chat.Read.All` to support some enumeration scenarios; this script avoids enumeration by caching chat IDs.

## Setup
1. **Create the app registration** in Entra ID (Azure AD) and note:
   - **Tenant ID**
   - **Client ID (Application ID)**
   - **Client Secret** (or set up a certificate and adjust the script accordingly)
2. **Add API permissions** (Application) listed above and click **Grant admin consent**.
3. Place the PowerShell script `Send-TeamsAdaptiveCardToUpn.ps1` alongside this README (or in your preferred directory).

## Usage

### Send a default card
```powershell
./Send-TeamsAdaptiveCardToUpn.ps1 `
  -TenantId "<TENANT_ID>" `
  -ClientId "<APP_ID>" `
  -ClientSecret "<APP_SECRET>" `
  -TargetUpn "user@contoso.com" `
  -UseDefaultCard
```

### Send a custom card from JSON
1. Author your Adaptive Card JSON (≤ v1.5) and save as `card.json`.
2. Run:
```powershell
./Send-TeamsAdaptiveCardToUpn.ps1 `
  -TenantId "<TENANT_ID>" `
  -ClientId "<APP_ID>" `
  -ClientSecret "<APP_SECRET>" `
  -TargetUpn "user@contoso.com" `
  -AdaptiveCardJsonPath ./card.json
```

> The script will **cache** the 1:1 chat ID in `chatCache.json` (same folder by default) to prevent duplicate chats and speed up subsequent sends.

## Adaptive Card schema notes
- Teams supports Adaptive Cards up to **v1.5** (check current platform docs if you need newer features).
- The script attaches the card with `contentType: application/vnd.microsoft.card.adaptive`.
- The message body is kept minimal because the **card content** is the primary UI payload.

## Chat ID caching
- A simple local JSON file (`chatCache.json`) maps `userId → chatId`.
- You can change the path via `-CachePath`. Store this in a persistent or shared location if running from automation (Azure Automation, GitHub Actions, etc.).

## Troubleshooting
- **403 Forbidden / Insufficient privileges**: Ensure **Admin consent** was granted for the app permissions.
- **User not found**: Verify the UPN/email and directory sync. The script calls `Get-MgUser -UserId <UPN>`.
- **Duplicate chats**: Reuse the cached `chatId` or ensure the cache path is consistent across runs.
- **Card doesn’t render**: Validate your JSON in the **Adaptive Cards Designer** and ensure the version is within Teams’ supported range.
- **Unknown app name/icon** in Teams: Configure your app branding (icon, name) in your Teams app manifest if distributing; otherwise messages may show as a generic app.

## Security recommendations
- **Do not hardcode secrets**. Prefer **Azure Key Vault**, environment variables, or a managed identity + certificate auth pattern.
- Use **least privilege** and limit who can manage the app registration and its secrets.
- Rotate secrets regularly and monitor **Graph audit logs**.

## FAQ
**Q: Does this send as a user or as an app?**
> As an **app** (service principal). It’s not user-impersonation.

**Q: Can I @mention the user inside the card?**
> Mentions render in the HTML message body via a mentions payload, not inside Adaptive Cards. If you need mentions, extend the script to include the mentions array in the message.

**Q: Can the recipient reply and trigger logic?**
> Replies go to the 1:1 chat with the app. For interactive bot logic (dialogs/commands), you would build a Bot Framework bot and optionally trigger it from automation.

**Q: Can I send to multiple users?**
> Yes—loop over UPNs, reuse the cache, and consider throttling/backoff for rate limits.

## Example adaptive card JSON
Save as `card.json` and use with `-AdaptiveCardJsonPath`.

```json
{
  "type": "AdaptiveCard",
  "version": "1.5",
  "body": [
    { "type": "TextBlock", "text": "Nightly Build Status", "weight": "Bolder", "size": "Medium" },
    { "type": "TextBlock", "text": "✅ Success", "wrap": true },
    {
      "type": "FactSet",
      "facts": [
        { "title": "Build #", "value": "2026.01.02.1" },
        { "title": "Finished", "value": "2026-01-02 14:30 UTC" }
      ]
    },
    {
      "type": "ActionSet",
      "actions": [
        {
          "type": "Action.OpenUrl",
          "title": "View pipeline",
          "url": "https://dev.azure.com/contoso/project/_build"
        }
      ]
    }
  ]
}
```

---

### Related
- Script file: `Send-TeamsAdaptiveCardToUpn.ps1`
- Cache file (optional): `chatCache.json`

If you need a multi-user/bulk send variant, or mentions, buttons with callbacks, or storage in Key Vault, open an issue or request enhancements.
