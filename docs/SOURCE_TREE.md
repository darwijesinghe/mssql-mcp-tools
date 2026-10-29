# Source tree

Repository layout (build artifacts `bin/`, `obj/`, and `.vs/` are gitignored).

```
mssql-mcp-tools/                           # Repository root
├── .gitignore
├── .gitattributes
├── global.json                            # .NET test runner configuration
├── LICENSE.txt
├── README.md
├── CONTRIBUTING.md                        # Contribution guidelines
├── CODE_OF_CONDUCT.md                     # Community standards
├── SECURITY.md                            # Vulnerability reporting
├── RELEASE_NOTES.md                       # In-repo version history (not the GitHub Release body)
├── .mcp.json                              # Cursor Directory MCP config (committed; no secrets)
├── mcp.example.json                       # MCP config when running from source (dotnet run)
├── mcp.release.example.json               # MCP config for Windows release binary
├── mcp.release.unix.example.json          # MCP config for Linux / macOS release binary
├── launcher/
│   ├── package.json                       # npm package mssql-mcp-tools (product name MssqlMcpTools)
│   ├── bin.js                             # Downloads the pinned GitHub Release and starts it
│   └── README.md                          # npm launcher install and host config
├── Mcp.sln
├── .github/
│   ├── ISSUE_TEMPLATE/
│   │   ├── bug_report.md
│   │   └── feature_request.md
│   ├── pull_request_template.md
│   └── workflows/
│       ├── build.yml                      # PR build + unit tests (main, dev; skips Integration)
│       └── release.yml                    # Unit tests, multi-RID archives, GitHub Release (body generated from commits), npm publish on v* tag push
├── docs/
│   ├── index.md                           # Documentation master index
│   ├── PROJECT_OVERVIEW.md
│   ├── SECURITY_POSTURE.md                # Protections, gaps, permissions, production guidance
│   └── SOURCE_TREE.md                     # This file
│
├── Mcp.Server/                            # MCP server (net9.0 console)
│   ├── Mcp.Server.csproj
│   ├── Program.cs                         # class Program — Generic Host, DI, Serilog, configuration
│   ├── Startup.cs                         # DB validation, stdio JSON-RPC loop
│   ├── appsettings.json                   # Masked shared template (YOUR_* placeholders; committed)
│   │                                      # Copy to appsettings.local.json in AppContext.BaseDirectory (gitignored)
│   │
│   ├── Models/
│   │   ├── AppSettings.cs                 # Database, QueryOptions, Log binding
│   │   ├── QueryResult.cs                 # Tool result DTO (columns, rows, text)
│   │   ├── JsonRpcRequest.cs              # Inbound: jsonrpc, id, method, params
│   │   ├── JsonRpcResponse.cs             # Outbound: jsonrpc, id, result, error
│   │   ├── McpCallToolResult.cs           # tools/call result: content + isError
│   │   ├── McpContentBlock.cs             # content[] item: type, text
│   │   ├── JsonRpcError.cs                # JSON-RPC error object
│   │   └── ErrorCodes.cs                  # Error codes (-32601, -32602, -32603)
│   │
│   ├── Services/
│   │   ├── McpMessageHandler.cs           # MCP protocol + tool dispatch (10 tools)
│   │   ├── DatabaseService.cs             # Catalog SQL + execute_read_query
│   │   ├── SqlExecutor.cs                 # Shared SQL execution, row limits
│   │   └── Interfaces/
│   │       ├── IDatabaseService.cs        # Database contract for handler and tests
│   │       └── ISqlExecutor.cs            # Low-level SQL execution contract
│   │
│   ├── Utils/
│   │   ├── JsonHelper.cs                  # JsonElement id -> CLR type for responses
│   │   └── QueryValidator.cs              # SELECT-only + fail-closed table-source allow-list (ScriptDom)
│   │
│   └── Properties/                        # May hold local PublishProfiles (gitignored)
│
└── Mcp.Test/                              # Unit and integration tests (MSTest)
    ├── Mcp.Test.csproj
    ├── .runsettings.example               # Template for integration test DB connection (copy -> .runsettings)
    ├── MSTestSettings.cs                  # Parallel test execution (method level)
    ├── McpMessageHandlerTests.cs          # Handler protocol and tool-call coverage
    ├── DatabaseServiceTests.cs            # DatabaseService unit tests (mocked executor)
    ├── DatabaseServiceIntegrationTests.cs # Live SQL Server integration tests
    ├── QueryValidatorTests.cs             # SELECT-only + table-source allow-list coverage
    ├── Script/
    │   └── integration-test-db.sql        # Creates mcp_test DB + objects for integration tests
    └── Helpers/
        ├── McpTestHelper.cs               # JSON-RPC request builders and assertions
        └── TestDatabaseService.cs         # In-memory IDatabaseService for handler tests
```

## File reference

### Root

| Path | Role |
|------|------|
| `.gitignore` | Excludes build output, IDE state, secrets, `appsettings.local.json`, `mcp.json`, `.runsettings`, `PublishProfiles/` |
| `.gitattributes` | Git line-ending and diff settings |
| `global.json` | Configures Microsoft.Testing.Platform as the test runner |
| `LICENSE.txt` | MIT license |
| `README.md` | Entry point, quick start, release install, configuration, and tool summary |
| `CONTRIBUTING.md` | Development setup, conventions, and pull request process |
| `CODE_OF_CONDUCT.md` | Contributor Covenant community standards |
| `SECURITY.md` | Private vulnerability reporting and disclosure policy |
| `RELEASE_NOTES.md` | In-repo version history. The workflow does not upload this file as the GitHub Release body. |
| `.mcp.json` | Committed Cursor Directory config for `mssql-mcp-tools` (`npx -y mssql-mcp-tools`). No secrets or machine paths. |
| `launcher/package.json` | npm package metadata for the release launcher |
| `launcher/bin.js` | Downloads the pinned GitHub Release for the current OS and starts `mssql-mcp-tools` |
| `launcher/README.md` | npm launcher install steps and Cursor / Claude config |
| `mcp.example.json` | MCP template for `dotnet run` (developers) |
| `mcp.release.example.json` | MCP template for Windows release binary (end users) |
| `mcp.release.unix.example.json` | MCP template for Linux / macOS release binary (end users) |
| `.github/workflows/build.yml` | CI: restore, build, and unit tests on PRs to `main`/`dev` (excludes `Integration`) |
| `.github/workflows/release.yml` | CI: unit tests (excludes `Integration`), multi-RID publish, GitHub Release with a body generated from commits, then npm publish of `launcher/` on `v*` tags |
| `.github/ISSUE_TEMPLATE/` | GitHub issue templates for bugs and feature requests |
| `Mcp.sln` | Solution file (Server + Test projects) |
| `docs/index.md` | Master documentation index |
| `docs/SECURITY_POSTURE.md` | Security protections, gaps, SQL permissions, and production guidance |

### `Mcp.Server/Program.cs`

`class Program` with `Main`: builds the Generic Host, clears console logging providers (stdout is MCP-only), loads `appsettings.json` and optional `appsettings.local.json` from `AppContext.BaseDirectory`, configures Serilog file logging, registers `ISqlExecutor`, `IDatabaseService`, and `McpMessageHandler`, then runs `Startup`.

### `Mcp.Server/Startup.cs`

Validates database connectivity (15s timeout), reads stdin lines, deserializes JSON-RPC, calls `McpMessageHandler`, writes one JSON line per response. Exits when stdin closes. On unexpected exceptions after a request is parsed, replies with JSON-RPC `-32603` so the host does not hang. Serialization omits null properties (`DefaultIgnoreCondition.WhenWritingNull`).

### Configuration

| File | Role |
|------|------|
| `appsettings.json` | Masked committed template (`YOUR_*` placeholders for connection string and log path); copied to output |
| `appsettings.local.json` | Machine-specific secrets (gitignored). Copy from `appsettings.json` into `AppContext.BaseDirectory` (the build output folder for `dotnet run`, or the folder that contains a published binary) and replace placeholders. A copy left only in `Mcp.Server/` is not loaded. |

### `Models/`

| File | Role |
|------|------|
| `AppSettings.cs` | Options binding for database, query limits, log settings |
| `QueryResult.cs` | Structured tool output (`Columns`, `Rows`, `RowCount`, `Truncated`, `Text`) |
| `JsonRpcRequest.cs` | Incoming message DTO (`JsonElement` for `id` and `params`) |
| `JsonRpcResponse.cs` | Outgoing message DTO (`id`, `result`, `error`) |
| `McpCallToolResult.cs` | `tools/call` payload (`content`, `isError`) |
| `McpContentBlock.cs` | Content block (`type`, `text`) |
| `JsonRpcError.cs` | JSON-RPC error payload (`code`, `message`, optional `data`) |
| `ErrorCodes.cs` | `MethodNotFound` (-32601), `InvalidParams` (-32602), `InternalError` (-32603) |

### `Services/`

| File | Role |
|------|------|
| `McpMessageHandler.cs` | `initialize`, `ping`, `tools/list`, `tools/call`; MCP `content` inside `result`; JSON-RPC `error` for protocol failures |
| `DatabaseService.cs` | Catalog queries and `ExecuteReadQueryAsync` |
| `SqlExecutor.cs` | Executes SQL with row/cell limits and timeouts |
| `Interfaces/IDatabaseService.cs` | Public contract implemented by `DatabaseService` |
| `Interfaces/ISqlExecutor.cs` | Execution contract implemented by `SqlExecutor` |

### `Utils/`

| File | Role |
|------|------|
| `JsonHelper.cs` | `ConvertId` extension for JSON-RPC response ids |
| `QueryValidator.cs` | Parses SELECT-only SQL for `execute_read_query`; allow-lists named tables/views, derived tables, and joins; rejects TVFs, OPEN*, table variables, and unrecognized table sources |

### `Mcp.Server.csproj`

- Target: `net9.0` executable
- Assembly name: `mssql-mcp-tools` (published file `mssql-mcp-tools.exe` on Windows, `mssql-mcp-tools` on Linux / macOS)
- Packages: `Microsoft.Data.SqlClient`, `Microsoft.Extensions.Hosting`, Serilog, ScriptDom
- `GenerateDocumentationFile` enabled
- Copies `appsettings.json` to output directory

### `Mcp.Test/`

| Path | Role |
|------|------|
| `McpMessageHandlerTests.cs` | Handler protocol and tool-call coverage |
| `DatabaseServiceTests.cs` | `DatabaseService` with mocked `ISqlExecutor` |
| `DatabaseServiceIntegrationTests.cs` | Live SQL Server tests (`[TestCategory("Integration")]`) |
| `QueryValidatorTests.cs` | `QueryValidator` allow/deny coverage (SELECT-only + table-source allow-list) |
| `Script/integration-test-db.sql` | Creates `mcp_test` with tables/views/procs/functions/trigger + seed data |
| `.runsettings.example` | Template — copy to `.runsettings` and set `DbConnectionString` |
| `.runsettings` | Local integration DB credentials (gitignored) |
| `Helpers/McpTestHelper.cs` | Builds `JsonRpcRequest` payloads; reads tool `content` / `isError`; asserts protocol errors |
| `Helpers/TestDatabaseService.cs` | Test double for `IDatabaseService` |
| `MSTestSettings.cs` | `[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]` |

### Not committed (gitignored)

| Pattern | Reason |
|---------|--------|
| `bin/`, `obj/` | Build output |
| `.vs/`, `*.user` | IDE machine state |
| `appsettings.local.json`, `appsettings.*.local.json` | May contain connection strings |
| `mcp.json`, `.env` | May contain secrets |
| `**/.runsettings` | Integration test connection strings |
| `**/Properties/PublishProfiles/` | Local Visual Studio publish profiles (machine-specific paths) |

`.mcp.json` is committed. It is the Cursor Directory listing (`npx -y mssql-mcp-tools`) and must not contain connection strings or machine-specific paths. The launcher stores the release and `appsettings.local.json` under the user profile (`.mssql-mcp-tools/`), not in git. A local host file named `mcp.json` stays gitignored.

## Dependency flow

```
Program.cs
    ├── Configuration (appsettings.json + appsettings.local.json)
    ├── Serilog
    ├── ISqlExecutor <- SqlExecutor
    ├── IDatabaseService <- DatabaseService
    └── Startup
            ├── McpMessageHandler
            │       ├── IDatabaseService
            │       └── ILogger<McpMessageHandler>
            └── IDatabaseService (connection validation)

DatabaseService
    ├── catalog row tools (list / describe / search / find)
    │       └── ISqlExecutor.ExecuteMultiColumnRowsAsync
    │               MaxRows, MaxCellLength, CommandTimeoutSeconds
    ├── get_object_definition
    │       └── ISqlExecutor.ExecuteScalarStringAsync
    │               full script, CommandTimeoutSeconds only
    └── execute_read_query
            └── QueryValidator -> ISqlExecutor.ExecuteMultiColumnRowsAsync
                    MaxRows, MaxCellLength, CommandTimeoutSeconds

Mcp.Test
    ├── TestDatabaseService : IDatabaseService
    ├── McpMessageHandlerTests -> McpMessageHandler
    ├── DatabaseServiceTests -> DatabaseService (mock ISqlExecutor)
    ├── QueryValidatorTests -> QueryValidator
    └── DatabaseServiceIntegrationTests -> DatabaseService (live SQL)
```

## Namespaces

| Namespace | Contents |
|-----------|----------|
| `Mcp.Server` | `Startup` |
| `Mcp.Server.Models` | DTOs, settings, and error codes |
| `Mcp.Server.Services` | Handler, database access, SQL executor |
| `Mcp.Server.Services.Interfaces` | `IDatabaseService`, `ISqlExecutor` |
| `Mcp.Server.Utils` | JSON helpers and query validation |
| *(file-scoped / global)* | `class Program` in `Program.cs` |
