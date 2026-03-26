// ============================================
// Book Editor JavaScript
// ============================================

(function () {
    'use strict';

    var autoSaveTimer = null;
    var hasUnsavedChanges = false;
    var isAutoSaveEnabled = true;

    // Only run on editor page
    if (typeof editorConfig === 'undefined') return;

    var paragraphText = document.getElementById('paragraphText');
    var metaText = document.getElementById('metaText');
    var editNoteText = document.getElementById('editNoteText');
    var saveStatus = document.getElementById('saveStatus');
    var paragraphId = editorConfig.paragraphId;

    // Track changes in all textareas
    var textareas = [paragraphText, metaText, editNoteText];
    textareas.forEach(function (el) {
        if (!el) return;
        el.addEventListener('input', function () {
            hasUnsavedChanges = true;
            if (isAutoSaveEnabled) {
                clearTimeout(autoSaveTimer);
                autoSaveTimer = setTimeout(autoSave, 3000);
            }
        });
    });

    // Auto-save function
    function autoSave() {
        if (!hasUnsavedChanges) return;

        updateStatus('Saving...', 'saving');

        var xhr = new XMLHttpRequest();
        xhr.open('POST', editorConfig.autoSaveUrl, true);
        xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
        xhr.onreadystatechange = function () {
            if (xhr.readyState !== 4) return;

            if (xhr.status === 200) {
                try {
                    var response = JSON.parse(xhr.responseText);
                    if (response.success) {
                        hasUnsavedChanges = false;
                        updateStatus('Saved at ' + response.timestamp, 'saved');
                    } else {
                        updateStatus('Save failed: ' + (response.message || 'Unknown error'), 'error');
                    }
                } catch (e) {
                    updateStatus('Save failed', 'error');
                }
            } else {
                updateStatus('Save failed (HTTP ' + xhr.status + ')', 'error');
            }
        };

        var data = 'paragraphId=' + encodeURIComponent(paragraphId) +
            '&paragraphText=' + encodeURIComponent(paragraphText ? paragraphText.value : '') +
            '&metaText=' + encodeURIComponent(metaText ? metaText.value : '') +
            '&editNoteText=' + encodeURIComponent(editNoteText ? editNoteText.value : '');
        xhr.send(data);
    }

    function updateStatus(text, className) {
        if (!saveStatus) return;
        saveStatus.textContent = text;
        saveStatus.className = 'save-status ' + (className || '');
    }

    // Keyboard shortcuts
    document.addEventListener('keydown', function (e) {
        var activeTag = document.activeElement ? document.activeElement.tagName : '';
        var inTextField = (activeTag === 'TEXTAREA' || activeTag === 'INPUT' || activeTag === 'SELECT');

        // Ctrl+S: Save — works everywhere including inside textareas
        if (e.ctrlKey && e.key === 's') {
            e.preventDefault();
            autoSave();
        }

        // Navigation and action shortcuts are suppressed when focus is inside
        // a text field so that Ctrl+Left/Right/Home/End perform normal cursor
        // movement and Ctrl+Shift+Arrow performs word selection as expected.
        if (inTextField) return;

        // Ctrl+Left: Previous paragraph
        if (e.ctrlKey && e.key === 'ArrowLeft') {
            e.preventDefault();
            if (editorConfig.prevUrl) {
                saveAndNavigate(editorConfig.prevUrl);
            }
        }

        // Ctrl+Right: Next paragraph
        if (e.ctrlKey && e.key === 'ArrowRight') {
            e.preventDefault();
            if (editorConfig.nextUrl) {
                saveAndNavigate(editorConfig.nextUrl);
            }
        }

        // Ctrl+Home: First paragraph
        if (e.ctrlKey && e.key === 'Home') {
            e.preventDefault();
            if (editorConfig.firstUrl) {
                saveAndNavigate(editorConfig.firstUrl);
            }
        }

        // Ctrl+End: Last paragraph
        if (e.ctrlKey && e.key === 'End') {
            e.preventDefault();
            if (editorConfig.lastUrl) {
                saveAndNavigate(editorConfig.lastUrl);
            }
        }

        // Ctrl+I: Insert paragraph after
        if (e.ctrlKey && !e.shiftKey && e.key === 'i') {
            e.preventDefault();
            var insertForm = document.querySelector('[action*="InsertParagraph"]:not([action*="before"])');
            if (insertForm) insertForm.submit();
        }

        // Ctrl+D: Delete paragraph
        if (e.ctrlKey && e.key === 'd') {
            e.preventDefault();
            var deleteForm = document.getElementById('deleteForm');
            if (deleteForm && confirm('Delete this paragraph? This cannot be undone.')) {
                deleteForm.submit();
            }
        }
    });

    // Intercept the Save button so it uses AJAX autosave instead of a full form
    // submit. This keeps the page URL intact, preserving any active search state.
    var btnSave = document.getElementById('btnSave');
    if (btnSave) {
        btnSave.addEventListener('click', function (e) {
            e.preventDefault();
            autoSave();
        });
    }

    // Append current search params (q, ww) to a navigation URL if not already
    // present, so active searches survive keyboard and button navigation.
    function appendSearchParams(url) {
        var cur = new URLSearchParams(window.location.search);
        var q = cur.get('q');
        if (!q) return url;
        if (url.indexOf('&q=') >= 0 || url.indexOf('?q=') >= 0) return url;
        url += (url.indexOf('?') >= 0 ? '&' : '?') + 'q=' + encodeURIComponent(q);
        if (cur.get('ww') === '1') url += '&ww=1';
        return url;
    }

    function saveAndNavigate(url) {
        url = appendSearchParams(url);
        if (hasUnsavedChanges) {
            // Save first, then navigate
            updateStatus('Saving...', 'saving');
            var xhr = new XMLHttpRequest();
            xhr.open('POST', editorConfig.autoSaveUrl, true);
            xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
            xhr.onreadystatechange = function () {
                if (xhr.readyState === 4) {
                    window.location.href = url;
                }
            };
            var data = 'paragraphId=' + encodeURIComponent(paragraphId) +
                '&paragraphText=' + encodeURIComponent(paragraphText ? paragraphText.value : '') +
                '&metaText=' + encodeURIComponent(metaText ? metaText.value : '') +
                '&editNoteText=' + encodeURIComponent(editNoteText ? editNoteText.value : '');
            xhr.send(data);
        } else {
            window.location.href = url;
        }
    }

    // Chapter jump dropdown — navigate directly; option values are full URLs.
    // Using window.location.href directly avoids the async save-then-navigate
    // race that caused nav buttons to be out of sync after a chapter jump.
    // Unsaved changes are handled by the beforeunload warning below.
    var chapterJump = document.getElementById('chapterJump');
    if (chapterJump) {
        chapterJump.addEventListener('change', function () {
            if (this.value) {
                window.location.href = this.value;
            }
        });
    }

    // Warn on page navigation with unsaved changes
    window.addEventListener('beforeunload', function (e) {
        if (hasUnsavedChanges) {
            e.preventDefault();
            e.returnValue = '';
        }
    });

    // Route First/Prev/Next/Last nav buttons through saveAndNavigate so unsaved
    // changes are written before leaving, rather than triggering beforeunload.
    ['btnPrev', 'btnNext'].forEach(function (id) {
        var el = document.getElementById(id);
        if (el) {
            el.addEventListener('click', function (e) {
                if (hasUnsavedChanges && el.href && !el.classList.contains('disabled')) {
                    e.preventDefault();
                    saveAndNavigate(el.href);
                }
            });
        }
    });
    ['First paragraph (Ctrl+Home)', 'Last paragraph (Ctrl+End)'].forEach(function (title) {
        var el = document.querySelector('a[title="' + title + '"]');
        if (el) {
            el.addEventListener('click', function (e) {
                if (hasUnsavedChanges && el.href && !el.classList.contains('disabled')) {
                    e.preventDefault();
                    saveAndNavigate(el.href);
                }
            });
        }
    });

    // Expose saveAndNavigate for the GoTo inline script
    window._editorSaveAndNavigate = saveAndNavigate;

    // Persist textarea heights within the browser session so sizes survive
    // paragraph-to-paragraph navigation. Uses sessionStorage (cleared on tab close)
    // since preferred sizes are viewport/session dependent.
    var taIds = ['paragraphText', 'metaText', 'editNoteText'];
    taIds.forEach(function (id) {
        var el = document.getElementById(id);
        if (!el) return;
        var key = 'editorTA_h_' + id;
        var saved = sessionStorage.getItem(key);
        if (saved) el.style.height = saved;
        if (typeof ResizeObserver !== 'undefined') {
            new ResizeObserver(function () {
                if (el.style.height) sessionStorage.setItem(key, el.style.height);
            }).observe(el);
        }
    });

})();
