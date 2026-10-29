using Microsoft.Data.SqlClient;
using Mcp.Server.Models;

namespace Mcp.Server.Services.Interfaces
{
    /// <summary>
    /// Provides methods for executing SQL queries against a SQL Server database, returning results 
    /// in a format suitable for MCP tool handlers.
    /// </summary>
    public interface ISqlExecutor
    {
        /// <summary>
        /// Executes a read-only query that returns a single string value.
        /// </summary>
        /// <param name="sql">The SQL statement to execute.</param>
        /// <param name="parameters">Optional parameters bound to the command.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>
        /// The first column of the first row, or <see langword="null"/> when no row is returned.
        /// </returns>
        Task<string?> ExecuteScalarStringAsync(string sql, SqlParameter[] parameters, CancellationToken cancellationToken);

        /// <summary>
        /// Executes a read-only SQL query and formats each result row by joining column values.
        /// </summary>
        /// <param name="sql">The SQL statement to execute.</param>
        /// <param name="parameters">Optional parameters bound to the command.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>
        /// A <see cref="QueryResult"/> containing the query results, with columns and rows formatted as specified.
        /// </returns>
        Task<QueryResult> ExecuteMultiColumnRowsAsync(string sql, SqlParameter[] parameters, CancellationToken cancellationToken);
    }
}
