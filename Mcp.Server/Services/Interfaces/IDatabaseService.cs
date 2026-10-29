using Mcp.Server.Models;

namespace Mcp.Server.Services.Interfaces;

/// <summary>
/// Read-only catalog queries against a SQL Server database for MCP tool handlers.
/// </summary>
public interface IDatabaseService
{
    /// <summary>
    /// Verifies that the configured database is reachable by opening and closing a connection.
    /// Called once at application startup before the MCP message loop begins.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// <see langword="true"/> if the connection is successfully opened and closed;
    /// <see langword="false"/> if the server is unreachable, authentication fails, or the attempt is cancelled.
    /// </returns>
    Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Lists user-defined base tables from 'sys.tables'.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A object of <see cref="QueryResult"/> containing the result.
    /// </returns>
    Task<QueryResult> GetTablesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Lists views from 'sys.views'.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A object of <see cref="QueryResult"/> containing the result.
    /// </returns>
    Task<QueryResult> GetViewsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Lists user-defined stored procedures from 'sys.procedures'.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A object of <see cref="QueryResult"/> containing the result.
    /// </returns>
    Task<QueryResult> GetProceduresAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Lists triggers from 'sys.triggers'.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A object of <see cref="QueryResult"/> containing the result.
    /// </returns>
    Task<QueryResult> GetTriggersAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Lists user-defined functions from 'sys.objects' (scalar and table-valued).
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A object of <see cref="QueryResult"/> containing the result.
    /// </returns>
    Task<QueryResult> GetFunctionsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Returns column metadata for a table or view from 'INFORMATION_SCHEMA.COLUMNS'.
    /// </summary>
    /// <param name="tableName">Table to describe, optionally as 'schema.table'.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A object of <see cref="QueryResult"/> containing the result.
    /// </returns>
    Task<QueryResult> DescribeTableAsync(string tableName, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the T-SQL source definition of a schema-scoped object (view, procedure, function, or trigger).
    /// </summary>
    /// <param name="objectName">Object to script, preferably as 'schema.object'.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A object of <see cref="QueryResult"/> containing the result.
    /// </returns>
    Task<QueryResult> GetObjectDefinitionAsync(string objectName, CancellationToken cancellationToken);

    /// <summary>
    /// Searches module definitions (procedures, views, functions, triggers) for the given text.
    /// </summary>
    /// <param name="text">Substring to find in object definitions.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A object of <see cref="QueryResult"/> containing the result.
    /// </returns>
    Task<QueryResult> SearchObjectDefinitionsAsync(string text, CancellationToken cancellationToken);

    /// <summary>
    /// Lists objects that reference the given table or view by name.
    /// </summary>
    /// <param name="objectName">Referenced table or view name.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A object of <see cref="QueryResult"/> containing the result.
    /// </returns>
    Task<QueryResult> GetObjectReferencesAsync(string objectName, CancellationToken cancellationToken);

    /// <summary>
    /// Executes an arbitrary read-only SQL query and returns the result.
    /// </summary>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A object of <see cref="QueryResult"/> containing the result.
    /// </returns>
    Task<QueryResult> ExecuteReadQueryAsync(string sql, CancellationToken cancellationToken = default);
}