# Deploying with GitHub Copilot CLI

This guide enables you to deploy the Copilot Adoption Bot entirely from the **GitHub Copilot CLI** terminal agent. The agent will create Azure resources, configure the app, build, deploy, and set up the Teams app — but it needs information from you first.

## TL;DR — Quickest Start

Already have a bot app registration, an Azure subscription, and Copilot CLI installed?

```powershell
# 1. Copy the config template and fill in your values
Copy-Item docs/deployment-config.example.json deployment-config.json

# 2. Launch Copilot CLI in this repo and paste:
#    "Deploy the Copilot Adoption Bot using deployment-config.json.
#     Follow docs/DEPLOYMENT-COPILOT-CLI.md."
```

The agent will read your config, create everything, build, deploy, and print a summary at the end. Manual steps (admin consent, bot endpoint, Teams app upload) are surfaced explicitly so you don't miss them.

If you don't have a config file yet, [Option B below](#option-b--fully-interactive) walks you through every question.

---

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
        - AIFoundryConfig__Endpoint, AIFoundryConfig__DeploymentName (if AI
          Foundry — auth is Azure RBAC only, so grant the App Service Managed
          Identity a role such as `Cognitive Services OpenAI User` on the AI
          Foundry resource; no API key)
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

## Redeploying / Updating

To push a new build of the same app, run this prompt:

```
Redeploy the Copilot Adoption Bot to the existing App Service named <app-name> in
resource group <resource-group>:
1. Build the frontend: cd src/Full/Bot/Web/web.client && npm ci && npm run build
2. Publish the backend: cd src/Full/Bot && dotnet publish Web/Web.Server/Web.Server.csproj -c Release -o ./publish
3. Zip the publish folder and run `az webapp deploy --src-path deploy.zip --type zip`
4. Tail the logs with `az webapp log tail` and confirm the app started cleanly.
```

The agent will skip Azure resource creation and just rebuild & redeploy. App settings, RBAC roles, and the Entra app registration are preserved.

> **App settings changed?** If you added a new config key (e.g., enabling AI Foundry), include it in the prompt: *"...and set AIFoundryConfig\_\_Endpoint and AIFoundryConfig\_\_DeploymentName on the app before redeploying, and grant the App Service Managed Identity `Cognitive Services OpenAI User` on the AI Foundry resource (AI Foundry uses Azure RBAC only — no API key)."*

---

## Tearing Down

To delete everything created by a deployment, use this prompt:

```
Tear down the Copilot Adoption Bot deployment:
1. Delete the resource group <resource-group> with `az group delete --yes --no-wait`.
   That removes the App Service, plan, storage account, App Insights and Key Vault.
2. Delete the Entra app registration(s) created for the bot and (if any) web admin
   panel: `az ad app delete --id <appId>`. Ask me first which apps were created so
   we don't delete a shared one.
3. Remind me to remove the Teams app from the org catalog (Teams Admin Center →
   Manage apps → ... → Delete) and from any users' personal scope if it was
   sideloaded.
```

> ⚠️ The agent should always confirm the resource group name and app registration IDs before deleting anything.

---

## Troubleshooting

### Azure CLI / deployment issues

| Issue | Solution |
|-------|----------|
| `DOTNET\|10.0` runtime not available in region | The .NET 10 stack is rolling out regionally. Try `eastus`, `westeurope`, `westus3`, or fall back to `DOTNETCORE\|10.0`. |
| `WebAppName ... is not available` | App Service names are globally unique. Use a more specific prefix (e.g., add tenant initials or a random suffix). |
| Storage account name rejected | Must be 3-24 chars, lowercase letters + digits only, globally unique. The agent should generate one automatically; tell it your preferred prefix. |
| `az login` works but commands fail with `AuthorizationFailed` | Confirm you're on the right subscription: `az account show`. Switch with `az account set --subscription "..."`. |
| `az ad app create` fails with `Insufficient privileges` | You need the **Application Administrator** or **Cloud Application Administrator** role to create app registrations. Ask your tenant admin or use an existing app registration (set `bot.mode: "existing"`). |
| `az role assignment create` fails | The signed-in user needs `Microsoft.Authorization/roleAssignments/write` (Owner or User Access Administrator) at the storage account scope. |
| Deploy step says "Conflict" / 409 | A previous deployment is still in progress. Wait 1-2 minutes, then retry. |

### Runtime issues after deployment

| Issue | Solution |
|-------|----------|
| Storage 403 Forbidden | If using RBAC, ensure managed identity is enabled (`az webapp identity show`) and all three roles (Blob/Table/Queue Data Contributor) are assigned. Role assignments can take up to 5 minutes to propagate. |
| Bot not responding in Teams | Verify the messaging endpoint is `https://<app>/api/messages` in the Teams Developer Portal. Check `az webapp log tail` for incoming requests. |
| Graph API 401/403 | Ensure admin consent was granted for all required permissions. Re-run `az ad app permission admin-consent --id <appId>` if needed. Note: requires Application Administrator role. |
| Frontend not loading (blank page or 404 on `/`) | Check the publish output included `wwwroot/index.html`. If the frontend wasn't built before `dotnet publish`, the SPA files will be missing. Rebuild and redeploy. |
| `appsettings.json` values aren't taking effect | Confirm you used `__` (double underscore) for nested keys in `az webapp config appsettings set`, not `:` or `.`. |
| AI Foundry calls fail | Check the deployment name matches exactly (case-sensitive) and the endpoint includes the trailing `/`. |

### Config-file issues

| Issue | Solution |
|-------|----------|
| Agent says "config file missing" | The agent looks for `deployment-config.json` at the repo root by default. If yours is elsewhere, pass the full path in your prompt. |
| Agent re-asks values that are in the file | Check the JSON is valid (`Get-Content deployment-config.json \| ConvertFrom-Json` on Windows, or `jq . deployment-config.json` on Unix). A single trailing comma will silently invalidate it. |
| Secrets accidentally committed | Rotate them immediately (bot client secret, storage key, AI Foundry key) and use `git filter-repo` or BFG to scrub history. Then double-check `.gitignore` includes `deployment-config.json`. |

