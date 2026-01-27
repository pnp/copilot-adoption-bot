# GitHub Actions Deployment Guide

This guide walks you through setting up automated CI/CD for the Copilot Adoption Bot using GitHub Actions with OIDC authentication (no secrets stored in GitHub).

## Prerequisites

Before starting, ensure you have:

- [ ] A GitHub account
- [ ] Azure subscription with permissions to create resources and app registrations
- [ ] Azure resources created (see [Manual Deployment](DEPLOYMENT-MANUAL.md) steps 1-2)
- [ ] [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli) installed locally

---

## Overview

The GitHub Actions workflow:
1. **Triggers** on push to `main` branch, pull requests, or manual dispatch
2. **Builds** both .NET backend and React frontend
3. **Runs** unit tests with test result publishing
4. **Deploys** to Azure Web App using OIDC authentication (main branch only)

**Pipeline Location:** `.github/workflows/azure-deploy.yml`

---

## Step 1: Fork or Clone the Repository

You need your own copy of the repository to configure GitHub Actions secrets and run deployments.

### Option A: Fork (Recommended)

Forking is recommended because it:
- Keeps a link to the original repository for easy updates
- Allows you to pull in future updates and bug fixes
- Preserves the commit history

**To fork:**

1. Go to the [Copilot Adoption Bot repository](https://github.com/pnp/copilot-adoption-bot)
2. Click the **Fork** button (top right)
3. Select your GitHub account or organization
4. Wait for the fork to complete
5. Clone your fork locally:
   ```bash
   git clone https://github.com/YOUR_ORG/copilot-adoption-bot.git
   cd copilot-adoption-bot
   ```

**To pull future updates from the original repo:**
```bash
# Add the original repo as upstream (one-time setup)
git remote add upstream https://github.com/pnp/copilot-adoption-bot.git

# Fetch and merge updates
git fetch upstream
git merge upstream/main
```

### Option B: Create a New Repository

If you prefer a completely independent copy:

1. Clone the original repository:
   ```bash
   git clone https://github.com/pnp/copilot-adoption-bot.git
   cd copilot-adoption-bot
   ```

2. Create a new repository on GitHub (don't initialize with README)

3. Change the remote and push:
   ```bash
   git remote remove origin
   git remote add origin https://github.com/YOUR_ORG/your-repo-name.git
   git push -u origin main
   ```

> **Important**: Note your GitHub organization/username and repository name—you'll need these when configuring federated credentials in Step 3.

---

## Step 2: Create Azure AD App Registration

Create a dedicated app registration for GitHub Actions deployments.

### 2.1 Create the App Registration

```bash
# Create the app registration
az ad app create --display-name "GitHub-Actions-CopilotAdoptionBot"
```

Note the `appId` (client ID) from the output.

### 2.2 Create a Service Principal

```bash
# Create service principal for the app
az ad sp create --id <app-id>
```

Note the `id` (object ID) from the output.

---

## Step 3: Configure Federated Credentials

Federated credentials allow GitHub Actions to authenticate without storing secrets.

### 3.1 Create Federated Credential for Main Branch (Required)

This credential is **required** for the workflow to authenticate with Azure during deployments.

**Option A: Using Azure Portal**

1. Go to **Microsoft Entra ID** → **App registrations** → Your app
2. Navigate to **Certificates & secrets** → **Federated credentials**
3. Click **+ Add credential**
4. Select **GitHub Actions deploying Azure resources**
5. Configure:
   - **Organization**: Your GitHub organization or username
   - **Repository**: `copilot-adoption-bot` (your repo name)
   - **Entity type**: Branch
   - **Branch**: `main`
   - **Name**: `github-actions-main`
6. Click **Add**

**Option B: Using Azure CLI**

```bash
# Create federated credential for main branch
az ad app federated-credential create \
  --id <app-id> \
  --parameters '{
    "name": "github-actions-main",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:YOUR_ORG/copilot-adoption-bot:ref:refs/heads/main",
    "description": "GitHub Actions main branch deployment",
    "audiences": ["api://AzureADTokenExchange"]
  }'
```

> **Note**: Replace `YOUR_ORG/copilot-adoption-bot` with your actual GitHub organization/username and repository name. The subject must match exactly, including case.

### 3.2 (Optional) Create Federated Credential for Pull Requests

If you want to run deployments from pull requests:

```bash
az ad app federated-credential create \
  --id <app-id> \
  --parameters '{
    "name": "github-actions-pr",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:YOUR_ORG/copilot-adoption-bot:pull_request",
    "description": "GitHub Actions pull request",
    "audiences": ["api://AzureADTokenExchange"]
  }'
```

---

## Step 4: Grant Azure Permissions

The service principal needs permissions to deploy to your Azure resources.

### 4.1 Required Permissions

| Permission | Scope | Purpose |
|------------|-------|---------|
| **Contributor** | Resource Group | Deploy and manage App Service |

### 4.2 Assign Contributor Role

```bash
# Get your subscription ID
SUBSCRIPTION_ID=$(az account show --query id -o tsv)

# Get your resource group name
RESOURCE_GROUP="rg-copilot-adoption-bot"

# Assign Contributor role to the service principal
az role assignment create \
  --assignee <app-id> \
  --role Contributor \
  --scope /subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP
```

### 4.3 (Optional) Additional Permissions for Key Vault

If using Azure Key Vault with the deployment:

```bash
# Grant Key Vault access
az keyvault set-policy \
  --name my-copilot-bot-kv \
  --spn <app-id> \
  --secret-permissions get list
```

---

## Step 5: Configure GitHub Repository

### 5.1 Add Repository Secrets

Go to your GitHub repository → **Settings** → **Secrets and variables** → **Actions** → **Secrets** tab.

> **Important**: Add these as **Repository secrets**, not Environment secrets. The workflow does not use GitHub Environments.

Add the following secrets:

| Secret Name | Description | How to Get It |
|-------------|-------------|---------------|
| `AZURE_CLIENT_ID` | Application (client) ID | From Step 2.1 output |
| `AZURE_TENANT_ID` | Azure AD tenant ID | `az account show --query tenantId -o tsv` |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription ID | `az account show --query id -o tsv` |
| `VITE_MSAL_CLIENT_ID` | Client ID for frontend MSAL auth | Same as `MicrosoftAppId` (bot app registration) |
| `VITE_MSAL_AUTHORITY` | Azure AD authority URL | `https://login.microsoftonline.com/<your-tenant-id>` |
| `VITE_MSAL_SCOPES` | API scopes for access token | `api://<your-client-id>/access_as_user` |
| `VITE_TEAMSFX_START_LOGIN_PAGE_URL` | (Optional) Login redirect URL for Teams SSO | `https://<your-app-name>.azurewebsites.net/auth-start.html` |
| `TESTS_APPSETTINGS_JSON` | (Optional) Full appsettings.json for unit tests | See below |

### 5.1.1 Configure TESTS_APPSETTINGS_JSON for Unit Tests

The `TESTS_APPSETTINGS_JSON` secret enables the workflow to run integration tests against real Azure resources. If not configured, tests are skipped with a warning.

**To configure:**

1. See the [Configuration Guide](CONFIGURATION.md) for the full appsettings.json structure and all available options
2. Use [appsettings.example.json](../src/Full/Bot/UnitTests/appsettings.example.json) as a starting template
3. At minimum, you need `ConnectionStrings:Storage` and `GraphConfig` settings for tests to run

**To add the secret:**

1. Create your JSON configuration based on the example template
2. Go to **Settings** → **Secrets and variables** → **Actions** → **Secrets** tab
3. Click **New repository secret**
4. Name: `TESTS_APPSETTINGS_JSON`
5. Value: Paste the entire JSON (minified or formatted - both work)
6. Click **Add secret**

> **Tip**: You can minify the JSON before pasting. The workflow writes it to `appsettings.json` in the test project directory.

> **Security Note**: Use a dedicated test Azure Storage account and consider using a service principal with minimal Graph API permissions for testing.

### 5.2 Add Repository Variables

Go to **Settings** → **Secrets and variables** → **Actions** → **Variables** tab.

Add the following variables:

| Variable Name | Description | Example |
|---------------|-------------|---------|
| `AZURE_WEBAPP_NAME` | Your Azure App Service name | `copilot-adoption-bot-app` |


---

## Step 6: Workflow File Reference

The repository includes a pre-configured GitHub Actions workflow at `.github/workflows/azure-deploy.yml`. No changes are needed to the workflow file itself—just configure the secrets and variables from Step 5.

### What the Workflow Does

| Stage | Actions |
|-------|---------|
| **Build** | Checkout → Setup .NET 10 & Node.js 20 → Build frontend → Build backend → Run tests → Publish artifact |
| **Deploy** | Download artifact → Azure Login (OIDC) → Deploy to App Service |

### Required Configuration Summary

The workflow expects these secrets and variables (configured in Step 5):

**Secrets** (Settings → Secrets and variables → Actions → Secrets):
```
AZURE_CLIENT_ID                  # App registration client ID from Step 2
AZURE_TENANT_ID                  # Your Azure AD tenant ID
AZURE_SUBSCRIPTION_ID            # Your Azure subscription ID
VITE_MSAL_CLIENT_ID              # Client ID for frontend auth (same as MicrosoftAppId)
VITE_MSAL_AUTHORITY              # Azure AD authority (https://login.microsoftonline.com/<tenant-id>)
VITE_MSAL_SCOPES                 # API scopes (api://<client-id>/access_as_user)
VITE_TEAMSFX_START_LOGIN_PAGE_URL # (Optional) Login redirect URL for Teams SSO
```

**Variables** (Settings → Secrets and variables → Actions → Variables):
```
AZURE_WEBAPP_NAME        # Your Azure App Service name (e.g., copilot-adoption-bot-app)
```

**Optional Secret** (for running integration tests):
```
TESTS_APPSETTINGS_JSON   # Full appsettings.json content for unit tests
```

> **Note**: If `TESTS_APPSETTINGS_JSON` is not configured, tests are skipped with a warning. This is fine for initial deployment.

### Workflow Triggers

The workflow runs automatically on:
- **Push to `main`** → Builds and deploys
- **Pull request to `main`** → Builds only (no deployment)
- **Manual trigger** → Use "Run workflow" button in Actions tab

---

## Step 7: Verify the Setup

### 7.1 Run the Workflow

1. Push a change to the `main` branch, or
2. Go to **Actions** → **Build and Deploy to Azure** → **Run workflow**

### 7.2 Monitor the Workflow

1. Go to **Actions** tab in your repository
2. Click on the running workflow
3. Check each step for success/failure

### 7.3 Verify Deployment

After successful deployment:
- Open `https://your-app-name.azurewebsites.net`
- Check Application Insights for any errors
- Test the bot in Teams

---

## Workflow Triggers Summary

| Trigger | Build | Tests | Deploy |
|---------|-------|-------|--------|
| Push to `main` | ✅ | ✅* | ✅ |
| Pull request to `main` | ✅ | ✅* | ❌ |
| Manual (`workflow_dispatch`) | ✅ | ✅* | ✅ |

\* Tests only run if `TESTS_APPSETTINGS_JSON` secret is configured.

---

## Security Considerations

### OIDC vs. Secrets

This guide uses **OIDC (OpenID Connect)** authentication:

| Approach | Security | Maintenance |
|----------|----------|-------------|
| **OIDC (This Guide)** | ✅ No secrets stored in GitHub | Low - No rotation needed |
| **Service Principal Secret** | ⚠️ Secret stored in GitHub | High - Requires rotation |

### Principle of Least Privilege

- The service principal only has **Contributor** access to the specific **resource group**
- Consider using a dedicated resource group for the bot
- Review and audit role assignments regularly

### Branch Protection

Recommended branch protection rules for `main`:

1. **Require pull request reviews**
2. **Require status checks** (build must pass)
3. **Require branches to be up to date**
4. **Restrict who can push**

---

## Troubleshooting

### OIDC Authentication Fails

**Error**: `AADSTS700024: Client assertion is not within its valid time range`

**Solution**: Ensure your federated credential subject matches exactly:
- For branch: `repo:ORG/REPO:ref:refs/heads/BRANCH`
- For pull requests: `repo:ORG/REPO:pull_request`
- Check organization/repository name is correct (case-sensitive)

### Deployment Fails with 403

**Error**: `The client does not have authorization to perform action`

**Solution**:
1. Verify the service principal has Contributor role
2. Check the role assignment scope matches the resource group
3. Allow 5-10 minutes for role assignments to propagate

### Build Fails - .NET SDK Not Found

**Error**: `Unable to locate .NET SDK`

**Solution**: Ensure `.github/workflows/azure-deploy.yml` uses:
```yaml
- uses: actions/setup-dotnet@v4
  with:
    dotnet-version: '10.0.x'
```

### Build Fails - npm ci Error

**Error**: `npm ERR! The 'npm ci' command can only install with an existing package-lock.json`

**Solution**: Ensure `package-lock.json` is committed to the repository.

---

## Advanced Configuration

### Deploying to Multiple Environments

Create separate workflows or use matrix strategy:

```yaml
jobs:
  deploy:
    strategy:
      matrix:
        environment: [staging, production]
    environment: ${{ matrix.environment }}
    steps:
      # Use environment-specific variables
      - name: Deploy
        uses: azure/webapps-deploy@v3
        with:
          app-name: ${{ vars[format('AZURE_WEBAPP_NAME_{0}', matrix.environment)] }}
```

### Adding Deployment Slots

For zero-downtime deployments:

```yaml
- name: Deploy to Staging Slot
  uses: azure/webapps-deploy@v3
  with:
    app-name: ${{ vars.AZURE_WEBAPP_NAME }}
    slot-name: staging
    package: ./publish

- name: Swap Slots
  run: |
    az webapp deployment slot swap \
      --resource-group rg-copilot-adoption-bot \
      --name ${{ vars.AZURE_WEBAPP_NAME }} \
      --slot staging \
      --target-slot production
```

---

## Next Steps

- Review [Security Best Practices](SECURITY.md)
- Set up [Application Insights](DEPLOYMENT.md#estimated-costs) for monitoring
- Configure [Azure Key Vault](DEPLOYMENT.md#azure-key-vault-integration) for secrets
