# ============================================
# Draft Management - Copy Script for Dibbler
# Run in PowerShell from any directory
# ============================================

$src = "\\192.168.69.75\seonyx-holding"
$dst = "C:\Users\Steve\Dropbox\VITALIS\Seonyx\NEW_SITE_FEB26"
$log = "$env:USERPROFILE\Desktop\copy-updates-log.txt"

Start-Transcript -Path $log -Force | Out-Null
Write-Host "Log: $log"

try {

    Write-Host "Draft Management: list, compare, delete" -ForegroundColor Cyan
    Write-Host "From: $src" -ForegroundColor Gray
    Write-Host "To:   $dst" -ForegroundColor Gray
    Write-Host ""

    if (-not (Test-Path $src)) {
        throw "ERROR: Cannot reach share at $src"
    }
    if (-not (Test-Path $dst)) {
        throw "ERROR: Destination not found at $dst"
    }

    # Create new directories if needed
    Write-Host "Creating directories..." -ForegroundColor Yellow
    $dirs = @(
        "Views\Draft"
    )
    foreach ($d in $dirs) {
        $fullPath = Join-Path $dst $d
        if (-not (Test-Path $fullPath)) {
            New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
            Write-Host "  Created: $d" -ForegroundColor Green
        }
    }

    # New files to copy
    Write-Host ""
    Write-Host "Copying new files..." -ForegroundColor Yellow
    $newFiles = @(
        "Views\Draft\Index.cshtml"
    )
    foreach ($f in $newFiles) {
        Copy-Item (Join-Path $src $f) (Join-Path $dst $f) -Force
        Write-Host "  NEW: $f" -ForegroundColor Green
    }

    # Modified files to update
    Write-Host ""
    Write-Host "Updating modified files..." -ForegroundColor Yellow
    $modifiedFiles = @(
        "Models\ViewModels\BookEditor\DraftDiffViewModel.cs",
        "Controllers\DraftController.cs",
        "Views\BookProject\Index.cshtml",
        "Content\css\book-editor.css"
    )
    foreach ($f in $modifiedFiles) {
        Copy-Item (Join-Path $src $f) (Join-Path $dst $f) -Force
        Write-Host "  UPD: $f" -ForegroundColor Cyan
    }

    Write-Host ""
    Write-Host "============================================" -ForegroundColor Yellow
    Write-Host "IMPORTANT - Manual steps needed:" -ForegroundColor Yellow
    Write-Host "============================================" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "1. No SQL changes - no database scripts to run." -ForegroundColor White
    Write-Host ""
    Write-Host "2. In VS 2022, Build (Ctrl+Shift+B) then F5 to run." -ForegroundColor White
    Write-Host ""
    Write-Host "3. On the Projects page, projects with any draft now show a 'Manage Drafts' button." -ForegroundColor White
    Write-Host "   The Manage Drafts page shows:" -ForegroundColor Gray
    Write-Host "   - All drafts with status, author, para count and import date" -ForegroundColor Gray
    Write-Host "   - Delete button per draft (disabled when only one draft remains)" -ForegroundColor Gray
    Write-Host "   - Compare bar at the top when two or more drafts exist" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Copy complete!" -ForegroundColor Green

} catch {
    Write-Host ""
    Write-Host "FAILED: $_" -ForegroundColor Red
} finally {
    Stop-Transcript | Out-Null
}
