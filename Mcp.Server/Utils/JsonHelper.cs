using Mcp.Server.Models;
using System.Text.Json;

namespace Mcp.Server.Utils;

/// <summary>
/// JSON serialization helpers for MCP message handling.
/// </summary>
public static class JsonHelper
{
    /// <summary>
    /// Converts a <see cref="JsonElement"/> request id to a CLR type suitable for <see cref="JsonRpcResponse.Id"/>.
    /// </summary>
    /// <param name="id">The raw JSON id element from the request.</param>
    /// <returns>
    /// An <see cref="int"/>, <see cref="string"/>, <see langword="null"/>, or raw JSON text for other kinds.
    /// </returns>
    public static object? ConvertId(this JsonElement id)
    {
        return id.ValueKind switch
        {
            JsonValueKind.Number => id.TryGetInt64(out var longId) ? longId : id.GetDouble(),
            JsonValueKind.String => id.GetString(),
            JsonValueKind.Null   => null,
            _                    => id.GetRawText()
        };
    }
}
