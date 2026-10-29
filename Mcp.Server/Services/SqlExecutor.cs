using Mcp.Server.Models;
using Mcp.Server.Services.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data;

namespace Mcp.Server.Services
{
    /// <summary>
    /// Executes SQL queries against a SQL Server database and returns results in a format suitable for MCP tool handlers.
    /// </summary>
    public class SqlExecutor : ISqlExecutor
    {
        /// <summary>
        /// The SQL server connection string from app settings.
        /// </summary>
        private readonly string       _connectionString;

        /// <summary>
        /// Query options from app settings.
        /// </summary>
        private readonly QueryOptions _queryOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlExecutor"/> class with the specified application settings.
        /// </summary>
        /// <param name="options">The application settings.</param>
        public SqlExecutor(IOptions<AppSettings> options)
        {
            _queryOptions     = options.Value.QueryOptions;
            _connectionString = options.Value.Database.ConnectionString;
        }

        /// <inheritdoc/>
        public async Task<string?> ExecuteScalarStringAsync(string sql, SqlParameter[] parameters, CancellationToken cancellationToken)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            await using var cmd = new SqlCommand(sql, conn)
            {
                CommandTimeout = _queryOptions.CommandTimeoutSeconds
            };

            if (parameters.Length > 0)
                cmd.Parameters.AddRange(parameters);

            var result = await cmd.ExecuteScalarAsync(cancellationToken);

            return result is null or DBNull ? null : Convert.ToString(result);
        }

        /// <inheritdoc/>
        public async Task<QueryResult> ExecuteMultiColumnRowsAsync(string sql, SqlParameter[] parameters, CancellationToken cancellationToken)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            await using var cmd = new SqlCommand(sql, conn)
            {
                CommandTimeout = _queryOptions.CommandTimeoutSeconds
            };

            if (parameters.Length > 0)
                cmd.Parameters.AddRange(parameters);

            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
            if (reader is null || reader.FieldCount == 0)
                return new QueryResult
                {
                    Columns   = Array.Empty<string>(),
                    Rows      = Array.Empty<Dictionary<string, object?>>(),
                    RowCount  = 0,
                    Truncated = false
                };

            // Columns
            var columns = Enumerable.Range(0, reader.FieldCount)
                .Select(i => reader.GetName(i))
                .ToList();

            // Rows
            var rows      = new List<Dictionary<string, object?>>();
            var truncated = false;

            while (await reader.ReadAsync(cancellationToken))
            {
                // Enforce max rows limit to prevent excessive memory usage
                if (rows.Count >= _queryOptions.MaxRows)
                {
                    truncated = true;
                    break;
                }

                // Build a dictionary for the current row, mapping column names to values
                var row = new Dictionary<string, object?>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    // Truncate long string values to prevent excessive memory usage
                    object? value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    if (value is string strValue && strValue.Length > _queryOptions.MaxCellLength)
                    {
                        value = $"{strValue.AsSpan(0, _queryOptions.MaxCellLength)}...(truncated)";
                    }

                    row[reader.GetName(i)] = value;
                }

                rows.Add(row);
            }

            return new QueryResult
            {
                Columns   = columns,
                Rows      = rows,
                RowCount  = rows.Count,
                Truncated = truncated
            };
        }
    }
}
