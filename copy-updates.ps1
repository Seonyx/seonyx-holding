# ============================================
# Generate EPUB export feature
# Run in PowerShell from any directory
# ============================================

$src = "\\192.168.69.75\seonyx-holding"
$dst = "C:\Users\Steve\Dropbox\VITALIS\Seonyx\NEW_SITE_FEB26"
$log = "$env:USERPROFILE\Desktop\copy-updates-log.txt"

Start-Transcript -Path $log -Force | Out-Null
Write-Host "Log: $log"

try {

    Write-Host "Author field on BookProject; epigraph PID detection fix" -ForegroundColor Cyan
    Write-Host "From: $src" -ForegroundColor Gray
    Write-Host "To:   $dst" -ForegroundColor Gray
    Write-Host ""

    if (-not (Test-Path $src)) {
        throw "ERROR: Cannot reach share at $src"
    }
    if (-not (Test-Path $dst)) {
        throw "ERROR: Destination not found at $dst"
    }

    Write-Host "Updating modified files..." -ForegroundColor Yellow
    $newFiles = @(
        "Database\migrations\add-bookproject-author.sql"
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
        "Models\BookProject.cs",
        "Models\ViewModels\BookEditor\BookProjectViewModel.cs",
        "Controllers\BookProjectController.cs",
        "Controllers\ExportController.cs",
        "Services\EpubExporter.cs",
        "Views\BookProject\Edit.cshtml"
    )
    foreach ($f in $modifiedFiles) {
        $destDir = Split-Path (Join-Path $dst $f) -Parent
        if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir -Force | Out-Null }
        Copy-Item (Join-Path $src $f) (Join-Path $dst $f) -Force
        Write-Host "  UPD: $f" -ForegroundColor Cyan
    }

    Write-Host ""
    Write-Host "============================================" -ForegroundColor Yellow
    Write-Host "Post-copy steps:" -ForegroundColor Yellow
    Write-Host "============================================" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "1. Run the migration SQL against the Seonyx database:" -ForegroundColor White
    Write-Host "   sqlcmd -S localhost -d Seonyx -i Database\migrations\add-bookproject-author.sql" -ForegroundColor Gray
    Write-Host "   (Or open in SSMS and execute.)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "2. Build (Ctrl+Shift+B) in VS 2022." -ForegroundColor White
    Write-Host ""
    Write-Host "3. Edit each book project and set the Author field." -ForegroundColor White
    Write-Host "   The EPUB config page will now default to the author name, not the project name." -ForegroundColor Gray
    Write-Host ""
    Write-Host "4. Re-export Mayfly Mutiny EPUB - CH02-EP001 epigraph will now appear on its own page." -ForegroundColor White
    Write-Host ""
    Write-Host "Copy complete!" -ForegroundColor Green

} catch {
    Write-Host ""
    Write-Host "FAILED: $_" -ForegroundColor Red
} finally {
    Stop-Transcript | Out-Null
}
