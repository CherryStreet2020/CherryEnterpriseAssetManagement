(function() {
    var STORAGE_KEY = 'cherryai_site_id';
    var wrapper = document.getElementById('siteSelectorWrapper');
    var btn = document.getElementById('siteSelectorBtn');
    var dropdown = document.getElementById('siteSelectorDropdown');
    var label = document.getElementById('siteSelectorLabel');
    var list = document.getElementById('siteSelectorList');

    if (!btn || !dropdown || !wrapper) return;

    var allSites = [];
    var selectedSiteId = localStorage.getItem(STORAGE_KEY) || null;
    if (selectedSiteId) selectedSiteId = parseInt(selectedSiteId) || null;
    var canShowAllSites = true;
    var lastCompanyNodeId = null;

    function getHeaders() {
        var orgNodeId = localStorage.getItem('cherryai_org_node_id') || 'a0000000-0000-0000-0000-000000000001';
        return {
            'X-Tenant-Id': 'default',
            'X-User-Id': 'system@localhost',
            'Content-Type': 'application/json',
            'X-Org-Node-Id': orgNodeId
        };
    }

    function renderSites() {
        list.innerHTML = '';

        if (canShowAllSites) {
            var resetItem = document.createElement('div');
            resetItem.className = 'site-node-item site-node-reset' + (!selectedSiteId ? ' selected' : '');
            resetItem.innerHTML =
                '<i class="fas fa-globe" style="color:#818cf8;margin-right:6px;font-size:11px;"></i>' +
                '<span class="site-node-name">All Sites</span>';
            resetItem.addEventListener('click', function() {
                selectSite(null);
            });
            list.appendChild(resetItem);

            if (allSites.length > 0) {
                var divider = document.createElement('div');
                divider.className = 'site-selector-divider';
                list.appendChild(divider);
            }
        }

        allSites.forEach(function(site) {
            var item = document.createElement('div');
            item.className = 'site-node-item' + (selectedSiteId === site.id ? ' selected' : '');
            item.innerHTML =
                '<i class="fas fa-map-marker-alt" style="color:#60a5fa;margin-right:6px;font-size:11px;"></i>' +
                '<span class="site-node-name">' + site.name + '</span>' +
                '<span class="site-node-code">' + site.siteCode + '</span>';
            item.addEventListener('click', function() {
                selectSite(site);
            });
            list.appendChild(item);
        });
    }

    function setSiteCookie(siteId) {
        if (siteId) {
            document.cookie = 'cherryai_site_id=' + siteId + ';path=/;max-age=31536000;SameSite=Lax';
        } else {
            document.cookie = 'cherryai_site_id=;path=/;max-age=0;SameSite=Lax';
        }
    }

    function selectSite(site) {
        if (site) {
            selectedSiteId = site.id;
            localStorage.setItem(STORAGE_KEY, site.id);
            setSiteCookie(site.id);
            label.textContent = site.name;
        } else {
            selectedSiteId = null;
            localStorage.removeItem(STORAGE_KEY);
            setSiteCookie(null);
            label.textContent = 'All Sites';
        }
        dropdown.style.display = 'none';
        window.location.reload();
    }

    function loadSites() {
        fetch('/api/v1/org/sites', { headers: getHeaders() })
            .then(function(r) { return r.json(); })
            .then(function(data) {
                allSites = data.sites || [];
                canShowAllSites = data.showAllSites !== false;

                if (allSites.length <= 1 && canShowAllSites) {
                    wrapper.style.display = 'none';
                    if (allSites.length === 1 && !canShowAllSites) {
                        selectedSiteId = allSites[0].id;
                        localStorage.setItem(STORAGE_KEY, selectedSiteId);
                    }
                    return;
                }

                wrapper.style.display = '';

                if (selectedSiteId) {
                    var validIds = allSites.map(function(s) { return s.id; });
                    if (validIds.indexOf(selectedSiteId) === -1) {
                        selectedSiteId = null;
                        localStorage.removeItem(STORAGE_KEY);
                    }
                }

                if (!canShowAllSites && !selectedSiteId && allSites.length > 0) {
                    selectedSiteId = allSites[0].id;
                    localStorage.setItem(STORAGE_KEY, selectedSiteId);
                }

                if (selectedSiteId) {
                    var selected = allSites.find(function(s) { return s.id === selectedSiteId; });
                    label.textContent = selected ? selected.name : 'All Sites';
                } else {
                    label.textContent = 'All Sites';
                }

                renderSites();
            })
            .catch(function(err) {
                console.error('Site selector load error:', err);
                wrapper.style.display = 'none';
            });
    }

    btn.addEventListener('click', function(e) {
        e.stopPropagation();
        var visible = dropdown.style.display !== 'none';
        dropdown.style.display = visible ? 'none' : 'block';
        if (!visible) {
            renderSites();
        }
    });

    document.addEventListener('click', function(e) {
        if (wrapper && !wrapper.contains(e.target)) {
            dropdown.style.display = 'none';
        }
    });

    var origOrgSelectNode = null;
    var orgSelectorLabel = document.getElementById('orgSelectorLabel');
    if (orgSelectorLabel) {
        var observer = new MutationObserver(function() {
            var currentOrgNodeId = localStorage.getItem('cherryai_org_node_id');
            if (currentOrgNodeId !== lastCompanyNodeId) {
                lastCompanyNodeId = currentOrgNodeId;
                selectedSiteId = null;
                localStorage.removeItem(STORAGE_KEY);
                setSiteCookie(null);
                loadSites();
            }
        });
        observer.observe(orgSelectorLabel, { childList: true, characterData: true, subtree: true });
    }

    lastCompanyNodeId = localStorage.getItem('cherryai_org_node_id');

    if (selectedSiteId) {
        setSiteCookie(selectedSiteId);
    }

    loadSites();
})();
