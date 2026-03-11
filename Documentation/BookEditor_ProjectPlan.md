# Book Editor - Project Specification
## For Seonyx Holding Company Admin Portal Integration

---

## 1. PROJECT OVERVIEW

### 1.1 Purpose
Create an admin-protected web-based manuscript editor that enables paragraph-level editing of AI-generated books with synchronized META explanations and editorial NOTES. This tool replaces a manual MS Access workflow with a more robust web-based solution.

### 1.2 User Workflow Summary
1. User creates a new book project
2. User uploads chapter files (.md), META files (.md), and NOTE files (.txt or similar)
3. System parses files, extracts paragraphs with unique IDs, and imports to MSSQL database
4. User navigates paragraph-by-paragraph editing the manuscript while viewing corresponding META explanations and adding/editing personal NOTES
5. User can insert new paragraphs (which generates new unique IDs and reorders ordinals) or delete paragraphs
6. User exports updated manuscript files at any time

### 1.3 Technical Context
- **Framework**: ASP.NET MVC 4.5 (matching existing Seonyx infrastructure)
- **Database**: Microsoft SQL Server
- **Integration**: Admin-protected area of existing Seonyx website
- **File Storage**: Server-side folder structure organized by book project name

---

## 2. DATABASE SCHEMA

### 2.1 BookProjects Table
```sql
CREATE TABLE BookProjects (
    BookProjectID INT IDENTITY(1,1) PRIMARY KEY,
    ProjectName NVARCHAR(255) NOT NULL UNIQUE,
    CoverImagePath NVARCHAR(500) NULL,
    FolderPath NVARCHAR(500) NOT NULL,
    CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
    LastModifiedDate DATETIME NOT NULL DEFAULT GETDATE(),
    IsActive BIT NOT NULL DEFAULT 1
);
```

### 2.2 Chapters Table
```sql
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
```

### 2.3 Paragraphs Table
```sql
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
```

### 2.4 MetaNotes Table
```sql
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
```

### 2.5 EditNotes Table
```sql
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
```

---

## 3. FILE STRUCTURE & PARSING

### 3.1 Server File Organization
```
/BookEditorFiles/
    /{ProjectName}/
        /uploads/
            ch01_filename.md
            ch01_filename_meta.md
            ch01_filename_notes.txt
            ch02_filename.md
            ...
        /covers/
            cover.jpg
        /exports/
            {timestamp}_export.zip
```

### 3.2 Chapter File Format (Example Observed)
```markdown
# Chapter 01 - Static in the Seminar

## POV
Mateo (third-person limited)

## Setting
Madrid, UPM campus lab + rooftop

## Chapter purpose
- Move the plot: Open with Mateo...

## Beat map (high level)
- Beat 01: Open with Mateo...

## Draft Paragraphs (keyed)

[[C01-4E4JEXDWPR]] The seminar room smelled like...

[[C01-7QK2N1ZP8A]] He'd come for a harmless hour...
```

**Parsing Rules:**
- Extract chapter number from filename pattern: `ch{NN}_`
- Extract chapter title from first `# Chapter` heading
- Extract POV, Setting, Chapter purpose from `##` headings
- Extract paragraphs starting with `[[UNIQUE_ID]]` pattern
- Each paragraph runs until the next `[[UNIQUE_ID]]` or EOF

### 3.3 META File Format (Example Observed)
```markdown
# Chapter 01 Meta Notes - Static in the Seminar

## What this file is
Keyed meta notes aligned 1:1 with the chapter text paragraphs.

## Meta entries

[[C01-4E4JEXDWPR]] Establish Mateo's baseline: cynical student...

[[C01-7QK2N1ZP8A]] Reinforce "normal world" signal-processing...
```

**Parsing Rules:**
- Extract entries starting with `[[UNIQUE_ID]]` pattern
- Match to corresponding paragraph by UniqueID
- Each META entry runs until next `[[UNIQUE_ID]]` or EOF

### 3.4 NOTE File Format (Example Observed)
```
FB9C6F812540|Another emdash. The paragraph described...
CD240D6C0AA7|Starts with a conjunction. Two more emdashes.
DD6DAC913B8C|n/a
```

**Parsing Rules:**
- Pipe-delimited format: `UNIQUE_ID|note text`
- One note per line
- Match to paragraph by UniqueID
- Handle blank notes (n/a or empty after pipe)

---

## 4. MVC ARCHITECTURE

### 4.1 Controllers

#### BookProjectController
**Actions:**
- `Index()` - List all book projects with thumbnails
- `Create()` [GET] - Show create new project form
- `Create(BookProjectModel)` [POST] - Create project and folder structure
- `Edit(int id)` [GET] - Edit project metadata
- `Edit(BookProjectModel)` [POST] - Update project metadata
- `Delete(int id)` [POST] - Delete project and all associated data
- `UploadCover(int id)` [POST] - Upload and process cover image
- `SwitchProject(int id)` [POST] - Set active project in session

#### FileUploadController
**Actions:**
- `Index(int projectId)` - Show file management interface for project
- `UploadChapter(int projectId)` [POST] - Upload chapter .md file
- `UploadMeta(int projectId)` [POST] - Upload META .md file
- `UploadNotes(int projectId)` [POST] - Upload NOTES file
- `DeleteFile(int projectId, string filename)` [POST] - Delete uploaded file
- `ReplaceFile(int projectId, string filename)` [POST] - Replace existing file
- `ImportFiles(int projectId)` [POST] - Parse and import uploaded files to database
- `GetFileList(int projectId)` [GET] - Return JSON list of files in project folder

#### EditorController
**Actions:**
- `Index(int projectId)` [GET] - Main editor interface
- `GetParagraph(int paragraphId)` [GET] - Return JSON with paragraph + meta + note
- `SaveParagraph(ParagraphEditModel)` [POST] - Save edited paragraph text
- `SaveNote(int paragraphId, string noteText)` [POST] - Save/update edit note
- `InsertParagraph(int currentParagraphId)` [POST] - Insert new paragraph after current
- `DeleteParagraph(int paragraphId)` [POST] - Delete paragraph and reorder ordinals
- `GetNavigationContext(int paragraphId)` [GET] - Return prev/next/first/last paragraph IDs
- `JumpToChapter(int projectId, int chapterNumber)` [GET] - Navigate to first paragraph of chapter
- `JumpToParagraph(int projectId, int ordinalPosition)` [GET] - Navigate to specific ordinal position
- `AutoSave(ParagraphEditModel)` [POST] - Auto-save edited content (AJAX)

#### ExportController
**Actions:**
- `ExportProject(int projectId)` [GET] - Generate and download complete export ZIP
- `ExportChapter(int chapterId, string format)` [GET] - Export single chapter
- `ExportManuscriptOnly(int projectId)` [GET] - Export only chapter files (no META/NOTES)
- `GetExportHistory(int projectId)` [GET] - List previous exports

### 4.2 Models

#### BookProjectModel
```csharp
public class BookProjectModel
{
    public int BookProjectID { get; set; }
    public string ProjectName { get; set; }
    public string CoverImagePath { get; set; }
    public string FolderPath { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime LastModifiedDate { get; set; }
    public int TotalChapters { get; set; }
    public int TotalParagraphs { get; set; }
}
```

#### ChapterModel
```csharp
public class ChapterModel
{
    public int ChapterID { get; set; }
    public int BookProjectID { get; set; }
    public int ChapterNumber { get; set; }
    public string ChapterTitle { get; set; }
    public string POV { get; set; }
    public string Setting { get; set; }
    public string ChapterPurpose { get; set; }
    public string SourceFileName { get; set; }
    public int ParagraphCount { get; set; }
}
```

#### ParagraphEditModel
```csharp
public class ParagraphEditModel
{
    public int ParagraphID { get; set; }
    public int ChapterID { get; set; }
    public string UniqueID { get; set; }
    public int OrdinalPosition { get; set; }
    public string ParagraphText { get; set; }
    public string MetaText { get; set; }
    public string EditNoteText { get; set; }
    public string ChapterTitle { get; set; }
    public int ChapterNumber { get; set; }
    
    // Navigation context
    public int? PrevParagraphID { get; set; }
    public int? NextParagraphID { get; set; }
    public int FirstParagraphID { get; set; }
    public int LastParagraphID { get; set; }
    public int TotalParagraphs { get; set; }
}
```

#### FileUploadModel
```csharp
public class FileUploadModel
{
    public int BookProjectID { get; set; }
    public string ProjectName { get; set; }
    public HttpPostedFileBase ChapterFile { get; set; }
    public HttpPostedFileBase MetaFile { get; set; }
    public HttpPostedFileBase NotesFile { get; set; }
    public List<UploadedFileInfo> ExistingFiles { get; set; }
}

public class UploadedFileInfo
{
    public string FileName { get; set; }
    public string FileType { get; set; } // "Chapter", "Meta", "Notes"
    public long FileSize { get; set; }
    public DateTime UploadDate { get; set; }
    public bool IsImported { get; set; }
}
```

### 4.3 Views

#### BookProject/Index.cshtml
- Grid/card layout showing all book projects
- Cover thumbnail for each project
- Project name, chapter count, paragraph count
- Action buttons: Edit, Delete, Open Editor, Manage Files
- "Create New Project" button

#### BookProject/Create.cshtml
- Form: Project Name input
- Cover image upload (optional at creation)
- Submit creates project + folder structure

#### FileUpload/Index.cshtml
- Project header with name and cover thumbnail
- File upload zones (drag-drop or browse):
  - Chapter files (.md)
  - META files (.md)
  - Notes files (.txt, .md)
- List of uploaded files with:
  - Filename
  - Type (Chapter/Meta/Notes)
  - Size
  - Upload date
  - Import status
  - Actions: Replace, Delete
- "Import All Files" button (parses and loads to DB)
- "Back to Projects" button

#### Editor/Index.cshtml
**Layout Structure:**
```
+--------------------------------------------------+
| Book: [Project Name]          Chapter: [N] of [Total] |
| [Switch Project ▼] [Manage Files] [Export]      |
+--------------------------------------------------+
|                                                  |
| Navigation: [« First] [‹ Prev] [Next ›] [Last »]|
|             [Insert New Paragraph] [Delete]      |
|                                                  |
| Position: [123] of [456]   Chapter: [N]         |
|                                                  |
+--------------------------------------------------+
| PARAGRAPH EDITOR                                 |
| +----------------------------------------------+ |
| | UniqueID: C01-4E4JEXDWPR                     | |
| |                                              | |
| | [Editable textarea - resizable, 8-10 lines]  | |
| | The seminar room smelled like dry-erase...   | |
| |                                              | |
| +----------------------------------------------+ |
|                                                  |
| META EXPLANATION (Read-only)                     |
| +----------------------------------------------+ |
| | Establish Mateo's baseline: cynical student  | |
| | comfort, seminar mundanity, and his          | |
| | attention tuning toward anomalies.           | |
| +----------------------------------------------+ |
|                                                  |
| EDIT NOTES (Your notes)                          |
| +----------------------------------------------+ |
| | [Editable textarea - 3-4 lines]              | |
| | Another emdash. The paragraph described...   | |
| +----------------------------------------------+ |
|                                                  |
| [Save Changes] [Auto-save: ON/OFF]               |
|                                                  |
+--------------------------------------------------+
```

**Key Features:**
- Three-panel layout: Paragraph (editable), META (read-only), Notes (editable)
- Clear visual hierarchy
- Keyboard shortcuts: Ctrl+S (save), Ctrl+→ (next), Ctrl+← (prev)
- Auto-save indicator showing last save time
- Ordinal position counter
- Chapter context display

#### Export/Index.cshtml
- Project selection dropdown
- Export format options:
  - Complete export (chapters + META + notes as separate files)
  - Manuscript only (clean chapter files)
  - Individual chapter selection
- Download as ZIP
- Export history table with download links

---

## 5. CORE FUNCTIONALITY SPECIFICATIONS

### 5.1 File Upload & Import Process

**Step 1: Upload Files**
- User uploads files to `/BookEditorFiles/{ProjectName}/uploads/`
- Files are validated (extension, size, naming pattern)
- Store metadata in temporary tracking table or session

**Step 2: Parse Files**
- Identify chapter number from filename pattern
- Parse chapter metadata (title, POV, setting, purpose)
- Extract all paragraphs with regex: `\[\[([A-Z0-9-]+)\]\]\s*(.*?)(?=\[\[|$)`
- Extract META entries with same regex
- Extract NOTE entries with regex: `^([A-Z0-9]+)\|(.*)$`

**Step 3: Import to Database**
- Create Chapter record
- Create Paragraph records with auto-incrementing OrdinalPosition
- Match and create MetaNotes records by UniqueID
- Match and create EditNotes records by UniqueID
- Handle orphaned IDs (paragraphs without META, META without paragraphs)
- Transaction: rollback if any critical errors

**Step 4: Validation**
- Verify all UniqueIDs match between chapter and META
- Report mismatches to user
- Allow user to proceed with warnings or fix files

### 5.2 Ordinal Position Management

**On Insert:**
```sql
-- Current paragraph at position 123
-- User clicks INSERT
-- New paragraph should become 124

-- Step 1: Increment all positions >= current+1
UPDATE Paragraphs 
SET OrdinalPosition = OrdinalPosition + 1
WHERE ChapterID = @ChapterID 
  AND OrdinalPosition > @CurrentPosition;

-- Step 2: Insert new paragraph at CurrentPosition + 1
INSERT INTO Paragraphs (ChapterID, UniqueID, OrdinalPosition, ParagraphText)
VALUES (@ChapterID, @NewUniqueID, @CurrentPosition + 1, '');

-- Step 3: Insert blank META and NOTE records
INSERT INTO MetaNotes (ParagraphID, UniqueID, MetaText)
VALUES (@NewParagraphID, @NewUniqueID, '');

INSERT INTO EditNotes (ParagraphID, UniqueID, NoteText)
VALUES (@NewParagraphID, @NewUniqueID, '');
```

**On Delete:**
```sql
-- Current paragraph at position 123
-- User clicks DELETE

-- Step 1: Delete paragraph (CASCADE deletes META and NOTE)
DELETE FROM Paragraphs WHERE ParagraphID = @ParagraphID;

-- Step 2: Decrement all positions > deleted position
UPDATE Paragraphs 
SET OrdinalPosition = OrdinalPosition - 1
WHERE ChapterID = @ChapterID 
  AND OrdinalPosition > @DeletedPosition;
```

**UniqueID Generation for Inserts:**
```csharp
// Pattern: C{ChapterNumber:D2}-{RandomAlphanumeric10}
public string GenerateUniqueID(int chapterNumber)
{
    string prefix = $"C{chapterNumber:D2}-";
    string random = Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper();
    return prefix + random;
}
```

### 5.3 Navigation Logic

**Database Query for Navigation Context:**
```sql
-- Given current ParagraphID, find prev/next/first/last

DECLARE @ChapterID INT, @CurrentOrdinal INT;

SELECT @ChapterID = ChapterID, @CurrentOrdinal = OrdinalPosition
FROM Paragraphs WHERE ParagraphID = @CurrentParagraphID;

-- Previous
SELECT TOP 1 ParagraphID 
FROM Paragraphs 
WHERE ChapterID = @ChapterID 
  AND OrdinalPosition < @CurrentOrdinal
ORDER BY OrdinalPosition DESC;

-- Next
SELECT TOP 1 ParagraphID 
FROM Paragraphs 
WHERE ChapterID = @ChapterID 
  AND OrdinalPosition > @CurrentOrdinal
ORDER BY OrdinalPosition ASC;

-- First
SELECT TOP 1 ParagraphID 
FROM Paragraphs 
WHERE ChapterID = @ChapterID 
ORDER BY OrdinalPosition ASC;

-- Last
SELECT TOP 1 ParagraphID 
FROM Paragraphs 
WHERE ChapterID = @ChapterID 
ORDER BY OrdinalPosition DESC;
```

**Cross-Chapter Navigation:**
- If at last paragraph of chapter, "Next" should jump to first paragraph of next chapter
- If at first paragraph of chapter, "Prev" should jump to last paragraph of previous chapter
- Requires cross-chapter query joining on BookProjectID

### 5.4 Auto-Save Implementation

**Client-Side (JavaScript):**
```javascript
var autoSaveTimer;
var hasUnsavedChanges = false;

$('#paragraphText, #editNoteText').on('input', function() {
    hasUnsavedChanges = true;
    clearTimeout(autoSaveTimer);
    autoSaveTimer = setTimeout(autoSave, 3000); // 3 seconds after last keystroke
});

function autoSave() {
    if (!hasUnsavedChanges) return;
    
    $.ajax({
        url: '/Editor/AutoSave',
        type: 'POST',
        data: {
            paragraphId: $('#paragraphId').val(),
            paragraphText: $('#paragraphText').val(),
            editNoteText: $('#editNoteText').val()
        },
        success: function(response) {
            hasUnsavedChanges = false;
            $('#saveStatus').text('Auto-saved at ' + new Date().toLocaleTimeString());
        }
    });
}

// Warn on navigation with unsaved changes
window.addEventListener('beforeunload', function(e) {
    if (hasUnsavedChanges) {
        e.preventDefault();
        e.returnValue = '';
    }
});
```

**Server-Side (Controller):**
```csharp
[HttpPost]
public JsonResult AutoSave(int paragraphId, string paragraphText, string editNoteText)
{
    using (var db = new BookEditorContext())
    {
        var paragraph = db.Paragraphs.Find(paragraphId);
        if (paragraph != null)
        {
            paragraph.ParagraphText = paragraphText;
            paragraph.LastModifiedDate = DateTime.Now;
            
            var editNote = db.EditNotes.FirstOrDefault(n => n.ParagraphID == paragraphId);
            if (editNote != null)
            {
                editNote.NoteText = editNoteText;
                editNote.LastModifiedDate = DateTime.Now;
            }
            
            db.SaveChanges();
            return Json(new { success = true, timestamp = DateTime.Now });
        }
    }
    return Json(new { success = false });
}
```

### 5.5 Export Functionality

**Export Complete Project:**
```csharp
public ActionResult ExportProject(int projectId)
{
    var project = db.BookProjects.Find(projectId);
    var chapters = db.Chapters.Where(c => c.BookProjectID == projectId)
                              .OrderBy(c => c.ChapterNumber).ToList();
    
    string exportPath = Path.Combine(project.FolderPath, "exports");
    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
    string zipFileName = $"{project.ProjectName}_{timestamp}.zip";
    string zipPath = Path.Combine(exportPath, zipFileName);
    
    using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
    {
        foreach (var chapter in chapters)
        {
            // Export chapter file
            string chapterMd = GenerateChapterMarkdown(chapter);
            string chapterFileName = $"ch{chapter.ChapterNumber:D2}_{Slugify(chapter.ChapterTitle)}.md";
            AddToZip(zip, chapterFileName, chapterMd);
            
            // Export META file
            string metaMd = GenerateMetaMarkdown(chapter);
            string metaFileName = $"ch{chapter.ChapterNumber:D2}_{Slugify(chapter.ChapterTitle)}_meta.md";
            AddToZip(zip, metaFileName, metaMd);
            
            // Export NOTES file
            string notesTxt = GenerateNotesText(chapter);
            string notesFileName = $"ch{chapter.ChapterNumber:D2}_{Slugify(chapter.ChapterTitle)}_notes.txt";
            AddToZip(zip, notesFileName, notesTxt);
        }
    }
    
    return File(zipPath, "application/zip", zipFileName);
}

private string GenerateChapterMarkdown(Chapter chapter)
{
    StringBuilder sb = new StringBuilder();
    sb.AppendLine($"# Chapter {chapter.ChapterNumber:D2} - {chapter.ChapterTitle}");
    sb.AppendLine();
    sb.AppendLine("## POV");
    sb.AppendLine(chapter.POV);
    sb.AppendLine();
    sb.AppendLine("## Setting");
    sb.AppendLine(chapter.Setting);
    sb.AppendLine();
    sb.AppendLine("## Chapter purpose");
    sb.AppendLine(chapter.ChapterPurpose);
    sb.AppendLine();
    sb.AppendLine("## Draft Paragraphs (keyed)");
    sb.AppendLine();
    
    var paragraphs = db.Paragraphs
                       .Where(p => p.ChapterID == chapter.ChapterID)
                       .OrderBy(p => p.OrdinalPosition)
                       .ToList();
    
    foreach (var para in paragraphs)
    {
        sb.AppendLine($"[[{para.UniqueID}]] {para.ParagraphText}");
        sb.AppendLine();
    }
    
    return sb.ToString();
}

private string GenerateMetaMarkdown(Chapter chapter)
{
    StringBuilder sb = new StringBuilder();
    sb.AppendLine($"# Chapter {chapter.ChapterNumber:D2} Meta Notes - {chapter.ChapterTitle}");
    sb.AppendLine();
    sb.AppendLine("## Meta entries");
    sb.AppendLine();
    
    var paragraphs = db.Paragraphs
                       .Where(p => p.ChapterID == chapter.ChapterID)
                       .OrderBy(p => p.OrdinalPosition)
                       .ToList();
    
    foreach (var para in paragraphs)
    {
        var meta = db.MetaNotes.FirstOrDefault(m => m.ParagraphID == para.ParagraphID);
        if (meta != null)
        {
            sb.AppendLine($"[[{para.UniqueID}]] {meta.MetaText}");
            sb.AppendLine();
        }
    }
    
    return sb.ToString();
}

private string GenerateNotesText(Chapter chapter)
{
    StringBuilder sb = new StringBuilder();
    
    var paragraphs = db.Paragraphs
                       .Where(p => p.ChapterID == chapter.ChapterID)
                       .OrderBy(p => p.OrdinalPosition)
                       .ToList();
    
    foreach (var para in paragraphs)
    {
        var note = db.EditNotes.FirstOrDefault(n => n.ParagraphID == para.ParagraphID);
        if (note != null && !string.IsNullOrWhiteSpace(note.NoteText))
        {
            sb.AppendLine($"{para.UniqueID}|{note.NoteText}");
        }
    }
    
    return sb.ToString();
}
```

---

## 6. UI/UX REQUIREMENTS

### 6.1 Design Principles
- **Minimize distraction**: Clean, focused interface with minimal chrome
- **Keyboard-first**: All navigation and editing accessible via keyboard
- **Responsive feedback**: Immediate visual confirmation of actions
- **Context awareness**: Always show current position, chapter, book
- **Error tolerance**: Confirm destructive actions, allow undo where possible

### 6.2 Visual Design
- Match existing Seonyx admin aesthetic
- Use monospace font for paragraph text editor (aids in editing)
- Subtle color coding: Paragraph (white/light), META (light blue tint), Notes (light yellow tint)
- Clear visual separation between the three content areas
- Sticky navigation bar (stays visible on scroll)

### 6.3 Keyboard Shortcuts
- `Ctrl + S` - Save changes
- `Ctrl + →` - Next paragraph
- `Ctrl + ←` - Previous paragraph
- `Ctrl + Home` - First paragraph
- `Ctrl + End` - Last paragraph
- `Ctrl + I` - Insert new paragraph
- `Ctrl + D` - Delete current paragraph (with confirmation)
- `Esc` - Cancel edit/close dialog

### 6.4 Mobile Considerations
- While primary use is desktop, editor should be usable on tablet
- Stack three panels vertically on smaller screens
- Touch-friendly navigation buttons
- Swipe gestures for prev/next

---

## 7. SECURITY & VALIDATION

### 7.1 Authentication & Authorization
- Book Editor area requires admin authentication (integrate with existing Seonyx auth)
- All actions verify user has admin role
- File operations validate user owns/has access to project

### 7.2 File Upload Validation
- Allowed extensions: `.md`, `.txt`
- Max file size: 10 MB per file
- Validate file content is valid UTF-8 text
- Scan for suspicious content patterns (no executable code)
- Generate safe folder names (sanitize project names)

### 7.3 Input Validation
- Project names: alphanumeric + spaces + hyphens only, max 255 chars
- Paragraph text: max 10,000 chars
- META text: max 2,000 chars
- Note text: max 1,000 chars
- UniqueID format validation: pattern `^[A-Z0-9-]{8,50}$`

### 7.4 SQL Injection Prevention
- Use parameterized queries exclusively
- Entity Framework for all database operations
- No dynamic SQL generation from user input

### 7.5 Path Traversal Prevention
- Validate all file paths are within allowed book editor directory
- Reject paths containing `..`, absolute paths, or special chars
- Use `Path.Combine()` and `Path.GetFullPath()` with validation

---

## 8. ERROR HANDLING

### 8.1 File Upload Errors
- Invalid file format → User-friendly message with accepted formats
- File too large → Show max size, suggest splitting chapters
- Parse errors → Show line number and sample of problematic content
- Duplicate UniqueIDs → List duplicates and affected chapters

### 8.2 Database Errors
- Constraint violations → Explain which rule was violated
- Deadlock/timeout → Retry logic with exponential backoff
- Data integrity issues → Rollback transaction, preserve user input

### 8.3 User-Facing Messages
- Success: "Chapter 3 imported successfully - 42 paragraphs loaded"
- Warning: "5 paragraphs have no matching META entries. Continue?"
- Error: "Unable to save changes. Please try again or contact support."

---

## 9. TESTING REQUIREMENTS

### 9.1 Unit Tests
- File parser: Test various markdown formats, edge cases, malformed input
- UniqueID generator: Verify uniqueness, format compliance
- Ordinal reordering: Insert/delete at various positions, boundary conditions
- Export: Verify output matches input after round-trip

### 9.2 Integration Tests
- Upload → Parse → Import → Edit → Export workflow
- Cross-chapter navigation
- Multi-user concurrent editing (if applicable)
- Large file handling (chapters with 100+ paragraphs)

### 9.3 Manual Testing Checklist
- [ ] Create new project
- [ ] Upload cover image
- [ ] Upload chapter files (single chapter, multiple chapters)
- [ ] Upload META files
- [ ] Upload NOTES files
- [ ] Import files to database
- [ ] Navigate forward/backward through paragraphs
- [ ] Edit paragraph text
- [ ] Edit note text
- [ ] Insert new paragraph at beginning
- [ ] Insert new paragraph in middle
- [ ] Insert new paragraph at end
- [ ] Delete paragraph at various positions
- [ ] Verify ordinal positions after insert/delete
- [ ] Auto-save functionality
- [ ] Manual save
- [ ] Switch between projects
- [ ] Export complete project
- [ ] Export single chapter
- [ ] Verify exported files match expected format
- [ ] Delete uploaded file
- [ ] Replace uploaded file
- [ ] Delete project

---

## 10. FUTURE ENHANCEMENTS (Phase 2)

### 10.1 eBook Generation
- Combine cover image + manuscript text → EPUB format
- Include META as endnotes or appendix
- Style template selection
- Download or email generated eBook

### 10.2 Collaboration Features
- Track changes per user
- Comment threads on paragraphs
- Version history with rollback
- Merge conflict resolution

### 10.3 Advanced Editor Features
- Rich text editing (bold, italic, formatting)
- Search within manuscript
- Find & replace across all paragraphs
- Word count statistics per paragraph/chapter/book
- Style guide checker (flag passive voice, adverbs, etc.)

### 10.4 AI Integration
- Auto-generate META notes for new paragraphs
- Suggest improvements based on context
- Consistency checker (character names, plot points)

---

## 11. DEPLOYMENT CHECKLIST

### Pre-Deployment
- [ ] Database schema scripts tested on dev/staging
- [ ] File upload directory created with proper permissions
- [ ] Connection strings configured
- [ ] Authentication integration tested
- [ ] All unit tests passing
- [ ] Security audit completed

### Deployment Steps
1. Backup existing database
2. Run schema migration scripts
3. Deploy application files
4. Create `/BookEditorFiles/` directory structure
5. Set folder permissions (IUSR read/write)
6. Update web.config with production settings
7. Test authentication flow
8. Test file upload/import with sample data
9. Verify export functionality

### Post-Deployment
- [ ] Monitor error logs for first 24 hours
- [ ] Verify auto-save is working
- [ ] Test performance with large manuscripts (10+ chapters)
- [ ] User acceptance testing
- [ ] Create admin documentation

---

## 12. TECHNICAL NOTES FOR CLAUDE CODE

### 12.1 Entity Framework Setup
```csharp
public class BookEditorContext : DbContext
{
    public DbSet<BookProject> BookProjects { get; set; }
    public DbSet<Chapter> Chapters { get; set; }
    public DbSet<Paragraph> Paragraphs { get; set; }
    public DbSet<MetaNote> MetaNotes { get; set; }
    public DbSet<EditNote> EditNotes { get; set; }
    
    protected override void OnModelCreating(DbModelBuilder modelBuilder)
    {
        // Configure cascade deletes
        modelBuilder.Entity<Chapter>()
            .HasRequired(c => c.BookProject)
            .WithMany(b => b.Chapters)
            .HasForeignKey(c => c.BookProjectID)
            .WillCascadeOnDelete(true);
            
        modelBuilder.Entity<Paragraph>()
            .HasRequired(p => p.Chapter)
            .WithMany(c => c.Paragraphs)
            .HasForeignKey(p => p.ChapterID)
            .WillCascadeOnDelete(true);
            
        // Unique constraints
        modelBuilder.Entity<Paragraph>()
            .HasIndex(p => new { p.ChapterID, p.UniqueID })
            .IsUnique();
    }
}
```

### 12.2 File Parsing Regex Patterns
```csharp
// Extract UniqueID + text from [[ID]] format
Regex paragraphPattern = new Regex(@"\[\[([A-Z0-9-]+)\]\]\s*(.*?)(?=\[\[|$)", RegexOptions.Singleline);

// Extract pipe-delimited notes
Regex notePattern = new Regex(@"^([A-Z0-9-]+)\|(.*)$", RegexOptions.Multiline);

// Extract chapter number from filename
Regex chapterNumberPattern = new Regex(@"ch(\d{2})_", RegexOptions.IgnoreCase);
```

### 12.3 Transaction Pattern for Insert/Delete
```csharp
using (var transaction = db.Database.BeginTransaction())
{
    try
    {
        // Perform ordinal updates
        // Insert/delete paragraph
        // Update related tables
        
        db.SaveChanges();
        transaction.Commit();
    }
    catch (Exception ex)
    {
        transaction.Rollback();
        // Log error and inform user
    }
}
```

---

## 13. OPEN QUESTIONS / DECISIONS NEEDED

1. **META Note Editability**: Should META notes be editable in the interface, or strictly read-only?
   - Current spec: Read-only (reflects AI's original intent)
   - Alternative: Editable (user may want to update as story evolves)

2. **Multi-User Scenarios**: Will multiple users edit the same book simultaneously?
   - If yes: Need optimistic concurrency control, conflict resolution
   - If no: Single-user locking sufficient

3. **Export Scheduling**: Should exports be generated on-demand only, or also auto-exported periodically?
   - Current spec: On-demand only
   - Alternative: Nightly backup exports to archive folder

4. **Chapter Metadata Editing**: Can user edit POV, Setting, Chapter Purpose in the interface?
   - Not specified - recommend allowing editing via separate modal/form

5. **Search Functionality**: Should there be full-text search across all paragraphs?
   - Phase 2 enhancement, or include in Phase 1?

---

## 14. SUCCESS CRITERIA

The Book Editor will be considered complete and successful when:

1. ✅ User can create multiple book projects and switch between them
2. ✅ User can upload chapter, META, and NOTE files for each project
3. ✅ Files are correctly parsed and imported to database with ordinal positions
4. ✅ User can navigate paragraph-by-paragraph with keyboard and mouse
5. ✅ Edits to paragraph text and notes are saved (auto-save and manual)
6. ✅ Insert paragraph creates new record at correct position and reorders ordinals
7. ✅ Delete paragraph removes record and reorders remaining ordinals
8. ✅ Export generates correct markdown files matching original format
9. ✅ Cover images are uploaded and displayed as thumbnails
10. ✅ All file CRUD operations work correctly (upload, replace, delete)
11. ✅ No data loss during normal operations
12. ✅ Interface is responsive and performs well with 10+ chapters, 500+ paragraphs
13. ✅ Integration with existing Seonyx admin authentication works seamlessly

---

## 15. PROJECT TIMELINE ESTIMATE

**Phase 1: Core Editor (Estimated 40-60 hours)**
- Database schema & Entity Framework setup: 6-8 hours
- File upload & storage infrastructure: 4-6 hours
- File parsing & import logic: 10-12 hours
- Editor interface & navigation: 12-16 hours
- Insert/Delete with ordinal management: 6-8 hours
- Auto-save & manual save: 4-6 hours
- Export functionality: 6-8 hours
- Testing & bug fixes: 8-10 hours

**Phase 2: Polish & Integration (Estimated 10-15 hours)**
- Cover image upload & thumbnails: 3-4 hours
- File CRUD interface: 4-6 hours
- Authentication integration: 2-3 hours
- UI refinement & responsiveness: 3-4 hours

**Phase 3: Future Enhancements (TBD)**
- eBook generation: 15-20 hours
- Collaboration features: 30-40 hours
- Advanced editor features: 20-30 hours

---

## 16. APPENDIX: EXAMPLE DATA FLOW

### Example: User Edits Paragraph 123

**Request:**
```
POST /Editor/SaveParagraph
{
    paragraphId: 123,
    paragraphText: "The seminar room smelled like dry-erase markers...",
    editNoteText: "Fixed the emdash issue here."
}
```

**Database Operations:**
```sql
UPDATE Paragraphs 
SET ParagraphText = @ParagraphText, LastModifiedDate = GETDATE()
WHERE ParagraphID = 123;

UPDATE EditNotes
SET NoteText = @EditNoteText, LastModifiedDate = GETDATE()
WHERE ParagraphID = 123;
```

**Response:**
```json
{
    "success": true,
    "timestamp": "2024-02-16T14:23:45",
    "paragraphId": 123
}
```

### Example: User Inserts Paragraph After #123

**Request:**
```
POST /Editor/InsertParagraph
{
    currentParagraphId: 123
}
```

**Database Operations:**
```sql
-- Get context
SELECT ChapterID, OrdinalPosition 
FROM Paragraphs WHERE ParagraphID = 123;
-- Returns: ChapterID=5, OrdinalPosition=45

-- Generate new UniqueID: C05-A1B2C3D4E5

-- Shift ordinals
UPDATE Paragraphs 
SET OrdinalPosition = OrdinalPosition + 1
WHERE ChapterID = 5 AND OrdinalPosition > 45;

-- Insert new paragraph
INSERT INTO Paragraphs (ChapterID, UniqueID, OrdinalPosition, ParagraphText)
VALUES (5, 'C05-A1B2C3D4E5', 46, '');

-- Get new ID
DECLARE @NewParagraphID = SCOPE_IDENTITY();

-- Insert blank META and NOTE
INSERT INTO MetaNotes (ParagraphID, UniqueID, MetaText)
VALUES (@NewParagraphID, 'C05-A1B2C3D4E5', '');

INSERT INTO EditNotes (ParagraphID, UniqueID, NoteText)
VALUES (@NewParagraphID, 'C05-A1B2C3D4E5', '');
```

**Response:**
```json
{
    "success": true,
    "newParagraphId": 456,
    "uniqueId": "C05-A1B2C3D4E5",
    "ordinalPosition": 46,
    "redirectUrl": "/Editor?paragraphId=456"
}
```

---

END OF SPECIFICATION
