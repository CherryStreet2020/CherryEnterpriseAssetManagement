/* =============================================================================
   voice-client.js — Sprint 11 Voice MVP

   The in-page voice client. Subscribes to the cherry:voice:state event that
   control-center.js dispatches when the Voice FAB toggles, drives
   webkitSpeechRecognition to capture the transcript, POSTs to /_voice/invoke,
   then narrates the response via SpeechSynthesisUtterance.

   Visual states (mirrored to the FAB via data-cc-voice-mode):
     - idle       — waiting
     - listening  — mic open, awaiting speech
     - processing — POST in flight
     - speaking   — narrating response

   Browser support:
     - webkitSpeechRecognition: Chrome / Edge / Safari (WebKit). Firefox NO.
     - SpeechSynthesisUtterance: all modern browsers.
     - We degrade gracefully on unsupported browsers (FAB still fires
       cherry:voice:state but the recognizer never starts; user sees a toast).

   Reference: ADR-014 D1/D2/D3 + ADR-015 D10 + ADR-016 D8.
   ============================================================================= */
(function () {
    'use strict';

    var SESSION_STORAGE_KEY = 'cherry:voice:session';
    var INVOKE_URL = '/_voice/invoke';

    var recognition = null;          // active SpeechRecognition instance
    var listening = false;
    var lastTranscript = '';
    var lastConfidence = null;
    var inflightController = null;   // AbortController for inflight POST

    function init() {
        // Wait for the FAB to exist (it's only on Control Center pages).
        if (!document.querySelector('[data-cc-voice]')) return;

        // Subscribe to the FAB state event control-center.js dispatches.
        window.addEventListener('cherry:voice:state', onFabStateChange);

        // Ensure we have a stable session id (one per browser session).
        ensureSessionId();
    }

    /* ---------- Session id (stable per browser session) ----------------- */

    function ensureSessionId() {
        try {
            var sid = sessionStorage.getItem(SESSION_STORAGE_KEY);
            if (!sid) {
                sid = uuid();
                sessionStorage.setItem(SESSION_STORAGE_KEY, sid);
            }
            return sid;
        } catch (e) {
            return uuid();
        }
    }

    function getSessionId() {
        try {
            return sessionStorage.getItem(SESSION_STORAGE_KEY) || uuid();
        } catch (e) {
            return uuid();
        }
    }

    function uuid() {
        // RFC4122 v4-ish. Good enough for an in-session correlation id.
        if (window.crypto && window.crypto.randomUUID) {
            return window.crypto.randomUUID();
        }
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
            var r = (Math.random() * 16) | 0;
            var v = c === 'x' ? r : (r & 0x3) | 0x8;
            return v.toString(16);
        });
    }

    /* ---------- FAB state plumbing -------------------------------------- */

    function setFabMode(mode) {
        var fab = document.querySelector('[data-cc-voice]');
        if (!fab) return;
        if (mode === 'idle') {
            fab.removeAttribute('data-cc-voice-mode');
        } else {
            fab.setAttribute('data-cc-voice-mode', mode);
        }
    }

    function onFabStateChange(e) {
        var active = !!(e.detail && e.detail.active);
        if (active) {
            startListening();
        } else if (listening) {
            // Push-to-talk release — stop recording and let onresult / onend
            // route the captured transcript.
            stopListening(/*fireInvoke*/ true);
        }
    }

    /* ---------- Speech recognition -------------------------------------- */

    function getRecognitionCtor() {
        return window.SpeechRecognition || window.webkitSpeechRecognition || null;
    }

    function startListening() {
        if (listening) return;

        var Ctor = getRecognitionCtor();
        if (!Ctor) {
            toast({
                title: 'Voice unavailable',
                body: 'This browser does not support speech recognition. Use Chrome, Edge, or Safari.',
                kind: 'error',
            });
            return;
        }

        try {
            recognition = new Ctor();
            recognition.lang = 'en-US';
            recognition.interimResults = false;
            recognition.maxAlternatives = 1;
            recognition.continuous = false;

            lastTranscript = '';
            lastConfidence = null;

            recognition.onresult = function (e) {
                if (!e.results || e.results.length === 0) return;
                var res = e.results[e.results.length - 1];
                if (!res || res.length === 0) return;
                lastTranscript = (res[0].transcript || '').trim();
                lastConfidence = typeof res[0].confidence === 'number'
                    ? Math.round(res[0].confidence * 1000) / 1000
                    : null;
            };

            recognition.onerror = function (e) {
                listening = false;
                setFabMode('idle');
                var msg = (e && e.error) || 'unknown error';
                if (msg === 'not-allowed' || msg === 'service-not-allowed') {
                    toast({
                        title: 'Microphone blocked',
                        body: 'Allow microphone access in your browser to use voice.',
                        kind: 'error',
                    });
                } else if (msg !== 'no-speech' && msg !== 'aborted') {
                    toast({
                        title: 'Voice error',
                        body: 'Recognizer reported: ' + msg,
                        kind: 'error',
                    });
                }
            };

            recognition.onend = function () {
                if (!listening) return;        // already handled (manual stop)
                listening = false;
                setFabMode('idle');
                if (lastTranscript) {
                    invoke(lastTranscript, lastConfidence);
                }
            };

            recognition.start();
            listening = true;
            setFabMode('listening');
        } catch (err) {
            listening = false;
            setFabMode('idle');
            toast({
                title: 'Voice failed to start',
                body: (err && err.message) || String(err),
                kind: 'error',
            });
        }
    }

    function stopListening(fireInvoke) {
        if (!listening) return;
        try {
            if (recognition) recognition.stop();
        } catch (e) { /* swallow */ }
        // onend will fire and run the invoke. If fireInvoke=false we just
        // suppress by clearing lastTranscript first.
        if (!fireInvoke) lastTranscript = '';
    }

    /* ---------- POST /_voice/invoke ------------------------------------- */

    function invoke(transcript, confidence) {
        setFabMode('processing');
        toast({
            title: 'Heard:',
            body: transcript,
            kind: 'info',
            ttlMs: 4000,
        });

        if (inflightController) {
            try { inflightController.abort(); } catch (e) { /* ignore */ }
        }
        inflightController = new AbortController();

        var payload = {
            transcript: transcript,
            aiSessionId: getSessionId(),
            confidence: confidence,
            voiceContext: readContextHints(),
        };

        var headers = { 'Content-Type': 'application/json', 'Accept': 'application/json' };
        var antiforgery = readAntiforgeryToken();
        if (antiforgery) headers['RequestVerificationToken'] = antiforgery;

        fetch(INVOKE_URL, {
            method: 'POST',
            credentials: 'same-origin',
            headers: headers,
            body: JSON.stringify(payload),
            signal: inflightController.signal,
        })
        .then(function (r) {
            if (!r.ok) {
                if (r.status === 401) {
                    return Promise.reject(new Error('You need to sign in to use voice.'));
                }
                return Promise.reject(new Error('Server returned ' + r.status));
            }
            return r.json();
        })
        .then(function (data) {
            handleResponse(data);
        })
        .catch(function (err) {
            if (err && err.name === 'AbortError') return;
            setFabMode('idle');
            toast({
                title: 'Voice request failed',
                body: (err && err.message) || String(err),
                kind: 'error',
            });
        });
    }

    function handleResponse(data) {
        if (!data) {
            setFabMode('idle');
            return;
        }

        var spoken = (data.spoken || '').trim();
        if (spoken) {
            speak(spoken, function () { setFabMode('idle'); });
            setFabMode('speaking');
        } else {
            setFabMode('idle');
        }

        toast({
            title: data.displayed && data.displayed.title ? data.displayed.title : 'Voice',
            body: data.displayed && data.displayed.lines ? data.displayed.lines.join('\n') : spoken,
            actionLinks: data.actionLinks || null,
            kind: data.ok ? 'success' : 'warning',
            ttlMs: 10000,
        });
    }

    function readContextHints() {
        function meta(name) {
            var el = document.querySelector('meta[name="' + name + '"]');
            return el ? el.getAttribute('content') : null;
        }
        return {
            route: meta('cherry:voice:route') || location.pathname,
            entityType: meta('cherry:voice:entity-type'),
            entityId: meta('cherry:voice:entity-id'),
            tab: meta('cherry:voice:tab'),
            focusedField: meta('cherry:voice:focused-field'),
        };
    }

    function readAntiforgeryToken() {
        var el = document.querySelector('input[name="__RequestVerificationToken"]');
        return el ? el.value : null;
    }

    /* ---------- Speech synthesis ---------------------------------------- */

    function speak(text, onDone) {
        if (!('speechSynthesis' in window)) {
            if (onDone) onDone();
            return;
        }
        try { window.speechSynthesis.cancel(); } catch (e) { /* ignore */ }
        var u = new SpeechSynthesisUtterance(text);
        u.lang = 'en-US';
        u.rate = 1.05;
        u.pitch = 1.0;
        u.onend = function () { if (onDone) onDone(); };
        u.onerror = function () { if (onDone) onDone(); };
        window.speechSynthesis.speak(u);
    }

    /* ---------- Toast (transient bubble) -------------------------------- */

    function ensureToastContainer() {
        var c = document.querySelector('[data-cc-voice-toast]');
        if (c) return c;
        c = document.createElement('div');
        c.setAttribute('data-cc-voice-toast', 'true');
        c.setAttribute('aria-live', 'polite');
        document.body.appendChild(c);
        return c;
    }

    function toast(opts) {
        opts = opts || {};
        var container = ensureToastContainer();
        var item = document.createElement('div');
        item.className = 'ds-cc-voice-toast ds-cc-voice-toast--' + (opts.kind || 'info');

        if (opts.title) {
            var t = document.createElement('div');
            t.className = 'ds-cc-voice-toast__title';
            t.textContent = opts.title;
            item.appendChild(t);
        }
        if (opts.body) {
            var b = document.createElement('div');
            b.className = 'ds-cc-voice-toast__body';
            b.textContent = opts.body;
            item.appendChild(b);
        }
        if (opts.actionLinks && opts.actionLinks.length) {
            var actions = document.createElement('div');
            actions.className = 'ds-cc-voice-toast__actions';
            opts.actionLinks.forEach(function (link) {
                var a = document.createElement('a');
                a.href = link.href;
                a.textContent = link.label;
                a.className = 'ds-cc-voice-toast__action';
                actions.appendChild(a);
            });
            item.appendChild(actions);
        }

        var close = document.createElement('button');
        close.type = 'button';
        close.className = 'ds-cc-voice-toast__close';
        close.setAttribute('aria-label', 'Dismiss');
        close.textContent = '×';
        close.addEventListener('click', function () { dismiss(item); });
        item.appendChild(close);

        container.appendChild(item);

        var ttl = typeof opts.ttlMs === 'number' ? opts.ttlMs : 8000;
        if (ttl > 0) {
            setTimeout(function () { dismiss(item); }, ttl);
        }
    }

    function dismiss(item) {
        if (!item || !item.parentNode) return;
        item.classList.add('ds-cc-voice-toast--leaving');
        setTimeout(function () {
            if (item.parentNode) item.parentNode.removeChild(item);
        }, 200);
    }

    /* ---------- Bootstrap ------------------------------------------------ */

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    window.CherryVoiceClient = {
        invoke: invoke,
        startListening: startListening,
        stopListening: stopListening,
    };
})();
