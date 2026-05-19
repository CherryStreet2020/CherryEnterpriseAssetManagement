/* =============================================================================
   Receiving scan handler — Zebra DataWedge keystroke-output friendly.
   Sprint 11 PR #6 — ADR-016 §D5.

   Activates on any page with a `[data-scan-form]` form. Reads from the
   page's `#scan-input` field and:

     1. Routes whole-string scans to a target field per `data-scan-target`
        on the input. Used when the operator scans a PO number into the
        ByPo wizard, or an ASN ID into ByAsn.

     2. Parses GS1-128 / DataMatrix payloads with embedded Application
        Identifiers (AIs) — when AIs are present, individual fields are
        extracted and routed independently. AIs supported:
          01  GTIN-14         → fills `data-scan-target="gtin"` (or "auto")
          10  batch / lot     → fills `data-scan-target="lot"`
          17  expiry date     → fills `data-scan-target="expiry"` (YYMMDD → YYYY-MM-DD)
          21  serial          → fills `data-scan-target="serial"`

     3. Falls back to whole-string when no AIs are detected.

     4. Supports `?simulate-scan=...` URL param for testing without a
        physical Zebra device. The value is injected on page load AFTER
        focus is set, so it exercises the same code path as a real scan.

   DataWedge default mode emits the scanned string followed by Enter.
   The keydown listener catches Enter on the scan input and triggers parsing.
   ============================================================================= */
(function () {
    'use strict';

    var GS1_FNC1 = ''; // GS1 group separator (ASCII 0x1D / FNC1)
    // AIs with fixed length (the spec defines these — for variable AIs we
    // read up to FNC1 or end-of-string).
    var FIXED_LEN = { '01': 14, '17': 6 };

    function init() {
        var form = document.querySelector('[data-scan-form]');
        if (!form) return;
        var input = form.querySelector('#scan-input');
        if (!input) return;

        // Auto-focus on load.
        try { input.focus(); } catch (e) { /* ignore — Safari sometimes refuses */ }

        // Re-focus after every successful scan, so the operator can rip
        // through multiple lines without clicking back.
        input.addEventListener('blur', function () {
            // Don't refocus if user clicked into another form field on purpose.
            setTimeout(function () {
                var active = document.activeElement;
                if (active && active.tagName === 'BODY') input.focus();
            }, 50);
        });

        input.addEventListener('keydown', function (e) {
            if (e.key !== 'Enter') return;
            e.preventDefault();
            handleScan(form, input.value || '');
            input.value = '';
            input.focus();
        });

        // ?simulate-scan=... support for testing.
        var sim = new URLSearchParams(window.location.search).get('simulate-scan');
        if (sim) {
            input.value = sim;
            setTimeout(function () {
                handleScan(form, sim);
                input.value = '';
                input.focus();
            }, 200);
        }
    }

    function handleScan(form, raw) {
        if (!raw) return;
        var trimmed = raw.trim();
        if (!trimmed) return;

        // Try GS1 AI parse first; fall back to whole-string route if no AIs.
        var parts = parseGs1(trimmed);
        if (parts && Object.keys(parts).length > 0) {
            applyAis(form, parts);
            // Fire a custom event for any page-level handlers that want it.
            form.dispatchEvent(new CustomEvent('cherry:scan', { detail: { type: 'gs1', parts: parts, raw: trimmed } }));
        } else {
            applyWhole(form, trimmed);
            form.dispatchEvent(new CustomEvent('cherry:scan', { detail: { type: 'plain', value: trimmed, raw: trimmed } }));
        }
    }

    // Parse a GS1-128 / DataMatrix payload into {ai: value, ...}.
    // Returns {} if no AIs are detected.
    function parseGs1(s) {
        var out = {};
        var i = 0;
        var found = false;
        while (i < s.length) {
            // Skip optional FNC1 between fields
            if (s[i] === GS1_FNC1) { i++; continue; }
            // Each AI starts with 2 digits
            if (i + 2 > s.length) break;
            var ai = s.substring(i, i + 2);
            if (!/^\d{2}$/.test(ai)) break;
            var len = FIXED_LEN[ai];
            i += 2;
            var value;
            if (len) {
                if (i + len > s.length) break;
                value = s.substring(i, i + len);
                i += len;
            } else {
                // Variable length — read until FNC1 or end.
                var end = s.indexOf(GS1_FNC1, i);
                if (end < 0) end = s.length;
                value = s.substring(i, end);
                i = end;
            }
            out[ai] = value;
            found = true;
        }
        return found ? out : null;
    }

    function applyAis(form, parts) {
        if (parts['01']) {
            // GTIN-14 — strip leading zeros for display routing
            setTargets(form, ['gtin', 'item', 'auto'], parts['01']);
        }
        if (parts['10']) setTargets(form, ['lot'], parts['10']);
        if (parts['17']) {
            // YYMMDD → ISO YYYY-MM-DD
            var v = parts['17'];
            if (/^\d{6}$/.test(v)) {
                v = '20' + v.substring(0, 2) + '-' + v.substring(2, 4) + '-' + v.substring(4, 6);
            }
            setTargets(form, ['expiry'], v);
        }
        if (parts['21']) setTargets(form, ['serial'], parts['21']);
    }

    function applyWhole(form, value) {
        var scanInput = form.querySelector('#scan-input');
        var target = scanInput ? scanInput.getAttribute('data-scan-target') : 'auto';
        if (!target || target === 'auto') {
            // Default destinations: po / asn / lot — whichever the page wires first.
            target = 'po';
        }
        setTargets(form, [target], value);
    }

    function setTargets(form, targets, value) {
        for (var i = 0; i < targets.length; i++) {
            var t = targets[i];
            // Match either the explicit data-scan-target attribute on an input
            // OR a field name like "Input.PoNumber" by convention.
            var sel = '[data-scan-target="' + t + '"]:not(#scan-input)';
            var el = form.querySelector(sel);
            if (!el) {
                // Try convention-based: Input.PoNumber / Input.LotNumber / etc.
                var conv = {
                    'po': 'Input.PoNumber',
                    'asn': 'Input.AsnId',
                    'lot': 'Input.LotNumber',
                    'serial': 'Input.SerialNumber',
                    'expiry': 'Input.Attributes',
                    'gtin': 'Input.ItemId',
                    'item': 'Input.ItemId',
                };
                if (conv[t]) {
                    el = form.querySelector('[name="' + conv[t] + '"]');
                }
            }
            if (el) {
                el.value = value;
                el.dispatchEvent(new Event('input', { bubbles: true }));
                el.dispatchEvent(new Event('change', { bubbles: true }));
                return; // first hit wins
            }
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
