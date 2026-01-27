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
  webAppName: 'copilot-adoption-bot-app'       # Your App Service name
  
  # Build configuration  
  vmImageName: 'ubuntu-latest'
  dotnetVersion: '10.0.x'
  nodeVersion: '18.x'
```

### 3.2 Pipeline Variables in Azure DevOps (Alternative)

For sensitive or environment-specific values:

1. Go to **Pipelines** → Your pipeline → **Edit**
2. Click **Variables** (top right)
3. Add variables:

| Variable Name | Value | Keep Secret |
|---------------|-------|-------------|
| `webAppName` | `copilot-adoption-bot-app` | No |
| `azureSubscription` | `AzureServiceConnection` | No |

---

## Step 4: Create the Pipeline

### 4.1 Create Pipeline File

Create `.azure-pipelines/azure-deploy.yml` in your repository:

```yaml
trigger:
  branches:
    include:
      - main
  paths:
    exclude:
      - '*.md'
      - 'docs/**'

pr:
  branches:
    include:
      - main

variables:
  # Azure configuration - update these values
  azureSubscription: 'AzureServiceConnection'
  webAppName: 'copilot-adoption-bot-app'
  
  # Build configuration
  vmImageName: 'ubuntu-latest'
  dotnetVersion: '10.0.x'
  nodeVersion: '18.x'
  
  # Paths
  solutionPath: 'src/Full/Bot'
  frontendPath: 'src/Full/Bot/Web/web.client'
  projectPath: 'src/Full/Bot/Web/Web.Server/Web.Server.csproj'

stages:
- stage: Build
  displayName: 'Build Stage'
  jobs:
  - job: Build
    displayName: 'Build Job'
    pool:
      vmImage: $(vmImageName)
    
    steps:
    - task: UseDotNet@2
      displayName: 'Setup .NET SDK'
      inputs:
        version: $(dotnetVersion)
        includePreviewVersions: true

    - task: NodeTool@0
      displayName: 'Setup Node.js'
      inputs:
        versionSpec: $(nodeVersion)

    - task: Npm@1
      displayName: 'Install frontend dependencies'
      inputs:
        command: 'ci'
        workingDir: $(frontendPath)

    - task: Npm@1
      displayName: 'Build frontend'
      inputs:
        command: 'custom'
        workingDir: $(frontendPath)
        customCommand: 'run build'

    - task: DotNetCoreCLI@2
      displayName: 'Restore NuGet packages'
      inputs:
        command: 'restore'
        projects: '$(solutionPath)/**/*.csproj'

    - task: DotNetCoreCLI@2
      displayName: 'Build solution'
      inputs:
        command: 'build'
        projects: '$(solutionPath)/**/*.csproj'
        arguments: '--configuration Release --no-restore'

    - task: DotNetCoreCLI@2
      displayName: 'Run tests'
      inputs:
        command: 'test'
        projects: '$(solutionPath)/**/*Tests.csproj'
        arguments: '--configuration Release --no-build --verbosity normal'
      continueOnError: false

    - task: DotNetCoreCLI@2
      displayName: 'Publish application'
      inputs:
        command: 'publish'
        publishWebProjects: false
        projects: $(projectPath)
        arguments: '--configuration Release --no-build --output $(Build.ArtifactStagingDirectory)/publish'
        zipAfterPublish: true

    - task: PublishBuildArtifacts@1
      displayName: 'Publish artifact'
      inputs:
        PathtoPublish: '$(Build.ArtifactStagingDirectory)/publish'
        ArtifactName: 'webapp'
        publishLocation: 'Container'

- stage: Deploy
  displayName: 'Deploy Stage'
  dependsOn: Build
  condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/main'))
  jobs:
  - deployment: Deploy
    displayName: 'Deploy to Azure'
    environment: 'Production'
    pool:
      vmImage: $(vmImageName)
    strategy:
      runOnce:
        deploy:
          steps:
          - task: DownloadBuildArtifacts@1
            displayName: 'Download artifact'
            inputs:
              buildType: 'current'
              downloadType: 'single'
              artifactName: 'webapp'
              downloadPath: '$(System.ArtifactsDirectory)'

          - task: AzureWebApp@1
            displayName: 'Deploy to Azure Web App'
            inputs:
              azureSubscription: $(azureSubscription)
              appType: 'webApp'
              appName: $(webAppName)
              package: '$(System.ArtifactsDirectory)/webapp/*.zip'
              deploymentMethod: 'auto'
```

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
