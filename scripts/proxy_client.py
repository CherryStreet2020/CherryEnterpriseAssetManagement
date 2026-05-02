#!/usr/bin/env python3
"""
Proxy client for CherryAI API calls.
BASE_URL: http://127.0.0.1:5000
Enforces port 5000 only and /api/v1/ paths.
Every request includes X-Tenant-Id, X-User-Id, X-Org-Node-Id headers.
"""
import os
import sys
import json
import urllib.request
import datetime

BASE_URL = "http://127.0.0.1:5000"
ALLOWED_PORT = "5000"

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LOG_DIR = os.path.join(REPO_ROOT, "proof", "runtime", "logs")
os.makedirs(LOG_DIR, exist_ok=True)
LOG_FILE = os.path.join(LOG_DIR, "proxy_requests.log")

TENANT_ID = "default"
USER_ID = "system@localhost"
ORG_NODE_ID = "7b4f0c36-0ef7-5695-8264-a3df7be80166"

def log_request(method, url, status, body_len):
    ts = datetime.datetime.now(datetime.UTC).isoformat() + "Z"
    entry = (f"{ts} | {method} {url} | status={status} | body_len={body_len} | "
             f"X-Tenant-Id={TENANT_ID} | X-User-Id={USER_ID} | X-Org-Node-Id={ORG_NODE_ID}\n")
    with open(LOG_FILE, "a") as f:
        f.write(entry)

def api_get(path, timeout=30):
    url = f"{BASE_URL}{path}"
    if f":{ALLOWED_PORT}" not in url:
        raise RuntimeError(f"URL does not use allowed port {ALLOWED_PORT}")
    req = urllib.request.Request(url)
    req.add_header("X-Tenant-Id", TENANT_ID)
    req.add_header("X-User-Id", USER_ID)
    req.add_header("X-Org-Node-Id", ORG_NODE_ID)
    req.add_header("Accept", "application/json")
    for attempt in range(3):
        try:
            with urllib.request.urlopen(req, timeout=timeout) as resp:
                body = resp.read().decode("utf-8", errors="replace")
                log_request("GET", url, resp.status, len(body))
                return resp.status, body
        except urllib.error.HTTPError as e:
            log_request("GET", url, e.code, 0)
            return e.code, ""
        except Exception as e:
            if attempt == 2:
                log_request("GET", url, -1, 0)
                return -1, str(e)
            import time; time.sleep(2)
    return -1, ""

def save_json(path, out_file):
    status, body = api_get(path)
    os.makedirs(os.path.dirname(out_file), exist_ok=True)
    if status == 200 and body:
        try:
            parsed = json.loads(body)
        except json.JSONDecodeError:
            parsed = {"raw": body[:2000], "error": "not valid JSON"}
        with open(out_file, "w") as f:
            json.dump(parsed, f, indent=2, default=str)
    else:
        with open(out_file, "w") as f:
            json.dump({"error": f"status={status}", "path": path}, f, indent=2)
    return status

if __name__ == "__main__":
    if len(sys.argv) > 1:
        status, body = api_get(sys.argv[1])
        print(f"Status: {status}")
        print(body[:500] if body else "(empty)")
    else:
        print(f"Proxy client ready. BASE_URL={BASE_URL}")
        print(f"Headers: X-Tenant-Id={TENANT_ID}, X-User-Id={USER_ID}, X-Org-Node-Id={ORG_NODE_ID}")
