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
})();
