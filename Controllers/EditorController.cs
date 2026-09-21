using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using Newtonsoft.Json;
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
                    .OrderBy(p => p.Chapter.SortOrder)
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
        public ActionResult SaveParagraph(int bookProjectID, int paragraphID, string paragraphRunsJson, string metaText, string editNoteText)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            var paragraph = db.Paragraphs.Find(paragraphID);
            if (paragraph == null) return HttpNotFound();

            SaveParagraphRuns(paragraphID, paragraph, paragraphRunsJson);
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
        public JsonResult AutoSave(int paragraphId, string paragraphRunsJson, string metaText, string editNoteText)
        {
            if (!IsAuthenticated())
                return Json(new { success = false, message = "Not authenticated" });

            try
            {
                var paragraph = db.Paragraphs.Find(paragraphId);
                if (paragraph == null)
                    return Json(new { success = false, message = "Paragraph not found" });

                SaveParagraphRuns(paragraphId, paragraph, paragraphRunsJson);
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

                return Json(new { success = true, timestamp = LocalNow().ToString("h:mm:ss tt") });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Persists the editor's run list for a paragraph and keeps the
        // legacy ParagraphText column in sync as plain prose text (via
        // GetProseText), since SearchParagraphIds queries that column
        // directly in SQL. Coalesces server-side as defense in depth --
        // the client already coalesces before sending, but SaveRuns
        // hard-rejects adjacent same-style runs, so a client bug must not
        // become a 500 here.
        //
        // A null/missing/empty paragraphRunsJson (as opposed to the
        // literal string "[]", meaning "user deleted all text") is treated
        // as "nothing to update" rather than "replace with zero runs" --
        // the rich-text editor always sends its current run list once
        // initialised, so an absent/empty payload means the request came
        // from somewhere that doesn't know about runs yet (or the editor
        // hadn't finished loading), and must not silently wipe content.
        private void SaveParagraphRuns(int paragraphId, Paragraph paragraph, string paragraphRunsJson)
        {
            if (string.IsNullOrEmpty(paragraphRunsJson)) return;

            var runService = new ParagraphRunService();
            var dtos = JsonConvert.DeserializeObject<List<InlineRun>>(paragraphRunsJson) ?? new List<InlineRun>();
            var coalesced = InlineXmlRunConverter.Coalesce(dtos);

            runService.SaveRuns(db, paragraphId, coalesced.Select(r => new ParagraphRun
            {
                RunText     = r.Text,
                IsEmph      = r.IsEmph,
                EmphType    = r.EmphType,
                IsStrong    = r.IsStrong,
                IsSmallCaps = r.IsSmallCaps,
                OverlayType = r.OverlayType,
                IsBreak     = r.IsBreak
            }).ToList());

            paragraph.ParagraphText = runService.GetProseText(db, paragraphId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult InsertParagraph(int projectId, int currentParagraphId, bool before = false, string paraType = "normal")
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            // Sanitise paraType — only allow known values
            var allowedTypes = new[] { "normal", "epigraph" };
            if (!allowedTypes.Contains(paraType)) paraType = "normal";

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
                        ChapterID       = current.ChapterID,
                        UniqueID        = uniqueId,
                        OrdinalPosition = newOrdinal,
                        ParagraphText   = "",
                        CreatedDate     = DateTime.Now,
                        LastModifiedDate = DateTime.Now
                    };
                    db.Paragraphs.Add(newParagraph);
                    db.SaveChanges();

                    // Create blank META and NOTE
                    db.MetaNotes.Add(new MetaNote
                    {
                        ParagraphID = newParagraph.ParagraphID,
                        UniqueID    = uniqueId,
                        MetaText    = ""
                    });
                    db.EditNotes.Add(new EditNote
                    {
                        ParagraphID      = newParagraph.ParagraphID,
                        UniqueID         = uniqueId,
                        NoteText         = "",
                        LastModifiedDate = DateTime.Now
                    });

                    // For non-normal types, create a ParagraphVersion so the type is persisted
                    if (paraType != "normal")
                    {
                        var project = db.BookProjects.FirstOrDefault(p => p.BookProjectID == projectId);
                        int draftNumber = project != null ? project.CurrentDraftNumber : 1;
                        db.ParagraphVersions.Add(new ParagraphVersion
                        {
                            Pid          = uniqueId,
                            ChapterID    = current.ChapterID,
                            DraftNumber  = draftNumber,
                            Seq          = newOrdinal * 1000,
                            ParaType     = paraType,
                            Content      = "",
                            DraftCreated = draftNumber,
                            DraftModified = draftNumber,
                            ModifiedBy   = "human",
                            ModifiedDate = DateTime.Now
                        });
                    }

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
                            .OrderBy(p => p.Chapter.SortOrder)
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
                    .Select(p => new { p.ParagraphID, SortOrder = p.Chapter.SortOrder, p.OrdinalPosition })
                    .ToList()
                    .OrderBy(p => p.SortOrder)
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

            var ids = SearchParagraphIds(projectId, q, wholeWord);
            return Json(ids, JsonRequestBehavior.AllowGet);
        }

        // Distinct overlay type values previously used anywhere in this
        // project, for the overlay dialog's autocomplete (Phase P3 Half B).
        [HttpGet]
        public JsonResult GetOverlayTypes(int projectId)
        {
            if (!IsAuthenticated())
                return Json(new string[0], JsonRequestBehavior.AllowGet);

            var types = db.ParagraphRuns
                .Where(r => r.OverlayType != null
                         && r.Paragraph.Chapter.BookProjectID == projectId)
                .Select(r => r.OverlayType)
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            return Json(types, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult SearchList(int projectId, string q, bool wholeWord = false)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");
            if (string.IsNullOrWhiteSpace(q))
                return Content("No search term.", "text/plain", System.Text.Encoding.UTF8);

            var ids = SearchParagraphIds(projectId, q, wholeWord);

            var paragraphs = db.Paragraphs
                .Where(p => ids.Contains(p.ParagraphID))
                .Select(p => new { p.ParagraphID, p.UniqueID, p.ParagraphText })
                .ToList()
                .OrderBy(p => Array.IndexOf(ids, p.ParagraphID))
                .ToList();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(string.Format("Search: \"{0}\"{1}  --  {2} match{3}",
                q,
                wholeWord ? " (whole word)" : "",
                paragraphs.Count,
                paragraphs.Count == 1 ? "" : "es"));
            sb.AppendLine(new string('-', 60));
            sb.AppendLine();
            foreach (var p in paragraphs)
            {
                sb.AppendLine(p.UniqueID);
                sb.AppendLine(p.ParagraphText);
                sb.AppendLine();
            }

            return Content(sb.ToString(), "text/plain", System.Text.Encoding.UTF8);
        }

        private int[] SearchParagraphIds(int projectId, string q, bool wholeWord)
        {
            var candidates = db.Paragraphs
                .Where(p => p.Chapter.BookProjectID == projectId && p.ParagraphText.Contains(q))
                .Select(p => new {
                    p.ParagraphID,
                    p.ParagraphText,
                    SortOrder = p.Chapter.SortOrder,
                    p.OrdinalPosition
                })
                .ToList()
                .OrderBy(p => p.SortOrder)
                .ThenBy(p => p.OrdinalPosition)
                .ToList();

            if (wholeWord)
            {
                var pattern = @"\b" + System.Text.RegularExpressions.Regex.Escape(q) + @"\b";
                var regex = new System.Text.RegularExpressions.Regex(
                    pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                return candidates.Where(p => regex.IsMatch(p.ParagraphText))
                                 .Select(p => p.ParagraphID)
                                 .ToArray();
            }

            return candidates.Select(p => p.ParagraphID).ToArray();
        }

        private ParagraphEditViewModel BuildEditViewModel(BookProject project, Paragraph paragraph)
        {
            var chapter = paragraph.Chapter;

            // Get all chapters ordered by number, with a ChapterID tiebreaker for stability.
            var chapters = db.Chapters
                .Where(c => c.BookProjectID == project.BookProjectID)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.ChapterID)
                .ToList();

            // Fetch all paragraphs for this project in one flat query (JOIN through Chapter),
            // pull into memory, then sort on plain ints — avoids any EF navigation-property
            // ordering bugs that affect ORDER BY translation in SQL.
            // SortOrder is set from the component index in book.xml, so the sort is stable.
            var allParagraphs = db.Paragraphs
                .Where(p => p.Chapter.BookProjectID == project.BookProjectID)
                .Select(p => new { p.ParagraphID, p.ChapterID, p.OrdinalPosition, SortOrder = p.Chapter.SortOrder })
                .ToList()                          // ← bring into memory before sorting
                .OrderBy(p => p.SortOrder)
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
                // allParagraphs is ordered by (SortOrder, OrdinalPosition), so the first
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
                InitialRuns = GetInitialRuns(paragraph),
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

        // Loads the paragraph's runs for the editor's hydration payload,
        // mapped to the framework-agnostic InlineRun DTO (same shape the
        // save path deserialises into) so load/save share one JSON contract.
        //
        // Also self-heals paragraphs still carrying pre-P3 Markdown-marker
        // text (**bold**/*italic*) that were never routed through P2's
        // importer conversion: if the paragraph's runs are still exactly
        // one flat, unstyled run (i.e. untouched since import/creation)
        // and that text actually contains Markdown markers, parse and
        // persist it as real styled runs right now, so both this page
        // load and any subsequent export/search see the fix immediately.
        // Any paragraph with more than one run, or any styling already
        // applied, is left completely alone -- this only ever touches the
        // narrow "never been parsed" case, never real edited content.
        private List<InlineRun> GetInitialRuns(Paragraph paragraph)
        {
            var runService = new ParagraphRunService();
            var existingRuns = runService.LoadRuns(db, paragraph.ParagraphID);

            var converted = TryConvertLegacyMarkdownRun(paragraph, existingRuns, runService);
            if (converted != null) return converted;

            return existingRuns.Select(r => new InlineRun
            {
                Text        = r.RunText,
                IsEmph      = r.IsEmph,
                EmphType    = r.EmphType,
                IsStrong    = r.IsStrong,
                IsSmallCaps = r.IsSmallCaps,
                OverlayType = r.OverlayType,
                IsBreak     = r.IsBreak
            }).ToList();
        }

        // Returns the newly-converted, already-persisted run list if the
        // paragraph qualified and was converted; null if it didn't
        // qualify (caller should use the unmodified existingRuns as-is).
        private List<InlineRun> TryConvertLegacyMarkdownRun(Paragraph paragraph, IList<ParagraphRun> existingRuns, ParagraphRunService runService)
        {
            if (existingRuns.Count != 1) return null;

            var only = existingRuns[0];
            bool isFlatAndUnstyled = !only.IsEmph && !only.IsStrong && !only.IsSmallCaps
                && only.OverlayType == null && !only.IsBreak;
            if (!isFlatAndUnstyled) return null;
            if (string.IsNullOrEmpty(only.RunText)) return null;

            IList<MarkdownFragment> fragments;
            try
            {
                fragments = MarkdownInlineParser.Parse(only.RunText ?? "", "paragraph-" + paragraph.ParagraphID);
            }
            catch (MarkdownParseException)
            {
                // Malformed markers (e.g. ***triple***) -- leave the
                // paragraph untouched rather than fail the page load.
                return null;
            }

            // "Nothing to convert" means: exactly one plain fragment whose
            // text is byte-identical to the original -- i.e. parsing was
            // a true no-op. A single plain fragment with DIFFERENT text
            // still counts as a real change (e.g. \*escaped\* unescaping
            // to *escaped*), same as a multi-fragment or styled result.
            bool isTrueNoOp = fragments.Count == 1
                && !fragments[0].IsStrong && !fragments[0].IsEmph
                && fragments[0].Text == (only.RunText ?? "");
            if (isTrueNoOp) return null;

            var converted = InlineXmlRunConverter.Coalesce(fragments.Select(f => new InlineRun
            {
                Text        = f.Text,
                IsEmph      = f.IsEmph,
                IsStrong    = f.IsStrong,
                IsSmallCaps = false,
                OverlayType = null,
                IsBreak     = false
            }).ToList());

            runService.SaveRuns(db, paragraph.ParagraphID, converted.Select(r => new ParagraphRun
            {
                RunText     = r.Text,
                IsEmph      = r.IsEmph,
                EmphType    = r.EmphType,
                IsStrong    = r.IsStrong,
                IsSmallCaps = r.IsSmallCaps,
                OverlayType = r.OverlayType,
                IsBreak     = r.IsBreak
            }).ToList());

            // Keep the legacy plain-text mirror in sync too, same as the
            // save endpoints do -- SearchParagraphIds queries this column
            // directly in SQL and would otherwise still match/display the
            // old asterisk-laden text after a fix that only touched runs.
            paragraph.ParagraphText = runService.GetProseText(db, paragraph.ParagraphID);
            db.SaveChanges();

            return converted.ToList();
        }

        private string GetEditNoteText(int paragraphId)
        {
            var note = db.EditNotes.FirstOrDefault(e => e.ParagraphID == paragraphId);
            return note != null ? note.NoteText : "";
        }

        private static DateTime LocalNow()
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            }
            catch
            {
                return DateTime.Now;
            }
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
