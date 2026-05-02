(function() {
    'use strict';

    var STORAGE_KEY = 'cherryai_theme';

    function getPreferred() {
        if (document.documentElement.classList.contains('dark')) return 'dark';
        var saved = localStorage.getItem(STORAGE_KEY);
        if (saved === 'dark' || saved === 'light') return saved;
        if (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) return 'dark';
        return 'light';
    }

    function apply(theme) {
        if (theme === 'dark') {
            document.documentElement.classList.add('dark');
        } else {
            document.documentElement.classList.remove('dark');
        }
        localStorage.setItem(STORAGE_KEY, theme);
        updateIcon(theme);
    }

    function updateIcon(theme) {
        var btn = document.getElementById('themeToggleBtn');
        if (!btn) return;
        var icon = btn.querySelector('i');
        if (!icon) return;
        if (theme === 'dark') {
            icon.className = 'fas fa-sun';
            btn.title = 'Switch to light mode';
            btn.setAttribute('aria-label', 'Switch to light mode');
            btn.setAttribute('aria-pressed', 'true');
        } else {
            icon.className = 'fas fa-moon';
            btn.title = 'Switch to dark mode';
            btn.setAttribute('aria-label', 'Switch to dark mode');
            btn.setAttribute('aria-pressed', 'false');
        }
    }

    function toggle() {
        var current = document.documentElement.classList.contains('dark') ? 'dark' : 'light';
        apply(current === 'dark' ? 'light' : 'dark');
    }

    apply(getPreferred());

    document.addEventListener('DOMContentLoaded', function() {
        var current = document.documentElement.classList.contains('dark') ? 'dark' : 'light';
        updateIcon(current);

        var btn = document.getElementById('themeToggleBtn');
        if (btn) {
            btn.addEventListener('click', toggle);
        }
    });
})();
