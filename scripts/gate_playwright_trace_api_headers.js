const fs = require('fs');
const path = require('path');
const AdmZip = require('adm-zip');

const ROOT = path.join(__dirname, '..');
const TRACE_PATH = path.join(ROOT, 'proof/ui/playwright/trace.zip');
const OUT_DIR = path.join(ROOT, 'proof/runtime/logs');
const TXT_OUT = path.join(OUT_DIR, 'gate_playwright_trace_api_headers_present.txt');
const JSON_OUT = path.join(OUT_DIR, 'gate_playwright_trace_api_headers_present.json');

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

fs.mkdirSync(OUT_DIR, { recursive: true });

if (!fs.existsSync(TRACE_PATH)) {
    const msg = 'FAIL: trace.zip not found at ' + TRACE_PATH;
    console.error(msg);
    fs.writeFileSync(TXT_OUT, msg + '\n');
    fs.writeFileSync(JSON_OUT, JSON.stringify({ pass: false, error: msg }));
    process.exit(1);
}

const zip = new AdmZip(TRACE_PATH);
let allEvents = [];

const networkEntry = zip.getEntry('trace.network');
const traceEntry = zip.getEntry('trace.trace');

for (const entry of [networkEntry, traceEntry].filter(Boolean)) {
    const content = entry.getData().toString('utf8');
    const lines = content.split('\n').filter(l => l.trim());
    for (const line of lines) {
        try {
            allEvents.push(JSON.parse(line));
        } catch(e) {}
    }
}

const apiRequests = [];
for (const evt of allEvents) {
    const req = evt.snapshot?.request || evt.request || {};
    const url = req.url || '';
    if (url.includes('/api/v1/')) {
        const headers = {};
        if (req.headers) {
            for (const h of req.headers) {
                headers[h.name.toLowerCase()] = h.value;
            }
        }
        apiRequests.push({ url, headers });
    }
}

let pass = true;
const failures = [];
const successes = [];

for (let i = 0; i < apiRequests.length; i++) {
    const r = apiRequests[i];
    const missing = [];
    
    const tenantId = r.headers['x-tenant-id'];
    const userId = r.headers['x-user-id'];
    const orgNodeId = r.headers['x-org-node-id'];
    
    if (!tenantId || tenantId !== 'default') missing.push('X-Tenant-Id');
    if (!userId || userId !== 'system@localhost') missing.push('X-User-Id');
    if (!orgNodeId || !UUID_RE.test(orgNodeId)) missing.push('X-Org-Node-Id');
    
    if (missing.length > 0) {
        pass = false;
        failures.push({ index: i, url: r.url, missing });
    } else {
        successes.push({ index: i, url: r.url });
    }
}

const outputLines = [];
outputLines.push('=== GATE: Playwright Trace API Headers Present ===');
outputLines.push(`Trace file: ${TRACE_PATH}`);
outputLines.push(`Total trace events: ${allEvents.length}`);
outputLines.push(`API /api/v1/ requests found: ${apiRequests.length}`);
outputLines.push('');

if (apiRequests.length === 0) {
    outputLines.push('WARNING: No /api/v1/ requests found in trace.');
    outputLines.push('This may indicate the trace was captured before any API calls were made.');
}

for (const s of successes) {
    outputLines.push(`  [OK] request #${s.index}: ${s.url}`);
}

for (const f of failures) {
    outputLines.push(`  [FAIL] request #${f.index}: ${f.url} -- missing: ${f.missing.join(', ')}`);
}

outputLines.push('');
outputLines.push(pass ? 'RESULT: PASS' : 'RESULT: FAIL');

const output = outputLines.join('\n');
console.log(output);
fs.writeFileSync(TXT_OUT, output + '\n');
fs.writeFileSync(JSON_OUT, JSON.stringify({
    pass,
    totalTraceEvents: allEvents.length,
    apiRequestCount: apiRequests.length,
    successCount: successes.length,
    failureCount: failures.length,
    failures
}, null, 2));

if (!pass) process.exit(1);
