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
  // Bootstrap on DOM ready
  // -------------------------------------------------------------------------
  function init() {
    applyDensity(getDensity());
    wireDensityToggle();
    observeCountUp();
    startNoise();
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
    noise: { start: startNoise, stop: stopNoise }
  };
})();
