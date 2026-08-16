# ============================================
# Fix: Audiobook export duplicate paragraphs (chapters 3/4)
# AudiobookPackageBuilder now sources paragraph text/order from the
# Paragraphs working copy instead of ParagraphVersions history,
# matching EpubExporter/BookmlExporter/ChapterReviewOdtExporter.
# Run in PowerShell from any directory
# ============================================

$src = "\\192.168.69.75\seonyx-holding"
$dst = "C:\Nextcloud\VITALIS\Seonyx\NEW_SITE_FEB26"
$log = "$env:USERPROFILE\Desktop\copy-updates-log.txt"

Start-Transcript -Path $log -Force | Out-Null
Write-Host "Log: $log"

try {

    Write-Host "Audiobook export duplicate-paragraph fix - copy to Dibbler" -ForegroundColor Cyan
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
        "Services\AudiobookPackageBuilder.cs"
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
    Write-Host "   No DB changes needed for this fix." -ForegroundColor Gray
    Write-Host ""
    Write-Host "2. Re-export the audiobook package for the affected book (same draft as before)." -ForegroundColor White
    Write-Host "   Check chapters 3 and 4 for duplicate consecutive paragraphs -- should be gone." -ForegroundColor Gray
    Write-Host ""
    Write-Host "3. Re-run the EPUB export for the same book as a regression check." -ForegroundColor White
    Write-Host "   (EpubExporter.cs is untouched, so this should be unaffected either way.)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "4. Optional: confirm root cause by running this query in SSMS against the book's DB" -ForegroundColor White
    Write-Host "   (shows orphaned ParagraphVersions rows for ch3/ch4 not present in Paragraphs):" -ForegroundColor Gray
    Write-Host ""
    Write-Host "   SELECT pv.ChapterID, pv.Pid, pv.DraftNumber, pv.Content" -ForegroundColor Gray
    Write-Host "   FROM ParagraphVersions pv" -ForegroundColor Gray
    Write-Host "   JOIN Chapters c ON c.ChapterID = pv.ChapterID" -ForegroundColor Gray
    Write-Host "   WHERE c.ChapterNumber IN (3, 4)" -ForegroundColor Gray
    Write-Host "     AND NOT EXISTS (" -ForegroundColor Gray
    Write-Host "         SELECT 1 FROM Paragraphs p" -ForegroundColor Gray
    Write-Host "         WHERE p.ChapterID = pv.ChapterID AND p.UniqueID = pv.Pid)" -ForegroundColor Gray
    Write-Host "   ORDER BY pv.ChapterID, pv.Pid;" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Copy complete!" -ForegroundColor Green

} catch {
    Write-Host ""
    Write-Host "FAILED: $_" -ForegroundColor Red
} finally {
    Stop-Transcript | Out-Null
}
