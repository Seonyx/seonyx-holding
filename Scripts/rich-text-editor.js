// ============================================
// Rich Text Editor — run-based contenteditable engine
//
// Renders a paragraph as an ordered list of "runs" (styled text
// fragments, mirroring the server's InlineRun/ParagraphRun shape)
// inside a contenteditable div, and keeps that list in sync with
// what the user types/selects/styles.
//
// Public surface (window._richTextEditor):
//   getRuns()          -> current coalesced run list (for save)
//   setRuns(runs)       -> replace the model and re-render
//   isReady()           -> true once initialised
//   focus()             -> focus the editable root
//
// Internal API other rich-text-*.js modules (Half B) may call:
//   RTE.getSelectionRunRange()
//   RTE.splitRunAtOffset(runIndex, offset)
//   RTE.applyStyleToRange(range, propName, value)
//   RTE.coalesceAndRerender()
// ============================================

(function () {
    'use strict';

    var root = document.getElementById('paragraphEditable');
    if (!root) return; // Only run on the editor page

    // ------------------------------------------------------------
    // Model
    // ------------------------------------------------------------

    // Each run: { text, isEmph, emphType, isStrong, isSmallCaps, overlayType, isBreak }
    var runs = [];
    var ready = false;
    var pendingStyleOverrides = null; // { isStrong, isEmph } or null

    function emptyRun() {
        return {
            text: '',
            isEmph: false,
            emphType: null,
            isStrong: false,
            isSmallCaps: false,
            overlayType: null,
            isBreak: false
        };
    }

    function cloneRun(r) {
        return {
            text: r.text,
            isEmph: r.isEmph,
            emphType: r.emphType,
            isStrong: r.isStrong,
            isSmallCaps: r.isSmallCaps,
            overlayType: r.overlayType,
            isBreak: r.isBreak
        };
    }

    function sameStyle(a, b) {
        return a.isEmph === b.isEmph &&
            a.emphType === b.emphType &&
            a.isStrong === b.isStrong &&
            a.isSmallCaps === b.isSmallCaps &&
            a.overlayType === b.overlayType &&
            a.isBreak === b.isBreak &&
            !a.isBreak;
    }

    // Mirrors Services/InlineXmlRunConverter.cs Coalesce() -- merge
    // adjacent runs with identical style (except IsBreak runs, which
    // always stay separate markers).
    function coalesce(list) {
        var result = [];
        for (var i = 0; i < list.length; i++) {
            var r = list[i];
            if (result.length > 0 && sameStyle(result[result.length - 1], r)) {
                result[result.length - 1].text = (result[result.length - 1].text || '') + (r.text || '');
            } else {
                result.push(cloneRun(r));
            }
        }
        return result;
    }

    // ------------------------------------------------------------
    // Rendering: model -> DOM
    // ------------------------------------------------------------

    function buildRunNode(run, index) {
        if (run.isBreak) {
            var br = document.createElement('br');
            br.className = 'rte-break';
            br.setAttribute('data-run-index', String(index));
            return br;
        }

        var span = document.createElement('span');
        span.setAttribute('data-run-index', String(index));
        var classes = [];
        if (run.isStrong) classes.push('rte-strong');
        if (run.isEmph) classes.push('rte-emph');
        if (run.isSmallCaps) classes.push('rte-smallcaps');
        if (run.overlayType) {
            classes.push('rte-overlay');
            span.setAttribute('data-overlay-type', run.overlayType);
        }
        if (run.emphType) {
            span.setAttribute('data-emph-type', run.emphType);
        }
        span.className = classes.join(' ');
        span.textContent = run.text || '';
        return span;
    }

    function renderRuns() {
        root.innerHTML = '';
        if (runs.length === 0) {
            // Keep a single empty run so there's always something to type into.
            runs = [emptyRun()];
        }
        for (var i = 0; i < runs.length; i++) {
            root.appendChild(buildRunNode(runs[i], i));
        }
        reindexSpans();
        updateToolbarState();
    }

    // After any structural change, data-run-index attributes must match
    // the current array positions exactly (splits/merges shift them).
    function reindexSpans() {
        var children = root.childNodes;
        var i = 0;
        for (var c = 0; c < children.length; c++) {
            var node = children[c];
            if (node.nodeType === 1) {
                node.setAttribute('data-run-index', String(i));
                i++;
            }
        }
    }

    // ------------------------------------------------------------
    // Selection <-> model mapping
    // ------------------------------------------------------------

    function findRunSpan(node) {
        while (node && node !== root) {
            if (node.nodeType === 1 && node.hasAttribute && node.hasAttribute('data-run-index')) {
                return node;
            }
            node = node.parentNode;
        }
        return null;
    }

    // Returns { startRun, startOffset, endRun, endOffset } in model
    // coordinates for the current selection, or null if the selection
    // isn't inside the editable root.
    function getSelectionRunRange() {
        var sel = window.getSelection();
        if (!sel || sel.rangeCount === 0) return null;
        var range = sel.getRangeAt(0);
        if (!root.contains(range.startContainer) || !root.contains(range.endContainer)) return null;

        var startSpan = findRunSpan(range.startContainer);
        var endSpan = findRunSpan(range.endContainer);
        if (!startSpan || !endSpan) return null;

        var startRun = parseInt(startSpan.getAttribute('data-run-index'), 10);
        var endRun = parseInt(endSpan.getAttribute('data-run-index'), 10);

        return {
            startRun: startRun,
            startOffset: range.startOffset,
            endRun: endRun,
            endOffset: range.endOffset,
            collapsed: range.collapsed
        };
    }

    // Splits run[runIndex] at the given character offset into two runs
    // with identical style. Returns the index of the new run that
    // begins at `offset` (i.e. the "right half"). No-op (returns
    // runIndex unchanged) if offset is at a run boundary already.
    function splitRunAtOffset(runIndex, offset) {
        var run = runs[runIndex];
        if (!run || run.isBreak) return runIndex;
        var text = run.text || '';
        if (offset <= 0) return runIndex;
        if (offset >= text.length) return runIndex + 1;

        var left = cloneRun(run);
        left.text = text.substring(0, offset);
        var right = cloneRun(run);
        right.text = text.substring(offset);

        runs.splice(runIndex, 1, left, right);
        return runIndex + 1;
    }

    // Normalises a selection range to whole-run boundaries by splitting
    // the start/end runs as needed, and returns the resulting
    // [firstIndex, lastIndex] inclusive range of whole runs covered.
    function normaliseRangeToRuns(sel) {
        // Split end first so start indices aren't invalidated by the
        // insertion caused by splitting.
        var endRunIndex = sel.endRun;
        var endOffset = sel.endOffset;
        if (endOffset > 0 && endOffset < (runs[endRunIndex].text || '').length) {
            splitRunAtOffset(endRunIndex, endOffset);
            // run at endRunIndex is now the left half (still selected);
            // the tail we don't want is at endRunIndex + 1.
        }

        var startRunIndex = sel.startRun;
        var startOffset = sel.startOffset;
        if (startOffset > 0 && startOffset < (runs[startRunIndex].text || '').length) {
            startRunIndex = splitRunAtOffset(startRunIndex, startOffset);
        }

        return { first: startRunIndex, last: endRunIndex };
    }

    function restoreSelectionAcrossRuns(firstIndex, lastIndex) {
        reindexSpans();
        var children = root.childNodes;
        var startNode = null, endNode = null;
        for (var i = 0; i < children.length; i++) {
            var idx = parseInt(children[i].getAttribute && children[i].getAttribute('data-run-index'), 10);
            if (idx === firstIndex) startNode = children[i];
            if (idx === lastIndex) endNode = children[i];
        }
        if (!startNode || !endNode) return;

        var range = document.createRange();
        try {
            range.setStartBefore(startNode);
            range.setEndAfter(endNode);
            var sel = window.getSelection();
            sel.removeAllRanges();
            sel.addRange(range);
        } catch (e) { /* best-effort */ }
    }

    // ------------------------------------------------------------
    // Style toggling (Bold / Italic and, for Half B, other flags)
    // ------------------------------------------------------------

    function allRunsHaveFlag(firstIndex, lastIndex, propName) {
        for (var i = firstIndex; i <= lastIndex; i++) {
            if (!runs[i][propName]) return false;
        }
        return true;
    }

    function applyStyleToRange(sel, propName, value) {
        var norm = normaliseRangeToRuns(sel);
        for (var i = norm.first; i <= norm.last; i++) {
            runs[i][propName] = value;
        }
        runs = coalesce(runs);
        renderRuns();
        markDirty();
    }

    function toggleStyle(propName) {
        var sel = getSelectionRunRange();
        if (!sel) return;

        if (sel.collapsed) {
            // No active selection: toggle the "type here styled" state
            // for the next character(s) typed, rather than editing runs.
            if (!pendingStyleOverrides) pendingStyleOverrides = {};
            var current = pendingStyleOverrides[propName];
            if (current === undefined) {
                var runAtCursor = runs[sel.startRun];
                current = runAtCursor ? !!runAtCursor[propName] : false;
            }
            pendingStyleOverrides[propName] = !current;
            updateToolbarState();
            return;
        }

        var norm = normaliseRangeToRuns(sel);
        var allSet = allRunsHaveFlag(norm.first, norm.last, propName);
        for (var i = norm.first; i <= norm.last; i++) {
            runs[i][propName] = !allSet;
        }
        runs = coalesce(runs);
        renderRuns();
        restoreSelectionAcrossRuns(
            Math.min(norm.first, runs.length - 1),
            Math.min(norm.last, runs.length - 1)
        );
        markDirty();
    }

    function updateToolbarState() {
        var sel = getSelectionRunRange();
        var strongActive = false, emphActive = false;
        if (sel) {
            var idx = sel.startRun;
            if (pendingStyleOverrides && pendingStyleOverrides.isStrong !== undefined) {
                strongActive = pendingStyleOverrides.isStrong;
            } else if (runs[idx]) {
                strongActive = !!runs[idx].isStrong;
            }
            if (pendingStyleOverrides && pendingStyleOverrides.isEmph !== undefined) {
                emphActive = pendingStyleOverrides.isEmph;
            } else if (runs[idx]) {
                emphActive = !!runs[idx].isEmph;
            }
        }
        if (btnBold) btnBold.classList.toggle('active', strongActive);
        if (btnItalic) btnItalic.classList.toggle('active', emphActive);
    }

    // ------------------------------------------------------------
    // Typing (fast path) and structural edits (beforeinput)
    // ------------------------------------------------------------

    function markDirty() {
        root.dispatchEvent(new Event('input', { bubbles: true }));
    }

    // Rebuilds the run list from the live DOM. Safety net for paste
    // and a pre-save consistency check -- not the steady-state path.
    function domToModel() {
        var result = [];
        var children = root.childNodes;
        for (var i = 0; i < children.length; i++) {
            var node = children[i];
            if (node.nodeType === 3) {
                // Stray text node (shouldn't normally happen) -- treat as
                // an unstyled run.
                if (node.textContent) {
                    var r = emptyRun();
                    r.text = node.textContent;
                    result.push(r);
                }
                continue;
            }
            if (node.nodeType !== 1) continue;

            if (node.tagName === 'BR') {
                var br = emptyRun();
                br.isBreak = true;
                br.text = null;
                result.push(br);
                continue;
            }

            var run = emptyRun();
            run.text = node.textContent || '';
            run.isStrong = node.classList.contains('rte-strong');
            run.isEmph = node.classList.contains('rte-emph');
            run.isSmallCaps = node.classList.contains('rte-smallcaps');
            run.overlayType = node.getAttribute('data-overlay-type') || null;
            run.emphType = node.getAttribute('data-emph-type') || null;
            if (run.text.length > 0) result.push(run);
        }
        runs = coalesce(result.length > 0 ? result : [emptyRun()]);
    }

    root.addEventListener('beforeinput', function (e) {
        if (e.inputType === 'insertParagraph') {
            // Shift+Enter (and plain Enter, since paragraph breaks are
            // handled at the Paragraph level, not inside one) -- insert
            // a break run instead of letting the browser create a new
            // block element.
            e.preventDefault();
            insertBreakAtCursor();
            return;
        }

        if (pendingStyleOverrides && (e.inputType === 'insertText' || e.inputType === 'insertCompositionText')) {
            // A style was toggled with no active selection (Bold/Italic
            // clicked at a collapsed cursor) -- the next characters typed
            // must land in a NEW run carrying that style, not the run the
            // cursor happens to be sitting in. Insert manually rather than
            // letting the browser type into the existing (wrongly-styled)
            // span.
            e.preventDefault();
            insertStyledTextAtCursor(e.data || '');
        }
        // Plain character insertion/deletion with no pending style is
        // left to the browser's native contenteditable behaviour; the
        // resulting DOM is synced back into the model on the subsequent
        // 'input' event (fast path below), not intercepted here.
    });

    function insertStyledTextAtCursor(text) {
        var sel = getSelectionRunRange();
        if (!sel || !text) return;
        var norm = normaliseRangeToRuns(sel);
        if (norm.last >= norm.first) {
            runs.splice(norm.first, norm.last - norm.first + 1);
        }
        var newRun = applyPending(emptyRun());
        newRun.text = text;
        runs.splice(norm.first, 0, newRun);
        pendingStyleOverrides = null;

        runs = coalesce(runs);
        renderRuns();
        // Place the cursor after the inserted text, inside the new run.
        placeCursorAtRunEnd(Math.min(norm.first, runs.length - 1));
        markDirty();
    }

    function placeCursorAtRunEnd(runIndex) {
        reindexSpans();
        var children = root.childNodes;
        for (var i = 0; i < children.length; i++) {
            var idx = parseInt(children[i].getAttribute && children[i].getAttribute('data-run-index'), 10);
            if (idx === runIndex && children[i].firstChild) {
                var range = document.createRange();
                var textNode = children[i].firstChild;
                range.setStart(textNode, textNode.length || 0);
                range.collapse(true);
                var sel = window.getSelection();
                sel.removeAllRanges();
                sel.addRange(range);
                return;
            }
        }
    }

    function insertBreakAtCursor() {
        var sel = getSelectionRunRange();
        if (!sel) return;
        var norm = normaliseRangeToRuns(sel);
        // Remove any selected content first (norm.first..norm.last),
        // matching typical "typing replaces selection" semantics.
        if (norm.last > norm.first || !sel.collapsed) {
            runs.splice(norm.first, norm.last - norm.first + 1);
        }
        var breakRun = emptyRun();
        breakRun.isBreak = true;
        breakRun.text = null;
        runs.splice(norm.first, 0, breakRun);

        var after = applyPending(emptyRun());
        runs.splice(norm.first + 1, 0, after);
        pendingStyleOverrides = null;

        runs = coalesce(runs);
        renderRuns();
        restoreSelectionAcrossRuns(
            Math.min(norm.first + 1, runs.length - 1),
            Math.min(norm.first + 1, runs.length - 1)
        );
        markDirty();
    }

    function applyPending(run) {
        if (!pendingStyleOverrides) return run;
        if (pendingStyleOverrides.isStrong !== undefined) run.isStrong = pendingStyleOverrides.isStrong;
        if (pendingStyleOverrides.isEmph !== undefined) run.isEmph = pendingStyleOverrides.isEmph;
        return run;
    }

    // Fast-path sync: after the browser's own contenteditable typing
    // mutates the DOM, pull just the affected run's text back into the
    // model rather than rebuilding everything.
    root.addEventListener('input', function (e) {
        if (!ready) return;

        // Normal typing with a pending style is handled synchronously in
        // the 'beforeinput' listener above (insertStyledTextAtCursor),
        // which clears pendingStyleOverrides itself. This branch only
        // catches edge cases beforeinput doesn't fully cover on some
        // browsers (IME composition) -- best-effort fallback: rebuild
        // from the DOM as-is and drop the stale pending flag rather than
        // risk applying it to the wrong text.
        if (pendingStyleOverrides) {
            domToModel();
            pendingStyleOverrides = null;
            reindexSpans();
            updateToolbarState();
            return;
        }

        // Fast path: sync each existing span's text from the DOM,
        // handling runs that vanished (deleted) or appeared (rare, e.g.
        // autocorrect) by falling back to a full rebuild when the
        // number of element children no longer matches the model length.
        var elementChildren = 0;
        for (var i = 0; i < root.childNodes.length; i++) {
            if (root.childNodes[i].nodeType === 1) elementChildren++;
        }

        if (elementChildren !== runs.length) {
            domToModel();
            reindexSpans();
            updateToolbarState();
            return;
        }

        for (var c = 0; c < root.childNodes.length; c++) {
            var node = root.childNodes[c];
            if (node.nodeType !== 1) continue;
            var idx = parseInt(node.getAttribute('data-run-index'), 10);
            if (isNaN(idx) || !runs[idx]) continue;
            if (node.tagName === 'BR') continue; // break runs carry no text
            runs[idx].text = node.textContent || '';
        }
        updateToolbarState();
    });

    root.addEventListener('paste', function (e) {
        e.preventDefault();
        var text = (e.clipboardData || window.clipboardData).getData('text/plain');
        if (!text) return;
        document.execCommand('insertText', false, text);
        // Native insertText fires 'input' afterwards, syncing via the
        // fast path above -- plain text only, per project decision.
    });

    root.addEventListener('selectionchange', updateToolbarState);
    document.addEventListener('selectionchange', function () {
        if (document.activeElement === root || root.contains(document.activeElement)) {
            updateToolbarState();
        }
    });

    // ------------------------------------------------------------
    // Toolbar wiring + keyboard shortcuts
    // ------------------------------------------------------------

    var btnBold = document.getElementById('rteBold');
    var btnItalic = document.getElementById('rteItalic');

    if (btnBold) {
        btnBold.addEventListener('click', function (e) {
            e.preventDefault();
            root.focus();
            toggleStyle('isStrong');
        });
    }
    if (btnItalic) {
        btnItalic.addEventListener('click', function (e) {
            e.preventDefault();
            root.focus();
            toggleStyle('isEmph');
        });
    }

    root.addEventListener('keydown', function (e) {
        if (e.ctrlKey && !e.shiftKey && (e.key === 'b' || e.key === 'B')) {
            e.preventDefault();
            e.stopPropagation();
            toggleStyle('isStrong');
            return;
        }
        if (e.ctrlKey && !e.shiftKey && (e.key === 'i' || e.key === 'I')) {
            e.preventDefault();
            e.stopPropagation();
            toggleStyle('isEmph');
            return;
        }
        if (e.key === 'Enter') {
            // Both plain Enter and Shift+Enter insert an inline line
            // break (handled by the 'beforeinput' insertParagraph
            // listener above) -- a full paragraph break is a separate
            // DB row, created via Insert After, not via Enter here.
            // Stop propagation so book-editor.js's document-level
            // handler (which has no Enter binding today, but this keeps
            // intent explicit) never sees it either.
            e.stopPropagation();
            return;
        }
    });

    // ------------------------------------------------------------
    // Load / save
    // ------------------------------------------------------------

    function loadInitialRuns() {
        var payload = document.getElementById('paragraphRunsInit');
        if (!payload) return [emptyRun()];
        try {
            var parsed = JSON.parse(payload.textContent || payload.innerText || '[]');
            if (!parsed || parsed.length === 0) return [emptyRun()];
            return parsed;
        } catch (e) {
            return [emptyRun()];
        }
    }

    function getRuns() {
        // Pre-save consistency check: reconcile model with actual DOM
        // state before serialising, in case fast-path sync drifted.
        domToModel();
        return runs;
    }

    function setRuns(newRuns) {
        runs = coalesce(newRuns && newRuns.length > 0 ? newRuns : [emptyRun()]);
        renderRuns();
    }

    runs = coalesce(loadInitialRuns());
    renderRuns();
    ready = true;

    window._richTextEditor = {
        getRuns: getRuns,
        setRuns: setRuns,
        isReady: function () { return ready; },
        focus: function () { root.focus(); },
        // Internal API for Half B (context menu / overlay dialogs)
        getSelectionRunRange: getSelectionRunRange,
        splitRunAtOffset: splitRunAtOffset,
        normaliseRangeToRuns: normaliseRangeToRuns,
        applyStyleToRange: applyStyleToRange,
        coalesceAndRerender: function () { runs = coalesce(runs); renderRuns(); markDirty(); },
        getRunsRaw: function () { return runs; }
    };
})();
