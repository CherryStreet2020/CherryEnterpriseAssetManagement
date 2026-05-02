/**
 * CherryAI Enterprise Typeahead
 * Auto-initializes Tom Select on any <select> with data-cherry-select attribute.
 *
 * Usage:
 *   <select data-cherry-select name="VendorId">
 *       <option value="">-- Search vendors --</option>
 *       @foreach (var v in Model.Vendors) {
 *           <option value="@v.Value">@v.Text</option>
 *       }
 *   </select>
 *
 * Options via data attributes:
 *   data-cherry-select            - enables typeahead
 *   data-placeholder="Search..."  - placeholder text
 *   data-allow-clear="true"       - adds clear button (default: true)
 *   data-create="true"            - allows creating new options
 *   data-max-items="1"            - single select (default) or multi
 */
document.addEventListener('DOMContentLoaded', function () {
    initCherryTypeaheads();
});

function initCherryTypeaheads() {
    document.querySelectorAll('select[data-cherry-select]').forEach(function (el) {
        if (el.tomselect) return;

        var placeholder = el.getAttribute('data-placeholder') || '';
        if (!placeholder && el.options.length > 0 && el.options[0].value === '') {
            placeholder = el.options[0].text;
        }
        if (!placeholder) placeholder = 'Search...';

        var allowClear = el.getAttribute('data-allow-clear') !== 'false';
        var allowCreate = el.getAttribute('data-create') === 'true';
        var maxItems = parseInt(el.getAttribute('data-max-items')) || 1;

        new TomSelect(el, {
            maxItems: maxItems,
            placeholder: placeholder,
            allowEmptyOption: true,
            plugins: allowClear ? ['clear_button'] : [],
            create: allowCreate,
            createFilter: function (input) { return input.length >= 2; },
            searchField: ['text'],
            render: {
                no_results: function (data, escape) {
                    return '<div class="no-results">No matches for "' + escape(data.input) + '"</div>';
                },
                option_create: function (data, escape) {
                    return '<div class="create">+ Create "' + escape(data.input) + '"</div>';
                }
            },
            onInitialize: function () {
                var control = this.control;
                if (control) {
                    control.setAttribute('data-initialized', 'true');
                }
            }
        });
    });
}
