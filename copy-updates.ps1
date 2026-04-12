# ============================================
# feature/chapter-xml-export: Individual chapter export -> BookML XML
# Run in PowerShell from any directory
# ============================================

$src = "\\192.168.69.75\seonyx-holding"
$dst = "C:\Users\Steve\Dropbox\VITALIS\Seonyx\NEW_SITE_FEB26"
$log = "$env:USERPROFILE\Desktop\copy-updates-log.txt"

Start-Transcript -Path $log -Force | Out-Null
Write-Host "Log: $log"

try {

    Write-Host "feature/chapter-xml-export - copy to Dibbler" -ForegroundColor Cyan
    Write-Host "From: $src" -ForegroundColor Gray
    Write-Host "To:   $dst" -ForegroundColor Gray
    Write-Host ""

    if (-not (Test-Path $src)) {
        throw "ERROR: Cannot reach share at $src"
    }
    if (-not (Test-Path $dst)) {
        throw "ERROR: Destination not found at $dst"
    }

    # Modified files
    Write-Host "Updating modified files..." -ForegroundColor Yellow
    $modifiedFiles = @(
        "Services\BookmlExporter.cs",
        "Controllers\ExportController.cs",
        "Views\Export\Index.cshtml"
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
    Write-Host "1. Rebuild the solution:" -ForegroundColor White
    Write-Host "   msbuild Seonyx.sln /t:Rebuild /p:Configuration=Debug /v:minimal" -ForegroundColor Gray
    Write-Host ""
    Write-Host "2. Run with F5, navigate to a Book Project -> Export." -ForegroundColor White
    Write-Host "   - Complete Export button should be gone." -ForegroundColor Gray
    Write-Host "   - Individual chapters should show 'Export Chapter XML' button." -ForegroundColor Gray
    Write-Host "   - Download a chapter ZIP and verify it contains ch01/ch01-chapter.xml." -ForegroundColor Gray
    Write-Host ""
    Write-Host "3. Test the CLI against the exported chapter XML:" -ForegroundColor White
    Write-Host "   dotnet run --project ContentAnalyserCli -- analyse --chapter ch01\ch01-chapter.xml" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Copy complete!" -ForegroundColor Green

} catch {
    Write-Host ""
    Write-Host "FAILED: $_" -ForegroundColor Red
} finally {
    Stop-Transcript | Out-Null
}
