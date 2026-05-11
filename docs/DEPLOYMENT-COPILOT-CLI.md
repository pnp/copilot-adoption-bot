# Deploying with GitHub Copilot CLI

This guide enables you to deploy the Copilot Adoption Bot entirely from the **GitHub Copilot CLI** terminal agent. The agent will create Azure resources, configure the app, build, deploy, and set up the Teams app — but it needs information from you first.

## Prerequisites

Before you start, make sure you have:

- [ ] **Azure CLI** (`az`) installed and logged in (`az login`)
- [ ] **Azure subscription** with Contributor access
- [ ] **.NET 10 SDK** installed
- [ ] **Node.js 20 LTS** installed
- [ ] **Microsoft 365 tenant** with Teams enabled
- [ ] **Global Admin or Application Admin** role in Entra ID (to grant Graph API consent)

## Two Ways to Run It

| Mode | When to use |
|------|-------------|
| **Interactive** | First-time deployment. The agent asks you each question and confirms choices. |
| **Config file** (recommended for repeat / scripted deployments) | You pre-fill a `deployment-config.json` and the agent uses it without asking. Anything missing or `null` will still be prompted. |

A complete template is in [`deployment-config.example.json`](deployment-config.example.json). Copy it to `deployment-config.json` at the repo root (the filename is gitignored) and fill in your values. If you'd rather keep it outside the repo entirely, just tell the agent the path when you prompt it.

## Information You'll Need to Provide

The agent will ask you for these values (or read them from your config file). Gather them beforehand to speed things up.

### 1. Azure Subscription & Location

| Item | Example | How to get it |
|------|---------|---------------|
| Azure Subscription name or ID | `My Subscription` | `az account list --output table` |
| Azure Region | `eastus` | Pick from `az account list-locations --output table` |
| Resource naming prefix | `copilotbot` | Your choice — used to name all resources consistently |

### 2. Bot Identity (Entra ID App Registration)

You have two options:

- **Option A — Let Copilot CLI create a new app registration** (recommended for new deployments). You just need your **Tenant ID** (`az account show --query tenantId`).
- **Option B — Use an existing bot** from the [Teams Developer Portal](https://dev.teams.microsoft.com/). Provide:

| Item | Example | Where to find it |
|------|---------|------------------|
| Bot App ID (Client ID) | `12345678-abcd-...` | Teams Developer Portal → Bot management → your bot |
| Bot Client Secret | `aBcDeFg...` | Generated when you created the bot (shown only once) |
| Tenant ID | `87654321-dcba-...` | Azure Portal → Entra ID → Overview |

### 3. Storage Authentication Choice

| Option | When to use | What's needed |
|--------|-------------|---------------|
| **RBAC + Managed Identity** (recommended) | Production deployments | Nothing extra — the agent assigns roles automatically |
| **Connection String** | Quick dev/test setups | Nothing extra — the agent retrieves the key |

### 4. Frontend (Web Admin Panel) Env Vars

The Vite frontend needs a `.env.local` file generated **before** the build. The agent writes this automatically from these values:

| Setting | Example |
|---------|---------|
| `VITE_MSAL_CLIENT_ID` | Web app's client ID (often same as bot) |
| `VITE_MSAL_AUTHORITY` | `https://login.microsoftonline.com/<tenantId>` |
| `VITE_MSAL_SCOPES` | `api://<clientId>/access_as_user` |
| `VITE_TEAMSFX_START_LOGIN_PAGE_URL` | `https://<app>.azurewebsites.net/auth-start` |

### 5. Optional Features

Tell the agent if you want any of these:

| Feature | What you'll need |
|---------|-----------------|
| **Azure AI Foundry** (AI-powered bot conversations) | An Azure OpenAI / AI Foundry endpoint, deployment name, and API key |
| **Application Insights** (monitoring) | Nothing extra — the agent creates it |
| **Azure Key Vault** (secret storage) | Nothing extra — the agent creates it |
| **Web Admin panel** (Teams tab app) | A second Entra app registration for web auth (or let the agent create one) |

### 6. Teams App Deployment

| Item | Example | Notes |
|------|---------|-------|
| Manifest source | `user` or `admin` | The repo has two templates under `src/Full/Teams Apps/` |
| Publish to org app catalog? | Yes / No | Requires Teams Admin role |
| Custom bot display name? | `Copilot Adoption Bot` | Shown in Teams |

---

## What the Agent Will Do

When you run one of the prompts below, the Copilot CLI agent will:

1. **Read config file** (if provided) — Load `deployment-config.json` and only prompt for missing values
2. **Create Azure resources** — Resource Group, App Service Plan, App Service (`.NET 10`), Storage Account
3. **Configure Entra ID** — Create or configure the app registration with required Graph permissions (`User.Read.All`, `TeamsActivity.Send`, `TeamsAppInstallation.ReadWriteForUser.All`, optionally `Reports.Read.All`); request admin consent
4. **Assign storage RBAC roles** (if RBAC chosen) — `Storage Blob Data Contributor`, `Storage Table Data Contributor`, `Storage Queue Data Contributor`
5. **Build frontend separately** — Write `.env.local` with `VITE_*` values, then `npm ci && npm run build` in `Web/web.client`
6. **Build & publish backend** — `dotnet publish Web/Web.Server/Web.Server.csproj -c Release -o ./publish`, then zip
7. **Deploy to App Service** — `az webapp deploy --src-path deploy.zip --type zip`
8. **Set app configuration** — All required app settings via `az webapp config appsettings set` (using `__` separator for nested keys)
9. **Tell you to configure bot endpoint** — Print the URL to paste into Teams Developer Portal
10. **Build Teams app package** — Substitute placeholders in `manifest-template.json`, package with icons into a `.zip`
11. **(Optional)** Create Application Insights, Key Vault, AI Foundry configuration
12. **(Optional)** Publish Teams app to the org catalog

> The Azure storage tables (`messagetemplates`, `messagebatches`, `messagelogs`, `ConversationCache`, `usercache`, `usersyncmetadata`, `smartgroups`, `smartgroupmembers`, `appsettings`), the blob container `message-templates`, and the queue `batch-messages` are auto-created by the application on first run — no need to provision them manually.

---

## The Prompts

### Option A — With a config file (no questions asked)

Copy `docs/deployment-config.example.json` to `deployment-config.json`, fill in your values, then paste this prompt:

```
Deploy the Copilot Adoption Bot to Azure using the values in deployment-config.json.
Follow docs/DEPLOYMENT-COPILOT-CLI.md.

1. Read deployment-config.json from the repo root. Validate that all required fields
   are present (azure.subscriptionId, azure.location, azure.resourcePrefix, bot.appId
   + bot.appPassword if bot.mode=existing, bot.tenantId). For any required value that
   is null/missing, prompt me — otherwise do not ask.
2. Confirm by printing a summary of what you're about to create, then proceed without
   further questions (unless an Azure command fails).
3. Execute the full deployment steps as described in the "What the Agent Will Do"
   section of docs/DEPLOYMENT-COPILOT-CLI.md.
4. Print a summary of all created resources, URLs, and remaining manual steps.
```

### Option B — Fully interactive

Use this if you don't want to fill in a JSON file first:

```
Deploy the Copilot Adoption Bot to Azure. Follow the guide in docs/DEPLOYMENT-COPILOT-CLI.md.

Walk me through it step-by-step:
1. Ask me for my Azure subscription, region, and a resource naming prefix.
2. Ask whether I have an existing bot app registration or need a new one (and gather
   bot.appId / bot.appPassword / bot.tenantId accordingly).
3. Ask whether to use RBAC (managed identity) or connection string for storage.
4. Ask which optional features I want (AI Foundry, App Insights, Key Vault, Web
   Admin panel).
5. If I want the Web Admin panel, gather VITE_MSAL_CLIENT_ID, VITE_MSAL_AUTHORITY,
   VITE_MSAL_SCOPES, VITE_TEAMSFX_START_LOGIN_PAGE_URL for the frontend build.
6. Then execute the full deployment:
   a. Create all Azure resources (resource group, app service plan with the right
      runtime — DOTNET|10.0 for Windows or DOTNETCORE|10.0 for Linux — app service,
      storage account, plus any optional resources).
   b. Create or configure the Entra ID app registration with the required Graph
      permissions (User.Read.All, TeamsActivity.Send,
      TeamsAppInstallation.ReadWriteForUser.All, and optionally Reports.Read.All).
      Request admin consent via `az ad app permission admin-consent`.
   c. If RBAC: enable system-assigned managed identity on the App Service and assign
      Storage Blob Data Contributor, Storage Table Data Contributor, and
      Storage Queue Data Contributor roles at the storage account scope.
   d. Build the frontend FIRST (it does NOT build automatically as part of dotnet
      publish on this repo's setup):
        cd src/Full/Bot/Web/web.client
        # Write a .env.local file with the VITE_* values gathered above
        npm ci
        npm run build
      Then build/publish the backend:
        cd src/Full/Bot
        dotnet publish Web/Web.Server/Web.Server.csproj -c Release -o ./publish
      Then zip the publish folder into deploy.zip.
   e. Deploy to App Service using `az webapp deploy --src-path deploy.zip --type zip`.
   f. Set all required app settings on the App Service via
      `az webapp config appsettings set` (use double-underscore syntax for nested
      keys — these are NOT connection strings):
        - MicrosoftAppId, MicrosoftAppPassword, MicrosoftAppType=SingleTenant
        - GraphConfig__ClientId, GraphConfig__ClientSecret, GraphConfig__TenantId
        - StorageAuthConfig__UseRBAC (or ConnectionStrings__Storage as a regular app
          setting — do NOT use `az webapp config connection-string set` because the
          app reads IConfiguration["ConnectionStrings:Storage"] directly and App
          Service would prepend CUSTOMCONNSTR_)
        - StorageAuthConfig__StorageAccountName (if RBAC)
        - WebAuthConfig__ClientId, WebAuthConfig__TenantId,
          WebAuthConfig__ApiAudience (if web admin panel)
        - AIFoundryConfig__Endpoint, AIFoundryConfig__DeploymentName,
          AIFoundryConfig__ApiKey (if AI Foundry)
        - APPLICATIONINSIGHTS_CONNECTION_STRING (if App Insights)
   g. Tell me to set the bot messaging endpoint in the Teams Developer Portal to
      https://<app-name>.azurewebsites.net/api/messages (this can't be reliably
      automated via az CLI).
   h. Build the Teams app package: copy
      `src/Full/Teams Apps/User/manifest-template.json` (or Admin/ for the admin
      tab), replace <<BOT_APP_ID>> with the bot app ID and any <<WEB_APP_ID>> /
      <<WEB_HTTPS_ROOT>> / <<WEB_DOMAIN>> placeholders for the admin manifest,
      save as manifest.json, then zip it with color.png and outline.png from the
      same folder.
   i. Tell me how to upload the Teams app package (Teams Developer Portal for
      personal testing, Teams Admin Center for org-wide).
7. After all steps, print a summary of every created resource (with resource IDs),
   all URLs, and any manual steps remaining (admin consent confirmation, bot
   endpoint, Teams upload).

If at any point you'd like me to fill in a JSON config file instead so you don't
have to ask each question, offer me docs/deployment-config.example.json as a
starting point.
```

---

## Manual Steps the Agent Cannot Automate

Some steps require portal access or admin privileges that CLI tools can't fully automate:

| Step | Why it's manual | Where |
|------|----------------|-------|
| **Grant admin consent** for Graph permissions | The agent will run `az ad app permission admin-consent`, but if you don't have admin rights it must be done via portal | Azure Portal → App Registrations → API Permissions |
| **Upload Teams app package** to org catalog | Requires Teams Admin | Teams Admin Center → Manage apps → Upload |
| **Set bot messaging endpoint** | Teams Developer Portal has no public API for this | [Teams Developer Portal](https://dev.teams.microsoft.com/) → Bot management |
| **Configure AI Foundry** | Requires an existing Azure OpenAI deployment | Azure Portal → AI Foundry |

---

## Config File Reference

The structure of `deployment-config.json` mirrors the questions above. See [`deployment-config.example.json`](deployment-config.example.json) for a fully commented template. Key sections:

| Section | Purpose |
|---------|---------|
| `azure` | Subscription, region, resource naming. **`subscriptionId`, `location`, `resourcePrefix` are required.** |
| `bot` | `mode: "existing"` or `"create-new"`. If `existing`: `appId`, `appPassword`, `tenantId` required. |
| `graph` | Graph API credentials. Set `useSameAsBot: true` to reuse the bot's app registration. |
| `storage` | `authMode: "rbac"` (recommended) or `"connectionString"`. |
| `webAuth` | Enable the web admin panel and configure its Entra app. |
| `frontend` | `VITE_*` env vars written to `.env.local` before `npm run build`. |
| `aiFoundry` | Optional — Azure OpenAI / AI Foundry for "Copilot Connected" mode. |
| `appInsights`, `keyVault` | Optional — toggle creation. |
| `teamsApp` | `manifestSource: "user"` or `"admin"`; whether to publish to the org catalog. |

> **Keep `deployment-config.json` out of git.** It contains client secrets. Add it to `.gitignore` or save it outside the repo and pass the path explicitly when prompting.

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| `DOTNET\|10.0` runtime not available | Try a different region, or use `DOTNETCORE\|10.0`. The .NET 10 stack is still rolling out. |
| Storage 403 Forbidden | If using RBAC, ensure managed identity is enabled and roles are assigned. Role assignments can take up to 5 minutes to propagate. |
| Bot not responding in Teams | Verify the messaging endpoint is `https://<app>/api/messages` in the Teams Developer Portal. |
| Graph API 401/403 | Ensure admin consent was granted for all required permissions. |
| Frontend not loading | Check that the App Service is running .NET 10 and the SPA files were included in the publish output. |
