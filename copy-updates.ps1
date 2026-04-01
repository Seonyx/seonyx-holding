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

    Write-Host "Generate EPUB export feature" -ForegroundColor Cyan
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
    $modifiedFiles = @(
        "Controllers\ExportController.cs",
        "Services\EpubExporter.cs",
        "Models\ViewModels\BookEditor\EpubConfigViewModel.cs",
        "Views\Export\EpubConfig.cshtml"
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
    Write-Host "1. Build (Ctrl+Shift+B) in VS 2022 - no new NuGet packages needed." -ForegroundColor White
    Write-Host "2. Run (F5) and navigate to any project's Export page." -ForegroundColor White
    Write-Host "3. Click 'Configure & Generate' to open the EPUB config form." -ForegroundColor White
    Write-Host "4. Fill in rights holder, year, optionally tick ARC and pick a cover image." -ForegroundColor White
    Write-Host "5. Click Generate EPUB - the .epub file should download." -ForegroundColor White
    Write-Host "6. Open the downloaded .epub in Calibre to verify chapter order and content." -ForegroundColor White
    Write-Host ""
    Write-Host "Copy complete!" -ForegroundColor Green

} catch {
    Write-Host ""
    Write-Host "FAILED: $_" -ForegroundColor Red
} finally {
    Stop-Transcript | Out-Null
}
