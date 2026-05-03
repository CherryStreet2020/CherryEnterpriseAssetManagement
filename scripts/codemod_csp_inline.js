#!/usr/bin/env node
// codemod_csp_inline.js — codemod tooling for Task #18
// (remove `script-src-attr 'unsafe-inline'` and `style-src-attr 'unsafe-inline'`).
//
// SAFE BY DEFAULT: dry-run unless `--apply` is passed. Always writes
// per-file backups under out/csp_codemod_backup/ before mutating.
//
// Usage:
//   node scripts/codemod_csp_inline.js --pass=styles                 # dry-run, styles only
//   node scripts/codemod_csp_inline.js --pass=styles   --apply
//   node scripts/codemod_csp_inline.js --pass=handlers --apply --strategy=auto
//   node scripts/codemod_csp_inline.js --pass=handlers --apply --strategy=sibling   --limit-to=Pages/Index.cshtml
//
// SIBLING MODE IS REPORT-ONLY (today). The codemod cannot reliably locate
// the matching `>` of an open-tag whose attribute list contains Razor
// expressions, so it does NOT mutate the file in sibling mode — it only
// emits manual-review entries (`sibling-pending`). To actually apply
// sibling-strategy rewrites, hand-edit using those entries as a worklist
// (or extend the codemod with a real Razor-aware HTML parser first).
//
// `--limit-to=<substr>`: substring match against the file path (NOT a
// glob). Pass e.g. `--limit-to=Pages/Maintenance/` to scope to a folder
// or `--limit-to=Index.cshtml` to scope to a specific file name.
//   node scripts/codemod_csp_inline.js --reverse --pass=styles --apply
//
// Passes:
//   --pass=styles    Rewrite ` style="X"` → ` data-csp-style="X"` in
//                    Razor markup, skipping <style>...</style> bodies.
//   --pass=handlers  Rewrite ` on{event}="X"` → either:
//                      * sibling-script binding (default outside table/select), or
//                      * delegated-action attributes (inside <table>/<tr>/<thead>/
//                        <tbody>/<select>/<optgroup>) — handler bodies that don't
//                        match the registered actions in wwwroot/js/csp-bootstrap.js
//                        are dumped to out/csp_handler_manual_review.tsv.
//
// Reverse mode: --reverse will undo a pass using the per-file backups.
//
// This file is intentionally dependency-free (pure Node stdlib) so it can
// run on any contributor box with no `npm install` required.

const fs = require('fs');
const path = require('path');

const ROOT = process.cwd();
const PAGES_DIR = path.join(ROOT, 'Pages');
const BACKUP_DIR = path.join(ROOT, 'out', 'csp_codemod_backup');
const REVIEW_TSV = path.join(ROOT, 'out', 'csp_handler_manual_review.tsv');

// Hoisted: passHandlers (invoked from the top-level loop below) reads
// this via matchKnownAction, so it must initialize before the loop runs.
const KNOWN_ACTIONS = {
    "^window\\.location\\s*=\\s*'([^']+)'$":                          ['navigate',     m => ({ href: m[1] })],
    '^window\\.location\\s*=\\s*"([^"]+)"$':                          ['navigate',     m => ({ href: m[1] })],
    '^window\\.location\\.href\\s*=\\s*\'([^\']+)\'$':                ['navigate',     m => ({ href: m[1] })],
    '^this\\.form\\.submit\\(\\);?$':                                 ['submitContainingForm', () => ({})],
    "^document\\.getElementById\\('([^']+)'\\)\\.submit\\(\\);?$":    ['submitForm',   m => ({ formId: m[1] })],
    "^document\\.getElementById\\('([^']+)'\\)\\.click\\(\\);?$":     ['clickById',    m => ({ targetId: m[1] })],
    "^document\\.getElementById\\('([^']+)'\\)\\.toggleAttribute\\('hidden'\\);?$": ['toggleHidden', m => ({ targetId: m[1] })],
    "^document\\.getElementById\\('([^']+)'\\)\\.setAttribute\\('hidden',''\\);?$": ['setHidden',    m => ({ targetId: m[1] })],
    "^document\\.getElementById\\('([^']+)'\\)\\.style\\.display='none';?$":        ['hide',         m => ({ targetId: m[1] })],
    "^history\\.back\\(\\);?$":                                       ['back',         () => ({})]
};

// --------------------------------------------------------------------- args
const args = parseArgs(process.argv.slice(2));
if (!args.pass || !['styles', 'handlers'].includes(args.pass)) {
    console.error('Usage: --pass=styles|handlers [--apply] [--reverse] [--strategy=auto|sibling|delegated] [--limit-to=<glob>]');
    process.exit(2);
}

const APPLY = !!args.apply;
const REVERSE = !!args.reverse;
const STRATEGY = args.strategy || 'auto';
const LIMIT_TO = args['limit-to'] || null;

if (!fs.existsSync(BACKUP_DIR)) fs.mkdirSync(BACKUP_DIR, { recursive: true });

// ---------------------------------------------------------------- file walk
const files = walkCshtml(PAGES_DIR).filter(f => !LIMIT_TO || f.includes(LIMIT_TO));

let totalEdits = 0;
let touchedFiles = 0;
const reviewLines = [];

for (const file of files) {
    const original = fs.readFileSync(file, 'utf8');
    let mutated;

    if (REVERSE) {
        mutated = restoreFromBackup(file);
        if (mutated == null) continue;
    } else if (args.pass === 'styles') {
        mutated = passStyles(original);
    } else {
        const result = passHandlers(original, file);
        mutated = result.text;
        for (const r of result.review) reviewLines.push(r);
    }

    if (mutated === original) continue;
    touchedFiles++;
    const editCount = countEdits(original, mutated, args.pass);
    totalEdits += editCount;

    if (APPLY) {
        backup(file, original);
        fs.writeFileSync(file, mutated, 'utf8');
        process.stdout.write(`MOD ${path.relative(ROOT, file)} (${editCount} edits)\n`);
    } else {
        process.stdout.write(`DRY ${path.relative(ROOT, file)} (${editCount} edits)\n`);
    }
}

if (reviewLines.length) {
    if (APPLY) {
        fs.writeFileSync(REVIEW_TSV, 'file\tline\tparent_tag\tattr\tbody\n' + reviewLines.join('\n') + '\n');
        process.stdout.write(`\nManual-review queue (${reviewLines.length}): ${path.relative(ROOT, REVIEW_TSV)}\n`);
    } else {
        process.stdout.write(`\n[dry-run] would write ${reviewLines.length} entries to manual-review queue\n`);
    }
}

process.stdout.write(`\n${APPLY ? 'Applied' : 'Dry-run'}: pass=${args.pass} files=${touchedFiles} edits=${totalEdits}\n`);

// ===================================================================
// passes
// ===================================================================

// ----- styles -----
// Replace ` style="…"` with ` data-csp-style="…"`, except inside
// <style>…</style> bodies and inside Razor strings.
function passStyles(src) {
    const styleBlockRanges = findStyleElementRanges(src);
    return rewriteAttributes(src, /\s+style\s*=\s*"([^"]*)"/g, (full, value, offset) => {
        if (offsetInsideRanges(offset, styleBlockRanges)) return full;
        return ` data-csp-style="${value}"`;
    });
}

// ----- handlers -----
// Map each ` on{event}="…"` to a rewrite, choosing strategy per-callsite.
// Sibling strategy emits a Razor `@{ var __c = $"csp_{Guid.NewGuid():N}"; }`
// preamble + a companion `<script>` block right after the element close-tag.
// Delegated strategy rewrites the attributes in place; the body is matched
// against the registry in wwwroot/js/csp-bootstrap.js (kept in sync with
// KNOWN_ACTIONS at the top of this file). Unmatched delegated handlers
// are queued for manual review.
function passHandlers(src, file) {
    const review = [];
    const rel = path.relative(ROOT, file);
    const handlerRe = /\s+on([a-z]+)\s*=\s*"([^"]*)"/g;
    let out = '';
    let cursor = 0;
    let match;
    let bindingCounter = 0;

    while ((match = handlerRe.exec(src)) !== null) {
        out += src.slice(cursor, match.index);
        const [full, eventName, body] = match;
        const before = src.slice(0, match.index);
        const insideForbidden = isInsideForbiddenForSibling(before);
        const lineNo = before.split('\n').length;

        const decision =
            STRATEGY === 'sibling'   ? 'sibling' :
            STRATEGY === 'delegated' ? 'delegated' :
            insideForbidden          ? 'delegated' : 'sibling';

        if (decision === 'sibling') {
            // SAFETY: emitting a correct companion `<script>` requires
            // locating the matching `>` of the open-tag, which is unsafe
            // in the presence of Razor inside attribute values. Rather
            // than risk silently breaking interactivity by stripping the
            // handler without binding it, we keep the inline attribute
            // verbatim and queue the call-site for hand-rewrite. This
            // means sibling mode is currently REPORT-ONLY — the codemod
            // never destroys interactivity in this branch.
            out += full;
            bindingCounter++;
            review.push([rel, lineNo, 'sibling-pending', `on${eventName}`, body].join('\t'));
        } else {
            const matched = matchKnownAction(body);
            if (matched) {
                const [action, argsObj] = matched;
                let attrs = ` data-csp-action="${action}" data-csp-on="${eventName}"`;
                for (const k of Object.keys(argsObj)) {
                    attrs += ` data-csp-arg-${camelToKebab(k)}="${escapeAttr(argsObj[k])}"`;
                }
                out += attrs;
            } else {
                // No registry match — keep the inline handler for now and
                // queue for manual review. The migration cannot complete
                // until every entry here is hand-rewritten.
                out += full;
                review.push([rel, lineNo, 'delegated-unmatched', `on${eventName}`, body].join('\t'));
            }
        }

        cursor = match.index + full.length;
    }
    out += src.slice(cursor);
    return { text: out, review };
}

function matchKnownAction(body) {
    const trimmed = body.trim();
    for (const pattern of Object.keys(KNOWN_ACTIONS)) {
        const re = new RegExp(pattern);
        const m = trimmed.match(re);
        if (m) {
            const [action, extract] = KNOWN_ACTIONS[pattern];
            return [action, extract(m)];
        }
    }
    return null;
}

// Approximate "is this offset inside a <table>/<tr>/<thead>/<tbody>/<select>/
// <optgroup> open scope?" — true when the most recent unbalanced opening
// tag of one of those names appears before any matching close.
function isInsideForbiddenForSibling(before) {
    const TAGS = ['table', 'thead', 'tbody', 'tr', 'select', 'optgroup'];
    let depth = Object.fromEntries(TAGS.map(t => [t, 0]));
    const tagRe = /<\/?([a-zA-Z][a-zA-Z0-9]*)\b[^>]*>/g;
    let m;
    while ((m = tagRe.exec(before)) !== null) {
        const name = m[1].toLowerCase();
        if (!TAGS.includes(name)) continue;
        if (m[0].startsWith('</')) depth[name] = Math.max(0, depth[name] - 1);
        else if (!m[0].endsWith('/>')) depth[name]++;
    }
    return TAGS.some(t => depth[t] > 0);
}

// ===================================================================
// helpers
// ===================================================================

function rewriteAttributes(src, regex, fn) {
    let out = '';
    let cursor = 0;
    let m;
    while ((m = regex.exec(src)) !== null) {
        out += src.slice(cursor, m.index);
        out += fn(m[0], m[1], m.index);
        cursor = m.index + m[0].length;
    }
    out += src.slice(cursor);
    return out;
}

function findStyleElementRanges(src) {
    const ranges = [];
    const re = /<style\b[^>]*>([\s\S]*?)<\/style>/gi;
    let m;
    while ((m = re.exec(src)) !== null) ranges.push([m.index, m.index + m[0].length]);
    return ranges;
}

function offsetInsideRanges(offset, ranges) {
    for (const [a, b] of ranges) if (offset >= a && offset < b) return true;
    return false;
}

function escapeAttr(v) {
    return String(v).replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;');
}

function camelToKebab(s) { return s.replace(/[A-Z]/g, c => '-' + c.toLowerCase()); }

function jsonStringForRazor(s) { return JSON.stringify(s); }

function countEdits(a, b, pass) {
    if (pass === 'styles') return (a.match(/\s+style\s*=\s*"/g) || []).length - (b.match(/\s+style\s*=\s*"/g) || []).length;
    return (a.match(/\s+on[a-z]+\s*=\s*"/g) || []).length - (b.match(/\s+on[a-z]+\s*=\s*"/g) || []).length;
}

function backup(file, content) {
    const rel = path.relative(ROOT, file);
    const dest = path.join(BACKUP_DIR, rel);
    fs.mkdirSync(path.dirname(dest), { recursive: true });
    if (!fs.existsSync(dest)) fs.writeFileSync(dest, content, 'utf8');
}

function restoreFromBackup(file) {
    const rel = path.relative(ROOT, file);
    const src = path.join(BACKUP_DIR, rel);
    if (!fs.existsSync(src)) return null;
    return fs.readFileSync(src, 'utf8');
}

function walkCshtml(dir) {
    const out = [];
    for (const ent of fs.readdirSync(dir, { withFileTypes: true })) {
        const p = path.join(dir, ent.name);
        if (ent.isDirectory()) out.push(...walkCshtml(p));
        else if (ent.isFile() && ent.name.endsWith('.cshtml')) out.push(p);
    }
    return out;
}

function parseArgs(argv) {
    const out = {};
    for (const a of argv) {
        const m = a.match(/^--([^=]+)(?:=(.*))?$/);
        if (!m) continue;
        out[m[1]] = m[2] === undefined ? true : m[2];
    }
    return out;
}
