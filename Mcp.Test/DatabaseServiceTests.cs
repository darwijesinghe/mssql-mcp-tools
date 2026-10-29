using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Moq;
using Mcp.Server.Models;
using Mcp.Server.Services;
using Mcp.Server.Services.Interfaces;

namespace Mcp.Test
{
    /// <summary>
    /// Unit tests for <see cref="DatabaseService"/>.
    /// </summary>
    [TestClass]
    public class DatabaseServiceTests
    {
        /// <summary>
        /// Tests that the <see cref="DatabaseService"/> constructor throws an <see cref="InvalidOperationException"/> when 
        /// the connection string is empty.
        /// </summary>
        [TestMethod]
        public void Test_DatabaseService_Constructor_Throws_InvalidOperationException()
        {
            // Arrange
            var options = Options.Create(new AppSettings
            {
                Database     = new Database { ConnectionString = "" },
                QueryOptions = new QueryOptions { CommandTimeoutSeconds = 30 }
            });

            ISqlExecutor sqlExecutor = new SqlExecutor(options);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => new DatabaseService(sqlExecutor, options));
        }

        /// <summary>
        /// Tests that the <see cref="DatabaseService.DescribeTableAsync"/> method correctly captures the table name and schema.
        /// </summary>
        [TestMethod]
        public async Task Test_DescribeTableAsync_CorrectlyCaptures_TableNameAndSchema() 
        {
            // Arrange
            var options = Options.Create(new AppSettings
            {
                Database     = new Database { ConnectionString = "test" },
                QueryOptions = new QueryOptions { CommandTimeoutSeconds = 30 }
            });

            var executor = new Mock<ISqlExecutor>();
            executor
                .Setup(x => x.ExecuteMultiColumnRowsAsync(
                    It.IsAny<string>(),
                    It.IsAny<SqlParameter[]>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new QueryResult());

            var service = new DatabaseService(executor.Object, options);

            // Act
            await service.DescribeTableAsync("dbo.Orders", CancellationToken.None);

            // Assert
            executor
                .Verify(x => x.ExecuteMultiColumnRowsAsync(
                   It.Is<string>(sql =>
                       sql.Contains("INFORMATION_SCHEMA.COLUMNS")),
                   It.Is<SqlParameter[]>(p =>
                       (string)p.Single(x => x.ParameterName == "@tableName").Value == "Orders" &&
                       (string)p.Single(x => x.ParameterName == "@tableSchema").Value == "dbo"),
                   It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>
        /// Tests that the <see cref="DatabaseService.GetObjectDefinitionAsync"/> method correctly captures the object name and schema.
        /// </summary>
        [TestMethod]
        public async Task Test_GetObjectDefinitionAsync_CorrectlyCaptures_ObjectNameAndSchema()
        {
            // Arrange
            var options = Options.Create(new AppSettings
            {
                Database     = new Database { ConnectionString = "test" },
                QueryOptions = new QueryOptions { CommandTimeoutSeconds = 30 }
            });
            var executor = new Mock<ISqlExecutor>();
            executor
                .Setup(x => x.ExecuteScalarStringAsync(
                    It.IsAny<string>(),
                    It.IsAny<SqlParameter[]>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync("123");

            var service = new DatabaseService(executor.Object, options);

            // Act
            await service.GetObjectDefinitionAsync("dbo.sp_GetOrders", CancellationToken.None);

            // Assert
            executor
                .Verify(x => x.ExecuteScalarStringAsync(
                   It.Is<string>(sql =>
                       sql.Contains("OBJECT_DEFINITION")),
                   It.Is<SqlParameter[]>(p =>
                       (string)p.Single(x => x.ParameterName == "@qualifiedName").Value == "dbo.sp_GetOrders" &&
                       (string)p.Single(x => x.ParameterName == "@objectName").Value == "sp_GetOrders" &&
                       (string)p.Single(x => x.ParameterName == "@schema").Value == "dbo"),
                   It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>
        /// Tests that the <see cref="DatabaseService.GetObjectReferencesAsync"/> method correctly captures the object name and reference schema.
        /// </summary>
        [TestMethod]
        public async Task Test_GetObjectReferencesAsync_CorrectlyCaptures_ObjectNameAndReferenceSchema()
        {
            // Arrange
            var options = Options.Create(new AppSettings
            {
                Database     = new Database { ConnectionString = "test" },
                QueryOptions = new QueryOptions { CommandTimeoutSeconds = 30 }
            });

            var executor = new Mock<ISqlExecutor>();
            executor
                .Setup(x => x.ExecuteMultiColumnRowsAsync(
                    It.IsAny<string>(),
                    It.IsAny<SqlParameter[]>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new QueryResult());

            var service = new DatabaseService(executor.Object, options);

            // Act
            await service.GetObjectReferencesAsync("dbo.Orders", CancellationToken.None);

            // Assert
            executor
                .Verify(x => x.ExecuteMultiColumnRowsAsync(
                   It.Is<string>(sql =>
                       sql.Contains("OBJECT_SCHEMA_NAME(REFERENCING_ID)")),
                   It.Is<SqlParameter[]>(p =>
                       (string)p.Single(x => x.ParameterName == "@ObjectName").Value == "Orders" &&
                       (string)p.Single(x => x.ParameterName == "@referencedSchema").Value == "dbo"),
                   It.IsAny<CancellationToken>()), Times.Once);
        }

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
            // Arrange
            var options = Options.Create(new AppSettings
            {
                Database     = new Database { ConnectionString = "test" },
                QueryOptions = new QueryOptions { CommandTimeoutSeconds = 30 }
            });

            var executor = new Mock<ISqlExecutor>();
            executor
                .Setup(x => x.ExecuteMultiColumnRowsAsync(
                    It.IsAny<string>(),
                    It.IsAny<SqlParameter[]>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new QueryResult());

            var service = new DatabaseService(executor.Object, options);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ExecuteReadQueryAsync(query, CancellationToken.None));
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
            // Arrange
            var options = Options.Create(new AppSettings
            {
                Database     = new Database { ConnectionString = "test" },
                QueryOptions = new QueryOptions { CommandTimeoutSeconds = 30 }
            });

            var executor = new Mock<ISqlExecutor>();
            executor
                .Setup(x => x.ExecuteMultiColumnRowsAsync(
                    It.IsAny<string>(),
                    It.IsAny<SqlParameter[]>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new QueryResult());

            var service = new DatabaseService(executor.Object, options);

            // Act
            await service.ExecuteReadQueryAsync(query, CancellationToken.None);

            // Assert
            // Success = no exception thrown
        }
    }
}