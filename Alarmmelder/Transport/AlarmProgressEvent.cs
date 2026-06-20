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
                value = new AlarmProgressEvent
                {
                    ModemImei = modemImei, Status = status, Timestamp = Text(data, "timestamp"), Source = source ?? "",
                    Action = "Modemstatus", Number = "-"
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
    }
}
