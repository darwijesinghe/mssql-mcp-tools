namespace Mcp.Server.Models
{
    /// <summary>
    /// Represents the result of a SQL query executed by the MCP server.
    /// </summary>
    public sealed class QueryResult
    {
        /// <summary>
        /// Gets or sets the optional text message associated with the query result.
        /// </summary>
        public string? Text                                    { get; set; }

        /// <summary>
        /// Gets or sets the list of column names returned by the query.
        /// </summary>
        public IReadOnlyList<string> Columns                   { get; init; }

        /// <summary>
        /// Gets or sets the list of rows returned by the query, where each row is represented 
        /// as a dictionary mapping column names to their corresponding values.
        /// </summary>
        public IReadOnlyList<Dictionary<string, object?>> Rows { get; init; }

        /// <summary>
        /// Gets or sets the total number of rows returned by the query. This may be 
        /// less than the actual number of rows if the result was truncated.
        /// </summary>
        public int RowCount                                    { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the query result was truncated due to exceeding a maximum row limit.
        /// </summary>
        public bool Truncated                                  { get; set; }
    }
}
