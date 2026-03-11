using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Seonyx.Web.Models;
using Seonyx.Web.Models.ViewModels.BookEditor;

namespace Seonyx.Web.Controllers
{
    [Authorize]
    public class DraftController : Controller
    {
        private readonly SeonyxContext db = new SeonyxContext();

        // GET: admin/bookeditor/draft/diff?projectId=N[&draftA=1&draftB=2[&chapter=N]]
        public ActionResult Diff(int projectId, int draftA = 0, int draftB = 0, int chapter = 0)
        {
            var project = db.BookProjects.Find(projectId);
            if (project == null)
                return HttpNotFound();

            var availableDrafts = db.Drafts
                .Where(d => d.BookProjectID == projectId)
                .OrderBy(d => d.DraftNumber)
                .ToList();

            if (availableDrafts.Count < 2)
            {
                TempData["Error"] = "At least two drafts are required to compare.";
                return RedirectToAction("Index", "BookProject");
            }

            // Default: compare the two most recent drafts
            if (draftA == 0 || draftB == 0)
            {
                draftB = availableDrafts.Last().DraftNumber;
                draftA = availableDrafts[availableDrafts.Count - 2].DraftNumber;
            }

            var draftARecord = availableDrafts.FirstOrDefault(d => d.DraftNumber == draftA);
            var draftBRecord = availableDrafts.FirstOrDefault(d => d.DraftNumber == draftB);

            if (draftARecord == null || draftBRecord == null)
                return HttpNotFound();

            var chapters = db.Chapters
                .Where(c => c.BookProjectID == projectId)
                .OrderBy(c => c.ChapterNumber)
                .ToList();

            var vm = new DraftDiffViewModel
            {
                BookProjectID   = projectId,
                ProjectName     = project.ProjectName,
                DraftA          = draftA,
                LabelA          = draftARecord.Label ?? "Draft " + draftA,
                DraftB          = draftB,
                LabelB          = draftBRecord.Label ?? "Draft " + draftB,
                AvailableDrafts = availableDrafts.Select(d => d.DraftNumber).ToList()
            };

            foreach (var chapter in chapters)
            {
                var versA = db.ParagraphVersions
                    .Where(v => v.ChapterID == chapter.ChapterID && v.DraftNumber == draftA)
                    .ToDictionary(v => v.Pid, StringComparer.OrdinalIgnoreCase);

                var versB = db.ParagraphVersions
                    .Where(v => v.ChapterID == chapter.ChapterID && v.DraftNumber == draftB)
                    .ToDictionary(v => v.Pid, StringComparer.OrdinalIgnoreCase);

                if (!versA.Any() && !versB.Any())
                    continue;

                var allPids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                allPids.UnionWith(versA.Keys);
                allPids.UnionWith(versB.Keys);

                var rows = new List<DraftDiffRow>();
                foreach (var pid in allPids)
                {
                    ParagraphVersion va, vb;
                    versA.TryGetValue(pid, out va);
                    versB.TryGetValue(pid, out vb);

                    string status;
                    if (va == null)
                        status = "added";
                    else if (vb == null)
                        status = "removed";
                    else if (string.Equals(va.Content, vb.Content, StringComparison.Ordinal))
                        status = "unchanged";
                    else
                        status = "modified";

                    rows.Add(new DraftDiffRow
                    {
                        Pid    = pid,
                        TextA  = va != null ? va.Content : null,
                        TextB  = vb != null ? vb.Content : null,
                        SeqA   = va != null ? va.Seq : int.MaxValue,
                        SeqB   = vb != null ? vb.Seq : int.MaxValue,
                        Status = status
                    });
                }

                // Sort by draft B position where available, else by draft A position
                rows = rows.OrderBy(r => r.SeqB < int.MaxValue ? r.SeqB : r.SeqA).ToList();

                // Look up current working-copy ParagraphID for each pid so the view
                // can link directly to that paragraph in the editor.
                var chapterPids = rows.Select(r => r.Pid).ToList();
                var pidToParaId = db.Paragraphs
                    .Where(p => p.ChapterID == chapter.ChapterID && chapterPids.Contains(p.UniqueID))
                    .Select(p => new { p.UniqueID, p.ParagraphID })
                    .ToDictionary(p => p.UniqueID, p => p.ParagraphID, StringComparer.OrdinalIgnoreCase);

                foreach (var row in rows)
                {
                    int paraId;
                    if (pidToParaId.TryGetValue(row.Pid, out paraId))
                        row.CurrentParagraphID = paraId;
                }

                var chDiff = new DraftDiffChapter
                {
                    ChapterNumber  = chapter.ChapterNumber,
                    ChapterTitle   = chapter.ChapterTitle,
                    Rows           = rows,
                    AddedCount     = rows.Count(r => r.Status == "added"),
                    RemovedCount   = rows.Count(r => r.Status == "removed"),
                    ModifiedCount  = rows.Count(r => r.Status == "modified"),
                    UnchangedCount = rows.Count(r => r.Status == "unchanged")
                };

                vm.Chapters.Add(chDiff);
                vm.TotalAdded     += chDiff.AddedCount;
                vm.TotalRemoved   += chDiff.RemovedCount;
                vm.TotalModified  += chDiff.ModifiedCount;
                vm.TotalUnchanged += chDiff.UnchangedCount;
            }

            // Chapter pagination: default to first chapter that has data
            var chapterNumbers = vm.Chapters.Select(c => c.ChapterNumber).ToList();
            if (chapter == 0 || !chapterNumbers.Contains(chapter))
                chapter = chapterNumbers.FirstOrDefault();

            var idx = chapterNumbers.IndexOf(chapter);
            vm.CurrentChapterNumber = chapter;
            vm.PrevChapterNumber    = idx > 0                          ? chapterNumbers[idx - 1] : (int?)null;
            vm.NextChapterNumber    = idx < chapterNumbers.Count - 1   ? chapterNumbers[idx + 1] : (int?)null;

            return View(vm);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
