using System.Linq;
using System.Web.Mvc;
using Seonyx.Web.Models;

namespace Seonyx.Web.Controllers
{
    public class DivisionController : Controller
    {
        private SeonyxContext db = new SeonyxContext();

        public ActionResult Index(string divisionSlug)
        {
            var division = db.Divisions
                .FirstOrDefault(d => d.Slug == divisionSlug && d.IsActive);

            if (division == null)
            {
                return HttpNotFound();
            }

            var pages = db.Pages
                .Where(p => p.DivisionId == division.DivisionId
                    && p.IsPublished
                    && p.ShowInNavigation)
                .OrderBy(p => p.SortOrder)
                .ToList();

            // For literary agency, also load authors
            if (division.Slug == "literary-agency")
            {
                ViewBag.Authors = db.Authors
                    .Where(a => a.IsActive)
                    .OrderBy(a => a.SortOrder)
                    .ToList();
            }

            ViewBag.Pages = pages;
            ViewBag.Title = division.Name;
            return View(division);
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
