# Project overview

## Purpose

**MssqlMcpTools** is an MCP server that lets an AI assistant explore a MSSQL Server database through **read-only** tools — fixed catalog queries plus validated SELECT execution. The host (Cursor, Claude Desktop, etc.) spawns this process and exchanges JSON-RPC messages over **stdin/stdout**, one message per line.

There is no HTTP endpoint. Notifications do not receive a response line.

## Technology stack

| Component | Choice |
|-----------|--------|
| Runtime | .NET 9 |
| Host / DI | [Microsoft.Extensions.Hosting](https://www.nuget.org/packages/Microsoft.Extensions.Hosting) 10.x |
| Logging | [Serilog](https://serilog.net/) (file sink via configuration) |
| Database driver | [Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient) 7.x |
| SQL validation | [Microsoft.SqlServer.TransactSql.ScriptDom](https://www.nuget.org/packages/Microsoft.SqlServer.TransactSql.ScriptDom) 180.x |
| Protocol | MCP over stdio, JSON-RPC 2.0 |
| Test framework | MSTest 4.x, Moq |
| Solution | `Mcp` (`Mcp.Server` executable + `Mcp.Test`) |

## High-level architecture

```
                            +---------------------------+
                            |         MCP host          |
                            |   Cursor / Claude / ...   |
                            |       trusted spawn       |
                            +-------------+-------------+
                                          |
                                          | stdio, JSON-RPC
                                          v
                            +---------------------------+    +---------------------------+
                            |        Program.cs         |    |          Serilog          |
                            |       Generic Host        |--> |         file sink         |
                            |        appsettings        |    |     from appsettings      |
                            +-------------+-------------+    +---------------------------+
                                          |
                                          v
                            +---------------------------+
                            |        Startup.cs         |
                            |        stdio loop         |
                            +-------------+-------------+
                                          |
                                          v
                            +---------------------------+
                            |     McpMessageHandler     |
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
                            |      DatabaseService      |
                            |       ISqlExecutor        |
                            |   CommandTimeoutSeconds   |
                            |       on every call       |
                            +-------------+-------------+
                                          |
                                          | TDS
                                          v
                            +---------------------------+
                            |       MSSQL Server        |
                            |   least-privilege login   |
                            +---------------------------+
```

### Startup

1. `Program.cs` (`class Program`) builds the Generic Host, loads `appsettings.json` then optional `appsettings.local.json` from `AppContext.BaseDirectory`, configures Serilog, and registers services.
2. `Startup.Run()` validates SQL connectivity via `IDatabaseService.ValidateConnectionAsync()` — returns `false` on failure/timeout; process exits with code **1**.
3. Enter the read loop on `Console.ReadLine()`.

### Per-message handling

1. Deserialize `JsonRpcRequest`.
2. `McpMessageHandler.HandleAsync` — returns `null` for `notifications/*`.
3. Serialize `JsonRpcResponse` (null properties omitted) and write one line to stdout.

Per-message exceptions are logged; the loop continues. Startup failures log and exit with code **1**. Database validation is capped at **15 seconds** so a hung SQL connect cannot exceed Cursor’s MCP client timeout.

## Response shape

Replies use standard JSON-RPC 2.0: success sets `result`, protocol failures set `error`.

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": { }
}
```

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "error": { "code": -32601, "message": "..." }
}
```

## MCP methods

| Method | Behavior |
|--------|----------|
| `initialize` | `result` has `protocolVersion` (`2025-11-25`), `capabilities.tools`, `serverInfo` (`mssql-mcp-tools` / assembly version) |
| `ping` | Empty `result` object |
| `tools/list` | `result.tools` array (name, description, `inputSchema`) |
| `tools/call` | Run tool from `params.name`; MCP `content` / `isError` inside `result` |
| `notifications/*` | Ignored (no stdout) |

Error codes live in `Models/ErrorCodes.cs` on the JSON-RPC `error` object (or inside tool results for execution failures):

| Code | Enum | When |
|------|------|------|
| `-32601` | `MethodNotFound` | Unknown JSON-RPC method |
| `-32602` | `InvalidParams` | Unknown tool, missing tool name, or missing required argument |
| `-32603` | `InternalError` | Unexpected exception in the stdio loop after a request was parsed |

Request `id` values are normalized via `JsonHelper.ConvertId` (number, string, or null) before echoing in responses.

## MCP tools

| Tool | `arguments` | Data source | Payload (inside MCP text) |
|------|-------------|-------------|---------------------------|
| `list_tables` | — | `sys.tables` | `QueryResult` rows: `SCHEMANAME`, `TABLENAME` |
| `list_views` | — | `sys.views` | `QueryResult` rows: `SCHEMANAME`, `VIEWNAME` |
| `list_procedures` | — | `sys.procedures` | `QueryResult` rows: `SCHEMANAME`, `PROCEDURENAME` |
| `list_triggers` | — | `sys.triggers` | `QueryResult` rows: `PARENTOBJECT`, `TRIGGERNAME`, `ISDISABLED` |
| `list_functions` | — | `sys.objects` (`FN`, `IF`, `TF`) | `QueryResult` rows: `SCHEMANAME`, `FUNCTIONNAME` |
| `describe_table` | `tableName` | `INFORMATION_SCHEMA.COLUMNS` | `QueryResult` rows: `COLUMN_NAME`, `DATA_TYPE`, `IS_NULLABLE`, `CHARACTER_MAXIMUM_LENGTH` |
| `get_object_definition` | `objectName` | `OBJECT_DEFINITION` / `sys.objects` | Plain T-SQL / message text |
| `search_definitions` | `text` | `sys.sql_modules` (LIKE) | `QueryResult` rows: `TYPE_DESC`, `SCHEMANAME`, `NAME` |
| `find_references` | `objectName` | `sys.sql_expression_dependencies` | `QueryResult` rows: `REFERENCING_SCHEMA_NAME`, `REFERENCING_OBJECT_NAME` |
| `execute_read_query` | `sql` | User-supplied SELECT | `QueryResult` rows (validated, limited) |

### Tool results

Successful `tools/call` responses use MCP CallToolResult inside JSON-RPC `result`:

```json
{
  "jsonrpc": "2.0",
  "id": 4,
  "result": {
    "content": [ { "type": "text", "text": "..." } ],
    "isError": false
  }
}
```

- Definition-style payloads (`get_object_definition`, or test doubles with only `Text`) put that string in `content[0].text`.
- Tabular payloads serialize the internal **`QueryResult`** as JSON in `content[0].text` (PascalCase: `Columns`, `Rows`, `RowCount`, `Truncated`, optional `Text`).

Tool execution failures (validation, SQL errors) still return JSON-RPC **success** with:

```json
{
  "jsonrpc": "2.0",
  "id": 4,
  "result": {
    "content": [ { "type": "text", "text": "error message" } ],
    "isError": true
  }
}
```

Protocol mistakes (unknown method / unknown tool / missing args) use JSON-RPC `error` (`-32601` / `-32602`). Unexpected loop exceptions reply with `-32603` so the client does not hang.

Limits come from `QueryOptions` in appsettings. Tabular tools truncate long string cells to `MaxCellLength` and stop after `MaxRows`. `get_object_definition` returns the full script and is limited by `CommandTimeoutSeconds` only.

### Notes

- **Qualified names:** Many tools accept `schema.object` or a bare name (bare names may match multiple schemas).
- **Read-only:** Catalog SQL is fixed in `DatabaseService`. Ad-hoc SQL is allowed only through `execute_read_query`, which must pass `QueryValidator` (SELECT-only ScriptDom parse with a fail-closed table-source allow-list).
- **Encrypted modules:** Definitions may be unavailable without `VIEW DEFINITION` permission.
- **Parameterized search:** `search_definitions` uses parameterized `LIKE`.

## Configuration and security

- **Credentials** in `Database.ConnectionString` — use masked `YOUR_*` values in committed `appsettings.json`; put real secrets only in `appsettings.local.json` in `AppContext.BaseDirectory` (see [.gitignore](../.gitignore)). A copy left only in `Mcp.Server/` is not loaded.
- **Cursor Directory** uses committed [`.mcp.json`](../.mcp.json) (`mssql-mcp-tools`, `npx -y mssql-mcp-tools`). The [launcher](../launcher/bin.js) downloads the pinned GitHub Release and reads `appsettings.local.json` from the user cache folder. It has no connection string. Local host config stays in gitignored `mcp.json`; copy from [mcp.example.json](../mcp.example.json) or the release examples.
- **Integration test secrets** stay in `Mcp.Test/.runsettings` (gitignored); use [`.runsettings.example`](../Mcp.Test/.runsettings.example) as the template.
- **Integration test database** is created by [integration-test-db.sql](../Mcp.Test/Script/integration-test-db.sql) (`mcp_test`).
- **Local publish profiles** under `Properties/PublishProfiles/` are gitignored (machine-specific paths).
- **Logging** via Serilog `WriteTo.File` path in appsettings (replace `YOUR_LOG_PATH/mssql-mcp-tools.log` in your local file). Single-file releases require `Serilog:Using` (`Serilog.Sinks.File`) and an explicit sink assembly in `Program.cs` (`ConfigurationReaderOptions`).
- **Query limits:** `QueryOptions.MaxRows` and `MaxCellLength` apply to tabular results. `CommandTimeoutSeconds` applies to every call, including `get_object_definition`.
- **Least privilege:** Use a SQL login with metadata read access; avoid `sa` in production.
- **Untrusted hosts:** Avoid pointing the server at production data when the MCP host is not under your control.

## Testing

| Test project area | Requires SQL Server |
|-------------------|---------------------|
| `McpMessageHandlerTests` | No (uses `TestDatabaseService`) |
| `DatabaseServiceTests` | No (mocks `ISqlExecutor`) |
| `DatabaseServiceIntegrationTests` | Yes (`[TestCategory("Integration")]`) |

### Integration test setup

1. Run [integration-test-db.sql](../Mcp.Test/Script/integration-test-db.sql) against a local SQL Server instance (creates database `mcp_test` and required objects).
2. Copy [`.runsettings.example`](../Mcp.Test/.runsettings.example) -> `Mcp.Test/.runsettings` and set `DbConnectionString` to that database.
3. Run tests with `--settings` (see below).

```powershell
# Unit tests only (same filter used in CI)
dotnet test --solution Mcp.sln --filter "TestCategory!=Integration"

# All tests including integration (needs mcp_test + local .runsettings)
dotnet test --solution Mcp.sln --settings Mcp.Test/.runsettings
```

CI workflows (`build.yml`, `release.yml`) always exclude `TestCategory=Integration`.

## Distribution (GitHub Releases)

End users download a pre-built archive for their OS from [GitHub Releases](https://github.com/darwijesinghe/mssql-mcp-tools/releases); they do not need the .NET SDK.

| Item | Detail |
|------|--------|
| Trigger | Push a tag matching `v*` (for example `v1.0.0`) |
| Workflow | `.github/workflows/release.yml` |
| CI steps | Unit tests (excludes `Integration`) -> matrix publish (self-contained single-file) -> archive -> collect artifacts -> single GitHub Release upload -> npm publish of `launcher/` (version and `RELEASE_TAG` taken from the tag). `generate_release_notes` builds the GitHub Release description from commits. [RELEASE_NOTES.md](../RELEASE_NOTES.md) is the in-repo history and is not that body. |
| Assets | `mssql-mcp-tools-win-x64.zip`, `mssql-mcp-tools-linux-x64.tar.gz`, `mssql-mcp-tools-osx-arm64.tar.gz`, `mssql-mcp-tools-osx-x64.tar.gz` (each contains the binary + `appsettings.json`) |
| Binary | `mssql-mcp-tools.exe` (Windows) or `mssql-mcp-tools` (Linux / macOS) |
| User config | Copy shipped `appsettings.json` -> `appsettings.local.json` beside the binary and replace `YOUR_*` placeholders; see [mcp.release.example.json](../mcp.release.example.json) (Windows) or [mcp.release.unix.example.json](../mcp.release.unix.example.json) (Linux / macOS) |

Maintainers create a release:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

Release binaries are not committed to git (`publish/` and `artifacts/` stay local or in CI only).

## Extension points

| Area | Change |
|------|--------|
| New catalog tool | Add method to `IDatabaseService` and `DatabaseService`; register in `HandleToolsList` and `HandleToolCallAsync`; update `TestDatabaseService` and tests |
| New MCP method | Extend `McpMessageHandler.HandleAsync` |
| New error code | Add to `ErrorCodes` |
| Query safety rules | Extend `QueryValidator` |
| SQL execution behavior | Extend `SqlExecutor` or `QueryOptions` |
| Transport | Replace `Startup.cs` loop; keep handler and service layers |
| Logging | Adjust Serilog section in appsettings or add sinks in `Program.cs` |

## Related documentation

- [Documentation index](index.md)
- [Source tree](SOURCE_TREE.md)
- [Security posture](SECURITY_POSTURE.md)
- [README](../README.md)
