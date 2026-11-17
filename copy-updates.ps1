# ============================================
# Zero-run paragraph data-loss fix + run repair action
#
# Fixes the class of bug where a paragraph has text (ParagraphText) but
# no ParagraphRuns rows: the editor showed it as empty, and the next save
# -- even one that only changed META or notes -- erased its prose.
#
# - Shared plain-text route (new Services\PlainTextRunBuilder.cs) used by
#   every writer: strict for BookML import (a malformed marker still
#   aborts the whole import), lenient for legacy .txt import (one plain
#   run plus a warning).
# - Legacy .txt import now writes runs for every paragraph it creates or
#   rewrites. Re-importing an unchanged paragraph leaves its text and runs
#   (including editor-only overlay/small caps/line break/emphasis type)
#   untouched; a changed one is rebuilt and warns if such formatting was
#   lost.
# - Readers (editor load, BookML export, GetProseText,
#   GetProseTextForChapter) never treat text-with-zero-runs as empty.
# - The server refuses a save that would blank a zero-run paragraph, with
#   a visible error; Prev/Next no longer navigate away from a failed save.
# - New admin page: Editor/RepairRuns (read-only preview, POST to run).
# - UPDATE: "Import All Files to Database" now always imports the legacy
#   text files; a BookML package in the same project no longer hijacks it
#   (that package has its own "Import BookML Package" button).
# - UPDATE: opening a paragraph in the editor no longer converts leftover
#   Markdown markers automatically (it also mangled asterisks typed on
#   purpose). Only the run repair converts them, and it now skips -- and
#   lists with a reason -- any conversion that looks unsafe.
# Run in PowerShell from any directory
# ============================================

$src = "\\192.168.69.75\seonyx-holding"
$dst = "C:\Users\Steve\Nextcloud\Work\VITALIS\Seonyx\NEW_SITE_FEB26"
$log = "$env:USERPROFILE\Desktop\copy-updates-log.txt"

Start-Transcript -Path $log -Force | Out-Null
Write-Host "Log: $log"

try {

    Write-Host "Zero-run paragraph fix + run repair - copy to Dibbler" -ForegroundColor Cyan
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
        "Services\PlainTextRunBuilder.cs",
        "Models\ViewModels\BookEditor\RepairRunsViewModel.cs",
        "Views\Editor\RepairRuns.cshtml"
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
        "Services\ParagraphRunService.cs",
        "Services\BookmlImporter.cs",
        "Services\BookmlExporter.cs",
        "Controllers\EditorController.cs",
        "Controllers\FileUploadController.cs",
        "Models\ViewModels\BookEditor\ParagraphEditViewModel.cs",
        "Views\Editor\Index.cshtml",
        "Scripts\book-editor.js",
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
    Write-Host "Post-copy steps (Dibbler only - nothing here touches production):" -ForegroundColor Yellow
    Write-Host "============================================" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "1. Back up the Dibbler Seonyx database first." -ForegroundColor White
    Write-Host ""
    Write-Host "2. Diagnostic count, in SSMS, BEFORE running anything. Note the number:" -ForegroundColor White
    Write-Host "   SELECT COUNT(*) AS ZeroRunParagraphsWithText FROM Paragraphs p" -ForegroundColor Gray
    Write-Host "   WHERE p.ParagraphText_Legacy LIKE '%[^ ' + CHAR(9) + CHAR(10) + CHAR(13) + ']%'" -ForegroundColor Gray
    Write-Host "     AND NOT EXISTS (SELECT 1 FROM ParagraphRuns r WHERE r.ParagraphID = p.ParagraphID);" -ForegroundColor Gray
    Write-Host ""
    Write-Host "3. Build and run (F5), hard refresh once (Ctrl+Shift+R). Open:" -ForegroundColor White
    Write-Host "   https://localhost:44327/admin/bookeditor/editor/RepairRuns" -ForegroundColor Gray
    Write-Host "   This is a PREVIEW - nothing is written. Check:" -ForegroundColor Gray
    Write-Host "   - the Pass A count equals the step 2 number;" -ForegroundColor Gray
    Write-Host "   - the Pass B list. Orange rows (formatting left on text with no" -ForegroundColor Gray
    Write-Host "     letters or digits, e.g. '* * *' becoming an italic space) are listed" -ForegroundColor Gray
    Write-Host "     first. If any Pass B row looks wrong, STOP and report before running." -ForegroundColor Gray
    Write-Host ""
    Write-Host "4. Before running the repair, if step 2 found any paragraphs:" -ForegroundColor White
    Write-Host "   a. Open one from the Pass A list: its prose must be visible, not empty." -ForegroundColor Gray
    Write-Host "   b. Refusal test: in the browser console run" -ForegroundColor Gray
    Write-Host "        window._richTextEditor.setRuns([])" -ForegroundColor Gray
    Write-Host "      then type in META and click Save -> red 'Save refused' message, and" -ForegroundColor Gray
    Write-Host "      the SQL count in step 2 is unchanged. Repeat, but click Next instead" -ForegroundColor Gray
    Write-Host "      of Save -> same message and the page does NOT move on." -ForegroundColor Gray
    Write-Host "   c. Reload, change only META, Save. In SSMS the paragraph now has" -ForegroundColor Gray
    Write-Host "      ParagraphRuns rows and its prose is intact." -ForegroundColor Gray
    Write-Host ""
    Write-Host "5. Pass B check: in a scratch project, pick one paragraph you will NOT open" -ForegroundColor White
    Write-Host "   first and give it a single run of literal Markdown (put its ID in @id):" -ForegroundColor Gray
    Write-Host "     DECLARE @id INT = 0;  -- the scratch ParagraphID" -ForegroundColor Gray
    Write-Host "     DELETE FROM ParagraphRuns WHERE ParagraphID = @id;" -ForegroundColor Gray
    Write-Host "     INSERT INTO ParagraphRuns (ParagraphID, Seq, RunText, IsEmph, IsStrong, IsSmallCaps, IsBreak)" -ForegroundColor Gray
    Write-Host "     VALUES (@id, 1, 'Some **bold** words', 0, 0, 0, 0);" -ForegroundColor Gray
    Write-Host "   Reload the preview: it appears under Pass B." -ForegroundColor Gray
    Write-Host ""
    Write-Host "6. Click 'Run repair'. Then:" -ForegroundColor White
    Write-Host "   - the step 2 SQL now returns 0;" -ForegroundColor Gray
    Write-Host "   - the step 5 paragraph opens with 'bold' as a real bold run;" -ForegroundColor Gray
    Write-Host "   - reload the preview: Pass A 0, Pass B 0. Run repair again: changes 0/0." -ForegroundColor Gray
    Write-Host "   If it says it stopped at the time limit, just run it again." -ForegroundColor Gray
    Write-Host ""
    Write-Host "7. Legacy .txt re-import, in a scratch project:" -ForegroundColor White
    Write-Host "   a. Import a chapter file (e.g. ch01_test.md); every paragraph gets runs." -ForegroundColor Gray
    Write-Host "   b. In the editor, mark a phrase in one paragraph as an overlay." -ForegroundColor Gray
    Write-Host "   c. Re-import the SAME file: the message reports paragraphs 'unchanged'" -ForegroundColor Gray
    Write-Host "      and the overlay is still there." -ForegroundColor Gray
    Write-Host "   d. Edit that paragraph's wording in the file and re-import: the import" -ForegroundColor Gray
    Write-Host "      log has a warning naming the paragraph and the lost 'overlay'." -ForegroundColor Gray
    Write-Host "   e. A paragraph containing ***triple*** imports as plain text with a warning." -ForegroundColor Gray
    Write-Host ""
    Write-Host "8. Regression: edit and save normally; Bold/Italic toolbar and Ctrl+B/I;" -ForegroundColor White
    Write-Host "   overlay round trip; export one chapter as BookML." -ForegroundColor Gray
    Write-Host ""
    Write-Host "9. Import routing (latest update): in the 'Test Formatting' project, which" -ForegroundColor White
    Write-Host "   holds both a BookML package and ch01_reimporttest.md, click 'Import All" -ForegroundColor Gray
    Write-Host "   Files to Database'. It must import the .md file (message mentions" -ForegroundColor Gray
    Write-Host "   paragraphs added/unchanged), NOT the BookML package." -ForegroundColor Gray
    Write-Host ""
    Write-Host "10. No conversion on open (latest update), in the Test Import project:" -ForegroundColor White
    Write-Host "   a. Open RT01-P0003, add the text  a * b * c  at the end, Save, reload." -ForegroundColor Gray
    Write-Host "      The asterisks must still be there, with nothing turned italic." -ForegroundColor Gray
    Write-Host "   b. Open the RepairRuns preview: Pass B lists RT01-P0003 under" -ForegroundColor Gray
    Write-Host "      'Skipped - left untouched' with a reason, and does not convert it." -ForegroundColor Gray
    Write-Host ""
    Write-Host "Copy complete!" -ForegroundColor Green

} catch {
    Write-Host ""
    Write-Host "FAILED: $_" -ForegroundColor Red
} finally {
    Stop-Transcript | Out-Null
}
