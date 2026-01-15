
<# 
.SYNOPSIS
    Sends an Adaptive Card to a specific user's UPN or multiple users from an Excel file in Microsoft Teams via Microsoft Graph (app-only).

.PARAMETER TenantId
    Entra ID tenant ID (GUID).

.PARAMETER ClientId
    App registration (client) ID (GUID).

.PARAMETER ClientSecret
    App client secret (use a secure method in production; e.g., Azure Key Vault).

.PARAMETER TargetUpn
    Target user's UPN/email (e.g., sam.betts@contoso.com). Mutually exclusive with ExcelFilePath.

.PARAMETER ExcelFilePath
    Path to an Excel file containing user UPNs. The file should have a column with the header 'UPN' or 'Email'. Mutually exclusive with TargetUpn.

.PARAMETER ExcelSheetName
    Name of the Excel worksheet to read from. Defaults to the first worksheet if not specified.

.PARAMETER AdaptiveCardJsonPath
    Path to a JSON file containing the Adaptive Card payload (schema 1.5 or below).

.PARAMETER CachePath
    Optional path to a local JSON file to cache userId→chatId mappings to avoid duplicate chats.

.PARAMETER UseDefaultCard
    Switch to send a simple default card if no JSON file is provided.

.PARAMETER TeamsAppId
    The Teams App ID (GUID) containing the bot to install for users. Required for creating new bot chats.

.PARAMETER BotId
    The Bot's Azure AD App ID (GUID). This is the app ID of the bot within the Teams app.

.NOTES
    Required Graph application permissions (admin consent required):
    - User.Read.All: Read user information
    - Chat.Create: Create new chats
    - Chat.ReadWrite.All: Read and write chats and messages (replaces ChatMessage.Send for application permissions)
    - TeamsAppInstallation.ReadWriteForUser.All: Install Teams apps for users (required for bot-based messaging)
    - Teamwork.Migrate.All: Required when sending messages to bot chats. This permission allows the app to send messages
      on behalf of bots in Teams. Even though it's named "Migrate", it's the required permission for proactive bot
      messaging via Graph API when the bot is not directly handling the message through Bot Framework.
    
    Note: ChatMessage.Send is a delegated permission only, not available for application permissions.
    
    Tested with Microsoft.Graph PowerShell SDK (v1.x) using v1.0 profile.
    For Excel file processing, requires ImportExcel module (will be installed if not present).
    For creating chats with a bot, both TeamsAppId and BotId are required.
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$TenantId,

    [Parameter(Mandatory=$true)]
    [string]$ClientId,

    [Parameter(Mandatory=$true)]
    [string]$ClientSecret,

    [Parameter(Mandatory=$false, ParameterSetName='SingleUser')]
    [string]$TargetUpn,

    [Parameter(Mandatory=$false, ParameterSetName='MultipleUsers')]
    [string]$ExcelFilePath,

    [Parameter(Mandatory=$false, ParameterSetName='MultipleUsers')]
    [string]$ExcelSheetName,

    [Parameter(Mandatory=$false)]
    [string]$AdaptiveCardJsonPath,

    [Parameter(Mandatory=$false)]
    [string]$CachePath = ".\chatCache.json",

    [switch]$UseDefaultCard,

    [Parameter(Mandatory=$false)]
    [string]$TeamsAppId,

    [Parameter(Mandatory=$false)]
    [string]$BotId
)

function Test-PowerShellVersion {
    $requiredVersion = [version]"5.1"
    $currentVersion = $PSVersionTable.PSVersion
    
    Write-Host "PowerShell Version: $currentVersion" -ForegroundColor DarkGray
    
    if ($currentVersion -lt $requiredVersion) {
        throw "This script requires PowerShell $requiredVersion or higher. Current version: $currentVersion. Please upgrade PowerShell."
    }
}

function Ensure-GraphModule {
    if (-not (Get-Module -ListAvailable -Name Microsoft.Graph)) {
        Write-Host "Installing Microsoft.Graph module..." -ForegroundColor Cyan
        Install-Module Microsoft.Graph -Scope CurrentUser -Force -AllowClobber -ErrorAction Stop
    }
    Import-Module Microsoft.Graph -ErrorAction Stop
}

function Ensure-ImportExcelModule {
    if (-not (Get-Module -ListAvailable -Name ImportExcel)) {
        Write-Host "Installing ImportExcel module..." -ForegroundColor Cyan
        Install-Module ImportExcel -Scope CurrentUser -Force -ErrorAction Stop
    }
    Import-Module ImportExcel -ErrorAction Stop
}

function Connect-AppOnly {
    param(
        [string]$TenantId,
        [string]$ClientId,
        [string]$ClientSecret
    )
    $secureSecret = ConvertTo-SecureString $ClientSecret -AsPlainText -Force
    $credential = New-Object System.Management.Automation.PSCredential($ClientId, $secureSecret)
    Write-Host "Connecting to Microsoft Graph (app-only)..." -ForegroundColor Cyan
    Connect-MgGraph -TenantId $TenantId -ClientSecretCredential $credential -NoWelcome -ErrorAction Stop
}

function Get-UserByUpn {
    param([string]$Upn)
    # Get-MgUser supports UPN directly
    try {
        $user = Get-MgUser -UserId $Upn -ErrorAction Stop
        return $user
    } catch {
        throw "User not found or inaccessible for UPN '$Upn'. Ensure the UPN is correct and app permissions are consented. Details: $($_.Exception.Message)"
    }
}

function Get-UpnsFromExcel {
    param(
        [string]$ExcelFilePath,
        [string]$SheetName
    )

    if (-not (Test-Path -LiteralPath $ExcelFilePath)) {
        throw "Excel file '$ExcelFilePath' does not exist."
    }

    try {
        Write-Host "Reading Excel file: $ExcelFilePath" -ForegroundColor Cyan
        
        if ($SheetName) {
            $data = Import-Excel -Path $ExcelFilePath -WorksheetName $SheetName -ErrorAction Stop
        } else {
            $data = Import-Excel -Path $ExcelFilePath -ErrorAction Stop
        }

        # Look for UPN or Email column (case-insensitive)
        $upnColumn = $null
        $firstRow = $data | Select-Object -First 1
        foreach ($prop in $firstRow.PSObject.Properties) {
            if ($prop.Name -match '^(UPN|Email)$') {
                $upnColumn = $prop.Name
                break
            }
        }

        if (-not $upnColumn) {
            throw "Excel file must contain a column named 'UPN' or 'Email'."
        }

        $upns = $data | Where-Object { $_.$upnColumn -and $_.$upnColumn.Trim() -ne "" } | ForEach-Object { $_.$upnColumn.Trim() }
        
        if ($upns.Count -eq 0) {
            throw "No valid UPNs found in Excel file."
        }

        Write-Host "Found $($upns.Count) UPN(s) in Excel file." -ForegroundColor Green
        return $upns
    } catch {
        throw "Failed to read Excel file. Details: $($_.Exception.Message)"
    }
}

function Load-Cache {
    param([string]$Path)
    if (Test-Path -LiteralPath $Path) {
        try {
            $jsonContent = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
            # Convert PSCustomObject to Hashtable
            $hashtable = @{}
            foreach ($property in $jsonContent.PSObject.Properties) {
                $hashtable[$property.Name] = $property.Value
            }
            return $hashtable
        } catch {
            Write-Warning "Failed to read cache at '$Path'. Starting with an empty cache."
            return @{}
        }
    }
    return @{}
}

function Save-Cache {
    param(
        [hashtable]$Cache,
        [string]$Path
    )
    ($Cache | ConvertTo-Json -Depth 6) | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Install-TeamsAppForUser {
    param(
        [string]$UserId,
        [string]$TeamsAppId
    )
    
    Write-Host "Installing Teams app $TeamsAppId for user $UserId..." -ForegroundColor Cyan
    
    # Check if app is already installed and get the chat ID
    try {
        $installedApps = Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/users/$UserId/teamwork/installedApps?`$expand=teamsApp,teamsAppDefinition&`$filter=teamsApp/id eq '$TeamsAppId'" -ErrorAction Stop
        
        if ($installedApps.value -and $installedApps.value.Count -gt 0) {
            Write-Host "Teams app already installed." -ForegroundColor DarkGreen
            $installationId = $installedApps.value[0].id
            
            # Get the chat associated with this installation
            $chatInfo = Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/users/$UserId/teamwork/installedApps/$installationId/chat" -ErrorAction Stop
            return $chatInfo.id
        }
    } catch {
        Write-Warning "Could not check existing app installations: $($_.Exception.Message)"
    }
    
    # Install the app
    try {
        $body = @{
            "teamsApp@odata.bind" = "https://graph.microsoft.com/v1.0/appCatalogs/teamsApps/$TeamsAppId"
        }
        
        $installation = Invoke-MgGraphRequest -Method POST -Uri "https://graph.microsoft.com/v1.0/users/$UserId/teamwork/installedApps" -Body $body -ErrorAction Stop
        Write-Host "Teams app installed successfully." -ForegroundColor Green
        
        # Extract installation ID from the response location header or ID
        $installationId = $installation.id
        if (-not $installationId) {
            # Wait a moment and query for it
            Start-Sleep -Seconds 3
            $installedApps = Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/users/$UserId/teamwork/installedApps?`$expand=teamsApp&`$filter=teamsApp/id eq '$TeamsAppId'" -ErrorAction Stop
            if ($installedApps.value -and $installedApps.value.Count -gt 0) {
                $installationId = $installedApps.value[0].id
            }
        }
        
        if ($installationId) {
            # Get the chat ID from the installation
            Start-Sleep -Seconds 2
            $chatInfo = Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/users/$UserId/teamwork/installedApps/$installationId/chat" -ErrorAction Stop
            return $chatInfo.id
        } else {
            throw "Could not retrieve installation ID after installing app."
        }
    } catch {
        throw "Failed to install Teams app. Ensure TeamsAppInstallation.ReadWriteForUser.All permission is granted. Details: $($_.Exception.Message)"
    }
}

function Get-OrCreateOneOnOneChat {
    param(
        [string]$UserId,
        [hashtable]$Cache,
        [string]$CachePath,
        [string]$TeamsAppId,
        [string]$BotId
    )

    if ($Cache.ContainsKey($UserId)) {
        Write-Host "Using cached chatId: $($Cache[$UserId])" -ForegroundColor DarkGreen
        return $Cache[$UserId]
    }

    # If using bot-based approach
    if ($TeamsAppId -and $BotId) {
        Write-Host "Using bot-based chat creation for userId $UserId..." -ForegroundColor Cyan
        
        # Install the Teams app for the user and get the chat ID
        try {
            $chatId = Install-TeamsAppForUser -UserId $UserId -TeamsAppId $TeamsAppId
            
            if ($chatId) {
                Write-Host "Got chatId from app installation: $chatId" -ForegroundColor Green
                $Cache[$UserId] = $chatId
                Save-Cache -Cache $Cache -Path $CachePath
                return $chatId
            } else {
                throw "Could not retrieve chat ID from app installation."
            }
        } catch {
            throw "Failed to setup bot chat. Details: $($_.Exception.Message)"
        }
    }

    # Fallback: search for existing chats
    Write-Host "Searching for existing chat with userId $UserId..." -ForegroundColor Cyan
    try {
        $chats = Get-MgChat -Filter "chatType eq 'oneOnOne'" -All -ErrorAction Stop
        foreach ($chat in $chats) {
            $members = Get-MgChatMember -ChatId $chat.Id -ErrorAction SilentlyContinue
            $userIds = $members | Where-Object { $_.'@odata.type' -eq '#microsoft.graph.aadUserConversationMember' } | ForEach-Object { $_.UserId }
            if ($userIds -contains $UserId) {
                $chatId = $chat.Id
                Write-Host "Found existing chatId: $chatId" -ForegroundColor Green
                $Cache[$UserId] = $chatId
                Save-Cache -Cache $Cache -Path $CachePath
                return $chatId
            }
        }
    } catch {
        Write-Warning "Failed to search for existing chats: $($_.Exception.Message)"
    }

    # Last resort: try creating without bot
    Write-Host "Creating one-on-one chat with userId $UserId..." -ForegroundColor Cyan
    try {
        $member = @{
            "@odata.type"    = "#microsoft.graph.aadUserConversationMember"
            roles            = @("owner")
            "user@odata.bind"= "https://graph.microsoft.com/v1.0/users('$UserId')"
        }

        $chat = New-MgChat -ChatType oneOnOne -Members @($member) -ErrorAction Stop
        $chatId = $chat.Id
        Write-Host "Created chatId: $chatId" -ForegroundColor Green
        $Cache[$UserId] = $chatId
        Save-Cache -Cache $Cache -Path $CachePath
        return $chatId
    } catch {
        throw "Failed to create one-on-one chat. Provide -TeamsAppId and -BotId parameters to use bot-based chat creation. Details: $($_.Exception.Message)"
    }
}

function Get-CardContent {
    param(
        [string]$AdaptiveCardJsonPath,
        [switch]$UseDefaultCard
    )

    if ($AdaptiveCardJsonPath) {
        if (-not (Test-Path -LiteralPath $AdaptiveCardJsonPath)) {
            throw "AdaptiveCardJsonPath '$AdaptiveCardJsonPath' does not exist."
        }
        try {
            $jsonRaw = Get-Content -LiteralPath $AdaptiveCardJsonPath -Raw
            $cardObj = $jsonRaw | ConvertFrom-Json
            return $cardObj
        } catch {
            throw "Failed to parse Adaptive Card JSON file. Details: $($_.Exception.Message)"
        }
    }

    if ($UseDefaultCard) {
        # Simple, valid Adaptive Card (1.5) as default
        $defaultCard = @{
            type    = "AdaptiveCard"
            version = "1.5"
            body    = @(
                @{
                    type  = "TextBlock"
                    text  = "Notification"
                    size  = "Medium"
                    weight= "Bolder"
                },
                @{
                    type  = "TextBlock"
                    text  = "Your job completed successfully."
                    wrap  = $true
                },
                @{
                    type  = "FactSet"
                    facts = @(
                        @{ title = "Finished at"; value = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss 'UTC'K") }
                        @{ title = "Triggered by"; value = "Automation Service" }
                    )
                }
            )
            actions = @(
                @{
                    type = "Action.OpenUrl"
                    title= "View details"
                    url  = "https://teams.microsoft.com/"
                }
            )
        }
        return $defaultCard
    }

    throw "No Adaptive Card provided. Supply -AdaptiveCardJsonPath or -UseDefaultCard."
}

function Send-AdaptiveCardToChat {
    param(
        [string]$ChatId,
        [object]$CardContent,
        [string]$UserId,
        [string]$TeamsAppId
    )

    # When sending to bot chats with app-only auth, we need to use migration mode
    # This requires Teamwork.Migrate.All permission and specific fields
    
    # Get current timestamp in ISO 8601 format
    $timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
    
    # Convert the adaptive card content to JSON string and ensure it's a plain string
    $cardContentJson = ($CardContent | ConvertTo-Json -Depth 10 -Compress).ToString()
    
    $body = @{
        createdDateTime = $timestamp
        from = @{
            user = @{
                id = $UserId
                displayName = "Notification"
                userIdentityType = "aadUser"
            }
        }
        body = @{
            contentType = "html"
            content     = '<attachment id="1"></attachment>'
        }
        attachments = @(
            @{
                id          = "1"
                contentType = "application/vnd.microsoft.card.adaptive"
                contentUrl  = $null
                content     = $cardContentJson
                name        = $null
            }
        )
    }

    $maxRetries = 3
    $retryCount = 0
    $success = $false
    
    while (-not $success -and $retryCount -lt $maxRetries) {
        try {
            if ($retryCount -gt 0) {
                $waitTime = [Math]::Pow(2, $retryCount)
                Write-Host "Retry attempt $retryCount after $waitTime seconds..." -ForegroundColor Yellow
                Start-Sleep -Seconds $waitTime
            }
            
            Write-Host "Sending Adaptive Card to chatId $ChatId..." -ForegroundColor Cyan
            
            # Convert to JSON manually to ensure proper serialization
            $bodyJson = $body | ConvertTo-Json -Depth 10
            
            Invoke-MgGraphRequest -Method POST -Uri "https://graph.microsoft.com/v1.0/chats/$ChatId/messages" -Body $bodyJson -ErrorAction Stop | Out-Null
            Write-Host "Card sent successfully." -ForegroundColor Green
            $success = $true
        } catch {
            $retryCount++
            $errorDetails = $_.Exception.Message
            
            # Check if it's a retryable error (502, 503, 504)
            $isRetryable = $errorDetails -match "(BadGateway|ServiceUnavailable|GatewayTimeout|502|503|504)"
            
            # Try to extract more details from the error
            if ($_.ErrorDetails) {
                $errorDetails += "`nError Details: $($_.ErrorDetails.Message)"
            }
            
            if ($_.Exception.Response) {
                try {
                    $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                    $responseBody = $reader.ReadToEnd()
                    $errorDetails += "`nResponse Body: $responseBody"
                } catch {
                    # Ignore if we can't read the response
                }
            }
            
            if (-not $isRetryable -or $retryCount -ge $maxRetries) {
                # Show the request body for debugging on final failure
                Write-Host "`nRequest Body sent:" -ForegroundColor Yellow
                Write-Host $bodyJson -ForegroundColor Gray
                
                throw "Failed to send message after $retryCount attempts. Confirm Chat.ReadWrite.All and Teamwork.Migrate.All permissions are granted with admin consent. Details: $errorDetails"
            } else {
                Write-Warning "Retryable error encountered: $($_.Exception.Message)"
            }
        }
    }
}

function Send-CardToUser {
    param(
        [string]$Upn,
        [hashtable]$Cache,
        [string]$CachePath,
        [object]$CardContent,
        [string]$TeamsAppId,
        [string]$BotId
    )

    try {
        Write-Host "`n--- Processing user: $Upn ---" -ForegroundColor Yellow
        $user   = Get-UserByUpn -Upn $Upn
        $chatId = Get-OrCreateOneOnOneChat -UserId $user.Id -Cache $Cache -CachePath $CachePath -TeamsAppId $TeamsAppId -BotId $BotId
        Send-AdaptiveCardToChat -ChatId $chatId -CardContent $CardContent -UserId $user.Id -TeamsAppId $TeamsAppId
        return @{ Success = $true; Upn = $Upn; Error = $null }
    } catch {
        Write-Warning "Failed to send card to $Upn`: $($_.Exception.Message)"
        return @{ Success = $false; Upn = $Upn; Error = $_.Exception.Message }
    }
}

# --- Main ---
try {
    Test-PowerShellVersion
    Ensure-GraphModule
    
    # Validate parameters
    if (-not $TargetUpn -and -not $ExcelFilePath) {
        throw "You must specify either -TargetUpn or -ExcelFilePath."
    }

    Connect-AppOnly -TenantId $TenantId -ClientId $ClientId -ClientSecret $ClientSecret

    # Optional: show current profile and auth type
    $ctx = Get-MgContext
    Write-Host "Graph profile: $($ctx.Profile), AuthType: $($ctx.AuthType)" -ForegroundColor DarkGray

    $cache  = Load-Cache -Path $CachePath
    $card   = Get-CardContent -AdaptiveCardJsonPath $AdaptiveCardJsonPath -UseDefaultCard:$UseDefaultCard

    # Determine UPN list
    $upnList = @()
    if ($ExcelFilePath) {
        Ensure-ImportExcelModule
        $upnList = Get-UpnsFromExcel -ExcelFilePath $ExcelFilePath -SheetName $ExcelSheetName
    } else {
        $upnList = @($TargetUpn)
    }

    # Validate bot parameters
    if ($TeamsAppId -and -not $BotId) {
        throw "BotId is required when TeamsAppId is specified."
    }
    if ($BotId -and -not $TeamsAppId) {
        throw "TeamsAppId is required when BotId is specified."
    }

    # Process each user
    $results = @()
    foreach ($upn in $upnList) {
        $result = Send-CardToUser -Upn $upn -Cache $cache -CachePath $CachePath -CardContent $card -TeamsAppId $TeamsAppId -BotId $BotId
        $results += $result
    }

    # Summary
    Write-Host "`n=== Summary ===" -ForegroundColor Cyan
    $successCount = ($results | Where-Object { $_.Success }).Count
    $failCount = ($results | Where-Object { -not $_.Success }).Count
    Write-Host "Total: $($results.Count) | Success: $successCount | Failed: $failCount" -ForegroundColor Cyan
    
    if ($failCount -gt 0) {
        Write-Host "`nFailed UPNs:" -ForegroundColor Red
        $results | Where-Object { -not $_.Success } | ForEach-Object {
            Write-Host "  - $($_.Upn): $($_.Error)" -ForegroundColor Red
        }
    }
}
catch {
    Write-Error $_.Exception.Message
    exit 1
}
