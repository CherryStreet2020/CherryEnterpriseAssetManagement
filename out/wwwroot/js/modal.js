(function() {
    'use strict';

    window.toggleInlineForm = function(formId) {
        var el = document.getElementById(formId);
        if (!el) return;
        if (el.style.display === 'none') {
            el.style.display = '';
            el.scrollIntoView({ behavior: 'smooth', block: 'start' });
            setTimeout(function() {
                var firstInput = el.querySelector('input:not([type="hidden"]), select, textarea');
                if (firstInput) firstInput.focus();
            }, 100);
        } else {
            el.style.display = 'none';
        }
    };

    window.openModal = window.toggleInlineForm;
    window.closeModal = function(formId) {
        var el = document.getElementById(formId);
        if (el) el.style.display = 'none';
    };

    window.initEnhancedGrid = function(tableId, options) {
        if (typeof EnhancedGrid !== 'undefined') {
            return new EnhancedGrid(tableId, options);
        }
        return null;
    };

    document.addEventListener('keydown', function(e) {
        if (e.key === 'Escape') {
            var panels = document.querySelectorAll('.inline-form-panel:not([style*="display: none"])');
            panels.forEach(function(panel) {
                panel.style.display = 'none';
            });
        }
    });
})();
