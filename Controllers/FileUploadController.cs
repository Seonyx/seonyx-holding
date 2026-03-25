using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Seonyx.Web.Models;
using Seonyx.Web.Models.ViewModels.BookEditor;
using Seonyx.Web.Services;

namespace Seonyx.Web.Controllers
{
    public class FileUploadController : Controller
    {
        private SeonyxContext db = new SeonyxContext();
        private BookFileParser parser = new BookFileParser();

        // ====================================================================
        // Import progress tracking (shared with ImportProgressController)
        // ====================================================================

        public class ImportProgressState
        {
            // volatile backing fields ensure cross-thread visibility when the
            // background import thread writes and the poll thread reads.
            private volatile int  _total;
            private volatile int  _done;
            private volatile int  _paragraphsWritten;
            private volatile bool _isComplete;
            private volatile bool _success;

            public int  Total             { get { return _total;             } set { _total             = value; } }
            public int  Done              { get { return _done;              } set { _done              = value; } }
            public int  ParagraphsWritten { get { return _paragraphsWritten; } set { _paragraphsWritten = value; } }
            public bool IsComplete        { get { return _isComplete;        } set { _isComplete        = value; } }
            public bool Success           { get { return _success;           } set { _success           = value; } }

            public string CurrentChapter { get; set; } = "";
            public string Message        { get; set; } = "";
            public string LogUrl         { get; set; } = "";

            private readonly System.Collections.Concurrent.ConcurrentQueue<string> _log
                = new System.Collections.Concurrent.ConcurrentQueue<string>();

            public void AddLine(string line)
            {
                _log.Enqueue(line);
                string discard;
                while (_log.Count > 8) _log.TryDequeue(out discard);
            }

            public string[] RecentLines { get { return _log.ToArray(); } }
        }

        public static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ImportProgressState>
            ActiveImports = new System.Collections.Concurrent.ConcurrentDictionary<string, ImportProgressState>();

        public ActionResult Index(int projectId)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            var project = db.BookProjects.Find(projectId);
            if (project == null) return HttpNotFound();

            var model = new FileUploadViewModel
            {
                BookProjectID  = project.BookProjectID,
                ProjectName    = project.ProjectName,
                CoverImagePath = project.CoverImagePath
            };

            // List legacy text uploads
            var uploadsPath = Path.Combine(project.FolderPath, "uploads");
            if (Directory.Exists(uploadsPath))
            {
                foreach (var file in Directory.GetFiles(uploadsPath).Select(f => new FileInfo(f)).OrderBy(f => f.Name))
                {
                    model.ExistingFiles.Add(new UploadedFileInfo
                    {
                        FileName   = file.Name,
                        FileType   = parser.ClassifyFile(file.Name),
                        FileSize   = file.Length,
                        UploadDate = file.LastWriteTime
                    });
                }
            }

            // Show BookML package status if one has been uploaded
            var bookXmlPath = FindBookXml(project.FolderPath);
            if (bookXmlPath != null)
            {
                model.BookmlPackagePresent = true;
                model.BookmlPackagePath    = bookXmlPath;
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Upload(int projectId, IEnumerable<HttpPostedFileBase> files)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            var project = db.BookProjects.Find(projectId);
            if (project == null) return HttpNotFound();

            var allowedTextExtensions = new[] { ".md", ".txt" };
            int uploaded = 0;

            if (files != null)
            {
                foreach (var file in files)
                {
                    if (file == null || file.ContentLength == 0) continue;

                    if (file.ContentLength > 10 * 1024 * 1024)
                    {
                        TempData["Error"] = string.Format(
                            "File \"{0}\" exceeds the 10 MB size limit.", file.FileName);
                        return RedirectToAction("Index", new { projectId });
                    }

                    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

                    if (ext == ".zip")
                    {
                        // BookML package: unzip to bookml/ preserving folder structure
                        var error = UnpackBookmlZip(file, project.FolderPath);
                        if (error != null)
                        {
                            TempData["Error"] = error;
                            return RedirectToAction("Index", new { projectId });
                        }
                        uploaded++;
                    }
                    else if (allowedTextExtensions.Contains(ext))
                    {
                        // Legacy text file
                        var uploadsPath = Path.Combine(project.FolderPath, "uploads");
                        Directory.CreateDirectory(uploadsPath);
                        var safeFileName = Path.GetFileName(file.FileName);
                        file.SaveAs(Path.Combine(uploadsPath, safeFileName));
                        uploaded++;
                    }
                    else
                    {
                        TempData["Error"] = string.Format(
                            "File \"{0}\" has an unsupported extension. " +
                            "Upload a BookML .zip package, or .md/.txt text files.",
                            file.FileName);
                        return RedirectToAction("Index", new { projectId });
                    }
                }
            }

            if (uploaded > 0)
                TempData["Message"] = string.Format("{0} file(s) uploaded successfully.", uploaded);

            return RedirectToAction("Index", new { projectId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteFile(int projectId, string fileName)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            var project = db.BookProjects.Find(projectId);
            if (project == null) return HttpNotFound();

            if (fileName.Contains("..") || Path.IsPathRooted(fileName))
                return new HttpStatusCodeResult(400, "Invalid filename");

            var safeFileName = Path.GetFileName(fileName);
            var filePath = Path.Combine(project.FolderPath, "uploads", safeFileName);

            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
                TempData["Message"] = string.Format("File \"{0}\" deleted.", safeFileName);
            }

            return RedirectToAction("Index", new { projectId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ImportFiles(int projectId)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            var project = db.BookProjects.Find(projectId);
            if (project == null) return HttpNotFound();

            // ----------------------------------------------------------------
            // BookML XML path: book.xml present in the bookml/ subfolder
            // ----------------------------------------------------------------
            var bookXmlPath = FindBookXml(project.FolderPath);
            if (bookXmlPath != null)
                return ImportBookml(projectId, project, bookXmlPath);

            // ----------------------------------------------------------------
            // Legacy text-file path
            // ----------------------------------------------------------------
            var uploadsPath = Path.Combine(project.FolderPath, "uploads");
            if (!Directory.Exists(uploadsPath))
            {
                TempData["Error"] = "No upload folder found. Upload files first.";
                return RedirectToAction("Index", new { projectId });
            }

            return ImportLegacyTextFiles(projectId, project, uploadsPath);
        }

        // ====================================================================
        // BookML import — AJAX entry point (returns progress token)
        // ====================================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult StartImport(int projectId)
        {
            if (!IsAuthenticated())
                return Json(new { error = "Unauthorized" });

            var project = db.BookProjects.Find(projectId);
            if (project == null)
                return Json(new { error = "Project not found" });

            var bookXmlPath = FindBookXml(project.FolderPath);
            if (bookXmlPath == null)
                return Json(new { error = "No BookML package found. Upload a .zip first." });

            var xsdBasePath    = Server.MapPath("~/App_Data/BookML");
            var importer       = new BookmlImporter(xsdBasePath);
            var bookmlRootDir  = Path.Combine(project.FolderPath, "bookml");
            var markerPath     = Path.Combine(bookmlRootDir, ".zipname");
            var sourceFileName = System.IO.File.Exists(markerPath)
                ? System.IO.File.ReadAllText(markerPath).Trim()
                : null;

            // Phase 1: validate synchronously before touching the DB
            var validationErrors = importer.Validate(bookXmlPath);
            if (validationErrors.Any())
            {
                WriteImportLog(projectId, false, null, validationErrors, sourceFileName);
                var logUrl2 = Url.Action("Index", "ImportLog", new { projectId });
                return Json(new { error = string.Format(
                    "Validation failed: {0} error(s). <a href='{1}'>View log</a>",
                    validationErrors.Count, logUrl2) });
            }

            var chapterCount = GetChapterCountFromBook(bookXmlPath);

            var token = Guid.NewGuid().ToString("N").Substring(0, 16);
            var state = new ImportProgressState { Total = chapterCount };
            ActiveImports[token] = state;

            // Capture everything needed in the background thread before the request ends
            var capturedBookXmlPath    = bookXmlPath;
            var capturedXsdBasePath    = xsdBasePath;
            var capturedProjectId      = projectId;
            var capturedSourceFileName = sourceFileName;
            var capturedLogUrl         = Url.Action("Index", "ImportLog", new { projectId });

            System.Web.Hosting.HostingEnvironment.QueueBackgroundWorkItem(ct =>
            {
                try
                {
                    var bgImporter = new BookmlImporter(capturedXsdBasePath);
                    ImportResult bgResult;
                    using (var importDb = new SeonyxContext())
                    {
                        bgResult = bgImporter.Import(importDb, capturedProjectId, capturedBookXmlPath,
                            (done, total, chapterTitle, parasAdded, parasUpdated) =>
                            {
                                state.Done               = done;
                                state.CurrentChapter     = chapterTitle;
                                state.ParagraphsWritten  = parasAdded + parasUpdated;
                                state.AddLine(string.Format(
                                    "Ch. {0}/{1}: {2} ({3} added, {4} updated)",
                                    done, total, chapterTitle, parasAdded, parasUpdated));
                            });
                    }
                    using (var logDb = new SeonyxContext())
                    {
                        WriteImportLogToDb(logDb, capturedProjectId, bgResult.Success,
                            bgResult, null, capturedSourceFileName);
                    }
                    state.Success = bgResult.Success;
                    state.Message = bgResult.Success
                        ? bgResult.Summary()
                        : string.Format("Import failed: {0}", string.Join("; ", bgResult.Errors));
                    state.LogUrl  = capturedLogUrl;
                    state.Done    = bgResult.ChaptersProcessed;
                }
                catch (Exception ex)
                {
                    state.Success = false;
                    state.Message = "Import failed: " + ex.Message;
                }
                finally
                {
                    state.IsComplete = true;
                }
            });

            return Json(new { token, total = chapterCount });
        }

        private int GetChapterCountFromBook(string bookXmlPath)
        {
            try
            {
                var ns  = System.Xml.Linq.XNamespace.Get("https://bookml.org/ns/1.0");
                var doc = System.Xml.Linq.XDocument.Load(bookXmlPath);
                int count = 0;
                var contentsEl = doc.Root.Element(ns + "contents");
                if (contentsEl == null) return 0;
                foreach (var matterName in new[] { "frontmatter", "bodymatter", "backmatter" })
                {
                    var matterEl = contentsEl.Element(ns + matterName);
                    if (matterEl != null)
                        count += matterEl.Elements(ns + "component").Count();
                }
                return count;
            }
            catch { return 0; }
        }

        // ====================================================================
        // BookML import — synchronous helper (called from background task)
        // ====================================================================

        private ActionResult ImportBookml(int projectId, BookProject project, string bookXmlPath)
        {
            var xsdBasePath = Server.MapPath("~/App_Data/BookML");
            var importer    = new BookmlImporter(xsdBasePath);

            // Read the original zip filename recorded at upload time
            var bookmlDir   = Path.GetDirectoryName(bookXmlPath);
            var markerPath  = Path.Combine(bookmlDir, ".zipname");
            var sourceFileName = System.IO.File.Exists(markerPath)
                ? System.IO.File.ReadAllText(markerPath).Trim()
                : null;

            // Phase 1: validate everything before touching the DB
            var validationErrors = importer.Validate(bookXmlPath);
            if (validationErrors.Any())
            {
                WriteImportLog(projectId, success: false, result: null, extraLines: validationErrors, sourceFileName: sourceFileName);
                TempData["Error"] = string.Format(
                    "BookML validation failed — no data was imported. {0} error(s). {1}",
                    validationErrors.Count, ImportLogLink(projectId));
                return RedirectToAction("Index", new { projectId });
            }

            // Phase 2: import — use a dedicated context so a failed/rolled-back import
            // never leaves stale tracked entities in the controller's db context.
            // WriteImportLog must always use a clean context to avoid FK errors.
            ImportResult result;
            using (var importDb = new SeonyxContext())
            {
                result = importer.Import(importDb, projectId, bookXmlPath);
            }
            WriteImportLog(projectId, result.Success, result, extraLines: null, sourceFileName: sourceFileName);

            if (result.Success)
            {
                var message = result.Summary();
                if (result.Warnings.Any())
                    message += string.Format(" {0} warning(s). {1}", result.Warnings.Count, ImportLogLink(projectId));
                TempData["Message"] = message;
            }
            else
            {
                TempData["Error"] = string.Format(
                    "BookML import failed. {0} error(s). {1}",
                    result.Errors.Count, ImportLogLink(projectId));
            }

            return RedirectToAction("Index", new { projectId });
        }

        private void WriteImportLog(int projectId, bool success, Services.ImportResult result,
            System.Collections.Generic.List<string> extraLines, string sourceFileName = null)
        {
            WriteImportLogToDb(db, projectId, success, result, extraLines, sourceFileName);
        }

        private static void WriteImportLogToDb(SeonyxContext logDb, int projectId, bool success,
            Services.ImportResult result, System.Collections.Generic.List<string> extraLines,
            string sourceFileName = null)
        {
            var log = new ImportLog
            {
                BookProjectID     = projectId,
                ImportedAt        = DateTime.Now,
                Success           = success,
                SourceFileName    = sourceFileName,
                DraftNumber       = result != null ? result.DraftNumber       : 0,
                ChaptersProcessed = result != null ? result.ChaptersProcessed : 0,
                ParagraphsAdded   = result != null ? result.ParagraphsAdded   : 0,
                ParagraphsUpdated = result != null ? result.ParagraphsUpdated : 0,
                ParagraphsRemoved = result != null ? result.ParagraphsRemoved : 0,
                VersionsRecorded  = result != null ? result.VersionsRecorded  : 0,
                WarningCount      = result != null ? result.Warnings.Count    : 0,
                ErrorCount        = result != null ? result.Errors.Count      : (extraLines != null ? extraLines.Count : 0)
            };

            var lines = new System.Collections.Generic.List<string>();
            if (result != null)
            {
                lines.AddRange(result.Warnings);
                lines.AddRange(result.Errors);
            }
            if (extraLines != null)
                lines.AddRange(extraLines);

            log.FullLog = lines.Any() ? string.Join("\n", lines) : null;

            logDb.ImportLogs.Add(log);
            logDb.SaveChanges();
        }

        private string ImportLogLink(int projectId)
        {
            var url = Url.Action("Index", "ImportLog", new { projectId });
            return string.Format("<a href=\"{0}\">View import log</a>", url);
        }

        // ====================================================================
        // Legacy text-file import (original logic, unchanged)
        // ====================================================================

        private ActionResult ImportLegacyTextFiles(int projectId, BookProject project, string uploadsPath)
        {
            var allFiles = Directory.GetFiles(uploadsPath).Select(f => new FileInfo(f)).ToList();

            var chapterFiles = allFiles.Where(f => parser.ClassifyFile(f.Name) == "Chapter").OrderBy(f => f.Name).ToList();
            var metaFiles    = allFiles.Where(f => parser.ClassifyFile(f.Name) == "Meta").OrderBy(f => f.Name).ToList();
            var notesFiles   = allFiles.Where(f => parser.ClassifyFile(f.Name) == "Notes").OrderBy(f => f.Name).ToList();

            if (chapterFiles.Count == 0)
            {
                TempData["Error"] = "No chapter files found. Upload chapter files (e.g., ch01_title.md) first.";
                return RedirectToAction("Index", new { projectId });
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var warnings = new List<string>();
                    int added = 0, updated = 0, removed = 0, chaptersProcessed = 0;

                    foreach (var chapterFile in chapterFiles)
                    {
                        var content = System.IO.File.ReadAllText(chapterFile.FullName);
                        var parsed  = parser.ParseChapterFile(content, chapterFile.Name);

                        if (parsed.ChapterNumber == 0)
                        {
                            warnings.Add(string.Format("Could not determine chapter number from \"{0}\". Skipped.", chapterFile.Name));
                            continue;
                        }

                        var chapterNum = parsed.ChapterNumber;

                        var metaDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        var matchingMeta = metaFiles.FirstOrDefault(f =>
                        {
                            var num = parser.ExtractChapterNumberFromFilename(f.Name);
                            return num.HasValue && num.Value == chapterNum;
                        });
                        if (matchingMeta != null)
                            metaDict = parser.ParseMetaFile(System.IO.File.ReadAllText(matchingMeta.FullName));

                        var notesDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        var matchingNotes = notesFiles.FirstOrDefault(f =>
                        {
                            var num = parser.ExtractChapterNumberFromFilename(f.Name);
                            return num.HasValue && num.Value == chapterNum;
                        });
                        if (matchingNotes != null)
                            notesDict = parser.ParseNotesFile(System.IO.File.ReadAllText(matchingNotes.FullName));

                        var existingChapter = db.Chapters.FirstOrDefault(
                            c => c.BookProjectID == projectId && c.ChapterNumber == chapterNum);

                        if (existingChapter == null)
                        {
                            var chapter = new Chapter
                            {
                                BookProjectID  = projectId,
                                ChapterNumber  = parsed.ChapterNumber,
                                ChapterTitle   = parsed.ChapterTitle,
                                POV            = parsed.POV,
                                Setting        = parsed.Setting,
                                ChapterPurpose = parsed.ChapterPurpose,
                                SourceFileName = parsed.SourceFileName
                            };
                            db.Chapters.Add(chapter);
                            db.SaveChanges();

                            foreach (var pp in parsed.Paragraphs)
                            {
                                var paragraph = new Paragraph
                                {
                                    ChapterID        = chapter.ChapterID,
                                    UniqueID         = pp.UniqueID,
                                    OrdinalPosition  = pp.OrdinalPosition,
                                    ParagraphText    = pp.Text,
                                    CreatedDate      = DateTime.Now,
                                    LastModifiedDate = DateTime.Now
                                };
                                db.Paragraphs.Add(paragraph);
                                db.SaveChanges();

                                string metaText; metaDict.TryGetValue(pp.UniqueID, out metaText);
                                db.MetaNotes.Add(new MetaNote { ParagraphID = paragraph.ParagraphID, UniqueID = pp.UniqueID, MetaText = metaText ?? "" });

                                string noteText; notesDict.TryGetValue(pp.UniqueID, out noteText);
                                db.EditNotes.Add(new EditNote { ParagraphID = paragraph.ParagraphID, UniqueID = pp.UniqueID, NoteText = noteText, LastModifiedDate = DateTime.Now });

                                added++;
                            }
                            db.SaveChanges();
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(parsed.ChapterTitle))   existingChapter.ChapterTitle   = parsed.ChapterTitle;
                            if (!string.IsNullOrEmpty(parsed.POV))            existingChapter.POV            = parsed.POV;
                            if (!string.IsNullOrEmpty(parsed.Setting))        existingChapter.Setting        = parsed.Setting;
                            if (!string.IsNullOrEmpty(parsed.ChapterPurpose)) existingChapter.ChapterPurpose = parsed.ChapterPurpose;
                            existingChapter.SourceFileName = parsed.SourceFileName;

                            var incomingIds   = new HashSet<string>(parsed.Paragraphs.Select(p => p.UniqueID), StringComparer.OrdinalIgnoreCase);
                            var existingParas = db.Paragraphs.Where(p => p.ChapterID == existingChapter.ChapterID).ToList();

                            foreach (var p in existingParas.Where(p => !incomingIds.Contains(p.UniqueID)))
                            {
                                db.Paragraphs.Remove(p);
                                removed++;
                            }
                            db.SaveChanges();

                            foreach (var pp in parsed.Paragraphs)
                            {
                                var existingPara = existingParas.FirstOrDefault(
                                    p => string.Equals(p.UniqueID, pp.UniqueID, StringComparison.OrdinalIgnoreCase));

                                if (existingPara != null)
                                {
                                    existingPara.ParagraphText    = pp.Text;
                                    existingPara.OrdinalPosition  = pp.OrdinalPosition;
                                    existingPara.LastModifiedDate = DateTime.Now;

                                    string metaText;
                                    if (metaDict.TryGetValue(pp.UniqueID, out metaText))
                                    {
                                        var m = db.MetaNotes.FirstOrDefault(x => x.ParagraphID == existingPara.ParagraphID);
                                        if (m != null) m.MetaText = metaText;
                                        else db.MetaNotes.Add(new MetaNote { ParagraphID = existingPara.ParagraphID, UniqueID = pp.UniqueID, MetaText = metaText });
                                    }

                                    string noteText;
                                    if (notesDict.TryGetValue(pp.UniqueID, out noteText))
                                    {
                                        var n = db.EditNotes.FirstOrDefault(x => x.ParagraphID == existingPara.ParagraphID);
                                        if (n != null) { n.NoteText = noteText; n.LastModifiedDate = DateTime.Now; }
                                        else db.EditNotes.Add(new EditNote { ParagraphID = existingPara.ParagraphID, UniqueID = pp.UniqueID, NoteText = noteText, LastModifiedDate = DateTime.Now });
                                    }
                                    updated++;
                                }
                                else
                                {
                                    var paragraph = new Paragraph
                                    {
                                        ChapterID        = existingChapter.ChapterID,
                                        UniqueID         = pp.UniqueID,
                                        OrdinalPosition  = pp.OrdinalPosition,
                                        ParagraphText    = pp.Text,
                                        CreatedDate      = DateTime.Now,
                                        LastModifiedDate = DateTime.Now
                                    };
                                    db.Paragraphs.Add(paragraph);
                                    db.SaveChanges();

                                    string metaText; metaDict.TryGetValue(pp.UniqueID, out metaText);
                                    db.MetaNotes.Add(new MetaNote { ParagraphID = paragraph.ParagraphID, UniqueID = pp.UniqueID, MetaText = metaText ?? "" });

                                    string noteText; notesDict.TryGetValue(pp.UniqueID, out noteText);
                                    db.EditNotes.Add(new EditNote { ParagraphID = paragraph.ParagraphID, UniqueID = pp.UniqueID, NoteText = noteText, LastModifiedDate = DateTime.Now });
                                    added++;
                                }
                            }
                            db.SaveChanges();
                        }

                        chaptersProcessed++;

                        if (matchingMeta != null)
                        {
                            var paragraphIds = new HashSet<string>(parsed.Paragraphs.Select(p => p.UniqueID), StringComparer.OrdinalIgnoreCase);
                            var unmatched = metaDict.Keys.Where(k => !paragraphIds.Contains(k)).ToList();
                            if (unmatched.Count > 0)
                                warnings.Add(string.Format("Chapter {0}: {1} META entries had no matching paragraph.", chapterNum, unmatched.Count));
                        }
                    }

                    project.LastModifiedDate = DateTime.Now;
                    db.SaveChanges();
                    transaction.Commit();

                    var parts = new List<string> { string.Format("{0} chapter(s) processed", chaptersProcessed) };
                    if (added   > 0) parts.Add(string.Format("{0} paragraphs added",   added));
                    if (updated > 0) parts.Add(string.Format("{0} paragraphs updated", updated));
                    if (removed > 0) parts.Add(string.Format("{0} paragraphs removed", removed));
                    var message = "Import complete: " + string.Join(", ", parts) + ".";

                    // Write log entry
                    var legacyLog = new ImportLog
                    {
                        BookProjectID     = projectId,
                        ImportedAt        = DateTime.Now,
                        Success           = true,
                        ParagraphsAdded   = added,
                        ParagraphsUpdated = updated,
                        ParagraphsRemoved = removed,
                        ChaptersProcessed = chaptersProcessed,
                        WarningCount      = warnings.Count,
                        FullLog           = warnings.Any() ? string.Join("\n", warnings) : null
                    };
                    db.ImportLogs.Add(legacyLog);
                    db.SaveChanges();

                    if (warnings.Any())
                        message += string.Format(" {0} warning(s). {1}", warnings.Count, ImportLogLink(projectId));
                    TempData["Message"] = message;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    TempData["Error"] = "Import failed: " + ex.Message;
                }
            }

            return RedirectToAction("Index", new { projectId });
        }

        // ====================================================================
        // Helpers
        // ====================================================================

        /// <summary>
        /// Finds book.xml in {projectFolder}/bookml/ or one level deeper
        /// (handles the case where the user zipped the containing folder).
        /// Returns null if no BookML package is present.
        /// </summary>
        private string FindBookXml(string projectFolderPath)
        {
            var bookmlDir  = Path.Combine(projectFolderPath, "bookml");
            var directPath = Path.Combine(bookmlDir, "book.xml");
            if (System.IO.File.Exists(directPath))
                return directPath;

            if (Directory.Exists(bookmlDir))
            {
                foreach (var subDir in Directory.GetDirectories(bookmlDir))
                {
                    var nested = Path.Combine(subDir, "book.xml");
                    if (System.IO.File.Exists(nested))
                        return nested;
                }
            }

            return null;
        }

        /// <summary>
        /// Extracts a BookML ZIP to {projectFolder}/bookml/.
        /// Returns an error message string on failure, null on success.
        /// </summary>
        private string UnpackBookmlZip(HttpPostedFileBase zipFile, string projectFolderPath)
        {
            var bookmlDir = Path.Combine(projectFolderPath, "bookml");

            // Wipe the existing bookml directory before extracting so stale files
            // from a previous ZIP (different folder structure) cannot be picked up.
            if (Directory.Exists(bookmlDir))
            {
                foreach (var file in Directory.GetFiles(bookmlDir, "*", SearchOption.AllDirectories))
                    System.IO.File.Delete(file);
                foreach (var dir in Directory.GetDirectories(bookmlDir).OrderByDescending(d => d.Length))
                    Directory.Delete(dir, recursive: true);
            }

            Directory.CreateDirectory(bookmlDir);
            var bookmlDirNorm = Path.GetFullPath(bookmlDir);

            try
            {
                using (var zip = new ZipArchive(zipFile.InputStream, ZipArchiveMode.Read))
                {
                    foreach (var entry in zip.Entries)
                    {
                        if (entry.FullName.Contains("..") || Path.IsPathRooted(entry.FullName))
                            return string.Format("ZIP rejected: entry \"{0}\" contains a path traversal sequence.", entry.FullName);

                        if (string.IsNullOrEmpty(entry.Name)) continue; // directory entry

                        var destPath = Path.GetFullPath(
                            Path.Combine(bookmlDir, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));

                        if (!destPath.StartsWith(bookmlDirNorm, StringComparison.OrdinalIgnoreCase))
                            return string.Format("ZIP rejected: entry \"{0}\" would extract outside the target folder.", entry.FullName);

                        Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                        entry.ExtractToFile(destPath, overwrite: true);
                    }
                }
            }
            catch (Exception ex)
            {
                return "Failed to unpack ZIP: " + ex.Message;
            }

            // Record the original zip filename so the import log can display it
            var markerPath = Path.Combine(bookmlDir, ".zipname");
            System.IO.File.WriteAllText(markerPath, Path.GetFileName(zipFile.FileName));

            return null;
        }

        private bool IsAuthenticated()
        {
            return User.Identity.IsAuthenticated;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
