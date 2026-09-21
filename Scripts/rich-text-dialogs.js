// ============================================
// Rich Text Editor — overlay type / emphasis type dialogs (Phase P3 Half B)
//
// Two native <dialog> forms: free-text overlay type (with autocomplete
// from previously-used values in the project) and a fixed four-option
// emphasis type select. Both receive a captured Range from
// rich-text-context-menu.js (never re-read window.getSelection() after
// the menu item was clicked -- dialog.showModal() steals focus the same
// way a toolbar button click does).
//
// Exposes window._rteDialogs = { openOverlayDialog(range), openEmphTypeDialog(range) }
// ============================================

(function () {
    'use strict';

    var root = document.getElementById('paragraphEditable');
    var RTE = window._richTextEditor;
    if (!root || !RTE) return; // Only run on the editor page, after Half A has initialised

    var overlayDialog = document.getElementById('rteOverlayDialog');
    var overlayInput = document.getElementById('rteOverlayTypeInput');
    var overlayList = document.getElementById('rteOverlayTypeList');
    var overlayCancel = document.getElementById('rteOverlayCancel');

    var emphTypeDialog = document.getElementById('rteEmphTypeDialog');
    var emphTypeSelect = document.getElementById('rteEmphTypeSelect');
    var emphTypeCancel = document.getElementById('rteEmphTypeCancel');

    if (!overlayDialog || !emphTypeDialog) return;

    // Config (projectId, GetOverlayTypes URL) is read from a JSON
    // hydration payload rather than the view's editorConfig object,
    // since editorConfig is declared in a @section Scripts block that
    // (per Razor's fixed section-injection point in the layout) actually
    // loads AFTER this file, not before -- moving the declaration inside
    // Index.cshtml doesn't change that, since sections are collected and
    // injected at the layout's @RenderSection call site regardless of
    // where in the child view they're written. Matches the existing
    // #paragraphRunsInit pattern already used for the same reason.
    var config = (function () {
        var payload = document.getElementById('rteConfigInit');
        if (!payload) return { projectId: null, getOverlayTypesUrl: null };
        try {
            return JSON.parse(payload.textContent || payload.innerText || '{}');
        } catch (e) {
            return { projectId: null, getOverlayTypesUrl: null };
        }
    })();

    // Cleared unconditionally on close (OK or Cancel/Escape), symmetric
    // with rich-text-context-menu.js's capturedRange cleanup, so a
    // cancelled-then-later-reopened interaction can't reuse a stale range.
    var pendingRange = null;

    // ------------------------------------------------------------
    // Overlay dialog
    // ------------------------------------------------------------

    function populateOverlayTypeList() {
        if (!overlayList) return;
        overlayList.innerHTML = '';
        if (!config.getOverlayTypesUrl || !config.projectId) return;

        var url = config.getOverlayTypesUrl + '?projectId=' + encodeURIComponent(config.projectId);
        fetch(url)
            .then(function (r) { return r.json(); })
            .then(function (types) {
                if (!types) return;
                overlayList.innerHTML = '';
                types.forEach(function (t) {
                    var opt = document.createElement('option');
                    opt.value = t;
                    overlayList.appendChild(opt);
                });
            })
            .catch(function () { /* autocomplete is a nice-to-have; fail silently */ });
    }

    function openOverlayDialog(range) {
        pendingRange = range;
        if (overlayInput) overlayInput.value = '';
        populateOverlayTypeList();
        overlayDialog.showModal();
        if (overlayInput) overlayInput.focus();
    }

    overlayDialog.addEventListener('close', function () {
        if (overlayDialog.returnValue === 'ok' && pendingRange && overlayInput) {
            var value = overlayInput.value.trim();
            if (value) {
                RTE.applyStyleToRange(pendingRange, 'overlayType', value);
            }
        }
        pendingRange = null;
        overlayDialog.returnValue = '';
    });

    if (overlayCancel) {
        overlayCancel.addEventListener('click', function () {
            overlayDialog.close(); // no returnValue set -> treated as cancel
        });
    }

    // ------------------------------------------------------------
    // Emphasis type dialog
    // ------------------------------------------------------------

    function openEmphTypeDialog(range) {
        pendingRange = range;

        // Pre-select the existing value if the whole selection already
        // shares one non-null emphType (covers "change thought -> foreign"
        // without an extra click); otherwise leave the default selected.
        var sel = RTE.getSelectionRunRange(range);
        var existing = null;
        if (sel) {
            var runs = RTE.getRunsRaw();
            var allSame = true;
            for (var i = sel.startRun; i <= sel.endRun; i++) {
                var r = runs[i];
                if (!r || !r.emphType) { allSame = false; break; }
                if (existing === null) existing = r.emphType;
                else if (r.emphType !== existing) { allSame = false; break; }
            }
            if (!allSame) existing = null;
        }
        if (emphTypeSelect) {
            emphTypeSelect.value = existing || emphTypeSelect.options[0].value;
        }

        emphTypeDialog.showModal();
        if (emphTypeSelect) emphTypeSelect.focus();
    }

    emphTypeDialog.addEventListener('close', function () {
        if (emphTypeDialog.returnValue === 'ok' && pendingRange && emphTypeSelect) {
            RTE.applyEmphType(pendingRange, emphTypeSelect.value);
        }
        pendingRange = null;
        emphTypeDialog.returnValue = '';
    });

    if (emphTypeCancel) {
        emphTypeCancel.addEventListener('click', function () {
            emphTypeDialog.close(); // no returnValue set -> treated as cancel
        });
    }

    // ------------------------------------------------------------
    // Public surface for rich-text-context-menu.js
    // ------------------------------------------------------------

    window._rteDialogs = {
        openOverlayDialog: openOverlayDialog,
        openEmphTypeDialog: openEmphTypeDialog
    };
})();
