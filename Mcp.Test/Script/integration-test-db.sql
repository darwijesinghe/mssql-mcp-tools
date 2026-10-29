/*
  Integration test database for MssqlMcpTools.

  Creates mcp_test with the minimum objects referenced by
  DatabaseServiceIntegrationTests. Run against a local MSSQL Server instance
  (default file paths — no machine-specific FILENAME).

  After creating the database, point Mcp.Test/.runsettings
  DbConnectionString at Database=mcp_test.
*/

IF DB_ID(N'mcp_test') IS NOT NULL
BEGIN
    ALTER DATABASE [mcp_test] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [mcp_test];
END
GO

CREATE DATABASE [mcp_test];
GO

USE [mcp_test];
GO

/* ---- Tables ---- */

CREATE TABLE dbo.Departments
(
    Id   INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Name NVARCHAR(100)     NOT NULL
);
GO

CREATE TABLE dbo.Customers
(
    Id      INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Name    NVARCHAR(100)     NOT NULL,
    Email   NVARCHAR(255)     NOT NULL UNIQUE,
    Country NVARCHAR(100)     NULL
);
GO

CREATE TABLE dbo.Employees
(
    Id           INT            NOT NULL PRIMARY KEY,
    Name         NVARCHAR(100)  NULL,
    Salary       DECIMAL(10, 2) NULL,
    DepartmentId INT            NULL,
    Department   NVARCHAR(50)   NULL
);
GO

CREATE TABLE dbo.EmployeeAudit
(
    AuditId    INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    EmployeeId INT               NULL,
    Action     NVARCHAR(20)      NULL,
    ActionDate DATETIME          NULL CONSTRAINT DF_EmployeeAudit_ActionDate DEFAULT (GETDATE())
);
GO

/* ---- Views ---- */

CREATE VIEW dbo.vwEmployees
AS
SELECT Id, Name, Department
FROM dbo.Employees;
GO

/* ---- Functions ---- */

CREATE FUNCTION dbo.GetAnnualSalary
(
    @MonthlySalary DECIMAL(10, 2)
)
RETURNS DECIMAL(10, 2)
AS
BEGIN
    RETURN @MonthlySalary * 12;
END;
GO

CREATE FUNCTION dbo.GetEmployeesByDept
(
    @Department NVARCHAR(50)
)
RETURNS TABLE
AS
RETURN
(
    SELECT Id, Name, Salary, DepartmentId, Department
    FROM dbo.Employees
    WHERE Department = @Department
);
GO

/* ---- Procedures ---- */

CREATE PROCEDURE dbo.GetEmployeeCount
(
    @Department NVARCHAR(50),
    @Count      INT OUTPUT
)
AS
BEGIN
    SELECT @Count = COUNT(*)
    FROM dbo.Employees
    WHERE Department = @Department;
END;
GO

/* ---- Triggers ---- */

CREATE TRIGGER dbo.trg_Employee_Insert
ON dbo.Employees
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.EmployeeAudit (EmployeeId, Action)
    SELECT Id, N'INSERT'
    FROM inserted;
END;
GO

/* ---- Seed data (enough for SELECT / JOIN / UNION tests) ---- */

INSERT INTO dbo.Departments (Name)
VALUES (N'IT'), (N'Sales');

INSERT INTO dbo.Customers (Name, Email, Country)
VALUES
    (N'Acme Corp', N'info@acme.example', N'USA'),
    (N'Beta LLC',  N'hello@beta.example', N'UK');

INSERT INTO dbo.Employees (Id, Name, Salary, DepartmentId, Department)
VALUES
    (1, N'Alice', 5000.00, 1, N'IT'),
    (2, N'Bob',   4500.00, 2, N'Sales');
GO
