#!/usr/bin/env python3
import json
import os
import queue
import threading
import time
import uuid
import webbrowser
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

from mqtt_test import MqttClient


CONFIG_PATH = os.path.expanduser("~/.mione_mqtt_gui.json")
STATE = None


HTML = r"""<!doctype html>
<html lang="de">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Mione MQTT Monitor</title>
<style>
:root { color-scheme: light dark; --line: #c9ced6; --soft: #eef1f4; --accent: #2563eb; --danger: #b91c1c; }
body { margin: 0; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; font-size: 14px; }
header { display: grid; grid-template-columns: 1.2fr .4fr 1fr 1fr auto auto; gap: 8px; padding: 12px; border-bottom: 1px solid var(--line); align-items: end; }
label { display: grid; gap: 3px; font-size: 12px; color: #667085; }
input, select, button, textarea { font: inherit; }
input, select { padding: 7px 8px; border: 1px solid var(--line); border-radius: 6px; }
button { padding: 8px 11px; border: 1px solid var(--line); border-radius: 6px; background: Canvas; cursor: pointer; }
button.primary { background: var(--accent); border-color: var(--accent); color: white; }
button.danger { color: var(--danger); }
#status { padding: 8px 12px; border-bottom: 1px solid var(--line); }
main { display: grid; grid-template-columns: minmax(420px, 1.4fr) minmax(360px, 1fr); gap: 12px; padding: 12px; }
section { border: 1px solid var(--line); border-radius: 8px; overflow: hidden; min-height: 120px; }
h2 { margin: 0; padding: 8px 10px; font-size: 14px; border-bottom: 1px solid var(--line); background: var(--soft); color: #344054; }
table { border-collapse: collapse; width: 100%; }
th, td { padding: 7px 8px; border-bottom: 1px solid var(--line); text-align: left; vertical-align: top; white-space: nowrap; }
th { font-size: 12px; color: #667085; background: color-mix(in srgb, var(--soft) 60%, Canvas); }
tr:hover { background: color-mix(in srgb, var(--soft) 50%, Canvas); }
.panel { padding: 10px; display: grid; gap: 8px; }
.cmd-grid { display: grid; grid-template-columns: 1fr 110px 110px 110px auto; gap: 8px; align-items: end; }
pre, textarea { box-sizing: border-box; width: 100%; min-height: 220px; margin: 0; padding: 10px; border: 0; border-top: 1px solid var(--line); background: Canvas; overflow: auto; }
.small { color: #667085; font-size: 12px; }
@media (max-width: 980px) { header, main, .cmd-grid { grid-template-columns: 1fr; } }
</style>
</head>
<body>
<header>
  <label>MQTT-Server<input id="host" value="localhost"></label>
  <label>Port<input id="port" value="1883"></label>
  <label>User / Top-Topic<input id="user"></label>
  <label>Passwort<input id="password" type="password"></label>
  <button class="primary" onclick="connect()">Verbinden</button>
  <button class="danger" onclick="disconnect()">Trennen</button>
</header>
<div id="status">Nicht verbunden</div>
<main>
  <div>
    <section>
      <h2>Boxenstatus</h2>
      <table>
        <thead><tr><th>Box</th><th>Kuh</th><th>Ansetzen</th><th>Betrieb</th><th>Text</th><th>Boxstatus</th><th>Milch</th><th>Zeit</th></tr></thead>
        <tbody id="boxes"></tbody>
      </table>
    </section>
    <section style="margin-top:12px">
      <h2>Melkroboter-Funktionen</h2>
      <div class="panel">
        <div class="cmd-grid">
          <label>Funktion<select id="command"></select></label>
          <label>boxNumber<input id="boxNumber"></label>
          <label>robotPosition<input id="robotPosition"></label>
          <label>samplingBox<input id="samplingBox"></label>
          <button class="primary" onclick="sendCommand()">Senden</button>
        </div>
        <div class="small" id="commandInfo">Warte auf Funktionskatalog ...</div>
      </div>
      <table>
        <thead><tr><th>Name</th><th>Beschreibung</th><th>Parameter</th></tr></thead>
        <tbody id="functions"></tbody>
      </table>
    </section>
  </div>
  <div>
    <section>
      <h2>Topics</h2>
      <table>
        <thead><tr><th>Topic</th><th>Zeit</th><th>Typ/Inhalt</th></tr></thead>
        <tbody id="topics"></tbody>
      </table>
    </section>
    <section style="margin-top:12px">
      <h2>JSON / Payload</h2>
      <pre id="detail"></pre>
    </section>
  </div>
</main>
<script>
const state = { topics: new Map(), boxes: new Map(), functions: [] };
const evt = new EventSource('/events');
evt.onmessage = (event) => handleEvent(JSON.parse(event.data));

async function api(path, data) {
  const res = await fetch(path, { method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify(data || {}) });
  const body = await res.json();
  if (!res.ok) throw new Error(body.error || res.statusText);
  return body;
}
async function loadConfig() {
  const res = await fetch('/config');
  const cfg = await res.json();
  host.value = cfg.host || 'localhost'; port.value = cfg.port || 1883; user.value = cfg.user || '';
}
async function connect() {
  try { await api('/connect', {host: host.value, port: Number(port.value), user: user.value, password: password.value}); }
  catch (err) { alert(err.message); }
}
async function disconnect() { await api('/disconnect'); }
async function sendCommand() {
  const payload = { requestId: crypto.randomUUID().slice(0, 8), command: command.value };
  addNumber(payload, 'boxNumber'); addNumber(payload, 'robotPosition'); addNumber(payload, 'samplingBox');
  try { await api('/publish-command', payload); detail.textContent = JSON.stringify(payload, null, 2); }
  catch (err) { alert(err.message); }
}
function addNumber(payload, key) {
  const value = document.getElementById(key).value.trim();
  if (!value) return;
  const number = Number(value);
  payload[key] = Number.isFinite(number) ? number : value;
}
function handleEvent(event) {
  if (event.kind === 'status') status.textContent = event.text;
  if (event.kind === 'error') status.textContent = 'Fehler: ' + event.text;
  if (event.kind === 'message' || event.kind === 'published') handleMessage(event.topic, event.payload, event.kind === 'published');
}
function handleMessage(topic, payload, published) {
  const stamp = new Date().toLocaleTimeString();
  let parsed = null;
  try { parsed = JSON.parse(payload); } catch {}
  state.topics.set(topic, {topic, stamp, payload, parsed, published});
  renderTopics();
  if (parsed) {
    const boxes = extractBoxes(parsed);
    if (boxes.length) {
      for (const box of boxes) updateBox(box, stamp);
      renderBoxes();
    }
    if (Array.isArray(parsed.functions)) {
      state.functions = parsed.functions;
      renderFunctions();
    }
  }
}
function extractBoxes(data) {
  if (Array.isArray(data.boxes)) return data.boxes;
  if (data.data && Array.isArray(data.data.boxes)) return data.data.boxes;
  return [];
}
function updateBox(box, stamp) {
  const id = text(box.boxNumber ?? box.BoxNumber);
  if (!id) return;
  state.boxes.set(id, {...box, _stamp: stamp});
}
function renderBoxes() {
  const rows = [...state.boxes.entries()].sort((a, b) => Number(a[0]) - Number(b[0]));
  boxes.innerHTML = rows.map(([id, b]) => `<tr><td>${esc(id)}</td><td>${esc(b.cowNumber ?? b.CowNumber)}</td><td>${esc(b.attachmentStatus ?? b.AttachmentStatus)}</td><td>${esc(b.operationStatus ?? b.OperationStatus)}</td><td>${esc(b.operationStatusText ?? b.OperationStatusText)}</td><td>${esc(b.boxStatusText ?? b.BoxStatusText ?? b.boxStatus)}</td><td>${esc(milk(b))}</td><td>${esc(b._stamp)}</td></tr>`).join('');
}
function renderFunctions() {
  command.innerHTML = '';
  functions.innerHTML = state.functions.map(fn => {
    const option = document.createElement('option');
    option.value = fn.name || ''; option.textContent = fn.name || '';
    command.appendChild(option);
    return `<tr onclick="selectFunction('${escAttr(fn.name || '')}')"><td>${esc(fn.name)}</td><td>${esc(fn.label)}</td><td>${esc(paramNames(fn).join(', '))}</td></tr>`;
  }).join('');
  commandInfo.textContent = state.functions.length + ' Funktionen gefunden';
}
function selectFunction(name) {
  command.value = name;
  const fn = state.functions.find(item => item.name === name);
  commandInfo.textContent = fn && fn.payloadExample ? fn.payloadExample : '';
}
function renderTopics() {
  const rows = [...state.topics.values()].sort((a, b) => a.topic.localeCompare(b.topic));
  topics.innerHTML = rows.map(item => `<tr onclick="showTopic('${escAttr(item.topic)}')"><td>${esc(item.topic)}${item.published ? ' [gesendet]' : ''}</td><td>${esc(item.stamp)}</td><td>${esc(summary(item))}</td></tr>`).join('');
}
function showTopic(topic) {
  const item = state.topics.get(topic);
  if (!item) return;
  detail.textContent = item.parsed ? JSON.stringify(item.parsed, null, 2) : item.payload;
}
function summary(item) {
  if (item.parsed && item.parsed.type) return item.parsed.type;
  if (item.parsed && typeof item.parsed === 'object') return Object.keys(item.parsed).slice(0, 4).join(', ');
  return item.payload.slice(0, 80);
}
function paramNames(fn) {
  return Array.isArray(fn.parameters) ? fn.parameters.map(p => p.name).filter(Boolean) : [];
}
function milk(b) {
  const current = b.milkYield ?? b.MilkYield ?? '';
  const expected = b.expectedMilkYield ?? b.ExpectedMilkYield ?? '';
  return expected ? `${current} / ${expected}` : current;
}
function text(value) { return value === undefined || value === null ? '' : String(value); }
function esc(value) { return text(value).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c])); }
function escAttr(value) { return esc(value).replace(/`/g, '&#96;'); }
loadConfig();
</script>
</body>
</html>
"""


class MqttWorker(threading.Thread):
    def __init__(self, host, port, user, password, events, stop_event):
        threading.Thread.__init__(self)
        self.daemon = True
        self.host = host
        self.port = port
        self.user = user.strip().strip("/")
        self.password = password
        self.events = events
        self.stop_event = stop_event
        self.outgoing = queue.Queue()
        self.client = None

    def publish(self, suffix, payload):
        self.outgoing.put((self.root(suffix), payload))

    def run(self):
        try:
            with MqttClient(self.host, self.port, self.user, self.password, 3.0) as client:
                self.client = client
                client.subscribe(self.root("Melkroboter/#"))
                client.subscribe(self.root("Alarmfunktionen/#"))
                self.events.put({"kind": "status", "text": "Verbunden: %s:%s" % (self.host, self.port)})
                last_ping = time.time()
                while not self.stop_event.is_set():
                    self.flush_outgoing()
                    for topic, payload in client.messages(0.5):
                        self.events.put({"kind": "message", "topic": topic, "payload": payload})
                    if time.time() - last_ping > 25:
                        client.ping()
                        last_ping = time.time()
        except Exception as exc:
            if not self.stop_event.is_set():
                self.events.put({"kind": "error", "text": str(exc)})
        finally:
            self.events.put({"kind": "status", "text": "Getrennt"})

    def flush_outgoing(self):
        while True:
            try:
                topic, payload = self.outgoing.get_nowait()
            except queue.Empty:
                return
            self.client.publish(topic, payload)
            self.events.put({"kind": "published", "topic": topic, "payload": payload})

    def root(self, suffix):
        return self.user + "/" + suffix.strip("/")


class AppState:
    def __init__(self):
        self.lock = threading.Lock()
        self.events = []
        self.clients = []
        self.stop_event = threading.Event()
        self.worker = None
        self.config = self.load_config()

    def load_config(self):
        try:
            with open(CONFIG_PATH, "r", encoding="utf-8") as handle:
                return json.load(handle)
        except Exception:
            return {"host": "localhost", "port": 1883, "user": ""}

    def save_config(self, host, port, user):
        self.config = {"host": host, "port": port, "user": user}
        try:
            with open(CONFIG_PATH, "w", encoding="utf-8") as handle:
                json.dump(self.config, handle, ensure_ascii=False, indent=2)
        except Exception:
            pass

    def connect(self, host, port, user, password):
        self.disconnect()
        self.save_config(host, port, user)
        self.stop_event = threading.Event()
        self.worker = MqttWorker(host, port, user, password, self, self.stop_event)
        self.worker.start()

    def disconnect(self):
        if self.worker:
            self.stop_event.set()
            self.worker = None

    def publish_command(self, payload):
        if not self.worker:
            raise RuntimeError("Nicht verbunden")
        if not payload.get("requestId"):
            payload["requestId"] = uuid.uuid4().hex[:8]
        if not payload.get("command"):
            raise RuntimeError("command fehlt")
        self.worker.publish("Melkroboter/Command", json.dumps(payload, ensure_ascii=False, separators=(",", ":")))

    def put(self, event):
        with self.lock:
            self.events.append(event)
            self.events = self.events[-200:]
            clients = list(self.clients)
        for client in clients:
            client.put(event)


class Handler(BaseHTTPRequestHandler):
    def log_message(self, fmt, *args):
        return

    def do_GET(self):
        if self.path == "/":
            self.respond(200, HTML.encode("utf-8"), "text/html; charset=utf-8")
            return
        if self.path == "/config":
            self.json_response(STATE.config)
            return
        if self.path == "/events":
            self.events()
            return
        self.respond(404, b"Not found", "text/plain")

    def do_POST(self):
        try:
            data = self.read_json()
            if self.path == "/connect":
                STATE.connect(str(data.get("host", "localhost")), int(data.get("port", 1883)), str(data.get("user", "")), str(data.get("password", "")))
                self.json_response({"ok": True})
                return
            if self.path == "/disconnect":
                STATE.disconnect()
                self.json_response({"ok": True})
                return
            if self.path == "/publish-command":
                STATE.publish_command(data)
                self.json_response({"ok": True})
                return
            self.json_response({"error": "Unbekannter Endpunkt"}, 404)
        except Exception as exc:
            self.json_response({"error": str(exc)}, 400)

    def events(self):
        client = queue.Queue()
        with STATE.lock:
            STATE.clients.append(client)
            backlog = list(STATE.events[-20:])
        self.send_response(200)
        self.send_header("Content-Type", "text/event-stream")
        self.send_header("Cache-Control", "no-cache")
        self.send_header("Connection", "keep-alive")
        self.end_headers()
        try:
            for event in backlog:
                self.write_event(event)
            while True:
                self.write_event(client.get(timeout=30))
        except Exception:
            with STATE.lock:
                if client in STATE.clients:
                    STATE.clients.remove(client)

    def write_event(self, event):
        data = json.dumps(event, ensure_ascii=False).encode("utf-8")
        self.wfile.write(b"data: " + data + b"\n\n")
        self.wfile.flush()

    def read_json(self):
        length = int(self.headers.get("Content-Length", "0"))
        if length <= 0:
            return {}
        return json.loads(self.rfile.read(length).decode("utf-8"))

    def json_response(self, data, status=200):
        self.respond(status, json.dumps(data, ensure_ascii=False).encode("utf-8"), "application/json; charset=utf-8")

    def respond(self, status, body, content_type):
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)


def main():
    global STATE
    STATE = AppState()
    server = ThreadingHTTPServer(("127.0.0.1", 8765), Handler)
    url = "http://127.0.0.1:8765"
    print("Mione MQTT Monitor startet auf", url)
    webbrowser.open(url)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        STATE.disconnect()
        server.server_close()


if __name__ == "__main__":
    main()
