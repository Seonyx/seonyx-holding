-- ============================================
-- Seonyx Holdings Database Schema
-- Run this script on a fresh 'Seonyx' database
-- ============================================

-- Divisions table (create first - referenced by Pages)
CREATE TABLE Divisions (
    DivisionId INT PRIMARY KEY IDENTITY(1,1),
    Name NVARCHAR(200) NOT NULL,
    Slug NVARCHAR(200) NOT NULL UNIQUE,
    Description NVARCHAR(MAX),
    LogoUrl NVARCHAR(500),
    WebsiteUrl NVARCHAR(500),
    SortOrder INT DEFAULT 0,
    IsActive BIT DEFAULT 1,
    BackgroundColor NVARCHAR(7),
    ForegroundColor NVARCHAR(7),
    CreatedDate DATETIME DEFAULT GETDATE()
);

CREATE INDEX IX_Divisions_Slug ON Divisions(Slug);

-- Pages table
CREATE TABLE Pages (
    PageId INT PRIMARY KEY IDENTITY(1,1),
    Slug NVARCHAR(200) NOT NULL UNIQUE,
    Title NVARCHAR(500) NOT NULL,
    MetaDescription NVARCHAR(500),
    MetaKeywords NVARCHAR(500),
    Content NVARCHAR(MAX) NOT NULL,
    ParentPageId INT NULL,
    DivisionId INT NULL,
    SortOrder INT DEFAULT 0,
    IsPublished BIT DEFAULT 1,
    ShowInNavigation BIT DEFAULT 1,
    CreatedDate DATETIME DEFAULT GETDATE(),
    ModifiedDate DATETIME DEFAULT GETDATE(),
    CONSTRAINT FK_Pages_Parent FOREIGN KEY (ParentPageId)
        REFERENCES Pages(PageId),
    CONSTRAINT FK_Pages_Division FOREIGN KEY (DivisionId)
        REFERENCES Divisions(DivisionId)
);

CREATE INDEX IX_Pages_Slug ON Pages(Slug);
CREATE INDEX IX_Pages_Division ON Pages(DivisionId);
CREATE INDEX IX_Pages_Parent ON Pages(ParentPageId);

-- Content Blocks table
CREATE TABLE ContentBlocks (
    BlockId INT PRIMARY KEY IDENTITY(1,1),
    BlockKey NVARCHAR(100) NOT NULL UNIQUE,
    Title NVARCHAR(200) NOT NULL,
    Content NVARCHAR(MAX) NOT NULL,
    IsActive BIT DEFAULT 1,
    ModifiedDate DATETIME DEFAULT GETDATE()
);

CREATE INDEX IX_ContentBlocks_Key ON ContentBlocks(BlockKey);

-- Contact Submissions table
CREATE TABLE ContactSubmissions (
    SubmissionId INT PRIMARY KEY IDENTITY(1,1),
    Name NVARCHAR(200) NOT NULL,
    Email NVARCHAR(320) NOT NULL,
    Subject NVARCHAR(500),
    Message NVARCHAR(MAX) NOT NULL,
    IpAddress NVARCHAR(45),
    UserAgent NVARCHAR(500),
    IsRead BIT DEFAULT 0,
    IsSpam BIT DEFAULT 0,
    SubmittedDate DATETIME DEFAULT GETDATE()
);

CREATE INDEX IX_ContactSubmissions_Date ON ContactSubmissions(SubmittedDate DESC);
CREATE INDEX IX_ContactSubmissions_Read ON ContactSubmissions(IsRead);

-- Authors table
CREATE TABLE Authors (
    AuthorId INT PRIMARY KEY IDENTITY(1,1),
    PenName NVARCHAR(200) NOT NULL,
    Biography NVARCHAR(MAX),
    PhotoUrl NVARCHAR(500),
    Genre NVARCHAR(200),
    Website NVARCHAR(500),
    SortOrder INT DEFAULT 0,
    IsActive BIT DEFAULT 1,
    CreatedDate DATETIME DEFAULT GETDATE()
);

-- Books table
CREATE TABLE Books (
    BookId INT PRIMARY KEY IDENTITY(1,1),
    AuthorId INT NOT NULL,
    Title NVARCHAR(500) NOT NULL,
    Synopsis NVARCHAR(MAX),
    CoverImageUrl NVARCHAR(500),
    AmazonUrl NVARCHAR(500),
    KDPUrl NVARCHAR(500),
    ISBN NVARCHAR(20),
    PublicationDate DATE,
    Genre NVARCHAR(200),
    SortOrder INT DEFAULT 0,
    IsPublished BIT DEFAULT 0,
    CreatedDate DATETIME DEFAULT GETDATE(),
    CONSTRAINT FK_Books_Author FOREIGN KEY (AuthorId)
        REFERENCES Authors(AuthorId)
);

CREATE INDEX IX_Books_Author ON Books(AuthorId);

-- Site Settings table
CREATE TABLE SiteSettings (
    SettingKey NVARCHAR(100) PRIMARY KEY,
    SettingValue NVARCHAR(MAX),
    Description NVARCHAR(500),
    ModifiedDate DATETIME DEFAULT GETDATE()
);
