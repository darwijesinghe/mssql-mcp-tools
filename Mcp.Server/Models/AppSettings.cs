namespace Mcp.Server.Models
{
    /// <summary>
    /// Represents the application settings for the MCP server.
    /// </summary>
    public sealed class AppSettings
    {
        /// <inheritdoc/>
        public Database Database         { get; set; }

        /// <inheritdoc/>
        public QueryOptions QueryOptions { get; set; }

        /// <inheritdoc/>
        public Log Log                   { get; set; }
    }

    /// <summary>
    /// Represents the database connection settings for the MCP server.
    /// </summary>
    public class Database
    {
        /// <summary>
        /// Gets or sets the connection string used to connect to the SQL Server database.
        /// </summary>
        public string ConnectionString { get; set; }
    }

    /// <summary>
    /// Represents the query execution options for the MCP server, including limits on rows, cell length, and command timeout.
    /// </summary>
    public class QueryOptions
    {
        /// <summary>
        /// Gets or sets the maximum number of rows that can be returned in a query result. If a query 
        /// exceeds this limit, the result will be truncated.
        /// </summary>
        public int MaxRows               { get; set; }

        /// <summary>
        /// Gets or sets the maximum length of a cell in the query result. If a cell exceeds this length, it will be truncated.
        /// </summary>
        public int MaxCellLength         { get; set; }
        
        /// <summary>
        /// Gets or sets the command timeout in seconds for query execution.
        /// </summary>
        public int CommandTimeoutSeconds { get; set; }
    }

    /// <summary>
    /// Represents the logging settings for the MCP server, including the path to the log file.
    /// </summary>
    public class Log
    {
        /// <summary>
        /// Gets or sets the path to the log file where server logs will be written.
        /// </summary>
        public string LogPath { get; set; }
    }
}