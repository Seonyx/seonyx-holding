-- ============================================================
-- Import Log Migration
-- Adds the ImportLogs table for tracking import history.
--
-- Run against the Seonyx database AFTER bookml-migration.sql.
-- Safe to run multiple times (idempotent).
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ImportLogs')
BEGIN
    CREATE TABLE ImportLogs (
        ImportLogID       INT           IDENTITY(1,1) PRIMARY KEY,
        BookProjectID     INT           NOT NULL,
        ImportedAt        DATETIME      NOT NULL DEFAULT GETDATE(),
        Success           BIT           NOT NULL,
        DraftNumber       INT           NOT NULL DEFAULT 0,
        ChaptersProcessed INT           NOT NULL DEFAULT 0,
        ParagraphsAdded   INT           NOT NULL DEFAULT 0,
        ParagraphsUpdated INT           NOT NULL DEFAULT 0,
        ParagraphsRemoved INT           NOT NULL DEFAULT 0,
        VersionsRecorded  INT           NOT NULL DEFAULT 0,
        WarningCount      INT           NOT NULL DEFAULT 0,
        ErrorCount        INT           NOT NULL DEFAULT 0,
        FullLog           NVARCHAR(MAX) NULL,
        CONSTRAINT FK_ImportLogs_BookProjects FOREIGN KEY (BookProjectID)
            REFERENCES BookProjects(BookProjectID)
    );

    CREATE INDEX IX_ImportLogs_Project ON ImportLogs(BookProjectID, ImportedAt DESC);
END
