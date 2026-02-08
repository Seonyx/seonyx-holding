# Seonyx Holdings Website

Corporate holding company website showcasing multiple business divisions.

## About

Seonyx Holdings manages a diverse portfolio of businesses:
- **Techwrite** - Non-fiction writing and editorial services
- **Literary Agency** - Representing science fiction authors
- **Inglesolar** - Solar energy consultancy for Southern Spain
- **Pixtracta** - AI-powered real estate software
- **Homesonthemed** - Mediterranean property listings

## Technology Stack

- ASP.NET MVC 5 (.NET Framework 4.5)
- Entity Framework 6
- Microsoft SQL Server
- Razor view engine

## Local Development Setup

### Prerequisites
- .NET Framework 4.5 SDK
- SQL Server or SQL Express
- Visual Studio Code
- Git

### Setup Steps

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/seonyx-holding.git
   cd seonyx-holding
   ```

2. **Create database**
   ```bash
   sqlcmd -S localhost -d Seonyx -i Database/schema.sql
   sqlcmd -S localhost -d Seonyx -i Database/seed-data.sql
   ```

3. **Configure connection string**
   ```bash
   cd Seonyx.Web
   cp Web.config.template Web.config
   # Edit Web.config and update connection string with your SQL Server details
   ```

4. **Restore NuGet packages**
   ```bash
   nuget restore
   ```

5. **Build and run**
   ```bash
   msbuild Seonyx.Web/Seonyx.Web.csproj
   ```

6. **Access the site**
   - Public site: `http://localhost:port/`
   - Admin area: `http://localhost:port/admin`

## Project Structure

```
Seonyx.Web/
├── Controllers/      # MVC Controllers
├── Models/           # Entity models and ViewModels
├── Views/            # Razor views
├── Content/          # CSS, images, fonts
├── Scripts/          # JavaScript files
└── App_Start/        # Application configuration
```

## Database

See `Documentation/DATABASE_SCHEMA.md` for complete schema documentation.

Key tables:
- `Pages` - Content pages
- `Divisions` - Business divisions
- `Authors` - Literary agency authors
- `Books` - Published books
- `ContactSubmissions` - Contact form data

## Admin Area

Access at `/admin` with credentials configured in Web.config.

Features:
- Page management (create, edit, delete)
- Division management
- Author and book management
- Content blocks editor
- Contact form submissions
- Site settings

## Deployment

See `Documentation/DEPLOYMENT.md` for deployment instructions.

## Documentation

Full documentation in the `/Documentation` folder:
- `PROJECT_OVERVIEW.md` - Project vision and goals
- `TECHNICAL_REQUIREMENTS.md` - Tech stack and constraints
- `DATABASE_SCHEMA.md` - Complete database documentation
- `SITE_STRUCTURE.md` - Pages and navigation
- `IMPLEMENTATION_GUIDE.md` - Step-by-step build guide
- `BRANDING.md` - Logo and design guidelines
- `DEPLOYMENT.md` - Deployment guide
- `GITHUB_SETUP.md` - Repository setup

## Security Notes

- Web.config is git-ignored (contains sensitive data)
- Use Web.config.template as a starting point
- Admin passwords should be strong and hashed
- All forms use CSRF tokens
- Contact form has honeypot anti-spam protection

## License

Proprietary - All rights reserved.
