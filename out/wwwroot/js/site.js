document.addEventListener('DOMContentLoaded', function() {
    initAutoEnhancedGrids();
});

function initAutoEnhancedGrids() {
    var tables = document.querySelectorAll('table[data-enhanced-grid="true"]');
    
    tables.forEach(function(table) {
        if (!table.id) {
            console.warn('[EnhancedGrid] Table with data-enhanced-grid="true" is missing an id attribute. Skipping.');
            return;
        }
        
        if (table.dataset.gridInitialized === 'true') {
            return;
        }
        
        var defaultOptions = {
            searchable: true,
            sortable: true,
            exportable: true,
            columnVisibility: true,
            clickableRows: false
        };
        
        var customOptions = {};
        if (table.dataset.gridOptions) {
            try {
                customOptions = JSON.parse(table.dataset.gridOptions);
            } catch (e) {
                console.warn('[EnhancedGrid] Invalid JSON in data-grid-options for table ' + table.id + ':', e);
            }
        }
        
        var options = Object.assign({}, defaultOptions, customOptions);
        
        if (typeof initEnhancedGrid === 'function') {
            initEnhancedGrid(table.id, options);
            table.dataset.gridInitialized = 'true';
        } else {
            console.warn('[EnhancedGrid] initEnhancedGrid function not found. Ensure enhanced-grid.js is loaded.');
        }
    });
}
