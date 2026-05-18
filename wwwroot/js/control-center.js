/* =============================================================================
   Control Center — interactive behavior (Sprint 11 PR #2)
   ADR-016 §D1 + D3.

   Wires up:
     - J / K / ↑ / ↓ keyboard navigation across exception-lane rows
     - Enter to open the drawer (or follow href) for the focused row
     - Esc to close the drawer
     - Click-to-open for any row with data-cc-drawer-id or data-cc-href
     - Activity feed collapse/expand
     - Voice FAB push-to-talk + status pulse (UI only — backend wired in PR #4)

   Idempotent: safe to call init() multiple times or on partial DOM swaps.
   ============================================================================= */
(function () {
    'use strict';

    var initialized = false;

    function init() {
        if (initialized) return;
        if (!document.querySelector('[data-cc-shell]')) return;
        initialized = true;

        wireLane();
        wireFeed();
        wireVoiceFab();
        wireGlobalKeys();
    }

    /* ---------- Exception lane ------------------------------------------- */

    function wireLane() {
        var lane = document.querySelector('[data-cc-lane]');
        if (!lane) return;
        var listbox = lane.querySelector('[role="listbox"]');
        if (!listbox) return;

        var rows = Array.prototype.slice.call(lane.querySelectorAll('[data-cc-row]'));
        if (!rows.length) return;

        var focusedIndex = -1;

        function focusRow(idx) {
            if (idx < 0 || idx >= rows.length) return;
            if (focusedIndex >= 0 && rows[focusedIndex]) {
                rows[focusedIndex].setAttribute('aria-selected', 'false');
            }
            focusedIndex = idx;
            var row = rows[idx];
            row.setAttribute('aria-selected', 'true');
            listbox.setAttribute('aria-activedescendant', row.id);
            row.scrollIntoView({ block: 'nearest' });
        }

        function activateRow(row) {
            if (!row) return;
            var drawerId = row.getAttribute('data-cc-drawer-id');
            var href = row.getAttribute('data-cc-href');
            if (drawerId && window.CherryDS && window.CherryDS.drawer && typeof window.CherryDS.drawer.open === 'function') {
                window.CherryDS.drawer.open(drawerId);
                setDrawerOpenState(true);
            } else if (href) {
                window.location.href = href;
            }
        }

        rows.forEach(function (row, idx) {
            row.addEventListener('click', function () {
                focusRow(idx);
                activateRow(row);
            });
        });

        listbox.addEventListener('keydown', function (e) {
            if (e.key === 'j' || e.key === 'ArrowDown') {
                e.preventDefault();
                focusRow(Math.min(focusedIndex + 1, rows.length - 1) || 0);
            } else if (e.key === 'k' || e.key === 'ArrowUp') {
                e.preventDefault();
                focusRow(Math.max(focusedIndex - 1, 0));
            } else if (e.key === 'Enter' && focusedIndex >= 0) {
                e.preventDefault();
                activateRow(rows[focusedIndex]);
            } else if (e.key === 'Home') {
                e.preventDefault();
                focusRow(0);
            } else if (e.key === 'End') {
                e.preventDefault();
                focusRow(rows.length - 1);
            }
        });

        listbox.addEventListener('focus', function () {
            if (focusedIndex < 0) focusRow(0);
        });
    }

    function setDrawerOpenState(open) {
        var shell = document.querySelector('[data-cc-shell]');
        if (!shell) return;
        if (open) shell.setAttribute('data-cc-drawer-open', 'true');
        else      shell.removeAttribute('data-cc-drawer-open');
    }

    /* ---------- Activity feed -------------------------------------------- */

    function wireFeed() {
        var feeds = document.querySelectorAll('[data-cc-feed]');
        feeds.forEach(function (feed) {
            var toggle = feed.querySelector('[data-cc-feed-toggle]');
            if (!toggle) return;
            toggle.addEventListener('click', function () {
                var collapsed = feed.classList.toggle('ds-cc-feed--collapsed');
                toggle.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
            });

            // Clickable entries — drawer or href
            feed.querySelectorAll('[data-cc-feed-entry]').forEach(function (entry) {
                if (!entry.classList.contains('ds-cc-feed__entry--clickable')) return;
                entry.addEventListener('click', function () {
                    var drawerId = entry.getAttribute('data-cc-drawer-id');
                    var href = entry.getAttribute('data-cc-href');
                    if (drawerId && window.CherryDS && window.CherryDS.drawer && typeof window.CherryDS.drawer.open === 'function') {
                        window.CherryDS.drawer.open(drawerId);
                        setDrawerOpenState(true);
                    } else if (href) {
                        window.location.href = href;
                    }
                });
            });
        });
    }

    /* ---------- Voice FAB (push-to-talk default) ------------------------- */

    function wireVoiceFab() {
        var fab = document.querySelector('[data-cc-voice]');
        if (!fab) return;
        var posture = fab.getAttribute('data-cc-voice-posture') || 'push-to-talk';

        function setActive(active) {
            if (active) fab.setAttribute('data-cc-voice-active', 'true');
            else        fab.removeAttribute('data-cc-voice-active');
            // Fire a custom event the voice client can subscribe to.
            window.dispatchEvent(new CustomEvent('cherry:voice:state', {
                detail: { active: active, posture: posture }
            }));
        }

        // Click toggles the listening state for keyboard users + accessibility.
        fab.addEventListener('click', function () {
            var active = fab.getAttribute('data-cc-voice-active') === 'true';
            setActive(!active);
        });

        // Push-to-talk: hold Space (when no input is focused) to enable.
        if (posture === 'push-to-talk') {
            var spaceHeld = false;
            document.addEventListener('keydown', function (e) {
                if (e.code !== 'Space') return;
                if (isTypingTarget(e.target)) return;
                if (spaceHeld) return;
                spaceHeld = true;
                e.preventDefault();
                setActive(true);
            });
            document.addEventListener('keyup', function (e) {
                if (e.code !== 'Space') return;
                if (!spaceHeld) return;
                spaceHeld = false;
                setActive(false);
            });
        }
    }

    function isTypingTarget(el) {
        if (!el) return false;
        var tag = (el.tagName || '').toUpperCase();
        if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') return true;
        if (el.isContentEditable) return true;
        return false;
    }

    /* ---------- Global keys (Esc closes drawer) -------------------------- */

    function wireGlobalKeys() {
        document.addEventListener('keydown', function (e) {
            if (e.key !== 'Escape') return;
            setDrawerOpenState(false);
            // The existing CherryDS.drawer handles its own ESC; we just sync
            // the shell attribute so the lane re-expands.
        });

        // Patch CherryDS.drawer.open/close to keep ds-cc-drawer-open in sync.
        // Done lazily so primitives.js (which defines CherryDS) can load first.
        function patch() {
            if (!window.CherryDS || !window.CherryDS.drawer) return false;
            var d = window.CherryDS.drawer;
            if (d.__ccPatched) return true;
            var origOpen = d.open;
            var origClose = d.close;
            d.open = function (id) {
                var r = origOpen ? origOpen.apply(this, arguments) : undefined;
                setDrawerOpenState(true);
                return r;
            };
            d.close = function (id) {
                var r = origClose ? origClose.apply(this, arguments) : undefined;
                setDrawerOpenState(false);
                return r;
            };
            d.__ccPatched = true;
            return true;
        }
        if (!patch()) {
            var tries = 0;
            var iv = setInterval(function () {
                if (patch() || ++tries > 20) clearInterval(iv);
            }, 100);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
    window.CherryControlCenter = { init: init };
})();
