/**
 * Unified Tab System (ADR-007)
 * Centralized tab navigation behavior for all pages using _TabNav partial.
 * 
 * Loaded by: Pages/Shared/_ModernLayout.cshtml
 * CSS: wwwroot/css/modules/tabs.css
 * 
 * Features:
 * - Button mode: role="tablist"/"tab" + aria-selected + tabindex roving
 * - URL sync: data-url-param="tab" syncs active tab to ?tab=xxx query param
 * - Panel hiding: sets hidden attribute on inactive panels
 */
(function() {
    'use strict';

    function initTabNav(tabNav) {
        const tabs = tabNav.querySelectorAll('.tab-nav__item');
        if (!tabs.length) return;

        const urlParam = tabNav.dataset.urlParam;
        if (urlParam) {
            const params = new URLSearchParams(window.location.search);
            const activeKey = params.get(urlParam);
            if (activeKey) {
                const targetTab = tabNav.querySelector('[data-tab="' + activeKey + '"]');
                if (targetTab) {
                    activateTab(tabNav, targetTab, false);
                }
            }
        }

        tabs.forEach(function(tab) {
            tab.addEventListener('click', function(e) {
                if (tab.tagName === 'A') return;
                e.preventDefault();
                activateTab(tabNav, tab, true);
            });

            tab.addEventListener('keydown', function(e) {
                handleKeyboard(tabNav, tabs, tab, e);
            });
        });
    }

    function activateTab(tabNav, activeTab, updateUrl) {
        const tabs = tabNav.querySelectorAll('.tab-nav__item');
        
        tabs.forEach(function(tab) {
            const isActive = tab === activeTab;
            tab.classList.toggle('tab-nav__item--active', isActive);
            tab.setAttribute('aria-selected', isActive ? 'true' : 'false');
            tab.setAttribute('tabindex', isActive ? '0' : '-1');
            
            const tabPanelId = tab.getAttribute('aria-controls') 
                || ('panel-' + tab.dataset.tab);
            var mainPanel = document.getElementById(tabPanelId);
            var linkedPanels = document.querySelectorAll('[data-linked-panel="' + tabPanelId + '"]');
            var allPanels = mainPanel ? [mainPanel] : [];
            linkedPanels.forEach(function(p) { allPanels.push(p); });
            allPanels.forEach(function(panel) {
                if (isActive) {
                    panel.classList.add('active');
                    panel.removeAttribute('hidden');
                } else {
                    panel.classList.remove('active');
                    panel.setAttribute('hidden', '');
                }
            });
        });

        if (updateUrl) {
            const urlParam = tabNav.dataset.urlParam;
            if (urlParam && activeTab.dataset.tab) {
                const params = new URLSearchParams(window.location.search);
                params.set(urlParam, activeTab.dataset.tab);
                const newUrl = window.location.pathname + '?' + params.toString();
                history.replaceState(null, '', newUrl);
            }
        }
    }

    function handleKeyboard(tabNav, tabs, currentTab, e) {
        const tabArray = Array.from(tabs);
        const currentIndex = tabArray.indexOf(currentTab);
        let newIndex = currentIndex;

        switch (e.key) {
            case 'ArrowLeft':
                newIndex = currentIndex > 0 ? currentIndex - 1 : tabArray.length - 1;
                break;
            case 'ArrowRight':
                newIndex = currentIndex < tabArray.length - 1 ? currentIndex + 1 : 0;
                break;
            case 'Home':
                newIndex = 0;
                break;
            case 'End':
                newIndex = tabArray.length - 1;
                break;
            default:
                return;
        }

        e.preventDefault();
        const newTab = tabArray[newIndex];
        newTab.focus();
        activateTab(tabNav, newTab);
    }

    function init() {
        var tabNavs = document.querySelectorAll('.tab-nav[role="tablist"]');
        tabNavs.forEach(initTabNav);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
