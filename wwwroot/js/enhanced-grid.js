/**
 * CherryAI Premium DataGrid v3.0
 * Features: Global Search, Multi-column Sort (max 3), Per-column Filters (text/select),
 *           Column Visibility, Export CSV/Excel (.xlsx), State Persistence, Sticky Headers,
 *           Row Click Navigation with routing-safe server URLs, Reset View
 * 
 * DataGrid Contract v3.0:
 * ─────────────────────────────────────────────────────────────────────────────
 * TABLE ATTRIBUTES:
 *   id="<stableId>"                      - Required for state persistence
 *   data-enhanced-grid="true"            - Enables premium features
 *   class="data-table"                   - Standard styling
 *   data-row-click="true"                - Enables row navigation
 * 
 * ROW ATTRIBUTES (on <tr> in tbody):
 *   data-row-id="<recordId>"             - Entity ID (for reference/export)
 *   data-row-href="<url>"                - REQUIRED for row click: Server-generated 
 *                                          routing-safe URL via Url.Page()
 * 
 * COLUMN ATTRIBUTES (on <th>):
 *   data-col="ColumnName"                - Display name in filter dropdown
 *   data-filter="text|select|number|date|none" - Filter type
 *   data-filter-options="Opt1|Opt2|..."  - Options for select filter
 *   data-export="false"                  - Exclude from export
 *   class="no-sort"                      - Disable sorting
 * 
 * NON-NAVIGATING ELEMENTS:
 *   data-no-row-nav                      - Prevents row navigation when clicked
 * 
 * KEYBOARD ACCESSIBILITY:
 *   - Tab to focus rows, Enter/Space to navigate
 *   - Ctrl/Cmd+Click or Ctrl/Cmd+Enter opens in new tab
 * 
 * DEPRECATED (removed in v3.0 - do not use):
 *   data-row-click-page, data-row-click-param, data-row-click-returnurl
 *   (Client-side URL building is brittle for route-segment pages)
 * ─────────────────────────────────────────────────────────────────────────────
 */

class EnhancedGrid {
    constructor(tableId, options = {}) {
        this.table = document.getElementById(tableId);
        if (!this.table) {
            console.warn(`EnhancedGrid: Table #${tableId} not found`);
            return;
        }
        
        if (this.table._enhancedGridInitialized) {
            // Already initialized — bail silently. Most grid pages instantiate
            // twice (auto-init from this file + explicit `new EnhancedGrid()` in
            // a page script). The guard is intentional; the warn was noise.
            return;
        }
        this.table._enhancedGridInitialized = true;
        
        this.tableId = tableId;
        this.storageKey = `gridState:${window.location.pathname}:${tableId}`;
        
        const tableOptions = this.parseTableOptions();
        this.options = {
            searchable: true,
            sortable: true,
            multiSort: true,
            maxSortColumns: 3,
            exportable: true,
            exportExcel: true,
            columnVisibility: true,
            columnFilters: true,
            stickyHeader: true,
            persistState: true,
            rowClick: tableOptions.rowClick,
            rowClickPage: tableOptions.rowClickPage,
            rowClickParam: tableOptions.rowClickParam || 'id',
            rowClickReturnUrl: tableOptions.rowClickReturnUrl !== false,
            ...options
        };
        
        this.originalData = [];
        this.filteredData = [];
        this.sortColumns = [];
        this.searchTerm = '';
        this.columnFilters = {};
        this.filterOperations = {};
        this.columnVisibilityState = {};
        this.columnMeta = [];
        
        this.pageSize = 25;
        this.currentPage = 1;
        this.pageSizeOptions = [25, 50, 100, 250, 500];
        
        this.init();
    }
    
    parseTableOptions() {
        return {
            rowClick: this.table.dataset.rowClick === 'true',
            rowClickPage: this.table.dataset.rowClickPage || null,
            rowClickParam: this.table.dataset.rowClickParam || 'id',
            rowClickReturnUrl: this.table.dataset.rowClickReturnurl !== 'false'
        };
    }
    
    init() {
        this.wrapTable();
        this.parseColumnMeta();
        this.captureData();
        this.loadState();
        this.createToolbar();
        if (this.options.sortable) {
            this.makeSortable();
        }
        // Setup row navigation if enabled (supports data-row-href or legacy rowClickPage)
        if (this.options.rowClick) {
            this.setupRowNavigation();
        }
        if (this.options.stickyHeader) {
            this.makeStickyHeader();
        }
        this.applyColumnVisibility();
        this.applyFilters(false);
        this.addHoverEffects();
    }
    
    wrapTable() {
        const parent = this.table.parentElement;
        const wrapper = document.createElement('div');
        wrapper.className = 'enhanced-grid-container';
        parent.insertBefore(wrapper, this.table);
        wrapper.appendChild(this.table);
        this.container = wrapper;
        this.table.classList.add('enhanced-grid');
    }
    
    parseColumnMeta() {
        const headers = this.table.querySelectorAll('thead th');
        this.columnMeta = Array.from(headers).map((th, index) => {
            const name = th.dataset.col || th.textContent.trim().replace(/[↑↓123]/g, '').trim();
            const isActions = name.toLowerCase() === 'actions' || th.dataset.export === 'false';
            return {
                index,
                name,
                filterType: th.dataset.filter || (isActions ? 'none' : 'text'),
                options: th.dataset.filterOptions ? th.dataset.filterOptions.split('|') : 
                         (th.dataset.options ? th.dataset.options.split('|') : []),
                noSort: th.classList.contains('no-sort') || isActions,
                noExport: th.dataset.export === 'false' || isActions,
                noToggle: isActions
            };
        });
    }
    
    captureData() {
        const rows = this.table.querySelectorAll('tbody tr');
        this.originalData = Array.from(rows).map(row => ({
            element: row,
            cells: Array.from(row.cells).map(cell => cell.textContent.trim()),
            cellsLower: Array.from(row.cells).map(cell => cell.textContent.trim().toLowerCase()),
            html: row.innerHTML,
            id: row.dataset.rowId || row.dataset.id || ''
        }));
        this.filteredData = [...this.originalData];
    }
    
    loadState() {
        if (!this.options.persistState) return;
        
        try {
            const saved = localStorage.getItem(this.storageKey);
            if (saved) {
                const state = JSON.parse(saved);
                this.sortColumns = state.sortColumns || [];
                this.columnFilters = state.columnFilters || {};
                this.filterOperations = state.filterOperations || {};
                this.columnVisibilityState = state.columnVisibility || {};
                this.searchTerm = state.searchTerm || '';
                if (state.pageSize) this.pageSize = state.pageSize;
                if (state.currentPage) this.currentPage = state.currentPage;
            }
        } catch (e) {
            console.warn('Could not load grid state:', e);
        }
        
        if (Object.keys(this.columnVisibilityState).length === 0) {
            this.columnMeta.forEach((col, index) => {
                this.columnVisibilityState[index] = true;
            });
        }
    }
    
    saveState() {
        if (!this.options.persistState) return;
        
        try {
            localStorage.setItem(this.storageKey, JSON.stringify({
                sortColumns: this.sortColumns,
                columnFilters: this.columnFilters,
                filterOperations: this.filterOperations,
                columnVisibility: this.columnVisibilityState,
                searchTerm: this.searchTerm,
                pageSize: this.pageSize,
                currentPage: this.currentPage
            }));
        } catch (e) {
            console.warn('Could not save grid state:', e);
        }
    }
    
    createToolbar() {
        const toolbar = document.createElement('div');
        toolbar.className = 'grid-toolbar grid-toolbar-premium';
        
        let html = '<div class="grid-toolbar-left">';
        
        if (this.options.searchable) {
            html += `
                <div class="grid-search">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                    </svg>
                    <input type="text" class="grid-search-input" placeholder="Search all columns..." value="${this.escapeHtml(this.searchTerm)}" />
                </div>
            `;
        }
        
        html += '</div><div class="grid-toolbar-right">';
        
        if (this.options.columnFilters) {
            html += `
                <div class="grid-filter-toggle">
                    <button type="button" class="grid-toolbar-btn grid-filter-btn" title="Column Filters" data-no-row-nav>
                        <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 4a1 1 0 011-1h16a1 1 0 011 1v2.586a1 1 0 01-.293.707l-6.414 6.414a1 1 0 00-.293.707V17l-4 4v-6.586a1 1 0 00-.293-.707L3.293 7.293A1 1 0 013 6.586V4z" />
                        </svg>
                        <span>Filters</span>
                        <span class="filter-count"></span>
                    </button>
                    <div class="grid-filter-dropdown"></div>
                </div>
            `;
        }
        
        if (this.options.columnVisibility) {
            html += `
                <div class="grid-column-toggle">
                    <button type="button" class="grid-toolbar-btn grid-column-btn" title="Toggle columns" data-no-row-nav>
                        <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 17V7m0 10a2 2 0 01-2 2H5a2 2 0 01-2-2V7a2 2 0 012-2h2a2 2 0 012 2m0 10a2 2 0 002 2h2a2 2 0 002-2M9 7a2 2 0 012-2h2a2 2 0 012 2m0 10V7m0 10a2 2 0 002 2h2a2 2 0 002-2V7a2 2 0 00-2-2h-2a2 2 0 00-2 2" />
                        </svg>
                        <span>Columns</span>
                    </button>
                    <div class="grid-column-dropdown"></div>
                </div>
            `;
        }
        
        if (this.options.exportable) {
            html += `
                <div class="grid-export-toggle">
                    <button type="button" class="grid-toolbar-btn grid-export-btn" data-no-row-nav>
                        <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
                        </svg>
                        <span>Export</span>
                    </button>
                    <div class="grid-export-dropdown">
                        <button type="button" class="grid-export-csv" data-no-row-nav>
                            <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                            </svg>
                            Export CSV
                        </button>
                        <button type="button" class="grid-export-excel" data-no-row-nav>
                            <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 10h18M3 14h18m-9-4v8m-7 0h14a2 2 0 002-2V8a2 2 0 00-2-2H5a2 2 0 00-2 2v8a2 2 0 002 2z" />
                            </svg>
                            Export Excel
                        </button>
                    </div>
                </div>
            `;
        }
        
        html += `
            <button type="button" class="grid-toolbar-btn grid-reset-btn" title="Reset view" data-no-row-nav>
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                </svg>
                <span>Reset</span>
            </button>
            <span class="grid-count"></span>
        </div>`;
        
        toolbar.innerHTML = html;
        this.container.insertBefore(toolbar, this.table);
        this.toolbar = toolbar;
        
        this.bindToolbarEvents(toolbar);
        this.countDisplay = toolbar.querySelector('.grid-count');
        this.updateCount();
        this.updateFilterCount();
    }
    
    bindToolbarEvents(toolbar) {
        if (this.options.searchable) {
            const searchInput = toolbar.querySelector('.grid-search-input');
            let debounceTimer;
            searchInput.addEventListener('input', (e) => {
                clearTimeout(debounceTimer);
                debounceTimer = setTimeout(() => {
                    this.searchTerm = e.target.value;
                    this.applyFilters();
                    this.saveState();
                }, 150);
            });
        }
        
        if (this.options.columnFilters) {
            this.setupFilterDropdown(toolbar);
        }
        
        if (this.options.columnVisibility) {
            this.setupColumnDropdown(toolbar);
        }
        
        if (this.options.exportable) {
            this.setupExportDropdown(toolbar);
        }
        
        const resetBtn = toolbar.querySelector('.grid-reset-btn');
        if (resetBtn) {
            resetBtn.addEventListener('click', () => this.resetView());
        }
    }
    
    setupFilterDropdown(toolbar) {
        const toggleContainer = toolbar.querySelector('.grid-filter-toggle');
        const toggleBtn = toolbar.querySelector('.grid-filter-btn');
        const dropdown = toolbar.querySelector('.grid-filter-dropdown');
        
        this.populateFilterDropdown(dropdown);
        
        toggleBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            this.closeAllDropdowns();
            dropdown.classList.toggle('show');
        });
        
        document.addEventListener('click', (e) => {
            if (!toggleContainer.contains(e.target)) {
                dropdown.classList.remove('show');
            }
        });
    }
    
    populateFilterDropdown(dropdown) {
        let html = '<div class="grid-dropdown-header">Column Filters</div><div class="grid-filter-list">';
        
        this.columnMeta.forEach((col, index) => {
            if (col.filterType === 'none' || col.name.toLowerCase() === 'actions') return;
            
            const currentValue = this.columnFilters[index] || '';
            const currentOp = this.filterOperations[index] || 'contains';
            
            html += `<div class="grid-filter-item">
                <label>${this.escapeHtml(col.name)}</label>`;
            
            if (col.filterType === 'select' && col.options.length > 0) {
                html += `<select data-col-index="${index}" class="grid-filter-select">
                    <option value="">All</option>
                    ${col.options.map(opt => `<option value="${this.escapeHtml(opt)}" ${currentValue === opt ? 'selected' : ''}>${this.escapeHtml(opt)}</option>`).join('')}
                </select>`;
            } else {
                html += `
                <div class="grid-filter-input-group">
                    <select data-op-index="${index}" class="grid-filter-op">
                        <option value="contains" ${currentOp === 'contains' ? 'selected' : ''}>Contains</option>
                        <option value="equals" ${currentOp === 'equals' ? 'selected' : ''}>Equals</option>
                        <option value="starts" ${currentOp === 'starts' ? 'selected' : ''}>Starts with</option>
                    </select>
                    <input type="text" data-col-index="${index}" class="grid-filter-input" placeholder="Filter..." value="${this.escapeHtml(currentValue)}" />
                </div>`;
            }
            
            html += '</div>';
        });
        
        html += `</div>
            <div class="grid-dropdown-footer">
                <button type="button" class="btn-link grid-clear-filters" data-no-row-nav>Clear All</button>
                <button type="button" class="btn-primary grid-apply-filters" data-no-row-nav>Apply</button>
            </div>`;
        
        dropdown.innerHTML = html;
        
        dropdown.querySelector('.grid-clear-filters').addEventListener('click', () => {
            this.columnFilters = {};
            this.filterOperations = {};
            dropdown.querySelectorAll('input, select').forEach(el => {
                if (el.classList.contains('grid-filter-op')) {
                    el.value = 'contains';
                } else {
                    el.value = '';
                }
            });
            this.applyFilters();
            this.saveState();
            this.updateFilterCount();
        });
        
        dropdown.querySelector('.grid-apply-filters').addEventListener('click', () => {
            dropdown.querySelectorAll('[data-col-index]').forEach(el => {
                const index = parseInt(el.dataset.colIndex);
                const value = el.value.trim();
                if (value) {
                    this.columnFilters[index] = value;
                } else {
                    delete this.columnFilters[index];
                }
            });
            dropdown.querySelectorAll('[data-op-index]').forEach(el => {
                const index = parseInt(el.dataset.opIndex);
                this.filterOperations[index] = el.value;
            });
            this.applyFilters();
            this.saveState();
            this.updateFilterCount();
            dropdown.classList.remove('show');
        });
    }
    
    updateFilterCount() {
        const count = Object.keys(this.columnFilters).length;
        const badge = this.toolbar.querySelector('.filter-count');
        if (badge) {
            badge.textContent = count > 0 ? count : '';
            badge.style.display = count > 0 ? 'inline-flex' : 'none';
        }
    }
    
    setupColumnDropdown(toolbar) {
        const toggleContainer = toolbar.querySelector('.grid-column-toggle');
        const toggleBtn = toolbar.querySelector('.grid-column-btn');
        const dropdown = toolbar.querySelector('.grid-column-dropdown');
        
        this.populateColumnDropdown(dropdown);
        
        toggleBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            this.closeAllDropdowns();
            dropdown.classList.toggle('show');
        });
        
        document.addEventListener('click', (e) => {
            if (!toggleContainer.contains(e.target)) {
                dropdown.classList.remove('show');
            }
        });
    }
    
    populateColumnDropdown(dropdown) {
        let html = '<div class="grid-dropdown-header">Show/Hide Columns</div><div class="grid-column-list">';
        
        this.columnMeta.forEach((col, index) => {
            if (col.noToggle) return;
            
            const isVisible = this.columnVisibilityState[index] !== false;
            
            html += `
                <label class="grid-column-item">
                    <input type="checkbox" data-col-index="${index}" ${isVisible ? 'checked' : ''} data-no-row-nav>
                    <span>${this.escapeHtml(col.name) || `Column ${index + 1}`}</span>
                </label>
            `;
        });
        
        html += `</div>
            <div class="grid-dropdown-footer">
                <button type="button" class="btn-link grid-reset-columns" data-no-row-nav>Show All</button>
            </div>
        `;
        
        dropdown.innerHTML = html;
        
        dropdown.querySelectorAll('input[type="checkbox"]').forEach(checkbox => {
            checkbox.addEventListener('change', (e) => {
                const colIndex = parseInt(e.target.dataset.colIndex);
                this.columnVisibilityState[colIndex] = e.target.checked;
                this.applyColumnVisibility();
                this.saveState();
            });
        });
        
        dropdown.querySelector('.grid-reset-columns').addEventListener('click', () => {
            this.columnMeta.forEach((col, index) => {
                this.columnVisibilityState[index] = true;
            });
            dropdown.querySelectorAll('input[type="checkbox"]').forEach(cb => {
                cb.checked = true;
            });
            this.applyColumnVisibility();
            this.saveState();
        });
    }
    
    setupExportDropdown(toolbar) {
        const toggleContainer = toolbar.querySelector('.grid-export-toggle');
        const toggleBtn = toolbar.querySelector('.grid-export-btn');
        const dropdown = toolbar.querySelector('.grid-export-dropdown');
        
        toggleBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            this.closeAllDropdowns();
            dropdown.classList.toggle('show');
        });
        
        document.addEventListener('click', (e) => {
            if (!toggleContainer.contains(e.target)) {
                dropdown.classList.remove('show');
            }
        });
        
        dropdown.querySelector('.grid-export-csv').addEventListener('click', () => {
            this.exportCSV();
            dropdown.classList.remove('show');
        });
        
        dropdown.querySelector('.grid-export-excel').addEventListener('click', () => {
            this.exportExcel();
            dropdown.classList.remove('show');
        });
    }
    
    closeAllDropdowns() {
        this.toolbar.querySelectorAll('.grid-filter-dropdown, .grid-column-dropdown, .grid-export-dropdown').forEach(dd => {
            dd.classList.remove('show');
        });
    }
    
    resetView() {
        try {
            localStorage.removeItem(this.storageKey);
        } catch (e) { }
        window.location.reload();
    }
    
    makeSortable() {
        const headers = this.table.querySelectorAll('thead th');
        headers.forEach((header, index) => {
            const meta = this.columnMeta[index];
            if (meta.noSort) return;
            
            header.classList.add('sortable');
            header.setAttribute('data-col-index', index);
            
            if (!header.querySelector('.sort-icon')) {
                const sortIcon = document.createElement('span');
                sortIcon.className = 'sort-icon';
                sortIcon.innerHTML = `
                    <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 16V4m0 0L3 8m4-4l4 4m6 0v12m0 0l4-4m-4 4l-4-4" />
                    </svg>
                `;
                header.appendChild(sortIcon);
            }
            
            header.addEventListener('click', (e) => {
                if (e.shiftKey && this.options.multiSort) {
                    this.addSort(index);
                } else {
                    this.sort(index);
                }
            });
        });
        
        this.applySortIndicators();
    }
    
    sort(columnIndex) {
        const existingSort = this.sortColumns.find(s => s.column === columnIndex);
        
        if (existingSort) {
            if (existingSort.direction === 'asc') {
                existingSort.direction = 'desc';
            } else {
                this.sortColumns = [];
            }
            if (existingSort.direction) {
                this.sortColumns = [existingSort];
            }
        } else {
            this.sortColumns = [{ column: columnIndex, direction: 'asc' }];
        }
        
        this.applySort();
        this.applySortIndicators();
        this.saveState();
    }
    
    addSort(columnIndex) {
        const existingIndex = this.sortColumns.findIndex(s => s.column === columnIndex);
        
        if (existingIndex >= 0) {
            const existing = this.sortColumns[existingIndex];
            if (existing.direction === 'asc') {
                existing.direction = 'desc';
            } else {
                this.sortColumns.splice(existingIndex, 1);
            }
        } else if (this.sortColumns.length < this.options.maxSortColumns) {
            this.sortColumns.push({ column: columnIndex, direction: 'asc' });
        }
        
        this.applySort();
        this.applySortIndicators();
        this.saveState();
    }
    
    applySort() {
        if (this.sortColumns.length === 0) {
            this.render();
            return;
        }
        
        this.filteredData.sort((a, b) => {
            for (const sortDef of this.sortColumns) {
                const valA = a.cells[sortDef.column] || '';
                const valB = b.cells[sortDef.column] || '';
                
                const numA = parseFloat(valA.replace(/[$,]/g, ''));
                const numB = parseFloat(valB.replace(/[$,]/g, ''));
                
                let comparison = 0;
                if (!isNaN(numA) && !isNaN(numB)) {
                    comparison = numA - numB;
                } else {
                    comparison = valA.localeCompare(valB);
                }
                
                if (comparison !== 0) {
                    return sortDef.direction === 'asc' ? comparison : -comparison;
                }
            }
            return 0;
        });
        
        this.render();
    }
    
    applySortIndicators() {
        const headers = this.table.querySelectorAll('thead th');
        headers.forEach(h => {
            h.classList.remove('sort-asc', 'sort-desc');
            const orderBadge = h.querySelector('.sort-badge');
            if (orderBadge) orderBadge.remove();
        });
        
        this.sortColumns.forEach((sortDef, index) => {
            const header = headers[sortDef.column];
            if (header) {
                header.classList.add(`sort-${sortDef.direction}`);
                if (this.sortColumns.length > 1) {
                    const orderBadge = document.createElement('span');
                    orderBadge.className = 'sort-badge';
                    orderBadge.textContent = index + 1;
                    header.appendChild(orderBadge);
                }
            }
        });
    }
    
    setupRowNavigation() {
        const tbody = this.table.querySelector('tbody');
        if (!tbody) return;
        
        this.table.classList.add('row-click-enabled');
        
        tbody.addEventListener('click', (e) => {
            if (this.shouldPreventNavigation(e.target)) return;
            
            const row = e.target.closest('tr');
            if (!row) return;
            
            // v3.0: ONLY use data-row-href (server-generated, routing-safe URL)
            const href = row.dataset.rowHref;
            if (href) {
                // Support Ctrl/Cmd-click to open in new tab
                if (e.ctrlKey || e.metaKey) {
                    window.open(href, '_blank');
                } else {
                    window.location.href = href;
                }
                return;
            }
            
            // v3.0: If data-row-href is missing, log warning and do nothing
            // Never attempt brittle client-side URL building
            const rowId = row.dataset.rowId || row.dataset.id;
            if (rowId) {
                console.warn(`[EnhancedGrid] Row click on table #${this.tableId} row id="${rowId}" - missing data-row-href. Server must provide routing-safe URL via Url.Page().`);
            }
            // Removed legacy fallback: no more client-side "/Details?id=..." building
        });
        
        const rows = tbody.querySelectorAll('tr');
        rows.forEach(row => {
            // v3.0: Row is only clickable if it has data-row-href
            const hasRowHref = row.dataset.rowHref;
            
            if (hasRowHref) {
                row.classList.add('clickable-row', 'row-clickable');
                row.setAttribute('role', 'link');
                row.setAttribute('tabindex', '0');
                row.style.cursor = 'pointer';
                
                // Keyboard accessibility: Enter or Space opens the row
                row.addEventListener('keydown', (e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                        e.preventDefault();
                        // Support Ctrl/Cmd for new tab
                        const href = row.dataset.rowHref;
                        if (href) {
                            if (e.ctrlKey || e.metaKey) {
                                window.open(href, '_blank');
                            } else {
                                window.location.href = href;
                            }
                        }
                    }
                });
            }
        });
    }
    
    shouldPreventNavigation(target) {
        if (target.hasAttribute('data-no-row-nav')) return true;
        if (target.closest('[data-no-row-nav]')) return true;
        
        const preventTags = ['A', 'BUTTON', 'INPUT', 'SELECT', 'TEXTAREA', 'LABEL'];
        if (preventTags.includes(target.tagName)) return true;
        if (target.closest('a, button, input, select, textarea, label')) return true;
        
        return false;
    }
    
    makeStickyHeader() {
        const thead = this.table.querySelector('thead');
        if (thead) {
            thead.classList.add('sticky-header');
        }
    }
    
    addHoverEffects() {
        this.table.classList.add('hover-effects');
    }
    
    applyFilters(resetPage = true) {
        if (resetPage) this.currentPage = 1;
        const searchLower = this.searchTerm.toLowerCase();
        
        this.filteredData = this.originalData.filter(row => {
            if (searchLower) {
                const matchesSearch = this.columnMeta.some((col, index) => {
                    if (col.noExport) return false;
                    if (this.columnVisibilityState[index] === false) return false;
                    return row.cellsLower[index]?.includes(searchLower);
                });
                if (!matchesSearch) return false;
            }
            
            for (const [colIndex, filterValue] of Object.entries(this.columnFilters)) {
                const index = parseInt(colIndex);
                const cellValue = row.cellsLower[index] || '';
                const filterLower = filterValue.toLowerCase();
                const operation = this.filterOperations[index] || 'contains';
                
                let matches = false;
                switch (operation) {
                    case 'equals':
                        matches = cellValue === filterLower;
                        break;
                    case 'starts':
                        matches = cellValue.startsWith(filterLower);
                        break;
                    case 'contains':
                    default:
                        matches = cellValue.includes(filterLower);
                        break;
                }
                
                if (!matches) return false;
            }
            
            return true;
        });
        
        if (this.sortColumns.length > 0) {
            this.applySort();
        } else {
            this.render();
        }
        
        this.updateCount();
    }
    
    applyColumnVisibility() {
        const headers = this.table.querySelectorAll('thead th');
        const rows = this.table.querySelectorAll('tbody tr');
        
        this.columnMeta.forEach((col, index) => {
            const isVisible = this.columnVisibilityState[index] !== false;
            const displayStyle = isVisible ? '' : 'none';
            
            if (headers[index]) {
                headers[index].style.display = displayStyle;
            }
            
            rows.forEach(row => {
                if (row.cells[index]) {
                    row.cells[index].style.display = displayStyle;
                }
            });
        });
    }
    
    render() {
        const tbody = this.table.querySelector('tbody');
        if (!tbody) return;
        
        const totalFiltered = this.filteredData.length;
        const totalPages = this.getTotalPages();
        if (this.currentPage > totalPages && totalPages > 0) {
            this.currentPage = totalPages;
        }
        
        const isShowAll = this.pageSize >= totalFiltered;
        const startIndex = isShowAll ? 0 : (this.currentPage - 1) * this.pageSize;
        const endIndex = isShowAll ? totalFiltered : Math.min(startIndex + this.pageSize, totalFiltered);
        const pageData = this.filteredData.slice(startIndex, endIndex);
        
        tbody.innerHTML = '';
        pageData.forEach(row => {
            tbody.appendChild(row.element);
        });
        
        this.applyColumnVisibility();
        
        if (this.options.rowClick) {
            const rows = tbody.querySelectorAll('tr');
            rows.forEach(row => {
                if (row.dataset.rowHref) {
                    row.classList.add('clickable-row', 'row-clickable');
                    row.setAttribute('role', 'link');
                    row.setAttribute('tabindex', '0');
                    row.style.cursor = 'pointer';
                }
            });
        }
        
        this.renderPagination(totalFiltered, startIndex, endIndex, totalPages);
    }
    
    getTotalPages() {
        const total = this.filteredData.length;
        if (this.pageSize >= total) return 1;
        return Math.ceil(total / this.pageSize);
    }
    
    renderPagination(totalFiltered, startIndex, endIndex, totalPages) {
        let paginationBar = this.container.querySelector('.pe-pagination');
        if (paginationBar) paginationBar.remove();
        
        if (totalFiltered === 0) return;
        
        paginationBar = document.createElement('div');
        paginationBar.className = 'pe-pagination';
        
        const startRecord = startIndex + 1;
        const endRecord = endIndex;
        
        let html = `<div class="pe-pagination__info">Showing <strong>${startRecord.toLocaleString()}–${endRecord.toLocaleString()}</strong> of <strong>${totalFiltered.toLocaleString()}</strong> records</div>`;
        html += '<div class="pe-pagination__controls">';
        // PR #116d.23a — aria-label for WCAG 2.1 AA select-name compliance.
        html += '<div class="pe-pagination__size"><label>Rows:</label><select class="pe-pagination__select" aria-label="Rows per page">';
        
        for (const size of this.pageSizeOptions) {
            const selected = this.pageSize === size ? ' selected' : '';
            html += `<option value="${size}"${selected}>${size}</option>`;
        }
        const allSelected = this.pageSize >= totalFiltered && totalFiltered > 500 ? ' selected' : '';
        html += `<option value="all"${allSelected}>All</option>`;
        html += '</select></div>';
        
        if (totalPages > 1) {
            html += '<nav class="pe-pagination__nav">';
            html += this.currentPage > 1
                ? `<button type="button" class="pe-pagination__btn" data-page="${this.currentPage - 1}" data-no-row-nav>‹ Prev</button>`
                : '<span class="pe-pagination__btn pe-pagination__btn--disabled">‹ Prev</span>';
            
            const pages = this.getPageNumbers(totalPages);
            for (const p of pages) {
                if (p === -1) {
                    html += '<span class="pe-pagination__ellipsis">…</span>';
                } else if (p === this.currentPage) {
                    html += `<span class="pe-pagination__btn pe-pagination__btn--active">${p}</span>`;
                } else {
                    html += `<button type="button" class="pe-pagination__btn" data-page="${p}" data-no-row-nav>${p}</button>`;
                }
            }
            
            html += this.currentPage < totalPages
                ? `<button type="button" class="pe-pagination__btn" data-page="${this.currentPage + 1}" data-no-row-nav>Next ›</button>`
                : '<span class="pe-pagination__btn pe-pagination__btn--disabled">Next ›</span>';
            html += '</nav>';
        }
        
        html += '</div>';
        paginationBar.innerHTML = html;
        this.container.appendChild(paginationBar);
        
        const sizeSelect = paginationBar.querySelector('.pe-pagination__select');
        if (sizeSelect) {
            sizeSelect.addEventListener('change', (e) => {
                const val = e.target.value;
                this.pageSize = val === 'all' ? this.filteredData.length + 1 : parseInt(val);
                this.currentPage = 1;
                this.saveState();
                this.render();
                this.updateCount();
            });
        }
        
        paginationBar.querySelectorAll('[data-page]').forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.preventDefault();
                this.currentPage = parseInt(btn.dataset.page);
                this.saveState();
                this.render();
                this.updateCount();
                this.table.scrollIntoView({ behavior: 'smooth', block: 'start' });
            });
        });
    }
    
    getPageNumbers(totalPages) {
        const pages = [];
        if (totalPages <= 7) {
            for (let i = 1; i <= totalPages; i++) pages.push(i);
        } else {
            pages.push(1);
            if (this.currentPage > 3) pages.push(-1);
            for (let i = Math.max(2, this.currentPage - 1); i <= Math.min(totalPages - 1, this.currentPage + 1); i++) {
                pages.push(i);
            }
            if (this.currentPage < totalPages - 2) pages.push(-1);
            pages.push(totalPages);
        }
        return pages;
    }
    
    updateCount() {
        if (this.countDisplay) {
            const total = this.originalData.length;
            const filtered = this.filteredData.length;
            if (filtered === total) {
                this.countDisplay.textContent = `${total} records`;
            } else {
                this.countDisplay.textContent = `${filtered} of ${total}`;
            }
        }
    }
    
    exportCSV() {
        const visibleCols = this.columnMeta.filter((col, index) => 
            !col.noExport && this.columnVisibilityState[index] !== false
        );
        
        const headers = visibleCols.map(col => `"${col.name.replace(/"/g, '""')}"`);
        const rows = this.filteredData.map(row => 
            visibleCols.map(col => {
                const value = row.cells[col.index] || '';
                return `"${value.replace(/"/g, '""')}"`;
            }).join(',')
        );
        
        const csv = [headers.join(','), ...rows].join('\n');
        this.downloadFile(csv, `${this.tableId}-export.csv`, 'text/csv;charset=utf-8;');
    }
    
    exportExcel() {
        const visibleCols = this.columnMeta.filter((col, index) => 
            !col.noExport && this.columnVisibilityState[index] !== false
        );
        
        if (typeof XLSX !== 'undefined') {
            const wsData = [
                visibleCols.map(col => col.name),
                ...this.filteredData.map(row => 
                    visibleCols.map(col => row.cells[col.index] || '')
                )
            ];
            
            const ws = XLSX.utils.aoa_to_sheet(wsData);
            const wb = XLSX.utils.book_new();
            XLSX.utils.book_append_sheet(wb, ws, 'Data');
            XLSX.writeFile(wb, `${this.tableId}-export.xlsx`);
        } else {
            const header = visibleCols.map(col => `<th>${this.escapeHtml(col.name)}</th>`).join('');
            const rows = this.filteredData.map(row => 
                '<tr>' + visibleCols.map(col => 
                    `<td>${this.escapeHtml(row.cells[col.index] || '')}</td>`
                ).join('') + '</tr>'
            ).join('');
            
            const xml = `<?xml version="1.0" encoding="UTF-8"?>
<?mso-application progid="Excel.Sheet"?>
<Workbook xmlns="urn:schemas-microsoft-com:office:spreadsheet"
 xmlns:ss="urn:schemas-microsoft-com:office:spreadsheet">
<Worksheet ss:Name="Data">
<Table>
<Row>${header}</Row>
${rows}
</Table>
</Worksheet>
</Workbook>`;
            
            this.downloadFile(xml, `${this.tableId}-export.xls`, 'application/vnd.ms-excel');
        }
    }
    
    downloadFile(content, filename, mimeType) {
        const blob = new Blob([content], { type: mimeType });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    }
    
    escapeHtml(str) {
        if (!str) return '';
        const div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    }
}

function initEnhancedGrid(tableId, options = {}) {
    return new EnhancedGrid(tableId, options);
}

document.addEventListener('DOMContentLoaded', function() {
    const tables = document.querySelectorAll('table[data-enhanced-grid="true"]');
    tables.forEach(table => {
        if (!table.id) {
            console.warn('EnhancedGrid: Table with data-enhanced-grid="true" is missing an id attribute');
            return;
        }
        initEnhancedGrid(table.id);
    });
});

if (typeof module !== 'undefined' && module.exports) {
    module.exports = { EnhancedGrid, initEnhancedGrid };
}
