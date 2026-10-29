# Mssql Mcp Tools

[![npm version](https://img.shields.io/npm/v/mssql-mcp-tools.svg)](https://www.npmjs.com/package/mssql-mcp-tools)

Launcher for [mssql-mcp-tools](https://github.com/darwijesinghe/mssql-mcp-tools), a read-only MCP server for MSSQL Server.

Requires Node.js 18 or later. No clone and no .NET SDK are required.

## Run from npm

```powershell
npx -y mssql-mcp-tools
```

Or install it and run the command:

```powershell
npm install -g mssql-mcp-tools
mssql-mcp-tools
```

That downloads the GitHub Release this package is built for (Windows x64, Linux x64, macOS arm64, or macOS x64). The first start stops and prints the install folder, under `~/.mssql-mcp-tools/` (`%USERPROFILE%\.mssql-mcp-tools\` on Windows). Copy `appsettings.json` there to `appsettings.local.json`, replace the `YOUR_*` connection string and log path, then start it again.

## Cursor and Claude

Add this server. If the file already has other servers, add only the `mssql-mcp-tools` entry inside `mcpServers`.

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

Restart the host and enable **mssql-mcp-tools**. Keep the SQL connection string in `appsettings.local.json` in the install folder. Do not put it in these MCP files.

Full documentation: https://github.com/darwijesinghe/mssql-mcp-tools#readme
