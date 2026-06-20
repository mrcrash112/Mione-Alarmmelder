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

        private static string Text(Dictionary<string, object> data, string key)
        {
            object value; return data.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : "";
        }
    }
}
