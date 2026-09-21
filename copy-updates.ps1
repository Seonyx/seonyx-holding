# ============================================
# BookML Inline Presentation - Phase P3 Half B
# Adds the right-click context menu, overlay marking (with autocomplete
# from previously-used types in the project), emphasis sub-type
# assignment (fixed thought/foreign/title/ship-name dropdown), and small
# caps toggle to the rich text paragraph editor. Builds entirely on Half
# A's existing engine (Scripts\rich-text-editor.js) -- no changes to its
# typing/rendering pipeline, only new exported primitives
# (applyEmphType, toggleStyleForRange) and a bug fix to applyStyleToRange
# (it was never actually called by Half A, so a real defect in it went
# undetected until Half B's real-browser tests exercised it directly).
# Run in PowerShell from any directory
# ============================================

$src = "\\192.168.69.75\seonyx-holding"
$dst = "C:\Nextcloud\Work\VITALIS\Seonyx\NEW_SITE_FEB26"
$log = "$env:USERPROFILE\Desktop\copy-updates-log.txt"

Start-Transcript -Path $log -Force | Out-Null
Write-Host "Log: $log"

try {

    Write-Host "BookML Inline Presentation P3 Half B - copy to Dibbler" -ForegroundColor Cyan
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
        "Scripts\rich-text-context-menu.js",
        "Scripts\rich-text-dialogs.js"
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
        "Scripts\rich-text-editor.js",
        "Controllers\EditorController.cs",
        "Views\Editor\Index.cshtml",
        "Views\Shared\_BookEditorLayout.cshtml",
        "Content\css\book-editor.css",
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
    Write-Host "1. No DB changes needed (GetOverlayTypes is a read-only query against" -ForegroundColor White
    Write-Host "   the existing ParagraphRuns table)." -ForegroundColor Gray
    Write-Host ""
    Write-Host "2. Build and run the web app (Seonyx.sln, F5)." -ForegroundColor White
    Write-Host "   Do ONE hard refresh (Ctrl+Shift+R) after this deploy -- from then on," -ForegroundColor Gray
    Write-Host "   cache-busting should keep ordinary reloads current automatically." -ForegroundColor Gray
    Write-Host ""
    Write-Host "3. Overlay round-trip: select a phrase, right-click, 'Mark as overlay...'," -ForegroundColor White
    Write-Host "   type a type value (e.g. 'hud'), OK -- confirm it renders as a teal pill" -ForegroundColor Gray
    Write-Host "   with the type shown on hover. Save, reload -- confirm it persists." -ForegroundColor Gray
    Write-Host ""
    Write-Host "4. Overlay autocomplete: after creating one overlay type in a project," -ForegroundColor White
    Write-Host "   open the dialog again on a different paragraph in the SAME project --" -ForegroundColor Gray
    Write-Host "   confirm that type appears as a suggestion." -ForegroundColor Gray
    Write-Host ""
    Write-Host "5. Remove overlay: right-click inside an existing overlay, 'Remove overlay'" -ForegroundColor White
    Write-Host "   -- confirm the pill disappears and text becomes plain." -ForegroundColor Gray
    Write-Host ""
    Write-Host "6. Emphasis type: select an ALREADY-ITALIC phrase, right-click -- confirm" -ForegroundColor White
    Write-Host "   'Set emphasis type...' appears; select a MIXED (partly italic) phrase --" -ForegroundColor Gray
    Write-Host "   confirm it does NOT appear. Pick a value, OK -- confirm it applies." -ForegroundColor Gray
    Write-Host "   Re-open the menu on that same phrase -- confirm the dialog pre-selects" -ForegroundColor Gray
    Write-Host "   the value you just set. 'Remove emphasis type' clears the sub-type" -ForegroundColor Gray
    Write-Host "   without un-italicizing the text." -ForegroundColor Gray
    Write-Host ""
    Write-Host "7. Small caps: select a phrase, right-click, 'Apply small caps' -- confirm" -ForegroundColor White
    Write-Host "   visual rendering; re-open the menu -- confirm it now reads 'Remove" -ForegroundColor Gray
    Write-Host "   small caps' and toggles back correctly." -ForegroundColor Gray
    Write-Host ""
    Write-Host "8. Menu dismissal: open the menu, click away -- confirm it closes with no" -ForegroundColor White
    Write-Host "   side effects. Open it again, press Escape -- confirm it closes." -ForegroundColor Gray
    Write-Host ""
    Write-Host "9. IMPORTANT -- collapsed-selection right-click: click once with NOTHING" -ForegroundColor White
    Write-Host "   selected, then right-click -- confirm the NATIVE browser context menu" -ForegroundColor Gray
    Write-Host "   appears (spell-check, Copy/Paste, Inspect), NOT the custom menu and NOT" -ForegroundColor Gray
    Write-Host "   nothing. This was a deliberate fix during plan review -- worth extra" -ForegroundColor Gray
    Write-Host "   attention since getting this wrong would silently break spell-check." -ForegroundColor Gray
    Write-Host ""
    Write-Host "10. Regression check: re-confirm Bold/Italic (toolbar and Ctrl+B/Ctrl+I)" -ForegroundColor White
    Write-Host "    still work correctly on a partial selection." -ForegroundColor Gray
    Write-Host ""
    Write-Host "Copy complete!" -ForegroundColor Green

} catch {
    Write-Host ""
    Write-Host "FAILED: $_" -ForegroundColor Red
} finally {
    Stop-Transcript | Out-Null
}
