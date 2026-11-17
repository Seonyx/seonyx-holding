# ============================================
# Scene breaks: carried through import, editor and every export
#
# BookML <break type="scene|section|page"/> used to be dropped on import
# (and never written on export), so scene breaks vanished. A break is now
# a working-copy row with no text, a B type-code pid (e.g. CH01-B0001,
# generated when the source break has none) and its kind in its
# ParagraphVersion.ParaType (break-scene / break-section / break-page).
# - BookML import keeps breaks in place; BookML export writes them back.
# - Editor shows a break as a divider; Ins Before/After offer "Scene break".
# - EPUB: scene = centred "smiling toad" symbol, section = space, page = page break.
# - Audiobook: "## SCENE_BREAK" (the script's existing 1.5 s pause).
# - Review ODT and plain-text manuscript export: the toad symbol.
# No database changes.
# Run in PowerShell from any directory
# ============================================

$src = "\\192.168.69.75\seonyx-holding"
$dst = "C:\Users\Steve\Nextcloud\Work\VITALIS\Seonyx\NEW_SITE_FEB26"
$log = "$env:USERPROFILE\Desktop\copy-updates-log.txt"

Start-Transcript -Path $log -Force | Out-Null
Write-Host "Log: $log"

try {

    Write-Host "Scene breaks - copy to Mort" -ForegroundColor Cyan
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
        "Services\BreakKinds.cs"
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
        "Services\BookmlImporter.cs",
        "Services\BookmlExporter.cs",
        "Services\EpubExporter.cs",
        "Services\AudiobookPackageBuilder.cs",
        "Services\ChapterReviewOdtExporter.cs",
        "Services\BookFileParser.cs",
        "Controllers\EditorController.cs",
        "Controllers\ExportController.cs",
        "Models\ViewModels\BookEditor\ParagraphEditViewModel.cs",
        "Views\Editor\Index.cshtml",
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
    Write-Host "Post-copy steps (Mort only - nothing here touches production):" -ForegroundColor Yellow
    Write-Host "============================================" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "1. No DB changes. Build and run (F5)." -ForegroundColor White
    Write-Host ""
    Write-Host "2. Import: create a NEW scratch project (e.g. 'Scene Break Test'), upload" -ForegroundColor White
    Write-Host "   testdata\carrier-to-yesterday-draft1.bookml.zip from the share, click" -ForegroundColor Gray
    Write-Host "   'Import BookML Package'. Do NOT re-import into the real Carrier To" -ForegroundColor Gray
    Write-Host "   Yesterday project (that would reset its text to the package version)." -ForegroundColor Gray
    Write-Host "   The import should succeed; breaks count among 'paragraphs added'." -ForegroundColor Gray
    Write-Host ""
    Write-Host "3. Editor: Go to CH01-B0001. It shows a divider (the toad symbol over" -ForegroundColor White
    Write-Host "   'SCENE BREAK') instead of a text box, between the right paragraphs." -ForegroundColor Gray
    Write-Host "   Prev/Next move through it normally. META/notes still save." -ForegroundColor Gray
    Write-Host ""
    Write-Host "4. Insert: on any paragraph choose 'Scene break' in the Ins After dropdown" -ForegroundColor White
    Write-Host "   and click Ins After. A divider appears with an id like CHnn-B-XXXXXXXX." -ForegroundColor Gray
    Write-Host "   Then Delete it (normal Delete button)." -ForegroundColor Gray
    Write-Host ""
    Write-Host "5. Round trip: Export the scratch project as BookML, then import that" -ForegroundColor White
    Write-Host "   export into a SECOND new scratch project. The breaks must still be" -ForegroundColor Gray
    Write-Host "   there, in the same places (open the zip: chapter XML has <break .../>)." -ForegroundColor Gray
    Write-Host ""
    Write-Host "6. EPUB: export from the scratch project and open in Calibre. Scene breaks" -ForegroundColor White
    Write-Host "   show as a centred toad symbol between scenes." -ForegroundColor Gray
    Write-Host ""
    Write-Host "7. Audiobook: export the package and open the ZIP; the chapter text files" -ForegroundColor White
    Write-Host "   contain '## SCENE_BREAK' lines where the breaks are." -ForegroundColor Gray
    Write-Host ""
    Write-Host "8. Review ODT and the plain-text manuscript export both show the toad" -ForegroundColor White
    Write-Host "   symbol at breaks." -ForegroundColor Gray
    Write-Host ""
    Write-Host "9. Regression: edit/save a normal paragraph; export a Chilli Racers chapter" -ForegroundColor White
    Write-Host "   as BookML (no breaks there - should look exactly as before)." -ForegroundColor Gray
    Write-Host ""
    Write-Host "Copy complete!" -ForegroundColor Green

} catch {
    Write-Host ""
    Write-Host "FAILED: $_" -ForegroundColor Red
} finally {
    Stop-Transcript | Out-Null
}
