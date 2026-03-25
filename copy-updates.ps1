# ============================================
# Epilogue sort order fix
# Run in PowerShell from any directory
# ============================================

$src = "\\192.168.69.75\seonyx-holding"
$dst = "C:\Users\Steve\Dropbox\VITALIS\Seonyx\NEW_SITE_FEB26"
$log = "$env:USERPROFILE\Desktop\copy-updates-log.txt"

Start-Transcript -Path $log -Force | Out-Null
Write-Host "Log: $log"

try {

    Write-Host "Epilogue sort order fix" -ForegroundColor Cyan
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
        "Views\Editor\Index.cshtml"
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
    Write-Host "1. Run the migration SQL against the Seonyx database:" -ForegroundColor White
    Write-Host "   sqlcmd -S localhost -d Seonyx -i Database\migrations\add-chapter-sortorder.sql" -ForegroundColor Gray
    Write-Host "   (Or open the file in SSMS and execute it.)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "2. In VS 2022, Build (Ctrl+Shift+B) then F5 to run." -ForegroundColor White
    Write-Host ""
    Write-Host "3. Re-upload the draft 3 ZIP (from testdata\mayfly-mutiny_draft3_bookml.zip on the share)," -ForegroundColor White
    Write-Host "   then import it. The ZIP upload now clears old files before extracting." -ForegroundColor White
    Write-Host ""
    Write-Host "4. Changes in this build:" -ForegroundColor White
    Write-Host "   - Chapters now have a SortOrder column (set from position in book.xml)." -ForegroundColor Gray
    Write-Host "     Unnumbered chapters like the epilogue sort after all numbered chapters." -ForegroundColor Gray
    Write-Host "   - Editor chapter header no longer shows 'Ch 0' for unnumbered chapters." -ForegroundColor Gray
    Write-Host "   - Chapter dropdown in the nav bar also cleaned up." -ForegroundColor Gray
    Write-Host ""
    Write-Host "Copy complete!" -ForegroundColor Green

} catch {
    Write-Host ""
    Write-Host "FAILED: $_" -ForegroundColor Red
} finally {
    Stop-Transcript | Out-Null
}
