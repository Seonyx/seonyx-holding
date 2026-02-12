using System.Linq;
using System.Web.Mvc;
using Seonyx.Web.Models;
using Seonyx.Web.Models.ViewModels;

namespace Seonyx.Web.Controllers
{
    public class HomeController : Controller
    {
        private SeonyxContext db = new SeonyxContext();

        public ActionResult Index()
        {
            var heroBlock = db.ContentBlocks
                .FirstOrDefault(b => b.BlockKey == "homepage-hero" && b.IsActive);

            var divisions = db.Divisions
                .Where(d => d.IsActive)
                .OrderBy(d => d.SortOrder)
                .ToList();

            var model = new HomeViewModel
            {
                HeroContent = heroBlock != null ? heroBlock.Content : "",
                Divisions = divisions
            };

            ViewBag.Title = "Seonyx Holdings";
            return View(model);
        }

        public ActionResult About()
        {
            var page = db.Pages.FirstOrDefault(p => p.Slug == "about" && p.IsPublished);
            if (page == null)
            {
                return HttpNotFound();
            }

            ViewBag.Title = page.Title;
            return View(page);
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
