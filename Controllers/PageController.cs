using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Seonyx.Web.Models;
using Seonyx.Web.Models.ViewModels;

namespace Seonyx.Web.Controllers
{
    public class PageController : Controller
    {
        private SeonyxContext db = new SeonyxContext();

        public ActionResult Index(string divisionSlug, string pageSlug)
        {
            var division = db.Divisions
                .FirstOrDefault(d => d.Slug == divisionSlug && d.IsActive);

            if (division == null)
            {
                return HttpNotFound();
            }

            var page = db.Pages
                .FirstOrDefault(p => p.Slug == pageSlug
                    && p.DivisionId == division.DivisionId
                    && p.IsPublished);

            if (page == null)
            {
                return HttpNotFound();
            }

            var siblingPages = db.Pages
                .Where(p => p.DivisionId == division.DivisionId
                    && p.IsPublished
                    && p.ShowInNavigation)
                .OrderBy(p => p.SortOrder)
                .ToList();

            var model = new PageViewModel
            {
                Page = page,
                Division = division,
                SiblingPages = siblingPages,
                Breadcrumbs = BuildBreadcrumbs(page, division)
            };

            ViewBag.Title = page.Title;
            return View(model);
        }

        private List<Page> BuildBreadcrumbs(Page page, Division division)
        {
            var breadcrumbs = new List<Page>();

            // Add division as first breadcrumb
            breadcrumbs.Add(new Page
            {
                Title = division.Name,
                Slug = division.Slug
            });

            // Walk up the parent chain
            var current = page;
            var trail = new List<Page>();
            while (current != null)
            {
                trail.Add(current);
                current = current.ParentPage;
            }

            trail.Reverse();
            breadcrumbs.AddRange(trail);

            return breadcrumbs;
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
