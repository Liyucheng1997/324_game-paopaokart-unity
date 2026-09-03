"""Call a single MCP for Unity tool via stdio and print the result.

Usage:
  python unity_call.py <tool_name> '<json-args>'
  python unity_call.py <tool_name> @args.json
"""
import json, subprocess, sys, threading, queue

tool = sys.argv[1]
raw = sys.argv[2] if len(sys.argv) > 2 else "{}"
if raw.startswith("@"):
    with open(raw[1:], encoding="utf-8") as f:
        args = json.load(f)
else:
    args = json.loads(raw)

proc = subprocess.Popen(
    ["uvx", "--from", "mcpforunityserver", "mcp-for-unity", "--transport", "stdio"],
    stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
    text=True, encoding="utf-8", bufsize=1,
)

q = queue.Queue()
def reader():
    for line in proc.stdout:
        line = line.strip()
        if line:
            q.put(line)
threading.Thread(target=reader, daemon=True).start()

def send(obj):
    proc.stdin.write(json.dumps(obj) + "\n")
    proc.stdin.flush()

def recv(timeout=120, want_id=None):
    import time as _t
    end = _t.time() + timeout
    while _t.time() < end:
        try:
            msg = json.loads(q.get(timeout=max(0.1, end - _t.time())))
        except queue.Empty:
            return None
        if want_id is None or msg.get("id") == want_id:
            return msg
    return None

send({"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {
    "protocolVersion": "2025-06-18", "capabilities": {},
    "clientInfo": {"name": "claude-driver", "version": "1.0"}}})
if recv(120, want_id=1) is None:
    print("ERROR: initialize timeout"); sys.exit(1)
send({"jsonrpc": "2.0", "method": "notifications/initialized"})

send({"jsonrpc": "2.0", "id": 2, "method": "tools/call",
      "params": {"name": tool, "arguments": args}})
r = recv(300, want_id=2)
proc.terminate()
if r is None:
    print("ERROR: tool call timeout"); sys.exit(1)
res = r.get("result", r.get("error", r))
# unwrap text content for readability
if isinstance(res, dict) and "content" in res:
    for c in res.get("content", []):
        if c.get("type") == "text":
            print(c["text"])
    if res.get("isError"):
        sys.exit(2)
else:
    print(json.dumps(res, ensure_ascii=False, indent=2))
