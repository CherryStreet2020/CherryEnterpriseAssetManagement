// PR #116a — scroll-position preservation on list ↔ detail navigation.
//
// Problem: clicking an asset row from /Assets (mid-scroll), then clicking
// "Back to results" on the detail page, returned the user to the TOP of
// the 334-row table. The user lost their place. Same pattern across
// every list-detail page.
//
// Solution: snapshot window.scrollY to sessionStorage keyed by the full
// current URL (path + query) before any same-origin navigation. On page
// load, if a snapshot exists for the current URL, restore it.
//
// Browser-native scroll restoration is unreliable here because Razor
// Pages full-reloads on every nav (no Turbo, no SPA). sessionStorage
// scoped to URL is the simplest correct primitive.
//
// Storage size: each entry is a small JSON of `{ y: number, t: number }`.
// We prune entries older than 30 minutes on every save to keep the store
// from growing unbounded across a long session.
(function() {
    'use strict';

    var STORAGE_KEY = 'cherryai_scrollPos_v1';
    var MAX_AGE_MS = 30 * 60 * 1000; // 30 minutes

    function readStore() {
        try {
            var raw = sessionStorage.getItem(STORAGE_KEY);
            return raw ? JSON.parse(raw) : {};
        } catch (e) {
            return {};
        }
    }

    function writeStore(store) {
        try {
            sessionStorage.setItem(STORAGE_KEY, JSON.stringify(store));
        } catch (e) {
            // sessionStorage quota or disabled — fail silently.
        }
    }

    function currentKey() {
        return window.location.pathname + window.location.search;
    }

    function pruneStore(store) {
        var now = Date.now();
        Object.keys(store).forEach(function(k) {
            if (!store[k] || (now - store[k].t) > MAX_AGE_MS) {
                delete store[k];
            }
        });
    }

    function snapshot() {
        var y = window.scrollY || document.documentElement.scrollTop || 0;
        // Skip storing y=0 — that's the default and would overwrite a
        // meaningful prior snapshot when the user navigates away after
        // returning to top.
        if (y <= 0) return;
        var store = readStore();
        store[currentKey()] = { y: y, t: Date.now() };
        pruneStore(store);
        writeStore(store);
    }

    function restore() {
        var store = readStore();
        var snap = store[currentKey()];
        if (!snap || typeof snap.y !== 'number') return;
        // Defer the scroll until the layout has settled. requestAnimationFrame
        // alone isn't always enough on Razor pages because images and
        // late-binding JS can shift content — wait two frames + 60ms.
        window.requestAnimationFrame(function() {
            window.requestAnimationFrame(function() {
                window.scrollTo({ top: snap.y, left: 0, behavior: 'instant' });
                // Belt + braces in case images shift the document height.
                setTimeout(function() {
                    window.scrollTo({ top: snap.y, left: 0, behavior: 'instant' });
                }, 60);
            });
        });
    }

    // Snapshot before leaving the page (covers tab close + nav).
    window.addEventListener('beforeunload', snapshot);

    // Snapshot on every same-origin link click and form submit so we
    // capture state before the navigation fires (beforeunload sometimes
    // gets squelched by browsers that consider the nav "user-initiated").
    document.addEventListener('click', function(e) {
        var anchor = e.target.closest('a[href]');
        if (!anchor) return;
        var href = anchor.getAttribute('href');
        if (!href || href.startsWith('#')) return;
        // Don't snapshot if user is opening in a new tab/window.
        if (e.metaKey || e.ctrlKey || e.shiftKey || anchor.target === '_blank') return;
        snapshot();
    }, true);
    document.addEventListener('submit', snapshot, true);

    // Disable the browser's automatic scroll restoration so we can do it
    // ourselves — without this, Chrome will fight us and stutter on Back.
    if ('scrollRestoration' in window.history) {
        window.history.scrollRestoration = 'manual';
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', restore);
    } else {
        restore();
    }
})();
