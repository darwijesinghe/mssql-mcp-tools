using System.Text.Json.Serialization;

namespace Mcp.Server.Models;

/// <summary>
/// Represents a JSON-RPC 2.0 error object.
/// </summary>
public sealed class JsonRpcError
{
    /// <summary>
    /// Gets or sets the numeric error code.
    /// </summary>
    [JsonPropertyName("code")]
    public int Code       { get; set; }

    /// <summary>
    /// Gets or sets a short human-readable error message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional additional error data.
    /// </summary>
    [JsonPropertyName("data")]
    public object? Data   { get; set; }
}
