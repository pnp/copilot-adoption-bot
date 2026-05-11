# Copilot instructions for the Copilot Adoption Bot

This file teaches Copilot (and other AI assistants) the conventions, hot paths and
gotchas of this repo. Keep it short, specific and current.

## Repo layout

- `src/Full/Bot/Adoption Bot.sln` – the solution. All C# work happens under here.
  - `Common/Engine` (`Engine.csproj`) – the core library: services, storage managers,
    background workers, bot conversation cache, Graph + AI Foundry integration.
  - `Common/DataUtils` – shared helpers.
  - `Web/Web.Server` (ASP.NET Core, `net10.0`) – API, Bot Framework adapter, DI root.
  - `Web/web.client` – React + Vite frontend (Node 20 LTS).
  - `UnitTests` – MSTest project; mix of pure unit tests (`UnitTests.Services.*`) and
    Azure-backed integration tests (`UnitTests.IntegrationTests.*`).
- `docs/` – user-facing setup / deployment / troubleshooting docs.
  Keep them in sync when you change configuration keys, runtimes, ports or table names.
- `.github/workflows/azure-deploy.yml` and `.azure-pipelines/azure-deploy.yml` – CI/CD.

## Tech & versions

- **Target framework**: `net10.0` everywhere. Do not introduce `netstandard*` or older
  TFMs. Prefer modern C# (collection expressions, primary constructors, `required`,
  `ArgumentNullException.ThrowIfNull`).
- **Test framework**: MSTest 3.x. Use `[TestClass]` / `[TestMethod]` and
  `Assert.*` / `CollectionAssert.*`. No xUnit / NUnit.
- **Azure SDKs**: `Azure.Data.Tables`, `Azure.Storage.Blobs`, `Azure.Storage.Queues`,
  `Azure.Identity`. Use `*Async` overloads inside `async` methods – never the
  synchronous `client.Query` / `client.GetEntity` / `client.AddEntity` overloads.
- **Graph**: `Microsoft.Graph` v5 SDK. There is exactly one `GraphServiceClient`
  registered as a singleton in `Common/Engine/DependencyInjection/GraphServiceExtensions.cs`.
  New services must take it by constructor injection – do **not** new up your own
  `ClientSecretCredential` + `GraphServiceClient`.
- **Frontend**: Vite + MSAL; env vars must be prefixed `VITE_`.

## Architectural conventions

- **DI composition root**: `Common/Engine/DependencyInjection.cs`
  (`AddBotServices` / `AddTeamsAppServices`). Register new services here, not in
  `Program.cs`. Honour the existing extension-method split
  (`AddGraphServices`, `AddMessageTemplateServices`, `AddStatisticsServices`,
  `AddSmartGroupServices`).
- **Storage auth**: read through `GetStorageAuthConfig(AppConfig)`. Prefer
  `StorageAuthConfig` (RBAC + `DefaultAzureCredential`) over the legacy
  `ConnectionStrings:Storage` path.
- **Pure helpers + I/O wrappers**: heavy I/O classes have a paired pure helper that
  is unit-tested in isolation. Follow this pattern when you add new logic:
  - `CopilotUsageCsvParser` ↔ `GraphCopilotStatsLoader`
  - `StatisticsCalculator` ↔ `StatisticsService`
  - `PendingCardMaterializer` ↔ `PendingCardLookupService`
  - `ODataFilter`, `TableBatch` – cross-cutting storage helpers
- **Background work**: long-running work goes through `BatchQueueService`
  (Azure Storage Queue, queue name `batch-messages`) and is consumed by
  `BatchMessageProcessorService` with bounded parallelism. Don't delete a queue
  message on a transient failure – let the visibility timeout redeliver it.
- **Single shared `HttpClient`**: `GraphCopilotStatsLoader` uses a `static readonly`
  shared `HttpClient`. Don't instantiate ad-hoc `HttpClient`s for outbound calls –
  reuse or take `IHttpClientFactory` if you must.

## Azure Storage rules (read these before touching storage)

- **All OData literals from user input must be escaped** with
  `Common.Engine.Storage.ODataFilter.EscapeLiteral(value)` before being interpolated
  into a filter string. UPNs can contain `'` (e.g. `o'connor@contoso.com`) which both
  breaks the query and is an injection vector.
- **Batch writes/deletes** with `Common.Engine.Storage.TableBatch.SubmitInBatchesAsync`.
  Azure Table transactions cap at 100 ops per batch and require a shared partition key.
  Always include a per-entity fallback on `RequestFailedException` so a single bad row
  doesn't abort the whole job.
- **Tables in use** (all auto-created on first run):
  `messagetemplates`, `messagebatches`, `messagelogs`, `ConversationCache`, `usercache`,
  `usersyncmetadata`, `smartgroups`, `smartgroupmembers`, `appsettings`. Blob container:
  `message-templates`. Queue: `batch-messages`. Keep `docs/DEPLOYMENT.md` in sync if you
  add/rename any.
- **RBAC roles required** (RBAC path): `Storage Blob Data Contributor`,
  `Storage Table Data Contributor`, `Storage Queue Data Contributor`.

## Bot conventions

- `BotConversationCache.GetCachedUser` is an in-memory `ConcurrentDictionary` – O(1)
  `TryGetValue`. Don't reintroduce `Values.Where(...).SingleOrDefault()` scans.
- `MessageSenderService` resolves a scoped `MessageTemplateService` inside the
  background worker via `IServiceProvider.CreateScope()`. Do the same for any scoped
  dependency you consume from a singleton/hosted service.
- The bot's messaging endpoint is `/api/messages` – configured in the Teams
  Developer Portal, served by `BotController`.

## Testing rules

- **Pure unit tests** go under `UnitTests/Services/` and must not reference any Azure
  resource, Graph client or filesystem state. They are gated in CI by
  `--filter FullyQualifiedName~UnitTests.Services` and always run.
- **Integration tests** go under `UnitTests/IntegrationTests/`, extend `AbstractTest`,
  and read `appsettings.json`. They only run in CI when `TESTS_APPSETTINGS_JSON`
  is provided.
- New behaviour needs a test in the matching pure helper. If a class needs Azure to
  test, extract the testable part into a static/pure helper first (see the pattern
  in *Architectural conventions* above).
- Use `Assert.ThrowsException<T>` / `Assert.ThrowsExceptionAsync<T>` – not
  `[ExpectedException]`.

## Local dev quick-reference

- Backend: `cd Web/Web.Server && dotnet run` – default URLs come from
  `Properties/launchSettings.json` (`https://localhost:7053`, `http://localhost:5295`).
- Frontend: `cd Web/web.client && npm install && npm run dev` – Vite serves the
  React app on **`https://localhost:5173`** (HTTPS) and proxies `/api/*` to the
  backend on `7053`. **Open `https://localhost:5173` in the browser**, not the
  backend port.
- Tunnel for bot testing: expose the **backend HTTPS** port (`7053` by default) –
  the bot messaging endpoint `/api/messages` is served by `Web.Server`, not by
  Vite. Update the bot endpoint in the Teams Developer Portal to
  `https://<tunnel>/api/messages`.
- Secrets live in **.NET User Secrets** (`Web.Server` project + `UnitTests`
  project, each with their own `UserSecretsId`). Local dev typically points at
  **Azurite** (`UseDevelopmentStorage=true` in `StorageAuthConfig.ConnectionString`
  / `ConnectionStrings:Storage`, `UseRBAC=false`). Never commit secrets or
  `appsettings.json` with real values.

## Style

- Don't add comments unless they explain a non-obvious choice. Match the surrounding
  style of the file.
- Logging: `ILogger<T>` with structured templates (`"... {RecipientUpn}"`) for new
  code. Some legacy methods use interpolated strings – fine to keep on touch.
- Keep async methods truly async. Don't `.Result` / `.Wait()`.

## Things that break easily – check before you change them

- DI registrations (singleton vs scoped) – the queue worker is a singleton hosted
  service; pulling scoped services in directly will throw at runtime.
- Graph permissions: `User.Read.All`, `Reports.Read.All` (optional, for Copilot
  stats), `TeamsActivity.Send`, `TeamsAppInstallation.ReadWriteForUser.All`.
  Adding a Graph call usually means adding a permission and requesting admin
  consent – update `docs/SETUP.md` if you do.
- The `WebAuthConfig` app registration is **separate** from the bot/Graph one.
- Runtime: App Service `.NET 10` stack identifier (`DOTNET|10.0` / `DOTNETCORE|10.0`)
  is regionally rolled out. Don't hard-code expectations of its availability in
  deployment docs.

## Agent behavior (applies to all work in this repo)

- **Never run `git commit` or `git push` unless the user explicitly asks.** Make
  edits and stop. The user reviews the diff and decides when to commit.
- This rule applies to every folder in this repository, not just `src/Full/Bot`.

## When in doubt

Re-read this file. If you change something that contradicts it, update this file in
the same PR.
