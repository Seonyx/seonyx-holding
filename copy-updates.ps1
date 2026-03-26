# ============================================
# Anchor Save button below nav bar
# Run in PowerShell from any directory
# ============================================

$src = "\\192.168.69.75\seonyx-holding"
$dst = "C:\Users\Steve\Dropbox\VITALIS\Seonyx\NEW_SITE_FEB26"
$log = "$env:USERPROFILE\Desktop\copy-updates-log.txt"

Start-Transcript -Path $log -Force | Out-Null
Write-Host "Log: $log"

try {

    Write-Host "Anchor Save button below nav bar" -ForegroundColor Cyan
    Write-Host "From: $src" -ForegroundColor Gray
    Write-Host "To:   $dst" -ForegroundColor Gray
    Write-Host ""

    if (-not (Test-Path $src)) {
        throw "ERROR: Cannot reach share at $src"
    }
    if (-not (Test-Path $dst)) {
        throw "ERROR: Destination not found at $dst"
    }

    Write-Host "Copying new files..." -ForegroundColor Yellow
    $newFiles = @(
        "Database\migrations\add-chapter-sortorder.sql"
    )
    foreach ($f in $newFiles) {
        $destDir = Split-Path (Join-Path $dst $f) -Parent
        if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir -Force | Out-Null }
        Copy-Item (Join-Path $src $f) (Join-Path $dst $f) -Force
        Write-Host "  NEW: $f" -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "Updating modified files..." -ForegroundColor Yellow
    $modifiedFiles = @(
        "App_Data\BookML\bookml-chapter.xsd",
        "Models\Chapter.cs",
        "Models\ViewModels\BookEditor\ParagraphEditViewModel.cs",
        "Services\BookmlImporter.cs",
        "Controllers\FileUploadController.cs",
        "Controllers\EditorController.cs",
        "Controllers\DraftController.cs",
        "Controllers\ExportController.cs",
        "Views\Editor\Index.cshtml",
        "Views\ImportLog\Index.cshtml",
        "Views\ImportLog\Detail.cshtml",
        "Content\css\book-editor.css",
        "Scripts\book-editor.js"
    )
    foreach ($f in $modifiedFiles) {
        $destDir = Split-Path (Join-Path $dst $f) -Parent
        if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir -Force | Out-Null }
        Copy-Item (Join-Path $src $f) (Join-Path $dst $f) -Force
        Write-Host "  UPD: $f" -ForegroundColor Cyan
    }

    Write-Host ""
    Write-Host "============================================" -ForegroundColor Yellow
    Write-Host "IMPORTANT - Manual steps needed:" -ForegroundColor Yellow
    Write-Host "============================================" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "1. In VS 2022, Build (Ctrl+Shift+B) then F5 to run." -ForegroundColor White
    Write-Host "   (No SQL migration needed for this change.)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "2. Changes in this build:" -ForegroundColor White
    Write-Host "   - Save button moved to its own right-aligned row below the nav bar." -ForegroundColor Gray
    Write-Host "     It no longer shares the flexbox row with GoTo/chapter controls, so the" -ForegroundColor Gray
    Write-Host "     'Saved at...' message appearing cannot cause the button to jump position." -ForegroundColor Gray
    Write-Host ""
    Write-Host "Copy complete!" -ForegroundColor Green

} catch {
    Write-Host ""
    Write-Host "FAILED: $_" -ForegroundColor Red
} finally {
    Stop-Transcript | Out-Null
}
