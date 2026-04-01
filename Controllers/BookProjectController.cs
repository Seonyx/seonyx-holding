using System;
using System.Configuration;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Seonyx.Web.Models;
using Seonyx.Web.Models.ViewModels.BookEditor;

namespace Seonyx.Web.Controllers
{
    public class BookProjectController : Controller
    {
        private SeonyxContext db = new SeonyxContext();

        public ActionResult Index()
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            var projects = db.BookProjects
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.LastModifiedDate)
                .ToList();

            var model = new BookProjectListViewModel();
            foreach (var p in projects)
            {
                model.Projects.Add(new BookProjectViewModel
                {
                    BookProjectID = p.BookProjectID,
                    ProjectName = p.ProjectName,
                    CoverImagePath = p.CoverImagePath,
                    FolderPath = p.FolderPath,
                    CreatedDate = p.CreatedDate,
                    LastModifiedDate = p.LastModifiedDate,
                    IsActive = p.IsActive,
                    TotalChapters = db.Chapters.Count(c => c.BookProjectID == p.BookProjectID),
                    TotalParagraphs = db.Paragraphs.Count(pr => pr.Chapter.BookProjectID == p.BookProjectID),
                    CurrentDraftNumber = p.CurrentDraftNumber
                });
            }

            return View(model);
        }

        public ActionResult Create()
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");
            return View(new BookProjectViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(BookProjectViewModel model)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            if (!ModelState.IsValid)
                return View(model);

            // Check for duplicate name
            if (db.BookProjects.Any(p => p.ProjectName == model.ProjectName))
            {
                ModelState.AddModelError("ProjectName", "A project with this name already exists.");
                return View(model);
            }

            // Create folder structure
            var basePath = GetBookEditorBasePath();
            var safeName = SanitizeFolderName(model.ProjectName);
            var projectPath = Path.Combine(basePath, safeName);

            Directory.CreateDirectory(projectPath);
            Directory.CreateDirectory(Path.Combine(projectPath, "uploads"));
            Directory.CreateDirectory(Path.Combine(projectPath, "covers"));
            Directory.CreateDirectory(Path.Combine(projectPath, "exports"));

            var project = new BookProject
            {
                ProjectName = model.ProjectName,
                FolderPath = projectPath,
                CreatedDate = DateTime.Now,
                LastModifiedDate = DateTime.Now,
                IsActive = true
            };

            db.BookProjects.Add(project);
            db.SaveChanges();

            TempData["Message"] = string.Format("Project \"{0}\" created successfully.", model.ProjectName);
            return RedirectToAction("Index");
        }

        public ActionResult Edit(int id)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            var project = db.BookProjects.Find(id);
            if (project == null) return HttpNotFound();

            var model = new BookProjectViewModel
            {
                BookProjectID = project.BookProjectID,
                ProjectName   = project.ProjectName,
                Author        = project.Author,
                CoverImagePath = project.CoverImagePath,
                FolderPath    = project.FolderPath,
                CreatedDate   = project.CreatedDate,
                LastModifiedDate = project.LastModifiedDate,
                IsActive      = project.IsActive
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(BookProjectViewModel model)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            if (!ModelState.IsValid)
                return View(model);

            var project = db.BookProjects.Find(model.BookProjectID);
            if (project == null) return HttpNotFound();

            // Check for duplicate name (excluding current)
            if (db.BookProjects.Any(p => p.ProjectName == model.ProjectName && p.BookProjectID != model.BookProjectID))
            {
                ModelState.AddModelError("ProjectName", "A project with this name already exists.");
                return View(model);
            }

            project.ProjectName      = model.ProjectName;
            project.Author           = model.Author;
            project.IsActive         = model.IsActive;
            project.LastModifiedDate = DateTime.Now;

            db.SaveChanges();

            TempData["Message"] = "Project updated successfully.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            var project = db.BookProjects.Find(id);
            if (project == null) return HttpNotFound();

            // ImportLogs have no cascade — must be deleted manually before the project
            var logs = db.ImportLogs.Where(l => l.BookProjectID == id).ToList();
            db.ImportLogs.RemoveRange(logs);
            db.SaveChanges();

            // Delete folder if it exists
            if (Directory.Exists(project.FolderPath))
            {
                Directory.Delete(project.FolderPath, true);
            }

            db.BookProjects.Remove(project);
            db.SaveChanges();

            TempData["Message"] = string.Format("Project \"{0}\" deleted.", project.ProjectName);
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UploadCover(int id, HttpPostedFileBase coverImage)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            var project = db.BookProjects.Find(id);
            if (project == null) return HttpNotFound();

            if (coverImage != null && coverImage.ContentLength > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var ext = Path.GetExtension(coverImage.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(ext))
                {
                    TempData["Error"] = "Cover image must be a JPG, PNG, or GIF file.";
                    return RedirectToAction("Edit", new { id });
                }

                var coversPath = Path.Combine(project.FolderPath, "covers");
                Directory.CreateDirectory(coversPath);

                var fileName = "cover" + ext;
                var filePath = Path.Combine(coversPath, fileName);
                coverImage.SaveAs(filePath);

                // Delete old thumbnail so it gets regenerated
                DeleteThumbnail(project.CoverImagePath);

                project.CoverImagePath = filePath;
                project.LastModifiedDate = DateTime.Now;
                db.SaveChanges();

                TempData["Message"] = "Cover image uploaded successfully.";
            }

            return RedirectToAction("Edit", new { id });
        }

        public ActionResult CoverImage(int id)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            var project = db.BookProjects.Find(id);
            if (project == null) return HttpNotFound();

            if (!string.IsNullOrEmpty(project.CoverImagePath) && System.IO.File.Exists(project.CoverImagePath))
            {
                var ext = Path.GetExtension(project.CoverImagePath).ToLowerInvariant();
                string contentType;
                switch (ext)
                {
                    case ".png": contentType = "image/png"; break;
                    case ".gif": contentType = "image/gif"; break;
                    default: contentType = "image/jpeg"; break;
                }
                return File(project.CoverImagePath, contentType);
            }

            return HttpNotFound();
        }

        public ActionResult CoverThumbnail(int id)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            var project = db.BookProjects.Find(id);
            if (project == null) return HttpNotFound();

            if (string.IsNullOrEmpty(project.CoverImagePath) || !System.IO.File.Exists(project.CoverImagePath))
                return HttpNotFound();

            var thumbPath = GetThumbnailPath(project.CoverImagePath);

            if (!System.IO.File.Exists(thumbPath))
            {
                GenerateThumbnail(project.CoverImagePath, thumbPath, 150);
            }

            var ext = Path.GetExtension(thumbPath).ToLowerInvariant();
            string contentType;
            switch (ext)
            {
                case ".png": contentType = "image/png"; break;
                case ".gif": contentType = "image/gif"; break;
                default: contentType = "image/jpeg"; break;
            }
            return File(thumbPath, contentType);
        }

        // Helpers

        private string GetThumbnailPath(string coverPath)
        {
            var dir = Path.GetDirectoryName(coverPath);
            var name = Path.GetFileNameWithoutExtension(coverPath);
            var ext = Path.GetExtension(coverPath);
            return Path.Combine(dir, name + "_thumb" + ext);
        }

        private void GenerateThumbnail(string sourcePath, string thumbPath, int maxWidth)
        {
            using (var original = System.Drawing.Image.FromFile(sourcePath))
            {
                int thumbWidth = Math.Min(maxWidth, original.Width);
                int thumbHeight = (int)(original.Height * ((double)thumbWidth / original.Width));

                using (var thumb = new Bitmap(thumbWidth, thumbHeight))
                using (var g = Graphics.FromImage(thumb))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.DrawImage(original, 0, 0, thumbWidth, thumbHeight);

                    var format = GetImageFormat(sourcePath);
                    thumb.Save(thumbPath, format);
                }
            }
        }

        private void DeleteThumbnail(string coverPath)
        {
            if (string.IsNullOrEmpty(coverPath)) return;
            // Delete any existing thumbnail regardless of extension
            var dir = Path.GetDirectoryName(coverPath);
            if (!Directory.Exists(dir)) return;
            foreach (var file in Directory.GetFiles(dir, "cover_thumb.*"))
            {
                System.IO.File.Delete(file);
            }
            var thumbPath = GetThumbnailPath(coverPath);
            if (System.IO.File.Exists(thumbPath))
            {
                System.IO.File.Delete(thumbPath);
            }
        }

        private ImageFormat GetImageFormat(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            switch (ext)
            {
                case ".png": return ImageFormat.Png;
                case ".gif": return ImageFormat.Gif;
                default: return ImageFormat.Jpeg;
            }
        }

        private bool IsAuthenticated()
        {
            return User.Identity.IsAuthenticated;
        }

        private string GetBookEditorBasePath()
        {
            var configPath = ConfigurationManager.AppSettings["BookEditorFilesPath"];
            if (!string.IsNullOrEmpty(configPath))
            {
                if (configPath.StartsWith("~"))
                    return Server.MapPath(configPath);
                return configPath;
            }
            return Server.MapPath("~/App_Data/BookEditorFiles");
        }

        private string SanitizeFolderName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = new string(name.Where(c => !invalid.Contains(c)).ToArray());
            return sanitized.Replace(" ", "_");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
