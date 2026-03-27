using System.Web.Mvc;

namespace Seonyx.Web.Controllers
{
    public class LegalController : Controller
    {
        public ActionResult PrivacyPolicy()
        {
            ViewBag.Title = "Privacy Policy";
            return View();
        }

        public ActionResult TermsAndConditions()
        {
            ViewBag.Title = "Terms and Conditions";
            return View();
        }

        public ActionResult Cookies()
        {
            ViewBag.Title = "Cookie Policy";
            return View();
        }
    }
}
