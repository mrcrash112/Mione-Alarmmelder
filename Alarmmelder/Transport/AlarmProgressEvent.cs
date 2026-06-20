using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace MioneAlarmmelder.Transport
{
    public sealed class AlarmProgressEvent : EventArgs
    {
        public string ModemImei { get; private set; }
        public string AlarmCode { get; private set; }
        public string AlarmText { get; private set; }
        public string Number { get; private set; }
        public string Action { get; private set; }
        public string Status { get; private set; }
        public string Timestamp { get; private set; }
        public string Source { get; private set; }
        public string FirmwareStatus { get; private set; }
        public bool FirmwareUpdateAvailable { get; private set; }

        private AlarmProgressEvent() { }

        public static bool TryParse(string json, string source, out AlarmProgressEvent value)
        {
            value = null;
            try
            {
                Dictionary<string, object> data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
                if (Text(data, "type") != "alarmProgress") return false;
                value = new AlarmProgressEvent
                {
                    ModemImei = Text(data, "modemImei"), AlarmCode = Text(data, "alarmCode"),
                    AlarmText = Text(data, "alarmText"), Number = Text(data, "number"),
                    Action = Text(data, "action"), Status = Text(data, "status"),
                    Timestamp = Text(data, "timestamp"), Source = source ?? ""
                };
                return value.ModemImei.Length > 0 && value.Number.Length > 0;
            }
            catch { return false; }
        }

        public static bool TryParseModemStatus(string json, string source, out AlarmProgressEvent value)
        {
            value = null;
            try
            {
                Dictionary<string, object> data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
                string type = Text(data, "type");
                if (type != "modemStatus" && type != "heartbeat" && type != "status") return false;
                string modemImei = Text(data, "modemImei");
                if (modemImei.Length == 0) return false;
                string status = Text(data, "status");
                if (status.Length == 0) status = Truthy(data, "online") || Truthy(data, "active") || Truthy(data, "value") ? "aktiv" : "online";
                Dictionary<string, object> update = Object(data, "update");
                string firmwareStatus = BuildFirmwareStatus(update);
                value = new AlarmProgressEvent
                {
                    ModemImei = modemImei, Status = status, Timestamp = Text(data, "timestamp"), Source = source ?? "",
                    Action = "Modemstatus", Number = "-", FirmwareStatus = firmwareStatus,
                    FirmwareUpdateAvailable = update != null && Truthy(update, "available")
                };
                return true;
            }
            catch { return false; }
        }

        private static string Text(Dictionary<string, object> data, string key)
        {
            object value; return data.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : "";
        }
        private static bool Truthy(Dictionary<string, object> data, string key)
        {
            object value; if (!data.TryGetValue(key, out value) || value == null) return false;
            if (value is bool) return (bool)value;
            string text = Convert.ToString(value); return text == "1" || String.Equals(text, "true", StringComparison.OrdinalIgnoreCase) || String.Equals(text, "aktiv", StringComparison.OrdinalIgnoreCase) || String.Equals(text, "online", StringComparison.OrdinalIgnoreCase);
        }
        private static Dictionary<string, object> Object(Dictionary<string, object> data, string key)
        {
            object value; if (!data.TryGetValue(key, out value) || value == null) return null;
            return value as Dictionary<string, object>;
        }
        private static string BuildFirmwareStatus(Dictionary<string, object> update)
        {
            if (update == null) return "";
            string firmware = Text(update, "currentVersion");
            string recovery = Text(update, "currentRecoveryVersion");
            if (recovery.Length == 0) recovery = Text(update, "recoveryVersion");
            string web = Text(update, "currentWebVersion");
            string channel = Text(update, "channel");
            string message = Text(update, "message");
            string progress = Text(update, "progress");
            string result = "FW " + (firmware.Length == 0 ? "?" : firmware) +
                " | Recovery " + (recovery.Length == 0 ? "?" : recovery) +
                " | WWW " + (web.Length == 0 ? "?" : web);
            if (channel.Length > 0) result += " | " + (String.Equals(channel, "beta", StringComparison.OrdinalIgnoreCase) ? "Beta" : "Stable");
            if (message.Length > 0) result += " | " + message;
            if (progress.Length > 0 && progress != "0") result += " (" + progress + "%)";
            return result;
        }
    }
}
