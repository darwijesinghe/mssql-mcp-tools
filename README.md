# Mssql Mcp Tools

![GitHub release](https://img.shields.io/github/v/release/darwijesinghe/mssql-mcp-tools.svg)
[![npm version](https://img.shields.io/npm/v/mssql-mcp-tools.svg)](https://www.npmjs.com/package/mssql-mcp-tools)
![GitHub downloads](https://img.shields.io/github/downloads/darwijesinghe/mssql-mcp-tools/total.svg)
![Build](https://github.com/darwijesinghe/mssql-mcp-tools/actions/workflows/build.yml/badge.svg)
![GitHub license](https://img.shields.io/github/license/darwijesinghe/mssql-mcp-tools.svg)
![Maintenance](https://img.shields.io/maintenance/yes/2026.svg)

A .NET 9 [MCP](https://modelcontextprotocol.io/) server that exposes **read-only** MSSQL Server tools to AI hosts (Cursor, Claude Desktop, etc.) over **stdio**.

## Architecture & security

```
                            +---------------------------+
                            |         MCP host          |
                            |   Cursor / Claude / ...   |
                            |       trusted spawn       |
                            +-------------+-------------+
                                          |
                                          | stdio, JSON-RPC
                                          v
                            +---------------------------+
                            |       MssqlMcpTools       |
                            |     JSON-RPC handler      |
                            +-------------+-------------+
                                          |
             +----------------------------+----------------------------+
             |                            |                            |
             v                            v                            v
+-------------------------+  +-------------------------+  +-------------------------+
|      Catalog tools      |  |  get_object_definition  |  |   execute_read_query    |
|     list / describe     |  |       full script       |  |     QueryValidator      |
|      search / find      |  |      timeout only       |  |       SELECT-only       |
|    row and cell caps    |  |     no row/cell cap     |  |    row and cell caps    |
+------------+------------+  +------------+------------+  +------------+------------+
             |                            |                            |
             +----------------------------+----------------------------+
                                          |
                                          v
                            +---------------------------+
                            |        SqlExecutor        |
                            |   CommandTimeoutSeconds   |
                            |       on every call       |
                            +-------------+-------------+
                                          |
                                          | TDS
                                          v
                            +---------------------------+
                            |       MSSQL Server        |
                            |   least-privilege login   |
                            |        SELECT only        |
                            +---------------------------+
```

No HTTP listener — transport is **stdio only**. Only `execute_read_query` goes through `QueryValidator`. Catalog tools use fixed SQL, with arguments bound by `SqlParameter` where needed. Tabular tools stop at `MaxRows` and `MaxCellLength`. `get_object_definition` returns the full script and is limited by `CommandTimeoutSeconds` only. That timeout applies to every call in `SqlExecutor`. Details: [Project overview](docs/PROJECT_OVERVIEW.md), [Security posture](docs/SECURITY_POSTURE.md).

## Documentation

| Document | Description |
|----------|-------------|
| [Documentation index](docs/index.md) | Master index for all project docs |
| [Project overview](docs/PROJECT_OVERVIEW.md) | Architecture, protocol flow, security, and all MCP tools |
| [Source tree](docs/SOURCE_TREE.md) | Repository layout and file responsibilities |
| [Contributing](CONTRIBUTING.md) | Development setup, conventions, and pull request guidelines |
| [Release notes](RELEASE_NOTES.md) | In-repo version history (the GitHub Release body is generated from commits) |
| [Security policy](SECURITY.md) | How to report vulnerabilities |
| [Security posture](docs/SECURITY_POSTURE.md) | Protections, gaps, permissions, and production guidance |
| [Code of conduct](CODE_OF_CONDUCT.md) | Community standards |

## Requirements

- MSSQL Server reachable from the machine running the server.
- An MCP host that can spawn the process.
- **End users (release install):** Windows x64, Linux x64, or macOS (arm64 / x64); no .NET SDK required.
- **Developers:** [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later (.NET 10 SDK recommended for `dotnet test` with Microsoft.Testing.Platform).

## Install from npm

Requires Node.js 18 or later. No clone and no .NET SDK are required.

Run the launcher directly:

```powershell
npx -y mssql-mcp-tools
```

Or install it and run the command:

```powershell
npm install -g mssql-mcp-tools
mssql-mcp-tools
```

That downloads the GitHub Release this package is built for (Windows x64, Linux x64, macOS arm64, or macOS x64). The first start stops before the server runs and prints the install folder, under `~/.mssql-mcp-tools/` (`%USERPROFILE%\.mssql-mcp-tools\` on Windows). Copy `appsettings.json` there to `appsettings.local.json`, replace the `YOUR_*` connection string and log path, then start it again.

Cursor and Claude do not run that command for you until the server is listed in their MCP config. Use this block. If the file already has other servers, add only the `mssql-mcp-tools` entry inside `mcpServers`.

```json
{
  "mcpServers": {
    "mssql-mcp-tools": {
      "command": "npx",
      "args": ["-y", "mssql-mcp-tools"]
    }
  }
}
```

| Host | Config file |
|------|-------------|
| Cursor, this project | `.cursor/mcp.json` |
| Cursor, every project | `%USERPROFILE%\.cursor\mcp.json` (`~/.cursor/mcp.json` on macOS and Linux) |
| Claude Desktop | Windows: `%APPDATA%\Claude\claude_desktop_config.json`. macOS: `~/Library/Application Support/Claude/claude_desktop_config.json`. Linux: `~/.config/Claude/claude_desktop_config.json` |
| Claude Code | `.mcp.json` in the project |

Restart the host and enable **mssql-mcp-tools**. The connection string stays in `appsettings.local.json` under the install folder above. Do not put it in these MCP files.

## Install from GitHub Release (manual)

For local use in Cursor without cloning or building:

1. Open **[Releases](https://github.com/darwijesinghe/mssql-mcp-tools/releases)** and download the archive for your OS from the latest `v*` tag (see [RELEASE_NOTES.md](RELEASE_NOTES.md)):

   | Platform | Asset |
   |----------|--------|
   | Windows x64 | `mssql-mcp-tools-win-x64.zip` |
   | Linux x64 | `mssql-mcp-tools-linux-x64.tar.gz` |
   | macOS Apple Silicon | `mssql-mcp-tools-osx-arm64.tar.gz` |
   | macOS Intel | `mssql-mcp-tools-osx-x64.tar.gz` |
2. Extract to a folder (for example `C:\Tools\mssql-mcp-tools\` or `~/Tools/mssql-mcp-tools/`). On Linux/macOS, ensure the binary is executable (`chmod +x mssql-mcp-tools` if needed).
3. Copy shipped `appsettings.json` to `appsettings.local.json` beside the binary (`mssql-mcp-tools.exe` on Windows, `mssql-mcp-tools` on Linux/macOS).
4. Edit `appsettings.local.json` — replace the `YOUR_*` placeholders for `Database.ConnectionString` and `Serilog.WriteTo[0].Args.path` (use an OS-appropriate log path).
5. Copy the MCP example for your OS into your Cursor MCP config (e.g. `.cursor/mcp.json` or user settings) and set `command` to the full path of the binary:
   - Windows: [mcp.release.example.json](mcp.release.example.json)
   - Linux / macOS: [mcp.release.unix.example.json](mcp.release.unix.example.json)
6. Restart Cursor and enable the **mssql-mcp-tools** server.

Do **not** commit `appsettings.local.json`, `.runsettings`, `mcp.json`, publish profiles, or other files with real credentials or machine-specific paths (see [.gitignore](.gitignore)). Committed [`.mcp.json`](.mcp.json) is the [Cursor Directory](https://cursor.directory) listing: it starts `mssql-mcp-tools` with `npx -y mssql-mcp-tools` and contains no credentials.

## Quick start (developers)

### Clone and build

```powershell
git clone https://github.com/darwijesinghe/mssql-mcp-tools.git
cd mssql-mcp-tools
dotnet build Mcp.sln
```

Default branch is `main`.

### Configure locally

The server reads configuration from `AppContext.BaseDirectory`, not from the `Mcp.Server/` source folder. For `dotnet run` that directory is `Mcp.Server/bin/Debug/net9.0/` (or `Mcp.Server/bin/Release/net9.0/` after a Release build).

1. Build once so that output folder exists (`dotnet build Mcp.sln`, or the `dotnet run` command below).
2. Copy [appsettings.json](Mcp.Server/appsettings.json) to `appsettings.local.json` in that output folder. The project copies `appsettings.json` there on build. It does not copy `appsettings.local.json`.
3. Edit that `appsettings.local.json` — replace the `YOUR_*` placeholders with your SQL Server connection string and log file path.
4. Leave [appsettings.json](Mcp.Server/appsettings.json) as the masked shared template. A local file left only under `Mcp.Server/` is not loaded.

### Run locally (stdio)

```powershell
dotnet run --project Mcp.Server/Mcp.Server.csproj
```

The server reads JSON-RPC from stdin and writes responses to stdout. Startup fails with exit code **1** if the database is unreachable or configuration is missing.

### Run tests

```powershell
# Unit tests only (matches CI)
dotnet test --solution Mcp.sln --filter "TestCategory!=Integration"
```

Or target the test project directly:

```powershell
dotnet test --project Mcp.Test/Mcp.Test.csproj --filter "TestCategory!=Integration"
```

Unit tests use mocks and do not require SQL Server. Integration tests (`[TestCategory("Integration")]`) need a live database:

1. Run [integration-test-db.sql](Mcp.Test/Script/integration-test-db.sql) to create `mcp_test`.
2. Copy [`.runsettings.example`](Mcp.Test/.runsettings.example) -> `Mcp.Test/.runsettings`, set `DbConnectionString`, then run with `--settings`.

CI skips integration tests.

### Configure in Cursor (from source)

Copy [mcp.example.json](mcp.example.json) to your Cursor MCP config and fix the project path in `args`. Put `appsettings.local.json` in the `dotnet run` output directory (`Mcp.Server/bin/Debug/net9.0/` by default).

## Create a release (maintainers)

Pushing a version tag triggers [.github/workflows/release.yml](.github/workflows/release.yml), which runs unit tests (excludes `Integration`), publishes self-contained single-file binaries for **win-x64**, **linux-x64**, **osx-arm64**, and **osx-x64**, attaches those archives to the GitHub Release, then publishes `launcher/` to npm. The workflow sets the npm version and `RELEASE_TAG` from the git tag. On npmjs.com, the package needs a GitHub Actions trusted publisher for this repository and workflow filename `release.yml`, with `npm publish` allowed.

```powershell
git checkout main
git pull
git tag v1.0.0
git push origin v1.0.0
```

Replace `v1.0.0` with your semver tag. Check the **Actions** tab for the workflow run, then the **Releases** tab for the download. The GitHub Release description is generated from commits (`generate_release_notes`). [RELEASE_NOTES.md](RELEASE_NOTES.md) is the version history in this repository. The workflow does not upload that file as the release body.

## Configuration

Settings are loaded from `AppContext.BaseDirectory`: `appsettings.json`, then optional `appsettings.local.json` overrides. That directory is the build output folder for `dotnet run`, and the folder that contains the binary for a published build or an npm install. It is not the process working directory.

| Section | Key | Description |
|---------|-----|-------------|
| `Database` | `ConnectionString` | MSSQL Server connection string (**required**) |
| `Serilog` | `WriteTo[].Args.path` | Log file path (required for file logging) |
| `QueryOptions` | `MaxRows` | Maximum rows returned per query (default: 500) |
| `QueryOptions` | `MaxCellLength` | Maximum string length per cell before truncation (default: 5000) |
| `QueryOptions` | `CommandTimeoutSeconds` | MSSQL command timeout (default: 30) |

See [appsettings.json](Mcp.Server/appsettings.json) for the masked committed template. Copy it to `appsettings.local.json` in the application base directory and replace the `YOUR_*` placeholders (never commit the local file). For `dotnet run`, use `Mcp.Server/bin/Debug/net9.0/`. For a release binary or the npm launcher, use the folder that contains the binary.

## MCP tools

Responses use JSON-RPC `result` / `error`. `tools/call` puts MCP `content` / `isError` inside `result`. See [Project overview — Response shape](docs/PROJECT_OVERVIEW.md#response-shape) and [Tool results](docs/PROJECT_OVERVIEW.md#tool-results).

| Tool | Arguments | Description |
|------|-----------|-------------|
| `list_tables` | — | User base tables (`SCHEMANAME`, `TABLENAME`) |
| `list_views` | — | Views (`SCHEMANAME`, `VIEWNAME`) |
| `list_procedures` | — | Stored procedures (`SCHEMANAME`, `PROCEDURENAME`) |
| `list_triggers` | — | Triggers (`PARENTOBJECT`, `TRIGGERNAME`, `ISDISABLED`) |
| `list_functions` | — | User functions (`SCHEMANAME`, `FUNCTIONNAME`) |
| `describe_table` | `tableName` | Column metadata for a table (`schema.table` or name) |
| `get_object_definition` | `objectName` | T-SQL for procedure, view, function, or trigger |
| `search_definitions` | `text` | Objects whose module body contains `text` |
| `find_references` | `objectName` | Objects that reference a table or view |
| `execute_read_query` | `sql` | Run a validated **SELECT-only** query |

## Contributing

Contributions are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) for setup, coding conventions, testing, and how to submit a pull request.

## License

This project is licensed under the [MIT License](LICENSE.txt).
