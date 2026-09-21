# ============================================
# Cache-busting for book-editor static assets
# The last round of P3 fixes (rich-text-editor.js) appeared broken after
# deploy purely because the browser was serving a cached copy of the old
# file -- a plain reload didn't pick up the change, only a hard refresh
# did. Adds a file-last-write-time query string to book-editor.css,
# rich-text-editor.js, and book-editor.js so an ordinary reload always
# gets the current version after any future deploy.
# Run in PowerShell from any directory
# ============================================

$src = "\\192.168.69.75\seonyx-holding"
$dst = "C:\Nextcloud\Work\VITALIS\Seonyx\NEW_SITE_FEB26"
$log = "$env:USERPROFILE\Desktop\copy-updates-log.txt"

Start-Transcript -Path $log -Force | Out-Null
Write-Host "Log: $log"

try {

    Write-Host "Cache-busting for book-editor assets - copy to Dibbler" -ForegroundColor Cyan
    Write-Host "From: $src" -ForegroundColor Gray
    Write-Host "To:   $dst" -ForegroundColor Gray
    Write-Host ""

    if (-not (Test-Path $src)) {
        throw "ERROR: Cannot reach share at $src"
    }
    if (-not (Test-Path $dst)) {
        throw "ERROR: Destination not found at $dst"
    }

    # New files
    Write-Host "Copying new files..." -ForegroundColor Yellow
    $newFiles = @(
        "Helpers\AssetVersion.cs"
    )
    foreach ($f in $newFiles) {
        $destDir = Split-Path (Join-Path $dst $f) -Parent
        if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir -Force | Out-Null }
        Copy-Item (Join-Path $src $f) (Join-Path $dst $f) -Force
        Write-Host "  NEW: $f" -ForegroundColor Green
    }

    # Modified files
    Write-Host ""
    Write-Host "Updating modified files..." -ForegroundColor Yellow
    $modifiedFiles = @(
        "Views\Shared\_BookEditorLayout.cshtml",
        "Seonyx.csproj"
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
    Write-Host "1. No DB changes needed." -ForegroundColor White
    Write-Host ""
    Write-Host "2. Build and run the web app (Seonyx.sln, F5)." -ForegroundColor White
    Write-Host "   Do ONE hard refresh (Ctrl+Shift+R) now to clear any already-cached" -ForegroundColor Gray
    Write-Host "   copies of book-editor.css/rich-text-editor.js/book-editor.js from" -ForegroundColor Gray
    Write-Host "   before this fix -- from this point on, an ordinary reload after any" -ForegroundColor Gray
    Write-Host "   future deploy should pick up changes automatically." -ForegroundColor Gray
    Write-Host ""
    Write-Host "3. View page source and confirm the script/link tags for book-editor.css," -ForegroundColor White
    Write-Host "   rich-text-editor.js, and book-editor.js now have a '?v=...' query" -ForegroundColor Gray
    Write-Host "   string on them." -ForegroundColor Gray
    Write-Host ""
    Write-Host "4. Make a trivial edit to rich-text-editor.js on Vimes, re-copy, restart" -ForegroundColor White
    Write-Host "   the app (new F5), and do a PLAIN reload (not hard refresh) of the" -ForegroundColor Gray
    Write-Host "   editor page -- confirm the change is picked up without a hard refresh." -ForegroundColor Gray
    Write-Host ""
    Write-Host "Copy complete!" -ForegroundColor Green

} catch {
    Write-Host ""
    Write-Host "FAILED: $_" -ForegroundColor Red
} finally {
    Stop-Transcript | Out-Null
}
