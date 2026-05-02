(function() {
    'use strict';

    var SESSION_KEY_PREFIX = 'cherryai_lastlist_';

    var LIST_PAGES = [
        '/Maintenance',
        '/Maintenance/WorkRequests',
        '/Assets',
        '/Materials/Items',
        '/Inventory',
        '/Purchasing',
        '/Receiving',
        '/AccountsPayable',
        '/CIP',
        '/CIP/Costs',
        '/Journals',
        '/BulkOperations',
        '/Books',
        '/Admin/Vendors',
        '/Materials/Vendors',
        '/Admin/Locations',
        '/Assets/Locations',
        '/Admin/Sites',
        '/Admin/Users',
        '/Admin/PMTemplates',
        '/Maintenance/PMTemplates',
        '/Admin/Lookups',
        '/CIP/PartyDrilldown',
        '/Reports/ReportHub',
    ];

    var MODULE_MAP = {
        '/Maintenance': 'work',
        '/Maintenance/WorkRequests': 'work',
        '/WorkOrders': 'work',
        '/Assets': 'assets',
        '/Materials': 'materials',
        '/Inventory': 'materials',
        '/Purchasing': 'materials',
        '/Receiving': 'materials',
        '/AccountsPayable': 'materials',
        '/Admin/Vendors': 'materials',
        '/Materials/Vendors': 'materials',
        '/CIP': 'finance',
        '/Journals': 'finance',
        '/Books': 'finance',
        '/BulkOperations': 'assets',
        '/Admin/Locations': 'assets',
        '/Assets/Locations': 'assets',
        '/Admin/Sites': 'admin',
        '/Admin/Users': 'admin',
        '/Admin/Lookups': 'admin',
        '/Admin/PMTemplates': 'work',
        '/Maintenance/PMTemplates': 'work',
        '/Reports': 'reports',
    };

    function getModule(path) {
        var keys = Object.keys(MODULE_MAP);
        for (var i = 0; i < keys.length; i++) {
            if (path.indexOf(keys[i]) === 0) return MODULE_MAP[keys[i]];
        }
        return 'general';
    }

    function getOrgScope() {
        var el = document.getElementById('orgSelectorLabel');
        return el ? el.textContent.trim() : 'default';
    }

    function storeKey(module) {
        return SESSION_KEY_PREFIX + module + '_' + getOrgScope();
    }

    function storeListUrl() {
        var path = window.location.pathname;
        var isListPage = LIST_PAGES.some(function(lp) {
            return path === lp || path === lp + '/';
        });
        if (!isListPage) return;

        var module = getModule(path);
        var fullUrl = window.location.pathname + window.location.search;
        try {
            sessionStorage.setItem(storeKey(module), fullUrl);
        } catch (e) { }
    }

    function getStoredListUrl(module) {
        try {
            return sessionStorage.getItem(storeKey(module)) || null;
        } catch (e) {
            return null;
        }
    }

    function resolveBackUrl() {
        var params = new URLSearchParams(window.location.search);
        var returnUrl = params.get('returnUrl');
        if (returnUrl && returnUrl.startsWith('/')) return returnUrl;

        var path = window.location.pathname;
        var module = getModule(path);
        var stored = getStoredListUrl(module);
        if (stored) return stored;

        return null;
    }

    function init() {
        storeListUrl();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    window.returnPath = {
        resolveBackUrl: resolveBackUrl,
        getStoredListUrl: getStoredListUrl,
        getModule: getModule,
        storeListUrl: storeListUrl
    };
})();
