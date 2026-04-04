# ============================================
# Audiobook Package export + Content Analysis Engine
# Run in PowerShell from any directory
# ============================================

$src = "\\192.168.69.75\seonyx-holding"
$dst = "C:\Users\Steve\Dropbox\VITALIS\Seonyx\NEW_SITE_FEB26"
$log = "$env:USERPROFILE\Desktop\copy-updates-log.txt"

Start-Transcript -Path $log -Force | Out-Null
Write-Host "Log: $log"

try {

    Write-Host "Audiobook Package export - copy to Dibbler" -ForegroundColor Cyan
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
        "Services\AudiobookPackageBuilder.cs",
        "Services\PythonScript.cs",
        "Services\VoiceLibrary.cs",
        "Models\ViewModels\BookEditor\AudiobookConfigViewModel.cs",
        "Views\Export\AudiobookConfig.cshtml",
        "ContentAnalysisEngine\ContentAnalysisEngine.csproj",
        "ContentAnalysisEngine\AnalysisConfiguration.cs",
        "ContentAnalysisEngine\AnalysisReport.cs",
        "ContentAnalysisEngine\BookmlReader.cs",
        "ContentAnalysisEngine\ChapterAnalyser.cs",
        "ContentAnalysisEngine\Tokeniser.cs",
        "ContentAnalysisEngine\Metrics\WordFrequencyMetric.cs",
        "ContentAnalysisEngine\Metrics\NgramMetric.cs",
        "ContentAnalysisEngine\Metrics\ProximityEchoMetric.cs",
        "ContentAnalysisEngine\Metrics\TtrMetric.cs",
        "ContentAnalysisEngine\Metrics\HapaxMetric.cs",
        "ContentAnalysisHarness\ContentAnalysisHarness.csproj",
        "ContentAnalysisHarness\Program.cs",
        "ContentAnalysisHarness\Properties\AssemblyInfo.cs",
        "ContentAnalysisEngine\Properties\AssemblyInfo.cs"
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
        "Controllers\ExportController.cs",
        "Controllers\FileUploadController.cs",
        "Services\BookmlImporter.cs",
        "Views\Export\Index.cshtml",
        "Seonyx.csproj",
        "App_Data\BookML\bookml-common.xsd",
        "Seonyx.sln"
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
    Write-Host "1. Build (Ctrl+Shift+B) in VS 2022 - solution now has 3 projects." -ForegroundColor White
    Write-Host "   No DB changes. No new NuGet packages (Newtonsoft.Json already in packages/)." -ForegroundColor White
    Write-Host "2. Test Content Analysis Engine:" -ForegroundColor White
    Write-Host "   Set ContentAnalysisHarness as startup project, run with args:" -ForegroundColor White
    Write-Host "   --chapter <path-to-any-chapter.xml>" -ForegroundColor White
    Write-Host "   e.g. --chapter C:\...\testdata\junk_draft2\ch01\ch01-chapter.xml" -ForegroundColor White
    Write-Host "   Expected: JSON report to stdout with metrics, flaggedWords, flaggedNgrams, proximityEchoes." -ForegroundColor White
    Write-Host "3. Test Audiobook export: open any book project, go to Export." -ForegroundColor White
    Write-Host "   'Audiobook Package' card -> select draft + voice -> Download Package." -ForegroundColor White
    Write-Host ""
    Write-Host "Copy complete!" -ForegroundColor Green

} catch {
    Write-Host ""
    Write-Host "FAILED: $_" -ForegroundColor Red
} finally {
    Stop-Transcript | Out-Null
}
