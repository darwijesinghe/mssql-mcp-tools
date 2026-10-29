using Mcp.Server.Models;
using Mcp.Server.Services;
using Mcp.Server.Services.Interfaces;
using Mcp.Test.Helpers;
using Microsoft.Extensions.Options;

namespace Mcp.Test
{
    [TestClass]
    [TestCategory("Integration")]
    public class DatabaseServiceIntegrationTests
    {
        /// <summary>
        /// Database service reference.
        /// </summary>
        private DatabaseService _dbService;

        /// <summary>
        /// Test context provided by the MSTest framework, used to 
        /// access test properties and configuration.
        /// </summary>
        public TestContext TestContext { get; set; }

        /// <summary>
        /// Initializes the test class by setting up the necessary dependencies for <see cref="DatabaseService"/>.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            var connectionString = TestContext.Properties["DbConnectionString"]?.ToString()
            ?? throw new InvalidOperationException("DbConnectionString is missing.");

            var options = Options.Create(new AppSettings
            {
                Database     = new Database { ConnectionString = connectionString },
                QueryOptions = new QueryOptions
                {
                    CommandTimeoutSeconds = 30,
                    MaxRows               = 100,
                    MaxCellLength         = 1000
                }
            });

            ISqlExecutor sqlExecutor = new SqlExecutor(options);
            _dbService = new DatabaseService(sqlExecutor, options);
        }

        /// <summary>
        /// Tests that the <see cref="DatabaseService.ValidateConnectionAsync"/> method returns true for a valid database connection.
        /// </summary>
        [TestMethod]
        public async Task Test_ValidateConnectionAsync_ReturnsTrue_ForValidConnection()
        {
            // Act
            var result = await _dbService.ValidateConnectionAsync(CancellationToken.None);

            // Assert
            Assert.IsTrue(result);
        }

        /// <summary>
        /// Tests that the <see cref="DatabaseService.GetTablesAsync"/> method returns a list of tables from the 
        /// database, and verifies that the expected table is present in the result.
        /// </summary>
        [TestMethod]
        public async Task Test_GetTablesAsync_ReturnsTables()
        {
            // Arrange
            string key       = "TABLENAME";
            string tableName = "Employees";

            // Act
            var result = await _dbService.GetTablesAsync(CancellationToken.None);

            // Assert
            Assert.IsNotEmpty(result.Rows);
            Assert.IsGreaterThan(0, result.RowCount);
            Assert.IsTrue(result.Rows.Any(t => t.GetValue(key)?.ToString() == tableName));
        }

        /// <summary>
        /// Tests that the <see cref="DatabaseService.GetViewsAsync"/> method returns a list of views from the 
        /// database, and verifies that the expected view is present in the result.
        /// </summary>
        [TestMethod]
        public async Task Test_GetViewsAsync_ReturnsViews()
        {
            // Arrange
            string key      = "VIEWNAME";
            string viewName = "vwEmployees";

            // Act
            var result = await _dbService.GetViewsAsync(CancellationToken.None);

            // Assert
            Assert.IsNotEmpty(result.Rows);
            Assert.IsGreaterThan(0, result.RowCount);
            Assert.IsTrue(result.Rows.Any(t => t.GetValue(key)?.ToString() == viewName));
        }

        /// <summary>
        /// Tests that the <see cref="DatabaseService.GetProceduresAsync"/> method returns a list of procedures from the 
        /// database, and verifies that the expected procedure is present in the result.
        /// </summary>
        [TestMethod]
        public async Task Test_GetProceduresAsync_ReturnsProcedures()
        {
            // Arrange
            string key           = "PROCEDURENAME";
            string procedureName = "GetEmployeeCount";

            // Act
            var result = await _dbService.GetProceduresAsync(CancellationToken.None);

            // Assert
            Assert.IsNotEmpty(result.Rows);
            Assert.IsGreaterThan(0, result.RowCount);
            Assert.IsTrue(result.Rows.Any(t => t.GetValue(key)?.ToString() == procedureName));
        }

        /// <summary>
        /// Tests that the <see cref="DatabaseService.GetTriggersAsync"/> method returns a list of triggers from the 
        /// database, and verifies that the expected trigger is present in the result.
        /// </summary>
        [TestMethod]
        public async Task Test_GetTriggersAsync_ReturnsTriggers()
        {
            // Arrange
            string key         = "TRIGGERNAME";
            string triggerName = "trg_Employee_Insert";

            // Act
            var result = await _dbService.GetTriggersAsync(CancellationToken.None);

            // Assert
            Assert.IsNotEmpty(result.Rows);
            Assert.IsGreaterThan(0, result.RowCount);
            Assert.IsTrue(result.Rows.Any(t => t.GetValue(key)?.ToString() == triggerName));
        }

        /// <summary>
        /// Tests that the <see cref="DatabaseService.GetFunctionsAsync"/> method returns a list of functions from the 
        /// database, and verifies that the expected function is present in the result.
        /// </summary>
        [TestMethod]
        public async Task Test_GetFunctionsAsync_ReturnsFunctions()
        {
            // Arrange
            string key          = "FUNCTIONNAME";
            string functionName = "GetAnnualSalary";

            // Act
            var result = await _dbService.GetFunctionsAsync(CancellationToken.None);

            // Assert
            Assert.IsNotEmpty(result.Rows);
            Assert.IsGreaterThan(0, result.RowCount);
            Assert.IsTrue(result.Rows.Any(t => t.GetValue(key)?.ToString() == functionName));
        }

        #region Describe table

        /// <summary>
        /// Tests that the <see cref="DatabaseService.DescribeTableAsync"/> method does not return any rows when the 
        /// specified table does not exist in the database. 
        /// </summary>
        [TestMethod]
        public async Task Test_DescribeTableAsync_ShouldNotReturnRows_WhenTableNotExists()
        {
            // Arrange
            string tableName = "dbo.orders";

            // Act
            var result = await _dbService.DescribeTableAsync(tableName, CancellationToken.None);

            // Assert
            Assert.IsEmpty(result.Rows);
        }

        /// <summary>
        /// Tests that the <see cref="DatabaseService.DescribeTableAsync"/> method returns rows when the 
        /// specified table exists in the database. 
        /// </summary>
        [TestMethod]
        public async Task Test_DescribeTableAsync_ShouldReturnRows_WhenTableExists()
        {
            // Arrange
            string tableName    = "dbo.Employees";
            string key          = "COLUMN_NAME";
            string tableColName = "Id";

            // Act
            var result = await _dbService.DescribeTableAsync(tableName, CancellationToken.None);

            // Assert
            Assert.IsNotEmpty(result.Rows);
            Assert.IsTrue(result.Rows.Any(t => t.GetValue(key)?.ToString() == tableColName));
        }

        /// <summary>
        /// Tests that the <see cref="DatabaseService.DescribeTableAsync"/> method returns rows when the specified table 
        /// exists in the database and is provided with a schema-qualified name.
        /// </summary>
        [TestMethod]
        public async Task Test_DescribeTableAsync_ShouldSupport_SchemaQualifiedName()
        {
            // Arrange
            string tableName = "dbo.Employees";

            // Act
            var result = await _dbService.DescribeTableAsync(tableName, CancellationToken.None);

            // Assert
            Assert.IsNotEmpty(result.Rows);
        }

        /// <summary>
        /// Tests that the <see cref="DatabaseService.DescribeTableAsync"/> method returns an empty result when the 
        /// specified schema does not exist in the database.
        /// </summary>
        [TestMethod]
        public async Task Test_DescribeTableAsync_ShouldReturnEmpty_WhenSchemaDoesNotExist()
        {
            // Arrange
            string tableName = "abc.Employees";

            // Act
            var result = await _dbService.DescribeTableAsync(tableName, CancellationToken.None);


            // Assert
            Assert.IsEmpty(result.Rows);
        }

        /// <summary>
        /// Tests that the <see cref="DatabaseService.DescribeTableAsync"/> method throws 
        /// an <see cref="OperationCanceledException"/> when the operation is cancelled via a <see cref="CancellationToken"/>.
        /// </summary>
        [TestMethod]
        public async Task Test_DescribeTableAsync_ShouldThrow_WhenCancelled()
        {
            // Arrange
            string tableName = "dbo.Employees";
            using var cts = new CancellationTokenSource();

            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => _dbService.DescribeTableAsync(tableName, cts.Token));
        }

        /// <summary>
        /// Tests that the <see cref="DatabaseService.DescribeTableAsync"/> method returns the expected column 
        /// names in the result when describing a table.
        /// </summary>
        [TestMethod]
        public async Task Test_DescribeTableAsync_ShouldReturnExpectedColumns()
        {
            // Arrange
            string tableName = "dbo.Employees";

            // Act
            var result = await _dbService.DescribeTableAsync(tableName, CancellationToken.None);

            // Assert
            var first = result.Rows.First();

            Assert.IsTrue(first.ContainsKey("COLUMN_NAME"));
            Assert.IsTrue(first.ContainsKey("DATA_TYPE"));
            Assert.IsTrue(first.ContainsKey("IS_NULLABLE"));
            Assert.IsTrue(first.ContainsKey("CHARACTER_MAXIMUM_LENGTH"));
        }

        #endregion

        #region Get object definition

        /// <summary>
        /// Tests that the <see cref="DatabaseService.GetObjectDefinitionAsync"/> method returns error text when the 
        /// specified database object does not exist.
        /// </summary>
        [TestMethod]
        public async Task Test_GetObjectDefinitionAsync_ShouldReturn_NoDefinitionText()
        {
            // Arrange
            string viewName = "dbo.nonexistent_view";

            // Act
            var result = await _dbService.GetObjectDefinitionAsync(viewName, CancellationToken.None);

            // Assert
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.Text));
            Assert.Contains("No definition found", result.Text);
        }

        /// <summary>
        /// Tests that the <see cref="DatabaseService.GetObjectDefinitionAsync"/> method returns the expected 
        /// definition text for various database objects.
        /// </summary>
        /// <param name="objectName">The name of the database object.</param>
        /// <param name="expectedDefinition">The expected definition text for the database object.</param>
        [TestMethod]
        [DataRow("dbo.vwEmployees"        , "CREATE VIEW vwEmployees")]
        [DataRow("dbo.GetEmployeeCount"   , "CREATE PROCEDURE GetEmployeeCount")]
        [DataRow("dbo.GetAnnualSalary"    , "CREATE FUNCTION GetAnnualSalary")]
        [DataRow("dbo.GetEmployeesByDept" , "CREATE FUNCTION GetEmployeesByDept")]
        [DataRow("dbo.trg_Employee_Insert", "CREATE TRIGGER trg_Employee_Insert")]
        public async Task Test_GetObjectDefinitionAsync_ShouldReturn_Definitions(string objectName, string expectedDefinition)
        {
            // Act
            var result = await _dbService.GetObjectDefinitionAsync(objectName, CancellationToken.None);

            // Assert
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.Text));
            Assert.Contains(expectedDefinition, result.Text);
        }

        #endregion

        #region Search object definition

        /// <summary>
        /// Tests that the <see cref="DatabaseService.SearchObjectDefinitionsAsync"/> method does not return rows when 
        /// no object definitions match the search text.
        /// </summary>
        [TestMethod]
        public async Task Test_SearchObjectDefinitionsAsync_ShouldNotReturnRows_WhenNoSearchResult()
        {
            // Arrange
            string viewName = "dbo.nonexistent_view";

            // Act
            var result = await _dbService.SearchObjectDefinitionsAsync(viewName, CancellationToken.None);

            // Assert
            Assert.IsEmpty(result.Rows);
        }

        /// <summary>
        /// Tests that the <see cref="DatabaseService.SearchObjectDefinitionsAsync"/> method returns rows when the 
        /// search text matches existing object definitions in the database.
        /// </summary>
        /// <param name="text">The search text to match against object definitions.</param>
        [TestMethod]
        [DataRow("vwEmployees")]
        [DataRow("trg_Employee_Insert")]
        [DataRow("GetEmployeesByDept")]
        [DataRow("GetAnnualSalary")]
        [DataRow("GetEmployeeCount")]
        public async Task Test_SearchObjectDefinitionsAsync_ShouldReturnRows_WhenSearchTextMatches(string text)
        {
            // Arrange
            string key = "NAME";

            // Act
            var result = await _dbService.SearchObjectDefinitionsAsync(text, CancellationToken.None);

            // Assert
            Assert.IsNotEmpty(result.Rows);
            Assert.IsTrue(result.Rows.Any(t => t.GetValue(key)?.ToString() == text));
        }

        #endregion

        #region Get object reference

        /// <summary>
        /// Tests that the <see cref="DatabaseService.GetObjectReferencesAsync"/> method does not return rows when 
        /// no object references exist for the specified object.
        /// </summary>
        [TestMethod]
        public async Task Test_GetObjectReferencesAsync_ShouldNotReturnRows_WhenObjectReferenceNotExists()
        {
            // Arrange
            string tableName = "dbo.NonExistentTable";

            // Act
            var result = await _dbService.GetObjectReferencesAsync(tableName, CancellationToken.None);

            // Assert
            Assert.IsEmpty(result.Rows);
        }

        /// <summary>
        /// Tests that the <see cref="DatabaseService.GetObjectReferencesAsync"/> method returns rows when object 
        /// references exist for the specified object.
        /// </summary>
        [TestMethod]
        public async Task Test_GetObjectReferencesAsync_ShouldReturnRows_WhenObjectReferenceExists()
        {
            // Arrange
            string tableName         = "dbo.Employees";
            string key               = "REFERENCING_OBJECT_NAME";
            string expectedReference = "vwEmployees";

            // Act
            var result = await _dbService.GetObjectReferencesAsync(tableName, CancellationToken.None);

            // Assert
            Assert.IsNotEmpty(result.Rows);
            Assert.IsTrue(result.Rows.Any(t => t.GetValue(key)?.ToString() == expectedReference));
        }

        #endregion

        #region Execute query

        /// <summary>
        /// Tests that the <see cref="DatabaseService.ExecuteReadQueryAsync"/> method throws an <see cref="InvalidOperationException"/>
        /// when the query is not valid.
        /// </summary>
        /// <param name="query">The query to execute.</param>
        [TestMethod]
        [DataRow("")]
        [DataRow("SELECT FROM")]                                                                                                       // Invalid syntax
        [DataRow("SELECT * INTO BackupEmployees FROM Employees")]                                                                      // SELECT INTO
        [DataRow("DELETE FROM Customers")]                                                                                             // DELETE
        [DataRow("UPDATE Customers SET Name = 'Test'")]                                                                                // UPDATE
        [DataRow("INSERT INTO Customers(Name) VALUES('Test')")]                                                                        // INSERT
        [DataRow("MERGE Customers AS t USING Customers AS s ON 1=1 WHEN MATCHED THEN UPDATE SET Name='Test'")]                         // MERGE
        [DataRow("EXEC sp_helpdb")]                                                                                                    // EXEC
        [DataRow("DROP TABLE Customers")]                                                                                              // DROP
        [DataRow("ALTER TABLE Customers ADD Age INT")]                                                                                 // ALTER
        [DataRow("CREATE TABLE Test(Id INT)")]                                                                                         // CREATE
        [DataRow("TRUNCATE TABLE Customers")]                                                                                          // TRUNCATE
        [DataRow("SELECT * FROM Employees FOR XML AUTO")]                                                                              // FOR XML
        [DataRow("SELECT * FROM Employees FOR JSON AUTO")]                                                                             // FOR JSON
        [DataRow("SELECT * FROM @Employees")]                                                                                          // Table variable
        [DataRow("SELECT * FROM OPENROWSET('SQLNCLI','Server=.;Trusted_Connection=yes;','SELECT 1')")]                                 // OPENROWSET
        [DataRow("SELECT * FROM OPENQUERY(MyServer,'SELECT * FROM Employees')")]                                                       // OPENQUERY
        [DataRow("SELECT * FROM OPENXML(@idoc,'/ROOT/Row',2)")]                                                                        // OPENXML
        [DataRow("SELECT * FROM (SELECT * FROM Employees FOR XML AUTO) x")]                                                            // DERIVED TABLE with FOR XML
        [DataRow("SELECT * FROM Employees UNION SELECT * FROM OPENROWSET('SQLNCLI', 'Server=.;Trusted_Connection=yes;', 'SELECT 1')")] // UNION with OPENROWSET
        [DataRow("SELECT * FROM (SELECT * FROM OPENQUERY(MyServer, 'SELECT * FROM Employees')) q")]                                    // DERIVED TABLE with OPENQUERY
        public async Task Test_ExecuteReadQueryAsync_ShouldThrow_InvalidOperationException_WhenQueryIsNotValid(string query)
        {
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _dbService.ExecuteReadQueryAsync(query, CancellationToken.None));
        }

        /// <summary>
        /// Tests that the <see cref="DatabaseService.ExecuteReadQueryAsync"/> method throws an <see cref="InvalidOperationException"/>
        /// when the query is not valid.
        /// </summary>
        /// <param name="query">The query to execute.</param>
        [TestMethod]
        [DataRow("SELECT * FROM Employees")]
        [DataRow("SELECT Name FROM Employees")]
        [DataRow("SELECT TOP 10 * FROM Employees")]
        [DataRow("SELECT DISTINCT Name FROM Employees")]
        [DataRow("SELECT COUNT(*) FROM Employees")]
        [DataRow("SELECT Name FROM Employees WHERE Id = 1")]
        [DataRow("SELECT Name FROM Employees ORDER BY Name")]
        [DataRow("SELECT DepartmentId, COUNT(*) FROM Employees GROUP BY DepartmentId")]
        [DataRow("SELECT e.Name, d.Name FROM Employees e JOIN Departments d ON e.DepartmentId = d.Id")]
        [DataRow("SELECT * FROM (SELECT * FROM Employees) e")]
        [DataRow("SELECT Name FROM Employees UNION SELECT Name FROM Customers")]
        [DataRow("SELECT Name FROM Employees UNION ALL SELECT Name FROM Customers")]
        [DataRow("SELECT Name FROM Employees INTERSECT SELECT Name FROM Customers")]
        [DataRow("SELECT Name FROM Employees EXCEPT SELECT Name FROM Customers")]
        [DataRow("WITH Emp AS (SELECT * FROM Employees) SELECT * FROM Emp")]
        [DataRow("SELECT * FROM (SELECT Name FROM Employees UNION SELECT Name FROM Customers) t")]
        public async Task Test_ExecuteReadQueryAsync_ShouldNotThrow_InvalidOperationException_WhenQueryIsValid(string query)
        {
            // Act
            await _dbService.ExecuteReadQueryAsync(query, CancellationToken.None);

            // Assert
            // Success = no exception thrown
        }

        /// <summary>
        /// Tests that the <see cref="DatabaseService.ExecuteReadQueryAsync"/> method returns rows for various valid SQL queries.
        /// </summary>
        /// <param name="query">The query to execute.</param>
        [TestMethod]
        [DataRow("SELECT * FROM Employees")]
        [DataRow("SELECT Name FROM Employees")]
        [DataRow("SELECT TOP 10 * FROM Employees")]
        [DataRow("SELECT DISTINCT Name FROM Employees")]
        [DataRow("SELECT COUNT(*) FROM Employees")]
        [DataRow("SELECT Name FROM Employees WHERE Id = 1")]
        [DataRow("SELECT Name FROM Employees ORDER BY Name")]
        [DataRow("SELECT DepartmentId, COUNT(*) FROM Employees GROUP BY DepartmentId")]
        [DataRow("SELECT e.Name, d.Name FROM Employees e JOIN Departments d ON e.DepartmentId = d.Id")]
        [DataRow("SELECT * FROM (SELECT * FROM Employees) e")]
        [DataRow("SELECT Name FROM Employees UNION SELECT Name FROM Customers")]
        [DataRow("SELECT Name FROM Employees UNION ALL SELECT Name FROM Customers")]
        [DataRow("SELECT Name FROM Employees INTERSECT SELECT Name FROM Customers")]
        [DataRow("SELECT Name FROM Employees EXCEPT SELECT Name FROM Customers")]
        [DataRow("WITH Emp AS (SELECT * FROM Employees) SELECT * FROM Emp")]
        [DataRow("SELECT * FROM (SELECT Name FROM Employees UNION SELECT Name FROM Customers) t")]
        public async Task Test_ExecuteReadQueryAsync_ShouldReturnRows_For_ValidQueries(string query)
        {
            // Act
            var result = await _dbService.ExecuteReadQueryAsync(query, CancellationToken.None);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result.Rows);
        }

        #endregion
    }
}
