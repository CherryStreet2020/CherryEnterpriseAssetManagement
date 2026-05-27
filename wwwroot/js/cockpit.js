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
        // Real ByAsn route is /Receiving/By-Asn/{AsnId?} (hyphenated, route-segment).
        // PR #6 hotfix 2026-05-19 — caught by Dean during live-verify.
        document.getElementById('pvReceiveBtn').href = '/Receiving/By-Asn/' + encodeURIComponent(asn.num);

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
    // Sprint 12A PR #7 — selectOrphan (Orphan Queue cockpit).
    //
    // Mirrors selectAsn but reads __orphanDetails and renders a candidate-PO
    // panel instead of a manifest table. Each candidate shows its score,
    // per-signal reason chips, and a 1-click "Match" CTA that links to the
    // GET-side confirmation page /Receiving/Match-Orphan/{receiptId}/{poNumber}.
    //
    // Different DOM ids vs PO/ASN (pvReceiptNumber / pvItemPart / pvCandidateList)
    // — the orphan preview partial has no header/lines table.
    // -------------------------------------------------------------------------
    var ORPHAN_DATA = [];
    try {
        var orphanBlob = document.getElementById('__orphanDetails');
        if (orphanBlob) { ORPHAN_DATA = JSON.parse(orphanBlob.textContent); }
    } catch (e) { /* swallow — preview pane stays on welcome state */ }

    function escapeHtml(s) {
        if (s === null || s === undefined) return '';
        return String(s)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function selectOrphan(id) {
        document.querySelectorAll('.cockpit__card').forEach(function (c) { c.classList.remove('cockpit__card--active'); });
        var card = document.querySelector('.cockpit__card[data-id="' + id + '"]');
        if (card) card.classList.add('cockpit__card--active');

        var o = ORPHAN_DATA.find(function (x) { return x.id === id; });
        if (!o) return;

        var welcome = document.getElementById('mainWelcome');
        var pv = document.getElementById('mainPreview');
        if (welcome) welcome.style.display = 'none';
        if (pv) pv.style.display = 'flex';

        var receiptNumEl = document.getElementById('pvReceiptNumber');
        if (receiptNumEl) receiptNumEl.textContent = o.receiptNumber;

        // Days-aged pill — escalates tone as the orphan dwells longer.
        var pill = document.getElementById('pvDaysAgedPill');
        if (pill) {
            pill.textContent = (o.daysAged || 0) + 'd on dock';
            var pillClass = 'cockpit__preview-status status-badge-p ';
            pillClass += (o.daysAged >= 10) ? 'status-badge-p--danger'
                       : (o.daysAged >= 4)  ? 'status-badge-p--warning'
                       :                      'status-badge-p--info';
            pill.className = pillClass;
        }

        // Open-receipt CTA points at the existing edit page (Sprint 11 plumb).
        var blindBtn = document.getElementById('pvBlindReceiveBtn');
        if (blindBtn) blindBtn.href = '/Admin/StockReceipts/Edit?id=' + encodeURIComponent(o.id);

        var setText = function (elId, val) {
            var el = document.getElementById(elId);
            if (el) el.textContent = (val === null || val === undefined || val === '') ? '—' : val;
        };
        setText('pvItemPart',        o.itemPartNumber);
        setText('pvItemDesc',        o.itemDescription);
        setText('pvPreferredVendor', o.preferredVendor);
        setText('pvQuantity',        o.quantity);
        setText('pvLot',             o.lotNumber);
        setText('pvReceivedAt',      o.receivedAt);
        setText('pvDaysAged',        (o.daysAged === undefined) ? '' : (o.daysAged + ' days'));
        setText('pvOrphanNotes',     o.notes);

        var countEl = document.getElementById('pvCandidateCount');
        var candidates = o.candidates || [];
        if (countEl) countEl.textContent = candidates.length + ' candidate' + (candidates.length === 1 ? '' : 's');

        var listEl = document.getElementById('pvCandidateList');
        if (!listEl) return;

        if (candidates.length === 0) {
            listEl.innerHTML = ''
                + '<div class="cockpit__candidates-empty">'
                + '  <i class="fas fa-circle-question"></i>'
                + '  <div>'
                + '    <strong>No matches found.</strong> '
                + '    <span class="cockpit__candidates-empty-sub">Match manually from the Open Receipt page above.</span>'
                + '  </div>'
                + '</div>';
            return;
        }

        listEl.innerHTML = candidates.map(function (c, idx) {
            var scoreToneClass = c.score >= 80 ? 'cockpit__candidate-score--strong'
                               : c.score >= 40 ? 'cockpit__candidate-score--medium'
                               :                 'cockpit__candidate-score--weak';
            var topBadge = idx === 0
                ? '<span class="cockpit__candidate-best"><i class="fas fa-trophy"></i> Top match</span>'
                : '';

            var reasonChips = (c.reasons || []).map(function (r) {
                return '<span class="cockpit__candidate-reason">' + escapeHtml(r) + '</span>';
            }).join('');

            return ''
                + '<div class="cockpit__candidate">'
                + '  <div class="cockpit__candidate-head">'
                + '    <div class="cockpit__candidate-titleblock">'
                + '      ' + topBadge
                + '      <div class="cockpit__candidate-po">' + escapeHtml(c.poNumber) + '</div>'
                + '      <div class="cockpit__candidate-vendor">' + escapeHtml(c.vendor) + '</div>'
                + '    </div>'
                + '    <div class="cockpit__candidate-score ' + scoreToneClass + '">'
                + '      <span class="cockpit__candidate-score-value">' + c.score + '</span>'
                + '      <span class="cockpit__candidate-score-label">/ 100</span>'
                + '    </div>'
                + '  </div>'
                + '  <div class="cockpit__candidate-meta">'
                + '    <span><i class="fas fa-calendar"></i> Ordered ' + escapeHtml(c.orderDate) + '</span>'
                + (c.requiredDate ? '    <span><i class="fas fa-flag"></i> Need by ' + escapeHtml(c.requiredDate) + '</span>' : '')
                + '    <span class="cockpit__candidate-status">' + escapeHtml(c.status) + '</span>'
                + '  </div>'
                + '  <div class="cockpit__candidate-reasons">' + reasonChips + '</div>'
                + '  <div class="cockpit__candidate-actions">'
                + '    <a class="btn-p btn-p--primary btn-p--sm" href="' + escapeHtml(c.matchUrl) + '">'
                + '      <i class="fas fa-link"></i> Match to this PO'
                + '    </a>'
                + '  </div>'
                + '</div>';
        }).join('');
    }
    window.selectOrphan = selectOrphan;

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

    // =========================================================================
    // B8 PR-PRO-9 — Transaction Drawer (2026-05-28)
    //
    // THE single transaction drawer pattern — right-side panel on BOM/Routing
    // row click. Current line + live status + suggested action + transaction
    // form + preview + validation + history. Reusable across both grids.
    //
    // Data hydrated from window.__bomDetails / window.__opDetails JSON blobs
    // emitted by Cockpit.cshtml. Opens via CherryDS.drawer.open('txn-drawer').
    // =========================================================================

    function esc(s) { return s == null ? '—' : String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;'); }
    function fmtQty(n) { return n == null ? '—' : Number(n).toFixed(2); }
    function fmtDate(iso) { if (!iso) return '—'; var d = new Date(iso); return d.toLocaleDateString('en-US', {month:'short',day:'numeric',hour:'2-digit',minute:'2-digit'}); }

    // Status → suggested action map for BOM lines
    function bomSuggestedAction(row) {
        var s = (row.status || '').toLowerCase();
        if (s === 'short')            return { verb: 'Link Supply',     icon: 'fa-link',              tone: 'danger',  desc: 'Material is short — link a supply source (PO, WO, or inventory) to resolve.' };
        if (s === 'notrequiredyet')    return { verb: 'Not Yet Needed',  icon: 'fa-clock',             tone: 'neutral', desc: 'This component is not required at the current operation stage.' };
        if (s === 'required')          return { verb: 'Issue Material',  icon: 'fa-arrow-right-to-bracket', tone: 'brand', desc: 'Material is required and available. Issue to the production order.' };
        if (s === 'reserved')          return { verb: 'Pick Material',   icon: 'fa-hand',              tone: 'info',    desc: 'Material is reserved. Pick from source location.' };
        if (s === 'picked')            return { verb: 'Stage Material',  icon: 'fa-boxes-stacked',     tone: 'info',    desc: 'Material is picked. Stage at point of use.' };
        if (s === 'staged')            return { verb: 'Issue Material',  icon: 'fa-arrow-right-to-bracket', tone: 'brand', desc: 'Material is staged at POU. Issue to consume.' };
        if (s === 'partiallyissued')   return { verb: 'Issue Remaining', icon: 'fa-arrow-right-to-bracket', tone: 'warning', desc: 'Partially issued. Issue remaining ' + fmtQty(row.remainingToIssue) + ' ' + (row.uom||'EA') + '.' };
        if (s === 'issued')            return { verb: 'Fully Issued',    icon: 'fa-check',             tone: 'success', desc: 'All material issued. Ready for consumption.' };
        if (s === 'overissued')        return { verb: 'Return Excess',   icon: 'fa-rotate-left',       tone: 'warning', desc: 'Over-issued — return excess material to inventory.' };
        if (s === 'consumed')          return { verb: 'Complete',        icon: 'fa-circle-check',      tone: 'success', desc: 'Material consumed. Line complete.' };
        if (s === 'returned')          return { verb: 'Re-Issue',        icon: 'fa-arrow-right-to-bracket', tone: 'info', desc: 'Material was returned. Re-issue if still needed.' };
        if (s === 'substituted')       return { verb: 'Review',          icon: 'fa-arrows-rotate',     tone: 'info',    desc: 'Substituted with alternate component.' };
        if (s === 'cancelled')         return { verb: 'Cancelled',       icon: 'fa-ban',               tone: 'neutral', desc: 'BOM line cancelled.' };
        return { verb: 'Review', icon: 'fa-eye', tone: 'neutral', desc: 'Review this BOM line.' };
    }

    // Status → suggested action map for Operations
    function opSuggestedAction(row) {
        var s = (row.status || '').toLowerCase();
        if (s === 'scheduled')    return { verb: 'Release',          icon: 'fa-lock-open',         tone: 'info',    desc: 'Operation is scheduled. Release to make available for work.' };
        if (s === 'released')     return { verb: 'Start Setup',      icon: 'fa-play',              tone: 'brand',   desc: 'Operation released. Begin setup phase.' };
        if (s === 'insetup')      return { verb: 'Complete Setup',   icon: 'fa-forward-step',      tone: 'brand',   desc: 'Setup in progress. Complete setup to begin production run.' };
        if (s === 'running')      return { verb: 'Complete',         icon: 'fa-flag-checkered',    tone: 'success', desc: 'Operation running. Complete when output is ready.' };
        if (s === 'paused')       return { verb: 'Resume',           icon: 'fa-play',              tone: 'warning', desc: 'Operation paused. Resume to continue production.' };
        if (s === 'completed')    return { verb: 'Review Results',   icon: 'fa-clipboard-check',   tone: 'success', desc: 'Operation complete. Review output quantities and quality.' };
        if (s === 'skipped')      return { verb: 'Re-activate',      icon: 'fa-rotate-left',       tone: 'neutral', desc: 'Operation was skipped. Re-activate if needed.' };
        return { verb: 'Review', icon: 'fa-eye', tone: 'neutral', desc: 'Review this operation.' };
    }

    // Build the BOM line drawer body HTML
    function buildBomDrawerBody(row) {
        var action = bomSuggestedAction(row);
        var toneColor = action.tone === 'danger' ? 'var(--v2-status-cancelled, #ef4444)'
            : action.tone === 'warning' ? 'var(--v2-status-onhold, #f59e0b)'
            : action.tone === 'success' ? 'var(--v2-status-completed, #22c55e)'
            : action.tone === 'brand' ? 'var(--v2-brand, #cf3339)'
            : 'var(--v2-text-muted, #94a3b8)';

        return '<div class="txn-section">'
            + '<div class="txn-action-banner" style="border-left: 3px solid ' + toneColor + ';">'
            + '<div class="txn-action-verb"><i class="fas ' + action.icon + '"></i> ' + esc(action.verb) + '</div>'
            + '<div class="txn-action-desc">' + esc(action.desc) + '</div>'
            + '</div>'
            + '</div>'

            + '<div class="txn-section"><h4 class="txn-section-title">Line Detail</h4>'
            + '<div class="txn-detail-grid">'
            + '<div class="txn-kv"><span class="txn-k">Part #</span><span class="txn-v mono">' + esc(row.partNumber) + '</span></div>'
            + '<div class="txn-kv"><span class="txn-k">Rev</span><span class="txn-v">' + esc(row.revision) + '</span></div>'
            + '<div class="txn-kv"><span class="txn-k">Description</span><span class="txn-v">' + esc(row.description) + '</span></div>'
            + '<div class="txn-kv"><span class="txn-k">Status</span><span class="txn-v"><span class="ds-pill" data-tone="' + (action.tone === 'brand' ? 'info' : action.tone) + '">' + esc(row.status) + '</span></span></div>'
            + '<div class="txn-kv"><span class="txn-k">Supply Type</span><span class="txn-v">' + esc(row.supplyType) + '</span></div>'
            + '<div class="txn-kv"><span class="txn-k">Supply Risk</span><span class="txn-v" style="color: ' + (row.supplyRisk === 'Critical' ? 'var(--v2-status-cancelled)' : row.supplyRisk === 'Warning' ? 'var(--v2-status-onhold)' : 'var(--v2-status-completed)') + '; font-weight: 600;">' + esc(row.supplyRisk) + '</span></div>'
            + '<div class="txn-kv"><span class="txn-k">Supply Link</span><span class="txn-v">' + esc(row.supplyLinkDescription) + '</span></div>'
            + '<div class="txn-kv"><span class="txn-k">Op Sequence</span><span class="txn-v mono">' + esc(row.operationSequence) + '</span></div>'
            + '</div></div>'

            + '<div class="txn-section"><h4 class="txn-section-title">Quantities</h4>'
            + '<div class="txn-qty-grid">'
            + buildQtyBar('Required',  row.requiredQty, 'Required (incl scrap): ' + fmtQty(row.requiredInclScrap))
            + buildQtyBar('Available',  row.available)
            + buildQtyBar('Reserved',   row.reserved)
            + buildQtyBar('Picked',     row.picked)
            + buildQtyBar('Staged',     row.staged)
            + buildQtyBar('Issued',     row.issued)
            + buildQtyBar('Consumed',   row.consumed)
            + buildQtyBar('Remaining',  row.remainingToIssue, null, row.remainingToIssue > 0 ? 'warning' : 'success')
            + buildQtyBar('Short',      row.short, null, row.short > 0 ? 'danger' : null)
            + '</div></div>'

            + '<div class="txn-section"><h4 class="txn-section-title">Cost</h4>'
            + '<div class="txn-detail-grid">'
            + '<div class="txn-kv"><span class="txn-k">Unit Cost</span><span class="txn-v mono">$' + fmtQty(row.cost) + '</span></div>'
            + '<div class="txn-kv"><span class="txn-k">Extended</span><span class="txn-v mono">$' + fmtQty(row.cost * row.requiredQty) + '</span></div>'
            + '<div class="txn-kv"><span class="txn-k">Backflush</span><span class="txn-v">' + (row.backflush ? 'Yes' : 'No') + '</span></div>'
            + '<div class="txn-kv"><span class="txn-k">Substitute OK</span><span class="txn-v">' + (row.substituteAllowed ? 'Yes' : 'No') + '</span></div>'
            + '</div></div>'

            + '<div class="txn-section"><h4 class="txn-section-title">Transaction History</h4>'
            + '<div class="txn-history-placeholder"><i class="fas fa-clock-rotate-left"></i> Transaction log wires in PR-PRO-12 reporting service.</div>'
            + '</div>';
    }

    function buildQtyBar(label, value, tooltip, tone) {
        var t = tone ? ' txn-qty--' + tone : '';
        return '<div class="txn-qty-row' + t + '"' + (tooltip ? ' title="' + tooltip + '"' : '') + '>'
            + '<span class="txn-qty-label">' + label + '</span>'
            + '<span class="txn-qty-value mono">' + fmtQty(value) + '</span>'
            + '</div>';
    }

    // Build the Operation drawer body HTML
    function buildOpDrawerBody(row) {
        var action = opSuggestedAction(row);
        var toneColor = action.tone === 'danger' ? 'var(--v2-status-cancelled, #ef4444)'
            : action.tone === 'warning' ? 'var(--v2-status-onhold, #f59e0b)'
            : action.tone === 'success' ? 'var(--v2-status-completed, #22c55e)'
            : action.tone === 'brand' ? 'var(--v2-brand, #cf3339)'
            : 'var(--v2-text-muted, #94a3b8)';

        var readinessColor = row.readinessStatus === 'Pass' ? 'var(--v2-status-completed, #22c55e)'
            : row.readinessStatus === 'Warning' ? 'var(--v2-status-onhold, #f59e0b)'
            : row.readinessStatus === 'Fail' ? 'var(--v2-status-cancelled, #ef4444)'
            : 'var(--v2-text-muted, #94a3b8)';

        return '<div class="txn-section">'
            + '<div class="txn-action-banner" style="border-left: 3px solid ' + toneColor + ';">'
            + '<div class="txn-action-verb"><i class="fas ' + action.icon + '"></i> ' + esc(action.verb) + '</div>'
            + '<div class="txn-action-desc">' + esc(action.desc) + '</div>'
            + '</div>'
            + '</div>'

            + '<div class="txn-section"><h4 class="txn-section-title">Operation Detail</h4>'
            + '<div class="txn-detail-grid">'
            + '<div class="txn-kv"><span class="txn-k">Sequence</span><span class="txn-v mono">' + esc(row.sequence) + '</span></div>'
            + '<div class="txn-kv"><span class="txn-k">Code</span><span class="txn-v mono">' + esc(row.operationCode) + '</span></div>'
            + '<div class="txn-kv"><span class="txn-k">Description</span><span class="txn-v">' + esc(row.description) + '</span></div>'
            + '<div class="txn-kv"><span class="txn-k">Work Center</span><span class="txn-v">' + esc(row.workCenterName) + '</span></div>'
            + '<div class="txn-kv"><span class="txn-k">Status</span><span class="txn-v"><span class="ds-pill" data-tone="' + (action.tone === 'brand' ? 'info' : action.tone) + '">' + esc(row.status) + '</span></span></div>'
            + '<div class="txn-kv"><span class="txn-k">Readiness</span><span class="txn-v" style="color: ' + readinessColor + '; font-weight: 600;">' + esc(row.readinessStatus) + (row.readinessSummary ? ' — ' + esc(row.readinessSummary) : '') + '</span></div>'
            + '<div class="txn-kv"><span class="txn-k">Material Ready</span><span class="txn-v" style="color: ' + (row.materialReady === 'Pass' ? 'var(--v2-status-completed)' : row.materialReady === 'Fail' ? 'var(--v2-status-cancelled)' : 'var(--v2-status-onhold)') + '; font-weight: 600;">' + esc(row.materialReady) + '</span></div>'
            + '</div></div>'

            + '<div class="txn-section"><h4 class="txn-section-title">Schedule</h4>'
            + '<div class="txn-detail-grid">'
            + '<div class="txn-kv"><span class="txn-k">Planned Start</span><span class="txn-v">' + fmtDate(row.plannedStart) + '</span></div>'
            + '<div class="txn-kv"><span class="txn-k">Planned End</span><span class="txn-v">' + fmtDate(row.plannedFinish) + '</span></div>'
            + '<div class="txn-kv"><span class="txn-k">Actual Start</span><span class="txn-v">' + fmtDate(row.actualStart) + '</span></div>'
            + '<div class="txn-kv"><span class="txn-k">Actual End</span><span class="txn-v">' + fmtDate(row.actualFinish) + '</span></div>'
            + '</div></div>'

            + '<div class="txn-section"><h4 class="txn-section-title">Time &amp; Output</h4>'
            + '<div class="txn-qty-grid">'
            + buildQtyBar('Setup Est',  row.setupEstimate, 'Estimated setup time (min)')
            + buildQtyBar('Setup Act',  row.setupActual, 'Actual setup time (min)', row.setupActual > row.setupEstimate * 1.1 ? 'warning' : null)
            + buildQtyBar('Run Est',    row.runEstimate, 'Estimated run time (min)')
            + buildQtyBar('Run Act',    row.runActual, 'Actual run time (min)', row.runActual > row.runEstimate * 1.1 ? 'warning' : null)
            + buildQtyBar('Labor Est',  row.laborEstimate, 'Estimated labor (min)')
            + buildQtyBar('Labor Act',  row.laborActual, 'Actual labor (min)')
            + '</div>'
            + '<div class="txn-qty-grid" style="margin-top: 0.75rem;">'
            + buildQtyBar('Good',     row.goodQty, null, 'success')
            + buildQtyBar('Scrap',    row.scrapQty, null, row.scrapQty > 0 ? 'danger' : null)
            + buildQtyBar('Rework',   row.reworkQty, null, row.reworkQty > 0 ? 'warning' : null)
            + buildQtyBar('Remaining', row.remainingQty)
            + '</div></div>'

            + '<div class="txn-section"><h4 class="txn-section-title">Transaction History</h4>'
            + '<div class="txn-history-placeholder"><i class="fas fa-clock-rotate-left"></i> Transaction log wires in PR-PRO-12 reporting service.</div>'
            + '</div>';
    }

    // Build footer with context-aware action buttons
    function buildBomDrawerFooter(row) {
        var action = bomSuggestedAction(row);
        return '<button class="ds-btn ds-btn--primary ds-btn--md" disabled>'
             + '<i class="fas ' + action.icon + '"></i> ' + esc(action.verb)
             + '</button>'
             + '<span class="txn-footer-note">Transaction forms wire in PR-PRO-11 validation pipeline.</span>';
    }

    function buildOpDrawerFooter(row) {
        var action = opSuggestedAction(row);
        return '<button class="ds-btn ds-btn--primary ds-btn--md" disabled>'
             + '<i class="fas ' + action.icon + '"></i> ' + esc(action.verb)
             + '</button>'
             + '<span class="txn-footer-note">Transaction forms wire in PR-PRO-11 validation pipeline.</span>';
    }

    // Open the transaction drawer for a BOM row
    function selectBomLine(bomId) {
        var data = (window.__bomDetails || []).find(function(b) { return b.id === bomId; });
        if (!data) return;
        document.getElementById('txn-drawer-title').textContent = data.partNumber + (data.revision ? ' Rev ' + data.revision : '');
        document.getElementById('txn-drawer-subtitle').textContent = 'BOM Line ' + data.line + ' — ' + (data.description || '');
        document.getElementById('txn-drawer-body').innerHTML = buildBomDrawerBody(data);
        document.getElementById('txn-drawer-foot').innerHTML = buildBomDrawerFooter(data);
        highlightRow('bom-grid', bomId, 'data-bom-id');
        if (window.CherryDS && window.CherryDS.drawer) window.CherryDS.drawer.open('txn-drawer');
    }

    // Open the transaction drawer for a Routing row
    function selectOperation(opId) {
        var data = (window.__opDetails || []).find(function(o) { return o.id === opId; });
        if (!data) return;
        document.getElementById('txn-drawer-title').textContent = 'Op ' + data.sequence + ' — ' + (data.operationCode || '');
        document.getElementById('txn-drawer-subtitle').textContent = data.description + (data.workCenterName ? ' · ' + data.workCenterName : '');
        document.getElementById('txn-drawer-body').innerHTML = buildOpDrawerBody(data);
        document.getElementById('txn-drawer-foot').innerHTML = buildOpDrawerFooter(data);
        highlightRow('routing-grid', opId, 'data-op-id');
        if (window.CherryDS && window.CherryDS.drawer) window.CherryDS.drawer.open('txn-drawer');
    }

    // Highlight the selected row
    function highlightRow(gridId, rowId, attr) {
        var grid = document.getElementById(gridId);
        if (!grid) return;
        grid.querySelectorAll('tr.txn-row--selected').forEach(function(tr) { tr.classList.remove('txn-row--selected'); });
        var row = grid.querySelector('tr[' + attr + '="' + rowId + '"]');
        if (row) row.classList.add('txn-row--selected');
    }

    // Wire row clicks on BOM and Routing grids
    function wireTxnDrawer() {
        document.querySelectorAll('#bom-grid tr[data-bom-id]').forEach(function(tr) {
            tr.addEventListener('click', function() {
                selectBomLine(parseInt(this.getAttribute('data-bom-id'), 10));
            });
        });
        document.querySelectorAll('#routing-grid tr[data-op-id]').forEach(function(tr) {
            tr.addEventListener('click', function() {
                selectOperation(parseInt(this.getAttribute('data-op-id'), 10));
            });
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', wireTxnDrawer);
    } else {
        wireTxnDrawer();
    }
})();
