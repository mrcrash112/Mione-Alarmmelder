#!/usr/bin/env python3
import argparse
import json
import socket
import sys
import time
import uuid


def enc_string(value):
    data = (value or "").encode("utf-8")
    return len(data).to_bytes(2, "big") + data


def enc_remaining_length(length):
    out = bytearray()
    while True:
        digit = length % 128
        length //= 128
        if length:
            digit |= 128
        out.append(digit)
        if not length:
            return bytes(out)


def send_packet(sock, header, body=b""):
    sock.sendall(bytes([header]) + enc_remaining_length(len(body)) + body)


def read_packet(sock):
    first = sock.recv(1)
    if not first:
        raise EOFError("MQTT-Verbindung wurde geschlossen")

    multiplier = 1
    length = 0
    while True:
        raw = sock.recv(1)
        if not raw:
            raise EOFError("MQTT-Paket unvollstaendig")
        digit = raw[0]
        length += (digit & 127) * multiplier
        multiplier *= 128
        if multiplier > 128 * 128 * 128 * 128:
            raise ValueError("Ungueltige MQTT-Paketlaenge")
        if not digit & 128:
            break

    body = bytearray()
    while len(body) < length:
        chunk = sock.recv(length - len(body))
        if not chunk:
            raise EOFError("MQTT-Paket unvollstaendig")
        body.extend(chunk)
    return first[0], bytes(body)


def mqtt_connect_error(code):
    errors = {
        1: "ungueltige Protokollversion",
        2: "Client-ID abgelehnt",
        3: "Server nicht verfuegbar",
        4: "Benutzername oder Passwort falsch",
        5: "nicht autorisiert",
    }
    return errors.get(code, "unbekannter Fehler")


class MqttClient:
    def __init__(self, host, port, username, password, timeout):
        self.host = host
        self.port = port
        self.username = username or ""
        self.password = password or ""
        self.timeout = timeout
        self.sock = None
        self.packet_id = 1

    def __enter__(self):
        self.sock = socket.create_connection((self.host, self.port), self.timeout)
        self.sock.settimeout(self.timeout)
        flags = 0x02
        if self.username or self.password:
            flags |= 0x80
        if self.password:
            flags |= 0x40

        client_id = "mione-mac-test-" + uuid.uuid4().hex[:8]
        body = enc_string("MQTT") + bytes([4, flags, 0, 60]) + enc_string(client_id)
        if self.username or self.password:
            body += enc_string(self.username)
        if self.password:
            body += enc_string(self.password)
        send_packet(self.sock, 0x10, body)

        header, response = read_packet(self.sock)
        if header >> 4 != 2 or len(response) < 2 or response[1] != 0:
            code = response[1] if len(response) > 1 else "?"
            detail = mqtt_connect_error(code) if isinstance(code, int) else "keine Antwortkennung"
            raise RuntimeError("MQTT-Anmeldung abgelehnt: %s (%s)" % (code, detail))
        return self

    def __exit__(self, exc_type, exc, tb):
        try:
            send_packet(self.sock, 0xE0)
        except Exception:
            pass
        try:
            self.sock.close()
        except Exception:
            pass

    def publish(self, topic, payload, retain=False):
        header = 0x31 if retain else 0x30
        send_packet(self.sock, header, enc_string(topic) + payload.encode("utf-8"))

    def ping(self):
        send_packet(self.sock, 0xC0)
        header, body = read_packet(self.sock)
        if header >> 4 != 13:
            raise RuntimeError("PINGRESP erwartet, Paket %s empfangen" % (header >> 4))

    def subscribe(self, topic):
        packet_id = self.packet_id
        self.packet_id += 1
        body = packet_id.to_bytes(2, "big") + enc_string(topic) + b"\x00"
        send_packet(self.sock, 0x82, body)
        header, response = read_packet(self.sock)
        if header >> 4 != 9:
            raise RuntimeError("SUBACK erwartet, Paket %s empfangen" % (header >> 4))
        return packet_id

    def messages(self, seconds):
        end = time.time() + seconds
        while time.time() < end:
            self.sock.settimeout(max(0.1, end - time.time()))
            try:
                header, body = read_packet(self.sock)
            except socket.timeout:
                return
            if header >> 4 != 3 or len(body) < 2:
                continue
            topic_len = int.from_bytes(body[:2], "big")
            topic = body[2:2 + topic_len].decode("utf-8", "replace")
            payload = body[2 + topic_len:].decode("utf-8", "replace")
            yield topic, payload


def pretty(payload):
    try:
        return json.dumps(json.loads(payload), ensure_ascii=False, indent=2)
    except Exception:
        return payload


def root(args, suffix):
    return args.user.strip("/") + "/" + suffix.strip("/")


def command_payload(args):
    if args.data:
        data = json.loads(args.data)
    else:
        data = {}
    data.setdefault("requestId", args.request_id or uuid.uuid4().hex[:8])
    data.setdefault("command", args.command_name)
    if args.box is not None:
        data["boxNumber"] = args.box
    if args.robot_position is not None:
        data["robotPosition"] = args.robot_position
    if args.sampling_box is not None:
        data["samplingBox"] = args.sampling_box
    return json.dumps(data, ensure_ascii=False, separators=(",", ":"))


def alarm_payload(args):
    data = {
        "type": "alarm",
        "datum": time.strftime("%d.%m.%y"),
        "uhrzeit": time.strftime("%H:%M:%S"),
        "alarmCode": args.code,
        "ort": args.location,
        "kuh": args.cow,
        "prioritaet": args.priority,
        "alarmText": args.text,
        "modemImei": args.modem_imei,
    }
    return json.dumps(data, ensure_ascii=False, separators=(",", ":"))


def heartbeat_payload(args):
    data = {"type": "heartbeat", "heartbeat": True, "modemImei": args.modem_imei}
    return json.dumps(data, ensure_ascii=False, separators=(",", ":"))


def main():
    parser = argparse.ArgumentParser(description="MQTT-Testprogramm fuer Mione Alarmmelder auf macOS")
    parser.add_argument("--host", default="localhost", help="MQTT-Broker, Standard: localhost")
    parser.add_argument("--port", type=int, default=1883, help="MQTT-Port, Standard: 1883")
    parser.add_argument("--user", required=True, help="MQTT-Benutzername und Top-Topic")
    parser.add_argument("--password", default="", help="MQTT-Passwort")
    parser.add_argument("--timeout", type=float, default=5.0, help="Timeout in Sekunden")
    sub = parser.add_subparsers(dest="mode", required=True)

    sub.add_parser("functions", help="Melkroboter-Funktionskatalog anzeigen")

    command = sub.add_parser("command", help="Melkroboter-Befehl senden")
    command.add_argument("command_name", help="z.B. stopMilking oder startSystemCleaning")
    command.add_argument("--box", type=int, help="boxNumber")
    command.add_argument("--robot-position", type=int, help="robotPosition")
    command.add_argument("--sampling-box", type=int, help="samplingBox")
    command.add_argument("--request-id", help="Eigene requestId")
    command.add_argument("--data", help="Kompletter JSON-Payload; ergaenzt command/requestId falls leer")
    command.add_argument("--wait-result", type=float, default=5.0, help="Sekunden auf Result warten, 0 = nicht warten")

    alarm = sub.add_parser("alarm", help="Testalarm nach Alarmfunktionen/Alarm senden")
    alarm.add_argument("--modem-imei", required=True)
    alarm.add_argument("--code", default="TEST")
    alarm.add_argument("--location", default="Mac-Test")
    alarm.add_argument("--cow", default="0")
    alarm.add_argument("--priority", default="urgent")
    alarm.add_argument("--text", default="Testalarm vom Mac MQTT-Testprogramm")

    heartbeat = sub.add_parser("heartbeat", help="Heartbeat nach Alarmfunktionen/Heartbeat senden")
    heartbeat.add_argument("--modem-imei", required=True)

    listen = sub.add_parser("listen", help="Topic lesen")
    listen.add_argument("topic", help="Relativ zum Benutzer, z.B. Melkroboter/Result")
    listen.add_argument("--seconds", type=float, default=30.0)

    args = parser.parse_args()
    with MqttClient(args.host, args.port, args.user, args.password, args.timeout) as mqtt:
        if args.mode == "functions":
            topic = root(args, "Melkroboter/Funktionen")
            mqtt.subscribe(topic)
            print("Warte auf", topic)
            for topic, payload in mqtt.messages(args.timeout):
                print("\n[%s]\n%s" % (topic, pretty(payload)))
                return 0
            print("Keine Nachricht empfangen. Ist der Alarmmelder mit Melkroboter/MQTT aktiv?")
            return 2

        if args.mode == "command":
            payload = command_payload(args)
            topic = root(args, "Melkroboter/Command")
            mqtt.publish(topic, payload)
            print("Gesendet an %s:\n%s" % (topic, pretty(payload)))
            if args.wait_result > 0:
                result_topic = root(args, "Melkroboter/Result")
                mqtt.subscribe(result_topic)
                print("\nWarte auf", result_topic)
                for topic, payload in mqtt.messages(args.wait_result):
                    print("\n[%s]\n%s" % (topic, pretty(payload)))
                    return 0
                print("Kein Result empfangen.")
                return 2
            return 0

        if args.mode == "alarm":
            payload = alarm_payload(args)
            topic = root(args, "Alarmfunktionen/Alarm")
            mqtt.publish(topic, payload)
            print("Gesendet an %s:\n%s" % (topic, pretty(payload)))
            return 0

        if args.mode == "heartbeat":
            payload = heartbeat_payload(args)
            topic = root(args, "Alarmfunktionen/Heartbeat")
            mqtt.publish(topic, payload)
            print("Gesendet an %s:\n%s" % (topic, pretty(payload)))
            return 0

        if args.mode == "listen":
            topic = root(args, args.topic)
            mqtt.subscribe(topic)
            print("Warte %s Sekunden auf %s" % (args.seconds, topic))
            for topic, payload in mqtt.messages(args.seconds):
                print("\n[%s]\n%s" % (topic, pretty(payload)))
            return 0

    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        print("\nAbgebrochen.")
        sys.exit(130)
