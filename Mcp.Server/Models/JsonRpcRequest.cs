using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mcp.Server.Models;

/// <summary>
/// Represents an incoming JSON-RPC 2.0 request deserialized from the MCP stdio transport.
/// </summary>
public sealed class JsonRpcRequest
{
    /// <summary>
    /// Gets or sets the JSON-RPC protocol version (expected value: '2.0').
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc     { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the request identifier used to correlate responses; may be a number, string, or null.
    /// </summary>
    [JsonPropertyName("id")]
    public JsonElement Id     { get; set; }

    /// <summary>
    /// Gets or sets the method name to invoke (for example, 'initialize' or 'tools/call').
    /// </summary>
    [JsonPropertyName("method")]
    public string Method      { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the method parameters as a JSON value; shape depends on <see cref="Method"/>.
    /// </summary>
    [JsonPropertyName("params")]
    public JsonElement Params { get; set; }
}
