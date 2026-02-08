# Deployment Guide

## Pre-Deployment Checklist

Before deploying to production, ensure:

- [ ] All code is committed to Git
- [ ] Database schema is tested locally
- [ ] Admin credentials are secure (strong hashed password)
- [ ] SMTP settings are configured correctly
- [ ] All sensitive data removed from code
- [ ] Web.config template is up to date
- [ ] SSL certificate is active on hosting
- [ ] Backup of any existing site data

## Local Testing

### 1. Test in Release Mode

```bash
# Build in Release configuration
cd Seonyx.Web
msbuild /p:Configuration=Release
```

### 2. Test Locally

Run with IIS Express and verify:
- All pages load correctly
- Navigation works
- Forms submit properly
- Admin area functions
- Database queries execute correctly
- Images and assets load

### 3. Performance Check

- Check page load times
- Verify CSS/JS minification works
- Test on mobile devices
- Check different browsers

## Production Database Setup

### 1. Create Database

Connect to your production SQL Server and create the database:

```sql
CREATE DATABASE Seonyx;
GO

USE Seonyx;
GO
```

### 2. Run Schema Script

Execute the schema creation script:

```bash
# Via SQL Server Management Studio:
# Open Database/schema.sql and execute

# Or via command line:
sqlcmd -S your-server -U your-user -P your-password -d Seonyx -i Database/schema.sql
```

### 3. Run Seed Data Script

Execute the seed data script:

```bash
sqlcmd -S your-server -U your-user -P your-password -d Seonyx -i Database/seed-data.sql
```

### 4. Verify Database

```sql
-- Check tables exist
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES;

-- Check seed data loaded
SELECT COUNT(*) FROM Divisions;
SELECT COUNT(*) FROM Pages;
SELECT COUNT(*) FROM ContentBlocks;

-- Should see:
-- Divisions: 5
-- Pages: 3 (home, about, contact)
-- ContentBlocks: 2
```

## Production Configuration

### 1. Create Production Web.config

Copy template and update:

```bash
cp Web.config.template Web.config
```

Edit `Web.config` and update:

**Connection String:**
```xml
<connectionStrings>
  <add name="SeonyxContext" 
       connectionString="Server=YOUR_PRODUCTION_SERVER;Database=Seonyx;User Id=YOUR_DB_USER;Password=YOUR_DB_PASSWORD;Encrypt=True;TrustServerCertificate=False;" 
       providerName="System.Data.SqlClient" />
</connectionStrings>
```

**SMTP Settings:**
```xml
<appSettings>
  <add key="ContactEmail" value="contact@seonyx.com" />
  <add key="SmtpHost" value="smtp.yourmailserver.com" />
  <add key="SmtpPort" value="587" />
  <add key="SmtpUsername" value="your-smtp-username" />
  <add key="SmtpPassword" value="your-smtp-password" />
  <add key="SmtpEnableSsl" value="true" />
</appSettings>
```

**Admin Password:**
Generate a strong password hash and update:
```xml
<add key="AdminPasswordHash" value="YOUR_HASHED_PASSWORD_HERE" />
```

### 2. Security Settings

**Compilation Mode (Production):**
```xml
<system.web>
  <compilation debug="false" targetFramework="4.5" />
  <customErrors mode="On" defaultRedirect="~/Error">
    <error statusCode="404" redirect="~/Error/NotFound" />
  </customErrors>
</system.web>
```

**Machine Key (Generate unique keys):**
```xml
<machineKey 
  validationKey="YOUR_UNIQUE_VALIDATION_KEY_HERE"
  decryptionKey="YOUR_UNIQUE_DECRYPTION_KEY_HERE"
  validation="SHA1" 
  decryption="AES" />
```

Generate keys at: https://www.allkeysgenerator.com/Random/ASP-Net-MachineKey-Generator.aspx

## FTP Deployment

### 1. Connect to FTP Server

Using FileZilla or similar FTP client:
- Host: ftp.yourhostingprovider.com
- Username: your-ftp-username
- Password: your-ftp-password
- Port: 21 (or 22 for SFTP)

### 2. Backup Existing Site

If there's an existing site:
1. Download everything to a local backup folder
2. Note the current database (export if needed)
3. Keep backup until new site is verified working

### 3. Upload Files

**Root Directory Structure on Server:**
```
wwwroot/ (or public_html/)
├── bin/
├── Content/
├── Controllers/ (optional - may not upload)
├── Models/ (optional - may not upload)
├── Scripts/
├── Views/
├── App_Data/
├── Global.asax
├── Web.config
└── packages.config
```

**Essential Files to Upload:**
- `/bin/` - All DLLs and compiled assemblies
- `/Content/` - CSS, images, fonts
- `/Scripts/` - JavaScript files
- `/Views/` - All .cshtml files
- `/App_Data/` - Empty folder (create if needed)
- `Global.asax` - Application entry point
- `Web.config` - Production configuration
- `packages.config` - NuGet package list

**DO NOT Upload:**
- `.git/` folder
- `.vs/` folder
- `obj/` folder
- `packages/` folder (unless hosting requires it)
- Source code (`.cs` files) - only compiled DLLs needed
- `Web.config.template`
- Any local development files

### 4. File Permissions

Ensure these folders have write permissions:
- `App_Data/` - For logs or temp files
- `Content/images/uploads/` - For uploaded images

## Post-Deployment Verification

### 1. Test Core Functionality

**Homepage:**
- Visit: https://seonyx.com/
- Verify: Logo displays, divisions load, layout correct

**Navigation:**
- Test all nav links
- Check dropdown menu works
- Verify breadcrumbs display

**Division Pages:**
- Visit each division: /techwrite, /literary-agency, etc.
- Check content loads from database

**Contact Form:**
- Submit test form
- Verify email sends
- Check database records submission

**Admin Area:**
- Visit: https://seonyx.com/admin
- Test login with admin credentials
- Check all admin functions work

### 2. Security Checks

- [ ] HTTPS enabled (SSL certificate active)
- [ ] Admin password is strong
- [ ] Database credentials secure
- [ ] SMTP credentials secure
- [ ] No debug information visible
- [ ] Custom error pages work
- [ ] No directory browsing enabled

### 3. Performance Checks

- [ ] Page load times acceptable (< 2 seconds)
- [ ] Images optimized
- [ ] CSS/JS minified
- [ ] Caching headers set
- [ ] GZIP compression enabled

### 4. Browser Testing

Test in multiple browsers:
- Chrome (desktop & mobile)
- Firefox
- Safari (desktop & mobile)
- Edge
- IE11 (if required)

### 5. Mobile Testing

- Responsive layout works
- Touch navigation functional
- Forms easy to use on mobile
- Images scale properly

## Common Deployment Issues

### Issue: "Could not load file or assembly"

**Solution:**
- Ensure all DLLs in `/bin/` folder
- Check NuGet packages are included
- Verify .NET Framework 4.5 installed on server

### Issue: "Login failed for user"

**Solution:**
- Verify connection string is correct
- Check SQL Server allows remote connections
- Confirm database user has proper permissions
- Test connection string with SQL Management Studio

### Issue: "Server Error in '/' Application"

**Solution:**
- Check Web.config for errors
- Set `<customErrors mode="Off" />` temporarily to see detailed errors
- Check IIS application pool settings
- Verify correct .NET Framework version

### Issue: Contact form not sending emails

**Solution:**
- Verify SMTP settings in Web.config
- Check SMTP server allows relay
- Test SMTP credentials independently
- Check firewall not blocking port 587/465
- Review email logs on server

### Issue: 404 errors on all pages

**Solution:**
- Check URL Rewrite module installed
- Verify MVC routing configured in Web.config
- Ensure Global.asax uploaded and working
- Check IIS Application Pool in Integrated mode

### Issue: CSS/JS not loading

**Solution:**
- Check file paths in layout (should be relative: ~/Content/css/)
- Verify files uploaded to correct directories
- Check IIS MIME types configured
- Clear browser cache

## Rollback Procedure

If deployment fails:

### 1. Quick Rollback

**Option A: Revert files**
1. Stop site in IIS (if possible)
2. Delete new files
3. Restore from backup
4. Restart site

**Option B: Restore database**
```sql
-- Restore database from backup
USE master;
GO
RESTORE DATABASE Seonyx 
FROM DISK = 'C:\Backups\Seonyx_backup.bak'
WITH REPLACE;
GO
```

### 2. Emergency Contact

Keep contact information handy:
- Hosting provider support
- Database administrator
- FTP credentials location
- Backup locations

## Ongoing Maintenance

### Daily Tasks
- Monitor contact form submissions
- Check for spam submissions
- Review any error logs

### Weekly Tasks
- Backup database
- Review admin activity
- Check for security updates
- Monitor site performance

### Monthly Tasks
- Full site backup (files + database)
- Update content as needed
- Review analytics (if implemented)
- Check for .NET security patches

## Automated Deployment (Optional Future Enhancement)

### Using GitHub Actions

Create `.github/workflows/deploy.yml`:

```yaml
name: Deploy to Production

on:
  push:
    branches: [ main ]
  workflow_dispatch:

jobs:
  deploy:
    runs-on: windows-latest
    
    steps:
    - name: Checkout code
      uses: actions/checkout@v2
    
    - name: Setup NuGet
      uses: NuGet/setup-nuget@v1
    
    - name: Restore NuGet packages
      run: nuget restore Seonyx.Web/packages.config -PackagesDirectory Seonyx.Web/packages
    
    - name: Setup MSBuild
      uses: microsoft/setup-msbuild@v1
    
    - name: Build solution
      run: msbuild Seonyx.Web/Seonyx.Web.csproj /p:Configuration=Release /p:DeployOnBuild=true /p:PublishProfile=FolderProfile
    
    - name: Deploy via FTP
      uses: SamKirkland/FTP-Deploy-Action@4.3.0
      with:
        server: ${{ secrets.FTP_SERVER }}
        username: ${{ secrets.FTP_USERNAME }}
        password: ${{ secrets.FTP_PASSWORD }}
        local-dir: ./Seonyx.Web/bin/Release/
        server-dir: /public_html/
```

**Required GitHub Secrets:**
- `FTP_SERVER`
- `FTP_USERNAME`
- `FTP_PASSWORD`

### Using Web Deploy (if supported by host)

```bash
# From local machine
msbuild Seonyx.Web.csproj /p:DeployOnBuild=true /p:PublishProfile=Production /p:Password=YOUR_WEBDEPLOY_PASSWORD
```

## Monitoring and Logging

### Enable Application Logging

Add to Web.config:

```xml
<system.diagnostics>
  <trace enabled="true" writeToDiagnosticsTrace="true">
    <listeners>
      <add name="WebPageTraceListener"
           type="System.Web.WebPageTraceListener, System.Web, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a" />
    </listeners>
  </trace>
</system.diagnostics>
```

### Custom Error Logging

Implement error logging in Global.asax.cs:

```csharp
protected void Application_Error(object sender, EventArgs e)
{
    Exception exception = Server.GetLastError();
    
    // Log to file
    string logPath = Server.MapPath("~/App_Data/Logs/");
    if (!Directory.Exists(logPath))
        Directory.CreateDirectory(logPath);
    
    string logFile = Path.Combine(logPath, $"errors_{DateTime.Now:yyyyMMdd}.log");
    File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception.ToString()}\n\n");
    
    // Clear error and show custom page
    Server.ClearError();
    Response.Redirect("~/Error");
}
```

## Performance Optimization

### Enable Output Caching

In controllers:
```csharp
[OutputCache(Duration = 3600, VaryByParam = "none")]
public ActionResult Index()
{
    // This page will be cached for 1 hour
}
```

### Enable GZIP Compression

Add to Web.config:
```xml
<system.webServer>
  <urlCompression doStaticCompression="true" doDynamicCompression="true" />
</system.webServer>
```

### Set Cache Headers

Add to Web.config:
```xml
<system.webServer>
  <staticContent>
    <clientCache cacheControlMode="UseMaxAge" cacheControlMaxAge="7.00:00:00" />
  </staticContent>
</system.webServer>
```

## SSL/HTTPS Configuration

### Force HTTPS

Add to Web.config:
```xml
<system.webServer>
  <rewrite>
    <rules>
      <rule name="Redirect to HTTPS" stopProcessing="true">
        <match url="(.*)" />
        <conditions>
          <add input="{HTTPS}" pattern="off" ignoreCase="true" />
        </conditions>
        <action type="Redirect" url="https://{HTTP_HOST}/{R:1}" redirectType="Permanent" />
      </rule>
    </rules>
  </rewrite>
</system.webServer>
```

### Security Headers

Add to Web.config:
```xml
<system.webServer>
  <httpProtocol>
    <customHeaders>
      <add name="X-Content-Type-Options" value="nosniff" />
      <add name="X-Frame-Options" value="SAMEORIGIN" />
      <add name="X-XSS-Protection" value="1; mode=block" />
      <add name="Referrer-Policy" value="strict-origin-when-cross-origin" />
    </customHeaders>
  </httpProtocol>
</system.webServer>
```

## Support Contacts

**Hosting Provider:**
- Support URL: [Your hosting provider support URL]
- Support Email: [support email]
- Support Phone: [support phone]

**Technical Support:**
- Developer: [Your email]
- Database Admin: [DBA email if applicable]

**Emergency Rollback:**
- Backup Location: [backup server/service]
- FTP Access: [credential location]
- Database Backups: [backup location]

This completes the deployment guide for the Seonyx Holdings website.
