-- ============================================
-- Book Editor Tables
-- Run this on an existing Seonyx database
-- that already has the base schema tables
-- ============================================

-- Book Projects table
CREATE TABLE BookProjects (
    BookProjectID INT IDENTITY(1,1) PRIMARY KEY,
    ProjectName NVARCHAR(255) NOT NULL UNIQUE,
    CoverImagePath NVARCHAR(500) NULL,
    FolderPath NVARCHAR(500) NOT NULL,
    CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
    LastModifiedDate DATETIME NOT NULL DEFAULT GETDATE(),
    IsActive BIT NOT NULL DEFAULT 1
);

-- Chapters table
CREATE TABLE Chapters (
    ChapterID INT IDENTITY(1,1) PRIMARY KEY,
    BookProjectID INT NOT NULL,
    ChapterNumber INT NOT NULL,
    ChapterTitle NVARCHAR(500) NULL,
    POV NVARCHAR(255) NULL,
    Setting NVARCHAR(500) NULL,
    ChapterPurpose NVARCHAR(MAX) NULL,
    SourceFileName NVARCHAR(255) NULL,
    CONSTRAINT FK_Chapters_BookProjects FOREIGN KEY (BookProjectID)
        REFERENCES BookProjects(BookProjectID) ON DELETE CASCADE,
    CONSTRAINT UQ_Chapter_Per_Book UNIQUE (BookProjectID, ChapterNumber)
);

-- Paragraphs table
CREATE TABLE Paragraphs (
    ParagraphID INT IDENTITY(1,1) PRIMARY KEY,
    ChapterID INT NOT NULL,
    UniqueID NVARCHAR(50) NOT NULL,
    OrdinalPosition INT NOT NULL,
    ParagraphText NVARCHAR(MAX) NOT NULL,
    CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
    LastModifiedDate DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Paragraphs_Chapters FOREIGN KEY (ChapterID)
        REFERENCES Chapters(ChapterID) ON DELETE CASCADE,
    CONSTRAINT UQ_UniqueID_Per_Chapter UNIQUE (ChapterID, UniqueID)
);

CREATE INDEX IX_Paragraphs_OrdinalPosition ON Paragraphs(ChapterID, OrdinalPosition);
CREATE INDEX IX_Paragraphs_UniqueID ON Paragraphs(UniqueID);

-- Meta Notes table
CREATE TABLE MetaNotes (
    MetaNoteID INT IDENTITY(1,1) PRIMARY KEY,
    ParagraphID INT NOT NULL,
    UniqueID NVARCHAR(50) NOT NULL,
    MetaText NVARCHAR(MAX) NOT NULL,
    CONSTRAINT FK_MetaNotes_Paragraphs FOREIGN KEY (ParagraphID)
        REFERENCES Paragraphs(ParagraphID) ON DELETE CASCADE,
    CONSTRAINT UQ_MetaNote_Per_Paragraph UNIQUE (ParagraphID)
);

CREATE INDEX IX_MetaNotes_UniqueID ON MetaNotes(UniqueID);

-- Edit Notes table
CREATE TABLE EditNotes (
    EditNoteID INT IDENTITY(1,1) PRIMARY KEY,
    ParagraphID INT NOT NULL,
    UniqueID NVARCHAR(50) NOT NULL,
    NoteText NVARCHAR(MAX) NULL,
    LastModifiedDate DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_EditNotes_Paragraphs FOREIGN KEY (ParagraphID)
        REFERENCES Paragraphs(ParagraphID) ON DELETE CASCADE,
    CONSTRAINT UQ_EditNote_Per_Paragraph UNIQUE (ParagraphID)
);

CREATE INDEX IX_EditNotes_UniqueID ON EditNotes(UniqueID);
