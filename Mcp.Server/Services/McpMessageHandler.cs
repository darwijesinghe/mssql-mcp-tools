using Mcp.Server.Models;
using Mcp.Server.Services.Interfaces;
using Mcp.Server.Utils;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mcp.Server.Services;

/// <summary>
/// Dispatches incoming JSON-RPC MCP requests to protocol handlers and database tools.
/// </summary>
/// <remarks>
/// <para>
/// Protocol methods: 'initialize', 'ping', 'tools/list', 'tools/call'.
/// Notifications ('notifications/*') are ignored.
/// </para>
/// <para>Tools delegate to <see cref="IDatabaseService"/>:</para>
/// <list type="bullet">
/// <item><description>list_tables           - user base tables (<see cref="IDatabaseService.GetTablesAsync"/>)</description></item>
/// <item><description>list_views            - views (<see cref="IDatabaseService.GetViewsAsync"/>)</description></item>
/// <item><description>list_procedures       - stored procedures (<see cref="IDatabaseService.GetProceduresAsync"/>)</description></item>
/// <item><description>list_triggers         - DML and DDL triggers (<see cref="IDatabaseService.GetTriggersAsync"/>)</description></item>
/// <item><description>list_functions        - user functions (<see cref="IDatabaseService.GetFunctionsAsync"/>)</description></item>
/// <item><description>describe_table        - column metadata (<see cref="IDatabaseService.DescribeTableAsync"/>)</description></item>
/// <item><description>get_object_definition - T-SQL source (<see cref="IDatabaseService.GetObjectDefinitionAsync"/>)</description></item>
/// <item><description>search_definitions    - find objects by text in module body (<see cref="IDatabaseService.SearchObjectDefinitionsAsync"/>)</description></item>
/// <item><description>find_references       - objects that reference a table or view (<see cref="IDatabaseService.GetObjectReferencesAsync"/>)</description></item>
/// <item><description>execute_read_query    - execute read-only SQL (<see cref="IDatabaseService.ExecuteReadQueryAsync"/>)</description></item>
/// </list>
/// </remarks>
public sealed class McpMessageHandler
{
    /// <summary>
    /// The JSON serializer options.
    /// </summary>
    private static readonly JsonSerializerOptions ToolResultJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented          = false
    };

    /// <summary>
    /// Database service reference.
    /// </summary>
    private readonly IDatabaseService           _database;

    /// <summary>
    /// Logger service reference.
    /// </summary>
    private readonly ILogger<McpMessageHandler> _logger;

    /// <summary>
    /// Initializes the handler with dependencies to run MCP tools.
    /// </summary>
    /// <param name="database">The database service used to execute MCP tool queries.</param>
    /// <param name="logger">The logger used to record diagnostic information.</param>
    public McpMessageHandler(IDatabaseService database, ILogger<McpMessageHandler> logger)
    {
        _database = database;
        _logger   = logger;
    }

    /// <summary>
    /// Handles a single JSON-RPC request and returns a response, or <see langword="null"/> for notifications.
    /// </summary>
    /// <param name="request">The deserialized JSON-RPC request.</param>
    /// <returns>
    /// A JSON-RPC response, or <see langword="null"/> when the request is a notification.
    /// </returns>
    public async Task<JsonRpcResponse?> HandleAsync(JsonRpcRequest request)
    {
        if (request.Method.StartsWith("notifications/", StringComparison.Ordinal))
        {
            _logger.LogInformation($"Ignoring notification: {request.Method}");
            return null;
        }

        return request.Method switch
        {
            "initialize" => HandleInitialize(request),          // id: 1
            "ping"       => HandlePing(request),                // id: 2
            "tools/list" => HandleToolsList(request),           // id: 3
            "tools/call" => await HandleToolCallAsync(request), // id: 4+

            _            => ProtocolError(JsonHelper.ConvertId(request.Id), ErrorCodes.MethodNotFound, $"Method '{request.Method}' not found")
        };
    }

    // Helpers ----------------------------------------------

    /// <summary>
    /// Responds to the MCP 'ping' method with an empty result.
    /// </summary>
    /// <param name="request">The incoming request.</param>
    /// <returns>
    /// A JSON-RPC success response with an empty 'result' object.
    /// </returns>
    private static JsonRpcResponse HandlePing(JsonRpcRequest request)
    {
        return new JsonRpcResponse
        {
            Id     = JsonHelper.ConvertId(request.Id),
            Result = new { }
        };
    }

    /// <summary>
    /// Responds to the MCP 'initialize' handshake with protocol version, capabilities, and server info.
    /// </summary>
    /// <param name="request">The incoming request.</param>
    /// <returns>
    /// A JSON-RPC success response whose 'result' contains 'protocolVersion', 'capabilities', and 'serverInfo'.
    /// </returns>
    private static JsonRpcResponse HandleInitialize(JsonRpcRequest request)
    {
        return new JsonRpcResponse
        {
            Id     = JsonHelper.ConvertId(request.Id),
            Result = new
            {
                protocolVersion = "2025-11-25",
                capabilities    = new
                {
                    tools = new { }
                },
                serverInfo = new
                {
                    name    = "mssql-mcp-tools",
                    version = typeof(McpMessageHandler).Assembly.GetName().Version?.ToString() ?? "unknown"
                }
            }
        };
    }

    /// <summary>
    /// Handles 'tools/list' and returns metadata for every MCP tool this server exposes.
    /// </summary>
    /// <param name="request">The incoming request.</param>
    /// <returns>
    /// A JSON-RPC success response whose 'result.tools' array describes each tool name, description, and input schema.
    /// </returns>
    private static JsonRpcResponse HandleToolsList(JsonRpcRequest request)
    {
        return new JsonRpcResponse
        {
            Id     = JsonHelper.ConvertId(request.Id),
            Result = new
            {
                tools = new object[]
                {
                    new
                    {
                        name        = "list_tables",
                        description = "Lists user base tables as SCHEMANAME and TABLENAME (excludes system and temporal history tables).",
                        inputSchema = new
                        {
                            type       = "object",
                            properties = new { }
                        }
                    },
                    new
                    {
                        name        = "describe_table",
                        description = "Returns column name, data type, nullability, and max length for a table (schema.table or table name).",
                        inputSchema = new
                        {
                            type       = "object",
                            properties = new
                            {
                                tableName = new
                                {
                                    type = "string"
                                }
                            },
                            required = new[] { "tableName" }
                        }
                    },
                    new
                    {
                        name        = "list_views",
                        description = "Lists views as SCHEMANAME and VIEWNAME.",
                        inputSchema = new
                        {
                            type       = "object",
                            properties = new { }
                        }
                    },
                    new
                    {
                        name        = "list_procedures",
                        description = "Lists user stored procedures as SCHEMANAME and PROCEDURENAME (excludes system and numbered variants).",
                        inputSchema = new
                        {
                            type       = "object",
                            properties = new { }
                        }
                    },
                    new
                    {
                        name        = "list_triggers",
                        description = "Lists triggers as PARENTOBJECT, TRIGGERNAME, and ISDISABLED. Database-level parent is (DATABASE).",
                        inputSchema = new
                        {
                            type       = "object",
                            properties = new { }
                        }
                    },
                    new
                    {
                        name        = "list_functions",
                        description = "Lists user functions as SCHEMANAME and FUNCTIONNAME (excludes system functions).",
                        inputSchema = new
                        {
                            type       = "object",
                            properties = new { }
                        }
                    },
                    new
                    {
                        name        = "get_object_definition",
                        description = "Get SQL definition for procedure, view, trigger or function.",
                        inputSchema = new
                        {
                            type       = "object",
                            properties = new
                            {
                                objectName = new
                                {
                                    type = "string"
                                }
                            },
                            required = new[]
                            {
                                "objectName"
                            }
                        }
                    },
                    new
                    {
                        name        = "search_definitions",
                        description = "Find procedures, views, functions, and triggers whose definition contains the given text.",
                        inputSchema = new
                        {
                            type       = "object",
                            properties = new
                            {
                                text = new
                                {
                                    type = "string"
                                }
                            },
                            required = new[]
                            {
                                "text"
                            }
                        }
                    },
                    new
                    {
                        name        = "find_references",
                        description = "List procedures, views, and functions that reference a table or view by name.",
                        inputSchema = new
                        {
                            type       = "object",
                            properties = new
                            {
                                objectName = new
                                {
                                    type = "string"
                                }
                            },
                            required = new[]
                            {
                                "objectName"
                            }
                        }
                    },
                    new
                    {
                        name        = "execute_read_query",
                        description = "Execute read-only SQL queries.",
                        inputSchema = new
                        {
                            type       = "object",
                            properties = new
                            {
                                sql = new
                                {
                                    type = "string"
                                }
                            },
                            required = new[]
                            {
                                "sql"
                            }
                        }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Handles 'tools/call' by running the tool named in 'params.name' and returning MCP text content.
    /// </summary>
    /// <param name="request">The incoming request. Tool arguments are in 'params.arguments' (for example, 'tableName' for 'describe_table').</param>
    /// <returns>
    /// A JSON-RPC success response with MCP 'content' blocks, a tool result with 'isError: true' for
    /// execution failures, or <see cref="ErrorCodes.InvalidParams"/> when the tool or required arguments are missing.
    /// </returns>
    private async Task<JsonRpcResponse> HandleToolCallAsync(JsonRpcRequest request)
    {
        // Echo the normalized request id on every success or error reply
        var id = JsonHelper.ConvertId(request.Id);

        // tools/call requires 'params.name'; reject early with 'InvalidParams' when missing or not a string
        if (request.Params.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null 
            || !request.Params.TryGetProperty("name", out var nameProp) 
            || nameProp.ValueKind != JsonValueKind.String)
        {
            return ProtocolError(id, ErrorCodes.InvalidParams, "Missing tool name.");
        }

        // Resolved tool name from 'params.name' (for example, 'list_tables' or 'execute_read_query')
        var tool = nameProp.GetString()!;

        // Optional 'params.arguments' object; catalog tools may omit it, argument-bearing tools require properties inside it
        JsonElement args = default;
        var hasArgs      = request.Params.TryGetProperty("arguments", out args) && args.ValueKind == JsonValueKind.Object;

        // Cancellation token for database calls; disposed when the method returns
        using var cts = new CancellationTokenSource();

        try
        {
            switch (tool)
            {
                case "list_tables":
                    return ToolSuccess(id, await _database.GetTablesAsync(cts.Token));

                case "list_views":
                    return ToolSuccess(id, await _database.GetViewsAsync(cts.Token));

                case "list_procedures":
                    return ToolSuccess(id, await _database.GetProceduresAsync(cts.Token));

                case "list_triggers":
                    return ToolSuccess(id, await _database.GetTriggersAsync(cts.Token));

                case "list_functions":
                    return ToolSuccess(id, await _database.GetFunctionsAsync(cts.Token));

                case "describe_table":
                    {
                        if (!hasArgs || !args.TryGetProperty("tableName", out var tableProp))
                            return ProtocolError(id, ErrorCodes.InvalidParams, "Missing required argument 'tableName'.");

                        var tableName = tableProp.GetString();
                        if (string.IsNullOrWhiteSpace(tableName))
                            return ProtocolError(id, ErrorCodes.InvalidParams, "Missing required argument 'tableName'.");

                        return ToolSuccess(id, await _database.DescribeTableAsync(tableName, cts.Token));
                    }

                case "get_object_definition":
                    {
                        if (!hasArgs || !args.TryGetProperty("objectName", out var objectProp))
                            return ProtocolError(id, ErrorCodes.InvalidParams, "Missing required argument 'objectName'.");

                        var objectName = objectProp.GetString();
                        if (string.IsNullOrWhiteSpace(objectName))
                            return ProtocolError(id, ErrorCodes.InvalidParams, "Missing required argument 'objectName'.");

                        return ToolSuccess(id, await _database.GetObjectDefinitionAsync(objectName, cts.Token));
                    }

                case "search_definitions":
                    {
                        if (!hasArgs || !args.TryGetProperty("text", out var textProp))
                            return ProtocolError(id, ErrorCodes.InvalidParams, "Missing required argument 'text'.");

                        var text = textProp.GetString();
                        if (string.IsNullOrWhiteSpace(text))
                            return ProtocolError(id, ErrorCodes.InvalidParams, "Missing required argument 'text'.");

                        return ToolSuccess(id, await _database.SearchObjectDefinitionsAsync(text, cts.Token));
                    }

                case "find_references":
                    {
                        if (!hasArgs || !args.TryGetProperty("objectName", out var objectProp))
                            return ProtocolError(id, ErrorCodes.InvalidParams, "Missing required argument 'objectName'.");

                        var objectName = objectProp.GetString();
                        if (string.IsNullOrWhiteSpace(objectName))
                            return ProtocolError(id, ErrorCodes.InvalidParams, "Missing required argument 'objectName'.");

                        return ToolSuccess(id, await _database.GetObjectReferencesAsync(objectName, cts.Token));
                    }

                case "execute_read_query":
                    {
                        if (!hasArgs || !args.TryGetProperty("sql", out var sqlProp))
                            return ProtocolError(id, ErrorCodes.InvalidParams, "Missing required argument 'sql'.");

                        var sql = sqlProp.GetString();
                        if (string.IsNullOrWhiteSpace(sql))
                            return ProtocolError(id, ErrorCodes.InvalidParams, "Missing required argument 'sql'.");

                        return ToolSuccess(id, await _database.ExecuteReadQueryAsync(sql, cts.Token));
                    }

                default:
                    return ProtocolError(id, ErrorCodes.InvalidParams, $"Unknown tool '{tool}'.");
            }
        }
        catch (Exception ex)
        {
            // Tool execution errors must reach the model via 'isError' (not only the log file)
            _logger.LogError(ex, "Tool '{Tool}' failed.", tool);
            return ToolFailure(id, ex.Message);
        }
    }

    /// <summary>
    /// Builds a successful tools/call result with a single MCP text content block.
    /// </summary>
    /// <param name="id">The request identifier to echo.</param>
    /// <param name="result">The <see cref="QueryResult"/> containing the tool output.</param>
    /// <returns>
    /// A JSON-RPC success response with <see cref="McpCallToolResult"/> in 'result'.
    /// </returns>
    private static JsonRpcResponse ToolSuccess(object? id, QueryResult result)
    {
        return new JsonRpcResponse
        {
            Id     = id,
            Result = McpCallToolResult.Ok(FormatToolText(result))
        };
    }

    /// <summary>
    /// Builds a tools/call failure that still uses JSON-RPC success with 'isError: true'.
    /// </summary>
    /// <param name="id">The request identifier to echo.</param>
    /// <param name="message">The error message for the host / model.</param>
    /// <returns>
    /// A JSON-RPC success response whose 'result.isError' is true.
    /// </returns>
    private static JsonRpcResponse ToolFailure(object? id, string message)
    {
        return new JsonRpcResponse
        {
            Id     = id,
            Result = McpCallToolResult.Error(message)
        };
    }

    /// <summary>
    /// Builds a JSON-RPC protocol error response.
    /// </summary>
    /// <param name="id">The request identifier to echo.</param>
    /// <param name="code">The JSON-RPC error code.</param>
    /// <param name="message">A human-readable error description.</param>
    /// <returns>
    /// A JSON-RPC error response.
    /// </returns>
    private static JsonRpcResponse ProtocolError(object? id, ErrorCodes code, string message)
    {
        return new JsonRpcResponse
        {
            Id    = id,
            Error = new JsonRpcError
            {
                Code    = (int)code,
                Message = message
            }
        };
    }

    /// <summary>
    /// Formats a <see cref="QueryResult"/> as text for MCP content blocks.
    /// </summary>
    /// <param name="result">The query result to format.</param>
    /// <returns>
    /// Plain <see cref="QueryResult.Text"/> when that is the only payload; otherwise JSON for rows/columns.
    /// </returns>
    private static string FormatToolText(QueryResult result)
    {
        // Definition-style tools and test doubles often set Text only
        if (!string.IsNullOrEmpty(result.Text) && (result.Rows is null || result.Rows.Count == 0))
            return result.Text;

        return JsonSerializer.Serialize(result, ToolResultJsonOptions);
    }
}
