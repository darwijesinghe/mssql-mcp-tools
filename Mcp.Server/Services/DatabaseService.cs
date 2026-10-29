using Mcp.Server.Models;
using Mcp.Server.Services.Interfaces;
using Mcp.Server.Utils;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Mcp.Server.Services;

/// <summary>
/// Provides read-only catalog queries against a SQL Server database for MCP tool handlers.
/// Catalog and query methods return a <see cref="QueryResult"/>.
/// </summary>
public sealed class DatabaseService : IDatabaseService
{
    /// <summary>
    /// SQL executor service reference.
    /// </summary>
    private readonly ISqlExecutor _sqlExecutor;

    /// <summary>
    /// The SQL server connection string from app settings.
    /// </summary>
    private readonly string       _connectionString;

    /// <summary>
    /// Initializes the service with a SQL Server connection string and query options.
    /// </summary>
    /// <param name="sqlExecutor">The SQL executor service.</param>
    /// <param name="options">The application settings.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no connection string is configured.
    /// </exception>
    public DatabaseService(ISqlExecutor sqlExecutor, IOptions<AppSettings> options)
    {
        _connectionString = string.IsNullOrWhiteSpace(options.Value.Database.ConnectionString)
            ? throw new InvalidOperationException("No connection string configured.")
            : options.Value.Database.ConnectionString;
        _sqlExecutor      = sqlExecutor;
    }

    /// <inheritdoc />
    public async Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken)
    {
        await using var conn = new SqlConnection(_connectionString);
        try
        {
            await conn.OpenAsync(cancellationToken);
            await conn.CloseAsync();
            return true;
        }
        catch (SqlException)
        {
            // Unreachable server, bad credentials, etc. — Startup treats false as fatal exit
            return false;
        }
        catch (OperationCanceledException)
        {
            // Startup bounds validation with a timeout; cancelled attempt counts as failure
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<QueryResult> GetTablesAsync(CancellationToken cancellationToken)
    {
        const string sql =
        """
        SELECT
            S.NAME AS SCHEMANAME,
            T.NAME AS TABLENAME
        FROM SYS.TABLES T
        INNER JOIN SYS.SCHEMAS S
            ON T.SCHEMA_ID = S.SCHEMA_ID
        WHERE T.IS_MS_SHIPPED = 0
          AND T.TEMPORAL_TYPE <> 2
        ORDER BY S.NAME, T.NAME;
        """;

        return await _sqlExecutor.ExecuteMultiColumnRowsAsync(sql, [], cancellationToken);
    }

    /// <inheritdoc />
    public async Task<QueryResult> GetViewsAsync(CancellationToken cancellationToken)
    {
        const string sql =
        """
        SELECT
            S.NAME AS SCHEMANAME,
            V.NAME AS VIEWNAME
        FROM SYS.VIEWS V
        INNER JOIN SYS.SCHEMAS S
            ON V.SCHEMA_ID = S.SCHEMA_ID
        ORDER BY S.NAME, V.NAME;
        """;

        return await _sqlExecutor.ExecuteMultiColumnRowsAsync(sql, [], cancellationToken);
    }

    /// <inheritdoc />
    public async Task<QueryResult> GetProceduresAsync(CancellationToken cancellationToken)
    {
        const string sql =
        """
        SELECT
            S.NAME AS SCHEMANAME,
            P.NAME AS PROCEDURENAME
        FROM SYS.PROCEDURES P
        INNER JOIN SYS.SCHEMAS S
            ON P.SCHEMA_ID = S.SCHEMA_ID
        WHERE P.IS_MS_SHIPPED = 0
          AND P.PARENT_OBJECT_ID = 0
        ORDER BY S.NAME, P.NAME;
        """;

        return await _sqlExecutor.ExecuteMultiColumnRowsAsync(sql, [], cancellationToken);
    }

    /// <inheritdoc />
    public async Task<QueryResult> GetTriggersAsync(CancellationToken cancellationToken)
    {
        const string sql =
        """
        SELECT
            CASE
                WHEN TR.PARENT_CLASS = 0 THEN N'(DATABASE)'
                ELSE CONCAT(OBJECT_SCHEMA_NAME(TR.PARENT_ID), N'.', OBJECT_NAME(TR.PARENT_ID))
            END AS PARENTOBJECT,
            TR.NAME AS TRIGGERNAME,
            TR.IS_DISABLED AS ISDISABLED
        FROM SYS.TRIGGERS TR
        WHERE TR.IS_MS_SHIPPED = 0
        ORDER BY PARENTOBJECT, TR.NAME;
        """;

        return await _sqlExecutor.ExecuteMultiColumnRowsAsync(sql, [], cancellationToken);
    }

    /// <inheritdoc />
    public async Task<QueryResult> GetFunctionsAsync(CancellationToken cancellationToken)
    {
        const string sql =
        """
        SELECT
            S.NAME AS SCHEMANAME,
            O.NAME AS FUNCTIONNAME
        FROM SYS.OBJECTS O
        INNER JOIN SYS.SCHEMAS S
            ON O.SCHEMA_ID = S.SCHEMA_ID
        WHERE O.TYPE IN (N'FN', N'IF', N'TF')
          AND O.IS_MS_SHIPPED = 0
        ORDER BY S.NAME, O.NAME;
        """;

        return await _sqlExecutor.ExecuteMultiColumnRowsAsync(sql, [], cancellationToken);
    }

    /// <inheritdoc />
    public async Task<QueryResult> DescribeTableAsync(string tableName, CancellationToken cancellationToken)
    {
        string? schema = null;
        string table;

        var dotIndex = tableName.IndexOf('.');
        if (dotIndex >= 0)
        {
            schema = tableName[..dotIndex];
            table  = tableName[(dotIndex + 1)..];
        }
        else
        {
            table = tableName;
        }

        const string sql =
        """
        SELECT
            c.COLUMN_NAME,
            c.DATA_TYPE,
            c.IS_NULLABLE,
            c.CHARACTER_MAXIMUM_LENGTH
        FROM INFORMATION_SCHEMA.COLUMNS c
        WHERE c.TABLE_NAME = @tableName
          AND (@tableSchema IS NULL OR c.TABLE_SCHEMA = @tableSchema)
        ORDER BY c.ORDINAL_POSITION;
        """;

        SqlParameter[] parameters =
        {
            new ("@tableName"  , table),
            new ("@tableSchema", (object?)schema ?? DBNull.Value)
        };

        return await _sqlExecutor.ExecuteMultiColumnRowsAsync(
            sql,
            parameters,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<QueryResult> GetObjectDefinitionAsync(string objectName, CancellationToken cancellationToken)
    {
        string? schema = null;
        string name;

        var dotIndex = objectName.IndexOf('.');
        if (dotIndex >= 0)
        {
            schema = objectName[..dotIndex];
            name   = objectName[(dotIndex + 1)..];
        }
        else
        {
            name = objectName;
        }

        var qualifiedName = schema is not null ? $"{schema}.{name}" : name;

        const string sql =
        """
        SELECT COALESCE(
            OBJECT_DEFINITION(OBJECT_ID(@qualifiedName)),
            (
                SELECT TOP (1) OBJECT_DEFINITION(O.OBJECT_ID)
                FROM SYS.OBJECTS O
                INNER JOIN SYS.SCHEMAS S ON O.SCHEMA_ID = S.SCHEMA_ID
                WHERE O.NAME = @objectName
                  AND (@schema IS NULL OR S.NAME = @schema)
                  AND O.TYPE IN (N'P', N'V', N'TR', N'FN', N'IF', N'TF', N'PC')
                ORDER BY S.NAME
            ),
            (
                SELECT TOP (1) OBJECT_DEFINITION(TR.OBJECT_ID)
                FROM SYS.TRIGGERS TR
                WHERE TR.NAME = @objectName
                  AND TR.PARENT_CLASS = 0
            )
        );
        """;

        SqlParameter[] parameters =
        {
            new ("@qualifiedName", qualifiedName),
            new ("@objectName"   , name),
            new ("@schema"       , (object?)schema ?? DBNull.Value)
        };

        var definition = await _sqlExecutor.ExecuteScalarStringAsync(
            sql,
            parameters,
            cancellationToken);

        if (string.IsNullOrEmpty(definition))
        {
            return new QueryResult
            {
                Text = $"No definition found for '{objectName}'. "
                 + "Use schema.object (for example, dbo.usp_GetOrders). "
                 + "Tables have no script; encrypted objects require VIEW DEFINITION permission."
            };
        }

        return new QueryResult
        {
            Text = definition
        };
    }

    /// <inheritdoc />
    public async Task<QueryResult> SearchObjectDefinitionsAsync(string text, CancellationToken cancellationToken)
    {
        const string sql =
        """
        SELECT
            O.TYPE_DESC,
            S.NAME AS SCHEMANAME,
            O.NAME
        FROM SYS.SQL_MODULES M
        INNER JOIN SYS.OBJECTS O
            ON M.OBJECT_ID = O.OBJECT_ID
        INNER JOIN SYS.SCHEMAS S
            ON O.SCHEMA_ID = S.SCHEMA_ID
        WHERE M.DEFINITION LIKE '%' + @text + '%'
          AND O.IS_MS_SHIPPED = 0
        ORDER BY O.NAME;
        """;

        SqlParameter[] parameters =
        {
            new SqlParameter("@text", text)
        };

        return await _sqlExecutor.ExecuteMultiColumnRowsAsync(
            sql,
            parameters, 
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<QueryResult> GetObjectReferencesAsync(string objectName, CancellationToken cancellationToken)
    {
        string? schema = null;
        string name;

        var dotIndex = objectName.IndexOf('.');
        if (dotIndex >= 0)
        {
            schema = objectName[..dotIndex];
            name   = objectName[(dotIndex + 1)..];
        }
        else
        {
            name = objectName;
        }

        const string sql =
        """
        SELECT
            OBJECT_SCHEMA_NAME(REFERENCING_ID) AS REFERENCING_SCHEMA_NAME,
            OBJECT_NAME(REFERENCING_ID) AS REFERENCING_OBJECT_NAME
        FROM SYS.SQL_EXPRESSION_DEPENDENCIES
        WHERE REFERENCED_ENTITY_NAME = @ObjectName
          AND (@referencedSchema IS NULL OR REFERENCED_SCHEMA_NAME = @referencedSchema OR REFERENCED_SCHEMA_NAME IS NULL)
        ORDER BY REFERENCING_SCHEMA_NAME, REFERENCING_OBJECT_NAME;
        """;

        SqlParameter[] parameters =
        {
            new ("@ObjectName"      , name),
            new ("@referencedSchema", (object?)schema ?? DBNull.Value)
        };

        return await _sqlExecutor.ExecuteMultiColumnRowsAsync(
            sql,
            parameters,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<QueryResult> ExecuteReadQueryAsync(string sql, CancellationToken cancellationToken = default)
    {
        QueryValidator.Validate(sql);
        return await _sqlExecutor.ExecuteMultiColumnRowsAsync(sql, [], cancellationToken);
    }
}