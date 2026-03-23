using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using Seonyx.Web.Models;
using Seonyx.Web.Models.ViewModels.BookEditor;
using Seonyx.Web.Services;

namespace Seonyx.Web.Controllers
{
    public class EditorController : Controller
    {
        private SeonyxContext db = new SeonyxContext();
        private BookFileParser parser = new BookFileParser();

        public ActionResult Index(int projectId, int? paragraphId)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            var project = db.BookProjects.Find(projectId);
            if (project == null) return HttpNotFound();

            // If no paragraph specified, get the first one
            Paragraph paragraph;
            if (paragraphId.HasValue)
            {
                paragraph = db.Paragraphs
                    .Include(p => p.Chapter)
                    .FirstOrDefault(p => p.ParagraphID == paragraphId.Value && p.Chapter.BookProjectID == projectId);
            }
            else
            {
                paragraph = db.Paragraphs
                    .Include(p => p.Chapter)
                    .Where(p => p.Chapter.BookProjectID == projectId)
                    .OrderBy(p => p.Chapter.ChapterNumber)
                    .ThenBy(p => p.OrdinalPosition)
                    .FirstOrDefault();
            }

            if (paragraph == null)
            {
                TempData["Error"] = "No paragraphs found. Import files first.";
                return RedirectToAction("Index", "FileUpload", new { projectId });
            }

            var model = BuildEditViewModel(project, paragraph);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult SaveParagraph(int bookProjectID, int paragraphID, string paragraphText, string metaText, string editNoteText)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            var paragraph = db.Paragraphs.Find(paragraphID);
            if (paragraph == null) return HttpNotFound();

            paragraph.ParagraphText = paragraphText ?? "";
            paragraph.LastModifiedDate = DateTime.Now;

            var metaNote = db.MetaNotes.FirstOrDefault(m => m.ParagraphID == paragraphID);
            if (metaNote != null)
            {
                metaNote.MetaText = metaText ?? "";
            }

            var editNote = db.EditNotes.FirstOrDefault(e => e.ParagraphID == paragraphID);
            if (editNote != null)
            {
                editNote.NoteText = editNoteText;
                editNote.LastModifiedDate = DateTime.Now;
            }

            db.SaveChanges();

            TempData["Message"] = "Changes saved.";
            return RedirectToAction("Index", new { projectId = bookProjectID, paragraphId = paragraphID });
        }

        [HttpPost]
        public JsonResult AutoSave(int paragraphId, string paragraphText, string metaText, string editNoteText)
        {
            if (!IsAuthenticated())
                return Json(new { success = false, message = "Not authenticated" });

            try
            {
                var paragraph = db.Paragraphs.Find(paragraphId);
                if (paragraph == null)
                    return Json(new { success = false, message = "Paragraph not found" });

                paragraph.ParagraphText = paragraphText ?? "";
                paragraph.LastModifiedDate = DateTime.Now;

                var metaNote = db.MetaNotes.FirstOrDefault(m => m.ParagraphID == paragraphId);
                if (metaNote != null)
                {
                    metaNote.MetaText = metaText ?? "";
                }

                var editNote = db.EditNotes.FirstOrDefault(e => e.ParagraphID == paragraphId);
                if (editNote != null)
                {
                    editNote.NoteText = editNoteText;
                    editNote.LastModifiedDate = DateTime.Now;
                }

                db.SaveChanges();
                return Json(new { success = true, timestamp = DateTime.Now.ToString("h:mm:ss tt") });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult InsertParagraph(int projectId, int currentParagraphId, bool before = false)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            var current = db.Paragraphs.Include(p => p.Chapter).FirstOrDefault(p => p.ParagraphID == currentParagraphId);
            if (current == null) return HttpNotFound();

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    int newOrdinal;
                    int savedOrdinal = current.OrdinalPosition;
                    if (before)
                    {
                        // Shift current and everything after it down
                        var toShift = db.Paragraphs
                            .Where(p => p.ChapterID == current.ChapterID && p.OrdinalPosition >= savedOrdinal)
                            .ToList();
                        foreach (var p in toShift)
                        {
                            p.OrdinalPosition++;
                        }
                        newOrdinal = savedOrdinal; // original position before shift
                    }
                    else
                    {
                        // Shift everything after current down
                        var toShift = db.Paragraphs
                            .Where(p => p.ChapterID == current.ChapterID && p.OrdinalPosition > current.OrdinalPosition)
                            .ToList();
                        foreach (var p in toShift)
                        {
                            p.OrdinalPosition++;
                        }
                        newOrdinal = current.OrdinalPosition + 1;
                    }
                    db.SaveChanges();

                    // Generate unique ID
                    var uniqueId = parser.GenerateUniqueID(current.Chapter.ChapterNumber);

                    // Insert new paragraph
                    var newParagraph = new Paragraph
                    {
                        ChapterID = current.ChapterID,
                        UniqueID = uniqueId,
                        OrdinalPosition = newOrdinal,
                        ParagraphText = "",
                        CreatedDate = DateTime.Now,
                        LastModifiedDate = DateTime.Now
                    };
                    db.Paragraphs.Add(newParagraph);
                    db.SaveChanges();

                    // Create blank META and NOTE
                    db.MetaNotes.Add(new MetaNote
                    {
                        ParagraphID = newParagraph.ParagraphID,
                        UniqueID = uniqueId,
                        MetaText = ""
                    });
                    db.EditNotes.Add(new EditNote
                    {
                        ParagraphID = newParagraph.ParagraphID,
                        UniqueID = uniqueId,
                        NoteText = "",
                        LastModifiedDate = DateTime.Now
                    });
                    db.SaveChanges();

                    transaction.Commit();

                    return RedirectToAction("Index", new { projectId, paragraphId = newParagraph.ParagraphID });
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    TempData["Error"] = "Failed to insert paragraph.";
                    return RedirectToAction("Index", new { projectId, paragraphId = currentParagraphId });
                }
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteParagraph(int projectId, int paragraphId)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            var paragraph = db.Paragraphs.Include(p => p.Chapter).FirstOrDefault(p => p.ParagraphID == paragraphId);
            if (paragraph == null) return HttpNotFound();

            int chapterId = paragraph.ChapterID;
            int deletedOrdinal = paragraph.OrdinalPosition;

            // Find where to navigate after deletion
            var nextParagraph = db.Paragraphs
                .Where(p => p.ChapterID == chapterId && p.OrdinalPosition > deletedOrdinal)
                .OrderBy(p => p.OrdinalPosition)
                .FirstOrDefault();

            var prevParagraph = db.Paragraphs
                .Where(p => p.ChapterID == chapterId && p.OrdinalPosition < deletedOrdinal)
                .OrderByDescending(p => p.OrdinalPosition)
                .FirstOrDefault();

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    // Delete paragraph (cascade deletes meta + note)
                    db.Paragraphs.Remove(paragraph);
                    db.SaveChanges();

                    // Reorder ordinals
                    var toShift = db.Paragraphs
                        .Where(p => p.ChapterID == chapterId && p.OrdinalPosition > deletedOrdinal)
                        .ToList();
                    foreach (var p in toShift)
                    {
                        p.OrdinalPosition--;
                    }
                    db.SaveChanges();

                    transaction.Commit();

                    // Navigate to next, or prev, or back to file upload if chapter is empty
                    int? navigateTo = null;
                    if (nextParagraph != null)
                        navigateTo = nextParagraph.ParagraphID;
                    else if (prevParagraph != null)
                        navigateTo = prevParagraph.ParagraphID;

                    if (navigateTo.HasValue)
                    {
                        return RedirectToAction("Index", new { projectId, paragraphId = navigateTo.Value });
                    }
                    else
                    {
                        // Chapter is now empty, go to first paragraph in project
                        var firstInProject = db.Paragraphs
                            .Where(p => p.Chapter.BookProjectID == projectId)
                            .OrderBy(p => p.Chapter.ChapterNumber)
                            .ThenBy(p => p.OrdinalPosition)
                            .FirstOrDefault();

                        if (firstInProject != null)
                            return RedirectToAction("Index", new { projectId, paragraphId = firstInProject.ParagraphID });

                        return RedirectToAction("Index", "FileUpload", new { projectId });
                    }
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    TempData["Error"] = "Failed to delete paragraph.";
                    return RedirectToAction("Index", new { projectId, paragraphId });
                }
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SetBookmark(int projectId, int paragraphId)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            var project = db.BookProjects.Find(projectId);
            if (project == null) return HttpNotFound();

            project.BookmarkParagraphID = paragraphId;
            db.SaveChanges();

            TempData["Message"] = "Bookmark set.";
            return RedirectToAction("Index", new { projectId, paragraphId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ClearBookmark(int projectId, int paragraphId)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            var project = db.BookProjects.Find(projectId);
            if (project == null) return HttpNotFound();

            project.BookmarkParagraphID = null;
            db.SaveChanges();

            TempData["Message"] = "Bookmark cleared.";
            return RedirectToAction("Index", new { projectId, paragraphId });
        }

        [HttpGet]
        public ActionResult GoTo(int projectId, string target)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            if (string.IsNullOrWhiteSpace(target))
                return RedirectToAction("Index", new { projectId });

            target = target.Trim();

            // Numeric: treat as 1-based global position
            int pos;
            if (int.TryParse(target, out pos))
            {
                var allParagraphs = db.Paragraphs
                    .Where(p => p.Chapter.BookProjectID == projectId)
                    .Select(p => new { p.ParagraphID, ChapterNumber = p.Chapter.ChapterNumber, p.OrdinalPosition })
                    .ToList()
                    .OrderBy(p => p.ChapterNumber)
                    .ThenBy(p => p.OrdinalPosition)
                    .ToList();

                if (pos >= 1 && pos <= allParagraphs.Count)
                    return RedirectToAction("Index", new { projectId, paragraphId = allParagraphs[pos - 1].ParagraphID });

                TempData["Error"] = "Position " + pos + " is out of range (1 to " + allParagraphs.Count + ").";
                return RedirectToAction("Index", new { projectId });
            }

            // Otherwise: treat as UniqueID / PID
            var paragraph = db.Paragraphs
                .Include(p => p.Chapter)
                .FirstOrDefault(p => p.Chapter.BookProjectID == projectId && p.UniqueID == target);

            if (paragraph != null)
                return RedirectToAction("Index", new { projectId, paragraphId = paragraph.ParagraphID });

            TempData["Error"] = "Paragraph '" + target + "' not found.";
            return RedirectToAction("Index", new { projectId });
        }

        [HttpGet]
        public JsonResult Search(int projectId, string q, bool wholeWord = true)
        {
            if (!IsAuthenticated())
                return Json(null, JsonRequestBehavior.AllowGet);

            if (string.IsNullOrWhiteSpace(q))
                return Json(new int[0], JsonRequestBehavior.AllowGet);

            var candidates = db.Paragraphs
                .Where(p => p.Chapter.BookProjectID == projectId && p.ParagraphText.Contains(q))
                .Select(p => new {
                    p.ParagraphID,
                    p.ParagraphText,
                    ChapterNumber = p.Chapter.ChapterNumber,
                    p.OrdinalPosition
                })
                .ToList()
                .OrderBy(p => p.ChapterNumber)
                .ThenBy(p => p.OrdinalPosition)
                .ToList();

            IEnumerable<int> ids;
            if (wholeWord)
            {
                var pattern = @"\b" + System.Text.RegularExpressions.Regex.Escape(q) + @"\b";
                var regex = new System.Text.RegularExpressions.Regex(
                    pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                ids = candidates.Where(p => regex.IsMatch(p.ParagraphText)).Select(p => p.ParagraphID);
            }
            else
            {
                ids = candidates.Select(p => p.ParagraphID);
            }

            return Json(ids.ToArray(), JsonRequestBehavior.AllowGet);
        }

        private ParagraphEditViewModel BuildEditViewModel(BookProject project, Paragraph paragraph)
        {
            var chapter = paragraph.Chapter;

            // Get all chapters ordered by number, with a ChapterID tiebreaker for stability.
            var chapters = db.Chapters
                .Where(c => c.BookProjectID == project.BookProjectID)
                .OrderBy(c => c.ChapterNumber)
                .ThenBy(c => c.ChapterID)
                .ToList();

            // Fetch all paragraphs for this project in one flat query (JOIN through Chapter),
            // pull into memory, then sort on plain ints — avoids any EF navigation-property
            // ordering bugs that affect ORDER BY translation in SQL.
            // ChapterNumber is unique per project (UNIQUE constraint), so the sort is stable.
            var allParagraphs = db.Paragraphs
                .Where(p => p.Chapter.BookProjectID == project.BookProjectID)
                .Select(p => new { p.ParagraphID, p.ChapterID, p.OrdinalPosition, ChapterNumber = p.Chapter.ChapterNumber })
                .ToList()                          // ← bring into memory before sorting
                .OrderBy(p => p.ChapterNumber)
                .ThenBy(p => p.OrdinalPosition)
                .ToList();

            var currentIndex = allParagraphs.FindIndex(p => p.ParagraphID == paragraph.ParagraphID);

            int? prevId = currentIndex > 0 ? allParagraphs[currentIndex - 1].ParagraphID : (int?)null;
            int? nextId = currentIndex >= 0 && currentIndex < allParagraphs.Count - 1
                ? allParagraphs[currentIndex + 1].ParagraphID
                : (int?)null;
            int firstId = allParagraphs.Count > 0 ? allParagraphs[0].ParagraphID : 0;
            int lastId  = allParagraphs.Count > 0 ? allParagraphs[allParagraphs.Count - 1].ParagraphID : 0;

            // Build chapter list from the already-loaded allParagraphs — no extra DB queries.
            var chapterList = new List<ChapterSummary>();
            foreach (var ch in chapters)
            {
                // allParagraphs is ordered by (ChapterNumber, OrdinalPosition), so the first
                // match for each chapter is guaranteed to be that chapter's first paragraph.
                var chParas = allParagraphs.Where(p => p.ChapterID == ch.ChapterID).ToList();
                chapterList.Add(new ChapterSummary
                {
                    ChapterID = ch.ChapterID,
                    ChapterNumber = ch.ChapterNumber,
                    ChapterTitle = ch.ChapterTitle,
                    ParagraphCount = chParas.Count,
                    FirstParagraphID = chParas.Count > 0 ? chParas[0].ParagraphID : 0
                });
            }

            return new ParagraphEditViewModel
            {
                BookProjectID = project.BookProjectID,
                ProjectName = project.ProjectName,
                ParagraphID = paragraph.ParagraphID,
                ChapterID = paragraph.ChapterID,
                UniqueID = paragraph.UniqueID,
                OrdinalPosition = paragraph.OrdinalPosition,
                ParagraphText = paragraph.ParagraphText,
                MetaText = GetMetaText(paragraph.ParagraphID),
                EditNoteText = GetEditNoteText(paragraph.ParagraphID),
                ChapterTitle = chapter.ChapterTitle,
                ChapterNumber = chapter.ChapterNumber,
                TotalChapters = chapters.Count,
                PrevParagraphID = prevId,
                NextParagraphID = nextId,
                FirstParagraphID = firstId,
                LastParagraphID = lastId,
                TotalParagraphs = allParagraphs.Count,
                GlobalPosition = currentIndex >= 0 ? currentIndex + 1 : 0,
                BookmarkParagraphID = project.BookmarkParagraphID,
                IsBookmarked = project.BookmarkParagraphID == paragraph.ParagraphID,
                ChapterList = chapterList
            };
        }

        private string GetMetaText(int paragraphId)
        {
            var meta = db.MetaNotes.FirstOrDefault(m => m.ParagraphID == paragraphId);
            return meta != null ? meta.MetaText : "";
        }

        private string GetEditNoteText(int paragraphId)
        {
            var note = db.EditNotes.FirstOrDefault(e => e.ParagraphID == paragraphId);
            return note != null ? note.NoteText : "";
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
