// ============================================
// Rich Text Editor — right-click context menu (Phase P3 Half B)
//
// Captures the current selection on 'contextmenu', shows/hides menu
// items based on the run(s) under that selection, and dispatches to
// either a dialog (overlay type, emphasis type) or a direct mutation
// (small caps) on item click.
//
// A collapsed selection (nothing highlighted) is NOT suppressed at the
// browser level -- the native context menu (spell-check, Copy/Paste,
// Inspect, etc.) is left alone in that case. Only a non-collapsed
// selection triggers preventDefault() and this custom menu.
// ============================================

(function () {
    'use strict';

    var root = document.getElementById('paragraphEditable');
    var RTE = window._richTextEditor;
    if (!root || !RTE) return; // Only run on the editor page, after Half A has initialised

    var menu = document.getElementById('rteContextMenu');
    if (!menu) return;

    var itemMarkOverlay = document.getElementById('rteMenuMarkOverlay');
    var itemRemoveOverlay = document.getElementById('rteMenuRemoveOverlay');
    var itemSetEmphType = document.getElementById('rteMenuSetEmphType');
    var itemRemoveEmphType = document.getElementById('rteMenuRemoveEmphType');
    var itemToggleSmallCaps = document.getElementById('rteMenuToggleSmallCaps');

    // Captured once per contextmenu event, at the moment the menu opens.
    // The selection cannot change while this (native-suppressed) menu is
    // open -- the user can only interact with the menu itself until it
    // closes -- so there's no need to re-capture per menu item.
    var capturedRange = null;
    var menuOpen = false;

    function runsInRange(sel) {
        var all = RTE.getRunsRaw();
        var list = [];
        // A selection boundary sitting exactly at a run seam is ambiguous
        // and different browsers resolve it to different sides for the
        // SAME on-screen selection. Chromium reports a whole-word
        // selection's end as offset 0 of the FOLLOWING run when more text
        // follows; Firefox instead reports the START as the full length of
        // the PRECEDING run when more text precedes it. Left unhandled,
        // either shape wrongly pulls a run the user never selected into
        // the "are all runs emphasized/overlaid/small-caps" checks below,
        // hiding menu items that should show (or showing ones that
        // shouldn't) for a clean whole-word selection.
        var firstRun = sel.startRun;
        if (firstRun < sel.endRun && sel.startOffset === (all[firstRun] && all[firstRun].text || '').length) {
            firstRun += 1;
        }
        var lastRun = sel.endRun;
        if (lastRun > firstRun && sel.endOffset === 0) {
            lastRun -= 1;
        }
        for (var i = firstRun; i <= lastRun; i++) {
            if (all[i]) list.push(all[i]);
        }
        return list;
    }

    function allRunsHave(list, predicate) {
        for (var i = 0; i < list.length; i++) {
            if (!predicate(list[i])) return false;
        }
        return list.length > 0;
    }

    function anyRunHas(list, predicate) {
        for (var i = 0; i < list.length; i++) {
            if (predicate(list[i])) return true;
        }
        return false;
    }

    function showItem(el, show) {
        if (!el) return;
        el.style.display = show ? '' : 'none';
    }

    function positionMenu(x, y) {
        // Render un-clamped first so offsetWidth/Height reflect actual
        // layout, then clamp to the viewport in both axes.
        menu.style.left = x + 'px';
        menu.style.top = y + 'px';
        menu.hidden = false;

        var menuWidth = menu.offsetWidth;
        var menuHeight = menu.offsetHeight;
        var left = x;
        var top = y;

        if (left + menuWidth > window.innerWidth) {
            left = window.innerWidth - menuWidth;
            if (left < 0) left = 0;
        }
        if (top + menuHeight > window.innerHeight) {
            top = y - menuHeight;
            if (top < 0) top = 0;
        }

        menu.style.left = left + 'px';
        menu.style.top = top + 'px';
    }

    function openMenu(x, y, sel) {
        var list = runsInRange(sel);

        var hasOverlay = anyRunHas(list, function (r) { return !!r.overlayType; });
        var allOverlay = allRunsHave(list, function (r) { return !!r.overlayType; });
        var allEmph = allRunsHave(list, function (r) { return r.isEmph === true; });
        var hasEmphType = anyRunHas(list, function (r) { return !!r.emphType; });
        var allSmallCaps = allRunsHave(list, function (r) { return r.isSmallCaps === true; });

        showItem(itemMarkOverlay, !allOverlay);
        showItem(itemRemoveOverlay, hasOverlay);
        showItem(itemSetEmphType, allEmph);
        showItem(itemRemoveEmphType, hasEmphType);
        if (itemToggleSmallCaps) {
            itemToggleSmallCaps.textContent = allSmallCaps ? 'Remove small caps' : 'Apply small caps';
        }

        positionMenu(x, y);
        menuOpen = true;
    }

    function closeMenu() {
        menu.hidden = true;
        menuOpen = false;
        capturedRange = null;
    }

    root.addEventListener('contextmenu', function (e) {
        var sel = RTE.getSelectionRunRange();
        if (!sel || sel.collapsed) {
            // Nothing selected: let the native browser context menu
            // (spell-check suggestions, Copy/Paste, Inspect, etc.)
            // appear normally -- none of this menu's actions make sense
            // without a selection.
            return;
        }

        e.preventDefault();
        var liveSel = window.getSelection();
        capturedRange = liveSel.getRangeAt(0).cloneRange();
        openMenu(e.clientX, e.clientY, sel);
    });

    // Click-away closes the menu without side effects. Must not call
    // preventDefault() -- the click's own default action (e.g. placing
    // the cursor elsewhere) should proceed normally.
    document.addEventListener('mousedown', function (e) {
        if (menuOpen && !menu.contains(e.target)) {
            closeMenu();
        }
    });

    document.addEventListener('keydown', function (e) {
        if (menuOpen && e.key === 'Escape') {
            closeMenu();
        }
    });

    // Every menu item: prevent the mousedown-triggered focus shift (same
    // hazard, same fix, as Half A's toolbar buttons), then act on click
    // using capturedRange -- never re-reading window.getSelection().
    function wireItem(el, action) {
        if (!el) return;
        el.addEventListener('mousedown', function (e) { e.preventDefault(); });
        el.addEventListener('click', function () {
            var rangeToUse = capturedRange;
            closeMenu();
            if (rangeToUse) action(rangeToUse);
        });
    }

    wireItem(itemMarkOverlay, function (range) {
        if (window._rteDialogs) window._rteDialogs.openOverlayDialog(range);
    });

    wireItem(itemRemoveOverlay, function (range) {
        RTE.applyStyleToRange(range, 'overlayType', null);
    });

    wireItem(itemSetEmphType, function (range) {
        if (window._rteDialogs) window._rteDialogs.openEmphTypeDialog(range);
    });

    wireItem(itemRemoveEmphType, function (range) {
        RTE.applyStyleToRange(range, 'emphType', null);
    });

    wireItem(itemToggleSmallCaps, function (range) {
        RTE.toggleStyleForRange('isSmallCaps', range);
    });
})();
