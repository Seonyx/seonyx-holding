using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using System.Xml.Linq;
using ContentAnalysisEngine;
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
                .OrderBy(c => c.SortOrder)
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

            foreach (var chap in chapters)
            {
                var versA = db.ParagraphVersions
                    .Where(v => v.ChapterID == chap.ChapterID && v.DraftNumber == draftA)
                    .ToDictionary(v => v.Pid, StringComparer.OrdinalIgnoreCase);

                var versB = db.ParagraphVersions
                    .Where(v => v.ChapterID == chap.ChapterID && v.DraftNumber == draftB)
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
                    .Where(p => p.ChapterID == chap.ChapterID && chapterPids.Contains(p.UniqueID))
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
                    ChapterNumber  = chap.ChapterNumber,
                    ChapterTitle   = chap.ChapterTitle,
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

        // GET: admin/bookeditor/draft/Index?projectId=N
        public ActionResult Index(int projectId)
        {
            var project = db.BookProjects.Find(projectId);
            if (project == null)
                return HttpNotFound();

            var drafts = db.Drafts
                .Where(d => d.BookProjectID == projectId)
                .OrderBy(d => d.DraftNumber)
                .ToList();

            var chapterIds = db.Chapters
                .Where(c => c.BookProjectID == projectId)
                .Select(c => c.ChapterID)
                .ToList();

            var paraCounts = db.ParagraphVersions
                .Where(pv => chapterIds.Contains(pv.ChapterID))
                .GroupBy(pv => pv.DraftNumber)
                .Select(g => new { DraftNumber = g.Key, Count = g.Count() })
                .ToDictionary(x => x.DraftNumber, x => x.Count);

            var vm = new DraftListViewModel
            {
                BookProjectID      = projectId,
                ProjectName        = project.ProjectName,
                CurrentDraftNumber = project.CurrentDraftNumber
            };

            foreach (var d in drafts)
            {
                int paraCount;
                paraCounts.TryGetValue(d.DraftNumber, out paraCount);
                vm.Drafts.Add(new DraftListItem
                {
                    DraftNumber    = d.DraftNumber,
                    Label          = d.Label,
                    Status         = d.Status,
                    AuthorType     = d.AuthorType,
                    Author         = d.Author,
                    BasedOn        = d.BasedOn,
                    CreatedDate    = d.CreatedDate,
                    ParagraphCount = paraCount,
                    CanDelete      = drafts.Count > 1
                });
            }

            return View(vm);
        }

        // POST: admin/bookeditor/draft/DeleteDraft
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteDraft(int projectId, int draftNumber)
        {
            var project = db.BookProjects.Find(projectId);
            if (project == null)
                return HttpNotFound();

            var draftCount = db.Drafts.Count(d => d.BookProjectID == projectId);
            if (draftCount <= 1)
            {
                TempData["Error"] = "Cannot delete the only draft.";
                return RedirectToAction("Index", new { projectId });
            }

            var draft = db.Drafts.FirstOrDefault(d => d.BookProjectID == projectId && d.DraftNumber == draftNumber);
            if (draft == null)
                return HttpNotFound();

            // Bulk-delete paragraph versions via SQL (avoids loading potentially thousands of rows into memory)
            db.Database.ExecuteSqlCommand(
                "DELETE pv FROM ParagraphVersions pv JOIN Chapters c ON c.ChapterID = pv.ChapterID WHERE c.BookProjectID = @p0 AND pv.DraftNumber = @p1",
                projectId, draftNumber);

            db.Drafts.Remove(draft);

            // Keep CurrentDraftNumber pointing at the highest remaining draft
            var newCurrent = db.Drafts
                .Where(d => d.BookProjectID == projectId && d.DraftNumber != draftNumber)
                .Max(d => (int?)d.DraftNumber) ?? 1;
            project.CurrentDraftNumber = newCurrent;

            db.SaveChanges();

            TempData["Message"] = string.Format("Draft {0} deleted.", draftNumber);
            return RedirectToAction("Index", new { projectId });
        }

        // =====================================================================
        // Draft Analysis + Work Order Review
        // =====================================================================

        // GET: admin/bookeditor/draft/Analysis?projectId=N[&chapterId=N][&draftNumber=N]
        public ActionResult Analysis(int projectId, int chapterId = 0, int draftNumber = 0)
        {
            var project = db.BookProjects.Find(projectId);
            if (project == null) return HttpNotFound();

            var chapters = db.Chapters
                .Where(c => c.BookProjectID == projectId)
                .OrderBy(c => c.SortOrder)
                .ToList();

            if (draftNumber == 0)
                draftNumber = project.CurrentDraftNumber;

            var vm = new DraftAnalysisViewModel
            {
                BookProjectID = projectId,
                ProjectName   = project.ProjectName,
                DraftNumber   = draftNumber,
                Chapters      = chapters.Select(c => new ChapterPickerItem
                {
                    ChapterID      = c.ChapterID,
                    ChapterNumber  = c.ChapterNumber,
                    ChapterTitle   = c.ChapterTitle,
                    BookmlChapterId = c.BookmlChapterId
                }).ToList()
            };

            // Pick the requested chapter (or first available)
            Chapter chapter = null;
            if (chapterId > 0)
                chapter = chapters.FirstOrDefault(c => c.ChapterID == chapterId);
            if (chapter == null)
                chapter = chapters.FirstOrDefault();

            if (chapter == null)
            {
                // No BookML chapters imported yet
                return View(vm);
            }

            vm.ChapterID       = chapter.ChapterID;
            vm.ChapterTitle    = chapter.ChapterTitle;
            vm.BookmlChapterId = chapter.BookmlChapterId;

            // Check session for an existing manifest
            var sessionKey = string.Format("WO_{0}_{1}_{2}", projectId, chapter.ChapterID, draftNumber);
            var manifestXml = Session[sessionKey] as string;
            if (manifestXml == null)
            {
                vm.HasManifest = false;
                return View(vm);
            }

            // Parse the stored manifest and build the view model
            try
            {
                var doc = XDocument.Parse(manifestXml);
                PopulateViewModelFromManifest(vm, doc, projectId, chapter.ChapterID);
            }
            catch
            {
                Session.Remove(sessionKey);
                vm.HasManifest = false;
            }

            return View(vm);
        }

        // POST: admin/bookeditor/draft/GenerateWorkOrders
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GenerateWorkOrders(int projectId, int chapterId, int draftNumber)
        {
            var project = db.BookProjects.Find(projectId);
            if (project == null) return HttpNotFound();

            var chapter = db.Chapters.FirstOrDefault(
                c => c.ChapterID == chapterId && c.BookProjectID == projectId);
            if (chapter == null) return HttpNotFound();

            // Load paragraph versions for this chapter and draft from the DB
            var versions = db.ParagraphVersions
                .Where(pv => pv.ChapterID == chapterId && pv.DraftNumber == draftNumber)
                .OrderBy(pv => pv.Seq)
                .ToList();

            if (versions.Count == 0)
            {
                TempData["Error"] = string.Format(
                    "No paragraph data found for chapter '{0}', draft {1}. Import a BookML package first.",
                    chapter.ChapterTitle, draftNumber);
                return RedirectToAction("Analysis", new { projectId, chapterId, draftNumber });
            }

            try
            {
                var paragraphs = versions.Select(pv => new ParagraphEntry
                {
                    Pid  = pv.Pid,
                    Seq  = pv.Seq,
                    Text = pv.Content ?? ""
                }).ToList();

                var chapterId2 = chapter.BookmlChapterId ?? chapter.ChapterID.ToString();

                // Suppress character names and aliases from analysis metrics
                var characterNames = db.Characters
                    .Where(c => c.BookProjectID == projectId)
                    .Select(c => c.Name)
                    .ToList();
                var aliasNames = db.CharacterAliases
                    .Where(a => a.Character.BookProjectID == projectId)
                    .Select(a => a.Alias)
                    .ToList();
                var config = new AnalysisConfiguration
                {
                    AdditionalStopWords = new HashSet<string>(
                        characterNames.Concat(aliasNames),
                        StringComparer.OrdinalIgnoreCase)
                };

                var analyser = new ChapterAnalyser(config);
                var report   = analyser.Analyse(paragraphs, chapterId2);
                var generator   = new WorkOrderGenerator();
                var manifestDoc = generator.Generate(report, chapterId2, draftNumber);

                var sessionKey = string.Format("WO_{0}_{1}_{2}", projectId, chapterId, draftNumber);
                Session[sessionKey] = manifestDoc.ToString();
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Analysis failed: " + ex.Message;
                return RedirectToAction("Analysis", new { projectId, chapterId, draftNumber });
            }

            return RedirectToAction("Analysis", new { projectId, chapterId, draftNumber });
        }

        // POST: admin/bookeditor/draft/ExportWorkOrders
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult ExportWorkOrders(int projectId, int chapterId, int draftNumber, string manifestXml)
        {
            if (string.IsNullOrEmpty(manifestXml))
                return new HttpStatusCodeResult(400, "No manifest XML provided.");

            try
            {
                // Validate it parses (throws on malformed XML)
                XDocument.Parse(manifestXml);
            }
            catch
            {
                return new HttpStatusCodeResult(400, "Manifest XML is not well-formed.");
            }

            var chapter = db.Chapters.FirstOrDefault(
                c => c.ChapterID == chapterId && c.BookProjectID == projectId);
            var bookmlId = chapter != null && chapter.BookmlChapterId != null
                ? chapter.BookmlChapterId
                : "chapter";

            var filename = string.Format("{0}_draft{1}_workorders.xml", bookmlId, draftNumber);
            var bytes    = Encoding.UTF8.GetBytes(manifestXml);

            return File(bytes, "application/xml", filename);
        }

        // =====================================================================
        // Private helpers — Draft Analysis
        // =====================================================================

        private static readonly XNamespace WoNs = XNamespace.Get("https://bookml.org/ns/workorder/1.0");

        private void PopulateViewModelFromManifest(
            DraftAnalysisViewModel vm,
            XDocument doc,
            int projectId,
            int chapterId)
        {
            var root = doc.Root;
            if (root == null) return;

            // Collect all PIDs that appear in the manifest so we can batch-resolve ParagraphIDs
            var allPids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in root.Elements(WoNs + "entry"))
            {
                var targets = entry.Element(WoNs + "targets");
                if (targets == null) continue;
                foreach (var pid in targets.Elements(WoNs + "pid"))
                {
                    var r = (string)pid.Attribute("ref");
                    if (!string.IsNullOrEmpty(r)) allPids.Add(r);
                }
            }

            // Batch look up ParagraphID for each PID (UniqueID in working copy)
            var pidList = allPids.ToList();
            var pidToParaId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (pidList.Count > 0)
            {
                var paraLookup = db.Paragraphs
                    .Where(p => p.ChapterID == chapterId && pidList.Contains(p.UniqueID))
                    .Select(p => new { p.UniqueID, p.ParagraphID })
                    .ToList();
                foreach (var row in paraLookup)
                    pidToParaId[row.UniqueID] = row.ParagraphID;
            }

            // Map each entry
            foreach (var entry in root.Elements(WoNs + "entry"))
            {
                var type    = (string)entry.Attribute("type") ?? "";
                var subject = (string)entry.Element(WoNs + "subject") ?? "";

                // Targets
                var targets  = new List<WorkOrderPidViewModel>();
                var targetsEl = entry.Element(WoNs + "targets");
                if (targetsEl != null)
                {
                    foreach (var pidEl in targetsEl.Elements(WoNs + "pid"))
                    {
                        var pidRef = (string)pidEl.Attribute("ref") ?? "";
                        var countAttr = (string)pidEl.Attribute("count");
                        int countVal;
                        int? count = (countAttr != null && int.TryParse(countAttr, out countVal))
                            ? countVal : (int?)null;
                        int paraId;
                        targets.Add(new WorkOrderPidViewModel
                        {
                            Pid         = pidRef,
                            Count       = count,
                            ParagraphID = pidToParaId.TryGetValue(pidRef, out paraId) ? paraId : (int?)null
                        });
                    }
                }

                // Constraints
                var bans  = new List<WorkOrderBanViewModel>();
                WorkOrderLimitViewModel limit = null;
                string instruction = "";
                var constraintsEl = entry.Element(WoNs + "constraints");
                if (constraintsEl != null)
                {
                    foreach (var ban in constraintsEl.Elements(WoNs + "ban"))
                        bans.Add(new WorkOrderBanViewModel
                        {
                            BanType = (string)ban.Attribute("type") ?? "word",
                            Term    = ban.Value
                        });

                    var limitEl = constraintsEl.Element(WoNs + "limit");
                    if (limitEl != null)
                    {
                        int limitVal;
                        limit = new WorkOrderLimitViewModel
                        {
                            LimitType = (string)limitEl.Attribute("type") ?? "",
                            Value     = int.TryParse((string)limitEl.Attribute("value"), out limitVal) ? limitVal : 0
                        };
                    }

                    var instrEl = constraintsEl.Element(WoNs + "instruction");
                    if (instrEl != null) instruction = instrEl.Value;
                }

                // Constraint summary for table column
                var banTerms = bans.Select(b => b.Term).Take(2).ToList();
                var summary  = banTerms.Count > 0 ? "Ban: " + string.Join(", ", banTerms) : "";
                if (bans.Count > 2) summary += string.Format(" (+{0} more)", bans.Count - 2);
                if (limit != null) summary  += string.Format(". Max: {0}/chapter", limit.Value);

                double severity;
                double.TryParse((string)entry.Attribute("severity"),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out severity);

                vm.Entries.Add(new WorkOrderEntryViewModel
                {
                    Id          = (string)entry.Attribute("id") ?? "",
                    Type        = type,
                    TypeLabel   = TypeLabel(type),
                    Severity    = severity,
                    Status      = (string)entry.Attribute("status") ?? "pending",
                    Subject     = subject,
                    Description = (string)entry.Element(WoNs + "description") ?? "",
                    Instruction = instruction,
                    ConstraintSummary = summary,
                    Targets     = targets,
                    Bans        = bans,
                    Limit       = limit
                });
            }

            // Summary counts
            vm.HasManifest  = true;
            vm.TotalEntries = vm.Entries.Count;
            vm.PendingCount = vm.Entries.Count(e => e.Status == "pending");
            vm.SkippedCount = vm.Entries.Count(e => e.Status == "skipped");
            vm.OutlierCount = vm.Entries.Count(e => e.Type == "outlier-word");
            vm.NgramCount   = vm.Entries.Count(e => e.Type == "repetitive-ngram");
            vm.EchoCount    = vm.Entries.Count(e => e.Type == "proximity-echo");
            vm.ManualCount  = vm.Entries.Count(e => e.Type == "manual");
        }

        private static string TypeLabel(string type)
        {
            switch (type)
            {
                case "outlier-word":     return "Outlier Word";
                case "repetitive-ngram": return "Repetitive N-gram";
                case "proximity-echo":  return "Proximity Echo";
                case "manual":          return "Manual";
                default:                return type;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
