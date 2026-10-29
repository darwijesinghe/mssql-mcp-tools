using Mcp.Server.Models;
using System.Text.Json;

namespace Mcp.Test.Helpers
{
    /// <summary>
    /// Provides helper methods and utilities for unit tests.
    /// </summary>
    internal static class McpTestHelper
    {
        /// <summary>
        /// Creates a <see cref="JsonRpcRequest"/> object with the specified <paramref name="id"/>, <paramref name="method"/>, and optional <paramref name="paramsJson"/>.
        /// </summary>
        /// <param name="id">The id of the JSON-RPC request.</param>
        /// <param name="method">The method name of the JSON-RPC request.</param>
        /// <param name="paramsJson">The optional JSON string representing the parameters of the JSON-RPC request.</param>
        /// <returns>
        /// A <see cref="JsonRpcRequest"/> object constructed from the provided parameters.
        /// </returns>
        public static JsonRpcRequest Request(int id, string method, string? paramsJson = null)
        {
            var json = paramsJson is null
                ? $$"""{"jsonrpc":"2.0","id":{{id}},"method":"{{method}}"}"""
                : $$"""{"jsonrpc":"2.0","id":{{id}},"method":"{{method}}","params":{{paramsJson}}}""";

            return JsonSerializer.Deserialize<JsonRpcRequest>(json)!;
        }

        /// <summary>
        /// Creates a <see cref="JsonRpcRequest"/> object for calling a tool with the specified <paramref name="id"/>, <paramref name="toolName"/>, and optional <paramref name="argumentsJson"/> in JSON format.
        /// </summary>
        /// <param name="id">The id of the JSON-RPC request.</param>
        /// <param name="toolName">The name of the tool to call (for example, 'describe_table').</param>
        /// <param name="argumentsJson">The optional JSON string representing the arguments to pass to the tool. If not provided, it defaults to an empty JSON object ("{}").</param>
        /// <returns>
        /// A <see cref="JsonRpcRequest"/> object constructed for calling the specified tool with the provided arguments.
        /// </returns>
        public static JsonRpcRequest ToolCall(int id, string toolName, string? argumentsJson = "{}")
            => Request(id, "tools/call", $$"""{"name":"{{toolName}}","arguments":{{argumentsJson}}}""");

        /// <summary>
        /// Extracts the MCP text content from a 'tools/call' JSON-RPC response.
        /// </summary>
        /// <param name="response">The <see cref="JsonRpcResponse"/> object from which to extract the tool text.</param>
        /// <returns>
        /// The text from 'result.content[0].text', or <see langword="null"/> when missing.
        /// </returns>
        public static string? GetToolText(JsonRpcResponse response)
        {
            if (response.Result is McpCallToolResult toolResult)
                return toolResult.Content.Count > 0 ? toolResult.Content[0].Text : null;

            using var doc = SerializeResult(response);
            if (doc is null)
                return null;

            if (!TryGetProperty(doc.RootElement, "content", out var content) ||
                content.ValueKind != JsonValueKind.Array ||
                content.GetArrayLength() == 0)
                return null;

            var first = content[0];
            return TryGetProperty(first, "text", out var textProp)
                ? textProp.GetString()
                : null;
        }

        /// <summary>
        /// Returns whether a 'tools/call' result is marked as a tool execution error.
        /// </summary>
        /// <param name="response">The JSON-RPC response.</param>
        /// <returns>
        /// <see langword="true"/> when 'result.isError' is <see langword="true"/>; otherwise <see langword="false"/>.
        /// </returns>
        public static bool IsToolError(JsonRpcResponse response)
        {
            if (response.Result is McpCallToolResult toolResult)
                return toolResult.IsError;

            using var doc = SerializeResult(response);
            if (doc is null)
                return false;

            return TryGetProperty(doc.RootElement, "isError", out var isError) && isError.GetBoolean();
        }

        /// <summary>
        /// Asserts that the actualId (which may be a <see langword="long"/>, <see langword="int"/>, or <see langword="null"/>) matches the 
        /// <paramref name="expectedId"/> (an <see langword="int"/>), by converting <paramref name="expectedId"/> to <see langword="long"/> for comparison.
        /// </summary>
        /// <param name="actualId">The actual id value from the <see cref="JsonRpcRequest"/>, which may be a <see langword="long"/>, <see langword="int"/>, or <see langword="null"/>.</param>
        /// <param name="expectedId">The expected id value as an <see langword="int"/>, which will be converted to <see langword="long"/> for comparison.</param>
        public static void AssertId(object? actualId, int expectedId) => Assert.AreEqual((double)expectedId, actualId);

        /// <summary>
        /// Asserts that the given <see cref="JsonRpcResponse"/> contains a protocol error with the specified code and a 
        /// message that contains the specified substring.
        /// </summary>
        /// <param name="response">The <see cref="JsonRpcResponse"/> object to check for the expected error code and message content.</param>
        /// <param name="code">The expected JSON-RPC error code.</param>
        /// <param name="messageContains">The substring that should be contained in the error message.</param>
        public static void AssertError(JsonRpcResponse response, ErrorCodes code, string messageContains)
        {
            Assert.IsNotNull(response.Error);
            Assert.AreEqual((int)code, response.Error.Code);
            StringAssert.Contains(response.Error.Message, messageContains);
        }

        /// <summary>
        /// Gets the value of a specified column from a row represented as a dictionary. If the column does not exist, returns <see langword="null"/>.
        /// </summary>
        /// <param name="row">The row represented as a dictionary.</param>
        /// <param name="columnName">The name of the column to retrieve the value for.</param>
        /// <returns>
        /// The value of the specified column, or <see langword="null"/> if the column does not exist.
        /// </returns>
        public static object? GetValue(this Dictionary<string, object?> row, string columnName)
        {
            return row.TryGetValue(columnName, out var value)
                ? value
                : null;
        }

        /// <summary>
        /// Serializes <see cref="JsonRpcResponse.Result"/> to a disposable <see cref="JsonDocument"/>.
        /// </summary>
        /// <param name="response">The JSON-RPC response whose <see cref="JsonRpcResponse.Result"/> is serialized.</param>
        /// <returns>
        /// A <see cref="JsonDocument"/> for 'result', or <see langword="null"/> when 'result' is missing.
        /// </returns>
        private static JsonDocument? SerializeResult(JsonRpcResponse response)
        {
            if (response.Result is null)
                return null;

            return JsonDocument.Parse(JsonSerializer.Serialize(response.Result));
        }

        /// <summary>
        /// Gets a JSON property by <paramref name="name"/>, ignoring case.
        /// </summary>
        /// <param name="element">The JSON object element to search.</param>
        /// <param name="name">The property name to find (case-insensitive).</param>
        /// <param name="value">
        /// When this method returns <see langword="true"/>, receives the matching property value;
        /// otherwise <see cref="JsonElement"/> default.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when a matching property exists; otherwise <see langword="false"/>.
        /// </returns>
        private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        value = prop.Value;
                        return true;
                    }
                }
            }

            value = default;
            return false;
        }
    }
}
