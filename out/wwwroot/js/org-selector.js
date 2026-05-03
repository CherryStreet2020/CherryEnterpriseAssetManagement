(function() {
    const STORAGE_KEY = 'cherryai_org_node_id';
    const BOOTSTRAP_ORG_ID = 'a0000000-0000-0000-0000-000000000001';
    const btn = document.getElementById('orgSelectorBtn');
    const dropdown = document.getElementById('orgSelectorDropdown');
    const label = document.getElementById('orgSelectorLabel');
    const list = document.getElementById('orgSelectorList');
    const search = document.getElementById('orgSearchInput');

    if (!btn || !dropdown) return;

    let allNodes = [];
    let rootNodeId = null;
    let selectedNodeId = localStorage.getItem(STORAGE_KEY) || null;

    function getHeaders(orgNodeId) {
        var h = {
            'X-Tenant-Id': 'default',
            'X-User-Id': 'system@localhost',
            'Content-Type': 'application/json',
            'X-Org-Node-Id': orgNodeId || selectedNodeId || BOOTSTRAP_ORG_ID
        };
        return h;
    }

    function typeIcon(nodeType) {
        switch(nodeType) {
            case 'holding': return 'fa-building-columns';
            case 'company': return 'fa-building';
            default: return 'fa-circle';
        }
    }

    function typeColor(nodeType) {
        switch(nodeType) {
            case 'holding': return '#818cf8';
            case 'company': return '#60a5fa';
            default: return '#6b7280';
        }
    }

    function formatLabel(node) {
        if (!node) return 'All Companies';
        if (node.nodeType === 'holding') return 'All Companies';
        return node.name;
    }

    var canShowAllCompanies = true;

    function renderNodes(filter) {
        list.innerHTML = '';
        var lowerFilter = (filter || '').toLowerCase();

        if (canShowAllCompanies) {
            var resetItem = document.createElement('div');
            resetItem.className = 'org-node-item org-node-reset' + (selectedNodeId === rootNodeId ? ' selected' : '');
            resetItem.innerHTML =
                '<i class="fas fa-globe" style="color:#818cf8;margin-right:6px;font-size:11px;"></i>' +
                '<span class="org-node-name">All Companies</span>';
            resetItem.addEventListener('click', function() {
                selectNode({ id: rootNodeId, name: 'All Companies', nodeType: 'holding' });
            });
            if (!lowerFilter) {
                list.appendChild(resetItem);

                var divider = document.createElement('div');
                divider.className = 'org-selector-divider';
                list.appendChild(divider);
            }
        }

        allNodes.forEach(function(node) {
            if (node.id === rootNodeId) return;

            if (lowerFilter && !node.name.toLowerCase().includes(lowerFilter) && !(node.code || '').toLowerCase().includes(lowerFilter)) return;

            var item = document.createElement('div');
            item.className = 'org-node-item' + (node.id === selectedNodeId ? ' selected' : '');
            item.style.paddingLeft = (12 + node.indentLevel * 16) + 'px';
            item.dataset.nodeId = node.id;

            item.innerHTML =
                '<i class="fas ' + typeIcon(node.nodeType) + '" style="color:' + typeColor(node.nodeType) + ';margin-right:6px;font-size:11px;"></i>' +
                '<span class="org-node-name">' + node.name + '</span>' +
                (node.code ? '<span class="org-node-code">' + node.code + '</span>' : '');

            item.addEventListener('click', function() {
                selectNode(node);
            });

            list.appendChild(item);
        });
    }

    function selectNode(node) {
        selectedNodeId = node.id;
        localStorage.setItem(STORAGE_KEY, node.id);
        localStorage.removeItem('cherryai_site_id');
        document.cookie = 'cherryai_site_id=;path=/;max-age=0;SameSite=Lax';
        label.textContent = formatLabel(node);
        dropdown.style.display = 'none';
        renderNodes(search.value);
        window.location.reload();
    }

    function loadTree() {
        fetch('/api/v1/org/tree', { headers: getHeaders(null) })
            .then(function(r) { return r.json(); })
            .then(function(data) {
                allNodes = data.nodes || [];
                rootNodeId = data.rootId;
                canShowAllCompanies = data.showAllCompanies !== false;

                if (allNodes.length === 0) {
                    label.textContent = 'No companies';
                    return;
                }

                if (!selectedNodeId) {
                    selectedNodeId = canShowAllCompanies ? rootNodeId : (allNodes.find(function(n) { return n.nodeType !== 'holding'; }) || allNodes[0]).id;
                    localStorage.setItem(STORAGE_KEY, selectedNodeId);
                }

                var validIds = allNodes.map(function(n) { return n.id; });
                if (validIds.indexOf(selectedNodeId) === -1) {
                    selectedNodeId = canShowAllCompanies ? rootNodeId : (allNodes.find(function(n) { return n.nodeType !== 'holding'; }) || allNodes[0]).id;
                    localStorage.setItem(STORAGE_KEY, selectedNodeId);
                }

                if (!canShowAllCompanies && selectedNodeId === rootNodeId) {
                    var firstCompany = allNodes.find(function(n) { return n.nodeType !== 'holding'; });
                    if (firstCompany) {
                        selectedNodeId = firstCompany.id;
                        localStorage.setItem(STORAGE_KEY, selectedNodeId);
                    }
                }

                var selected = allNodes.find(function(n) { return n.id === selectedNodeId; });
                label.textContent = formatLabel(selected);

                renderNodes('');
            })
            .catch(function(err) {
                label.textContent = 'Error loading';
                console.error('Org tree load error:', err);
            });
    }

    btn.addEventListener('click', function(e) {
        e.stopPropagation();
        var visible = dropdown.style.display !== 'none';
        dropdown.style.display = visible ? 'none' : 'block';
        if (!visible) {
            search.value = '';
            renderNodes('');
            search.focus();
        }
    });

    search.addEventListener('input', function() {
        renderNodes(this.value);
    });

    document.addEventListener('click', function(e) {
        var wrapper = document.getElementById('orgSelectorWrapper');
        if (wrapper && !wrapper.contains(e.target)) {
            dropdown.style.display = 'none';
        }
    });

    loadTree();

    var origFetch = window.fetch;
    window.fetch = function(url, options) {
        options = options || {};
        if (typeof url === 'string' && url.startsWith('/api/')) {
            options.headers = options.headers || {};
            if (!options.headers['X-Org-Node-Id']) {
                options.headers['X-Org-Node-Id'] = selectedNodeId || BOOTSTRAP_ORG_ID;
            }
            if (!options.headers['X-Tenant-Id']) {
                options.headers['X-Tenant-Id'] = 'default';
            }
            if (!options.headers['X-User-Id']) {
                options.headers['X-User-Id'] = 'system@localhost';
            }
        }
        return origFetch.call(this, url, options);
    };
})();
