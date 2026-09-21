# ============================================
# P3 follow-up fixes
# 1. Bold/Italic toolbar buttons and Ctrl+B/Ctrl+I selected/applied to the
#    whole paragraph instead of the actual selection -- fixed a run-index
#    inversion bug in normaliseRangeToRuns() plus a focus-theft issue on
#    the toolbar mouse-click path.
# 2. Paragraphs still carrying pre-P3 Markdown-marker text (**bold**/
#    *italic*) that predates P2's importer conversion now self-convert
#    into real styled runs the moment they're opened in the editor,
#    persisted immediately so export/search see the fix right away too.
# Run in PowerShell from any directory
# ============================================

$src = "\\192.168.69.75\seonyx-holding"
$dst = "C:\Nextcloud\Work\VITALIS\Seonyx\NEW_SITE_FEB26"
$log = "$env:USERPROFILE\Desktop\copy-updates-log.txt"

Start-Transcript -Path $log -Force | Out-Null
Write-Host "Log: $log"

try {

    Write-Host "P3 follow-up fixes - copy to Dibbler" -ForegroundColor Cyan
    Write-Host "From: $src" -ForegroundColor Gray
    Write-Host "To:   $dst" -ForegroundColor Gray
    Write-Host ""

    if (-not (Test-Path $src)) {
        throw "ERROR: Cannot reach share at $src"
    }
    if (-not (Test-Path $dst)) {
        throw "ERROR: Destination not found at $dst"
    }

    # Modified files
    Write-Host "Updating modified files..." -ForegroundColor Yellow
    $modifiedFiles = @(
        "Scripts\rich-text-editor.js",
        "Controllers\EditorController.cs"
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
    Write-Host ""
    Write-Host "3. Open a Chilli Racers chapter 1 paragraph with *single*/**double**" -ForegroundColor White
    Write-Host "   asterisk markers still showing literally -- confirm it now renders" -ForegroundColor Gray
    Write-Host "   as real italic/bold, not literal asterisks. Reload the page -- confirm" -ForegroundColor Gray
    Write-Host "   it stays converted and nothing errors on the second load." -ForegroundColor Gray
    Write-Host ""
    Write-Host "4. Select a PARTIAL phrase within a paragraph (not the whole thing) and" -ForegroundColor White
    Write-Host "   click the B or I toolbar button -- confirm only the selected phrase" -ForegroundColor Gray
    Write-Host "   is styled, not the entire paragraph. Repeat with Ctrl+B/Ctrl+I." -ForegroundColor Gray
    Write-Host ""
    Write-Host "5. Save and re-export (or check via SSMS) -- confirm ParagraphRuns for" -ForegroundColor White
    Write-Host "   a converted paragraph shows separate styled rows, not one flat run," -ForegroundColor Gray
    Write-Host "   proving the on-load conversion actually persisted." -ForegroundColor Gray
    Write-Host ""
    Write-Host "Copy complete!" -ForegroundColor Green

} catch {
    Write-Host ""
    Write-Host "FAILED: $_" -ForegroundColor Red
} finally {
    Stop-Transcript | Out-Null
}
