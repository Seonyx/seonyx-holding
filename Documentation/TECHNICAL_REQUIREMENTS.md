# Technical Requirements

## Platform Constraints

### Hosting Environment
- **Platform**: Windows shared hosting
- **Framework**: .NET Framework 4.5
- **Web Framework**: ASP.NET MVC 5 (compatible with .NET 4.5)
- **Database**: Microsoft SQL Server (MSSQL)
- **Deployment**: Manual FTP upload

### Technology Stack

**Server-Side**
- C# / ASP.NET MVC 5
- Entity Framework 6 (for database access)
- Razor view engine
- .NET Framework 4.5

**Client-Side**
- HTML5
- CSS3 (responsive design)
- JavaScript (minimal, progressive enhancement)
- jQuery (already bundled with MVC 5)
- Bootstrap 3 (bundled with MVC 5, or custom CSS)

**Database**
- MSSQL Server
- Connection string in web.config
- Database name: Seonyx (assumed existing)

## Development Environment

### Local Development
- **IDE**: Visual Studio Code
- **CLI Tool**: Claude Code (for code generation)
- **Testing**: IIS Express or local IIS
- **Version Control**: Git + GitHub

### Repository Structure
```
seonyx-holding/
├── .gitignore
├── README.md
├── Seonyx.Web/
│   ├── App_Data/
│   │   └── (database files - excluded from git)
│   ├── Content/
│   │   ├── css/
│   │   ├── images/
│   │   └── fonts/
│   ├── Controllers/
│   ├── Models/
│   ├── Views/
│   ├── Scripts/
│   ├── Web.config (template version in git)
│   └── Global.asax
└── Documentation/
    └── (these .md files)
```

## Configuration Management

### Web.config
- Template version committed to git: `Web.config.template`
- Actual version excluded: `Web.config` in .gitignore
- Connection strings use placeholder values in template
- Admin password stored in appSettings (hashed)

### Sensitive Data Exclusion
Files to exclude from git:
- `Web.config`
- `App_Data/*.mdf` (database files)
- `App_Data/*.ldf` (log files)
- `/obj/` and `/bin/` directories
- User-specific files (`.suo`, `.user`)

## Security Requirements

### Contact Form
- CAPTCHA or honeypot anti-spam
- Server-side validation
- Rate limiting (prevent spam)
- Email sent via SMTP (credentials in web.config)

### Admin Area
- Simple password authentication
- Password stored hashed in web.config
- Session-based authentication
- CSRF protection on forms

### General Security
- All forms use AntiForgeryToken
- Input validation and sanitization
- SQL injection prevention (Entity Framework parameterization)
- XSS prevention (Razor automatic encoding)

## Performance Requirements

- Page load time < 2 seconds
- Minimal external dependencies
- Image optimization
- CSS/JS minification
- Browser caching headers

## Browser Support

- Modern browsers (Chrome, Firefox, Safari, Edge)
- IE11+ (if needed for corporate users)
- Mobile responsive (iOS Safari, Chrome Mobile)

## Accessibility

- Semantic HTML5
- ARIA labels where appropriate
- Keyboard navigation
- Sufficient color contrast (WCAG AA minimum)

## Deployment Process

1. **Development**: Local VS Code + Claude Code
2. **Testing**: Local IIS Express
3. **Version Control**: Commit to GitHub
4. **Deployment**: Manual FTP upload to hosting
   - Upload built files from `/bin/` and all views/content
   - Update web.config on server with actual connection strings
   - Test on live environment

## Database Connection

### Connection String Format (in web.config)
```xml
<connectionStrings>
  <add name="SeonyxContext" 
       connectionString="Server=YOUR_SERVER;Database=Seonyx;User Id=YOUR_USER;Password=YOUR_PASSWORD;" 
       providerName="System.Data.SqlClient" />
</connectionStrings>
```

### Entity Framework Configuration
- Code-First approach
- Automatic migrations disabled (manual SQL scripts for production)
- Seed data for initial content

## Development Dependencies

**NuGet Packages Required:**
- EntityFramework 6.x
- Microsoft.AspNet.Mvc 5.x
- Microsoft.AspNet.Razor 3.x
- Microsoft.AspNet.WebPages 3.x
- Newtonsoft.Json (for JSON serialization if needed)

## Build and Deployment Notes

- No automated build pipeline initially
- Manual FTP deployment workflow:
  1. Build solution in Release mode
  2. Copy required files via FTP
  3. Update web.config on server
  4. Test live site
- Future consideration: GitHub Actions + FTP deployment automation
