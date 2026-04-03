# ============================================
# Audiobook Package export feature
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
        "Views\Export\AudiobookConfig.cshtml"
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
        "Views\Export\Index.cshtml",
        "Seonyx.csproj",
        "App_Data\BookML\bookml-common.xsd"
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
    Write-Host "1. Build (Ctrl+Shift+B) in VS 2022 - no DB changes, no new packages." -ForegroundColor White
    Write-Host "   NOTE: bookml-common.xsd updated - BookML imports with 3-part PIDs will now validate." -ForegroundColor White
    Write-Host "2. Open any book project and go to Export." -ForegroundColor White
    Write-Host "3. You should see a new 'Audiobook Package' option card." -ForegroundColor White
    Write-Host "4. Select a voice and click 'Download Package' - a ZIP should download." -ForegroundColor White
    Write-Host "5. Inspect the ZIP: it should contain config.json, README.txt," -ForegroundColor White
    Write-Host "   generate_audiobook.py, and a chapters/ folder with .txt files." -ForegroundColor White
    Write-Host "6. To test audio generation: extract the ZIP, open a terminal there," -ForegroundColor White
    Write-Host "   run:  python generate_audiobook.py" -ForegroundColor White
    Write-Host "   (Piper TTS and the voice model will download automatically on first run.)" -ForegroundColor White
    Write-Host ""
    Write-Host "Copy complete!" -ForegroundColor Green

} catch {
    Write-Host ""
    Write-Host "FAILED: $_" -ForegroundColor Red
} finally {
    Stop-Transcript | Out-Null
}
