/* =============================================================================
   cockpit.js — Sprint 12A PR #2 / Cockpit primitives extract.
   ADR-018 §D3 + §D6 — JSON-blob preview hydration pattern (proven ~100ms median
   page load in PR #117.8 of the legacy /Receiving/Cockpit-Legacy page).

   Source: extracted verbatim from the inline <script> block in
   Pages/Receiving/Index.cshtml.

   Public globals (set on window when this file loads):
     - filterQueue(q): filter the queue rows by case-insensitive substring on
       data-po + data-vendor attributes; collapses groups with no visible rows.
     - selectPO(id): swap the right pane from welcome → preview, render the
       hydrated entity from the in-page JSON blob.

   Consumers must:
     - Emit a <script id="__poDetails" type="application/json">[…]</script>
       blob in the page body, before this script tag, with the same row schema
       (id / num / vendor / orderDate / requiredDate / status / total / shipTo /
       lines[]).
     - Wire row click handlers in the queue partials to call selectPO(rowId).
     - Wire the search input to call filterQueue(value).

   PR #3 will introduce ICockpitQueueRow + CockpitPreviewSerializer so the
   payload schema is contract-validated. For now the shape is whatever the
   page's inline JSON emits.
   ============================================================================= */

(function () {
    'use strict';

    var PO_DATA = [];
    try {
        var blob = document.getElementById('__poDetails');
        if (blob) { PO_DATA = JSON.parse(blob.textContent); }
    } catch (e) { /* swallow — preview pane will stay on welcome state */ }

    function filterQueue(q) {
        var lc = (q || '').toLowerCase();
        document.querySelectorAll('.cockpit__card').forEach(function (card) {
            var po = (card.getAttribute('data-po') || '').toLowerCase();
            var vendor = (card.getAttribute('data-vendor') || '').toLowerCase();
            card.style.display = (lc.length === 0 || po.indexOf(lc) >= 0 || vendor.indexOf(lc) >= 0) ? '' : 'none';
        });
        document.querySelectorAll('.cockpit__group').forEach(function (g) {
            g.style.display = g.querySelectorAll('.cockpit__card:not([style*="display: none"])').length > 0 ? '' : 'none';
        });
    }

    function selectPO(id) {
        document.querySelectorAll('.cockpit__card').forEach(function (c) { c.classList.remove('cockpit__card--active'); });
        var card = document.querySelector('.cockpit__card[data-id="' + id + '"]');
        if (card) card.classList.add('cockpit__card--active');

        var po = PO_DATA.find(function (p) { return p.id === id; });
        if (!po) return;

        var welcome = document.getElementById('mainWelcome');
        var pv = document.getElementById('mainPreview');
        if (welcome) welcome.style.display = 'none';
        if (pv) pv.style.display = 'flex';

        document.getElementById('pvPoNum').textContent = po.num;
        document.getElementById('pvReceiveBtn').href = '/Receiving/Receive/' + id;

        var statusEl = document.getElementById('pvStatus');
        statusEl.textContent = po.status;
        statusEl.className = 'cockpit__preview-status status-badge-p ' + (
            po.status === 'PartiallyReceived' ? 'status-badge-p--pending' :
            po.status === 'Sent' ? 'status-badge-p--info' : 'status-badge-p--approved'
        );

        document.getElementById('pvVendor').textContent = po.vendor;
        document.getElementById('pvOrderDate').textContent = po.orderDate;
        document.getElementById('pvReqDate').textContent = po.requiredDate;
        document.getElementById('pvShipTo').textContent = po.shipTo;
        document.getElementById('pvTotal').textContent = '$' + po.total;
        document.getElementById('pvLineCount').textContent = po.lines.length + ' lines';

        var tbody = document.getElementById('pvLinesBody');
        tbody.innerHTML = po.lines.map(function (l) {
            var done = l.remaining <= 0;
            var remHtml = done
                ? '<span class="status-badge-p status-badge-p--approved" data-csp-style="font-size:0.55rem;">DONE</span>'
                : '<strong>' + l.remaining.toFixed(0) + '</strong>';
            var putaway = l.putaway.length > 0 ? l.putaway.join(' / ') : '—';

            return '<tr class="line-table__row" data-csp-style="' + (done ? 'opacity:0.4;' : '') + '">'
                + '<td class="line-table__td line-table__td--mono" data-csp-style="font-weight:600;">' + l.partNum + '</td>'
                + '<td class="line-table__td line-table__td--primary">' + l.desc + '</td>'
                + '<td class="line-table__td line-table__td--muted">' + l.uom + '</td>'
                + '<td class="line-table__td line-table__td--right line-table__td--mono">' + l.ordered.toFixed(0) + '</td>'
                + '<td class="line-table__td line-table__td--right line-table__td--mono line-table__td--muted">' + l.received.toFixed(0) + '</td>'
                + '<td class="line-table__td line-table__td--right line-table__td--mono">' + remHtml + '</td>'
                + '<td class="line-table__td line-table__td--right line-table__td--mono line-table__td--bold">$' + l.lineTotal.toFixed(2) + '</td>'
                + '<td class="line-table__td line-table__td--muted" data-csp-style="font-size:0.75rem;">' + putaway + '</td>'
                + '</tr>';
        }).join('');
    }

    // Expose to the page so inline onclick/oninput handlers in the legacy
    // Pages/Receiving/Index.cshtml + _QueueCard.cshtml partial keep working.
    // PR #5 (PO Queue tab on the new shell) will migrate these to delegated
    // event listeners attached after the partial renders.
    window.filterQueue = filterQueue;
    window.selectPO = selectPO;

    // -------------------------------------------------------------------------
    // Sprint 12A PR #6 — selectAsn (ASN Queue cockpit).
    //
    // Mirrors selectPO but reads __asnDetails (ASN-specific shape) and maps
    // ASN-specific fields: Carrier, Tracking, Source PO. Manifest lines use
    // expected/received/remaining/lot/heat instead of ordered/received/
    // remaining/unitPrice/lineTotal/putaway. Same #pvXxx DOM ids as the PO
    // preview so the same #mainPreview container hosts both.
    //
    // The Receive CTA links to the existing /Receiving/ByAsn page with
    // ?asn={id} query — Sprint 11 plumbed that handler already.
    // -------------------------------------------------------------------------
    var ASN_DATA = [];
    try {
        var asnBlob = document.getElementById('__asnDetails');
        if (asnBlob) { ASN_DATA = JSON.parse(asnBlob.textContent); }
    } catch (e) { /* swallow — preview pane stays on welcome state */ }

    function selectAsn(id) {
        document.querySelectorAll('.cockpit__card').forEach(function (c) { c.classList.remove('cockpit__card--active'); });
        var card = document.querySelector('.cockpit__card[data-id="' + id + '"]');
        if (card) card.classList.add('cockpit__card--active');

        var asn = ASN_DATA.find(function (a) { return a.id === id; });
        if (!asn) return;

        var welcome = document.getElementById('mainWelcome');
        var pv = document.getElementById('mainPreview');
        if (welcome) welcome.style.display = 'none';
        if (pv) pv.style.display = 'flex';

        document.getElementById('pvPoNum').textContent = asn.num;
        document.getElementById('pvReceiveBtn').href = '/Receiving/ByAsn?asn=' + encodeURIComponent(asn.num);

        var statusEl = document.getElementById('pvStatus');
        statusEl.textContent = asn.status;
        statusEl.className = 'cockpit__preview-status status-badge-p ' + (
            asn.status === 'Arrived'   ? 'status-badge-p--warning' :
            asn.status === 'Receiving' ? 'status-badge-p--pending' :
            asn.status === 'InTransit' ? 'status-badge-p--info' :
            'status-badge-p--approved'
        );

        document.getElementById('pvVendor').textContent = asn.vendor;
        document.getElementById('pvOrderDate').textContent = asn.orderDate;
        document.getElementById('pvReqDate').textContent = asn.requiredDate;
        document.getElementById('pvShipTo').textContent = asn.shipTo;
        document.getElementById('pvTotal').textContent = asn.total;

        var carrierEl = document.getElementById('pvCarrier');
        if (carrierEl) carrierEl.textContent = asn.carrier || '—';
        var trackingEl = document.getElementById('pvTracking');
        if (trackingEl) trackingEl.textContent = asn.tracking || '—';
        var sourcePoEl = document.getElementById('pvSourcePo');
        if (sourcePoEl) sourcePoEl.textContent = asn.sourcePo || '—';

        document.getElementById('pvLineCount').textContent = asn.lines.length + ' lines';

        var tbody = document.getElementById('pvLinesBody');
        tbody.innerHTML = asn.lines.map(function (l) {
            var done = l.remaining <= 0;
            var remHtml = done
                ? '<span class="status-badge-p status-badge-p--approved" data-csp-style="font-size:0.55rem;">DONE</span>'
                : '<strong>' + l.remaining.toFixed(0) + '</strong>';
            var lotOrHeat = l.heat ? ('HT ' + l.heat) : (l.lot || '—');

            return '<tr class="line-table__row" data-csp-style="' + (done ? 'opacity:0.4;' : '') + '">'
                + '<td class="line-table__td line-table__td--mono" data-csp-style="font-weight:600;">' + l.partNum + '</td>'
                + '<td class="line-table__td line-table__td--primary">' + l.desc + '</td>'
                + '<td class="line-table__td line-table__td--muted">' + l.uom + '</td>'
                + '<td class="line-table__td line-table__td--right line-table__td--mono">' + l.expected.toFixed(0) + '</td>'
                + '<td class="line-table__td line-table__td--right line-table__td--mono line-table__td--muted">' + l.received.toFixed(0) + '</td>'
                + '<td class="line-table__td line-table__td--right line-table__td--mono">' + remHtml + '</td>'
                + '<td class="line-table__td line-table__td--muted" data-csp-style="font-size:0.75rem;">' + lotOrHeat + '</td>'
                + '</tr>';
        }).join('');
    }
    window.selectAsn = selectAsn;

    // -------------------------------------------------------------------------
    // Cockpit tab keyboard nav (Sprint 12A PR #4 / ADR-018 §D2).
    // Left/Right arrows cycle through tabs when focus is on the tab bar.
    // Home/End jump to first/last. The clicked tab navigates via its href so
    // browser back/forward preserves tab state automatically.
    // -------------------------------------------------------------------------
    function bindTabKeyboardNav() {
        var bar = document.querySelector('[data-cockpit-tabs]');
        if (!bar) return;
        bar.addEventListener('keydown', function (e) {
            var tabs = Array.prototype.slice.call(bar.querySelectorAll('.cockpit-tab'));
            if (!tabs.length) return;
            var idx = tabs.indexOf(document.activeElement);
            if (idx < 0) idx = tabs.findIndex(function (t) { return t.classList.contains('cockpit-tab--active'); });
            var next = null;
            switch (e.key) {
                case 'ArrowRight': next = tabs[(idx + 1) % tabs.length]; break;
                case 'ArrowLeft':  next = tabs[(idx - 1 + tabs.length) % tabs.length]; break;
                case 'Home':       next = tabs[0]; break;
                case 'End':        next = tabs[tabs.length - 1]; break;
                default: return;
            }
            if (next) {
                e.preventDefault();
                next.focus();
                next.click();
            }
        });
    }
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', bindTabKeyboardNav);
    } else {
        bindTabKeyboardNav();
    }

    // -------------------------------------------------------------------------
    // Sprint 12A PR #5.1 — KPI band drill-through.
    //
    // Workload tiles (Overdue / Due Today / This Week) carry data-drill-scroll
    // with a group key ("overdue" / "today" / "this-week" / "later"). Clicking
    // such a tile scrolls .cockpit__group[data-group="{key}"] into view and
    // briefly pulses it (CSS keyframe drives the visual). The page never
    // navigates — the drill is a focus shift within the same canvas.
    //
    // Quality tiles use a hard href (e.g. "/Receiving?tab=exceptions") and
    // are rendered as <a> — the browser handles the navigation natively, no
    // JS needed here.
    // -------------------------------------------------------------------------
    function bindKpiBandDrill() {
        var band = document.querySelector('.cockpit-kpi-band');
        if (!band) return;

        band.addEventListener('click', function (e) {
            var tile = e.target.closest('[data-drill-target="scroll"]');
            if (!tile) return;
            var key = tile.getAttribute('data-drill-scroll');
            if (!key) return;
            var group = document.querySelector('.cockpit__group[data-group="' + key + '"]');
            if (!group) return;
            try { group.scrollIntoView({ behavior: 'smooth', block: 'start' }); } catch (_) { group.scrollIntoView(); }
            group.classList.remove('cockpit__group--drill-pulse');
            // re-trigger the keyframe by forcing reflow
            // eslint-disable-next-line no-unused-expressions
            void group.offsetWidth;
            group.classList.add('cockpit__group--drill-pulse');
            setTimeout(function () { group.classList.remove('cockpit__group--drill-pulse'); }, 800);
        });
    }
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', bindKpiBandDrill);
    } else {
        bindKpiBandDrill();
    }

    // -------------------------------------------------------------------------
    // Sprint 12A PR #5.1 — auto-collapse sidebar on Control Center pages.
    //
    // The workspace canvas is the focus on a Control Center; the IA sidebar
    // doesn't need to take 230px on first paint. We force the sidebar's
    // existing .collapsed state on entry to ANY page whose body carries the
    // .is-control-center class. The user can click the collapse button to
    // expand temporarily (existing sidebar-nav.js handles localStorage).
    //
    // Idempotent — safe to run multiple times.
    // -------------------------------------------------------------------------
    function applyControlCenterRailMode() {
        if (!document.body.classList.contains('is-control-center')) return;
        var sidebar = document.getElementById('mainSidebar');
        if (!sidebar) return;
        if (!sidebar.classList.contains('collapsed')) {
            sidebar.classList.add('collapsed');
            document.body.classList.add('sidebar-is-collapsed');
        }
    }
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', applyControlCenterRailMode);
    } else {
        applyControlCenterRailMode();
    }
})();
