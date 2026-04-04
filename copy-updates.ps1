# ============================================
# Phase 2 Session 2: Work Order Review Panel (UI)
# + Phase 2 Session 1 files (include if not already copied)
# Run in PowerShell from any directory
# ============================================

$src = "\\192.168.69.75\seonyx-holding"
$dst = "C:\Users\Steve\Dropbox\VITALIS\Seonyx\NEW_SITE_FEB26"
$log = "$env:USERPROFILE\Desktop\copy-updates-log.txt"

Start-Transcript -Path $log -Force | Out-Null
Write-Host "Log: $log"

try {

    Write-Host "Phase 2 Session 2 - copy to Dibbler" -ForegroundColor Cyan
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
        "Models\ViewModels\BookEditor\DraftAnalysisViewModel.cs",
        "Views\Draft\Analysis.cshtml",
        "Content\css\work-order.css",
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
        "Controllers\DraftController.cs",
        "Seonyx.csproj",
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
    Write-Host "1. Build (Ctrl+Shift+B) in VS 2022 - zero errors expected." -ForegroundColor White
    Write-Host ""
    Write-Host "2. Navigate to:" -ForegroundColor White
    Write-Host "   /admin/bookeditor/draft/Analysis?projectId=N" -ForegroundColor Gray
    Write-Host "   (replace N with a project that has a BookML import)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "3. Select a chapter with a BookML ID and click 'Generate Work Orders'." -ForegroundColor White
    Write-Host "   Default filter shows Outlier Words only (not all 3000+ entries)." -ForegroundColor White
    Write-Host ""
    Write-Host "4. Test interactions:" -ForegroundColor White
    Write-Host "   - Click a row to open the detail panel" -ForegroundColor White
    Write-Host "   - Toggle a row status (Pending/Skipped)" -ForegroundColor White
    Write-Host "   - Use 'Skip All Echoes' to reduce batch size" -ForegroundColor White
    Write-Host "   - Add a manual entry" -ForegroundColor White
    Write-Host "   - Export -> verify XML file downloads with processing-notes element" -ForegroundColor White
    Write-Host ""
    Write-Host "Copy complete!" -ForegroundColor Green

} catch {
    Write-Host ""
    Write-Host "FAILED: $_" -ForegroundColor Red
} finally {
    Stop-Transcript | Out-Null
}
