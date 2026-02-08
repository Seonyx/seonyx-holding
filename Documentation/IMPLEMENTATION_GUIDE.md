# Implementation Guide for Claude Code

## Overview

This guide provides step-by-step instructions for building the Seonyx Holdings website using ASP.NET MVC 5 on .NET Framework 4.5.

## Prerequisites

- .NET Framework 4.5 SDK installed
- Visual Studio Code
- Claude Code CLI tool
- SQL Server (or SQL Express for local development)
- Git installed

## Project Setup

### Step 1: Create MVC Project Structure

```bash
# Create solution directory
mkdir seonyx-holding
cd seonyx-holding

# Create the web project directory
mkdir Seonyx.Web
cd Seonyx.Web
```

### Step 2: Initialize .NET MVC Project

Create the following project structure:

```
Seonyx.Web/
├── App_Data/
├── App_Start/
│   ├── RouteConfig.cs
│   └── FilterConfig.cs
├── Content/
│   ├── css/
│   │   ├── site.css
│   │   └── admin.css
│   └── images/
│       └── logo.svg
├── Controllers/
│   ├── HomeController.cs
│   ├── PageController.cs
│   ├── DivisionController.cs
│   ├── LiteraryAgencyController.cs
│   ├── ContactController.cs
│   └── AdminController.cs
├── Models/
│   ├── Page.cs
│   ├── Division.cs
│   ├── ContentBlock.cs
│   ├── ContactSubmission.cs
│   ├── Author.cs
│   ├── Book.cs
│   ├── SiteSetting.cs
│   ├── SeonyxContext.cs
│   └── ViewModels/
│       ├── HomeViewModel.cs
│       ├── PageViewModel.cs
│       ├── ContactViewModel.cs
│       └── Admin/
├── Views/
│   ├── Shared/
│   │   ├── _Layout.cshtml
│   │   ├── _AdminLayout.cshtml
│   │   └── _Navigation.cshtml
│   ├── Home/
│   │   └── Index.cshtml
│   ├── Page/
│   │   └── Index.cshtml
│   ├── Division/
│   │   └── Index.cshtml
│   ├── LiteraryAgency/
│   │   ├── Author.cshtml
│   │   └── Book.cshtml
│   ├── Contact/
│   │   ├── Index.cshtml
│   │   └── Success.cshtml
│   └── Admin/
│       ├── Login.cshtml
│       ├── Dashboard.cshtml
│       └── Pages/
├── Scripts/
├── Web.config
├── Web.config.template
├── Global.asax
├── Global.asax.cs
└── packages.config
```

## Implementation Steps

### Phase 1: Core Setup (Do First)

#### 1.1: Web.config and Connection Strings

**File:** `Web.config.template`

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <configSections>
    <section name="entityFramework" type="System.Data.Entity.Internal.ConfigFile.EntityFrameworkSection, EntityFramework, Version=6.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" requirePermission="false" />
  </configSections>
  
  <connectionStrings>
    <add name="SeonyxContext" 
         connectionString="Server=YOUR_SERVER;Database=Seonyx;User Id=YOUR_USER;Password=YOUR_PASSWORD;" 
         providerName="System.Data.SqlClient" />
  </connectionStrings>
  
  <appSettings>
    <add key="webpages:Version" value="3.0.0.0" />
    <add key="webpages:Enabled" value="false" />
    <add key="ClientValidationEnabled" value="true" />
    <add key="UnobtrusiveJavaScriptEnabled" value="true" />
    <add key="AdminPasswordHash" value="YOUR_HASHED_PASSWORD" />
    <add key="ContactEmail" value="contact@seonyx.com" />
    <add key="SmtpHost" value="smtp.example.com" />
    <add key="SmtpPort" value="587" />
    <add key="SmtpUsername" value="YOUR_SMTP_USER" />
    <add key="SmtpPassword" value="YOUR_SMTP_PASS" />
  </appSettings>
  
  <system.web>
    <compilation debug="true" targetFramework="4.5" />
    <httpRuntime targetFramework="4.5" />
    <authentication mode="Forms">
      <forms loginUrl="~/admin/login" timeout="120" />
    </authentication>
    <machineKey validationKey="AUTO" decryptionKey="AUTO" validation="SHA1" />
  </system.web>
  
  <system.webServer>
    <handlers>
      <remove name="ExtensionlessUrlHandler-Integrated-4.0" />
      <add name="ExtensionlessUrlHandler-Integrated-4.0" path="*." verb="*" type="System.Web.Handlers.TransferRequestHandler" preCondition="integratedMode,runtimeVersionv4.0" />
    </handlers>
  </system.webServer>
  
  <runtime>
    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
      <dependentAssembly>
        <assemblyIdentity name="System.Web.Mvc" publicKeyToken="31bf3856ad364e35" />
        <bindingRedirect oldVersion="0.0.0.0-5.2.7.0" newVersion="5.2.7.0" />
      </dependentAssembly>
    </assemblyBinding>
  </runtime>
  
  <entityFramework>
    <defaultConnectionFactory type="System.Data.Entity.Infrastructure.SqlConnectionFactory, EntityFramework" />
    <providers>
      <provider invariantName="System.Data.SqlClient" type="System.Data.Entity.SqlServer.SqlProviderServices, EntityFramework.SqlServer" />
    </providers>
  </entityFramework>
</configuration>
```

#### 1.2: Create Entity Models

**File:** `Models/Page.cs`

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Seonyx.Web.Models
{
    [Table("Pages")]
    public class Page
    {
        [Key]
        public int PageId { get; set; }

        [Required]
        [StringLength(200)]
        [Index(IsUnique = true)]
        public string Slug { get; set; }

        [Required]
        [StringLength(500)]
        public string Title { get; set; }

        [StringLength(500)]
        public string MetaDescription { get; set; }

        [StringLength(500)]
        public string MetaKeywords { get; set; }

        [Required]
        public string Content { get; set; }

        public int? ParentPageId { get; set; }

        public int? DivisionId { get; set; }

        public int SortOrder { get; set; }

        public bool IsPublished { get; set; }

        public bool ShowInNavigation { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime ModifiedDate { get; set; }

        // Navigation properties
        [ForeignKey("ParentPageId")]
        public virtual Page ParentPage { get; set; }

        [ForeignKey("DivisionId")]
        public virtual Division Division { get; set; }
    }
}
```

**File:** `Models/Division.cs`

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Seonyx.Web.Models
{
    [Table("Divisions")]
    public class Division
    {
        [Key]
        public int DivisionId { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; }

        [Required]
        [StringLength(200)]
        [Index(IsUnique = true)]
        public string Slug { get; set; }

        public string Description { get; set; }

        [StringLength(500)]
        public string LogoUrl { get; set; }

        [StringLength(500)]
        public string WebsiteUrl { get; set; }

        public int SortOrder { get; set; }

        public bool IsActive { get; set; }

        [StringLength(7)]
        public string BackgroundColor { get; set; }

        [StringLength(7)]
        public string ForegroundColor { get; set; }

        public DateTime CreatedDate { get; set; }
    }
}
```

**File:** `Models/ContentBlock.cs`

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Seonyx.Web.Models
{
    [Table("ContentBlocks")]
    public class ContentBlock
    {
        [Key]
        public int BlockId { get; set; }

        [Required]
        [StringLength(100)]
        [Index(IsUnique = true)]
        public string BlockKey { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }

        public bool IsActive { get; set; }

        public DateTime ModifiedDate { get; set; }
    }
}
```

**File:** `Models/ContactSubmission.cs`

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Seonyx.Web.Models
{
    [Table("ContactSubmissions")]
    public class ContactSubmission
    {
        [Key]
        public int SubmissionId { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; }

        [Required]
        [StringLength(320)]
        [EmailAddress]
        public string Email { get; set; }

        [StringLength(500)]
        public string Subject { get; set; }

        [Required]
        public string Message { get; set; }

        [StringLength(45)]
        public string IpAddress { get; set; }

        [StringLength(500)]
        public string UserAgent { get; set; }

        public bool IsRead { get; set; }

        public bool IsSpam { get; set; }

        public DateTime SubmittedDate { get; set; }
    }
}
```

**File:** `Models/Author.cs`

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Seonyx.Web.Models
{
    [Table("Authors")]
    public class Author
    {
        [Key]
        public int AuthorId { get; set; }

        [Required]
        [StringLength(200)]
        public string PenName { get; set; }

        public string Biography { get; set; }

        [StringLength(500)]
        public string PhotoUrl { get; set; }

        [StringLength(200)]
        public string Genre { get; set; }

        [StringLength(500)]
        public string Website { get; set; }

        public int SortOrder { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedDate { get; set; }
    }
}
```

**File:** `Models/Book.cs`

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Seonyx.Web.Models
{
    [Table("Books")]
    public class Book
    {
        [Key]
        public int BookId { get; set; }

        public int AuthorId { get; set; }

        [Required]
        [StringLength(500)]
        public string Title { get; set; }

        public string Synopsis { get; set; }

        [StringLength(500)]
        public string CoverImageUrl { get; set; }

        [StringLength(500)]
        public string AmazonUrl { get; set; }

        [StringLength(500)]
        public string KDPUrl { get; set; }

        [StringLength(20)]
        public string ISBN { get; set; }

        public DateTime? PublicationDate { get; set; }

        [StringLength(200)]
        public string Genre { get; set; }

        public int SortOrder { get; set; }

        public bool IsPublished { get; set; }

        public DateTime CreatedDate { get; set; }

        [ForeignKey("AuthorId")]
        public virtual Author Author { get; set; }
    }
}
```

**File:** `Models/SiteSetting.cs`

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Seonyx.Web.Models
{
    [Table("SiteSettings")]
    public class SiteSetting
    {
        [Key]
        [StringLength(100)]
        public string SettingKey { get; set; }

        public string SettingValue { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        public DateTime ModifiedDate { get; set; }
    }
}
```

#### 1.3: Create DbContext

**File:** `Models/SeonyxContext.cs`

```csharp
using System.Data.Entity;

namespace Seonyx.Web.Models
{
    public class SeonyxContext : DbContext
    {
        public SeonyxContext() : base("SeonyxContext")
        {
            // Disable automatic migrations
            Database.SetInitializer<SeonyxContext>(null);
        }

        public DbSet<Page> Pages { get; set; }
        public DbSet<Division> Divisions { get; set; }
        public DbSet<ContentBlock> ContentBlocks { get; set; }
        public DbSet<ContactSubmission> ContactSubmissions { get; set; }
        public DbSet<Author> Authors { get; set; }
        public DbSet<Book> Books { get; set; }
        public DbSet<SiteSetting> SiteSettings { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure relationships and constraints if needed
        }
    }
}
```

### Phase 2: Routing and Configuration

#### 2.1: Global.asax

**File:** `Global.asax.cs`

```csharp
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
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
        }
    }
}
```

**File:** `Global.asax`

```
<%@ Application Codebehind="Global.asax.cs" Inherits="Seonyx.Web.MvcApplication" Language="C#" %>
```

#### 2.2: Route Configuration

**File:** `App_Start/RouteConfig.cs`

```csharp
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

            // Division route (e.g., /techwrite, /literary-agency)
            routes.MapRoute(
                name: "Division",
                url: "{divisionSlug}",
                defaults: new { controller = "Division", action = "Index" },
                constraints: new { divisionSlug = @"^(?!admin|contact|content|scripts).*" }
            );

            // Division page route (e.g., /techwrite/services)
            routes.MapRoute(
                name: "DivisionPage",
                url: "{divisionSlug}/{pageSlug}",
                defaults: new { controller = "Page", action = "Index" }
            );

            // Home page
            routes.MapRoute(
                name: "Default",
                url: "{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
```

**File:** `App_Start/FilterConfig.cs`

```csharp
using System.Web.Mvc;

namespace Seonyx.Web
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
        }
    }
}
```

### Phase 3: Controllers

#### 3.1: HomeController

**File:** `Controllers/HomeController.cs`

```csharp
using System.Linq;
using System.Web.Mvc;
using Seonyx.Web.Models;

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

            ViewBag.HeroContent = heroBlock != null ? heroBlock.Content : "";
            ViewBag.Divisions = divisions;

            return View();
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
```

#### 3.2: PageController

**File:** `Controllers/PageController.cs`

```csharp
using System.Linq;
using System.Web.Mvc;
using Seonyx.Web.Models;

namespace Seonyx.Web.Controllers
{
    public class PageController : Controller
    {
        private SeonyxContext db = new SeonyxContext();

        public ActionResult Index(string divisionSlug, string pageSlug)
        {
            // Find the division
            var division = db.Divisions
                .FirstOrDefault(d => d.Slug == divisionSlug && d.IsActive);

            if (division == null)
            {
                return HttpNotFound();
            }

            // Find the page
            var page = db.Pages
                .FirstOrDefault(p => p.Slug == pageSlug 
                    && p.DivisionId == division.DivisionId 
                    && p.IsPublished);

            if (page == null)
            {
                return HttpNotFound();
            }

            ViewBag.Division = division;
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
```

#### 3.3: DivisionController

**File:** `Controllers/DivisionController.cs`

```csharp
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

            ViewBag.Pages = pages;
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
```

Continue with remaining controllers in next section...

### Phase 4: Views and Layout

#### 4.1: Main Layout

**File:** `Views/Shared/_Layout.cshtml`

```html
@{
    var db = new Seonyx.Web.Models.SeonyxContext();
    var divisions = db.Divisions.Where(d => d.IsActive).OrderBy(d => d.SortOrder).ToList();
}
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>@ViewBag.Title - Seonyx Holdings</title>
    <link href="~/Content/css/site.css" rel="stylesheet" />
</head>
<body>
    <header>
        <nav class="navbar">
            <div class="container">
                <div class="navbar-header">
                    <a href="/" class="navbar-brand">
                        <img src="~/Content/images/logo.svg" alt="Seonyx" />
                    </a>
                </div>
                <ul class="nav navbar-nav">
                    <li><a href="/about">About</a></li>
                    <li class="dropdown">
                        <a href="#" class="dropdown-toggle">Divisions</a>
                        <ul class="dropdown-menu">
                            @foreach (var div in divisions)
                            {
                                <li><a href="/@div.Slug">@div.Name</a></li>
                            }
                        </ul>
                    </li>
                    <li><a href="/contact">Contact</a></li>
                </ul>
            </div>
        </nav>
    </header>

    <main>
        @RenderBody()
    </main>

    <footer>
        <div class="container">
            <p>&copy; @DateTime.Now.Year Seonyx Holdings. All rights reserved.</p>
        </div>
    </footer>

    <script src="~/Scripts/site.js"></script>
</body>
</html>
```

Continue implementation in remaining phases...

## Testing Checklist

- [ ] Homepage loads and displays divisions
- [ ] Navigation works between pages
- [ ] Division pages load correctly
- [ ] Contact form submits and saves to database
- [ ] Admin login works
- [ ] Admin can create/edit pages
- [ ] Author profiles display correctly
- [ ] Books list under authors
- [ ] Mobile responsive design works
- [ ] Forms have CSRF protection
- [ ] Anti-spam measures work on contact form

## Deployment Checklist

- [ ] Update Web.config with production connection string
- [ ] Run database seed script on production
- [ ] Upload all files via FTP
- [ ] Test all routes on live site
- [ ] Verify admin area works
- [ ] Test contact form email delivery
- [ ] Check SSL certificate
- [ ] Submit sitemap to search engines

## Maintenance

### Regular Tasks
- Check contact form submissions
- Update content via admin area
- Add new authors/books as needed
- Monitor for spam submissions

### Database Backups
- Weekly full backup recommended
- Store backups off-server
- Test restore procedure

### Updates
- Keep NuGet packages updated (when compatible with .NET 4.5)
- Review security patches
- Monitor error logs
