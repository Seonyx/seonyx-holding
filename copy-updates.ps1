# ============================================
# Import progress + route fix
# Run in PowerShell from any directory
# ============================================

$src = "\\192.168.69.75\seonyx-holding"
$dst = "C:\Users\Steve\Dropbox\VITALIS\Seonyx\NEW_SITE_FEB26"
$log = "$env:USERPROFILE\Desktop\copy-updates-log.txt"

Start-Transcript -Path $log -Force | Out-Null
Write-Host "Log: $log"

try {

    Write-Host "Epigraph paragraph support in book editor" -ForegroundColor Cyan
    Write-Host "From: $src" -ForegroundColor Gray
    Write-Host "To:   $dst" -ForegroundColor Gray
    Write-Host ""

    if (-not (Test-Path $src)) {
        throw "ERROR: Cannot reach share at $src"
    }
    if (-not (Test-Path $dst)) {
        throw "ERROR: Destination not found at $dst"
    }

    # Modified files to update
    Write-Host "Updating modified files..." -ForegroundColor Yellow
    $modifiedFiles = @(
        "Services\BookmlImporter.cs"
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
    Write-Host "1. No SQL changes." -ForegroundColor White
    Write-Host ""
    Write-Host "2. In VS 2022, Build (Ctrl+Shift+B) then F5 to run." -ForegroundColor White
    Write-Host ""
    Write-Host "3. One change in this build:" -ForegroundColor White
    Write-Host "   - Epigraph paragraphs (<chapterinfo><epigraph>) now extracted" -ForegroundColor Gray
    Write-Host "     by the importer and shown in the book editor like regular" -ForegroundColor Gray
    Write-Host "     paragraphs. Previously they were silently skipped." -ForegroundColor Gray
    Write-Host ""
    Write-Host "4. After copying, re-import the Draft 2 ZIP to pick up the" -ForegroundColor White
    Write-Host "   epigraph paragraphs. They will appear first in each chapter." -ForegroundColor Gray
    Write-Host ""
    Write-Host "Copy complete!" -ForegroundColor Green

} catch {
    Write-Host ""
    Write-Host "FAILED: $_" -ForegroundColor Red
} finally {
    Stop-Transcript | Out-Null
}
