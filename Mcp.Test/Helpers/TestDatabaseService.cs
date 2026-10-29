using Mcp.Server.Models;
using Mcp.Server.Services.Interfaces;

namespace Mcp.Test.Helpers
{
    /// <summary>
    /// Test double for <see cref="IDatabaseService"/>; returns configurable strings per method.
    /// </summary>
    internal sealed class TestDatabaseService : IDatabaseService
    {
        /// <summary>
        /// Creates a <see cref="QueryResult"/> with the given text.
        /// </summary>
        /// <param name="text">The text to return in the <see cref="QueryResult"/>.</param>
        /// <returns>
        /// A <see cref="QueryResult"/> with the given text and default values for other properties.
        /// </returns>
        private static QueryResult Result(string text) => new() { Text = text };

        // Configurable responses
        public QueryResult TablesResult             { get; set; } = Result("dbo.Orders");
        public QueryResult ViewsResult              { get; set; } = Result("dbo.vwActive");
        public QueryResult ProceduresResult         { get; set; } = Result("dbo.sp_GetOrders");
        public QueryResult TriggersResult           { get; set; } = Result("dbo.tr_Audit_Insert");
        public QueryResult FunctionsResult          { get; set; } = Result("dbo.fn_FormatDate");
        public QueryResult DescribeTableResult      { get; set; } = Result("Id.int.NO.");
        public QueryResult ObjectDefinitionResult   { get; set; } = Result("CREATE PROCEDURE dbo.sp_GetOrders AS SELECT 1;");
        public QueryResult SearchDefinitionsResult  { get; set; } = Result("SQL_STORED_PROCEDURE.dbo.sp_GetOrders");
        public QueryResult ObjectReferencesResult   { get; set; } = Result("dbo.sp_GetOrders");
        public QueryResult ExecuteReadQueryResult   { get; set; } = Result("SELECT 1");
        public Exception? ExecuteReadQueryException { get; set; }

        // Captured inputs
        public string? LastDescribeTableName    { get; private set; }
        public string? LastObjectDefinitionName { get; private set; }
        public string? LastSearchText           { get; private set; }
        public string? LastReferencesObjectName { get; private set; }
        public string? LastExecuteReadQuerySql  { get; private set; }

        public Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken)   => Task.FromResult(true);
        public Task<QueryResult> GetTablesAsync(CancellationToken cancellationToken)     => Task.FromResult(TablesResult);     // list_tables
        public Task<QueryResult> GetViewsAsync(CancellationToken cancellationToken)      => Task.FromResult(ViewsResult);      // list_views
        public Task<QueryResult> GetProceduresAsync(CancellationToken cancellationToken) => Task.FromResult(ProceduresResult); // list_procedures
        public Task<QueryResult> GetTriggersAsync(CancellationToken cancellationToken)   => Task.FromResult(TriggersResult);   // list_triggers
        public Task<QueryResult> GetFunctionsAsync(CancellationToken cancellationToken)  => Task.FromResult(FunctionsResult);  // list_functions
        
        // describe_table
        public Task<QueryResult> DescribeTableAsync(string tableName, CancellationToken cancellationToken)
        {
            LastDescribeTableName = tableName;
            return Task.FromResult(DescribeTableResult);
        }

        // get_object_definition
        public Task<QueryResult> GetObjectDefinitionAsync(string objectName, CancellationToken cancellationToken)
        {
            LastObjectDefinitionName = objectName;
            return Task.FromResult(ObjectDefinitionResult);
        }

        // search_definitions
        public Task<QueryResult> SearchObjectDefinitionsAsync(string text, CancellationToken cancellationToken)
        {
            LastSearchText = text;
            return Task.FromResult(SearchDefinitionsResult);
        }

        // find_references
        public Task<QueryResult> GetObjectReferencesAsync(string objectName, CancellationToken cancellationToken)
        {
            LastReferencesObjectName = objectName;
            return Task.FromResult(ObjectReferencesResult);
        }

        // execute_read_query
        public Task<QueryResult> ExecuteReadQueryAsync(string sql, CancellationToken cancellationToken)
        {
            LastExecuteReadQuerySql = sql;
            if (ExecuteReadQueryException is not null)
                throw ExecuteReadQueryException;
            return Task.FromResult(ExecuteReadQueryResult);
        }
    }
}
