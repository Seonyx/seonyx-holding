# ============================================
# P5: overlay rendering in EPUB and audiobook (BookML Amendment 2)
#
# - EPUB: an overlay inside a sentence becomes <span class="overlay overlay-X">;
#   a paragraph that is entirely one overlay becomes
#   <aside class="overlay overlay-X"><p>...</p></aside>. X is the type
#   lower-cased with anything else turned into hyphens ("Sponsored Alert"
#   -> overlay-sponsored-alert). Bold/italic/small caps also now come
#   through from the editor's formatting (previously plain text only).
# - Each project has its own overlay-styles.css in its project folder
#   (App_Data\BookEditorFiles\<project>\). It is created with defaults the
#   first time it is needed (new project, or first EPUB export) and bundled
#   into every EPUB. Edit it by hand (FTP on production) - no admin page.
#   The .overlay baseline rule means overlay types with no rule of their own
#   still look like overlays.
# - Audiobook: overlays are left out of the narration by default. New
#   "Include overlays" tick box reads them aloud with a 0.5 s pause before
#   and after ("## PAUSE" lines). The overlay type never affects the audio.
# No database changes.
# Run in PowerShell from any directory
# ============================================

$src = "\\192.168.69.75\seonyx-holding"
$dst = "C:\Users\Steve\Nextcloud\Work\VITALIS\Seonyx\NEW_SITE_FEB26"
$log = "$env:USERPROFILE\Desktop\copy-updates-log.txt"

Start-Transcript -Path $log -Force | Out-Null
Write-Host "Log: $log"

try {

    Write-Host "P5 overlay rendering - copy to Mort" -ForegroundColor Cyan
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
        "Services\OverlayStyles.cs",
        "Services\RunsToXhtml.cs"
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
        "Services\EpubExporter.cs",
        "Services\AudiobookPackageBuilder.cs",
        "Services\PythonScript.cs",
        "Controllers\ExportController.cs",
        "Controllers\BookProjectController.cs",
        "Models\ViewModels\BookEditor\AudiobookConfigViewModel.cs",
        "Views\Export\AudiobookConfig.cshtml",
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
    Write-Host "2. Set up overlays: in a scratch project (e.g. 'Scene Break Test'), in the" -ForegroundColor White
    Write-Host "   editor mark three overlays with right-click > Mark as overlay:" -ForegroundColor Gray
    Write-Host "     a) a few words in the middle of a sentence, type: hud" -ForegroundColor Gray
    Write-Host "     b) the whole of a short paragraph, type: Sponsored Alert" -ForegroundColor Gray
    Write-Host "     c) a few words somewhere else, type: Mystery Kind (no style rule)" -ForegroundColor Gray
    Write-Host "   Also bold one word and italicise another in a normal paragraph. Save." -ForegroundColor Gray
    Write-Host ""
    Write-Host "3. EPUB: export the scratch project and open it in Calibre." -ForegroundColor White
    Write-Host "   a) green-on-black monospace text, staying inside its sentence" -ForegroundColor Gray
    Write-Host "   b) an orange-bordered box on its own, full width" -ForegroundColor Gray
    Write-Host "   c) pale blue box with a border (the baseline) - NOT plain text" -ForegroundColor Gray
    Write-Host "   The bold and italic words are bold and italic. Scene breaks still show" -ForegroundColor Gray
    Write-Host "   the toad symbol." -ForegroundColor Gray
    Write-Host ""
    Write-Host "4. Stylesheet file: after the export, this file exists:" -ForegroundColor White
    Write-Host "   App_Data\BookEditorFiles\<scratch project folder>\overlay-styles.css" -ForegroundColor Gray
    Write-Host "   Open it in Notepad, change the .overlay-hud 'color: #0f0;' to" -ForegroundColor Gray
    Write-Host "   'color: #f00;', save, export the EPUB again: the hud text is now red." -ForegroundColor Gray
    Write-Host ""
    Write-Host "5. New project: create a new project; its folder gets overlay-styles.css" -ForegroundColor White
    Write-Host "   straight away. (Delete the project afterwards if you like.)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "6. Audiobook, default: export the scratch project's audiobook package with" -ForegroundColor White
    Write-Host "   'Include overlays' NOT ticked. Open the ZIP: the chapter text files do" -ForegroundColor Gray
    Write-Host "   not contain the overlay words; the sentence around (a) reads naturally" -ForegroundColor Gray
    Write-Host "   with no doubled-up words; paragraph (b) is missing entirely. README" -ForegroundColor Gray
    Write-Host "   says overlays are NOT read aloud." -ForegroundColor Gray
    Write-Host ""
    Write-Host "7. Audiobook, included: export again WITH 'Include overlays' ticked. Each" -ForegroundColor White
    Write-Host "   overlay sits on its own line between two '## PAUSE' lines. README says" -ForegroundColor Gray
    Write-Host "   overlays are INCLUDED. (Optional: run the package's generate script" -ForegroundColor Gray
    Write-Host "   and listen for the short pauses.)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "8. Regression: export a Chilli Racers EPUB and audiobook (no overlays) -" -ForegroundColor White
    Write-Host "   should read exactly as before, apart from bold/italic now showing." -ForegroundColor Gray
    Write-Host ""
    Write-Host "Copy complete!" -ForegroundColor Green

} catch {
    Write-Host ""
    Write-Host "FAILED: $_" -ForegroundColor Red
} finally {
    Stop-Transcript | Out-Null
}
