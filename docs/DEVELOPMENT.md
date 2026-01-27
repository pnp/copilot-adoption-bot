# Development Environment Setup

This guide covers setting up a local development environment for the Copilot Adoption Bot, including tools, secrets management, and tunneling for Teams bot testing.

## Prerequisites

### Required Tools

| Tool | Version | Purpose | Download |
|------|---------|---------|----------|
| [.NET SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | 10.0+ | Backend development | [Download](https://dotnet.microsoft.com/download/dotnet/10.0) |
| [Node.js](https://nodejs.org/) | 18+ | Frontend development | [Download](https://nodejs.org/) |
| [npm](https://www.npmjs.com/) | 9+ | Package management | Included with Node.js |
| [Git](https://git-scm.com/) | Latest | Version control | [Download](https://git-scm.com/) |
| [Visual Studio 2022](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/) | Latest | IDE | [VS 2022](https://visualstudio.microsoft.com/) / [VS Code](https://code.visualstudio.com/) |

### Optional Tools

| Tool | Purpose | Download |
|------|---------|----------|
| [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli) | Azure resource management | [Download](https://docs.microsoft.com/cli/azure/install-azure-cli) |
| [Azure Storage Explorer](https://azure.microsoft.com/features/storage-explorer/) | Browse storage accounts | [Download](https://azure.microsoft.com/features/storage-explorer/) |
| [Ngrok](https://ngrok.com/) or [Dev Tunnels](https://learn.microsoft.com/azure/developer/dev-tunnels/) | Expose localhost for bot testing | [Ngrok](https://ngrok.com/) / [Dev Tunnels](https://learn.microsoft.com/azure/developer/dev-tunnels/) |

---

## Clone the Repository

```bash
git clone https://github.com/pnp/copilot-adoption-bot.git
cd copilot-adoption-bot/src/Full/Bot
```

---

## Install Dependencies

### Backend (.NET)

```bash
dotnet restore
dotnet build
```

### Frontend (React)

```bash
cd Web/web.client
npm install
cd ../..
```

---

## Configuration with User Secrets

For local development, use .NET User Secrets to store sensitive configuration. This keeps secrets out of source control.

> **For a complete list of all configuration options**, see the [Configuration Reference](CONFIGURATION.md).

### Initialize User Secrets

```bash
cd Web/Web.Server
dotnet user-secrets init
```

### Required Secrets

```bash
# Bot identity (from Teams Developer Portal)
dotnet user-secrets set "MicrosoftAppId" "your-bot-app-id"
dotnet user-secrets set "MicrosoftAppPassword" "your-bot-app-password"

# Graph API configuration
dotnet user-secrets set "GraphConfig:ClientId" "your-bot-app-id"
dotnet user-secrets set "GraphConfig:ClientSecret" "your-bot-app-password"
dotnet user-secrets set "GraphConfig:TenantId" "your-tenant-id"

# Storage (use connection string for local dev)
dotnet user-secrets set "ConnectionStrings:Storage" "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=..."
```

### Optional Secrets

```bash
# Application Insights
dotnet user-secrets set "APPLICATIONINSIGHTS_CONNECTION_STRING" "your-connection-string"

# Azure AI Foundry (for Copilot Connected mode)
dotnet user-secrets set "AIFoundryConfig:Endpoint" "https://your-resource.openai.azure.com/"
dotnet user-secrets set "AIFoundryConfig:DeploymentName" "gpt-4o-mini"
dotnet user-secrets set "AIFoundryConfig:ApiKey" "your-api-key"

# Development mode
dotnet user-secrets set "DevMode" "true"
dotnet user-secrets set "TestUPN" "your-test-user@yourdomain.com"
```

### View All Secrets

```bash
dotnet user-secrets list
```

### Secrets Location

User secrets are stored outside your project directory:
- **Windows**: `%APPDATA%\Microsoft\UserSecrets\<user_secrets_id>\secrets.json`
- **macOS/Linux**: `~/.microsoft/usersecrets/<user_secrets_id>/secrets.json`

---

## Frontend Configuration

Create `src/Full/Bot/Web/web.client/.env.local`:

```env
VITE_MSAL_CLIENT_ID=your-bot-app-id
VITE_MSAL_AUTHORITY=https://login.microsoftonline.com/your-tenant-id
VITE_MSAL_SCOPES=api://your-bot-app-id/access_as_user
VITE_TEAMSFX_START_LOGIN_PAGE_URL=https://localhost:5001/auth-start.html
```

---

## Running the Application Locally

### Start the Backend

```bash
cd Web/Web.Server
dotnet run
```

The API will be available at `https://localhost:5001`

### Start the Frontend (Development Mode)

In a separate terminal:

```bash
cd Web/web.client
npm run dev
```

The React app will be available at `http://localhost:5173`

### Access Points

| URL | Description |
|-----|-------------|
| `https://localhost:5001` | Backend API |
| `https://localhost:5001/swagger` | API documentation |
| `http://localhost:5173` | Frontend (dev server) |

---

## Tunneling for Teams Bot Testing

Teams bots require a publicly accessible HTTPS endpoint. During development, use a tunnel to expose your localhost.

### Option 1: Ngrok (Recommended for Simplicity)

#### Install Ngrok

- **Windows (winget)**: `winget install ngrok.ngrok`
- **Windows (Chocolatey)**: `choco install ngrok`
- **macOS (Homebrew)**: `brew install ngrok`
- **Manual**: Download from [ngrok.com](https://ngrok.com/download)

#### Create Free Account

1. Sign up at [ngrok.com](https://ngrok.com/)
2. Copy your authtoken from the dashboard
3. Configure ngrok:
   ```bash
   ngrok config add-authtoken YOUR_AUTH_TOKEN
   ```

#### Start the Tunnel

```bash
# Expose the backend port (5001)
ngrok http https://localhost:5001
```

You'll see output like:
```
Forwarding    https://abc123.ngrok-free.app -> https://localhost:5001
```

#### Configure Bot Endpoint

1. Copy the ngrok HTTPS URL (e.g., `https://abc123.ngrok-free.app`)
2. Go to [Teams Developer Portal](https://dev.teams.microsoft.com/)
3. Navigate to **Tools** → **Bot management** → Your bot
4. Set **Endpoint address** to: `https://abc123.ngrok-free.app/api/messages`
5. Click **Save**

> **Note**: The ngrok URL changes each time you restart (unless you have a paid plan). Update the bot endpoint accordingly.

### Option 2: VS Code Dev Tunnels

Dev Tunnels are built into VS Code and don't require a separate account.

#### Enable Dev Tunnels

1. Open VS Code
2. Open the Command Palette (`Ctrl+Shift+P` / `Cmd+Shift+P`)
3. Search for **"Forward a Port"**
4. Enter port `5001`
5. Set visibility to **Public** (required for Teams)
6. Copy the generated URL

#### Configure Bot Endpoint

Use the Dev Tunnel URL as your bot's messaging endpoint:
```
https://your-tunnel-url.devtunnels.ms/api/messages
```

### Option 3: Visual Studio Dev Tunnels

Visual Studio 2022 has built-in Dev Tunnels support.

1. Right-click on the `Web.Server` project
2. Select **Properties** → **Debug** → **General**
3. Check **Use dev tunnels**
4. Select **Public** access
5. Run the project - VS will create the tunnel automatically

---

## Testing the Bot Locally

### 1. Start Your Application

```bash
# Terminal 1: Backend
cd Web/Web.Server
dotnet run

# Terminal 2: Frontend
cd Web/web.client
npm run dev

# Terminal 3: Tunnel (choose one)
ngrok http https://localhost:5001
```

### 2. Update Bot Endpoint

Set your tunnel URL + `/api/messages` in the Teams Developer Portal.

### 3. Install the Teams App

1. Go to Teams Developer Portal → **Apps** → Your app
2. Click **Preview in Teams** or download the app package
3. Install to your Teams environment

### 4. Test Bot Interaction

1. Open Teams
2. Find your bot in the chat list or search for it
3. Send a message to verify it responds

---

## Debugging Tips

### View Bot Framework Traffic

Ngrok provides a web interface to inspect traffic:
- Open `http://localhost:4040` in your browser
- View all requests/responses to your tunnel

### Enable Detailed Logging

In `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.Bot": "Debug"
    }
  }
}
```

### Common Issues

| Issue | Solution |
|-------|----------|
| Bot doesn't respond | Check tunnel is running and endpoint is updated |
| 401 Unauthorized | Verify `MicrosoftAppId` and `MicrosoftAppPassword` match |
| Graph API errors | Ensure Graph permissions have admin consent |
| Storage errors | Verify connection string is correct |
| Tunnel URL changed | Update bot endpoint in Teams Developer Portal |

---

## Running Tests

### Unit Tests

```bash
cd src/Full/Bot
dotnet test
```

### With Test Configuration

Create `UnitTests/appsettings.json` (copy from `appsettings.example.json`):

```json
{
  "GraphConfig": {
    "ClientId": "your-test-client-id",
    "ClientSecret": "your-test-client-secret",
    "TenantId": "your-test-tenant-id"
  },
  "ConnectionStrings": {
    "Storage": "your-test-storage-connection-string"
  }
}
```

> **Warning**: Don't commit test configuration with real credentials. The file is in `.gitignore`.

---

## Next Steps

- **[Configuration Reference](CONFIGURATION.md)** - Complete configuration options
- **[Setup Guide](SETUP.md)** - Teams bot setup and Graph permissions
- **[Usage Guide](USAGE.md)** - How to use the application
- **[Deployment Guide](DEPLOYMENT.md)** - Deploy to Azure
