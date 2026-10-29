# Release Notes

## [v1.0.0] - 2026-10-29

### Summary

Initial release of MssqlMcpTools, a read-only MCP server for exploring MSSQL Server over stdio.

### Added

- MCP server over stdio (JSON-RPC 2.0, protocol `2025-11-25`) for Cursor and other MCP hosts.
- Ten tools: nine catalog tools plus `execute_read_query` (SELECT-only, validated by `QueryValidator`).
- Fail-closed table-source checks: named tables and views, derived tables, and joins are allowed. TVFs, `OPENJSON`, and unrecognized sources are rejected.
- JSON-RPC `result` / `error` responses. `tools/call` returns MCP `content` / `isError` inside `result`.
- Query limits through `QueryOptions` (`MaxRows`, `MaxCellLength`, `CommandTimeoutSeconds`).
- Startup database check, capped at 15 seconds. File logging only, so stdout stays JSON-RPC.
- Configuration from masked `appsettings.json`, with secrets in gitignored `appsettings.local.json`.
- npm launcher `mssql-mcp-tools` (`npx -y mssql-mcp-tools`) for the Cursor Directory listing in `.mcp.json`.
- Self-contained release archives: `mssql-mcp-tools-win-x64.zip`, `mssql-mcp-tools-linux-x64.tar.gz`, `mssql-mcp-tools-osx-arm64.tar.gz`, and `mssql-mcp-tools-osx-x64.tar.gz`. Each archive contains `mssql-mcp-tools.exe` (Windows) or `mssql-mcp-tools` (Linux / macOS) plus `appsettings.json`.
- Example MCP configs: `mcp.example.json`, `mcp.release.example.json`, and `mcp.release.unix.example.json`.

### Notes

- End users do not need the .NET SDK. Copy `appsettings.json` to `appsettings.local.json` beside the binary and replace the `YOUR_*` placeholders before starting the server.
- Developers need the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later. Place `appsettings.local.json` in the build output directory (`Mcp.Server/bin/Debug/net9.0/` for `dotnet run`). `global.json` selects the Microsoft.Testing.Platform runner and does not pin an SDK. CI installs the .NET 10 SDK.
- Publish this release with `git tag v1.0.0` then `git push origin v1.0.0`. That runs `.github/workflows/release.yml`. The GitHub Release description is generated from commits. This file is the in-repo history and is not uploaded as that body.
