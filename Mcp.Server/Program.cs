using Mcp.Server;
using Mcp.Server.Models;
using Mcp.Server.Services;
using Mcp.Server.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Settings.Configuration;
using Log = Serilog.Log;

class Program
{
    /// <summary>
    /// Application entry point: builds the host, configures file-only logging, and runs the MCP stdio loop.
    /// </summary>
    /// <param name="args">Command-line arguments (unused).</param>
    static async Task Main(string[] args)
    {
        // Early logger so bootstrap failures still hit a file (cwd-independent)
        var bootstrapLog = Path.Combine(AppContext.BaseDirectory, "mssql-mcp-tools-bootstrap.log");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(bootstrapLog, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            Log.Information("Starting mssql-mcp-tools...");

            // adding services
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    // MCP uses stdout for JSON-RPC only — never write logs to the console
                    logging.ClearProviders();
                })
                .ConfigureAppConfiguration((context, config) =>
                {
                    // Use the base directory of the application to locate configuration files, ensuring
                    // that the application can find its settings regardless of the current working directory
                    var basePath = AppContext.BaseDirectory;

                    config.SetBasePath(basePath)
                           .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                           .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);
                })
                .UseSerilog((context, services, configuration) =>
                {
                    // Single-file publish cannot scan for Serilog.* DLLs — name the File sink assembly explicitly
                    var readerOptions = new ConfigurationReaderOptions(
                        typeof(FileLoggerConfigurationExtensions).Assembly);

                    configuration
                        .ReadFrom.Configuration(context.Configuration, readerOptions)
                        .ReadFrom.Services(services);
                })
                .ConfigureServices((context, services) =>
                {
                    // Binds sections of appsettings.json
                    services.Configure<AppSettings>(context.Configuration);

                    // Singleton services for dependency injection
                    services.AddSingleton<ISqlExecutor, SqlExecutor>();
                    services.AddSingleton<IDatabaseService, DatabaseService>();
                    services.AddSingleton<McpMessageHandler>();
                })
                .Build();

            // Automatically resolve and inject any dependencies required by the Startup class's constructor
            // This helps in creating instances of classes that have dependencies registered in the DI container

            var service = ActivatorUtilities.CreateInstance<Startup>(host.Services);
            await service.Run();
            Log.Information("Program.cs -> All good.");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly.");
            Environment.ExitCode = 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}
