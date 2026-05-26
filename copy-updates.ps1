# ============================================
# Chapter Review ODT Export feature
# + Solution split: ContentAnalyserCli.sln
# + ContentAnalysisEngine names.xml expansion + stop-word filtering
# + WorkOrderValidator ban-check scope + word-count tolerance
# Run in PowerShell from any directory
# ============================================

$src = "\\192.168.69.75\seonyx-holding"
$dst = "C:\Users\Steve\Dropbox\VITALIS\Seonyx\NEW_SITE_FEB26"
$log = "$env:USERPROFILE\Desktop\copy-updates-log.txt"

Start-Transcript -Path $log -Force | Out-Null
Write-Host "Log: $log"

try {

    Write-Host "Phase 3 & 4 (Revised) + fixes - copy to Dibbler" -ForegroundColor Cyan
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
        "Services\ChapterReviewOdtExporter.cs",
        "ContentAnalyserCli.sln",
        "ContentAnalysisEngine\NamesReader.cs",
        "ContentAnalysisEngine\BookReader.cs",
        "ContentAnalysisEngine\BookAnalyser.cs",
        "ContentAnalysisEngine\BookAnalysisReport.cs",
        "ContentAnalysisEngine\LexicalSummaryGenerator.cs",
        "ContentAnalysisEngine\BookDeltaComparer.cs"
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
        "Seonyx.sln",
        "Seonyx.csproj",
        "Controllers\ExportController.cs",
        "Views\Export\Index.cshtml",
        "ContentAnalysisEngine\ContentAnalysisEngine.csproj",
        "ContentAnalysisEngine\WorkOrderValidator.cs",
        "ContentAnalyserCli\Program.cs",
        "Views\Character\Edit.cshtml",
        "Controllers\AdminController.cs",
        "Global.asax.cs",
        "Services\BookmlExporter.cs"
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
    Write-Host "1. Build and run the web app (Seonyx.sln, F5)." -ForegroundColor White
    Write-Host "   Test: open the Export page for any book, click 'Export for Review' on a chapter." -ForegroundColor Gray
    Write-Host "   Expected: browser downloads *-review.odt. Open in LibreOffice or Word." -ForegroundColor Gray
    Write-Host "   Verify: double spacing, PID labels in grey Courier, 1.5in margins, title centred bold." -ForegroundColor Gray
    Write-Host ""
    Write-Host "2. CLI work -- open ContentAnalyserCli.sln in a separate VS window." -ForegroundColor White
    Write-Host "   Ctrl+Shift+B builds the .NET 8 project; publish for Linux:" -ForegroundColor Gray
    Write-Host "   cd $dst\ContentAnalyserCli" -ForegroundColor Gray
    Write-Host "   dotnet publish -c Release -r linux-x64 --self-contained true -o publish\linux-x64" -ForegroundColor Gray
    Write-Host "   (copy publish\linux-x64\ContentAnalyserCli to ~/tools/content-analyser/ on Binky)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Copy complete!" -ForegroundColor Green

} catch {
    Write-Host ""
    Write-Host "FAILED: $_" -ForegroundColor Red
} finally {
    Stop-Transcript | Out-Null
}
