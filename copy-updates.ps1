# ============================================
# BookML Inline Presentation - Phase P2
# Wires the BookML importer/exporter to ParagraphRuns: a Markdown-subset
# parser, an XML<->runs converter (both directions), a runs-to-Markdown
# serializer, XSD changes for the new inline elements (emph/strong/
# smallcaps/overlay/linebreak), and a save-side sync shim in
# EditorController so the P1-era textarea editor doesn't desync runs
# from ParagraphText until P3 lands. RoundTripCheckCli (new, net462
# console app) replaces automated tests for this phase.
# Run in PowerShell from any directory
# ============================================

$src = "\\192.168.69.75\seonyx-holding"
$dst = "C:\Nextcloud\VITALIS\Seonyx\NEW_SITE_FEB26"
$log = "$env:USERPROFILE\Desktop\copy-updates-log.txt"

Start-Transcript -Path $log -Force | Out-Null
Write-Host "Log: $log"

try {

    Write-Host "BookML Inline Presentation P2 - copy to Dibbler" -ForegroundColor Cyan
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
        "Services\MarkdownInlineParser.cs",
        "Services\InlineXmlRunConverter.cs",
        "Services\RunsToMarkdownSerializer.cs"
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
        "App_Data\BookML\bookml-common.xsd",
        "App_Data\BookML\bookml-chapter.xsd",
        "Services\BookmlImporter.cs",
        "Services\BookmlExporter.cs",
        "Controllers\EditorController.cs",
        "Scripts\book-editor.js",
        "Seonyx.csproj",
        "Documentation\BookML-Inline-Presentation-Amendment.md",
        "Documentation\CC-Brief-BookML-Inline-Presentation.md"
    )
    foreach ($f in $modifiedFiles) {
        $destDir = Split-Path (Join-Path $dst $f) -Parent
        if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir -Force | Out-Null }
        Copy-Item (Join-Path $src $f) (Join-Path $dst $f) -Force
        Write-Host "  UPD: $f" -ForegroundColor Cyan
    }

    # New project: RoundTripCheckCli (whole tree, including Fixtures/)
    Write-Host ""
    Write-Host "Copying RoundTripCheckCli project (new)..." -ForegroundColor Yellow
    $rtcSrc = Join-Path $src "RoundTripCheckCli"
    $rtcDst = Join-Path $dst "RoundTripCheckCli"
    if (Test-Path $rtcDst) { Remove-Item $rtcDst -Recurse -Force }
    Copy-Item $rtcSrc $rtcDst -Recurse -Force
    Write-Host "  NEW: RoundTripCheckCli\ (project + Program.cs + 22 fixtures + README)" -ForegroundColor Green

    Write-Host ""
    Write-Host "============================================" -ForegroundColor Yellow
    Write-Host "Post-copy steps:" -ForegroundColor Yellow
    Write-Host "============================================" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "1. No DB changes for this phase (P1's migration already covers the schema)." -ForegroundColor White
    Write-Host ""
    Write-Host "2. Build and run RoundTripCheckCli FIRST, before the web app:" -ForegroundColor White
    Write-Host "   cd $dst\RoundTripCheckCli" -ForegroundColor Gray
    Write-Host "   dotnet build" -ForegroundColor Gray
    Write-Host "   dotnet run -- roundtrip --fixtures Fixtures" -ForegroundColor Gray
    Write-Host "   All 22 fixtures must show PASS. This is the primary automated gate" -ForegroundColor Gray
    Write-Host "   for this phase (no test project exists in the solution)." -ForegroundColor Gray
    Write-Host ""
    Write-Host "3. Build and run the web app (Seonyx.sln, F5)." -ForegroundColor White
    Write-Host "   Reimport a BookML chapter with existing emph/strong spans" -ForegroundColor Gray
    Write-Host "   (e.g. a copy of testdata\junk_draft2\ch04\ch04-chapter.xml) and confirm" -ForegroundColor Gray
    Write-Host "   the emphasis survives as styled runs, not flattened plain text." -ForegroundColor Gray
    Write-Host ""
    Write-Host "4. Edit that same paragraph UNCHANGED through the editor textarea and save," -ForegroundColor White
    Write-Host "   then re-export -- confirm the emphasis still survives (this is the" -ForegroundColor Gray
    Write-Host "   specific regression the P2 plan's decision #6 fix was written to prevent)." -ForegroundColor Gray
    Write-Host ""
    Write-Host "5. Try saving a paragraph with deliberately malformed markdown (***triple***)" -ForegroundColor White
    Write-Host "   through the editor -- confirm the save still succeeds with a warning," -ForegroundColor Gray
    Write-Host "   rather than failing or destroying existing runs." -ForegroundColor Gray
    Write-Host ""
    Write-Host "Copy complete!" -ForegroundColor Green

} catch {
    Write-Host ""
    Write-Host "FAILED: $_" -ForegroundColor Red
} finally {
    Stop-Transcript | Out-Null
}
