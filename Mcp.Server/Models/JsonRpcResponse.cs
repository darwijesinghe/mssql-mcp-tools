using System.Text.Json.Serialization;

namespace Mcp.Server.Models;

/// <summary>
/// Represents a JSON-RPC 2.0 response sent back to the MCP client over stdio.
/// </summary>
/// <remarks>
/// Success responses set <see cref="Result"/>; protocol failures set <see cref="Error"/>.
/// Tool execution failures use a successful JSON-RPC response whose <see cref="Result"/> is
/// an <see cref="McpCallToolResult"/> with 'isError: true'.
/// </remarks>
public sealed class JsonRpcResponse
{
    /// <summary>
    /// Gets or sets the JSON-RPC protocol version (always '2.0').
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc      { get; set; } = "2.0";

    /// <summary>
    /// Gets or sets the request identifier echoed from the corresponding request.
    /// </summary>
    [JsonPropertyName("id")]
    public object? Id          { get; set; }

    /// <summary>
    /// Gets or sets the successful result payload when the request succeeded.
    /// </summary>
    [JsonPropertyName("result")]
    public object? Result      { get; set; }

    /// <summary>
    /// Gets or sets the error object when the request failed with a protocol error.
    /// </summary>
    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; set; }
}
