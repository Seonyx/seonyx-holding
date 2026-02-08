using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using Seonyx.Web.Models;

namespace Seonyx.Web.Controllers
{
    public class LiteraryAgencyController : Controller
    {
        private SeonyxContext db = new SeonyxContext();

        public ActionResult Author(string slug)
        {
            var authors = db.Authors
                .Where(a => a.IsActive)
                .ToList();

            var author = authors.FirstOrDefault(a => GenerateSlug(a.PenName) == slug);

            if (author == null)
            {
                return HttpNotFound();
            }

            var books = db.Books
                .Where(b => b.AuthorId == author.AuthorId && b.IsPublished)
                .OrderBy(b => b.SortOrder)
                .ToList();

            ViewBag.Books = books;
            ViewBag.Title = author.PenName;
            return View(author);
        }

        public ActionResult Book(string slug)
        {
            var books = db.Books
                .Where(b => b.IsPublished)
                .ToList();

            var book = books.FirstOrDefault(b => GenerateSlug(b.Title) == slug);

            if (book == null)
            {
                return HttpNotFound();
            }

            ViewBag.Title = book.Title;
            return View(book);
        }

        private string GenerateSlug(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            string slug = text.ToLowerInvariant();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = Regex.Replace(slug, @"-+", "-");
            slug = slug.Trim('-');

            return slug;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
