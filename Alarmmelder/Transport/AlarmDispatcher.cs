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
        public event EventHandler<DispatchResultEventArgs> MobileConfigCompleted;
        public event EventHandler<AlarmProgressEvent> ProgressReceived;

        public AlarmDispatcher(AppSettings settings) { this.settings = settings; }
        public void ApplySettings(AppSettings value) { settings = value; }

        public void Dispatch(AlarmMessage alarm)
        {
            AppSettings snapshot = settings;
            ThreadPool.QueueUserWorkItem(delegate
            {
                int sent = 0; StringBuilder errors = new StringBuilder();
                StringBuilder mqttErrors = new StringBuilder(); StringBuilder tcpErrors = new StringBuilder();
                bool mqttSuccessful = false, tcpSuccessful = false;
                if (!snapshot.MqttEnabled && !snapshot.TcpEnabled) errors.Append("Kein Versandweg aktiviert. ");
                string json = BuildJson(alarm, snapshot.ModemImei);
                if (snapshot.MqttEnabled)
                {
                    try { MqttPublisher.Publish(snapshot.MqttHost, snapshot.MqttPort, snapshot.MqttUser, snapshot.MqttPassword, Topic(snapshot.MqttUser, "Alarmfunktionen/Alarm"), json); mqttSuccessful = true; sent++; }
                    catch (Exception ex) { mqttErrors.Append(ex.Message); errors.Append("MQTT: " + ex.Message + " "); }
                }
                if (snapshot.TcpEnabled)
                {
                    try
                    {
                        if (snapshot.ShowAlarmProgress) TcpPublisher.PublishWithProgress(snapshot.TcpHost, snapshot.TcpPort, json,
                            delegate(AlarmProgressEvent value) { if (value.ModemImei == snapshot.ModemImei) OnProgressReceived(value); }, 30000);
                        else TcpPublisher.Publish(snapshot.TcpHost, snapshot.TcpPort, json);
                        tcpSuccessful = true; sent++;
                    }
                    catch (Exception ex) { tcpErrors.Append(ex.Message); errors.Append("TCP: " + ex.Message + " "); }
                }
                OnCompleted(new DispatchResultEventArgs(sent, errors.ToString().Trim(), snapshot.MqttEnabled, mqttSuccessful,
                    mqttErrors.ToString().Trim(), snapshot.TcpEnabled, tcpSuccessful, tcpErrors.ToString().Trim()));
            });
        }

        public void DispatchHeartbeat(bool value)
        {
            AppSettings snapshot = settings;
            ThreadPool.QueueUserWorkItem(delegate
            {
                int sent = 0; StringBuilder errors = new StringBuilder();
                bool mqttSuccessful = false, tcpSuccessful = false;
                string mqttError = "", tcpError = "";
                string json = BuildHeartbeatJson(value, snapshot.ModemImei);
                if (snapshot.MqttEnabled)
                {
                    try
                    {
                        MqttPublisher.Publish(snapshot.MqttHost, snapshot.MqttPort, snapshot.MqttUser, snapshot.MqttPassword, Topic(snapshot.MqttUser, "Alarmfunktionen/Heartbeat"), json);
                        mqttSuccessful = true; sent++;
                    }
                    catch (Exception ex) { mqttError = ex.Message; errors.Append("MQTT-Heartbeat: " + ex.Message + " "); }
                }
                if (snapshot.TcpEnabled)
                {
                    try
                    {
                        AlarmProgressEvent status = TcpPublisher.RequestModemStatus(snapshot.TcpHost, snapshot.TcpPort, json, 5000);
                        if (!String.Equals(status.ModemImei, snapshot.ModemImei, StringComparison.Ordinal))
                            throw new InvalidOperationException("Die Statusantwort gehört zu einer anderen Modem-IMEI.");
                        OnProgressReceived(status); tcpSuccessful = true; sent++;
                    }
                    catch (Exception ex) { tcpError = ex.Message; errors.Append("TCP-Heartbeat: " + ex.Message + " "); }
                }
                OnHeartbeatCompleted(new DispatchResultEventArgs(sent, errors.ToString().Trim(), snapshot.MqttEnabled,
                    mqttSuccessful, mqttError, snapshot.TcpEnabled, tcpSuccessful, tcpError));
            });
        }

        public void PublishMobileConfiguration(MobileNumberConfig[] mobiles)
        {
            AppSettings snapshot = settings; MobileNumberConfig[] values = mobiles;
            ThreadPool.QueueUserWorkItem(delegate
            {
                int sent = 0; StringBuilder errors = new StringBuilder(); bool mqttSuccessful = false, tcpSuccessful = false; string mqttError = "", tcpError = "";
                string modemStatusTopic = snapshot.MqttEnabled && !String.IsNullOrEmpty(snapshot.MqttUser) ? Topic(snapshot.MqttUser, "Alarmfunktionen/ModemStatus") : "";
                string json = BuildMobileJson(values, snapshot.ModemImei, modemStatusTopic);
                if (snapshot.MqttEnabled)
                {
                    try
                    {
                        MqttPublisher.Publish(snapshot.MqttHost, snapshot.MqttPort, snapshot.MqttUser, snapshot.MqttPassword, Topic(snapshot.MqttUser, "Alarmfunktionen/Config/Mobile/modemImei"), snapshot.ModemImei);
                        MqttPublisher.Publish(snapshot.MqttHost, snapshot.MqttPort, snapshot.MqttUser, snapshot.MqttPassword, Topic(snapshot.MqttUser, "Alarmfunktionen/Config/Mobile"), json);
                        mqttSuccessful = true; sent += 2;
                    }
                    catch (Exception ex) { mqttError = ex.Message; errors.Append("MQTT-Mobilkonfiguration: " + ex.Message + " "); }
                }
                if (snapshot.TcpEnabled)
                {
                    try { TcpPublisher.Publish(snapshot.TcpHost, snapshot.TcpPort, json); tcpSuccessful = true; sent++; }
                    catch (Exception ex) { tcpError = ex.Message; errors.Append("TCP-Mobilkonfiguration: " + ex.Message + " "); }
                }
                OnMobileConfigCompleted(new DispatchResultEventArgs(sent, errors.ToString().Trim(), snapshot.MqttEnabled, mqttSuccessful, mqttError, snapshot.TcpEnabled, tcpSuccessful, tcpError));
            });
        }

        private static string BuildJson(AlarmMessage a, string modemImei)
        {
            StringBuilder b = new StringBuilder(); b.Append("{");
            Add(b, "modemImei", modemImei); b.Append(',');
            Add(b, "datum", a.DateText); b.Append(','); Add(b, "uhrzeit", a.TimeText); b.Append(',');
            Add(b, "alarmCode", a.Code); b.Append(','); Add(b, "ort", a.Location); b.Append(',');
            Add(b, "kuh", a.CowNumber); b.Append(','); Add(b, "prioritaet", a.Priority); b.Append(',');
            Add(b, "alarmText", WithoutGermanUmlauts(a.ClearText)); b.Append("}"); return b.ToString();
        }
        private static string BuildHeartbeatJson(bool value, string modemImei)
        {
            StringBuilder b = new StringBuilder(); b.Append("{"); Add(b, "type", "heartbeat"); b.Append(','); Add(b, "modemImei", modemImei); b.Append(',').Append("\"value\":").Append(value ? "true" : "false").Append(','); Add(b, "timestampUtc", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            b.Append("}"); return b.ToString();
        }
        private static string BuildMobileJson(MobileNumberConfig[] mobiles, string modemImei, string modemStatusTopic)
        {
            StringBuilder b = new StringBuilder(); b.Append("{"); Add(b, "modemImei", modemImei); b.Append(',');
            Add(b, "modemStatusTopic", modemStatusTopic); b.Append(",\"mobile\":[");
            for (int i = 0; i < mobiles.Length; i++)
            {
                if (i > 0) b.Append(','); b.Append('{'); b.Append("\"slot\":").Append(mobiles[i].Slot).Append(',');
                Add(b, "nummer", mobiles[i].Number); b.Append(',').Append("\"aktiv\":").Append(mobiles[i].Active ? "true" : "false").Append(',');
                b.Append("\"alarmsTo\":").Append(mobiles[i].AlarmsTo).Append(','); Add(b, "alarmierung", mobiles[i].AlarmModeText); b.Append(',');
                b.Append("\"technicalAlarmMessagingFrom\":").Append(mobiles[i].TechnicalAlarmMessagingFrom).Append(','); Add(b, "technicalAlarmMessagingFromText", mobiles[i].TechnicalAlarmMessagingFromText); b.Append(',');
                b.Append("\"technicalAlarmMessagingUntil\":").Append(mobiles[i].TechnicalAlarmMessagingUntil).Append(','); Add(b, "technicalAlarmMessagingUntilText", mobiles[i].TechnicalAlarmMessagingUntilText); b.Append('}');
            }
            b.Append("]}"); return b.ToString();
        }
        private static string Topic(string user, string subTopic)
        {
            string top = (user ?? "").Trim().Trim('/'); if (top.Length == 0) throw new InvalidOperationException("MQTT-Benutzername/Top-Topic fehlt.");
            return top + "/" + subTopic.TrimStart('/');
        }
        private static void Add(StringBuilder b, string key, string value) { b.Append('"').Append(Escape(key)).Append("\":\"").Append(Escape(value)).Append('"'); }
        private static string WithoutGermanUmlauts(string value)
        {
            if (value == null) return "";
            return value.Replace("Ä", "Ae").Replace("Ö", "Oe").Replace("Ü", "Ue")
                .Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");
        }
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
        private void OnMobileConfigCompleted(DispatchResultEventArgs e) { if (MobileConfigCompleted != null) MobileConfigCompleted(this, e); }
        private void OnProgressReceived(AlarmProgressEvent e) { if (ProgressReceived != null) ProgressReceived(this, e); }
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
