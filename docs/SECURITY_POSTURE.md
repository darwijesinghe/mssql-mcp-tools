# Security posture

How MssqlMcpTools limits risk when an AI host explores a MSSQL database over stdio. This is operational guidance, not a formal threat model or compliance claim. Report vulnerabilities per [SECURITY.md](../SECURITY.md).

## Threat model in one line

The MCP **host** (Cursor, Claude Desktop, etc.) is trusted to spawn this process and send tool calls. The server hardens **what SQL can run** and **how much data returns**. It does **not** decide whether the human or model *should* see that data.

```
Untrusted content / user prompts
        |
        v
   MCP host (agent)  --stdio-->  MssqlMcpTools  -->  MSSQL (login permissions)
        |                              |
        |                              +-- QueryValidator (SELECT-only, table-source allow-list)
        |                              +-- QueryOptions (rows / cells / timeout)
        v
   Tool results in the chat / context
```

## What the server protects against

| Risk | How it is mitigated |
|------|---------------------|
| **Writes / DDL / DML** | Catalog tools use fixed SQL in `DatabaseService`. Ad-hoc SQL only via `execute_read_query`, which must pass `QueryValidator` (ScriptDom `TSql180Parser`): top-level statements must be **SELECT**, and `SELECT INTO` is rejected. |
| **Bulk export shapes** | `FOR XML` / `FOR JSON` rejected (`QuerySpecification.ForClause`). |
| **Unsafe / non-table FROM sources (fail-closed)** | `QueryValidator` allow-lists table sources: named tables/views, derived tables, and joins (including parenthesized joins). Explicitly rejected: table variables, `OPENROWSET`, `OPENQUERY`, `OPENXML`, `OPENJSON`, table-valued functions (including via `CROSS APPLY` / `OUTER APPLY`), and **any other** ScriptDom `TableReference` type (`STRING_SPLIT`, etc.). |
| **Classic SQL injection in catalog tools** | Argument-taking catalog tools bind values with `SqlParameter`: `describe_table`, `get_object_definition`, `search_definitions` (`LIKE '%' + @text + '%'`), `find_references`. List tools take no user SQL. |
| **Oversized tabular results** | `QueryOptions.MaxRows` stops further row reads and sets `Truncated`; `MaxCellLength` truncates **string** cells; `CommandTimeoutSeconds` sets `SqlCommand.CommandTimeout`. |
| **Hung startup** | `Startup` bounds connection validation with a **15-second** `CancellationTokenSource` before the stdio loop. |
| **Accidental secret commits** | Committed `appsettings.json` uses `YOUR_*` placeholders. Committed `.mcp.json` is the Cursor Directory listing and has no credentials. Real secrets belong in gitignored `appsettings.local.json` / `.runsettings` / `mcp.json`. |
| **Stdout pollution** | Logging is file-oriented (Serilog); console providers are cleared so MCP JSON-RPC on stdout is not mixed with log lines. Transport is **stdio only** — no HTTP listener. |

These controls assume a correctly configured SQL login and a host you control. They are defense in depth, not a substitute for database permissions.

## What it does not protect against

| Gap | Why it matters |
|-----|----------------|
| **Authorized reads of sensitive data** | Any table/view the SQL login can `SELECT` is reachable via `execute_read_query` or visible via catalog/definition tools. |
| **Prompt injection / agent misuse** | The model or host can call tools with SQL or object names chosen by untrusted text in the conversation. The server executes valid SELECT requests; it does not judge intent. |
| **Compromised or untrusted MCP host** | Whoever controls stdin effectively controls the SQL login. Do not point an untrusted host at production. |
| **Exfiltration through the host** | Results return in JSON-RPC and may appear in chat, other tools, or files the agent controls. |
| **Multiple SELECT statements** | A single `execute_read_query` payload may contain more than one SELECT batch; each must still be SELECT-shaped. |
| **`execute_read_query` is not parameterized** | The validated SQL string is executed as submitted. That is intentional for ad-hoc SELECT; injection here means “the agent chose the whole query,” not classic string concat into a fixed statement. |
| **Large object definitions** | `get_object_definition` uses `ExecuteScalarStringAsync`, which does **not** apply `MaxRows` / `MaxCellLength`. Module text can be large. |
| **Scalar functions / views with side effects** | TVFs in `FROM`/`APPLY` are blocked, but a SELECT may still call **scalar** UDFs in the select list or read a **view** that performs work under elevated rights. Prefer denying execute/impersonation paths in SQL. |
| **Parser / ScriptDom coverage gaps** | Validation depends on ScriptDom classifying constructs correctly. Novel or mis-parsed syntax is fail-closed when it surfaces as an unknown table source, but unusual SELECT shapes that still parse as allowed forms can slip through. Server-side permissions remain the backstop. |
| **Denial of service against SQL** | Timeouts and row caps reduce impact; expensive SELECT plans are still possible within those bounds. Caps apply **per call**, not across a session. |
| **Credential theft from disk** | Connection strings live in config under `AppContext.BaseDirectory` (and optionally bootstrap logs beside the exe). Anyone who can read that directory can read secrets. |
| **Error and log leakage** | Unhandled tool/loop exceptions may put `ex.Message` into JSON-RPC errors and Serilog/file logs. Do not assume query text or SQL error detail never leaves the process. |

## SQL permissions

Grant the MCP login **least privilege**. The server does not elevate or impersonate.

**Recommended baseline (read-only exploration):**

- `CONNECT` to the target database.
- `SELECT` only on schemas/tables/views the agent is allowed to see (prefer a dedicated schema or role, not `db_datareader` on everything unless that is intentional).
- `VIEW DEFINITION` only if `get_object_definition` / `search_definitions` must return module text (encrypted modules still need appropriate rights).
- Metadata catalog access sufficient for the fixed `sys.*` / `INFORMATION_SCHEMA` queries used by list/describe/search tools.

**Avoid:**

- `sa`, `sysadmin`, `db_owner`, or any write role (`INSERT`/`UPDATE`/`DELETE`/`ALTER`/`EXECUTE` on arbitrary procedures) for the MCP login.
- Cross-database ownership chaining or linked-server rights that widen what a SELECT can reach.
- Credentials shared with write-capable apps.

If a SELECT still succeeds, the login was allowed to read that object — treat that as expected behavior.

## Prompt injection considerations

MCP tools are callable by the agent. Text from emails, tickets, wiki pages, or DB content can instruct the model to run `execute_read_query` or dump definitions.

**Implications:**

- Treat **tool results as confidential** to the same degree as the database itself.
- Do not rely on system prompts alone (“never query PII”) as a control — they are bypassable.
- Prefer **narrow SQL permissions** and non-production databases when experimenting with agents that ingest untrusted content.
- Review which host features auto-approve tool calls; require confirmation for `execute_read_query` when available.

The validator stops many *mutating* payloads; it does not stop *authorized read* payloads suggested by injected instructions.

## Data exfiltration

Exfiltration paths in this architecture are mostly **through the host**, not through a separate channel on the server:

1. **Tool results** — rows, definitions, and catalog listings return in JSON-RPC `result.content` and enter the model context.
2. **Repeated queries** — row caps apply per call; an agent can issue many calls.
3. **Definitions and search** — `get_object_definition` / `search_definitions` can expose business logic and embedded secrets in module text (definitions are not cell-truncated).
4. **Host-side forwarding** — the agent may paste results into other MCP servers, browsers, or files.

**Mitigations that actually help:** least-privilege SQL, non-prod targets, tight `MaxRows` / `MaxCellLength`, host tool-approval policies, and not embedding secrets in stored procedures.

## Credential handling

| Practice | Detail |
|----------|--------|
| **Local secrets** | Put real connection strings in `appsettings.local.json` (gitignored). Keep `appsettings.json` masked. Config is loaded from `AppContext.BaseDirectory`. |
| **Tests** | Integration secrets in `Mcp.Test/.runsettings` (gitignored); use `.runsettings.example` as the template. |
| **MCP config** | Do not commit real `mcp.json` (`**/mcp.json` is gitignored). Use the `*.example` templates. Committed `.mcp.json` is the public Cursor Directory config (`npx -y mssql-mcp-tools`) and must not contain a connection string or a machine-specific path. The launcher keeps `appsettings.local.json` in the user profile cache (`.mssql-mcp-tools/`), not in the repository. |
| **Publish profiles** | Machine-specific profiles under `Properties/PublishProfiles/` are gitignored. |
| **In process** | `SqlExecutor` / `DatabaseService` read `Database.ConnectionString` from configuration; there is no secret vault integration. |
| **Bootstrap log** | `Program` writes an early file logger to `mssql-mcp-tools-bootstrap.log` under the app base directory before full Serilog config loads. |
| **Rotation** | Rotate the SQL password if the install directory, logs, or backups may have leaked config. |
| **Release zip** | Shipped `appsettings.json` is a template — copy to `appsettings.local.json` beside the exe before use. |

Prefer Windows/Azure AD auth in the connection string when your environment supports it, so passwords are not stored in plaintext JSON.

## Query limits

Configured under `QueryOptions` (defaults from `appsettings.json`):

| Option | Default | Effect |
|--------|---------|--------|
| `MaxRows` | `500` | In `ExecuteMultiColumnRowsAsync`, stops reading further rows; sets `Truncated` on `QueryResult`. |
| `MaxCellLength` | `5000` | Truncates **string** cell values with `...(truncated)`. Non-string types are unchanged. |
| `CommandTimeoutSeconds` | `30` | Applied to `SqlCommand` in both multi-row and scalar executors. |

**Where limits apply:** tabular paths through `SqlExecutor.ExecuteMultiColumnRowsAsync` — including `execute_read_query` and catalog list/describe/search/reference tools.

**Where they do not:** `get_object_definition` returns full scalar text via `ExecuteScalarStringAsync` (timeout only).

Limits bound **volume per response**, not total data an agent can retrieve over many calls. Lower them for higher-sensitivity databases.

## Production recommendations

1. **Dedicated read-only SQL login** with the smallest schema footprint that still supports your use case; never `sa`.
2. **Prefer a replica, scrubbed, or non-production database** for agent-assisted exploration of real schemas.
3. **Run only under an MCP host you control**, with tool approval enabled for ad-hoc SQL when the host supports it.
4. **Keep secrets out of git** — local appsettings / `.runsettings` / `mcp.json` only. Committed `.mcp.json` only runs `npx -y mssql-mcp-tools`. The SQL connection string belongs in `appsettings.local.json` inside the user cache folder. Rotate on suspected exposure.
5. **Tune `QueryOptions`** downward for sensitive environments; raise only when you accept larger context dumps.
6. **Restrict filesystem ACLs** on the install directory so other local users cannot read `appsettings.local.json` or log files.
7. **Review Serilog path and retention** (and `mssql-mcp-tools-bootstrap.log`) — ensure log folders are not world-readable.
8. **Do not expose the process over the network** — stdio is local; wrapping it in an open remote shell defeats the trust model.
9. **Monitor SQL** — audit the MCP login’s sessions and top queries if connecting near production data.
10. **Report issues privately** via [SECURITY.md](../SECURITY.md).

## Related documentation

- [Project overview — Configuration and security](PROJECT_OVERVIEW.md#configuration-and-security)
- [Documentation index](index.md)
- [SECURITY.md](../SECURITY.md) — vulnerability reporting
- [CONTRIBUTING.md](../CONTRIBUTING.md) — contributor security expectations
