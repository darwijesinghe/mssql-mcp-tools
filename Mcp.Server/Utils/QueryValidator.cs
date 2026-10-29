using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Mcp.Server.Utils
{
    /// <summary>
    /// Provides methods for validating SQL queries to ensure they are safe and conform to 
    /// allowed statement types (e.g. SELECT statements only).
    /// </summary>
    public static class QueryValidator
    {
        /// <summary>
        /// Validates the provided SQL query string to ensure it is a valid 'SELECT' statement and does 
        /// not contain any disallowed statements.
        /// </summary>
        /// <param name="sql">The SQL query string to validate.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the SQL query is empty, contains parsing errors, or contains any disallowed statement types.
        /// </exception>
        public static void Validate(string sql)
        {
            // Parse the SQL query
            var parser       = new TSql180Parser(initialQuotedIdentifiers: false);
            using var reader = new StringReader(sql);
            var fragment     = parser.Parse(reader, out IList<ParseError> errors);

            if (errors.Count > 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(x => x.Message)));

            // Validate the statements in the parsed fragment
            ValidateStatements(fragment);
        }

        // Helpers -------------------------------------------

        /// <summary>
        /// Validates that the provided <see cref="TSqlFragment"/> contains only allowed statements (SELECT statements) and 
        /// throws an exception if any disallowed statements are found.
        /// </summary>
        /// <param name="fragment">The <see cref="TSqlFragment"/> to validate.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the fragment is not a <see cref="TSqlScript"/>, contains no statements, or contains any 
        /// disallowed statement types.
        /// </exception>
        private static void ValidateStatements(TSqlFragment fragment)
        {
            // Cast the fragment to a 'TSqlScript'
            var script = (TSqlScript)fragment;

            // Ensure the script contains statements
            var statements = script.Batches
                .SelectMany(x => x.Statements)
                .ToList();

            if (statements.Count == 0)
                throw new InvalidOperationException("No statements found.");
            
            // Ensure all statements are 'SELECT' statements
            foreach (var statement in statements)
            {
                //Debug.WriteLine("SelectStatement properties:");
                //foreach (var p in typeof(SelectStatement).GetProperties())
                //{
                //    Debug.WriteLine($" - {p.Name}");
                //}

                //Debug.WriteLine("QuerySpecification properties:");
                //foreach (var p in typeof(QuerySpecification).GetProperties())
                //{
                //    Debug.WriteLine($" - {p.Name}");
                //}

                if (statement is not SelectStatement select)
                    throw new InvalidOperationException($"Statement type '{statement.GetType().Name}' is not allowed.");

                if (select.Into != null)
                    throw new InvalidOperationException("SELECT INTO is not allowed.");

                // Extended validations
                ValidateQueryExpression(select.QueryExpression);
            }
        }

        /// <summary>
        /// Determines the type of query expression (simple SELECT, UNION/INTERSECT/EXCEPT, or parenthesized query) and 
        /// recursively validates it.
        /// </summary>
        /// <param name="expression">The <see cref="QueryExpression"/> to validate.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the query expression contains any disallowed constructs.
        /// </exception>
        private static void ValidateQueryExpression(QueryExpression expression)
        {
            switch (expression)
            {
                case QuerySpecification query                :
                    ValidateQuerySpecification(query);
                    break;

                case BinaryQueryExpression binary            :
                    ValidateQueryExpression(binary.FirstQueryExpression);
                    ValidateQueryExpression(binary.SecondQueryExpression);
                    break;

                case QueryParenthesisExpression parenthesized:
                    ValidateQueryExpression(parenthesized.QueryExpression);
                    break;

                default                                      :
                    throw new InvalidOperationException($"Unsupported query expression '{expression.GetType().Name}'.");
            }
        }

        /// <summary>
        /// Validates a single 'SELECT' statement by rejecting 'FOR XML/FOR JSON' and validating every table source in its 'FROM' clause.
        /// </summary>
        /// <param name="query">The <see cref="QuerySpecification"/> to validate.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the query specification contains any disallowed constructs.
        /// </exception>
        private static void ValidateQuerySpecification(QuerySpecification query)
        {
            // Reject 'FOR XML / FOR JSON'
            if (query.ForClause != null)
                throw new InvalidOperationException("FOR XML/FOR JSON is not allowed.");

            if (query.FromClause == null)
                return;

            foreach (var table in query.FromClause.TableReferences)
            {
                ValidateTableReference(table);
            }
        }

        /// <summary>
        /// Validates each table source (FROM/JOIN) using an allow-list: 'named tables', 'derived tables', and 
        /// 'qualified/parenthesized' joins are permitted (with recursive checks); all other 
        /// <see cref="TableReference"/> types are rejected, including 'table variables', 'OPENROWSET', 
        /// 'OPENQUERY', 'OPENXML', 'table-valued' functions, 'OPENJSON', and unrecognized 'ScriptDom' sources.
        /// </summary>
        /// <param name="table">The <see cref="TableReference"/> to validate.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the table reference is not an allowed type or contains nested disallowed constructs.
        /// </exception>
        private static void ValidateTableReference(TableReference table)
        {
            switch (table)
            {
                case NamedTableReference                        :
                    return;

                case QueryDerivedTable derived                  :
                    ValidateQueryExpression(derived.QueryExpression);
                    return;

                case QualifiedJoin join                         :
                    ValidateTableReference(join.FirstTableReference);
                    ValidateTableReference(join.SecondTableReference);
                    return;

                case JoinParenthesisTableReference parenthesized:
                    ValidateTableReference(parenthesized.Join);
                    return;

                case VariableTableReference                     :
                    throw new InvalidOperationException("Table variables are not allowed.");

                case OpenRowsetTableReference                   :
                    throw new InvalidOperationException("OPENROWSET is not allowed.");

                case OpenQueryTableReference                    :
                    throw new InvalidOperationException("OPENQUERY is not allowed.");

                case OpenXmlTableReference                      :
                    throw new InvalidOperationException("OPENXML is not allowed.");

                // Optional: clearer messages for common cases that would otherwise hit default
                case SchemaObjectFunctionTableReference         :
                    throw new InvalidOperationException("Table-valued functions are not allowed.");

                case OpenJsonTableReference                     :
                    throw new InvalidOperationException("OPENJSON is not allowed.");

                default                                         :
                    throw new InvalidOperationException(
                        $"Table source '{table.GetType().Name}' is not allowed.");
            }
        }
    }
}
