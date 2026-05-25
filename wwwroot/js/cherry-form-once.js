/* PR #336 (2026-05-25). Defense-in-depth submit dedup for forms that mutate
 * ledger state.
 *
 * Two attribute hooks:
 *
 *   data-cherry-submit-once
 *     Place on <button type="submit">. On click the button is immediately
 *     disabled + aria-busy + label flipped to a spinner state until the page
 *     navigates away (which is what happens on a successful POST → redirect).
 *     Prevents accidental double-submission from missed double-clicks, slow
 *     servers, or impatient users. NOT the primary dedup — that's the
 *     idempotency key (server-side). This is the visible feedback layer.
 *
 *   data-cherry-confirm="prompt text"
 *     Place on the <form>. Intercepts submit and shows a confirm() dialog
 *     with the given prompt text. Submission proceeds only if the user
 *     accepts. Used on irreversible-ish ledger actions like VoidPayment.
 *
 * Both attributes can coexist on the same form (confirm gates first, then
 * the submit-once script kicks in once the dialog is accepted).
 *
 * Idempotent: re-running the wire-up via document re-render is a no-op
 * because each handler is bound once per element via a sentinel attribute.
 */
(function () {
  'use strict';

  function wireSubmitOnce(btn) {
    if (btn.dataset.cherryWired === '1') return;
    btn.dataset.cherryWired = '1';

    // Locate the form ancestor. If the button isn't inside one, just disable
    // on click — better than nothing.
    var form = btn.form || btn.closest('form');
    var fire = function () {
      // Capture the original label so we can restore if navigation fails.
      var originalHtml = btn.innerHTML;
      btn.setAttribute('aria-busy', 'true');
      btn.disabled = true;
      btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Working…';

      // Belt and suspenders: if for some reason the page does NOT navigate
      // within 8 seconds (e.g. server error returns same page), restore the
      // button so the user can retry once they've read any error.
      window.setTimeout(function () {
        if (document.body.contains(btn)) {
          btn.disabled = false;
          btn.removeAttribute('aria-busy');
          btn.innerHTML = originalHtml;
        }
      }, 8000);
    };

    if (form) {
      form.addEventListener('submit', function (ev) {
        // Don't fire if the form's submit was prevented (e.g. confirm dialog
        // rejected, native validation failed).
        if (ev.defaultPrevented) return;
        fire();
      });
    } else {
      btn.addEventListener('click', fire);
    }
  }

  function wireConfirm(form) {
    if (form.dataset.cherryConfirmWired === '1') return;
    form.dataset.cherryConfirmWired = '1';

    form.addEventListener('submit', function (ev) {
      var prompt = form.getAttribute('data-cherry-confirm') || 'Are you sure?';
      if (!window.confirm(prompt)) {
        ev.preventDefault();
        ev.stopPropagation();
      }
    });
  }

  function bootstrap() {
    // Wire confirm first so it runs before submit-once on shared forms.
    document.querySelectorAll('form[data-cherry-confirm]').forEach(wireConfirm);
    document.querySelectorAll('button[data-cherry-submit-once]').forEach(wireSubmitOnce);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', bootstrap);
  } else {
    bootstrap();
  }

  // Expose a re-wire hook in case a future page injects forms dynamically.
  // Most pages here are server-rendered, but the receiving Control Center
  // and a few other interactive surfaces stream rows in via fetch().
  window.CherryFormOnce = { rewire: bootstrap };
})();
