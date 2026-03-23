# ============================================
# Favicon support
# Run in PowerShell from any directory
# ============================================

$src = "\\192.168.69.75\seonyx-holding"
$dst = "C:\Users\Steve\Dropbox\VITALIS\Seonyx\NEW_SITE_FEB26"
$log = "$env:USERPROFILE\Desktop\copy-updates-log.txt"

Start-Transcript -Path $log -Force | Out-Null
Write-Host "Log: $log"

try {

    Write-Host "Favicon support" -ForegroundColor Cyan
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
        "favicon.ico",
        "favicon-16x16.png",
        "favicon-32x32.png",
        "apple-touch-icon.png",
        "android-chrome-192x192.png",
        "android-chrome-512x512.png",
        "site.webmanifest"
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
        "Views\Shared\_Layout.cshtml",
        "Views\Shared\_AdminLayout.cshtml",
        "Views\Shared\_BookEditorLayout.cshtml"
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
    Write-Host "3. Change in this build:" -ForegroundColor White
    Write-Host "   - Favicon added to all pages (public site, admin, book editor)." -ForegroundColor Gray
    Write-Host "     The browser tab icon error in DevTools should be gone." -ForegroundColor Gray
    Write-Host ""
    Write-Host "Copy complete!" -ForegroundColor Green

} catch {
    Write-Host ""
    Write-Host "FAILED: $_" -ForegroundColor Red
} finally {
    Stop-Transcript | Out-Null
}
