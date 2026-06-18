using System;
using System.Text;
using System.Threading;
using MioneAlarmmelder.Core;

namespace MioneAlarmmelder.Transport
{
    public sealed class AlarmDispatcher
    {
        private AppSettings settings;
        public event EventHandler<DispatchResultEventArgs> Completed;
        public event EventHandler<DispatchResultEventArgs> HeartbeatCompleted;

        public AlarmDispatcher(AppSettings settings) { this.settings = settings; }
        public void ApplySettings(AppSettings value) { settings = value; }

        public void Dispatch(AlarmMessage alarm, string[] numbers)
        {
            AppSettings snapshot = settings; string[] recipients = numbers;
            ThreadPool.QueueUserWorkItem(delegate
            {
                int sent = 0; StringBuilder errors = new StringBuilder();
                StringBuilder mqttErrors = new StringBuilder(); StringBuilder tcpErrors = new StringBuilder();
                bool mqttSuccessful = snapshot.MqttEnabled && recipients.Length > 0;
                bool tcpSuccessful = snapshot.TcpEnabled && recipients.Length > 0;
                if (recipients.Length == 0) errors.Append("Keine aktive Rufnummer vorhanden. ");
                if (!snapshot.MqttEnabled && !snapshot.TcpEnabled) errors.Append("Kein Versandweg aktiviert. ");
                for (int i = 0; i < recipients.Length; i++)
                {
                    string json = BuildJson(snapshot.CustomerId, recipients[i], alarm);
                    if (snapshot.MqttEnabled)
                    {
                        try
                        {
                            string topic = (snapshot.MqttTopic ?? "").Replace("{kunde}", snapshot.CustomerId ?? "");
                            MqttPublisher.Publish(snapshot.MqttHost, snapshot.MqttPort, snapshot.MqttUser, snapshot.MqttPassword, topic, json); sent++;
                        }
                        catch (Exception ex) { mqttSuccessful = false; mqttErrors.Append(ex.Message + " "); errors.Append("MQTT: " + ex.Message + " "); }
                    }
                    if (snapshot.TcpEnabled)
                    {
                        try { TcpPublisher.Publish(snapshot.TcpHost, snapshot.TcpPort, json); sent++; }
                        catch (Exception ex) { tcpSuccessful = false; tcpErrors.Append(ex.Message + " "); errors.Append("TCP: " + ex.Message + " "); }
                    }
                }
                if (snapshot.MqttEnabled && recipients.Length == 0) mqttErrors.Append("Keine aktive Rufnummer vorhanden.");
                if (snapshot.TcpEnabled && recipients.Length == 0) tcpErrors.Append("Keine aktive Rufnummer vorhanden.");
                OnCompleted(new DispatchResultEventArgs(sent, errors.ToString().Trim(), snapshot.MqttEnabled, mqttSuccessful,
                    mqttErrors.ToString().Trim(), snapshot.TcpEnabled, tcpSuccessful, tcpErrors.ToString().Trim()));
            });
        }

        public void DispatchHeartbeat()
        {
            AppSettings snapshot = settings;
            ThreadPool.QueueUserWorkItem(delegate
            {
                int sent = 0; StringBuilder errors = new StringBuilder();
                bool mqttSuccessful = false, tcpSuccessful = false;
                string mqttError = "", tcpError = "";
                string json = BuildHeartbeatJson(snapshot.CustomerId);
                if (snapshot.MqttEnabled)
                {
                    try
                    {
                        string topic = BuildHeartbeatTopic(snapshot.MqttTopic, snapshot.CustomerId);
                        MqttPublisher.Publish(snapshot.MqttHost, snapshot.MqttPort, snapshot.MqttUser, snapshot.MqttPassword, topic, json);
                        mqttSuccessful = true; sent++;
                    }
                    catch (Exception ex) { mqttError = ex.Message; errors.Append("MQTT-Heartbeat: " + ex.Message + " "); }
                }
                if (snapshot.TcpEnabled)
                {
                    try { TcpPublisher.Publish(snapshot.TcpHost, snapshot.TcpPort, json); tcpSuccessful = true; sent++; }
                    catch (Exception ex) { tcpError = ex.Message; errors.Append("TCP-Heartbeat: " + ex.Message + " "); }
                }
                OnHeartbeatCompleted(new DispatchResultEventArgs(sent, errors.ToString().Trim(), snapshot.MqttEnabled,
                    mqttSuccessful, mqttError, snapshot.TcpEnabled, tcpSuccessful, tcpError));
            });
        }

        private static string BuildJson(string customer, string phone, AlarmMessage a)
        {
            StringBuilder b = new StringBuilder(); b.Append("{");
            Add(b, "kunde", customer); b.Append(','); Add(b, "rufnummer", phone); b.Append(',');
            Add(b, "datum", a.DateText); b.Append(','); Add(b, "uhrzeit", a.TimeText); b.Append(',');
            Add(b, "alarmCode", a.Code); b.Append(','); Add(b, "ort", a.Location); b.Append(',');
            Add(b, "kuhnummer", a.CowNumber); b.Append(','); Add(b, "prioritaet", a.Priority); b.Append(',');
            Add(b, "alarmText", a.ClearText); b.Append("}"); return b.ToString();
        }
        private static string BuildHeartbeatJson(string customer)
        {
            StringBuilder b = new StringBuilder(); b.Append("{"); Add(b, "type", "heartbeat"); b.Append(',');
            Add(b, "kunde", customer); b.Append(','); Add(b, "timestampUtc", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            b.Append("}"); return b.ToString();
        }
        private static string BuildHeartbeatTopic(string alarmTopic, string customer)
        {
            string topic = (alarmTopic ?? "").Replace("{kunde}", customer ?? "").TrimEnd('/');
            if (topic.EndsWith("/alarm", StringComparison.OrdinalIgnoreCase)) return topic.Substring(0, topic.Length - 6) + "/heartbeat";
            return topic + "/heartbeat";
        }
        private static void Add(StringBuilder b, string key, string value) { b.Append('"').Append(Escape(key)).Append("\":\"").Append(Escape(value)).Append('"'); }
        private static string Escape(string value)
        {
            if (value == null) return ""; StringBuilder b = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '"') b.Append("\\\""); else if (c == '\\') b.Append("\\\\"); else if (c == '\r') b.Append("\\r");
                else if (c == '\n') b.Append("\\n"); else if (c == '\t') b.Append("\\t"); else if (c < 32) b.Append("\\u").Append(((int)c).ToString("x4")); else b.Append(c);
            }
            return b.ToString();
        }
        private void OnCompleted(DispatchResultEventArgs e) { if (Completed != null) Completed(this, e); }
        private void OnHeartbeatCompleted(DispatchResultEventArgs e) { if (HeartbeatCompleted != null) HeartbeatCompleted(this, e); }
    }

    public sealed class DispatchResultEventArgs : EventArgs
    {
        public int SentCount { get; private set; } public string Error { get; private set; }
        public bool MqttEnabled { get; private set; } public bool MqttSuccessful { get; private set; } public string MqttError { get; private set; }
        public bool TcpEnabled { get; private set; } public bool TcpSuccessful { get; private set; } public string TcpError { get; private set; }
        public DispatchResultEventArgs(int count, string error, bool mqttEnabled, bool mqttSuccessful, string mqttError,
            bool tcpEnabled, bool tcpSuccessful, string tcpError)
        {
            SentCount = count; Error = error; MqttEnabled = mqttEnabled; MqttSuccessful = mqttSuccessful; MqttError = mqttError;
            TcpEnabled = tcpEnabled; TcpSuccessful = tcpSuccessful; TcpError = tcpError;
        }
    }
}
