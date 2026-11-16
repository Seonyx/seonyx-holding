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

            try
            {
                SaveParagraphRuns(paragraphID, paragraph, paragraphRunsJson);
            }
            catch (BlankingSaveRefusedException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Index", new { projectId = bookProjectID, paragraphId = paragraphID });
            }
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
        //
        // Refuses (BlankingSaveRefusedException, nothing written) a save
        // that carries no text for a paragraph whose text exists only in
        // ParagraphText (zero stored runs): that is the signature of an
        // editor page that showed such a paragraph as empty, and saving it
        // would erase the prose.
        private void SaveParagraphRuns(int paragraphId, Paragraph paragraph, string paragraphRunsJson)
        {
            if (string.IsNullOrEmpty(paragraphRunsJson)) return;

            var runService = new ParagraphRunService();
            var dtos = JsonConvert.DeserializeObject<List<InlineRun>>(paragraphRunsJson) ?? new List<InlineRun>();

            bool incomingHasText = dtos.Any(d => !d.IsBreak && PlainTextRunBuilder.HasText(d.Text));
            if (!incomingHasText
                && PlainTextRunBuilder.HasText(paragraph.ParagraphText)
                && !db.ParagraphRuns.Any(r => r.ParagraphID == paragraphId))
            {
                throw new BlankingSaveRefusedException(
                    "Save refused: this paragraph's text has not yet been converted to editor format, " +
                    "and this save would have erased it. Nothing was saved. Reload the page to see the " +
                    "paragraph's text, then make your change again.");
            }

            var coalesced = InlineXmlRunConverter.Coalesce(dtos);

            // A paragraph the user has cleared keeps one empty run rather
            // than zero, so the reader fallback can never resurrect its old
            // ParagraphText.
            if (coalesced.Count == 0)
                coalesced = new List<InlineRun> { new InlineRun { Text = "" } };

            runService.SaveRuns(db, paragraphId, ParagraphRunService.ToParagraphRuns(coalesced));

            paragraph.ParagraphText = runService.GetProseText(db, paragraphId);
        }

        private class BlankingSaveRefusedException : Exception
        {
            public BlankingSaveRefusedException(string message) : base(message) { }
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

        // ------------------------------------------------------------
        // Run repair (admin): GET is a read-only preview, POST writes.
        //   Pass A: paragraphs with text but zero runs get runs from their
        //           ParagraphText via the shared plain-text route (lenient).
        //   Pass B: paragraphs the editor's load-time Markdown
        //           self-conversion would convert are converted now, using
        //           that same shared code (ParagraphRunService
        //           .TryConvertLegacyMarkdownRun).
        // Each paragraph is committed as it is repaired, and the POST stops
        // at a time budget, so a long run on shared hosting can't hit the
        // request timeout and roll everything back. Both passes are
        // idempotent: re-running continues an unfinished repair and
        // changes nothing once it is complete.
        // ------------------------------------------------------------

        private const int RepairRowLimit = 500;
        private const int RepairBatchSize = 200;
        private static readonly TimeSpan RepairTimeBudget = TimeSpan.FromSeconds(60);

        [HttpGet]
        public ActionResult RepairRuns()
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            var model = new RepairRunsViewModel
            {
                PassA = new RepairPassSummary(),
                PassB = new RepairPassSummary(),
                RowLimit = RepairRowLimit
            };

            // Pass A, simulated. Its output is also fed to the Pass B
            // predicate, since Pass B runs after Pass A in the same POST.
            foreach (var p in LoadZeroRunParagraphsWithText(db))
            {
                string warning;
                var runs = PlainTextRunBuilder.BuildRunsFromPlainText(
                    p.ParagraphText, p.UniqueID, PlainTextParsePolicy.Lenient, out warning);
                if (warning != null) model.PassA.Warnings.Add(warning);
                model.PassA.Count++;
                model.PassA.Rows.Add(MakeRepairRow(p.ProjectID, p.ParagraphID, p.UniqueID, p.ParagraphText, runs));

                var afterB = PlainTextRunBuilder.TryBuildLegacyMarkdownConversion(runs, p.UniqueID);
                if (afterB != null)
                {
                    model.PassB.Count++;
                    model.PassB.Rows.Add(MakeRepairRow(p.ProjectID, p.ParagraphID, p.UniqueID, runs[0].Text, afterB));
                }
            }

            // Pass B on paragraphs that already have runs.
            foreach (var c in LoadSingleFlatRunParagraphs(db))
            {
                var converted = PlainTextRunBuilder.TryBuildLegacyMarkdownConversion(
                    new List<InlineRun> { new InlineRun { Text = c.RunText } }, c.UniqueID);
                if (converted == null) continue;
                model.PassB.Count++;
                model.PassB.Rows.Add(MakeRepairRow(c.ProjectID, c.ParagraphID, c.UniqueID, c.RunText, converted));
            }

            SortAndCapRows(model.PassA);
            SortAndCapRows(model.PassB);
            return View(model);
        }

        [HttpPost]
        [ActionName("RepairRuns")]
        [ValidateAntiForgeryToken]
        public ActionResult RepairRunsPost()
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            var model = new RepairRunsViewModel
            {
                IsResult = true,
                PassA = new RepairPassSummary(),
                PassB = new RepairPassSummary(),
                RowLimit = RepairRowLimit
            };
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var runService = new ParagraphRunService();

            // Pass A
            var passAIds = LoadZeroRunParagraphsWithText(db).Select(p => p.ParagraphID).ToList();
            foreach (var batch in Batches(passAIds))
            {
                if (clock.Elapsed > RepairTimeBudget) { model.StoppedEarly = true; break; }
                using (var ctx = new SeonyxContext())
                {
                    foreach (var id in batch)
                    {
                        var para = ctx.Paragraphs.Include(x => x.Chapter).FirstOrDefault(x => x.ParagraphID == id);
                        if (para == null || !PlainTextRunBuilder.HasText(para.ParagraphText)
                            || ctx.ParagraphRuns.Any(r => r.ParagraphID == id))
                            continue;

                        var warnings = new List<string>();
                        runService.SaveRunsFromPlainText(ctx, id, para.ParagraphText, para.UniqueID,
                            PlainTextParsePolicy.Lenient, warnings);
                        model.PassA.Warnings.AddRange(warnings);
                        model.PassA.Count++;
                        model.PassA.Rows.Add(MakeRepairRow(para.Chapter.BookProjectID, id, para.UniqueID,
                            para.ParagraphText, runService.LoadEffectiveRuns(ctx, para, out _)));
                    }
                }
            }

            // Pass B (only once Pass A has finished, so its output is included)
            if (!model.StoppedEarly)
            {
                var passBIds = LoadSingleFlatRunParagraphs(db)
                    .Where(c => PlainTextRunBuilder.TryBuildLegacyMarkdownConversion(
                        new List<InlineRun> { new InlineRun { Text = c.RunText } }, c.UniqueID) != null)
                    .Select(c => c.ParagraphID)
                    .ToList();

                foreach (var batch in Batches(passBIds))
                {
                    if (clock.Elapsed > RepairTimeBudget) { model.StoppedEarly = true; break; }
                    using (var ctx = new SeonyxContext())
                    {
                        foreach (var id in batch)
                        {
                            var para = ctx.Paragraphs.Include(x => x.Chapter).FirstOrDefault(x => x.ParagraphID == id);
                            if (para == null) continue;
                            var existing = runService.LoadRuns(ctx, id);
                            string before = existing.Count == 1 ? existing[0].RunText : null;

                            var converted = runService.TryConvertLegacyMarkdownRun(ctx, para, existing);
                            if (converted == null) continue;
                            model.PassB.Count++;
                            model.PassB.Rows.Add(MakeRepairRow(para.Chapter.BookProjectID, id, para.UniqueID, before, converted));
                        }
                    }
                }
            }

            SortAndCapRows(model.PassA);
            SortAndCapRows(model.PassB);
            return View("RepairRuns", model);
        }

        public class RepairCandidate
        {
            public int ParagraphID { get; set; }
            public int ProjectID { get; set; }
            public string UniqueID { get; set; }
            public string ParagraphText { get; set; }
            public string RunText { get; set; }
        }

        // Matches the diagnostic SQL: text containing any character other
        // than space/tab/CR/LF, and no ParagraphRuns rows.
        private static List<RepairCandidate> LoadZeroRunParagraphsWithText(SeonyxContext context)
        {
            return context.Paragraphs
                .Where(p => !context.ParagraphRuns.Any(r => r.ParagraphID == p.ParagraphID))
                .Select(p => new RepairCandidate
                {
                    ParagraphID = p.ParagraphID,
                    ProjectID = p.Chapter.BookProjectID,
                    UniqueID = p.UniqueID,
                    ParagraphText = p.ParagraphText
                })
                .ToList()
                .Where(p => PlainTextRunBuilder.HasText(p.ParagraphText))
                .OrderBy(p => p.ParagraphID)
                .ToList();
        }

        // Paragraphs whose runs are exactly one flat, unstyled run -- the
        // only shape the self-conversion predicate can accept.
        private static List<RepairCandidate> LoadSingleFlatRunParagraphs(SeonyxContext context)
        {
            var singleRunIds = context.ParagraphRuns
                .GroupBy(r => r.ParagraphID)
                .Where(g => g.Count() == 1)
                .Select(g => g.Key);

            return context.ParagraphRuns
                .Where(r => singleRunIds.Contains(r.ParagraphID)
                         && !r.IsEmph && !r.IsStrong && !r.IsSmallCaps
                         && r.OverlayType == null && !r.IsBreak)
                .Select(r => new RepairCandidate
                {
                    ParagraphID = r.ParagraphID,
                    ProjectID = r.Paragraph.Chapter.BookProjectID,
                    UniqueID = r.Paragraph.UniqueID,
                    RunText = r.RunText
                })
                .OrderBy(c => c.ParagraphID)
                .ToList();
        }

        private static IEnumerable<List<int>> Batches(List<int> ids)
        {
            for (int i = 0; i < ids.Count; i += RepairBatchSize)
                yield return ids.Skip(i).Take(RepairBatchSize).ToList();
        }

        private static RepairParagraphRow MakeRepairRow(int projectId, int paragraphId, string pid,
            string before, IList<InlineRun> after)
        {
            bool styledWithoutWords = after.Any(r => (r.IsStrong || r.IsEmph) && !HasLetterOrDigit(r.Text));
            bool lostAllWords = HasLetterOrDigit(before) && !after.Any(r => HasLetterOrDigit(r.Text));
            return new RepairParagraphRow
            {
                ProjectID = projectId,
                ParagraphID = paragraphId,
                Pid = pid,
                Before = before,
                After = after,
                Flagged = styledWithoutWords || lostAllWords
            };
        }

        private static bool HasLetterOrDigit(string s)
        {
            return s != null && s.Any(char.IsLetterOrDigit);
        }

        private static void SortAndCapRows(RepairPassSummary pass)
        {
            pass.Rows = pass.Rows
                .OrderByDescending(r => r.Flagged)
                .ThenBy(r => r.ParagraphID)
                .Take(RepairRowLimit)
                .ToList();
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

            string runsWarning;
            var initialRuns = GetInitialRuns(paragraph, out runsWarning);

            return new ParagraphEditViewModel
            {
                BookProjectID = project.BookProjectID,
                ProjectName = project.ProjectName,
                ParagraphID = paragraph.ParagraphID,
                ChapterID = paragraph.ChapterID,
                UniqueID = paragraph.UniqueID,
                OrdinalPosition = paragraph.OrdinalPosition,
                ParagraphText = paragraph.ParagraphText,
                InitialRuns = initialRuns,
                RunsWarning = runsWarning,
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
        //
        // A paragraph with text but no stored runs is never shown as empty:
        // the shared reader fallback parses its ParagraphText instead.
        private List<InlineRun> GetInitialRuns(Paragraph paragraph, out string runsWarning)
        {
            runsWarning = null;
            var runService = new ParagraphRunService();
            var existingRuns = runService.LoadRuns(db, paragraph.ParagraphID);

            var converted = runService.TryConvertLegacyMarkdownRun(db, paragraph, existingRuns);
            if (converted != null) return converted.ToList();

            return PlainTextRunBuilder.EffectiveRuns(paragraph.ParagraphText, paragraph.UniqueID,
                ParagraphRunService.ToInlineRuns(existingRuns), out runsWarning).ToList();
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
