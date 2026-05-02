(function() {
    'use strict';

    function initSidebarNav() {
        var menuGroups = document.querySelectorAll('.menu-group');
        var savedState = JSON.parse(localStorage.getItem('sidebarState') || '{}');

        menuGroups.forEach(function(group) {
            var header = group.querySelector('.menu-group-header');
            var section = group.dataset.section;
            
            if (header) {
                if (savedState[section] === true) {
                    group.classList.add('expanded');
                }

                var activeItem = group.querySelector('.menu-item.active');
                if (activeItem) {
                    group.classList.add('expanded');
                }

                header.addEventListener('click', function(e) {
                    e.preventDefault();
                    toggleMenuGroup(group);
                });

                header.addEventListener('keydown', function(e) {
                    if (e.key === 'Enter' || e.key === ' ') {
                        e.preventDefault();
                        toggleMenuGroup(group);
                    }
                });

                header.setAttribute('tabindex', '0');
                header.setAttribute('role', 'button');
                header.setAttribute('aria-expanded', group.classList.contains('expanded'));
            }
        });

        highlightActiveItem();
        initCollapseToggle();
        initMobileMenu();
        initSearchShortcut();
    }

    function toggleMenuGroup(group) {
        var isExpanded = group.classList.contains('expanded');
        var section = group.dataset.section;
        var header = group.querySelector('.menu-group-header');
        
        group.classList.toggle('expanded');
        
        if (header) {
            header.setAttribute('aria-expanded', !isExpanded);
        }

        var savedState = JSON.parse(localStorage.getItem('sidebarState') || '{}');
        savedState[section] = !isExpanded;
        localStorage.setItem('sidebarState', JSON.stringify(savedState));
    }

    function highlightActiveItem() {
        var currentPath = window.location.pathname.toLowerCase();
        var menuItems = document.querySelectorAll('.menu-item, .nav-item');
        
        menuItems.forEach(function(item) {
            var href = item.getAttribute('href');
            if (href) {
                var itemPath = href.split('?')[0].toLowerCase();
                if (currentPath === itemPath || 
                    (itemPath !== '/' && currentPath.startsWith(itemPath))) {
                } else {
                    item.classList.remove('active');
                }
            }
        });
    }

    function initCollapseToggle() {
        var btn = document.getElementById('sidebarCollapseBtn');
        var sidebar = document.getElementById('mainSidebar');
        if (!btn || !sidebar) return;

        var collapsed = localStorage.getItem('sidebarCollapsed') === 'true';
        if (collapsed) {
            sidebar.classList.add('collapsed');
        }

        btn.addEventListener('click', function() {
            sidebar.classList.toggle('collapsed');
            var isNowCollapsed = sidebar.classList.contains('collapsed');
            localStorage.setItem('sidebarCollapsed', isNowCollapsed);
        });
    }

    function initMobileMenu() {
        var btn = document.getElementById('mobileMenuBtn');
        var sidebar = document.getElementById('mainSidebar');
        var overlay = document.getElementById('sidebarOverlay');
        if (!btn || !sidebar) return;

        btn.addEventListener('click', function() {
            sidebar.classList.toggle('mobile-open');
            if (overlay) overlay.classList.toggle('active');
        });

        if (overlay) {
            overlay.addEventListener('click', function() {
                sidebar.classList.remove('mobile-open');
                overlay.classList.remove('active');
            });
        }
    }

    function initSearchShortcut() {
        var searchInput = document.getElementById('globalSearchInput');
        if (!searchInput) return;

        document.addEventListener('keydown', function(e) {
            if (e.key === '/' && !isInputFocused()) {
                e.preventDefault();
                searchInput.focus();
            }
        });

        searchInput.addEventListener('keydown', function(e) {
            if (e.key === 'Escape') {
                searchInput.value = '';
                searchInput.blur();
            }
        });
    }

    function isInputFocused() {
        var active = document.activeElement;
        if (!active) return false;
        var tag = active.tagName.toLowerCase();
        return tag === 'input' || tag === 'textarea' || tag === 'select' || active.isContentEditable;
    }

    function expandAllGroups() {
        document.querySelectorAll('.menu-group').forEach(function(group) {
            group.classList.add('expanded');
            var header = group.querySelector('.menu-group-header');
            if (header) header.setAttribute('aria-expanded', 'true');
        });
    }

    function collapseAllGroups() {
        document.querySelectorAll('.menu-group').forEach(function(group) {
            group.classList.remove('expanded');
            var header = group.querySelector('.menu-group-header');
            if (header) header.setAttribute('aria-expanded', 'false');
        });
    }

    window.sidebarNav = {
        init: initSidebarNav,
        toggle: toggleMenuGroup,
        expandAll: expandAllGroups,
        collapseAll: collapseAllGroups
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initSidebarNav);
    } else {
        initSidebarNav();
    }
})();
