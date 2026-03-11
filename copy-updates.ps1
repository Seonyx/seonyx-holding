# ============================================
# BookML Importer + Draft Compare - Copy Script for Dibbler
# Run in PowerShell from any directory
# ============================================

$src = "\\192.168.69.75\seonyx-holding"
$dst = "C:\Users\Steve\Dropbox\VITALIS\Seonyx\NEW_SITE_FEB26"
$log = "$env:USERPROFILE\Desktop\copy-updates-log.txt"

Start-Transcript -Path $log -Force | Out-Null
Write-Host "Log: $log"

try {

    Write-Host "FK Fix: PidVersion Constraint + Isolated Import Context" -ForegroundColor Cyan
    Write-Host "From: $src" -ForegroundColor Gray
    Write-Host "To:   $dst" -ForegroundColor Gray
    Write-Host ""

    if (-not (Test-Path $src)) {
        throw "ERROR: Cannot reach share at $src"
    }
    if (-not (Test-Path $dst)) {
        throw "ERROR: Destination not found at $dst"
    }

    # Create new directories if needed
    Write-Host "Creating directories..." -ForegroundColor Yellow
    $dirs = @(
        "App_Data\BookML",
        "Database",
        "Services",
        "Views\ImportLog",
        "Views\Draft"
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
        "App_Data\BookML\bookml-common.xsd",
        "App_Data\BookML\bookml-book.xsd",
        "App_Data\BookML\bookml-chapter.xsd",
        "App_Data\BookML\bookml-meta.xsd",
        "App_Data\BookML\bookml-notes.xsd",
        "Models\Draft.cs",
        "Models\ParagraphVersion.cs",
        "Models\ImportLog.cs",
        "Models\ViewModels\BookEditor\DraftDiffViewModel.cs",
        "Services\BookmlImporter.cs",
        "Controllers\ImportLogController.cs",
        "Controllers\DraftController.cs",
        "Views\ImportLog\Index.cshtml",
        "Views\ImportLog\Detail.cshtml",
        "Views\Draft\Diff.cshtml",
        "Database\bookml-migration.sql",
        "Database\add-importlog.sql",
        "Database\add_import_log_filename.sql",
        "Database\fix-pidversion-unique.sql"
    )
    foreach ($f in $newFiles) {
        Copy-Item (Join-Path $src $f) (Join-Path $dst $f) -Force
        Write-Host "  NEW: $f" -ForegroundColor Green
    }

    # Modified files to update
    Write-Host ""
    Write-Host "Updating modified files..." -ForegroundColor Yellow
    $modifiedFiles = @(
        "App_Data\BookML\bookml-common.xsd",
        "App_Start\RouteConfig.cs",
        "Models\BookProject.cs",
        "Models\Chapter.cs",
        "Models\SeonyxContext.cs",
        "Models\ViewModels\BookEditor\BookProjectViewModel.cs",
        "Models\ViewModels\BookEditor\FileUploadViewModel.cs",
        "Models\ViewModels\BookEditor\ParagraphEditViewModel.cs",
        "Controllers\BookProjectController.cs",
        "Controllers\EditorController.cs",
        "Controllers\FileUploadController.cs",
        "Controllers\ExportController.cs",
        "Services\BookmlImporter.cs",
        "Content\css\book-editor.css",
        "Views\Shared\_BookEditorLayout.cshtml",
        "Views\Editor\Index.cshtml",
        "Scripts\book-editor.js",
        "Views\BookProject\Index.cshtml",
        "Views\Export\Index.cshtml",
        "Views\FileUpload\Index.cshtml",
        "Seonyx.csproj"
    )
    foreach ($f in $modifiedFiles) {
        Copy-Item (Join-Path $src $f) (Join-Path $dst $f) -Force
        Write-Host "  UPD: $f" -ForegroundColor Cyan
    }

    Write-Host ""
    Write-Host "============================================" -ForegroundColor Yellow
    Write-Host "IMPORTANT - Manual steps needed:" -ForegroundColor Yellow
    Write-Host "============================================" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "1. In SSMS, run these SQL files against the Seonyx database:" -ForegroundColor White
    Write-Host "   $dst\Database\bookml-migration.sql           (if not already run)" -ForegroundColor Gray
    Write-Host "   $dst\Database\add-importlog.sql              (if not already run)" -ForegroundColor Gray
    Write-Host "   $dst\Database\add_import_log_filename.sql    (if not already run)" -ForegroundColor Gray
    Write-Host "   $dst\Database\fix-pidversion-unique.sql      (new - run once)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "2. In VS 2022, Build (Ctrl+Shift+B) then F5 to run." -ForegroundColor White
    Write-Host ""
    Write-Host "3. Delete the old Mayfly project (if it still exists) and re-import to verify:" -ForegroundColor White
    Write-Host "   - Import completes without FK errors" -ForegroundColor Gray
    Write-Host "   - No false duplicate-version warnings" -ForegroundColor Gray
    Write-Host "   - Source file name shown in the import log list and detail page" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Copy complete!" -ForegroundColor Green

} catch {
    Write-Host ""
    Write-Host "FAILED: $_" -ForegroundColor Red
} finally {
    Stop-Transcript | Out-Null
}
