-- ============================================================
-- BookML Migration
-- Adds draft versioning and BookML identity fields to the
-- Book Editor tables.
--
-- Safe to run multiple times (idempotent).
-- Run against an existing Seonyx database AFTER schema.sql.
-- ============================================================

-- ------------------------------------------------------------
-- 1. Add BookML identity fields to BookProjects
-- ------------------------------------------------------------
IF COL_LENGTH('BookProjects', 'BookmlId') IS NULL
    ALTER TABLE BookProjects ADD BookmlId NVARCHAR(100) NULL;

IF COL_LENGTH('BookProjects', 'CurrentDraftNumber') IS NULL
    ALTER TABLE BookProjects ADD CurrentDraftNumber INT NOT NULL DEFAULT 1;

-- ------------------------------------------------------------
-- 2. Add BookML chapter id to Chapters
-- ------------------------------------------------------------
IF COL_LENGTH('Chapters', 'BookmlChapterId') IS NULL
    ALTER TABLE Chapters ADD BookmlChapterId NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Chapters_BookmlId' AND object_id = OBJECT_ID('Chapters'))
    CREATE INDEX IX_Chapters_BookmlId ON Chapters(BookmlChapterId);

-- ------------------------------------------------------------
-- 3. Drafts table
-- One row per draft per project. Never delete rows.
-- Exactly one draft per project may have Status = 'in-progress'.
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Drafts')
BEGIN
    CREATE TABLE Drafts (
        DraftID       INT           IDENTITY(1,1) PRIMARY KEY,
        BookProjectID INT           NOT NULL,
        DraftNumber   INT           NOT NULL,
        Status        NVARCHAR(20)  NOT NULL DEFAULT 'in-progress',
        CreatedDate   DATETIME      NOT NULL DEFAULT GETDATE(),
        BasedOn       INT           NOT NULL DEFAULT 0,
        AuthorType    NVARCHAR(10)  NOT NULL,
        Author        NVARCHAR(200) NOT NULL,
        Label         NVARCHAR(200) NULL,
        ExportDate    DATETIME      NULL,
        DraftNote     NVARCHAR(MAX) NULL,
        CONSTRAINT FK_Drafts_BookProjects FOREIGN KEY (BookProjectID)
            REFERENCES BookProjects(BookProjectID) ON DELETE CASCADE,
        CONSTRAINT UQ_Draft_Per_Book UNIQUE (BookProjectID, DraftNumber)
    );

    CREATE INDEX IX_Drafts_BookProject ON Drafts(BookProjectID);
END

-- ------------------------------------------------------------
-- 4. ParagraphVersions table
-- Append-only snapshot history. Never update or delete rows.
-- Join is ALWAYS on Pid. Seq is never used as a foreign key.
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ParagraphVersions')
BEGIN
    CREATE TABLE ParagraphVersions (
        VersionID     INT           IDENTITY(1,1) PRIMARY KEY,
        Pid           NVARCHAR(50)  NOT NULL,
        ChapterID     INT           NOT NULL,
        DraftNumber   INT           NOT NULL,
        Seq           INT           NOT NULL,
        ParaType      NVARCHAR(20)  NOT NULL DEFAULT 'normal',
        Content       NVARCHAR(MAX) NOT NULL,
        DraftCreated  INT           NOT NULL,
        DraftModified INT           NOT NULL,
        ModifiedBy    NVARCHAR(10)  NOT NULL,
        ModifiedDate  DATETIME      NULL,
        ChangeType    NVARCHAR(20)  NULL,
        CONSTRAINT FK_ParaVersions_Chapters FOREIGN KEY (ChapterID)
            REFERENCES Chapters(ChapterID) ON DELETE CASCADE,
        CONSTRAINT UQ_PidVersion UNIQUE (Pid, DraftNumber)
    );

    CREATE INDEX IX_ParaVersions_Pid     ON ParagraphVersions(Pid);
    CREATE INDEX IX_ParaVersions_Chapter ON ParagraphVersions(ChapterID, DraftNumber);
END
