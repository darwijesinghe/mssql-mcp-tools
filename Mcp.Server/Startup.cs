using Mcp.Server.Models;
using Mcp.Server.Services;
using Mcp.Server.Services.Interfaces;
using Mcp.Server.Utils;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mcp.Server
{
    /// <summary>
    /// The Startup class is responsible for initializing services and running the main loop 
    /// of the MCP server, which processes JSON-RPC messages from stdin and writes responses to stdout.
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// Logger service reference.
        /// </summary>
        private readonly ILogger<Startup>  _logger;

        /// <summary>
        /// Database service reference.
        /// </summary>
        private readonly IDatabaseService  _database;

        /// <summary>
        /// The MCP message handler reference.
        /// </summary>
        private readonly McpMessageHandler _handler;

        /// <summary>
        /// The constructor for the Startup class, which initializes the logger service.
        /// </summary>
        /// <param name="logger">The logger service used to record diagnostic information.</param>
        /// <param name="database">The database service used to execute MCP tool queries.</param>
        /// <param name="handler">The MCP message handler used to process incoming requests.</param>
        public Startup(ILogger<Startup> logger, IDatabaseService database, McpMessageHandler handler) 
        {
            _logger   = logger;
            _database = database;
            _handler  = handler;
        }

        /// <summary>
        /// Starts the MCP server by entering a loop that reads JSON-RPC messages from 
        /// stdin, processes them, and writes responses to stdout.
        /// </summary>
        public async Task Run()
        {
            // MCP servers communicate over stdio: the host (e.g. Cursor) spawns this process and
            // sends one JSON-RPC message per line on stdin; we write one JSON-RPC response per line on stdout
            // Fatal startup errors exit with code 1 so the host can detect a failed launch
            try
            {
                // Bound DB validation so a hung SQL connect cannot exceed Cursor's MCP createClient timeout
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

                _logger.LogInformation("Application starting...");

                // Fail fast before entering the loop if SQL Server is unreachable or misconfigured
                var con = await _database.ValidateConnectionAsync(cts.Token);
                if (!con)
                {
                    _logger.LogError("Database connection validation failed.");
                    Environment.Exit(1);
                }

                _logger.LogInformation("Database connected; entering MCP stdio loop.");

                // Omit null properties from responses (e.g. no "error" field on success) per JSON-RPC conventions
                var serializerOptions = new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                // Process messages until the host closes stdin or kills the process
                while (true)
                {
                    var line = Console.ReadLine();

                    // Host closed stdin (disconnect / restart) — exit cleanly so Cursor can spawn a fresh process
                    if (line is null)
                        break;

                    // Blank lines are ignored; the host should send valid JSON-RPC payloads only
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    JsonRpcRequest? request = null;
                    try
                    {
                        // Deserialize the incoming JSON-RPC request
                        request = JsonSerializer.Deserialize<JsonRpcRequest>(line);

                        // Malformed JSON or empty object: skip without replying (no id to correlate an error to)
                        if (request is null)
                            continue;

                        // Handle the request and get a response
                        var response = await _handler.HandleAsync(request);

                        // Notifications (methods starting with "notifications/") do not get a response
                        if (response is null)
                            continue;

                        var json = JsonSerializer.Serialize(response, serializerOptions);

                        // Each response must be a single line so the host can frame messages on stdout
                        Console.WriteLine(json);
                    }
                    catch (Exception ex)
                    {
                        // Log and continue: one bad message must not tear down the whole server session
                        _logger.LogError(ex, "Error processing message.");

                        // Always reply when we have a request id — otherwise CallMcpTool hangs until timeout
                        if (request is not null)
                        {
                            var errorResponse = new JsonRpcResponse
                            {
                                Id = JsonHelper.ConvertId(request.Id),
                                Error = new JsonRpcError
                                {
                                    Code    = (int)ErrorCodes.InternalError,
                                    Message = ex.Message
                                }
                            };

                            Console.WriteLine(JsonSerializer.Serialize(errorResponse, serializerOptions));
                        }
                    }
                }

                _logger.LogInformation("Stdin closed; shutting down MCP stdio loop.");
            }
            catch (Exception ex)
            {
                // Startup failures (missing env var, DB unreachable, etc.) are logged and reported via exit code
                _logger.LogError(ex, "Startup failure.");
                Environment.Exit(1);
            }
        }
    }
}
