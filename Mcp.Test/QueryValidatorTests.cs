using Mcp.Server.Utils;

namespace Mcp.Test
{
    /// <summary>
    /// Unit tests for <see cref="QueryValidator"/>.
    /// </summary>
    [TestClass]
    public class QueryValidatorTests
    {
        /// <summary>
        /// Tests that <see cref="QueryValidator.Validate"/> allows a simple 'SELECT' statement.
        /// </summary>
        [TestMethod]
        public void Test_Validate_Allows_SimpleSelect()
        {
            QueryValidator.Validate("SELECT Id, Name FROM dbo.Customers");
        }

        /// <summary>
        /// Tests that <see cref="QueryValidator.Validate"/> allows a 'SELECT' statement with a 'JOIN'.
        /// </summary>
        [TestMethod]
        public void Test_Validate_Allows_SelectWithJoin()
        {
            QueryValidator.Validate(@"
                SELECT c.Id, o.OrderId
                FROM dbo.Customers c
                INNER JOIN dbo.Orders o ON o.CustomerId = c.Id");
        }

        /// <summary>
        /// Tests that <see cref="QueryValidator.Validate"/> allows a 'SELECT' statement that uses 'UNION ALL'.
        /// </summary>
        [TestMethod]
        public void Test_Validate_Allows_Union()
        {
            QueryValidator.Validate(@"
                SELECT Id FROM dbo.A
                UNION ALL
                SELECT Id FROM dbo.B");
        }

        /// <summary>
        /// Tests that <see cref="QueryValidator.Validate"/> allows a 'SELECT' statement that uses a derived table.
        /// </summary>
        [TestMethod]
        public void Test_Validate_Allows_DerivedTable()
        {
            QueryValidator.Validate(@"
                SELECT x.Id
                FROM (SELECT Id FROM dbo.Customers) AS x");
        }

        /// <summary>
        /// Tests that <see cref="QueryValidator.Validate"/> allows a 'SELECT' statement without a 'FROM' clause.
        /// </summary>
        [TestMethod]
        public void Test_Validate_Allows_SelectWithoutFrom()
        {
            QueryValidator.Validate("SELECT 1 AS Value");
        }

        /// <summary>
        /// Tests that <see cref="QueryValidator.Validate"/> throws an <see cref="InvalidOperationException"/> when 
        /// the SQL string is empty.
        /// </summary>
        [TestMethod]
        public void Test_Validate_Throws_WhenSqlIsEmpty()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => QueryValidator.Validate(""));
            StringAssert.Contains(ex.Message, "No statements found.");
        }

        /// <summary>
        /// Tests that <see cref="QueryValidator.Validate"/> throws an <see cref="InvalidOperationException"/> when 
        /// the SQL cannot be parsed.
        /// </summary>
        [TestMethod]
        public void Test_Validate_Throws_OnParseError()
        {
            Assert.Throws<InvalidOperationException>(() => QueryValidator.Validate("SELECT FROM"));
        }

        /// <summary>
        /// Tests that <see cref="QueryValidator.Validate"/> throws an <see cref="InvalidOperationException"/> for 
        /// an 'INSERT' statement.
        /// </summary>
        [TestMethod]
        public void Test_Validate_Throws_OnInsert()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => QueryValidator.Validate("INSERT INTO dbo.Customers (Name) VALUES ('x')"));
            StringAssert.Contains(ex.Message, "is not allowed.");
        }

        /// <summary>
        /// Tests that <see cref="QueryValidator.Validate"/> throws an <see cref="InvalidOperationException"/> for 
        /// an 'UPDATE' statement.
        /// </summary>
        [TestMethod]
        public void Test_Validate_Throws_OnUpdate()
        {
            Assert.Throws<InvalidOperationException>(
                () => QueryValidator.Validate("UPDATE dbo.Customers SET Name = 'x'"));
        }

        /// <summary>
        /// Tests that <see cref="QueryValidator.Validate"/> throws an <see cref="InvalidOperationException"/> for 
        /// a 'DELETE' statement.
        /// </summary>
        [TestMethod]
        public void Test_Validate_Throws_OnDelete()
        {
            Assert.Throws<InvalidOperationException>(
                () => QueryValidator.Validate("DELETE FROM dbo.Customers"));
        }

        /// <summary>
        /// Tests that <see cref="QueryValidator.Validate"/> throws an <see cref="InvalidOperationException"/> for 
        /// a 'DROP' statement.
        /// </summary>
        [TestMethod]
        public void Test_Validate_Throws_OnDrop()
        {
            Assert.Throws<InvalidOperationException>(
                () => QueryValidator.Validate("DROP TABLE dbo.Customers"));
        }

        /// <summary>
        /// Tests that <see cref="QueryValidator.Validate"/> throws an <see cref="InvalidOperationException"/> for 
        /// a 'SELECT INTO' statement.
        /// </summary>
        [TestMethod]
        public void Test_Validate_Throws_OnSelectInto()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => QueryValidator.Validate("SELECT Id INTO #tmp FROM dbo.Customers"));
            StringAssert.Contains(ex.Message, "SELECT INTO is not allowed.");
        }

        /// <summary>
        /// Tests that <see cref="QueryValidator.Validate"/> throws an <see cref="InvalidOperationException"/> for 
        /// a 'SELECT' that uses 'FOR XML'.
        /// </summary>
        [TestMethod]
        public void Test_Validate_Throws_OnForXml()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => QueryValidator.Validate("SELECT Id FROM dbo.Customers FOR XML AUTO"));
            StringAssert.Contains(ex.Message, "FOR XML/FOR JSON is not allowed.");
        }

        /// <summary>
        /// Tests that <see cref="QueryValidator.Validate"/> throws an <see cref="InvalidOperationException"/> for 
        /// a 'SELECT' that uses 'FOR JSON'.
        /// </summary>
        [TestMethod]
        public void Test_Validate_Throws_OnForJson()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => QueryValidator.Validate("SELECT Id FROM dbo.Customers FOR JSON PATH"));
            StringAssert.Contains(ex.Message, "FOR XML/FOR JSON is not allowed.");
        }

        /// <summary>
        /// Tests that <see cref="QueryValidator.Validate"/> throws an <see cref="InvalidOperationException"/> when 
        /// a table variable is referenced.
        /// </summary>
        [TestMethod]
        public void Test_Validate_Throws_OnTableVariable()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => QueryValidator.Validate("SELECT * FROM @CustomerTable"));
            StringAssert.Contains(ex.Message, "Table variables are not allowed.");
        }

        /// <summary>
        /// Tests that <see cref="QueryValidator.Validate"/> throws an <see cref="InvalidOperationException"/> for 
        /// an 'OPENROWSET' table source.
        /// </summary>
        [TestMethod]
        public void Test_Validate_Throws_OnOpenRowset()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => QueryValidator.Validate(
                "SELECT * FROM OPENROWSET('SQLNCLI', 'Server=.;Trusted_Connection=yes;', 'SELECT 1')"));
            StringAssert.Contains(ex.Message, "OPENROWSET is not allowed.");
        }

        /// <summary>
        /// Tests that <see cref="QueryValidator.Validate"/> throws an <see cref="InvalidOperationException"/> for 
        /// an 'OPENQUERY' table source.
        /// </summary>
        [TestMethod]
        public void Test_Validate_Throws_OnOpenQuery()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => QueryValidator.Validate("SELECT * FROM OPENQUERY(LinkedServer, 'SELECT 1')"));
            StringAssert.Contains(ex.Message, "OPENQUERY is not allowed.");
        }

        /// <summary>
        /// Tests that <see cref="QueryValidator.Validate"/> throws an <see cref="InvalidOperationException"/> for 
        /// an 'OPENXML' table source.
        /// </summary>
        [TestMethod]
        public void Test_Validate_Throws_OnOpenXml()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => QueryValidator.Validate("SELECT * FROM OPENXML(@id, '/root')"));
            StringAssert.Contains(ex.Message, "OPENXML is not allowed.");
        }

        /// <summary>
        /// Tests that <see cref="QueryValidator.Validate"/> throws an <see cref="InvalidOperationException"/> for 
        /// a 'table-valued' function in the 'FROM' clause.
        /// </summary>
        [TestMethod]
        public void Test_Validate_Throws_OnTableValuedFunction()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => QueryValidator.Validate("SELECT * FROM dbo.MyTvf(1)"));
            StringAssert.Contains(ex.Message, "Table-valued functions are not allowed.");
        }

        /// <summary>
        /// Tests that <see cref="QueryValidator.Validate"/> throws an <see cref="InvalidOperationException"/> for 
        /// an 'OPENJSON' table source.
        /// </summary>
        [TestMethod]
        public void Test_Validate_Throws_OnOpenJson()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => QueryValidator.Validate("SELECT * FROM OPENJSON('{\"a\":1}')"));
            StringAssert.Contains(ex.Message, "OPENJSON is not allowed.");
        }

        /// <summary>
        /// Tests that <see cref="QueryValidator.Validate"/> throws an <see cref="InvalidOperationException"/> for 
        /// unrecognized table sources such as 'STRING_SPLIT' (fail-closed allow-list).
        /// </summary>
        [TestMethod]
        public void Test_Validate_Throws_OnUnrecognizedTableSource_StringSplit()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => QueryValidator.Validate("SELECT * FROM STRING_SPLIT('a,b', ',')"));
            StringAssert.Contains(ex.Message, "is not allowed.");
        }

        /// <summary>
        /// Tests that <see cref="QueryValidator.Validate"/> throws an <see cref="InvalidOperationException"/> when 
        /// a 'table-valued' function appears on the right side of 'CROSS APPLY'.
        /// </summary>
        [TestMethod]
        public void Test_Validate_Throws_OnCrossApplyWithTableValuedFunction()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => QueryValidator.Validate(@"
                SELECT c.Id
                FROM dbo.Customers c
                CROSS APPLY dbo.MyTvf(c.Id)"));
            StringAssert.Contains(ex.Message, "is not allowed.");
        }
    }
}
