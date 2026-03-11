using System.Linq;
using System.Web.Mvc;
using Seonyx.Web.Models;

namespace Seonyx.Web.Controllers
{
    public class ImportLogController : Controller
    {
        private SeonyxContext db = new SeonyxContext();

        public ActionResult Index(int projectId)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            var project = db.BookProjects.Find(projectId);
            if (project == null) return HttpNotFound();

            ViewBag.ProjectId   = projectId;
            ViewBag.ProjectName = project.ProjectName;

            var logs = db.ImportLogs
                .Where(l => l.BookProjectID == projectId)
                .OrderByDescending(l => l.ImportedAt)
                .ToList();

            return View(logs);
        }

        public ActionResult Detail(int id)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Admin");

            var log = db.ImportLogs.Find(id);
            if (log == null) return HttpNotFound();

            ViewBag.ProjectName = db.BookProjects.Find(log.BookProjectID)?.ProjectName ?? "";

            return View(log);
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
