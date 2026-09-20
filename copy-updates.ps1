# ============================================
# BookML Inline Presentation - Phase P3 Half A
# Converts the plain-textarea paragraph editor into a run-native
# contenteditable rich text editor: Bold (Ctrl+B), Italic (Ctrl+I), and
# Shift+Enter/Enter line breaks, backed directly by ParagraphRuns. Removes
# the P2-era SyncParagraphRuns markdown-resync shim entirely -- the editor
# itself is now the source of truth for styled content.
# Half B (context menu, overlay dialog, emphasis type, small caps) is a
# separate, later phase -- not included in this copy.
# Run in PowerShell from any directory
# ============================================

$src = "\\192.168.69.75\seonyx-holding"
$dst = "C:\Nextcloud\Work\VITALIS\Seonyx\NEW_SITE_FEB26"
$log = "$env:USERPROFILE\Desktop\copy-updates-log.txt"

Start-Transcript -Path $log -Force | Out-Null
Write-Host "Log: $log"

try {

    Write-Host "BookML Inline Presentation P3 Half A - copy to Dibbler" -ForegroundColor Cyan
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
        "Scripts\rich-text-editor.js"
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
        "Views\Editor\Index.cshtml",
        "Views\Shared\_BookEditorLayout.cshtml",
        "Scripts\book-editor.js",
        "Content\css\book-editor.css",
        "Controllers\EditorController.cs",
        "Models\ViewModels\BookEditor\ParagraphEditViewModel.cs",
        "Services\ParagraphRunService.cs",
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
    Write-Host "1. No DB changes for this phase (schema already covers ParagraphRuns)." -ForegroundColor White
    Write-Host ""
    Write-Host "2. Build and run the web app (Seonyx.sln, F5)." -ForegroundColor White
    Write-Host ""
    Write-Host "3. Open a paragraph imported via P2 with existing emph/strong spans" -ForegroundColor White
    Write-Host "   (e.g. reimport testdata\junk_draft2.bookml.zip, chapter 04) -- confirm" -ForegroundColor Gray
    Write-Host "   it renders as real bold/italic in the editor, not flattened text." -ForegroundColor Gray
    Write-Host ""
    Write-Host "4. Type new text, select across a run boundary, apply Ctrl+B/Ctrl+I --" -ForegroundColor White
    Write-Host "   confirm styling applies correctly and save succeeds (no coalescing" -ForegroundColor Gray
    Write-Host "   errors, which would mean adjacent same-style runs weren't merged)." -ForegroundColor Gray
    Write-Host ""
    Write-Host "5. Confirm Ctrl+I inside the editor toggles italic, and Ctrl+I with focus" -ForegroundColor White
    Write-Host "   OUTSIDE the editor (e.g. a nav button) still does 'insert paragraph" -ForegroundColor Gray
    Write-Host "   after' as before -- both behaviours must coexist correctly." -ForegroundColor Gray
    Write-Host ""
    Write-Host "6. Save via both the Save button and 3-second autosave -- confirm" -ForegroundColor White
    Write-Host "   ParagraphRuns persisted and paragraph search still finds the paragraph" -ForegroundColor Gray
    Write-Host "   by its new text (ParagraphText legacy column must stay in sync)." -ForegroundColor Gray
    Write-Host ""
    Write-Host "7. Press Enter or Shift+Enter mid-paragraph -- confirm a line-break marker" -ForegroundColor White
    Write-Host "   appears, persists on save, and re-loads correctly." -ForegroundColor Gray
    Write-Host ""
    Write-Host "8. Paste plain text and rich text (e.g. from Word) -- confirm both paste" -ForegroundColor White
    Write-Host "   as plain text cleanly, no broken DOM state." -ForegroundColor Gray
    Write-Host ""
    Write-Host "Copy complete!" -ForegroundColor Green

} catch {
    Write-Host ""
    Write-Host "FAILED: $_" -ForegroundColor Red
} finally {
    Stop-Transcript | Out-Null
}
