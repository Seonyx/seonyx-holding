# Database Schema for Seonyx CMS

## Overview

Simple database-driven CMS allowing content management without code changes. All content stored in MSSQL tables with Entity Framework access.

## Schema Design Philosophy

- Normalize for data integrity
- Keep it simple - no over-engineering
- Support multiple content types
- Enable easy querying for navigation
- Version tracking for content changes (optional phase 2)

## Tables

### 1. Pages

Primary content table for all site pages.

```sql
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
```

**Fields Explained:**
- `Slug`: URL-friendly identifier (e.g., "about-us", "literary-agency")
- `ParentPageId`: For hierarchical pages (nullable for top-level)
- `DivisionId`: Associates page with a business division
- `SortOrder`: Controls navigation order
- `IsPublished`: Draft vs Published status
- `ShowInNavigation`: Some pages exist but aren't in menus

### 2. Divisions

Business divisions under the Seonyx umbrella.

```sql
CREATE TABLE Divisions (
    DivisionId INT PRIMARY KEY IDENTITY(1,1),
    Name NVARCHAR(200) NOT NULL,
    Slug NVARCHAR(200) NOT NULL UNIQUE,
    Description NVARCHAR(MAX),
    LogoUrl NVARCHAR(500),
    WebsiteUrl NVARCHAR(500),
    SortOrder INT DEFAULT 0,
    IsActive BIT DEFAULT 1,
    BackgroundColor NVARCHAR(7), -- Hex color for branding
    ForegroundColor NVARCHAR(7),
    CreatedDate DATETIME DEFAULT GETDATE()
);

CREATE INDEX IX_Divisions_Slug ON Divisions(Slug);
```

**Initial Data:**
- Techwrite
- Literary Agency
- Inglesolar
- Pixtracta
- Homesonthemed

### 3. ContentBlocks

Reusable content blocks for things like homepage intro, footers, etc.

```sql
CREATE TABLE ContentBlocks (
    BlockId INT PRIMARY KEY IDENTITY(1,1),
    BlockKey NVARCHAR(100) NOT NULL UNIQUE,
    Title NVARCHAR(200) NOT NULL,
    Content NVARCHAR(MAX) NOT NULL,
    IsActive BIT DEFAULT 1,
    ModifiedDate DATETIME DEFAULT GETDATE()
);

CREATE INDEX IX_ContentBlocks_Key ON ContentBlocks(BlockKey);
```

**Usage Examples:**
- `homepage-hero`: Hero section on home page
- `footer-text`: Footer content
- `contact-intro`: Contact page introduction

### 4. ContactSubmissions

Store contact form submissions.

```sql
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
```

### 5. Authors

For the Literary Agency division - represents pen names and their details.

```sql
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
```

### 6. Books

Books written by authors in the stable.

```sql
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
```

### 7. SiteSettings

Global site configuration.

```sql
CREATE TABLE SiteSettings (
    SettingKey NVARCHAR(100) PRIMARY KEY,
    SettingValue NVARCHAR(MAX),
    Description NVARCHAR(500),
    ModifiedDate DATETIME DEFAULT GETDATE()
);
```

**Common Settings:**
- `site-title`: "Seonyx Holdings"
- `site-tagline`: Main tagline
- `contact-email`: Where contact forms send to
- `smtp-host`, `smtp-port`, etc.
- `admin-password-hash`: Hashed admin password
- `recaptcha-site-key`, `recaptcha-secret-key`

## Seed Data Script

```sql
-- Insert Divisions
INSERT INTO Divisions (Name, Slug, Description, SortOrder, BackgroundColor, ForegroundColor) VALUES
('Techwrite', 'techwrite', 'Non-fiction writing and editorial services', 1, '#6B46C1', '#FFFFFF'),
('Literary Agency', 'literary-agency', 'Representing science fiction authors', 2, '#059669', '#FFFFFF'),
('Inglesolar', 'inglesolar', 'Solar energy consultancy for Southern Spain', 3, '#F59E0B', '#000000'),
('Pixtracta', 'pixtracta', 'AI-powered real estate software with image recognition', 4, '#3B82F6', '#FFFFFF'),
('Homesonthemed', 'homesonthemed', 'Mediterranean property listings', 5, '#EF4444', '#FFFFFF');

-- Insert Home Page
INSERT INTO Pages (Slug, Title, MetaDescription, Content, SortOrder, ShowInNavigation) VALUES
('home', 'Seonyx Holdings', 'Seonyx Holdings - A diversified holding company with divisions in publishing, renewable energy, and technology', 
'<h1>Seonyx Holdings</h1><p>A diversified holding company with expertise across multiple sectors.</p>', 
0, 0);

-- Insert About Page
INSERT INTO Pages (Slug, Title, MetaDescription, Content, SortOrder) VALUES
('about', 'About Seonyx', 'Learn about Seonyx Holdings and our diverse portfolio of businesses', 
'<h1>About Seonyx Holdings</h1><p>Established in 2010, Seonyx has evolved into a holding company managing diverse business ventures.</p>', 
1);

-- Insert Contact Page
INSERT INTO Pages (Slug, Title, MetaDescription, Content, SortOrder) VALUES
('contact', 'Contact Us', 'Get in touch with Seonyx Holdings', 
'<h1>Contact Us</h1><p>We would love to hear from you.</p>', 
99);

-- Insert Maureen Avis as initial author
INSERT INTO Authors (PenName, Biography, Genre, SortOrder) VALUES
('Maureen Avis', 
'Maureen Avis is an up-and-coming science fiction author with a unique voice in speculative fiction. Her work explores the intersection of technology and humanity, set against richly imagined futures.', 
'Science Fiction', 
1);

-- Insert Content Blocks
INSERT INTO ContentBlocks (BlockKey, Title, Content) VALUES
('homepage-hero', 'Homepage Hero', '<h1>Seonyx Holdings</h1><p class="lead">Building tomorrow''s businesses today</p>'),
('footer-text', 'Footer Content', '<p>&copy; 2025 Seonyx Holdings. All rights reserved.</p>');

-- Insert Site Settings
INSERT INTO SiteSettings (SettingKey, SettingValue, Description) VALUES
('site-title', 'Seonyx Holdings', 'Main site title'),
('site-tagline', 'Building Tomorrow''s Businesses Today', 'Site tagline/slogan'),
('contact-email', 'contact@seonyx.com', 'Email address for contact form submissions'),
('admin-username', 'admin', 'Admin username for CMS'),
('items-per-page', '10', 'Pagination default');
```

## Entity Framework Models

These tables will map to C# models using Entity Framework Code First approach:

- `Page.cs`
- `Division.cs`
- `ContentBlock.cs`
- `ContactSubmission.cs`
- `Author.cs`
- `Book.cs`
- `SiteSetting.cs`

Plus a `SeonyxContext.cs` DbContext class to manage them all.

## Migration Strategy

### Initial Setup
1. Run seed data SQL script manually on production database
2. Entity Framework will use existing tables (no automatic migrations)

### Future Changes
1. Modify models in code
2. Generate SQL migration scripts manually
3. Test on local database
4. Apply to production via SQL script

## Admin Interface Queries

Common queries the admin interface will need:

```sql
-- Get all pages for a division
SELECT * FROM Pages WHERE DivisionId = @DivisionId ORDER BY SortOrder;

-- Get navigation structure
SELECT * FROM Pages WHERE ShowInNavigation = 1 ORDER BY SortOrder;

-- Get unread contact submissions
SELECT * FROM ContactSubmissions WHERE IsRead = 0 ORDER BY SubmittedDate DESC;

-- Get all books for an author
SELECT * FROM Books WHERE AuthorId = @AuthorId ORDER BY SortOrder;
```

## Indexes Summary

Already included in table definitions above:
- Slug indexes for fast URL lookups
- Division/Parent indexes for hierarchical queries
- Date indexes for chronological sorting
- Status indexes (IsRead, IsPublished) for filtering

## Backup Recommendations

- Regular MSSQL backups via hosting provider
- Export seed data periodically
- Keep SQL scripts in git repository for disaster recovery
