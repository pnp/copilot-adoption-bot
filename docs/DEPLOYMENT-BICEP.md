# Deploy with Bicep

Infrastructure-as-code deployment. This is the recommended path: it is repeatable,
reviewable and idempotent, unlike copying `az` commands out of a markdown file.

| | |
|---|---|
| **Best for** | Any environment you'll rebuild, share or audit |
| **Entry points** | `deploy/Deploy-AdoptionBot.ps1` (operator) or the **Provision Azure Infrastructure** workflow (CI) |
| **Source of truth** | `deploy/main.bicep` — both entry points use it |

> Prefer the older hand-run path? [Manual Deployment](DEPLOYMENT-MANUAL.md) still documents
> every `az` command individually.

---

## What it creates

| Resource | Notes |
|---|---|
| App Service Plan | SKU configurable (default `B1`) |
| App Service | .NET 10, **64-bit**, HTTPS-only, TLS 1.2, FTPS disabled, health check on `/health`, system-assigned managed identity |
| Storage account | StorageV2, HTTPS-only, **shared key access disabled**, no public blob access |
| Application Insights | Optional (`appInsights.enabled`) |
| Role assignments | Storage **Blob**, **Table** and **Queue** Data Contributor, granted to the App Service's managed identity |

### What it deliberately does *not* create

- **Entra app registrations** (bot, Graph, web auth). These require admin consent and are
  covered in [Setup Guide](SETUP.md). Create them first and record the ids/secrets in your
  config.
- **Tables, blob containers and queues.** The application creates these on first run —
  see [Deployment Overview](DEPLOYMENT.md) for the full list.
- **`alwaysOn`.** Intentionally left unset, because it is governed by policy in some
  environments. See [Scaling Guide](SCALING.md) §7 for how background work is built to
  tolerate the worker being unloaded.

---

## Option A — operator script

```powershell
# 1. Create the app registrations first (docs/SETUP.md)

# 2. Copy and fill in the config
cp docs/deployment-config.example.json deployment-config.json

# 3. Preview what will change
./deploy/Deploy-AdoptionBot.ps1 -ConfigPath ./deployment-config.json -WhatIf

# 4. Apply
./deploy/Deploy-AdoptionBot.ps1 -ConfigPath ./deployment-config.json
```

The script validates prerequisites (Azure CLI, Bicep, sign-in, required config values),
creates the resource group if needed, deploys the template, then builds and publishes the
application. Re-running is safe.

**Useful switches**

| Switch | Effect |
|---|---|
| `-WhatIf` | Preview infrastructure changes; nothing is applied |
| `-SkipCodeDeploy` | Provision infrastructure only |
| `-ResourceGroup` / `-SubscriptionId` | Override the config file |

> `deployment-config.json` contains secrets. It is **not** committed — keep it local or
> use a secret store.

## Option B — CI

Run the **Provision Azure Infrastructure** workflow from the Actions tab. Set `mode` to
`what-if` first to preview, then re-run with `deploy`.

It authenticates with **OIDC federated credentials**, so no cloud secret is stored in
GitHub. Required repository secrets:

| Secret | Purpose |
|---|---|
| `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` | OIDC login |
| `BOT_APP_ID`, `BOT_APP_PASSWORD`, `BOT_TENANT_ID` | Bot identity |
| `GRAPH_CLIENT_ID`, `GRAPH_CLIENT_SECRET`, `GRAPH_TENANT_ID` | Graph identity |

The `deploy` mode targets the `production` GitHub environment, so you can require an
approval before infrastructure changes are applied.

Provisioning is **separate** from the existing **Build and Deploy to Azure** workflow,
which handles routine code deployments and assumes the resources already exist.

---

## After deployment

The template provisions Azure resources and configures the app. It does **not** create Entra
app registrations or register the bot with Teams — those need admin consent and portal steps
with no public API.

Work through these in order. **Until steps 1–2 are done the bot cannot send or receive
anything in Teams, and until step 3 is done nobody can sign in to the admin UI** — even though
`/health` returns `Healthy` and the site loads.

### 1. Register the bot in the Teams Developer Portal

The deployment gives you a *web app*, not a *bot*. Teams only routes messages to bots
registered in [Teams Developer Portal](https://dev.teams.microsoft.com/) → **Tools** →
**Bot management**.

Either:

- **Create a new bot** there and use the app registration it generates — then update
  `bot.appId` / `bot.appPassword` in your config and redeploy, or
- **Attach an existing app registration** (the one referenced in your config) to a new bot.

See [Setup Guide §1](SETUP.md#1-create-a-bot-in-teams-developer-portal).

### 2. Set the messaging endpoint

In the same bot's settings, set the endpoint to:

```
https://<appServiceName>.azurewebsites.net/api/messages
```

Both deployment entry points print this URL when they finish.

### 3. Configure the app registration for the admin UI

The backend validates admin-UI tokens using `WebAuthConfig`. Whichever registration that
points at (by default the bot's) **must** have:

| Requirement | Value |
|---|---|
| Application ID URI | `api://<clientId>` |
| Delegated scope | `access_as_user` |
| SPA redirect URIs | `https://<appServiceName>.azurewebsites.net` and `https://localhost:5173` for local dev |

Without the scope, the SPA requests a token that cannot be issued and sign-in fails with no
useful error. See [Setup Guide §Web Authentication Setup](SETUP.md#web-authentication-setup).

> The deploy script writes `.env.local` (`VITE_MSAL_CLIENT_ID`, `VITE_MSAL_AUTHORITY`,
> `VITE_MSAL_SCOPES`, `VITE_TEAMSFX_START_LOGIN_PAGE_URL`) **before** the frontend build,
> because Vite inlines those values at build time. If you build the client by hand, do the
> same — otherwise the UI ships with no MSAL configuration.

### 4. Grant admin consent for Graph

Application permissions: `User.Read.All`, `TeamsActivity.Send`,
`TeamsAppInstallation.ReadWriteForUser.All`, and `Reports.Read.All` (optional, for Copilot
usage statistics). See [Setup Guide §2](SETUP.md#2-configure-graph-permissions-for-the-bots-entra-id-app).

### 5. Build and upload the Teams app package

Then set `AppCatalogTeamAppId` on the App Service. **Leave it unset and the bot cannot install
itself for users who have never messaged it** — those nudges will not be delivered. See
[Setup Guide §Teams App Deployment](SETUP.md#teams-app-deployment).

---

## Automated vs manual, at a glance

| | |
|---|---|
| ✅ Automated | App Service Plan, App Service (64-bit, health check, HTTPS-only, MSI), storage (RBAC, shared keys off), App Insights, the three storage role assignments, all app settings, frontend `.env.local`, code build and publish |
| ❌ Manual | Entra app registrations, Teams Developer Portal bot, messaging endpoint, `access_as_user` scope and SPA redirect URIs, admin consent, Teams app package, `AppCatalogTeamAppId` |

Storage tables, blob containers and queues are created by the application on first run.

---

## Verifying

```powershell
# App is up
curl https://<appServiceName>.azurewebsites.net/health

# Managed identity has the three storage data roles
$principalId = az webapp identity show -g <rg> -n <app> --query principalId -o tsv
az role assignment list --assignee $principalId --all `
  --query "[].roleDefinitionName" -o tsv
```

Expect `Storage Blob Data Contributor`, `Storage Table Data Contributor` and
`Storage Queue Data Contributor`.

> Role assignments can take a minute or two to propagate. If the app logs storage
> authorisation failures immediately after a first deployment, restart it once the roles
> appear.

---

## Related

- [Setup Guide](SETUP.md) — app registrations and Graph permissions (**do this first**)
- [Deployment Overview](DEPLOYMENT.md) — all deployment paths and the resource inventory
- [Configuration Reference](CONFIGURATION.md) — every setting
- [Scaling Guide](SCALING.md) — capacity model and the hosting decision
