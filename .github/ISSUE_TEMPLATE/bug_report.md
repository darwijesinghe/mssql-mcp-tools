---
name: Bug report
about: Create a report to help us improve
title: "[BUG] Brief description of the problem"
labels: bug
assignees: ''

---

**Describe the bug**

A clear and concise description of what the bug is.

**To Reproduce**

Steps to reproduce the behavior:
1. Configure `appsettings.local.json` (connection string and log path)
2. Configure MCP host (e.g. Cursor) with `mcp.json` pointing at the server
3. Run or invoke the server (source: `dotnet run`, release: `mssql-mcp-tools` / `mssql-mcp-tools.exe`)
4. Call the affected MCP tool or method
5. See error

**Expected behavior**

A clear and concise description of what you expected to happen.

**Environment**

- OS: [e.g. Windows 11 x64 / Ubuntu 24.04 x64 / macOS 15 arm64]
- Install type: [source / GitHub Release archive]
- Release RID (if applicable): [win-x64 / linux-x64 / osx-arm64 / osx-x64]
- .NET version (if running from source): [e.g. 9.0.x]
- MCP host: [e.g. Cursor 0.x]
- SQL Server version: [e.g. SQL Server 2019]

**Logs**

If applicable, paste relevant lines from the Serilog file configured in `Serilog.WriteTo[].Args.path`. Redact connection strings and credentials.

**Additional context**

Add any other context about the problem here.
