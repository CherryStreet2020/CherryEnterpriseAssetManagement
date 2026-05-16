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
                    // PR #116a fix: in collapsed-rail mode the submenu is
                    // display:none — toggling it does nothing visible.
                    // Instead, navigate straight to the group's first
                    // child link so the click isn't dead.
                    var sidebar = document.getElementById('mainSidebar');
                    if (sidebar && sidebar.classList.contains('collapsed')) {
                        var firstChild = group.querySelector('.menu-items a[href]');
                        if (firstChild) {
                            window.location.href = firstChild.getAttribute('href');
                            return;
                        }
                    }
                    toggleMenuGroup(group);
                });

                header.addEventListener('keydown', function(e) {
                    if (e.key === 'Enter' || e.key === ' ') {
                        e.preventDefault();
                        var sidebar = document.getElementById('mainSidebar');
                        if (sidebar && sidebar.classList.contains('collapsed')) {
                            var firstChild = group.querySelector('.menu-items a[href]');
                            if (firstChild) {
                                window.location.href = firstChild.getAttribute('href');
                                return;
                            }
                        }
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
        initSidebarUserDropdown();
    }

    // PR #116b: sidebar user dropdown (replaces the killed topbar user-btn).
    function initSidebarUserDropdown() {
        var trigger = document.getElementById('sidebarUserTrigger');
        var popover = document.getElementById('sidebarUserPopover');
        if (!trigger || !popover) return;

        function setOpen(open) {
            popover.style.display = open ? 'block' : 'none';
            trigger.setAttribute('aria-expanded', open ? 'true' : 'false');
        }

        setOpen(false);

        trigger.addEventListener('click', function(e) {
            e.stopPropagation();
            var isOpen = popover.style.display === 'block';
            setOpen(!isOpen);
        });

        // Click outside closes.
        document.addEventListener('click', function(e) {
            if (popover.style.display !== 'block') return;
            if (popover.contains(e.target) || trigger.contains(e.target)) return;
            setOpen(false);
        });

        // Escape key closes.
        document.addEventListener('keydown', function(e) {
            if (e.key === 'Escape' && popover.style.display === 'block') {
                setOpen(false);
                trigger.focus();
            }
        });
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

        // PR #116a: keep a body class in sync with the sidebar collapsed
        // state. The CSS uses :has(.sidebar.collapsed) to override
        // --sidebar-width but :has() isn't universal — body.sidebar-is-collapsed
        // gives us a defense-in-depth selector that works in every browser.
        function syncBodyClass(isCollapsed) {
            document.body.classList.toggle('sidebar-is-collapsed', isCollapsed);
        }

        var collapsed = localStorage.getItem('sidebarCollapsed') === 'true';
        if (collapsed) {
            sidebar.classList.add('collapsed');
        }
        syncBodyClass(collapsed);

        btn.addEventListener('click', function() {
            sidebar.classList.toggle('collapsed');
            var isNowCollapsed = sidebar.classList.contains('collapsed');
            localStorage.setItem('sidebarCollapsed', isNowCollapsed);
            syncBodyClass(isNowCollapsed);
            // Clear any in-flight peek state when the user manually toggles.
            sidebar.classList.remove('peek-open');
        });

        initHoverPeek(sidebar);
    }

    // PR #116a-followup: hover-peek (Linear/Notion/Stripe pattern).
    //
    // When the sidebar is in its collapsed icon-rail state, hovering on
    // the rail expands it to its full width AS AN OVERLAY — the content
    // pane does not reflow. This lets the user see all labels and the
    // full IA without un-pinning the rail. Mouse-out collapses back
    // after a short grace period so the user can drift off-target
    // without immediate snap-back.
    //
    // The pinned-collapsed flag in localStorage is preserved; peek is
    // a transient overlay only.
    function initHoverPeek(sidebar) {
        var ENTER_DELAY_MS = 140;   // brush-tolerant
        var LEAVE_GRACE_MS = 320;   // forgiving but snappy
        var enterTimer = null;
        var leaveTimer = null;

        function clearTimers() {
            if (enterTimer) { clearTimeout(enterTimer); enterTimer = null; }
            if (leaveTimer) { clearTimeout(leaveTimer); leaveTimer = null; }
        }

        function onEnter() {
            // Only peek when actually collapsed and not in mobile-open mode.
            if (!sidebar.classList.contains('collapsed')) return;
            if (sidebar.classList.contains('mobile-open')) return;
            clearTimers();
            enterTimer = setTimeout(function() {
                sidebar.classList.add('peek-open');
            }, ENTER_DELAY_MS);
        }

        function onLeave() {
            clearTimers();
            leaveTimer = setTimeout(function() {
                sidebar.classList.remove('peek-open');
            }, LEAVE_GRACE_MS);
        }

        sidebar.addEventListener('mouseenter', onEnter);
        sidebar.addEventListener('mouseleave', onLeave);

        // Keyboard accessibility: focusing any link inside the rail also
        // opens the peek so keyboard navigation isn't worse than mouse.
        sidebar.addEventListener('focusin', onEnter);
        sidebar.addEventListener('focusout', function(e) {
            // Only collapse if focus actually leaves the sidebar tree.
            if (!sidebar.contains(e.relatedTarget)) onLeave();
        });

        // Defensive: if the user clicks a nav item, the page navigates
        // and the peek state evaporates with the unload — no extra work
        // needed. If they click the collapse toggle, the click handler
        // above clears peek explicitly.
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
        // PR #116b: the always-visible topbar search input is gone.
        // `/` now opens the Cmd-K command palette instead. The palette
        // itself handles Cmd-K / Ctrl-K in command-palette.js — this
        // handler exists so users with the muscle memory for `/` aren't
        // stranded.
        document.addEventListener('keydown', function(e) {
            if (e.key !== '/' || isInputFocused()) return;
            e.preventDefault();
            var trigger = document.getElementById('cmdPaletteTrigger');
            if (trigger) trigger.click();
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
