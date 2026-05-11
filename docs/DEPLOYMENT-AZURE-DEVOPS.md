# Azure DevOps Deployment Guide

This guide walks you through setting up automated CI/CD for the Copilot Adoption Bot using Azure DevOps Pipelines.

## Prerequisites

Before starting, ensure you have:

- [ ] An Azure DevOps organization and project
- [ ] Azure subscription with permissions to create resources
- [ ] Azure resources created (see [Manual Deployment](DEPLOYMENT-MANUAL.md) steps 1-2)
- [ ] Repository imported/connected to Azure DevOps

---

## Overview

The Azure DevOps pipeline:
1. **Triggers** on push to `main` branch
2. **Builds** both .NET backend and React frontend
3. **Runs** unit tests with automatic result publishing
4. **Deploys** to Azure Web App using service connection

**Pipeline Location:** `.azure-pipelines/azure-deploy.yml`

---

## Step 1: Create Azure Service Connection

The service connection allows Azure DevOps to deploy to your Azure subscription.

### 1.1 Navigate to Service Connections

1. Go to your Azure DevOps project
2. Click **Project Settings** (bottom left)
3. Under **Pipelines**, select **Service connections**
4. Click **New service connection**

### 1.2 Choose Authentication Method

**Option A: Workload Identity Federation (Recommended)**

More secure, no secret rotation required.

1. Select **Azure Resource Manager**
2. Choose **Workload identity federation (automatic)**
3. Configure:
   - **Scope level**: Resource Group
   - **Subscription**: Select your Azure subscription
   - **Resource group**: Select your resource group (e.g., `rg-copilot-adoption-bot`)
   - **Service connection name**: `AzureServiceConnection`
4. Check **Grant access permission to all pipelines** (or manage individually)
5. Click **Save**

**Option B: Service Principal (Manual)**

Use when automatic creation is not possible.

1. Select **Azure Resource Manager**
2. Choose **Service principal (manual)**
3. You'll need to [create a service principal first](#appendix-creating-a-service-principal)
4. Enter:
   - **Subscription Id**: Your Azure subscription ID
   - **Subscription Name**: Your subscription name
   - **Service Principal Id**: The app ID
   - **Service Principal Key**: The client secret
   - **Tenant Id**: Your Azure AD tenant ID
   - **Service connection name**: `AzureServiceConnection`
5. Click **Verify and save**

---

## Step 2: Service Connection Permissions

The service connection needs specific Azure permissions to deploy.

### 2.1 Required Permissions

| Permission | Scope | Purpose |
|------------|-------|---------|
| **Contributor** | Resource Group | Deploy and manage App Service |
| **Website Contributor** | App Service (optional) | More restrictive alternative |

### 2.2 Verify Permissions

If using automatic service connection creation, permissions are assigned automatically.

For manual service principals:

```bash
# Get your subscription ID
SUBSCRIPTION_ID=$(az account show --query id -o tsv)

# Get your resource group name
RESOURCE_GROUP="rg-copilot-adoption-bot"

# Assign Contributor role
az role assignment create \
  --assignee <service-principal-app-id> \
  --role Contributor \
  --scope /subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP
```

---

## Step 3: Configure Pipeline Variables

### 3.1 Pipeline Variables in YAML

Edit `.azure-pipelines/azure-deploy.yml` and update the default values:

```yaml
variables:
  # Azure configuration
  azureSubscription: 'AzureServiceConnection'  # Your service connection name
  webAppName: 'copilot-adoption-bot-app'       # Your App Service name (default in the repo is 'office-nudge-web')

  # Build configuration
  dotnetVersion: '10.0.x'
  # Node.js 20 LTS is required by the React frontend
```

### 3.2 Tests run against Azurite

The pipeline starts **Azurite** (Azure Storage emulator) as a Docker container before tests run
and writes a minimal `appsettings.json` that points at it (`UseDevelopmentStorage=true`). Both the
pure unit tests and the storage-only integration tests
(`StorageManagerIntegrationTests`, `BatchQueueServiceIntegrationTests`,
`MessageTemplateServiceIntegrationTests`) execute against Azurite on every run.

No `TESTS_APPSETTINGS_JSON` pipeline variable is required. Graph-dependent integration tests are
intentionally excluded from CI and must be run locally with real configuration.

### 3.3 Pipeline Variables in Azure DevOps (Alternative)

For sensitive or environment-specific values:

1. Go to **Pipelines** → Your pipeline → **Edit**
2. Click **Variables** (top right)
3. Add variables:

| Variable Name | Value | Keep Secret |
|---------------|-------|-------------|
| `webAppName` | `copilot-adoption-bot-app` | No |
| `azureSubscription` | `AzureServiceConnection` | No |

---

## Step 4: Use the Pipeline File

The repository already ships a working pipeline at
[`.azure-pipelines/azure-deploy.yml`](../.azure-pipelines/azure-deploy.yml). Use that file as the source
of truth rather than copy-pasting a sample; this section just calls out the values you must change.

> ⚠️ **Before your first run**, edit the checked-in pipeline and update these placeholder values:
>
> | Variable | Default in repo | Change to |
> |----------|-----------------|-----------|
> | `webAppName` | `office-nudge-web` | The App Service name you created in Step 1 |
> | `azureSubscription` | `AzureServiceConnection` | Your service connection name from this guide |
>
> The repo pipeline targets **Linux** App Service (`appType: webAppLinux`, `runtimeStack: DOTNETCORE|10.0`)
> with Node 20 and .NET 10. If you created a Windows App Service in Step 1, change `appType` to `webApp`
> and drop the `runtimeStack` line.

### 4.2 Import Pipeline in Azure DevOps

1. Go to **Pipelines** → **New Pipeline**
2. Select where your code is:
   - **Azure Repos Git** if code is in Azure DevOps
   - **GitHub** if code is on GitHub (requires GitHub connection)
3. Select your repository
4. Choose **Existing Azure Pipelines YAML file**
5. Select `.azure-pipelines/azure-deploy.yml`
6. Click **Run** to save and execute

---

## Step 5: Create Deployment Environment

Environments provide deployment history and optional approval gates.

### 5.1 Create Environment

1. Go to **Pipelines** → **Environments**
2. Click **New environment**
3. Configure:
   - **Name**: `Production`
   - **Description**: Production deployment environment
4. Click **Create**

### 5.2 Configure Approvals (Optional)

For deployment approvals:

1. Click on the **Production** environment
2. Click **⋮** (more options) → **Approvals and checks**
3. Click **+** → **Approvals**
4. Add approvers (users or groups)
5. Configure:
   - **Timeout**: How long to wait for approval (e.g., 72 hours)
   - **Instructions**: Optional deployment notes
6. Click **Create**

---

## Step 6: Run and Verify

### 6.1 Trigger the Pipeline

- **Automatic**: Push to `main` branch
- **Manual**: Go to **Pipelines** → Select pipeline → **Run pipeline**

### 6.2 Monitor Pipeline

1. Go to **Pipelines** → Click on running pipeline
2. Watch stage progress:
   - **Build**: Compiles and tests code
   - **Deploy**: Deploys to Azure (main branch only)

### 6.3 Verify Deployment

After successful deployment:

| Check | URL/Action |
|-------|------------|
| Application | `https://your-app-name.azurewebsites.net` |
| Swagger | `https://your-app-name.azurewebsites.net/swagger` |
| Graph test | `https://your-app-name.azurewebsites.net/api/Diagnostics/TestGraphConnection` |
| Bot test | Send a message to the bot in Teams |

---

## Pipeline Stages Summary

| Stage | Condition | Actions |
|-------|-----------|---------|
| **Build** | Always | Restore, build frontend, build backend, test, publish artifact |
| **Deploy** | `main` branch only, build succeeded | Deploy to Azure Web App |

---

## Security Considerations

### Workload Identity vs. Service Principal Secret

| Approach | Security | Maintenance |
|----------|----------|-------------|
| **Workload Identity (Recommended)** | ✅ No secrets stored | Low - No rotation needed |
| **Service Principal Secret** | ⚠️ Secret stored in Azure DevOps | High - Requires rotation |

### Principle of Least Privilege

- Service connection only has access to the specific resource group
- Consider using deployment slots for production
- Review service connection permissions regularly

### Branch Policies

Recommended policies for `main` branch:

1. Go to **Repos** → **Branches** → **⋮** on `main` → **Branch policies**
2. Enable:
   - **Require a minimum number of reviewers**
   - **Check for linked work items**
   - **Build validation** (add build policy)
   - **Require comment resolution**

---

## Troubleshooting

### Service Connection Issues

**Error**: `Could not find service connection`

**Solution**:
1. Verify the service connection name matches exactly
2. Check the service connection grants access to all pipelines, or
3. Authorize the pipeline to use the service connection

### Build Fails - .NET SDK Not Found

**Error**: `Unable to locate .NET SDK`

**Solution**: Ensure the pipeline uses:
```yaml
- task: UseDotNet@2
  inputs:
    version: '10.0.x'
    includePreviewVersions: true
```

### Deployment Fails - Permission Denied

**Error**: `The client does not have authorization`

**Solution**:
1. Verify service connection has Contributor role on resource group
2. Check the service principal hasn't expired
3. Re-authorize the service connection

### Tests Fail - Cannot Find Test Projects

**Error**: `No test matches the given testcase filter`

**Solution**: Verify the test project path pattern:
```yaml
projects: '$(solutionPath)/**/*Tests.csproj'
```

---

## Advanced Configuration

### Deployment Slots

For zero-downtime deployments:

```yaml
- task: AzureWebApp@1
  displayName: 'Deploy to Staging Slot'
  inputs:
    azureSubscription: $(azureSubscription)
    appType: 'webApp'
    appName: $(webAppName)
    deployToSlotOrASE: true
    slotName: 'staging'
    package: '$(System.ArtifactsDirectory)/webapp/*.zip'

- task: AzureAppServiceManage@0
  displayName: 'Swap Slots'
  inputs:
    azureSubscription: $(azureSubscription)
    action: 'Swap Slots'
    webAppName: $(webAppName)
    sourceSlot: 'staging'
    targetSlot: 'production'
```

### Multi-Environment Deployment

```yaml
stages:
- stage: DeployStaging
  displayName: 'Deploy to Staging'
  condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/main'))
  jobs:
  - deployment: DeployStaging
    environment: 'Staging'
    # ... deployment steps with staging app name

- stage: DeployProduction
  displayName: 'Deploy to Production'
  dependsOn: DeployStaging
  condition: succeeded()
  jobs:
  - deployment: DeployProduction
    environment: 'Production'
    # ... deployment steps with production app name
```

### Variable Groups

For shared configuration across pipelines:

1. Go to **Pipelines** → **Library** → **+ Variable group**
2. Create a group (e.g., `copilot-bot-config`)
3. Add variables
4. Reference in pipeline:

```yaml
variables:
- group: 'copilot-bot-config'
```

---

## Appendix: Creating a Service Principal

If you need to create a service principal manually:

```bash
# Create service principal with Contributor role
az ad sp create-for-rbac \
  --name "AzureDevOps-CopilotAdoptionBot" \
  --role Contributor \
  --scopes /subscriptions/{subscription-id}/resourceGroups/rg-copilot-adoption-bot \
  --years 2

# Output includes:
# - appId (Service Principal Id)
# - password (Service Principal Key) - save this immediately
# - tenant (Tenant Id)
```

> ⚠️ **Important**: The password is only shown once. Store it securely immediately.

---

## Next Steps

- Review [Security Best Practices](SECURITY.md)
- Set up [Application Insights](DEPLOYMENT.md#estimated-costs) for monitoring
- Configure [Azure Key Vault](DEPLOYMENT.md#azure-key-vault-integration) for secrets
