# ============================================
# Book Editor - Copy Script for Dibbler
# Run in PowerShell from any directory
# ============================================

$src = "\\192.168.69.75\seonyx-holding"
$dst = "C:\Users\Steve\Dropbox\VITALIS\Seonyx\NEW_SITE_FEB26"

Write-Host "Book Editor File Copy" -ForegroundColor Cyan
Write-Host "From: $src" -ForegroundColor Gray
Write-Host "To:   $dst" -ForegroundColor Gray
Write-Host ""

# Verify source and destination exist
if (-not (Test-Path $src)) {
    Write-Host "ERROR: Cannot reach share at $src" -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $dst)) {
    Write-Host "ERROR: Destination not found at $dst" -ForegroundColor Red
    exit 1
}

# Create new directories
Write-Host "Creating directories..." -ForegroundColor Yellow
$dirs = @(
    "Models\ViewModels\BookEditor",
    "Services",
    "Views\BookProject",
    "Views\FileUpload",
    "Views\Editor",
    "Views\Export",
    "Database"
)
foreach ($d in $dirs) {
    $fullPath = Join-Path $dst $d
    if (-not (Test-Path $fullPath)) {
        New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
        Write-Host "  Created: $d" -ForegroundColor Green
    }
}

# New files to copy
Write-Host ""
Write-Host "Copying new files..." -ForegroundColor Yellow
$newFiles = @(
    "Models\BookProject.cs",
    "Models\Chapter.cs",
    "Models\Paragraph.cs",
    "Models\MetaNote.cs",
    "Models\EditNote.cs",
    "Models\ViewModels\BookEditor\BookProjectViewModel.cs",
    "Models\ViewModels\BookEditor\FileUploadViewModel.cs",
    "Models\ViewModels\BookEditor\ParagraphEditViewModel.cs",
    "Models\ViewModels\BookEditor\ExportViewModel.cs",
    "Services\BookFileParser.cs",
    "Controllers\BookProjectController.cs",
    "Controllers\FileUploadController.cs",
    "Controllers\EditorController.cs",
    "Controllers\ExportController.cs",
    "Views\BookProject\Index.cshtml",
    "Views\BookProject\Create.cshtml",
    "Views\BookProject\Edit.cshtml",
    "Views\FileUpload\Index.cshtml",
    "Views\Editor\Index.cshtml",
    "Views\Export\Index.cshtml",
    "Views\Shared\_BookEditorLayout.cshtml",
    "Content\css\book-editor.css",
    "Scripts\book-editor.js",
    "Database\book-editor-tables.sql"
)
foreach ($f in $newFiles) {
    Copy-Item (Join-Path $src $f) (Join-Path $dst $f) -Force
    Write-Host "  NEW: $f" -ForegroundColor Green
}

# Modified files to update
Write-Host ""
Write-Host "Updating modified files..." -ForegroundColor Yellow
$modifiedFiles = @(
    "Models\SeonyxContext.cs",
    "App_Start\RouteConfig.cs",
    "Views\Shared\_AdminLayout.cshtml",
    "Seonyx.csproj",
    "Database\schema.sql",
    "Web.config.template"
)
foreach ($f in $modifiedFiles) {
    Copy-Item (Join-Path $src $f) (Join-Path $dst $f) -Force
    Write-Host "  UPD: $f" -ForegroundColor Cyan
}

# Reminder about Web.config
Write-Host ""
Write-Host "============================================" -ForegroundColor Yellow
Write-Host "IMPORTANT - Manual steps needed:" -ForegroundColor Yellow
Write-Host "============================================" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Add this line to your Web.config <appSettings>:" -ForegroundColor White
Write-Host '   <add key="BookEditorFilesPath" value="~/App_Data/BookEditorFiles" />' -ForegroundColor Gray
Write-Host ""
Write-Host "2. In SSMS, open and run:" -ForegroundColor White
Write-Host "   $dst\Database\book-editor-tables.sql" -ForegroundColor Gray
Write-Host ""
Write-Host "3. In VS 2022:" -ForegroundColor White
Write-Host "   - Right-click Solution > Restore NuGet Packages" -ForegroundColor Gray
Write-Host "   - Build (Ctrl+Shift+B)" -ForegroundColor Gray
Write-Host "   - F5 to run" -ForegroundColor Gray
Write-Host ""
Write-Host "4. Navigate to /admin/bookeditor/projects to test" -ForegroundColor White
Write-Host ""
Write-Host "Copy complete!" -ForegroundColor Green
