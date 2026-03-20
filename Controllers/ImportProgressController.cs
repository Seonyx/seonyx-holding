using System.Web.Mvc;
using System.Web.SessionState;

namespace Seonyx.Web.Controllers
{
    // Session must be disabled so progress polls are never blocked by the
    // session lock held by the long-running ImportFiles request.
    [Authorize]
    [SessionState(SessionStateBehavior.Disabled)]
    public class ImportProgressController : Controller
    {
        public ActionResult Status(string token)
        {
            FileUploadController.ImportProgressState state;
            if (string.IsNullOrEmpty(token) ||
                !FileUploadController.ActiveImports.TryGetValue(token, out state))
            {
                return Json(new { error = "Unknown token" }, JsonRequestBehavior.AllowGet);
            }

            return Json(new
            {
                done               = state.Done,
                total              = state.Total,
                currentChapter     = state.CurrentChapter,
                paragraphsWritten  = state.ParagraphsWritten,
                recentLines        = state.RecentLines,
                isComplete         = state.IsComplete,
                success            = state.Success,
                message            = state.Message,
                logUrl             = state.LogUrl
            }, JsonRequestBehavior.AllowGet);
        }
    }
}
