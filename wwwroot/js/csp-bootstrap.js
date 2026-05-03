// csp-bootstrap.js — runtime helper for the Task #18 migration off
// `script-src-attr 'unsafe-inline'` / `style-src-attr 'unsafe-inline'`.
//
// SHIPS SAFE-BY-DEFAULT: until the codemod (scripts/codemod_csp_inline.js)
// has been run, no element carries `data-csp-style` or `data-csp-action`,
// and this file is a pure no-op.
//
// Two responsibilities:
//
//   1. Style propagation. For every [data-csp-style] element (now and in
//      the future via MutationObserver), copy `el.dataset.cspStyle` onto
//      `el.style.cssText`. CSP3 only gates the *parsed-from-HTML* `style`
//      attribute via `style-src-attr`; assignments via the CSSOM are
//      always allowed.
//
//   2. Delegated event dispatch. The codemod's "delegated-action"
//      strategy (used inside <table>/<select> where a sibling <script>
//      would be invalid HTML) annotates the call-site with:
//
//          data-csp-action="<name>"
//          data-csp-arg-<key>="<value>"   (any number of these)
//
//      A single document-level click/change/submit/input listener picks
//      these up and dispatches into `window.CspActions[name](el, args, event)`.
//      The action registry is open for extension by any page-specific JS
//      that loads after this bootstrap.
(function () {
    'use strict';

    // ---------------------------------------------------------------- styles
    function applyStyle(el) {
        var v = el.getAttribute('data-csp-style');
        if (v != null) el.style.cssText = v;
    }

    function applyAllStyles(root) {
        var scope = root && root.querySelectorAll ? root : document;
        var nodes = scope.querySelectorAll('[data-csp-style]');
        for (var i = 0; i < nodes.length; i++) applyStyle(nodes[i]);
    }

    // --------------------------------------------------------------- actions
    // Argument extraction: every attribute whose name starts with
    // `data-csp-arg-` becomes a key in the args object (camelCased).
    function readArgs(el) {
        var args = {};
        var attrs = el.attributes;
        for (var i = 0; i < attrs.length; i++) {
            var n = attrs[i].name;
            if (n.indexOf('data-csp-arg-') !== 0) continue;
            var key = n.slice('data-csp-arg-'.length).replace(/-([a-z])/g, function (_m, c) {
                return c.toUpperCase();
            });
            args[key] = attrs[i].value;
        }
        return args;
    }

    var Actions = {
        // window.location = href
        navigate: function (el, args) {
            if (args.href) window.location.assign(args.href);
        },
        // history.back()
        back: function () { history.back(); },
        // submit the form whose id is in args.formId, or the closest <form>
        submitForm: function (el, args) {
            var form = args.formId ? document.getElementById(args.formId) : el.closest('form');
            if (form) form.submit();
        },
        // submit the <form> that contains this control
        submitContainingForm: function (el) {
            var form = el.closest('form');
            if (form) form.submit();
        },
        // toggle the [hidden] attribute on the element with id=args.targetId
        toggleHidden: function (el, args) {
            var t = document.getElementById(args.targetId);
            if (t) t.toggleAttribute('hidden');
        },
        // set [hidden] on element with id=args.targetId
        setHidden: function (el, args) {
            var t = document.getElementById(args.targetId);
            if (t) t.setAttribute('hidden', '');
        },
        // set style.display='none' on element with id=args.targetId
        hide: function (el, args) {
            var t = document.getElementById(args.targetId);
            if (t) t.style.display = 'none';
        },
        // .click() on element with id=args.targetId
        clickById: function (el, args) {
            var t = document.getElementById(args.targetId);
            if (t) t.click();
        },
        // .click() on element matching args.selector
        clickSelector: function (el, args) {
            var t = document.querySelector(args.selector);
            if (t) t.click();
        },
        // confirm(args.message) and submit args.formId (or closest form) on OK
        confirmAndSubmit: function (el, args) {
            if (window.confirm(args.message || 'Are you sure?')) {
                Actions.submitForm(el, args);
            }
        },
        // copy args.text (or el.textContent) to clipboard
        copyText: function (el, args) {
            var text = args.text != null ? args.text : el.textContent;
            if (navigator.clipboard) navigator.clipboard.writeText(text);
        },
        // alert(args.message)
        alertMessage: function (_el, args) {
            window.alert(args.message || '');
        },
        // dispatch into a globally-registered function: window[args.fn](this, args, event)
        callGlobal: function (el, args, event) {
            var fn = window[args.fn];
            if (typeof fn === 'function') return fn.call(el, event, args);
        }
    };

    function dispatch(eventName, event) {
        // Walk up from the target so handlers attached to a row containing
        // a clicked button still fire.
        var node = event.target;
        while (node && node !== document) {
            if (node.nodeType === 1 && node.hasAttribute && node.hasAttribute('data-csp-action')) {
                // Only fire if this element opted into this event type. The
                // attribute `data-csp-on` (space-separated event list) gates
                // dispatch; absence means {click} only.
                var onList = (node.getAttribute('data-csp-on') || 'click').split(/\s+/);
                if (onList.indexOf(eventName) !== -1) {
                    var name = node.getAttribute('data-csp-action');
                    var fn = window.CspActions && window.CspActions[name];
                    if (typeof fn === 'function') {
                        var ret = fn(node, readArgs(node), event);
                        if (ret === false) {
                            event.preventDefault();
                            event.stopPropagation();
                        }
                        return;
                    }
                }
            }
            node = node.parentNode;
        }
    }

    // ----------------------------------------------------------------- boot
    function init() {
        applyAllStyles(document);

        // Pick up styles + actions on dynamically-injected DOM.
        if (typeof MutationObserver === 'function') {
            var mo = new MutationObserver(function (records) {
                for (var i = 0; i < records.length; i++) {
                    var added = records[i].addedNodes;
                    for (var j = 0; j < added.length; j++) {
                        var n = added[j];
                        if (n.nodeType !== 1) continue;
                        if (n.hasAttribute && n.hasAttribute('data-csp-style')) applyStyle(n);
                        applyAllStyles(n);
                    }
                }
            });
            mo.observe(document.documentElement, { childList: true, subtree: true });
        }

        // One delegated listener per supported event family. Use capture
        // phase so we run before page-level handlers that may stopPropagation.
        ['click', 'change', 'input', 'submit'].forEach(function (evt) {
            document.addEventListener(evt, function (e) { dispatch(evt, e); }, true);
        });
    }

    // Expose registry first so page scripts that load between this file
    // and DOMContentLoaded can register additional actions.
    window.CspActions = Object.assign(window.CspActions || {}, Actions);

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
