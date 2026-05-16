(function() {
    'use strict';

    var ROUTES = [
        { group: 'Overview', label: 'Dashboard', path: '/', icon: 'fa-gauge-high' },
        { group: 'Work', label: 'Requests', path: '/Maintenance/WorkRequests', icon: 'fa-inbox' },
        { group: 'Work', label: 'Work Orders', path: '/Maintenance', icon: 'fa-clipboard-list' },
        { group: 'Work', label: 'Create Work Request', path: '/Maintenance/WorkRequests/Create', icon: 'fa-plus-circle' },
        { group: 'Work', label: 'Planning & Scheduling', path: '/Maintenance/Schedules', icon: 'fa-calendar-days' },
        { group: 'Work', label: 'PM Program', path: '/Maintenance/PMTemplates', icon: 'fa-clock-rotate-left' },
        { group: 'Work', label: 'PM Assignments', path: '/Maintenance/Assignments', icon: 'fa-users' },
        { group: 'Assets', label: 'Asset Registry', path: '/Assets', icon: 'fa-list' },
        { group: 'Assets', label: 'Locations', path: '/Assets/Locations', icon: 'fa-location-dot' },
        { group: 'Assets', label: 'Transfer Asset', path: '/Assets?action=transfer', icon: 'fa-exchange-alt' },
        { group: 'Assets', label: 'Dispose Asset', path: '/Assets?action=dispose', icon: 'fa-trash-alt' },
        { group: 'Assets', label: 'Bulk Operations', path: '/BulkOperations', icon: 'fa-layer-group' },
        { group: 'Materials', label: 'Inventory (Items)', path: '/Materials/Items', icon: 'fa-box-open' },
        { group: 'Materials', label: 'Warehouses', path: '/Inventory', icon: 'fa-warehouse' },
        { group: 'Materials', label: 'Vendors', path: '/Materials/Vendors', icon: 'fa-building' },
        { group: 'Materials', label: 'Purchase Orders', path: '/Purchasing', icon: 'fa-file-invoice' },
        { group: 'Materials', label: 'Receipts', path: '/Receiving', icon: 'fa-truck-ramp-box' },
        { group: 'Materials', label: 'Invoices', path: '/AccountsPayable', icon: 'fa-file-invoice-dollar' },
        { group: 'Finance', label: 'CIP Projects', path: '/CIP', icon: 'fa-building-columns' },
        { group: 'Finance', label: 'Capitalizations', path: '/CIP/Costs', icon: 'fa-coins' },
        { group: 'Finance', label: 'Journals', path: '/Journals', icon: 'fa-book' },
        { group: 'Finance', label: 'Cost Analytics', path: '/CIP/PartyDrilldown', icon: 'fa-chart-line' },
        { group: 'Finance', label: 'Depreciation Books', path: '/Books', icon: 'fa-book-open' },
        { group: 'Finance', label: 'Reports', path: '/Reports/ReportHub', icon: 'fa-chart-bar' },
        { group: 'Finance', label: 'US Tax (MACRS/179)', path: '/UsTax', icon: 'fa-flag-usa' },
        { group: 'Finance', label: 'Canadian CCA', path: '/CCA', icon: 'fa-leaf' },
        { group: 'Finance', label: 'Report Builder', path: '/Reports/Builder', icon: 'fa-tools' },
        { group: 'Finance', label: 'Form 4562 (US)', path: '/Reports/Form4562', icon: 'fa-file-alt' },
        { group: 'Admin', label: 'Admin Hub', path: '/Admin', icon: 'fa-th-large' },
        { group: 'Admin', label: 'Organization & Sites', path: '/Admin/Sites', icon: 'fa-sitemap' },
        { group: 'Admin', label: 'Users & Roles', path: '/Admin/Users', icon: 'fa-users' },
        { group: 'Admin', label: 'Lookups', path: '/Admin/Lookups', icon: 'fa-table-list' },
        { group: 'Admin', label: 'Integrations', path: '/Admin/Integrations', icon: 'fa-plug' },
        { group: 'Admin', label: 'Company Settings', path: '/Admin/Company', icon: 'fa-building' },
        { group: 'Admin', label: 'System Settings', path: '/Admin/SystemSettings', icon: 'fa-cogs' },
        { group: 'Admin', label: 'Audit Log', path: '/Admin/AuditLog', icon: 'fa-clipboard-list' },
        { group: 'Admin', label: 'Departments', path: '/Admin/Departments', icon: 'fa-users' },
        { group: 'Admin', label: 'Cost Centers', path: '/Admin/CostCenters', icon: 'fa-coins' },
        { group: 'Admin', label: 'Asset Categories', path: '/Admin/AssetCategories', icon: 'fa-tags' },
        { group: 'Admin', label: 'Chart of Accounts', path: '/Admin/GlAccounts', icon: 'fa-calculator' },
        { group: 'Admin', label: 'Manufacturers', path: '/Admin/Manufacturers', icon: 'fa-industry' },
        { group: 'Admin', label: 'Exchange Rates', path: '/Admin/ExchangeRates', icon: 'fa-exchange-alt' },
        { group: 'Admin', label: 'PM Templates', path: '/Admin/PMTemplates', icon: 'fa-calendar-check' },
        { group: 'Tools', label: 'AI Assistant', path: '/AI', icon: 'fa-robot' },
        { group: 'Tools', label: 'API Integration', path: '/API', icon: 'fa-code' },
        { group: 'Tools', label: 'Help Center', path: '/Help', icon: 'fa-circle-question' },
    ];

    var overlay, input, resultsEl, selectedIndex;

    function init() {
        overlay = document.getElementById('commandPaletteOverlay');
        input = document.getElementById('commandPaletteInput');
        resultsEl = document.getElementById('commandPaletteResults');
        if (!overlay || !input || !resultsEl) return;

        var triggerBtn = document.getElementById('cmdPaletteTrigger');
        if (triggerBtn) {
            triggerBtn.addEventListener('click', function() { open(); });
        }

        overlay.addEventListener('click', function(e) {
            if (e.target === overlay) close();
        });

        input.addEventListener('input', function() { render(input.value); });
        input.addEventListener('keydown', handleKeydown);

        document.addEventListener('keydown', function(e) {
            if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
                e.preventDefault();
                if (overlay.style.display === 'none') open();
                else close();
            }
        });
    }

    function open() {
        overlay.style.display = 'flex';
        input.value = '';
        selectedIndex = 0;
        render('');
        setTimeout(function() { input.focus(); }, 50);
    }

    function close() {
        overlay.style.display = 'none';
        input.value = '';
    }

    function render(query) {
        var q = query.toLowerCase().trim();
        var filtered = ROUTES;
        if (q) {
            filtered = ROUTES.filter(function(r) {
                return r.label.toLowerCase().indexOf(q) !== -1 ||
                       r.group.toLowerCase().indexOf(q) !== -1 ||
                       r.path.toLowerCase().indexOf(q) !== -1;
            });
        }

        if (selectedIndex >= filtered.length) selectedIndex = Math.max(0, filtered.length - 1);

        var html = '';
        var lastGroup = '';
        filtered.forEach(function(r, i) {
            if (r.group !== lastGroup) {
                html += '<div class="command-palette-group-label">' + r.group + '</div>';
                lastGroup = r.group;
            }
            html += '<div class="command-palette-item' + (i === selectedIndex ? ' selected' : '') + '" data-index="' + i + '" data-path="' + r.path + '">' +
                '<div class="command-palette-item-icon"><i class="fas ' + r.icon + '"></i></div>' +
                '<span class="command-palette-item-label">' + r.label + '</span>' +
                '<span class="command-palette-item-path">' + r.path + '</span>' +
                '</div>';
        });

        resultsEl.innerHTML = html;

        var items = resultsEl.querySelectorAll('.command-palette-item');
        items.forEach(function(item) {
            item.addEventListener('click', function() {
                window.location.href = item.dataset.path;
            });
            item.addEventListener('mouseenter', function() {
                var idx = parseInt(item.dataset.index, 10);
                selectedIndex = idx;
                updateSelection(items);
            });
        });
    }

    function updateSelection(items) {
        items.forEach(function(item, i) {
            if (i === selectedIndex) {
                item.classList.add('selected');
                item.scrollIntoView({ block: 'nearest' });
            } else {
                item.classList.remove('selected');
            }
        });
    }

    function handleKeydown(e) {
        var items = resultsEl.querySelectorAll('.command-palette-item');
        if (e.key === 'ArrowDown') {
            e.preventDefault();
            selectedIndex = Math.min(selectedIndex + 1, items.length - 1);
            updateSelection(items);
        } else if (e.key === 'ArrowUp') {
            e.preventDefault();
            selectedIndex = Math.max(selectedIndex - 1, 0);
            updateSelection(items);
        } else if (e.key === 'Enter') {
            e.preventDefault();
            if (items[selectedIndex]) {
                window.location.href = items[selectedIndex].dataset.path;
            }
        } else if (e.key === 'Escape') {
            e.preventDefault();
            close();
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    window.commandPalette = { open: open, close: close };
})();
