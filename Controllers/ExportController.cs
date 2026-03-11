using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using Seonyx.Web.Models;
using Seonyx.Web.Models.ViewModels.BookEditor;

namespace Seonyx.Web.Controllers
{
    public class ExportController : Controller
    {
        private SeonyxContext db = new SeonyxContext();

        public ActionResult Index(int projectId)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            var project = db.BookProjects.Find(projectId);
            if (project == null) return HttpNotFound();

            var model = new ExportViewModel
            {
                BookProjectID = project.BookProjectID,
                ProjectName = project.ProjectName
            };

            var chapters = db.Chapters
                .Where(c => c.BookProjectID == projectId)
                .OrderBy(c => c.ChapterNumber)
                .ToList();

            foreach (var ch in chapters)
            {
                model.Chapters.Add(new ChapterExportItem
                {
                    ChapterID = ch.ChapterID,
                    ChapterNumber = ch.ChapterNumber,
                    ChapterTitle = ch.ChapterTitle,
                    ParagraphCount = db.Paragraphs.Count(p => p.ChapterID == ch.ChapterID)
                });
            }

            return View(model);
        }

        public ActionResult ExportProject(int projectId)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            var project = db.BookProjects.Find(projectId);
            if (project == null) return HttpNotFound();

            var chapters = db.Chapters
                .Where(c => c.BookProjectID == projectId)
                .OrderBy(c => c.ChapterNumber)
                .ToList();

            var memoryStream = new MemoryStream();
            using (var zip = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                foreach (var chapter in chapters)
                {
                    var slug = Slugify(chapter.ChapterTitle);
                    var chNum = chapter.ChapterNumber.ToString("D2");

                    // Chapter file
                    var chapterMd = GenerateChapterMarkdown(chapter);
                    AddTextEntry(zip, string.Format("ch{0}_{1}.md", chNum, slug), chapterMd);

                    // META file
                    var metaMd = GenerateMetaMarkdown(chapter);
                    AddTextEntry(zip, string.Format("ch{0}_{1}_meta.md", chNum, slug), metaMd);

                    // Notes file
                    var notesTxt = GenerateNotesText(chapter);
                    AddTextEntry(zip, string.Format("ch{0}_{1}_notes.txt", chNum, slug), notesTxt);
                }
            }

            memoryStream.Position = 0;
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var zipFileName = string.Format("{0}_{1}.zip", Slugify(project.ProjectName), timestamp);

            return File(memoryStream, "application/zip", zipFileName);
        }

        public ActionResult ExportChapter(int chapterId)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            var chapter = db.Chapters.Find(chapterId);
            if (chapter == null) return HttpNotFound();

            var slug = Slugify(chapter.ChapterTitle);
            var chNum = chapter.ChapterNumber.ToString("D2");

            var memoryStream = new MemoryStream();
            using (var zip = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                AddTextEntry(zip, string.Format("ch{0}_{1}.md", chNum, slug), GenerateChapterMarkdown(chapter));
                AddTextEntry(zip, string.Format("ch{0}_{1}_meta.md", chNum, slug), GenerateMetaMarkdown(chapter));
                AddTextEntry(zip, string.Format("ch{0}_{1}_notes.txt", chNum, slug), GenerateNotesText(chapter));
            }

            memoryStream.Position = 0;
            var zipFileName = string.Format("ch{0}_{1}.zip", chNum, slug);

            return File(memoryStream, "application/zip", zipFileName);
        }

        public ActionResult ExportManuscriptOnly(int projectId)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            var project = db.BookProjects.Find(projectId);
            if (project == null) return HttpNotFound();

            var chapters = db.Chapters
                .Where(c => c.BookProjectID == projectId)
                .OrderBy(c => c.ChapterNumber)
                .ToList();

            var memoryStream = new MemoryStream();
            using (var zip = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                foreach (var chapter in chapters)
                {
                    var slug = Slugify(chapter.ChapterTitle);
                    var chNum = chapter.ChapterNumber.ToString("D2");
                    var chapterMd = GenerateCleanManuscript(chapter);
                    AddTextEntry(zip, string.Format("ch{0}_{1}.md", chNum, slug), chapterMd);
                }
            }

            memoryStream.Position = 0;
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var zipFileName = string.Format("{0}_manuscript_{1}.zip", Slugify(project.ProjectName), timestamp);

            return File(memoryStream, "application/zip", zipFileName);
        }

        // ==================== Generators ====================

        private string GenerateCleanManuscript(Chapter chapter)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Format("# Chapter {0} — {1}", chapter.ChapterNumber, chapter.ChapterTitle));
            sb.AppendLine();

            var paragraphs = db.Paragraphs
                .Where(p => p.ChapterID == chapter.ChapterID)
                .OrderBy(p => p.OrdinalPosition)
                .ToList();

            foreach (var para in paragraphs)
            {
                sb.AppendLine(para.ParagraphText);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string GenerateChapterMarkdown(Chapter chapter)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Format("# Chapter {0:D2} - {1}", chapter.ChapterNumber, chapter.ChapterTitle));
            sb.AppendLine();

            if (!string.IsNullOrEmpty(chapter.POV))
            {
                sb.AppendLine("## POV");
                sb.AppendLine(chapter.POV);
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(chapter.Setting))
            {
                sb.AppendLine("## Setting");
                sb.AppendLine(chapter.Setting);
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(chapter.ChapterPurpose))
            {
                sb.AppendLine("## Chapter purpose");
                sb.AppendLine(chapter.ChapterPurpose);
                sb.AppendLine();
            }

            sb.AppendLine("## Draft Paragraphs (keyed)");
            sb.AppendLine();

            var paragraphs = db.Paragraphs
                .Where(p => p.ChapterID == chapter.ChapterID)
                .OrderBy(p => p.OrdinalPosition)
                .ToList();

            foreach (var para in paragraphs)
            {
                sb.AppendLine(string.Format("[[{0}]] {1}", para.UniqueID, para.ParagraphText));
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string GenerateMetaMarkdown(Chapter chapter)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Format("# Chapter {0:D2} Meta Notes - {1}", chapter.ChapterNumber, chapter.ChapterTitle));
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
                if (meta != null && !string.IsNullOrEmpty(meta.MetaText))
                {
                    sb.AppendLine(string.Format("[[{0}]] {1}", para.UniqueID, meta.MetaText));
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        private string GenerateNotesText(Chapter chapter)
        {
            var sb = new StringBuilder();

            var paragraphs = db.Paragraphs
                .Where(p => p.ChapterID == chapter.ChapterID)
                .OrderBy(p => p.OrdinalPosition)
                .ToList();

            foreach (var para in paragraphs)
            {
                var note = db.EditNotes.FirstOrDefault(n => n.ParagraphID == para.ParagraphID);
                if (note != null && !string.IsNullOrWhiteSpace(note.NoteText))
                {
                    sb.AppendLine(string.Format("{0}|{1}", para.UniqueID, note.NoteText));
                }
            }

            return sb.ToString();
        }

        // ==================== Helpers ====================

        private void AddTextEntry(ZipArchive zip, string entryName, string content)
        {
            var entry = zip.CreateEntry(entryName);
            using (var writer = new StreamWriter(entry.Open(), Encoding.UTF8))
            {
                writer.Write(content);
            }
        }

        private string Slugify(string text)
        {
            if (string.IsNullOrEmpty(text)) return "untitled";
            var slug = text.ToLowerInvariant();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = slug.Trim('-');
            return string.IsNullOrEmpty(slug) ? "untitled" : slug;
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
