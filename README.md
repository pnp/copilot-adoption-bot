# Copilot Adoption Bot

**Accelerate Microsoft 365 Copilot adoption with targeted, contextual guidance delivered directly in Microsoft Teams.**

![Copilot Adoption Bot Demo](docs/images/office-nudge-bot-demo.png)

A Teams bot that sends beautifully designed adaptive cards with Copilot tips, prompt examples, and best practices to help your users get the most out of Microsoft 365 Copilot. Perfect for IT teams and adoption specialists looking to drive real behavior change and maximize Copilot ROI.

---

## Why This Bot?

Traditional training emails get ignored. Long training sessions are forgotten. **Copilot Adoption Bot meets users where they work**—right in Microsoft Teams—with bite-sized, actionable guidance at the perfect moment.

| Traditional Email | Copilot Adoption Bot |
|------------------|------------------|
| Often ignored or filtered | Appears directly in Teams chat |
| Static content | Interactive adaptive cards |
| No engagement tracking | Full delivery and interaction logging |
| One-size-fits-all | Targeted to specific users or groups |
| Manual sending | Scheduled and automated delivery |

---

## Key Use Cases

### Copilot Adoption (Primary Focus)
- **Copilot Chat Tips** - Ready-to-use prompts and best practices
- **App-Specific Guidance** - Tips for Copilot in Outlook, Word, Excel, PowerPoint, Teams
- **Prompt Engineering** - Help users craft better prompts
- **Feature Discovery** - Introduce new Copilot capabilities
- **Adoption Campaigns** - Progressive tip delivery over time

### Additional Scenarios
- **Employee Onboarding** - Welcome messages and getting-started resources
- **Training Reinforcement** - Follow-up nudges after training sessions
- **Feature Announcements** - New Microsoft 365 features
- **Feedback Collection** - Gather user feedback through interactive cards

---

## Key Features

- **Template Management** - Create and edit adaptive card templates
- **Azure Storage Only** - No SQL database required (Table + Blob Storage)
- **Teams Bot Integration** - Direct delivery via Teams bot conversations
- **Message Logging** - Track delivery status and recipients
- **User Cache with Delta Queries** - Efficient user data syncing from Microsoft Graph
- **Copilot Usage Statistics** - Optional per-user Microsoft 365 Copilot activity tracking
- **Smart Groups** - AI-powered dynamic user targeting (requires AI Foundry)
- **Authentication** - Teams SSO and MSAL support
- **Modern UI** - React-based interface with Fluent UI components

---

## Technology Stack

**Backend**
- .NET 10 | ASP.NET Core | Microsoft Bot Framework
- Azure Table Storage | Azure Blob Storage
- Microsoft Graph API

**Frontend**
- React 18 | TypeScript | Fluent UI
- Vite | Azure MSAL | Teams JS SDK

---

## Documentation

| Document | Description |
|----------|-------------|
| **[Setup Guide](docs/SETUP.md)** | Installation, configuration, and running the app |
| **[Usage Guide](docs/USAGE.md)** | How to create templates and send messages |
| **[Features Guide](docs/FEATURES.md)** | Detailed feature documentation |
| **[Deployment Guide](DEPLOYMENT.md)** | Azure deployment and CI/CD pipelines |
| **[Security Guide](docs/SECURITY.md)** | Security best practices |
| **[Troubleshooting](docs/TROUBLESHOOTING.md)** | Common issues and solutions |

---

## Quick Start

### 1. Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 18+](https://nodejs.org/)
- [Azure Subscription](https://azure.microsoft.com/free/)
- [Microsoft 365 tenant](https://developer.microsoft.com/microsoft-365/dev-program) with Teams

### 2. Clone & Install

```bash
git clone https://github.com/pnp/copilot-adoption-bot.git
cd copilot-adoption-bot/src/Full/Bot

# Backend
dotnet restore
dotnet build

# Frontend
cd Web/web.client
npm install
npm run build
```

### 3. Configure

See the **[Setup Guide](docs/SETUP.md)** for detailed configuration steps:
- Create Teams bot in Developer Portal
- Configure Microsoft Graph permissions
- Set up user secrets for local development
- Configure Azure resources

### 4. Run Locally

```bash
# Backend
cd Web/Web.Server
dotnet run

# Frontend (in another terminal)
cd Web/web.client
npm run dev
```

Visit `https://localhost:5001` for the API and `http://localhost:5173` for the web interface.

---

## Deployment

Deploy to Azure App Service using:

**Manual Deployment:**
```bash
cd src/Full/Bot
dotnet publish Web/Web.Server/Web.Server.csproj -c Release -o ./publish
az webapp deploy --resource-group myResourceGroup --name myAppName --src-path ./publish
```

**Automated CI/CD:**
- GitHub Actions (`.github/workflows/azure-deploy.yml`)
- Azure DevOps (`.azure-pipelines/azure-deploy.yml`)

See the **[Deployment Guide](DEPLOYMENT.md)** for complete instructions.

---

## Project Structure

```
src/Full/Bot/
??? Web.Server/          # ASP.NET Core Web API and Teams bot
??? web.client/          # React frontend application
??? Common.Engine/       # Core business logic and services
??? Common.DataUtils/    # Data access and storage utilities
??? UnitTests/          # Unit and integration tests
```

---

## Security

**Key security practices:**
- Use Azure Key Vault for secrets in production
- Use `dotnet user-secrets` for local development
- Enable HTTPS only
- Implement proper RBAC for Azure resources
- Validate all adaptive card JSON before storage
- Regularly rotate secrets and keys

See the **[Security Guide](docs/SECURITY.md)** for comprehensive security best practices.

---

## License

This project is provided as-is under the MIT License. See [LICENSE](LICENSE) for details.

---

## Contributing

Contributions are welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

---

## Support

- **Issues**: Report bugs or request features via [GitHub Issues](https://github.com/pnp/copilot-adoption-bot/issues)
- **Documentation**: Check our comprehensive [documentation](#documentation)
- **Troubleshooting**: See the [Troubleshooting Guide](docs/TROUBLESHOOTING.md)

---

## Community

This project is part of the [Microsoft 365 & Power Platform Community](https://pnp.github.io/).

- **Twitter**: [@m365pnp](https://twitter.com/m365pnp)
- **YouTube**: [Microsoft 365 Community](https://aka.ms/m365pnp/videos)
- **Blog**: [Microsoft 365 Community Blog](https://pnp.github.io/blog/)

---

Made with ?? by the Microsoft 365 Community
