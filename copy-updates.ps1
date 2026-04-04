# ============================================
# Phase 2 Session 1: Work Order Schema + Generation
# + Analysis Report XML serialisation (Phase 1 amendment)
# Run in PowerShell from any directory
# ============================================

$src = "\\192.168.69.75\seonyx-holding"
$dst = "C:\Users\Steve\Dropbox\VITALIS\Seonyx\NEW_SITE_FEB26"
$log = "$env:USERPROFILE\Desktop\copy-updates-log.txt"

Start-Transcript -Path $log -Force | Out-Null
Write-Host "Log: $log"

try {

    Write-Host "Phase 2 Session 1 - copy to Dibbler" -ForegroundColor Cyan
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
        "ContentAnalysisEngine\WorkOrderConfiguration.cs",
        "ContentAnalysisEngine\WorkOrderGenerator.cs",
        "App_Data\BookML\bookml-workorder.xsd"
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
        "ContentAnalysisEngine\AnalysisReport.cs",
        "ContentAnalysisEngine\ContentAnalysisEngine.csproj",
        "ContentAnalysisEngine\packages.config",
        "ContentAnalysisHarness\Program.cs",
        "ContentAnalysisHarness\ContentAnalysisHarness.csproj",
        "ContentAnalysisHarness\packages.config"
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
    Write-Host "1. Build (Ctrl+Shift+B) in VS 2022." -ForegroundColor White
    Write-Host "   - Newtonsoft.Json has been removed from both ContentAnalysisEngine" -ForegroundColor White
    Write-Host "     and ContentAnalysisHarness. Build should complete with zero errors." -ForegroundColor White
    Write-Host ""
    Write-Host "2. Test analysis XML output (Phase 1 amendment):" -ForegroundColor White
    Write-Host "   Run ContentAnalysisHarness as startup project with args:" -ForegroundColor White
    Write-Host "   --chapter <path-to-chapter.xml>" -ForegroundColor White
    Write-Host "   Expected: well-formed XML to stdout (namespace https://bookml.org/ns/analysis/1.0)." -ForegroundColor White
    Write-Host "   NOT JSON. Round-trip: paste XML into a parser and confirm it reads back cleanly." -ForegroundColor White
    Write-Host ""
    Write-Host "3. Test work order generation:" -ForegroundColor White
    Write-Host "   --chapter <path-to-chapter.xml> --workorder ch01-workorders.xml --draft 2" -ForegroundColor White
    Write-Host "   Expected: ch01-workorders.xml written, namespace https://bookml.org/ns/workorder/1.0." -ForegroundColor White
    Write-Host "   Entries sorted by severity desc. source-analysis attribute ends in .xml." -ForegroundColor White
    Write-Host ""
    Write-Host "4. Test --quiet flag:" -ForegroundColor White
    Write-Host "   --chapter <path> --workorder out.xml --quiet" -ForegroundColor White
    Write-Host "   Expected: no stdout output, only the work order file written." -ForegroundColor White
    Write-Host ""
    Write-Host "5. Validate work order XML in VS XML editor:" -ForegroundColor White
    Write-Host "   Right-click ch01-workorders.xml -> Validate" -ForegroundColor White
    Write-Host "   (Schema must be associated: bookml-workorder.xsd)" -ForegroundColor White
    Write-Host ""
    Write-Host "Copy complete!" -ForegroundColor Green

} catch {
    Write-Host ""
    Write-Host "FAILED: $_" -ForegroundColor Red
} finally {
    Stop-Transcript | Out-Null
}
