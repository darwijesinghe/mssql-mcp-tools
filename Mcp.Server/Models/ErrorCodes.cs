namespace Mcp.Server.Models;

/// <summary>
/// Standard JSON-RPC 2.0 error codes returned by <see cref="Services.McpMessageHandler"/>.
/// </summary>
public enum ErrorCodes
{
    /// <summary>
    /// The requested JSON-RPC method does not exist.
    /// </summary>
    MethodNotFound = -32601,

    /// <summary>
    /// Invalid method parameter(s), including missing tool names or required tool arguments.
    /// </summary>
    InvalidParams  = -32602,

    /// <summary>
    /// Internal JSON-RPC / server error (for example, an unexpected exception while handling a request).
    /// </summary>
    InternalError  = -32603
}
