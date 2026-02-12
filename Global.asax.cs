using System;
using System.IO;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace Seonyx.Web
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            Exception exception = Server.GetLastError();

            string logPath = Server.MapPath("~/App_Data/Logs/");
            if (!Directory.Exists(logPath))
                Directory.CreateDirectory(logPath);

            string logFile = Path.Combine(logPath, string.Format("errors_{0:yyyyMMdd}.log", DateTime.Now));
            File.AppendAllText(logFile, string.Format("[{0:yyyy-MM-dd HH:mm:ss}] {1}\n\n", DateTime.Now, exception));
        }
    }
}
