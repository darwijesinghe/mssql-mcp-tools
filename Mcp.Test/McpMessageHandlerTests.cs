using Mcp.Server.Models;
using Mcp.Server.Services;
using Mcp.Test.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mcp.Test
{
    /// <summary>
    /// Unit tests for the <see cref="McpMessageHandler"/> class.
    /// </summary>
    [TestClass]
    public sealed class McpMessageHandlerTests
    {
        
        /// <summary>
        /// Test database service reference.
        /// </summary>
        private TestDatabaseService _test;

        /// <summary>
        /// MCP message handler reference.
        /// </summary>
        private McpMessageHandler   _handler;

        [TestInitialize]
        public void Initialize()
        {
            _test    = new TestDatabaseService();
            _handler = new McpMessageHandler(_test, NullLogger<McpMessageHandler>.Instance);
        }

        /// <summary>
        /// Tests that the <see cref="McpMessageHandler.HandleAsync"/> method returns <see langword="null"/> when a notification message is processed.
        /// </summary>
        [TestMethod]
        public async Task Test_HandleAsync_Notification_ReturnsNull()
        {
            // Arrange
            var request = McpTestHelper.Request(id: 1, method: "notifications/initialized");

            // Act
            var response = await _handler.HandleAsync(request);

            // Assert
            Assert.IsNull(response);
        }

        /// <summary>
        /// Tests that the <see cref="McpMessageHandler.HandleAsync"/> method returns a valid response when an 
        /// initialize request is processed.
        /// </summary>
        [TestMethod]
        public async Task Test_HandleAsync_Initialize_ReturnsServiceInfo()
        {
            // Arrange
            var request = McpTestHelper.Request(id: 1, method: "initialize");

            // Act
            var response = await _handler.HandleAsync(request);

            // Assert
            Assert.IsNotNull(response);
            Assert.IsNull(response.Error);
            Assert.IsNotNull(response.Result);
            McpTestHelper.AssertId(actualId: response.Id, expectedId: 1);

            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(response.Result));
            Assert.AreEqual("mssql-mcp-tools", doc.RootElement.GetProperty("serverInfo").GetProperty("name").GetString());
            Assert.AreEqual("1.0.0.0", doc.RootElement.GetProperty("serverInfo").GetProperty("version").GetString());
            Assert.AreEqual("2025-11-25", doc.RootElement.GetProperty("protocolVersion").GetString());
        }

        /// <summary>
        /// Tests that the <see cref="McpMessageHandler.HandleAsync"/> method returns the protocol version on 
        /// the result without wrapping in an MCP content array.
        /// </summary>
        [TestMethod]
        public async Task Test_HandleAsync_Initialize_WireJson_HasProtocolVersionOnResult()
        {
            // Arrange
            var request           = McpTestHelper.Request(id: 1, method: "initialize");
            var serializerOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            // Act
            var response = await _handler.HandleAsync(request);
            var wireJson = JsonSerializer.Serialize(response, serializerOptions);

            // Assert — full JSON-RPC line as Startup would write to stdout
            using var doc = JsonDocument.Parse(wireJson);
            Assert.IsTrue(doc.RootElement.TryGetProperty("result", out var result));
            Assert.IsFalse(doc.RootElement.TryGetProperty("error", out _));

            // Hosts read 'protocolVersion' from result directly
            Assert.AreEqual("2025-11-25", result.GetProperty("protocolVersion").GetString());
            Assert.IsFalse(
                result.TryGetProperty("content", out _),
                "initialize result must not wrap the handshake in an MCP content array.");
        }

        /// <summary>
        /// Tests that the <see cref="McpMessageHandler.HandleAsync"/> method returns an empty result when a 
        /// ping request is processed.
        /// </summary>
        [TestMethod]
        public async Task Test_HandleAsync_Ping_ReturnsEmptyResult()
        {
            // Arrange
            var request = McpTestHelper.Request(id: 2, method: "ping");

            // Act
            var response = await _handler.HandleAsync(request);

            // Assert
            Assert.IsNotNull(response);
            Assert.IsNull(response.Error);
            Assert.IsNotNull(response.Result);
            McpTestHelper.AssertId(actualId: response.Id, expectedId: 2);
        }

        /// <summary>
        /// Tests that the <see cref="McpMessageHandler.HandleAsync"/> method returns a list of tools when a 
        /// 'tools/list' request is processed.
        /// </summary>
        [TestMethod]
        public async Task Test_HandleAsync_ToolsList_ReturnsTenTools()
        {
            // Arrange
            var request = McpTestHelper.Request(id: 3, method: "tools/list");

            // Act
            var response = await _handler.HandleAsync(request);

            // Assert
            Assert.IsNotNull(response);
            Assert.IsNull(response.Error);

            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(response.Result));
            var tools     = doc.RootElement.GetProperty("tools").EnumerateArray().ToList();
            Assert.HasCount(10, tools);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "list_tables", "describe_table", "list_views", "list_procedures",
                    "list_triggers", "list_functions", "get_object_definition", "search_definitions", "find_references",
                    "execute_read_query"
                },
                tools.Select(t => t.GetProperty("name").GetString()).ToList());
        }

        /// <summary>
        /// Tests that the <see cref="McpMessageHandler.HandleAsync"/> method returns an error response when an unknown 
        /// method is processed.
        /// </summary>
        [TestMethod]
        public async Task Test_HandleAsync_UnknownMethod_Returns32601()
        {
            // Arrange
            var request = McpTestHelper.Request(id: 4, method: "does/not/exist");

            // Act
            var response = await _handler.HandleAsync(request);

            // Assert
            Assert.IsNotNull(response);
            Assert.IsNull(response.Result);
            McpTestHelper.AssertError(response, ErrorCodes.MethodNotFound, "does/not/exist");
        }

        /// <summary>
        /// Tests that the <see cref="McpMessageHandler.HandleAsync"/> method returns the expected database 
        /// text when a tool call is processed.
        /// </summary>
        /// <param name="tool">The name of the tool to call.</param>
        /// <param name="expected">The expected text to be returned in the result.</param>
        [TestMethod]
        [DataRow("list_tables"    , "dbo.Orders")]
        [DataRow("list_views"     , "dbo.vwActive")]
        [DataRow("list_procedures", "dbo.sp_GetOrders")]
        [DataRow("list_triggers"  , "dbo.tr_Audit_Insert")]
        [DataRow("list_functions" , "dbo.fn_FormatDate")]
        public async Task Test_HandleAsync_ToolCall_ReturnsDatabaseText(string tool, string expected)
        {
            // Arrange
            var request = McpTestHelper.ToolCall(4, tool);

            // Act
            var response = await _handler.HandleAsync(request);

            // Assert
            Assert.IsNotNull(response);
            Assert.IsNull(response.Error);
            Assert.IsNotNull(response.Result);
            McpTestHelper.AssertId(actualId: response.Id, expectedId: 4);
            Assert.AreEqual(expected, McpTestHelper.GetToolText(response));
            Assert.IsFalse(McpTestHelper.IsToolError(response));
        }

        /// <summary>
        /// Tests that the <see cref="McpMessageHandler.HandleAsync"/> method correctly passes the table name to the 
        /// database service when a 'describe_table' tool call is processed.
        /// </summary>
        [TestMethod]
        public async Task Test_HandleAsync_DescribeTable_PassesTableName()
        {
            // Arrange
            var request = McpTestHelper.ToolCall(4, "describe_table", """{"tableName":"dbo.Orders"}""");

            // Act
            var response = await _handler.HandleAsync(request);

            // Assert
            Assert.IsNotNull(response);
            Assert.AreEqual("dbo.Orders", _test.LastDescribeTableName);
            Assert.AreEqual(_test.DescribeTableResult.Text, McpTestHelper.GetToolText(response));
            Assert.IsFalse(McpTestHelper.IsToolError(response));
        }

        /// <summary>
        /// Tests that tool execution failures return MCP 'isError: true' content instead of a hang.
        /// </summary>
        [TestMethod]
        public async Task Test_HandleAsync_ExecuteReadQuery_ValidationFailure_ReturnsToolError()
        {
            // Arrange
            _test.ExecuteReadQueryException = new InvalidOperationException("Only SELECT statements are allowed.");
            var request = McpTestHelper.ToolCall(4, "execute_read_query", """{"sql":"DELETE FROM Customers"}""");

            // Act
            var response = await _handler.HandleAsync(request);

            // Assert
            Assert.IsNotNull(response);
            Assert.IsNull(response.Error);
            Assert.IsTrue(McpTestHelper.IsToolError(response));
            StringAssert.Contains(McpTestHelper.GetToolText(response), "Only SELECT statements are allowed.");
        }

        /// <summary>
        /// Tests that the <see cref="McpMessageHandler.HandleAsync"/> method correctly passes the object name to the 
        /// database service when a 'get_object_definition' tool call is processed.
        /// </summary>
        [TestMethod]
        public async Task Test_HandleAsync_GetObjectDefinition_PassesObjectName()
        {
            // Arrange
            var request = McpTestHelper.ToolCall(4, "get_object_definition", """{"objectName":"dbo.sp_GetOrders"}""");

            // Act
            var response = await _handler.HandleAsync(request);

            // Assert
            Assert.IsNotNull(response);
            Assert.AreEqual("dbo.sp_GetOrders", _test.LastObjectDefinitionName);
        }

        /// <summary>
        /// Tests that the <see cref="McpMessageHandler.HandleAsync"/> method correctly passes the search text to the 
        /// database service when a 'search_definitions' tool call is processed.
        /// </summary>
        [TestMethod]
        public async Task Test_HandleAsync_SearchDefinitions_PassesText()
        {
            // Arrange
            var request = McpTestHelper.ToolCall(4, "search_definitions", """{"text":"SELECT"}""");

            // Act
            var response = await _handler.HandleAsync(request);

            // Assert
            Assert.IsNotNull(response);
            Assert.AreEqual("SELECT", _test.LastSearchText);
        }

        /// <summary>
        /// Tests that the <see cref="McpMessageHandler.HandleAsync"/> method correctly passes the object name to the 
        /// database service when a 'find_references' tool call is processed.
        /// </summary>
        [TestMethod]
        public async Task Test_HandleAsync_FindReferences_PassesObjectName()
        {
            // Arrange
            var request = McpTestHelper.ToolCall(4, "find_references", """{"objectName":"dbo.Orders"}""");

            // Act
            var response = await _handler.HandleAsync(request);

            // Assert
            Assert.IsNotNull(response);
            Assert.AreEqual("dbo.Orders", _test.LastReferencesObjectName);
        }

        /// <summary>
        /// Tests that the <see cref="McpMessageHandler.HandleAsync"/> method returns an error response when a request 
        /// with a missing or invalid method name is processed.
        /// </summary>
        /// <param name="methodName">The name of the method being called.</param>
        [TestMethod]
        [DataRow("invalid/method")]
        [DataRow("")]
        public async Task Test_HandleAsync_Missing_Or_Invalid_MethodName_Returns32601(string methodName)
        {
            // Arrange
            var request = McpTestHelper.Request(4, methodName);

            // Act
            var response = await _handler.HandleAsync(request);

            // Assert
            Assert.IsNotNull(response);
            McpTestHelper.AssertError(response, code: ErrorCodes.MethodNotFound, messageContains: $"Method '{request.Method}' not found");
        }

        /// <summary>
        /// Tests that the <see cref="McpMessageHandler.HandleAsync"/> method returns an error response when a tool 
        /// call is processed with an unknown tool name.
        /// </summary>
        [TestMethod]
        public async Task Test_HandleAsync_ToolCall_UnknownTool_Returns32602()
        {
            // Arrange
            var request = McpTestHelper.ToolCall(4, "not_a_real_tool");

            // Act
            var response = await _handler.HandleAsync(request);

            // Assert
            Assert.IsNotNull(response);
            McpTestHelper.AssertError(response, code: ErrorCodes.InvalidParams, messageContains: "Unknown tool");
        }

        /// <summary>
        /// Tests that the <see cref="McpMessageHandler.HandleAsync"/> method returns an error response when a
        /// tool call is processed without required parameter.
        /// </summary>
        /// <param name="toolName">The name of the tool being called.</param>
        /// <param name="contains">The name of the required parameter that is missing.</param>
        [TestMethod]
        [DataRow("describe_table"       , "tableName")]
        [DataRow("get_object_definition", "objectName")]
        [DataRow("search_definitions"   , "text")]
        [DataRow("find_references"      , "objectName")]
        [DataRow("execute_read_query"   , "sql")]
        public async Task Test_HandleAsync_Returns32602_For_MissingArguments(string toolName, string contains)
        {
            // Arrange
            var request = McpTestHelper.ToolCall(4, toolName, "{}");

            // Act
            var response = await _handler.HandleAsync(request);

            // Assert
            Assert.IsNotNull(response);
            McpTestHelper.AssertError(response, code: ErrorCodes.InvalidParams, messageContains: contains);
        }

        /// <summary>
        /// Tests that the <see cref="McpMessageHandler.HandleAsync"/> method returns an error response when a
        /// tool call is processed with empty parameter.
        /// </summary>
        /// <param name="toolName">The name of the tool being called.</param>
        /// <param name="contains">The name of the required parameter that is empty.</param>
        /// <param name="value">The value of the empty parameter.</param>
        [TestMethod]
        [DataRow("describe_table"       , "tableName" , "")]
        [DataRow("get_object_definition", "objectName", "")]
        [DataRow("search_definitions"   , "text"      , "")]
        [DataRow("find_references"      , "objectName", "")]
        [DataRow("execute_read_query"   , "sql"       , "")]
        public async Task Test_HandleAsync_Returns32602_For_EmptyArguments(string toolName, string contains, string value)
        {
            // Arrange
            var request = McpTestHelper.ToolCall(4, toolName, $"{{\"{contains}\":\"{value}\"}}");

            // Act
            var response = await _handler.HandleAsync(request);

            // Assert
            Assert.IsNotNull(response);
            McpTestHelper.AssertError(response, code: ErrorCodes.InvalidParams, messageContains: contains);
        }
    }
}
