<#
.SYNOPSIS
    Provisions and (optionally) deploys the Copilot Adoption Bot.

.DESCRIPTION
    Wraps deploy/main.bicep so an operator can stand the solution up in one command.
    The Bicep template is the single source of truth for infrastructure; this script
    validates prerequisites, reads deployment-config.json, and passes the secrets that
    must not live in source control.

    Idempotent: re-running updates the existing resources in place.

    This script does NOT create the Entra app registrations. Those need admin consent
    and are documented in docs/SETUP.md - create them first and record the ids/secrets
    in your config file.

.PARAMETER ConfigPath
    Path to deployment-config.json. Defaults to ./deployment-config.json.

.PARAMETER ResourceGroup
    Target resource group. Overrides the value in the config file.

.PARAMETER SubscriptionId
    Target subscription. Overrides the value in the config file.

.PARAMETER WhatIf
    Show what would change without applying it (runs `az deployment group what-if`).

.PARAMETER SkipCodeDeploy
    Provision infrastructure only; don't build and publish the application.

.EXAMPLE
    ./Deploy-AdoptionBot.ps1 -ConfigPath ../deployment-config.json -WhatIf

.EXAMPLE
    ./Deploy-AdoptionBot.ps1 -ConfigPath ../deployment-config.json
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $ConfigPath = './deployment-config.json',
    [string] $ResourceGroup,
    [string] $SubscriptionId,
    [switch] $SkipCodeDeploy
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$bicepPath = Join-Path $PSScriptRoot 'main.bicep'
$solutionPath = Join-Path $repoRoot 'src/Full/Bot/Adoption Bot.sln'
$webServerPath = Join-Path $repoRoot 'src/Full/Bot/Web/Web.Server'

function Write-Step   { param($m) Write-Host "`n=== $m ===" -ForegroundColor Cyan }
function Write-Ok     { param($m) Write-Host "  [ok] $m" -ForegroundColor Green }
function Write-Warn   { param($m) Write-Host "  [!]  $m" -ForegroundColor Yellow }
function Fail         { param($m) throw $m }

# ---------------------------------------------------------------------------
# Prerequisites
# ---------------------------------------------------------------------------

Write-Step 'Checking prerequisites'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Fail 'Azure CLI (az) not found. Install: https://aka.ms/installazurecli'
}
Write-Ok 'Azure CLI found'

# Bicep ships with recent Azure CLI, but confirm rather than failing mid-deployment.
$null = az bicep version 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Warn 'Bicep not installed; installing'
    az bicep install | Out-Null
}
Write-Ok 'Bicep available'

$account = az account show 2>$null | ConvertFrom-Json
if (-not $account) { Fail "Not signed in. Run: az login" }
Write-Ok "Signed in as $($account.user.name)"

if (-not (Test-Path $ConfigPath)) {
    Fail "Config file not found: $ConfigPath`nCopy docs/deployment-config.example.json and fill it in."
}
$config = Get-Content $ConfigPath -Raw | ConvertFrom-Json
Write-Ok "Loaded config from $ConfigPath"

# ---------------------------------------------------------------------------
# Resolve settings
# ---------------------------------------------------------------------------

Write-Step 'Resolving settings'

if (-not $SubscriptionId) { $SubscriptionId = $config.azure.subscriptionId }
if (-not $ResourceGroup)  { $ResourceGroup  = $config.azure.resourceGroup }

if (-not $SubscriptionId) { Fail 'No subscriptionId in config and none supplied.' }
if (-not $ResourceGroup)  { Fail 'No resourceGroup in config and none supplied.' }

$location        = $config.azure.location
$appServiceName  = $config.azure.appServiceName
$storageName     = $config.azure.storageAccountName
$planSku         = $config.azure.appServicePlanSku

foreach ($pair in @(
    @{ n = 'azure.location';           v = $location },
    @{ n = 'azure.appServiceName';     v = $appServiceName },
    @{ n = 'azure.storageAccountName'; v = $storageName },
    @{ n = 'bot.appId';                v = $config.bot.appId },
    @{ n = 'bot.appPassword';          v = $config.bot.appPassword },
    @{ n = 'bot.tenantId';             v = $config.bot.tenantId }
)) {
    if ([string]::IsNullOrWhiteSpace([string]$pair.v)) { Fail "Required config value missing: $($pair.n)" }
}

# Graph credentials default to the bot's registration unless overridden.
$graphClientId     = if ($config.graph.useSameAsBot) { $config.bot.appId }       else { $config.graph.clientId }
$graphClientSecret = if ($config.graph.useSameAsBot) { $config.bot.appPassword } else { $config.graph.clientSecret }
$graphTenantId     = if ($config.graph.useSameAsBot) { $config.bot.tenantId }    else { $config.graph.tenantId }

if ([string]::IsNullOrWhiteSpace([string]$graphClientId)) {
    Fail 'Graph client id unresolved. Set graph.useSameAsBot=true or provide graph.clientId.'
}

Write-Ok "Subscription : $SubscriptionId"
Write-Ok "Resource group: $ResourceGroup ($location)"
Write-Ok "App Service  : $appServiceName"
Write-Ok "Storage      : $storageName"

az account set --subscription $SubscriptionId
if ($LASTEXITCODE -ne 0) { Fail "Could not select subscription $SubscriptionId" }

# ---------------------------------------------------------------------------
# Resource group
# ---------------------------------------------------------------------------

Write-Step 'Ensuring resource group'

$rgExists = az group exists --name $ResourceGroup | ConvertFrom-Json
if (-not $rgExists) {
    if ($WhatIfPreference) {
        # az deployment group what-if requires the resource group to exist, and -WhatIf must
        # not create anything. Report and stop rather than failing inside ARM.
        Write-Warn "Resource group '$ResourceGroup' does not exist yet."
        Write-Host ""
        Write-Host "  WhatIf: every resource would be created new:" -ForegroundColor Yellow
        Write-Host "    - Resource group  $ResourceGroup ($location)"
        Write-Host "    - App Service Plan $appServiceName-plan ($planSku)"
        Write-Host "    - App Service      $appServiceName"
        Write-Host "    - Storage account  $storageName"
        if ($config.appInsights.enabled) { Write-Host "    - App Insights     $appServiceName-insights" }
        Write-Host "    - Role assignments Storage Blob/Table/Queue Data Contributor"
        Write-Host ""
        Write-Host "  Re-run without -WhatIf to apply, or create the resource group first to" -ForegroundColor DarkGray
        Write-Host "  get a resource-level diff from ARM." -ForegroundColor DarkGray
        return
    }

    if ($PSCmdlet.ShouldProcess($ResourceGroup, 'Create resource group')) {
        az group create --name $ResourceGroup --location $location --output none
        Write-Ok "Created $ResourceGroup"
    }
} else {
    Write-Ok "$ResourceGroup already exists"
}

# ---------------------------------------------------------------------------
# Infrastructure
# ---------------------------------------------------------------------------

Write-Step 'Deploying infrastructure'

$params = @(
    "appServiceName=$appServiceName"
    "storageAccountName=$storageName"
    "location=$location"
    "botAppId=$($config.bot.appId)"
    "botAppPassword=$($config.bot.appPassword)"
    "botTenantId=$($config.bot.tenantId)"
    "botAppType=$($config.bot.appType)"
    "graphClientId=$graphClientId"
    "graphClientSecret=$graphClientSecret"
    "graphTenantId=$graphTenantId"
)

if ($planSku)                          { $params += "appServicePlanSku=$planSku" }
if ($config.appInsights.enabled -ne $null) { $params += "deployAppInsights=$($config.appInsights.enabled.ToString().ToLower())" }
if ($config.aiFoundry.enabled -and $config.aiFoundry.endpoint) {
    $params += "aiFoundryEndpoint=$($config.aiFoundry.endpoint)"
    $params += "aiFoundryDeploymentName=$($config.aiFoundry.deploymentName)"
}
if ($config.webAuth.enabled) {
    $params += "webAuthClientId=$($config.webAuth.clientId)"
    $params += "webAuthClientSecret=$($config.webAuth.clientSecret)"
    $params += "webAuthTenantId=$($config.webAuth.tenantId)"
    $params += "webAuthApiAudience=$($config.webAuth.apiAudience)"
}

$deploymentName = "adoptionbot-$(Get-Date -Format 'yyyyMMdd-HHmmss')"

if ($WhatIfPreference) {
    Write-Warn 'WhatIf: showing changes only'
    az deployment group what-if `
        --resource-group $ResourceGroup `
        --template-file $bicepPath `
        --parameters $params
    return
}

$result = az deployment group create `
    --name $deploymentName `
    --resource-group $ResourceGroup `
    --template-file $bicepPath `
    --parameters $params `
    --output json | ConvertFrom-Json

if ($LASTEXITCODE -ne 0 -or -not $result) { Fail 'Infrastructure deployment failed.' }

$outputs = $result.properties.outputs
Write-Ok 'Infrastructure deployed'

# ---------------------------------------------------------------------------
# Application code
# ---------------------------------------------------------------------------

if (-not $SkipCodeDeploy) {
    Write-Step 'Building and publishing application'

    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { Fail 'dotnet SDK not found.' }
    if (-not (Get-Command npm -ErrorAction SilentlyContinue))    { Fail 'npm not found (needed for the React client).' }

    $clientPath = Join-Path $repoRoot 'src/Full/Bot/Web/web.client'

    # Vite inlines VITE_* values at BUILD time, so they must exist before `npm run build`.
    # Without this the admin UI ships with no MSAL configuration and sign-in silently fails -
    # the app looks deployed and healthy but nobody can log in.
    $msalClientId = if ($config.frontend.msalClientId) { $config.frontend.msalClientId }
                    elseif ($config.webAuth.enabled -and $config.webAuth.clientId) { $config.webAuth.clientId }
                    else { $graphClientId }

    $msalTenantId = if ($config.webAuth.enabled -and $config.webAuth.tenantId) { $config.webAuth.tenantId } else { $graphTenantId }

    $msalAuthority = if ($config.frontend.msalAuthority -and $config.frontend.msalAuthority -notmatch '<') {
                         $config.frontend.msalAuthority
                     } else { "https://login.microsoftonline.com/$msalTenantId" }

    $msalScopes = if ($config.frontend.msalScopes -and $config.frontend.msalScopes -notmatch '<') {
                      $config.frontend.msalScopes
                  } else { "api://$msalClientId/access_as_user" }

    $startLoginUrl = if ($config.frontend.teamsfxStartLoginPageUrl -and $config.frontend.teamsfxStartLoginPageUrl -notmatch '<') {
                         $config.frontend.teamsfxStartLoginPageUrl
                     } else { "https://$appServiceName.azurewebsites.net/auth-start" }

    $envPath = Join-Path $clientPath '.env.local'
    @(
        "VITE_MSAL_CLIENT_ID=$msalClientId"
        "VITE_MSAL_AUTHORITY=$msalAuthority"
        "VITE_MSAL_SCOPES=$msalScopes"
        "VITE_TEAMSFX_START_LOGIN_PAGE_URL=$startLoginUrl"
    ) | Set-Content $envPath -Encoding utf8
    Write-Ok "Wrote frontend config (.env.local)"

    Push-Location $clientPath
    try {
        npm ci
        if ($LASTEXITCODE -ne 0) { Fail 'npm ci failed.' }
        npm run build
        if ($LASTEXITCODE -ne 0) { Fail 'Frontend build failed.' }
    } finally { Pop-Location }
    Write-Ok 'Frontend built'

    $publishDir = Join-Path ([System.IO.Path]::GetTempPath()) "adoptionbot-publish-$(Get-Random)"
    dotnet publish $webServerPath -c Release -o $publishDir
    if ($LASTEXITCODE -ne 0) { Fail 'dotnet publish failed.' }
    Write-Ok 'Backend published'

    $zipPath = "$publishDir.zip"
    Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -Force

    az webapp deploy `
        --resource-group $ResourceGroup `
        --name $appServiceName `
        --src-path $zipPath `
        --type zip `
        --output none
    if ($LASTEXITCODE -ne 0) { Fail 'Code deployment failed.' }
    Write-Ok 'Application deployed'

    Remove-Item $publishDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
}

# ---------------------------------------------------------------------------
# Next steps
# ---------------------------------------------------------------------------

Write-Step 'Done'

$hostName  = $outputs.appServiceHostName.value
$messaging = $outputs.messagingEndpoint.value

Write-Host ""
Write-Host "  Admin UI          : https://$hostName" -ForegroundColor White
Write-Host "  Messaging endpoint: $messaging" -ForegroundColor White
Write-Host ""
Write-Host "  Remaining manual steps:" -ForegroundColor Yellow
Write-Host "   1. Register the bot in the Teams Developer Portal (Tools > Bot management)."
Write-Host "      This deployment creates a web app, not a bot - Teams only routes messages"
Write-Host "      to a registered bot. Attach your existing app registration, or create a new"
Write-Host "      bot there and update the config with its id/secret."
Write-Host "   2. Set that bot's messaging endpoint to:"
Write-Host "        $messaging"
Write-Host "   3. Ensure the WebAuthConfig app registration exposes an 'access_as_user' scope,"
Write-Host "      has Application ID URI api://<clientId>, and lists"
Write-Host "        https://$hostName"
Write-Host "      as an SPA redirect URI - otherwise admin UI sign-in fails."
Write-Host "   4. Grant admin consent for the Graph permissions (docs/SETUP.md)."
Write-Host "   5. Upload the Teams app package, then set AppCatalogTeamAppId. Without it the"
Write-Host "      bot cannot install itself for users who have never messaged it."
Write-Host ""
Write-Host "  See docs/DEPLOYMENT-BICEP.md for the full checklist." -ForegroundColor DarkGray
Write-Host "  Storage tables, blob containers and queues are created by the app on first run." -ForegroundColor DarkGray
Write-Host "  Note: AlwaysOn is not set by this template - see docs/SCALING.md section 7." -ForegroundColor DarkGray
