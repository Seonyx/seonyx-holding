# ============================================
# BookML Inline Presentation - Phase P3 Half B
# Adds the right-click context menu, overlay marking (with autocomplete
# from previously-used types in the project), emphasis sub-type
# assignment (fixed thought/foreign/title/ship-name dropdown), and small
# caps toggle to the rich text paragraph editor. Builds entirely on Half
# A's existing engine (Scripts\rich-text-editor.js) -- no changes to its
# typing/rendering pipeline, only new exported primitives
# (applyEmphType, toggleStyleForRange) and a bug fix to applyStyleToRange
# (it was never actually called by Half A, so a real defect in it went
# undetected until Half B's real-browser tests exercised it directly).
#
# UPDATE: fixes the overlay-not-saving bug reported after first Half B
# deploy. Root cause: the context menu <ul> and both <dialog> elements
# were originally rendered inside <form id="editorForm">, and each
# dialog's own <form method="dialog"> was therefore nested inside that
# outer form -- invalid HTML. Browsers drop a nested form from
# document.forms during parsing, so method="dialog" (submit-closes-dialog)
# never actually applied to it; the inner OK button was instead treated
# as belonging to the outer form and triggered ITS submit/navigation
# (matching the reported "Saved message fires immediately, but nothing
# changes" symptom). Fixed by moving the menu/dialog markup out of
# #editorForm entirely, following this file's own existing precedent
# for the bookmark/insert/delete forms ("outside main form to avoid
# nested forms"). Confirmed via a faithful Puppeteer reproduction: the
# same test suite that passed against a non-nested test harness crashed
# with "Execution context was destroyed, most likely because of a
# navigation" against a nested-form harness, and passes cleanly again
# once the dialogs are moved outside the form -- exactly the fix applied
# here.
#
# UPDATE 2: fixes "Set emphasis type..." not appearing when right-clicking
# a whole previously-italicized word. Root cause: when a selection's end
# boundary lands at offset 0 of the run AFTER the emphasized word (which is
# exactly what double-click word-selection commonly produces when the word
# is followed by more text), both the menu's gating logic and the
# mutation's own run-boundary normalisation wrongly treated that next
# (unselected) run as part of the selection. This hid the menu item when
# it should have shown, AND -- more seriously -- would have caused
# "Set emphasis type" to spill emphType/isEmph onto the adjacent plain
# text if it were ever triggered some other way (confirmed via a targeted
# test). Fixed in both rich-text-context-menu.js's runsInRange() and
# rich-text-editor.js's normaliseRangeToRuns(): an end boundary at offset 0
# now correctly excludes that run and uses the previous run's end instead.
#
# UPDATE 3: fixes Ctrl+B/Ctrl+I corrupting the model when toggling OFF a
# style on a re-selected word (e.g. bold "hundred" via the toolbar, then
# select "hundred" again and press Ctrl+B: instead of un-bolding just that
# word, the whole sentence went bold, or the run structure silently
# desynced from the DOM). Root cause: Chrome's contenteditable has its own
# NATIVE Ctrl+B/Ctrl+I handling (a beforeinput event with inputType
# "formatBold"/"formatItalic") that fires independently of and races with
# our own keydown handler's toggleStyle() call for the same keypress --
# the native command mutates the DOM directly (confirmed via a real-browser
# trace: it can delete/rewrap existing rte-* spans), corrupting the
# model/DOM sync our own code relies on. keydown's preventDefault() stops
# the key's default action but NOT this separate native formatting command.
# Fixed by explicitly preventDefault()-ing formatBold/formatItalic
# beforeinput events in rich-text-editor.js, so only our own handler ever
# applies these two styles.
#
# UPDATE 4: fixes the visual selection highlight jumping to cover the
# WHOLE SENTENCE when toggling a style OFF on a re-selected word (e.g.
# bold "hundred", re-select just "hundred", press Ctrl+B again -- the
# underlying data was already correct after UPDATE 3, but the on-screen
# highlight wrongly expanded to the whole sentence). Root cause: after
# un-bolding, coalesce() correctly merges the now-identically-styled
# neighbouring runs back into one -- but the selection-restore step was
# still keyed on the OLD run INDICES from before that merge, which after
# merging pointed at the single new run spanning the whole sentence, so
# the restored highlight covered all of it even though only one word's
# style actually changed. Fixed by capturing the selection as absolute
# CHARACTER offsets (immune to how much merging happens) before the
# mutation, and restoring the highlight from those offsets afterwards
# (new restoreSelectionByCharOffset() in rich-text-editor.js, used by
# toggleStyleOverRange -- the shared core of both the Bold/Italic
# toolbar/keyboard path and the Half B menu's small-caps toggle).
#
# UPDATE 5: fixes Firefox specifically -- UPDATES 3/4 fully fixed Chrome
# and Brave, but Ctrl+B toggle-off (and, by the same mechanism, the
# right-click menu's "Set emphasis type..."/overlay/small-caps gating) was
# still broken in Firefox alone. Root cause: a selection boundary that
# sits exactly at the seam between two runs is genuinely ambiguous, and
# Chrome and Firefox resolve it to OPPOSITE sides. For a whole-word
# selection immediately preceded by more text, Chrome reports the START as
# offset 0 of the word's own run; Firefox instead reports it as the FULL
# LENGTH of the PRECEDING run (confirmed via a real Firefox trace). The
# existing boundary fix (UPDATE 2) only handled Chrome's shape (an
# unwanted trailing run at the END of a selection); Firefox's shape is the
# mirror case at the START, which was still unhandled -- so on Firefox,
# toggling a style "off" was actually toggling it on the wrong, wider
# range (the preceding run plus the target word), which is exactly the
# "whole sentence" symptom. Fixed with the symmetric check in both
# rich-text-editor.js (normaliseRangeToRuns, the mutation path) and
# rich-text-context-menu.js (runsInRange, the menu-gating path). Verified
# against a real Firefox instance (not just Chrome/Brave) via Puppeteer's
# WebDriver BiDi support.
#
# UPDATE 6: adds a third toolbar button, "Clear", next to Bold/Italic. It
# strips ALL styling (bold, italic, emphasis type, small caps, overlay)
# from the WHOLE paragraph in one click, regardless of selection -- text
# and line breaks are untouched. Added after testing revealed it's easy to
# end up with overlapping/tangled styles that are hard to spot visually,
# and there was no quick way back to clean plain text short of manually
# un-toggling each style on each affected phrase. New
# RTE.clearFormatting() in rich-text-editor.js; new #rteClearFormatting
# button in Index.cshtml; new .rte-toolbar-btn-clear rule in
# book-editor.css.
# Run in PowerShell from any directory
# ============================================

$src = "\\192.168.69.75\seonyx-holding"
$dst = "C:\Nextcloud\Work\VITALIS\Seonyx\NEW_SITE_FEB26"
$log = "$env:USERPROFILE\Desktop\copy-updates-log.txt"

Start-Transcript -Path $log -Force | Out-Null
Write-Host "Log: $log"

try {

    Write-Host "BookML Inline Presentation P3 Half B - copy to Dibbler" -ForegroundColor Cyan
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
        "Scripts\rich-text-context-menu.js",
        "Scripts\rich-text-dialogs.js"
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
        "Scripts\rich-text-editor.js",
        "Controllers\EditorController.cs",
        "Views\Editor\Index.cshtml",
        "Views\Shared\_BookEditorLayout.cshtml",
        "Content\css\book-editor.css",
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
    Write-Host "Post-copy steps:" -ForegroundColor Yellow
    Write-Host "============================================" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "1. No DB changes needed (GetOverlayTypes is a read-only query against" -ForegroundColor White
    Write-Host "   the existing ParagraphRuns table)." -ForegroundColor Gray
    Write-Host ""
    Write-Host "1b. THIS UPDATE FIXES the overlay-not-saving bug: the context menu and" -ForegroundColor Magenta
    Write-Host "    both dialogs were nested inside <form id=editorForm>, which is invalid" -ForegroundColor Gray
    Write-Host "    HTML and silently broke the dialog's OK button (it fell through to the" -ForegroundColor Gray
    Write-Host "    outer form's own submit instead of closing the dialog). Now fixed by" -ForegroundColor Gray
    Write-Host "    moving that markup outside the main form. Please re-test overlay" -ForegroundColor Gray
    Write-Host "    marking specifically (step 3 below) -- this is the step that was broken." -ForegroundColor Gray
    Write-Host ""
    Write-Host "2. Build and run the web app (Seonyx.sln, F5)." -ForegroundColor White
    Write-Host "   Do ONE hard refresh (Ctrl+Shift+R) after this deploy -- from then on," -ForegroundColor Gray
    Write-Host "   cache-busting should keep ordinary reloads current automatically." -ForegroundColor Gray
    Write-Host ""
    Write-Host "3. Overlay round-trip: select a phrase, right-click, 'Mark as overlay...'," -ForegroundColor White
    Write-Host "   type a type value (e.g. 'hud'), OK -- confirm it renders as a teal pill" -ForegroundColor Gray
    Write-Host "   with the type shown on hover. Save, reload -- confirm it persists." -ForegroundColor Gray
    Write-Host ""
    Write-Host "4. Overlay autocomplete: after creating one overlay type in a project," -ForegroundColor White
    Write-Host "   open the dialog again on a different paragraph in the SAME project --" -ForegroundColor Gray
    Write-Host "   confirm that type appears as a suggestion." -ForegroundColor Gray
    Write-Host ""
    Write-Host "5. Remove overlay: right-click inside an existing overlay, 'Remove overlay'" -ForegroundColor White
    Write-Host "   -- confirm the pill disappears and text becomes plain." -ForegroundColor Gray
    Write-Host ""
    Write-Host "6. Emphasis type: select an ALREADY-ITALIC phrase, right-click -- confirm" -ForegroundColor White
    Write-Host "   'Set emphasis type...' appears; select a MIXED (partly italic) phrase --" -ForegroundColor Gray
    Write-Host "   confirm it does NOT appear. Pick a value, OK -- confirm it applies." -ForegroundColor Gray
    Write-Host "   Re-open the menu on that same phrase -- confirm the dialog pre-selects" -ForegroundColor Gray
    Write-Host "   the value you just set. 'Remove emphasis type' clears the sub-type" -ForegroundColor Gray
    Write-Host "   without un-italicizing the text." -ForegroundColor Gray
    Write-Host ""
    Write-Host "7. Small caps: select a phrase, right-click, 'Apply small caps' -- confirm" -ForegroundColor White
    Write-Host "   visual rendering; re-open the menu -- confirm it now reads 'Remove" -ForegroundColor Gray
    Write-Host "   small caps' and toggles back correctly." -ForegroundColor Gray
    Write-Host ""
    Write-Host "8. Menu dismissal: open the menu, click away -- confirm it closes with no" -ForegroundColor White
    Write-Host "   side effects. Open it again, press Escape -- confirm it closes." -ForegroundColor Gray
    Write-Host ""
    Write-Host "9. IMPORTANT -- collapsed-selection right-click: click once with NOTHING" -ForegroundColor White
    Write-Host "   selected, then right-click -- confirm the NATIVE browser context menu" -ForegroundColor Gray
    Write-Host "   appears (spell-check, Copy/Paste, Inspect), NOT the custom menu and NOT" -ForegroundColor Gray
    Write-Host "   nothing. This was a deliberate fix during plan review -- worth extra" -ForegroundColor Gray
    Write-Host "   attention since getting this wrong would silently break spell-check." -ForegroundColor Gray
    Write-Host ""
    Write-Host "10. Regression check: re-confirm Bold/Italic (toolbar and Ctrl+B/Ctrl+I)" -ForegroundColor White
    Write-Host "    still work correctly on a partial selection." -ForegroundColor Gray
    Write-Host ""
    Write-Host "11. Clear formatting: apply a mix of bold/italic/small caps/overlay to" -ForegroundColor White
    Write-Host "    different phrases in one paragraph, click Clear -- confirm the whole" -ForegroundColor Gray
    Write-Host "    paragraph becomes plain text with no styling left, and the wording" -ForegroundColor Gray
    Write-Host "    itself (including any line breaks) is unchanged. Try it with nothing" -ForegroundColor Gray
    Write-Host "    selected too -- it should still clear the whole paragraph." -ForegroundColor Gray
    Write-Host ""
    Write-Host "Copy complete!" -ForegroundColor Green

} catch {
    Write-Host ""
    Write-Host "FAILED: $_" -ForegroundColor Red
} finally {
    Stop-Transcript | Out-Null
}
