# Contributing to MssqlMcpTools

Thank you for your interest in contributing. This guide covers how to set up a development environment, follow project conventions, and submit changes.

## Before you start

- Read the [documentation index](docs/index.md) and [project overview](docs/PROJECT_OVERVIEW.md) for architecture, MCP protocol flow, and security constraints.
- Review the [source tree](docs/SOURCE_TREE.md) to understand where code lives.
- MCP tools are **read-only** — catalog queries plus `execute_read_query` (SELECT-only, validated via ScriptDom).
- Follow the [code of conduct](CODE_OF_CONDUCT.md). Report security issues per [SECURITY.md](SECURITY.md), not public issues.

## Development setup

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later. `global.json` selects the Microsoft.Testing.Platform test runner and does not pin an SDK. CI installs the .NET 10 SDK in [build.yml](.github/workflows/build.yml) and [release.yml](.github/workflows/release.yml).
- SQL Server reachable from your machine (optional for unit tests; required for integration tests and manual checks).
- An MCP host such as Cursor (optional, for end-to-end testing).

### Clone and build

```powershell
git clone https://github.com/darwijesinghe/mssql-mcp-tools.git
cd mssql-mcp-tools
dotnet build Mcp.sln
```

Default branch is `main`.

### Configure appsettings

1. Build once so the output folder exists. The server loads configuration from `AppContext.BaseDirectory`, which for `dotnet run` is `Mcp.Server/bin/Debug/net9.0/` (or `Mcp.Server/bin/Release/net9.0/` after a Release build).
2. Copy [appsettings.json](Mcp.Server/appsettings.json) to `appsettings.local.json` in that output folder. A copy left only in `Mcp.Server/` is not loaded.
3. Replace the `YOUR_*` placeholders (`Database.ConnectionString` and Serilog log path) in the local file.
4. Never commit `appsettings.local.json` (see [.gitignore](.gitignore)).

### Run tests

Unit and handler tests use mocks and do **not** require SQL Server. Prefer the same filter CI uses:

```powershell
dotnet test --solution Mcp.sln --filter "TestCategory!=Integration"
```

Or target the test project directly:

```powershell
dotnet test --project Mcp.Test/Mcp.Test.csproj --filter "TestCategory!=Integration"
```

`DatabaseServiceIntegrationTests` (`[TestCategory("Integration")]`) require a live SQL Server:

1. Run [integration-test-db.sql](Mcp.Test/Script/integration-test-db.sql) to create database `mcp_test` and the objects the tests expect.
2. Copy [`.runsettings.example`](Mcp.Test/.runsettings.example) to `Mcp.Test/.runsettings`, set `DbConnectionString`, and never commit `.runsettings`.

They are excluded from GitHub Actions.

### Run locally (stdio)

```powershell
dotnet run --project Mcp.Server/Mcp.Server.csproj
```

For Cursor integration from source, copy [mcp.example.json](mcp.example.json), adjust the project path in `args`, and put `appsettings.local.json` in the `dotnet run` output directory.

## What to contribute

Good contributions include:

- Bug fixes with tests.
- New read-only catalog tools (with handler, service, executor, test double, and tests).
- Improvements to `QueryValidator` safety rules.
- Documentation improvements.
- Test coverage for existing behavior.

Out of scope unless discussed with maintainers first:

- HTTP or non-stdio transports.
- Write SQL (INSERT, UPDATE, DELETE, DDL) or bypassing query validation.
- Breaking changes to existing tool output formats without a version bump plan.

## Code conventions

### C# style

- Match existing patterns in the file you are editing (namespaces, naming, structure).
- Keep changes focused; avoid unrelated refactors in the same pull request.
- `GenerateDocumentationFile` is enabled — public APIs need XML documentation.

When you **modify** an existing method, class, or interface, add or update a `<remarks>` block describing what changed and why:

```csharp
/// <remarks>
/// This method has been updated to [clear explanation of what changed and why].
/// <para>
/// Author: Your Name<br/>
/// Last Updated: dd/MM/yyyy
/// </para>
/// </remarks>
```

Include XML `<summary>`, `<param>`, `<returns>`, and `<exception>` tags on public members. Add inline comments only for non-obvious logic.

### Adding a new MCP tool

Follow the extension points documented in [Project overview — Extension points](docs/PROJECT_OVERVIEW.md#extension-points):

1. Add a method to `IDatabaseService` and implement it in `DatabaseService` (read-only SQL only).
2. Register the tool in `McpMessageHandler` (`HandleToolsList` and `HandleToolCallAsync`).
3. Update `TestDatabaseService` in `Mcp.Test/Helpers/` with test data for the new method.
4. Add unit tests in `McpMessageHandlerTests` and/or `DatabaseServiceTests`.
5. Update [README.md](README.md) tool table and [docs/PROJECT_OVERVIEW.md](docs/PROJECT_OVERVIEW.md) if behavior is user-facing.

### Project layout

| Project | Purpose |
|---------|---------|
| `Mcp.Server` | MCP server executable (Generic Host, DI, Serilog) |
| `Mcp.Test` | MSTest unit and integration tests |

Do not commit build output (`bin/`, `obj/`), IDE state, publish profiles (`Properties/PublishProfiles/`), or files that may contain secrets (`appsettings.local.json`, `mcp.json`, `.runsettings`, `.env`). Committed [`.mcp.json`](.mcp.json) is the Cursor Directory listing (`npx -y mssql-mcp-tools`) and must stay free of connection strings and machine-specific paths. The launcher cache (`.mssql-mcp-tools/` in the user profile) holds the downloaded release and local `appsettings.local.json`.

## Security

- Never commit connection strings, passwords, `appsettings.local.json`, `.runsettings`, or real `mcp.json` configs. Keep [`.mcp.json`](.mcp.json) free of those values; it is the public Cursor Directory config.
- Do not commit Visual Studio publish profiles with machine-specific paths.
- Use a SQL login with least privilege (metadata read access; avoid `sa` in shared environments).
- Treat MCP hosts as trusted only when you control them.
- Any new SQL surface must go through validation (`QueryValidator`) or use fixed, parameterized catalog SQL.

## Commit messages

Use [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <short summary>
```

Types: `feat`, `fix`, `chore`, `docs`, `refactor`, `test`, `perf`, `ci`, `build`, `revert`.

Examples:

```
feat(tools): add list_indexes catalog tool

fix(handler): return InvalidParams when tableName is missing

docs(readme): document appsettings configuration
```

Keep commits small and focused. Each commit should build and pass tests.

## Pull request process

1. Fork the repository and create a feature branch from `main`.
2. Make your changes and add or update tests as needed.
3. Run `dotnet test --solution Mcp.sln --filter "TestCategory!=Integration"` locally.
4. Open a pull request targeting `main` with:
   - A clear summary of what changed and why.
   - Steps to verify the change (test commands, manual checks).
   - Notes on any documentation updates.
5. Address review feedback. Maintainers may squash or rebase before merge.

CI (`.github/workflows/build.yml`) runs on pull requests targeting `main` or `dev`.

## Releases (maintainers)

Pushing a semver tag matching `v*` triggers [.github/workflows/release.yml](.github/workflows/release.yml):

```powershell
git checkout main
git pull
git tag v1.0.0
git push origin v1.0.0
```

The workflow runs unit tests (excludes `Integration`), publishes self-contained single-file binaries for **win-x64**, **linux-x64**, **osx-arm64**, and **osx-x64**, attaches those archives to the GitHub Release, then publishes `launcher/` to npm. The npm version and `RELEASE_TAG` come from the git tag. The GitHub Release description is generated from commits. [RELEASE_NOTES.md](RELEASE_NOTES.md) stays in the repository and is not uploaded as that body. npmjs.com needs a GitHub Actions trusted publisher for workflow `release.yml` with `npm publish` allowed. End users must add their own `appsettings.local.json` beside the binary.

## Questions

Open an issue for bugs, feature ideas, or questions before large changes. For small fixes, a pull request with a short description is fine.

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE.txt).
