/* =============================================================================
   CherryAI EAM — Design System Primitives JS v1.0
   PR #116d.1 — luxury motion + density toggle + noise canvas.

   Exposes window.CherryDS with three APIs:
     - density.set(mode)    // 'compact' | 'comfortable' | 'spacious'
     - density.get()
     - countUp.animate(el)  // imperative trigger
     - noise.start() / stop()

   Auto-runs on DOMContentLoaded:
     - reads density mode from localStorage
     - observes [data-count-up] elements and animates when they enter viewport
     - starts subtle film-grain noise canvas if .ds-noise-canvas exists
   ============================================================================= */
(function () {
  'use strict';

  // ---------------------------------------------------------------------------
  // Chrome-extension noise filter
  //
  // Browser extensions (Wispr Flow, password managers, dev-tool extensions, etc.)
  // commonly register `chrome.runtime.onMessage.addListener` callbacks that
  // `return true` to signal an async response — but never call sendResponse.
  // When the underlying message channel closes, Chrome raises:
  //
  //   Uncaught (in promise) Error: A listener indicated an asynchronous response
  //   by returning true, but the message channel closed before a response was received.
  //
  // The error is attributed to "line 0 col 0" of the current page URL, NOT to
  // the extension that caused it. That makes our app's console look broken when
  // it isn't, and pollutes any error-tracking pipeline we wire in later.
  //
  // We swallow that one specific message (and nothing else). Real app errors
  // still surface as before.
  // ---------------------------------------------------------------------------
  const EXT_NOISE_RE = /listener indicated an asynchronous response by returning true.*message channel closed/i;

  window.addEventListener('error', function (evt) {
    if (evt && evt.message && EXT_NOISE_RE.test(evt.message)) {
      evt.stopImmediatePropagation();
      evt.preventDefault();
      return true;
    }
  }, true);

  window.addEventListener('unhandledrejection', function (evt) {
    const msg = evt && evt.reason && (evt.reason.message || String(evt.reason));
    if (msg && EXT_NOISE_RE.test(msg)) {
      evt.stopImmediatePropagation();
      evt.preventDefault();
      return true;
    }
  }, true);

  const STORAGE_KEY = 'cherryai.density';
  const VALID_MODES = ['compact', 'comfortable', 'spacious'];
  const prefersReducedMotion = typeof window.matchMedia === 'function'
    && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  // -------------------------------------------------------------------------
  // Density mode
  // -------------------------------------------------------------------------
  function applyDensity(mode) {
    if (!VALID_MODES.includes(mode)) mode = 'comfortable';
    const html = document.documentElement;
    VALID_MODES.forEach(m => html.classList.remove('density-' + m));
    if (mode !== 'comfortable') {
      html.classList.add('density-' + mode);
    }
    // Update toggle buttons if present
    document.querySelectorAll('.ds-density-toggle__btn').forEach(btn => {
      btn.setAttribute('aria-pressed', btn.dataset.density === mode ? 'true' : 'false');
    });
    return mode;
  }

  function getDensity() {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored && VALID_MODES.includes(stored)) return stored;
    } catch (_e) { /* localStorage unavailable */ }
    return 'comfortable';
  }

  function setDensity(mode) {
    const applied = applyDensity(mode);
    try { localStorage.setItem(STORAGE_KEY, applied); } catch (_e) { /* ignore */ }
    return applied;
  }

  // Wire any .ds-density-toggle__btn[data-density="..."] elements
  function wireDensityToggle(root) {
    (root || document).querySelectorAll('.ds-density-toggle__btn').forEach(btn => {
      btn.addEventListener('click', () => setDensity(btn.dataset.density));
    });
  }

  // -------------------------------------------------------------------------
  // Count-up animation
  // -------------------------------------------------------------------------
  function parseNumeric(str) {
    if (!str) return null;
    // Strip everything that isn't a digit, decimal point, or minus.
    const cleaned = String(str).replace(/[^0-9.\-]/g, '');
    if (!cleaned || cleaned === '-' || cleaned === '.') return null;
    const n = parseFloat(cleaned);
    return Number.isFinite(n) ? n : null;
  }

  function formatLikeOriginal(value, originalText) {
    // Preserve decimal precision of the original text.
    const dot = String(originalText).indexOf('.');
    const digits = (dot >= 0) ? (String(originalText).length - dot - 1) : 0;
    const formatted = digits > 0 ? value.toFixed(Math.min(digits, 3)) : Math.round(value).toLocaleString();
    return formatted;
  }

  function animateCountUp(el) {
    if (!el || el.dataset.countUpDone === 'true') return;
    if (prefersReducedMotion) { el.dataset.countUpDone = 'true'; return; }

    // Find the text node containing the numeric value.
    let textNode = null;
    for (const node of el.childNodes) {
      if (node.nodeType === Node.TEXT_NODE && node.textContent.trim().length > 0) {
        textNode = node;
        break;
      }
    }
    if (!textNode) return;

    const originalText = textNode.textContent;
    const target = parseNumeric(originalText);
    if (target === null) { el.dataset.countUpDone = 'true'; return; }

    const duration = 1200; // var(--dur-count-up) in ms
    const startTime = performance.now();
    const ease = t => 1 - Math.pow(1 - t, 3); // ease-out-cubic
    const startValue = 0;

    function frame(now) {
      const elapsed = now - startTime;
      const t = Math.min(1, elapsed / duration);
      const v = startValue + (target - startValue) * ease(t);
      textNode.textContent = formatLikeOriginal(v, originalText);
      if (t < 1) {
        requestAnimationFrame(frame);
      } else {
        textNode.textContent = originalText; // ensure final value is exact
        el.dataset.countUpDone = 'true';
      }
    }
    requestAnimationFrame(frame);
  }

  function observeCountUp(root) {
    if (typeof IntersectionObserver === 'undefined' || prefersReducedMotion) {
      (root || document).querySelectorAll('[data-count-up="true"]').forEach(el => {
        el.dataset.countUpDone = 'true';
      });
      return;
    }
    const io = new IntersectionObserver((entries) => {
      entries.forEach(entry => {
        if (entry.isIntersecting) {
          animateCountUp(entry.target);
          io.unobserve(entry.target);
        }
      });
    }, { threshold: 0.35, rootMargin: '0px 0px -10% 0px' });

    (root || document).querySelectorAll('[data-count-up="true"]').forEach(el => io.observe(el));
  }

  // -------------------------------------------------------------------------
  // Film-grain noise canvas
  // -------------------------------------------------------------------------
  let noiseTimer = null;

  function paintNoise(ctx, w, h) {
    const id = ctx.createImageData(w, h);
    const buf = id.data;
    for (let i = 0; i < buf.length; i += 4) {
      const v = (Math.random() * 255) | 0;
      buf[i] = v; buf[i + 1] = v; buf[i + 2] = v; buf[i + 3] = 255;
    }
    ctx.putImageData(id, 0, 0);
  }

  function startNoise() {
    if (prefersReducedMotion) return;
    const canvas = document.querySelector('.ds-noise-canvas');
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    function resize() {
      // Low-res noise — upscale via CSS for performance.
      canvas.width = 256;
      canvas.height = 256;
    }
    resize();
    window.addEventListener('resize', resize);
    // Repaint at 8fps so the grain stays alive without burning CPU.
    function tick() {
      paintNoise(ctx, canvas.width, canvas.height);
      noiseTimer = window.setTimeout(() => requestAnimationFrame(tick), 125);
    }
    tick();
  }

  function stopNoise() {
    if (noiseTimer) {
      clearTimeout(noiseTimer);
      noiseTimer = null;
    }
  }

  // -------------------------------------------------------------------------
  // Context drawer — open/close + ESC + backdrop click + focus trap
  // -------------------------------------------------------------------------
  function drawerOpen(id) {
    const el = document.getElementById(id);
    if (!el || !el.classList.contains('ds-drawer')) return;
    el.setAttribute('aria-hidden', 'false');
    document.body.style.overflow = 'hidden';
    // Move focus to first focusable element inside the panel.
    const focusable = el.querySelector('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])');
    if (focusable) focusable.focus();
  }

  function drawerClose(id) {
    const el = document.getElementById(id);
    if (!el) return;
    el.setAttribute('aria-hidden', 'true');
    document.body.style.overflow = '';
  }

  function drawerCloseAll() {
    document.querySelectorAll('.ds-drawer[aria-hidden="false"]').forEach(el => {
      el.setAttribute('aria-hidden', 'true');
    });
    document.body.style.overflow = '';
  }

  function wireDrawers(root) {
    (root || document).querySelectorAll('[data-drawer-close]').forEach(btn => {
      btn.addEventListener('click', () => drawerClose(btn.getAttribute('data-drawer-close')));
    });
    (root || document).querySelectorAll('[data-drawer-open]').forEach(btn => {
      btn.addEventListener('click', () => drawerOpen(btn.getAttribute('data-drawer-open')));
    });
  }

  // -------------------------------------------------------------------------
  // DataTable — column sort on click of [data-sortable] th
  // -------------------------------------------------------------------------
  function wireDataTables(root) {
    (root || document).querySelectorAll('.ds-table table').forEach(table => {
      const thead = table.querySelector('thead');
      if (!thead) return;
      thead.querySelectorAll('th[data-sortable="true"]').forEach((th, idx) => {
        th.addEventListener('click', () => sortByColumn(table, idx, th));
      });
    });
  }

  function sortByColumn(table, columnIndex, th) {
    const tbody = table.querySelector('tbody');
    if (!tbody) return;
    const rows = Array.from(tbody.querySelectorAll('tr')).filter(r => r.children.length > columnIndex);
    const current = th.getAttribute('data-sort-dir') || '';
    const next = current === 'asc' ? 'desc' : 'asc';
    // Clear sibling sort indicators
    th.parentElement.querySelectorAll('th').forEach(t => t.removeAttribute('data-sort-dir'));
    th.setAttribute('data-sort-dir', next);

    const numericRe = /^[-+]?[\d,]+(?:\.\d+)?$/;
    rows.sort((a, b) => {
      const av = a.children[columnIndex].textContent.trim();
      const bv = b.children[columnIndex].textContent.trim();
      const an = av.replace(/,/g, '');
      const bn = bv.replace(/,/g, '');
      if (numericRe.test(an) && numericRe.test(bn)) {
        return (next === 'asc' ? 1 : -1) * (parseFloat(an) - parseFloat(bn));
      }
      return (next === 'asc' ? 1 : -1) * av.localeCompare(bv);
    });
    rows.forEach(r => tbody.appendChild(r));
  }

  // -------------------------------------------------------------------------
  // Bootstrap on DOM ready
  // -------------------------------------------------------------------------
  function init() {
    applyDensity(getDensity());
    wireDensityToggle();
    observeCountUp();
    wireDrawers();
    wireDataTables();
    startNoise();
    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape') drawerCloseAll();
    });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }

  // -------------------------------------------------------------------------
  // Public API
  // -------------------------------------------------------------------------
  window.CherryDS = {
    density: { set: setDensity, get: getDensity, apply: applyDensity },
    countUp: { animate: animateCountUp, observe: observeCountUp },
    noise: { start: startNoise, stop: stopNoise },
    drawer: { open: drawerOpen, close: drawerClose, closeAll: drawerCloseAll },
    table: { sortByColumn: sortByColumn, wireAll: wireDataTables }
  };
})();
