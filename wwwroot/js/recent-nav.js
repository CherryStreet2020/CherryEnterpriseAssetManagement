(function() {
    'use strict';

    var STORAGE_KEY = 'cherryai_recent';
    var MAX_ITEMS = 5;
    var EXCLUDED_PATHS = ['/Account/Login', '/Account/Logout', '/Account/AccessDenied', '/Error'];

    function init() {
        recordVisit();
        renderRecentSection();
    }

    function recordVisit() {
        var path = window.location.pathname;
        if (EXCLUDED_PATHS.indexOf(path) !== -1) return;

        var title = document.title.replace('CherryAI EAM — ', '').replace('CherryAI EAM', 'Dashboard');
        var recent = getRecent();

        recent = recent.filter(function(r) { return r.path !== path; });
        recent.unshift({ path: path, label: title, ts: Date.now() });
        if (recent.length > MAX_ITEMS) recent = recent.slice(0, MAX_ITEMS);

        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(recent));
        } catch (e) { }
    }

    function getRecent() {
        try {
            var data = localStorage.getItem(STORAGE_KEY);
            return data ? JSON.parse(data) : [];
        } catch (e) {
            return [];
        }
    }

    function renderRecentSection() {
        var featureFlag = document.cookie.indexOf('FEATURE_RECENT_NAV=true') !== -1;
        var section = document.getElementById('recentNavSection');
        var list = document.getElementById('recentNavList');
        if (!section || !list) return;

        if (!featureFlag) {
            section.style.display = 'none';
            return;
        }

        var recent = getRecent();
        if (recent.length === 0) {
            section.style.display = 'none';
            return;
        }

        section.style.display = 'block';
        var html = '';
        recent.forEach(function(r) {
            var isActive = window.location.pathname === r.path;
            html += '<a href="' + r.path + '" class="menu-item recent-item' + (isActive ? ' active' : '') + '">' +
                '<i class="fas fa-clock menu-item-icon"></i>' +
                '<span>' + escapeHtml(r.label) + '</span>' +
                '</a>';
        });
        list.innerHTML = html;
    }

    function escapeHtml(str) {
        var div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    window.recentNav = { getRecent: getRecent, recordVisit: recordVisit };
})();
