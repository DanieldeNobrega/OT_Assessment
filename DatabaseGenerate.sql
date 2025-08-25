/*
Recreates the database schema for the OT Assessment.
Run on SQL Server 2022 (Developer). Adjust database creation if needed.
*/

IF DB_ID('OT_Assessment_DB') IS NULL
BEGIN
    CREATE DATABASE OT_Assessment_DB;
END
GO

USE OT_Assessment_DB;
GO

-- Create schema
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'casino')
    EXEC('CREATE SCHEMA casino AUTHORIZATION dbo');
GO

-- Players
IF OBJECT_ID('casino.Players') IS NULL
BEGIN
    CREATE TABLE casino.Players (
        AccountId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        Username NVARCHAR(100) NOT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Players_CreatedAt DEFAULT SYSUTCDATETIME()
    );
END
GO

-- Wagers
IF OBJECT_ID('casino.Wagers') IS NULL
BEGIN
    CREATE TABLE casino.Wagers (
        WagerId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        AccountId UNIQUEIDENTIFIER NOT NULL,
        GameName NVARCHAR(200) NOT NULL,
        Provider NVARCHAR(200) NOT NULL,
        Amount DECIMAL(18,4) NOT NULL,
        CreatedDateTime DATETIMEOFFSET NOT NULL,
        CONSTRAINT FK_Wagers_Players FOREIGN KEY (AccountId) REFERENCES casino.Players(AccountId)
    );

    CREATE INDEX IX_Wagers_Account_Created ON casino.Wagers(AccountId, CreatedDateTime DESC);
END
GO

-- Ingest proc (UPSERT player + insert wager idempotently)
IF OBJECT_ID('casino.usp_IngestWager') IS NOT NULL DROP PROCEDURE casino.usp_IngestWager;
GO
CREATE PROCEDURE casino.usp_IngestWager
    @WagerId UNIQUEIDENTIFIER,
    @AccountId UNIQUEIDENTIFIER,
    @Username NVARCHAR(100),
    @GameName NVARCHAR(200),
    @Provider NVARCHAR(200),
    @Amount DECIMAL(18,4),
    @CreatedDateTime DATETIMEOFFSET
AS
BEGIN
    SET NOCOUNT ON;

    -- Upsert player
    MERGE casino.Players AS tgt
    USING (SELECT @AccountId AS AccountId, @Username AS Username) AS src
    ON (tgt.AccountId = src.AccountId)
    WHEN MATCHED AND tgt.Username <> src.Username THEN
        UPDATE SET Username = src.Username
    WHEN NOT MATCHED THEN
        INSERT (AccountId, Username) VALUES (src.AccountId, src.Username);

    -- Insert wager if not exists (idempotent)
    IF NOT EXISTS (SELECT 1 FROM casino.Wagers WHERE WagerId = @WagerId)
    BEGIN
        INSERT INTO casino.Wagers (WagerId, AccountId, GameName, Provider, Amount, CreatedDateTime)
        VALUES (@WagerId, @AccountId, @GameName, @Provider, @Amount, @CreatedDateTime);
    END
END
GO

-- Paged player wagers
IF OBJECT_ID('casino.usp_GetPlayerWagersPaged') IS NOT NULL DROP PROCEDURE casino.usp_GetPlayerWagersPaged;
GO
CREATE PROCEDURE casino.usp_GetPlayerWagersPaged
    @AccountId UNIQUEIDENTIFIER,
    @PageNumber INT,
    @PageSize INT
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH Ordered AS (
        SELECT
            WagerId,
            GameName AS [Game],
            Provider,
            Amount,
            CreatedDateTime AS CreatedDate,
            ROW_NUMBER() OVER (ORDER BY CreatedDateTime DESC, WagerId DESC) AS rn
        FROM casino.Wagers
        WHERE AccountId = @AccountId
    )
    SELECT WagerId, [Game], Provider, Amount, CreatedDate
    FROM Ordered
    WHERE rn BETWEEN ((@PageNumber - 1) * @PageSize + 1) AND (@PageNumber * @PageSize)
    ORDER BY CreatedDate DESC;

    DECLARE @Total INT = (SELECT COUNT(*) FROM casino.Wagers WHERE AccountId = @AccountId);
    DECLARE @TotalPages INT = CASE WHEN @Total=0 THEN 0 ELSE CEILING(@Total * 1.0 / @PageSize) END;
    SELECT @Total AS Total, @TotalPages AS TotalPages;
END
GO

-- Top spenders
IF OBJECT_ID('casino.usp_GetTopSpenders') IS NOT NULL DROP PROCEDURE casino.usp_GetTopSpenders;
GO
CREATE PROCEDURE casino.usp_GetTopSpenders
    @Top INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@Top)
        p.AccountId,
        p.Username,
        CONVERT(DECIMAL(18,4), SUM(w.Amount)) AS TotalAmountSpend
    FROM casino.Wagers w
    INNER JOIN casino.Players p ON p.AccountId = w.AccountId
    GROUP BY p.AccountId, p.Username
    ORDER BY SUM(w.Amount) DESC;
END
GO
