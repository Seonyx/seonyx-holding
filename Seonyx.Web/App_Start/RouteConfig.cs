using System.Web.Mvc;
using System.Web.Routing;

namespace Seonyx.Web
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            // Admin routes
            routes.MapRoute(
                name: "AdminLogin",
                url: "admin/login",
                defaults: new { controller = "Admin", action = "Login" }
            );

            routes.MapRoute(
                name: "AdminLogout",
                url: "admin/logout",
                defaults: new { controller = "Admin", action = "Logout" }
            );

            routes.MapRoute(
                name: "AdminPages",
                url: "admin/pages/{action}/{id}",
                defaults: new { controller = "Admin", action = "Pages", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "AdminAuthors",
                url: "admin/authors/{action}/{id}",
                defaults: new { controller = "Admin", action = "Authors", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "AdminBooks",
                url: "admin/books/{action}/{id}",
                defaults: new { controller = "Admin", action = "Books", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "AdminContentBlocks",
                url: "admin/contentblocks/{action}/{id}",
                defaults: new { controller = "Admin", action = "ContentBlocks", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "AdminContactSubmissions",
                url: "admin/submissions/{action}/{id}",
                defaults: new { controller = "Admin", action = "Submissions", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "AdminSettings",
                url: "admin/settings",
                defaults: new { controller = "Admin", action = "Settings" }
            );

            routes.MapRoute(
                name: "Admin",
                url: "admin/{action}/{id}",
                defaults: new { controller = "Admin", action = "Dashboard", id = UrlParameter.Optional }
            );

            // Author profile route
            routes.MapRoute(
                name: "AuthorProfile",
                url: "literary-agency/authors/{slug}",
                defaults: new { controller = "LiteraryAgency", action = "Author" }
            );

            // Book detail route
            routes.MapRoute(
                name: "BookDetail",
                url: "literary-agency/books/{slug}",
                defaults: new { controller = "LiteraryAgency", action = "Book" }
            );

            // Contact route
            routes.MapRoute(
                name: "Contact",
                url: "contact",
                defaults: new { controller = "Contact", action = "Index" }
            );

            // About route
            routes.MapRoute(
                name: "About",
                url: "about",
                defaults: new { controller = "Home", action = "About" }
            );

            // Home page
            routes.MapRoute(
                name: "Default",
                url: "",
                defaults: new { controller = "Home", action = "Index" }
            );

            // Division route (e.g., /techwrite, /literary-agency)
            routes.MapRoute(
                name: "Division",
                url: "{divisionSlug}",
                defaults: new { controller = "Division", action = "Index" },
                constraints: new { divisionSlug = @"^(?!admin|contact|content|scripts|about).*" }
            );

            // Division page route (e.g., /techwrite/services)
            routes.MapRoute(
                name: "DivisionPage",
                url: "{divisionSlug}/{pageSlug}",
                defaults: new { controller = "Page", action = "Index" }
            );
        }
    }
}
