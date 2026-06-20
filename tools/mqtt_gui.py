#!/usr/bin/env python3
import json
import os
import queue
import threading
import time
import uuid
import tkinter as tk
from tkinter import messagebox, ttk

from mqtt_test import MqttClient, pretty


CONFIG_PATH = os.path.expanduser("~/.mione_mqtt_gui.json")


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

    def close(self):
        self.stop_event.set()
        try:
            if self.client and self.client.sock:
                self.client.sock.close()
        except Exception:
            pass

    def run(self):
        self.events.put(("log", "Verbinde mit %s:%s ..." % (self.host, self.port)))
        try:
            with MqttClient(self.host, self.port, self.user, self.password, 3.0) as client:
                self.client = client
                self.events.put(("status", "Verbunden: %s:%s" % (self.host, self.port)))
                for topic in (self.root("Melkroboter/#"), self.root("Alarmfunktionen/#")):
                    self.events.put(("log", "Abonniere " + topic))
                    client.subscribe(topic)
                self.events.put(("log", "Abos aktiv. Warte auf MQTT-Daten ..."))

                last_ping = time.time()
                while not self.stop_event.is_set():
                    self.flush_outgoing()
                    for topic, payload in client.messages(0.5):
                        self.events.put(("message", topic, payload))
                    if time.time() - last_ping > 25:
                        client.ping()
                        last_ping = time.time()
        except Exception as exc:
            if not self.stop_event.is_set():
                self.events.put(("error", "%s: %s" % (exc.__class__.__name__, exc)))
        finally:
            self.client = None
            self.events.put(("status", "Getrennt"))

    def flush_outgoing(self):
        while True:
            try:
                topic, payload = self.outgoing.get_nowait()
            except queue.Empty:
                return
            self.client.publish(topic, payload)
            self.events.put(("published", topic, payload))
            self.events.put(("log", "Gesendet: " + topic))

    def root(self, suffix):
        return self.user + "/" + suffix.strip("/")


class MqttGui(tk.Tk):
    def __init__(self):
        tk.Tk.__init__(self)
        self.title("Mione MQTT Monitor")
        self.geometry("1240x780")
        self.minsize(980, 620)

        self.events = queue.Queue()
        self.stop_event = threading.Event()
        self.worker = None
        self.topic_payloads = {}
        self.functions = []
        self.config_data = self.load_config()

        self.host_var = tk.StringVar(value=self.config_data.get("host", "localhost"))
        self.port_var = tk.StringVar(value=str(self.config_data.get("port", 1883)))
        self.user_var = tk.StringVar(value=self.config_data.get("user", ""))
        self.password_var = tk.StringVar()
        self.status_var = tk.StringVar(value="Nicht verbunden")
        self.command_var = tk.StringVar()
        self.box_var = tk.StringVar()
        self.robot_position_var = tk.StringVar()
        self.sampling_box_var = tk.StringVar()

        self.create_widgets()
        self.protocol("WM_DELETE_WINDOW", self.close)
        self.after(150, self.process_events)

    def create_widgets(self):
        top = ttk.Frame(self, padding=10)
        top.pack(fill="x")

        self.add_labeled_entry(top, "MQTT-Server", self.host_var, 0, 0, 24)
        self.add_labeled_entry(top, "Port", self.port_var, 0, 1, 8)
        self.add_labeled_entry(top, "User / Top-Topic", self.user_var, 0, 2, 24)
        self.add_labeled_entry(top, "Passwort", self.password_var, 0, 3, 24, show="*")
        ttk.Button(top, text="Verbinden", command=self.connect).grid(row=1, column=4, padx=(8, 4), sticky="ew")
        ttk.Button(top, text="Trennen", command=self.disconnect).grid(row=1, column=5, sticky="ew")
        for col in (0, 2, 3):
            top.columnconfigure(col, weight=1)

        status = ttk.Frame(self, padding=(10, 0, 10, 8))
        status.pack(fill="x")
        ttk.Label(status, textvariable=self.status_var).pack(side="left")

        tabs = ttk.Notebook(self)
        tabs.pack(fill="both", expand=True, padx=10, pady=(0, 10))
        self.box_tab = ttk.Frame(tabs, padding=8)
        self.command_tab = ttk.Frame(tabs, padding=8)
        self.topic_tab = ttk.Frame(tabs, padding=8)
        self.log_tab = ttk.Frame(tabs, padding=8)
        tabs.add(self.box_tab, text="Boxenstatus")
        tabs.add(self.command_tab, text="Funktionen senden")
        tabs.add(self.topic_tab, text="Topics / JSON")
        tabs.add(self.log_tab, text="Log")

        self.create_box_tab()
        self.create_command_tab()
        self.create_topic_tab()
        self.create_log_tab()

    def add_labeled_entry(self, parent, label, variable, row, column, width, show=None):
        ttk.Label(parent, text=label).grid(row=row, column=column, sticky="w")
        ttk.Entry(parent, textvariable=variable, width=width, show=show).grid(row=row + 1, column=column, sticky="ew", padx=(0, 8))

    def create_box_tab(self):
        columns = ("box", "cow", "attachment", "operation", "operation_text", "box_status", "milk", "updated")
        self.box_tree = ttk.Treeview(self.box_tab, columns=columns, show="headings", height=20)
        headings = {
            "box": "Box", "cow": "Kuh", "attachment": "Ansetzen", "operation": "Betrieb",
            "operation_text": "Betrieb Text", "box_status": "Boxstatus", "milk": "Milch", "updated": "Aktualisiert"
        }
        widths = {"box": 60, "cow": 80, "attachment": 110, "operation": 90, "operation_text": 190, "box_status": 160, "milk": 100, "updated": 120}
        for col in columns:
            self.box_tree.heading(col, text=headings[col])
            self.box_tree.column(col, width=widths[col], anchor="w")
        self.box_tree.pack(side="left", fill="both", expand=True)
        scroll = ttk.Scrollbar(self.box_tab, orient="vertical", command=self.box_tree.yview)
        scroll.pack(side="right", fill="y")
        self.box_tree.configure(yscrollcommand=scroll.set)

    def create_command_tab(self):
        left = ttk.Frame(self.command_tab)
        left.pack(side="left", fill="both", expand=True, padx=(0, 10))
        right = ttk.Frame(self.command_tab)
        right.pack(side="right", fill="both", expand=True)

        columns = ("name", "label", "params")
        self.function_tree = ttk.Treeview(left, columns=columns, show="headings", height=20)
        for col, text, width in (("name", "Name", 210), ("label", "Beschreibung", 300), ("params", "Parameter", 170)):
            self.function_tree.heading(col, text=text)
            self.function_tree.column(col, width=width, anchor="w")
        self.function_tree.pack(fill="both", expand=True)
        self.function_tree.bind("<<TreeviewSelect>>", self.function_selected)

        form = ttk.LabelFrame(right, text="Befehl publishen", padding=10)
        form.pack(fill="x")
        self.add_form_entry(form, "Funktion", self.command_var, 0)
        self.add_form_entry(form, "boxNumber", self.box_var, 1)
        self.add_form_entry(form, "robotPosition", self.robot_position_var, 2)
        self.add_form_entry(form, "samplingBox", self.sampling_box_var, 3)
        ttk.Button(form, text="Befehl senden", command=self.send_command).grid(row=4, column=1, sticky="e", pady=(8, 0))
        form.columnconfigure(1, weight=1)

        ttk.Label(right, text="Payload / Beispiel").pack(anchor="w", pady=(14, 2))
        self.payload_text = tk.Text(right, height=12, wrap="word")
        self.payload_text.pack(fill="both", expand=True)

    def add_form_entry(self, parent, label, variable, row):
        ttk.Label(parent, text=label).grid(row=row, column=0, sticky="w", padx=(0, 8), pady=2)
        ttk.Entry(parent, textvariable=variable).grid(row=row, column=1, sticky="ew", pady=2)

    def create_topic_tab(self):
        left = ttk.Frame(self.topic_tab)
        left.pack(side="left", fill="both", expand=True, padx=(0, 10))
        right = ttk.Frame(self.topic_tab)
        right.pack(side="right", fill="both", expand=True)

        self.topic_tree = ttk.Treeview(left, columns=("topic", "updated", "summary"), show="headings", height=20)
        for col, text, width in (("topic", "Topic", 430), ("updated", "Zeit", 90), ("summary", "Inhalt", 280)):
            self.topic_tree.heading(col, text=text)
            self.topic_tree.column(col, width=width, anchor="w")
        self.topic_tree.pack(fill="both", expand=True)
        self.topic_tree.bind("<<TreeviewSelect>>", self.topic_selected)

        ttk.Label(right, text="JSON / Payload").pack(anchor="w")
        self.detail_text = tk.Text(right, wrap="word")
        self.detail_text.pack(fill="both", expand=True)

    def create_log_tab(self):
        self.log_text = tk.Text(self.log_tab, wrap="word")
        self.log_text.pack(side="left", fill="both", expand=True)
        scroll = ttk.Scrollbar(self.log_tab, orient="vertical", command=self.log_text.yview)
        scroll.pack(side="right", fill="y")
        self.log_text.configure(yscrollcommand=scroll.set)

    def connect(self):
        if self.worker:
            self.log("Bereits verbunden oder Verbindung laeuft.")
            return
        user = self.user_var.get().strip().strip("/")
        if not user:
            messagebox.showerror("MQTT", "Bitte MQTT-User / Top-Topic eintragen.")
            return
        try:
            port = int(self.port_var.get().strip())
        except ValueError:
            messagebox.showerror("MQTT", "Port ist keine Zahl.")
            return
        self.save_config()
        self.stop_event = threading.Event()
        self.worker = MqttWorker(self.host_var.get().strip(), port, user, self.password_var.get(), self.events, self.stop_event)
        self.worker.start()
        self.status_var.set("Verbinde ...")

    def disconnect(self):
        if not self.worker:
            self.status_var.set("Nicht verbunden")
            return
        self.worker.close()
        self.worker = None
        self.status_var.set("Trenne ...")

    def process_events(self):
        while True:
            try:
                event = self.events.get_nowait()
            except queue.Empty:
                break
            kind = event[0]
            if kind == "status":
                self.status_var.set(event[1])
                self.log(event[1])
                if event[1] == "Getrennt":
                    self.worker = None
            elif kind == "error":
                self.status_var.set("Fehler: " + event[1])
                self.log("FEHLER: " + event[1])
                self.worker = None
            elif kind == "log":
                self.log(event[1])
            elif kind == "message":
                self.handle_message(event[1], event[2], False)
            elif kind == "published":
                self.handle_message(event[1] + " [gesendet]", event[2], True)
        self.after(150, self.process_events)

    def handle_message(self, topic, payload, published):
        now = time.strftime("%H:%M:%S")
        self.topic_payloads[topic] = payload
        self.upsert(self.topic_tree, topic, (topic, now, self.summary(payload)))
        self.log(("Gesendet " if published else "Empfangen ") + topic)
        try:
            data = json.loads(payload)
        except Exception:
            return
        boxes = self.extract_boxes(data)
        if boxes:
            self.update_boxes(boxes, now)
        functions = data.get("functions") if isinstance(data, dict) else None
        if isinstance(functions, list):
            self.update_functions(functions)

    def update_boxes(self, boxes, updated):
        for box in boxes:
            if not isinstance(box, dict):
                continue
            box_number = self.text(box, "boxNumber") or self.text(box, "BoxNumber")
            if not box_number:
                continue
            milk = self.text(box, "milkYield") or self.text(box, "MilkYield")
            expected = self.text(box, "expectedMilkYield") or self.text(box, "ExpectedMilkYield")
            if expected:
                milk = milk + " / " + expected if milk else expected
            values = (
                box_number,
                self.text(box, "cowNumber") or self.text(box, "CowNumber"),
                self.text(box, "attachmentStatus") or self.text(box, "AttachmentStatus"),
                self.text(box, "operationStatus") or self.text(box, "OperationStatus"),
                self.text(box, "operationStatusText") or self.text(box, "OperationStatusText"),
                self.text(box, "boxStatusText") or self.text(box, "BoxStatusText") or self.text(box, "boxStatus"),
                milk,
                updated,
            )
            self.upsert(self.box_tree, box_number, values)

    def update_functions(self, functions):
        self.functions = functions
        self.function_tree.delete(*self.function_tree.get_children())
        for item in functions:
            if not isinstance(item, dict):
                continue
            params = item.get("parameters") or []
            param_names = []
            for param in params:
                if isinstance(param, dict) and param.get("name"):
                    param_names.append(str(param.get("name")))
            name = str(item.get("name", ""))
            self.function_tree.insert("", "end", iid=name, values=(name, item.get("label", ""), ", ".join(param_names)))
        self.log("%s Melkroboter-Funktionen geladen." % len(functions))

    def send_command(self):
        if not self.worker:
            messagebox.showerror("MQTT", "Bitte zuerst verbinden.")
            return
        command = self.command_var.get().strip()
        if not command:
            messagebox.showerror("MQTT", "Bitte Funktion auswählen oder eintragen.")
            return
        payload = {"requestId": uuid.uuid4().hex[:8], "command": command}
        self.add_optional_number(payload, "boxNumber", self.box_var.get())
        self.add_optional_number(payload, "robotPosition", self.robot_position_var.get())
        self.add_optional_number(payload, "samplingBox", self.sampling_box_var.get())
        text = json.dumps(payload, ensure_ascii=False, separators=(",", ":"))
        self.worker.publish("Melkroboter/Command", text)
        self.show_payload(text)

    def function_selected(self, event):
        selected = self.function_tree.selection()
        if not selected:
            return
        name = selected[0]
        self.command_var.set(name)
        for item in self.functions:
            if isinstance(item, dict) and item.get("name") == name:
                self.show_payload(item.get("payloadExample", ""))
                return

    def topic_selected(self, event):
        selected = self.topic_tree.selection()
        if not selected:
            return
        payload = self.topic_payloads.get(selected[0], "")
        self.detail_text.delete("1.0", "end")
        self.detail_text.insert("1.0", pretty(payload))

    def show_payload(self, payload):
        self.payload_text.delete("1.0", "end")
        self.payload_text.insert("1.0", pretty(payload))

    def extract_boxes(self, data):
        if not isinstance(data, dict):
            return []
        if isinstance(data.get("boxes"), list):
            return data.get("boxes")
        nested = data.get("data")
        if isinstance(nested, dict) and isinstance(nested.get("boxes"), list):
            return nested.get("boxes")
        return []

    def summary(self, payload):
        try:
            data = json.loads(payload)
            if isinstance(data, dict):
                if data.get("type"):
                    return str(data.get("type"))
                return ", ".join(list(data.keys())[:4])
            if isinstance(data, list):
                return str(len(data)) + " Eintraege"
        except Exception:
            pass
        return payload.replace("\n", " ")[:80]

    def upsert(self, tree, item_id, values):
        item_id = str(item_id)
        if tree.exists(item_id):
            tree.item(item_id, values=values)
        else:
            tree.insert("", "end", iid=item_id, values=values)

    def text(self, values, key):
        value = values.get(key, "")
        return "" if value is None else str(value)

    def add_optional_number(self, payload, key, value):
        value = value.strip()
        if not value:
            return
        try:
            payload[key] = int(value)
        except ValueError:
            payload[key] = value

    def log(self, text):
        stamp = time.strftime("%H:%M:%S")
        self.log_text.insert("end", "[%s] %s\n" % (stamp, text))
        self.log_text.see("end")

    def load_config(self):
        try:
            with open(CONFIG_PATH, "r", encoding="utf-8") as handle:
                return json.load(handle)
        except Exception:
            return {}

    def save_config(self):
        data = {
            "host": self.host_var.get().strip(),
            "port": int(self.port_var.get().strip() or "1883"),
            "user": self.user_var.get().strip(),
        }
        try:
            with open(CONFIG_PATH, "w", encoding="utf-8") as handle:
                json.dump(data, handle, ensure_ascii=False, indent=2)
        except Exception:
            pass

    def close(self):
        self.disconnect()
        self.destroy()


if __name__ == "__main__":
    MqttGui().mainloop()
