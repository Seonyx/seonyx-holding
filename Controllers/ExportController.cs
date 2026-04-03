using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using Seonyx.Web.Models;
using Seonyx.Web.Models.ViewModels.BookEditor;
using Seonyx.Web.Services;

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
                .OrderBy(c => c.SortOrder)
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
                .OrderBy(c => c.SortOrder)
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
                .OrderBy(c => c.SortOrder)
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

        public ActionResult EpubConfig(int projectId)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            var project = db.BookProjects.Find(projectId);
            if (project == null) return HttpNotFound();

            bool hasExisting = !string.IsNullOrEmpty(project.CoverImagePath)
                && System.IO.File.Exists(project.CoverImagePath);

            var model = new EpubConfigViewModel
            {
                BookProjectID    = project.BookProjectID,
                ProjectName      = project.ProjectName,
                RightsHolder     = !string.IsNullOrWhiteSpace(project.Author) ? project.Author : project.ProjectName,
                CopyrightYear    = DateTime.UtcNow.Year,
                ArcDisclaimer    = true,
                HasExistingCover = hasExisting,
                CoverOption      = hasExisting ? EpubCoverOption.UseExisting : EpubCoverOption.UseGenerated
            };

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ExportEpub(EpubConfigViewModel model, HttpPostedFileBase coverImage)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            var project = db.BookProjects.Find(model.BookProjectID);
            if (project == null) return HttpNotFound();
            model.ProjectName      = project.ProjectName;
            model.HasExistingCover = !string.IsNullOrEmpty(project.CoverImagePath)
                && System.IO.File.Exists(project.CoverImagePath);

            if (!ModelState.IsValid)
                return View("EpubConfig", model);

            byte[] coverBytes = null;
            string coverMime  = null;

            if (model.CoverOption == EpubCoverOption.UseExisting)
            {
                if (model.HasExistingCover)
                {
                    coverBytes = System.IO.File.ReadAllBytes(project.CoverImagePath);
                    var ext    = Path.GetExtension(project.CoverImagePath).ToLowerInvariant();
                    coverMime  = (ext == ".png") ? "image/png" : "image/jpeg";
                }
                // If somehow no existing cover, fall through to generated
            }
            else if (model.CoverOption == EpubCoverOption.Upload)
            {
                if (coverImage == null || coverImage.ContentLength == 0)
                {
                    ModelState.AddModelError("", "Please select an image file to upload.");
                    return View("EpubConfig", model);
                }
                var mime = coverImage.ContentType.ToLowerInvariant();
                if (mime != "image/jpeg" && mime != "image/jpg" && mime != "image/png")
                {
                    ModelState.AddModelError("", "Cover image must be JPEG or PNG.");
                    return View("EpubConfig", model);
                }
                if (coverImage.ContentLength > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError("", "Cover image must be 5 MB or smaller.");
                    return View("EpubConfig", model);
                }
                using (var ms = new MemoryStream())
                {
                    coverImage.InputStream.CopyTo(ms);
                    coverBytes = ms.ToArray();
                }
                coverMime = (mime == "image/png") ? "image/png" : "image/jpeg";
            }
            else // UseGenerated
            {
                coverBytes = GenerateCoverImage(project.ProjectName, model.RightsHolder, model.CopyrightYear);
                coverMime  = "image/jpeg";
            }

            var memoryStream = new MemoryStream();
            var exporter     = new EpubExporter();
            var result       = exporter.Export(db, model, coverBytes, coverMime, memoryStream);

            if (!result.Success)
                return new HttpStatusCodeResult(
                    System.Net.HttpStatusCode.InternalServerError,
                    "EPUB export failed: " + string.Join("; ", result.Warnings));

            memoryStream.Position = 0;
            return File(memoryStream, "application/epub+zip", result.EpubFileName);
        }

        private byte[] GenerateCoverImage(string title, string author, int year)
        {
            const int W = 800;
            const int H = 1200;

            using (var bmp = new Bitmap(W, H))
            using (var g   = Graphics.FromImage(bmp))
            {
                g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                // Background — dark navy
                g.Clear(Color.FromArgb(24, 32, 56));

                // Decorative border lines
                var borderPen = new Pen(Color.FromArgb(180, 160, 100), 2);
                g.DrawRectangle(borderPen, 30, 30, W - 61, H - 61);
                g.DrawRectangle(borderPen, 40, 40, W - 81, H - 81);

                // Title
                var titleFont  = new Font("Georgia", 48, FontStyle.Bold, GraphicsUnit.Pixel);
                var titleBrush = new SolidBrush(Color.FromArgb(240, 220, 160));
                var titleArea  = new RectangleF(80, 200, W - 160, 500);
                var titleFormat = new StringFormat
                {
                    Alignment     = StringAlignment.Center,
                    LineAlignment = StringAlignment.Near,
                    Trimming      = StringTrimming.Word
                };
                g.DrawString(title, titleFont, titleBrush, titleArea, titleFormat);

                // Divider line
                g.DrawLine(new Pen(Color.FromArgb(180, 160, 100), 1), 120, H - 280, W - 120, H - 280);

                // Author
                var authorFont   = new Font("Georgia", 28, FontStyle.Regular, GraphicsUnit.Pixel);
                var authorBrush  = new SolidBrush(Color.FromArgb(200, 190, 170));
                var authorFormat = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString(author, authorFont, authorBrush,
                    new RectangleF(80, H - 260, W - 160, 60), authorFormat);

                // Copyright
                var copyFont   = new Font("Arial", 18, FontStyle.Regular, GraphicsUnit.Pixel);
                var copyBrush  = new SolidBrush(Color.FromArgb(140, 130, 110));
                var copyFormat = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString(string.Format("Copyright {0} {1}", year, author),
                    copyFont, copyBrush,
                    new RectangleF(80, H - 180, W - 160, 40), copyFormat);

                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Jpeg);
                    return ms.ToArray();
                }
            }
        }

        public ActionResult AudiobookConfig(int projectId)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            var project = db.BookProjects.Find(projectId);
            if (project == null) return HttpNotFound();

            var model = new AudiobookConfigViewModel
            {
                BookProjectID   = project.BookProjectID,
                ProjectName     = project.ProjectName,
                Author          = project.Author ?? "",
                SelectedVoiceId = VoiceLibrary.All.Count > 0 ? VoiceLibrary.All[0].VoiceId : "",
                Voices          = VoiceLibrary.All
            };

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult DownloadAudiobookPackage(AudiobookConfigViewModel model)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            var project = db.BookProjects.Find(model.BookProjectID);
            if (project == null) return HttpNotFound();

            model.ProjectName = project.ProjectName;
            model.Voices      = VoiceLibrary.All;

            if (!ModelState.IsValid)
                return View("AudiobookConfig", model);

            var voice = VoiceLibrary.All.FirstOrDefault(
                v => string.Equals(v.VoiceId, model.SelectedVoiceId, StringComparison.OrdinalIgnoreCase));

            if (voice == null)
            {
                ModelState.AddModelError("SelectedVoiceId", "Please select a valid voice.");
                return View("AudiobookConfig", model);
            }

            var builder  = new AudiobookPackageBuilder();
            var zipBytes = builder.BuildPackage(db, model.BookProjectID, voice);

            var dateStamp = DateTime.Today.ToString("yyyyMMdd");
            var filename  = string.Format("{0}_audiobook_package_{1}.zip",
                Slugify(project.ProjectName), dateStamp);

            return File(zipBytes, "application/zip", filename);
        }

        public ActionResult ExportBookml(int projectId)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            var project = db.BookProjects.Find(projectId);
            if (project == null) return HttpNotFound();

            var memoryStream = new MemoryStream();
            var exporter = new BookmlExporter();
            var result   = exporter.Export(db, projectId, memoryStream);

            if (!result.Success)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError,
                    "Export failed: " + string.Join("; ", result.Warnings));

            memoryStream.Position = 0;
            return File(memoryStream, "application/zip", result.ZipFileName);
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
