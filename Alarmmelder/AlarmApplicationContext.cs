using System;
using System.Threading;
using System.Windows.Forms;
using MioneAlarmmelder.Core;
using MioneAlarmmelder.Forms;
using MioneAlarmmelder.Transport;

namespace MioneAlarmmelder
{
    public sealed class AlarmApplicationContext : ApplicationContext
    {
        private AppSettings settings; private MainForm main; private FileMonitorService monitor; private AlarmDispatcher dispatcher;
        private MqttProgressSubscriber mqttProgressSubscriber;
        private System.Threading.Timer heartbeatTimer, updateTimer; private int heartbeatPending, updateCheckPending;
        private string notifiedUpdateTag = "";
        private bool heartbeatValue, mqttModemActive;
        private DateTime lastModemStatusUtc = DateTime.MinValue;
        private bool modemStatusTimedOut;

        public AlarmApplicationContext()
        {
            using (SplashForm splash = new SplashForm()) splash.ShowDialog();
            settings = SettingsStore.Load();
            if (settings.MissingFiles().Length > 0)
            {
                using (PathSettingsForm paths = new PathSettingsForm(settings))
                { if (paths.ShowDialog() != DialogResult.OK) { MessageBox.Show("Die Überwachung kann erst mit gültigen Dateipfaden starten.", "Mione Alarmmelder", MessageBoxButtons.OK, MessageBoxIcon.Warning); } }
            }
            main = new MainForm(settings); MainForm = main; main.FormClosed += delegate { Stop(); ExitThread(); };
            main.SettingsSaved += delegate { Restart(); };
            main.UpdateCheckRequested += delegate { CheckForUpdates(true); };
            main.TestAlarmRequested += delegate { SendTestAlarm(); };
            main.UrgentTestAlarmRequested += delegate { SendUrgentTestAlarm(); };
            main.Show(); Start(); ScheduleUpdateCheck();
        }

        private void Start()
        {
            if (settings.MissingFiles().Length > 0) { main.SetStatus("Dateipfade unvollständig", MonitorState.Error); return; }
            dispatcher = new AlarmDispatcher(settings); dispatcher.Completed += DispatchCompleted; dispatcher.HeartbeatCompleted += HeartbeatCompleted; dispatcher.MobileConfigCompleted += MobileConfigCompleted; dispatcher.ProgressReceived += AlarmProgressReceived;
            if (settings.MqttEnabled)
            {
                mqttProgressSubscriber = new MqttProgressSubscriber(settings);
                mqttProgressSubscriber.ProgressReceived += AlarmProgressReceived;
                mqttProgressSubscriber.Start();
            }
            monitor = new FileMonitorService(settings); monitor.StatusChanged += MonitorStatusChanged; monitor.AlarmFound += AlarmFound; monitor.PhoneSettingsChanged += PhonesChanged;
            try
            {
                monitor.Start(); main.SetPhones(monitor.GetMobileConfigurations()); main.SetStatus("Überwachung aktiv", MonitorState.Ok);
                main.SetModemStatus(settings.TcpEnabled ? "Socket" : settings.MqttEnabled ? "MQTT" : "", settings.TcpEnabled ? "wird geprüft" : settings.MqttEnabled ? "warte auf Rückmeldung" : "deaktiviert", settings.TcpEnabled || settings.MqttEnabled ? MonitorState.Waiting : MonitorState.Disabled);
                lastModemStatusUtc = DateTime.UtcNow;
                heartbeatTimer = new System.Threading.Timer(HeartbeatTick, null, 5000, 5000);
            }
            catch (Exception ex) { ErrorLogger.Log("Dateiüberwachung", ex); main.SetStatus("Fehler - siehe Fehlerprotokoll", MonitorState.Error); }
        }

        private void Restart() { Stop(); settings = SettingsStore.Load(); Start(); ScheduleUpdateCheck(); }
        private void Stop()
        {
            if (heartbeatTimer != null) { heartbeatTimer.Dispose(); heartbeatTimer = null; }
            if (updateTimer != null) { updateTimer.Dispose(); updateTimer = null; }
            if (mqttProgressSubscriber != null) { mqttProgressSubscriber.Dispose(); mqttProgressSubscriber = null; }
            Interlocked.Exchange(ref heartbeatPending, 0);
            heartbeatValue = false;
            mqttModemActive = false;
            lastModemStatusUtc = DateTime.MinValue;
            modemStatusTimedOut = false;
            Interlocked.Exchange(ref updateCheckPending, 0);
            if (monitor != null) { monitor.Dispose(); monitor = null; } dispatcher = null;
        }
        private void AlarmProgressReceived(object sender, AlarmProgressEvent e)
        {
            if (String.Equals(e.Action, "Modemstatus", StringComparison.OrdinalIgnoreCase))
            {
                MonitorState modemState = ModemState(e.Status);
                lastModemStatusUtc = DateTime.UtcNow;
                modemStatusTimedOut = false;
                if (String.Equals(e.Source, "MQTT", StringComparison.OrdinalIgnoreCase)) mqttModemActive = modemState == MonitorState.Ok;
                main.SetModemStatus(e.Source, e.Status.Length == 0 ? "aktiv" : e.Status, modemState);
                if (!String.IsNullOrEmpty(e.FirmwareStatus))
                    main.SetFirmwareStatus(e.FirmwareStatus, modemState == MonitorState.Error ? MonitorState.Error :
                        e.FirmwareUpdateAvailable ? MonitorState.Waiting : MonitorState.Ok);
                return;
            }
            MonitorState state = ModemState(e.Status);
            main.ShowAlarmProgress(e);
        }
        private static MonitorState ModemState(string status)
        {
            string text = (status ?? "").ToLowerInvariant();
            if (text.IndexOf("offline") >= 0 || text.IndexOf("fehler") >= 0 || text.IndexOf("error") >= 0 || text.IndexOf("nicht") >= 0) return MonitorState.Error;
            return MonitorState.Ok;
        }
        private void MonitorStatusChanged(object sender, MonitorStatusEventArgs e) { if (e.State == MonitorState.Error) { ErrorLogger.Log("Dateiüberwachung", e.Text); main.SetStatus("Fehler - siehe Fehlerprotokoll", MonitorState.Error); } else main.SetStatus(e.Text, e.State); }
        private void PhonesChanged(object sender, EventArgs e)
        {
            if (monitor == null) return; main.SetPhones(monitor.GetMobileConfigurations());
            if (dispatcher != null) dispatcher.PublishMobileConfiguration(monitor.GetMobileConfigurations());
        }
        private void AlarmFound(object sender, AlarmEventArgs e)
        {
            main.AddAlarm(e.Alarm); main.SetStatus("Alarm " + e.Alarm.Code + " wird versendet", MonitorState.Sending);
            if (settings.MqttEnabled) main.SetMqttStatus("sendet Alarm " + e.Alarm.Code, MonitorState.Sending);
            if (settings.TcpEnabled) main.SetTcpStatus("sendet Alarm " + e.Alarm.Code, MonitorState.Sending);
            if (dispatcher != null) dispatcher.Dispatch(e.Alarm);
        }
        private void SendTestAlarm()
        {
            if (monitor == null || dispatcher == null) { ErrorLogger.Log("Testfehler", "Überwachung oder Versand ist nicht gestartet."); main.SetStatus("Testfehler nicht möglich", MonitorState.Error); return; }
            AlarmMessage alarm = new AlarmMessage
            {
                DateText = DateTime.Now.ToString("dd.MM.yy"), TimeText = DateTime.Now.ToString("HH:mm:ss"), Code = "TEST",
                Location = "Test", CowNumber = "0", Priority = "technical", ClearText = "Dies ist ein Testfehler zur Prüfung der Alarmübertragung."
            };
            main.AddAlarm(alarm); main.SetStatus("Testfehler wird versendet", MonitorState.Sending);
            if (settings.MqttEnabled) main.SetMqttStatus("sendet Testfehler", MonitorState.Sending);
            if (settings.TcpEnabled) main.SetTcpStatus("sendet Testfehler", MonitorState.Sending);
            dispatcher.Dispatch(alarm);
        }
        private void SendUrgentTestAlarm()
        {
            if (dispatcher == null) { ErrorLogger.Log("Testalarm", "Der Versand ist nicht gestartet."); main.SetStatus("Testalarm nicht möglich", MonitorState.Error); return; }
            DateTime now = DateTime.Now;
            AlarmMessage alarm = new AlarmMessage
            {
                DateText = now.ToString("dd.MM.yy"), TimeText = now.ToString("HH:mm:ss"), Code = "TEST",
                Location = "Test", CowNumber = "0", Priority = "urgent",
                ClearText = "Testalarm vom " + now.ToString("dd.MM.yyyy") + " um " + now.ToString("HH:mm:ss") + " Uhr zur Prüfung der Alarmierung."
            };
            main.AddAlarm(alarm); main.SetStatus("Testalarm (urgent) wird versendet", MonitorState.Sending);
            if (settings.MqttEnabled) main.SetMqttStatus("sendet Testalarm (urgent)", MonitorState.Sending);
            if (settings.TcpEnabled) main.SetTcpStatus("sendet Testalarm (urgent)", MonitorState.Sending);
            dispatcher.Dispatch(alarm);
        }
        private void DispatchCompleted(object sender, DispatchResultEventArgs e)
        {
            main.SetMqttStatus(e.MqttEnabled ? (e.MqttSuccessful ? "Versand erfolgreich" : "Fehler") : "deaktiviert",
                e.MqttEnabled ? (e.MqttSuccessful ? MonitorState.Ok : MonitorState.Error) : MonitorState.Disabled);
            main.SetTcpStatus(e.TcpEnabled ? (e.TcpSuccessful ? "Versand erfolgreich" : "Fehler") : "deaktiviert",
                e.TcpEnabled ? (e.TcpSuccessful ? MonitorState.Ok : MonitorState.Error) : MonitorState.Disabled);
            if (e.Error.Length > 0) { ErrorLogger.Log("Alarmversand", e.Error); main.SetStatus("Versandfehler - siehe Fehlerprotokoll", MonitorState.Error); }
            else main.SetStatus(e.SentCount + " Nachricht(en) erfolgreich versendet", MonitorState.Ok);
        }
        private void HeartbeatTick(object state)
        {
            if (dispatcher == null || (!settings.MqttEnabled && !settings.TcpEnabled)) return;
            CheckModemStatusTimeout();
            if (Interlocked.Exchange(ref heartbeatPending, 1) != 0) return;
            if (settings.MqttEnabled) main.SetMqttStatus("Heartbeat wird gesendet", MonitorState.Sending);
            if (settings.TcpEnabled) main.SetTcpStatus("Heartbeat wird gesendet", MonitorState.Sending);
            heartbeatValue = !heartbeatValue; dispatcher.DispatchHeartbeat(heartbeatValue);
            if (monitor != null && (settings.MqttEnabled || settings.TcpEnabled)) dispatcher.PublishMobileConfiguration(monitor.GetMobileConfigurations());
        }
        private void HeartbeatCompleted(object sender, DispatchResultEventArgs e)
        {
            Interlocked.Exchange(ref heartbeatPending, 0);
            main.SetMqttStatus(e.MqttEnabled ? (e.MqttSuccessful ? "Heartbeat OK" : "Heartbeat-Fehler") : "deaktiviert",
                e.MqttEnabled ? (e.MqttSuccessful ? MonitorState.Ok : MonitorState.Error) : MonitorState.Disabled);
            main.SetTcpStatus(e.TcpEnabled ? (e.TcpSuccessful ? "Heartbeat OK" : "Heartbeat-Fehler") : "deaktiviert",
                e.TcpEnabled ? (e.TcpSuccessful ? MonitorState.Ok : MonitorState.Error) : MonitorState.Disabled);
            if (e.TcpEnabled && !e.TcpSuccessful) main.SetModemStatus("Socket", "nicht erreichbar", MonitorState.Error);
            else if (e.MqttEnabled && !e.MqttSuccessful) main.SetModemStatus("MQTT", "Verbindung fehlerhaft", MonitorState.Error);
            else if (e.MqttEnabled)
            {
                bool fresh = DateTime.UtcNow.Subtract(lastModemStatusUtc).TotalSeconds <= 15;
                main.SetModemStatus("MQTT", mqttModemActive && fresh ? "aktiv" : fresh ? "warte auf Rückmeldung" : "keine Rückmeldung seit 15 Sekunden",
                    mqttModemActive && fresh ? MonitorState.Ok : fresh ? MonitorState.Waiting : MonitorState.Error);
            }
            else main.SetModemStatus("", "deaktiviert", MonitorState.Disabled);
            if (e.Error.Length > 0) { ErrorLogger.Log("Heartbeat", e.Error); main.SetStatus("Heartbeat-Fehler - siehe Fehlerprotokoll", MonitorState.Error); }
        }
        private void CheckModemStatusTimeout()
        {
            if (lastModemStatusUtc == DateTime.MinValue || DateTime.UtcNow.Subtract(lastModemStatusUtc).TotalSeconds <= 15) return;
            mqttModemActive = false;
            main.SetModemStatus(settings.TcpEnabled ? "Socket" : "MQTT", "keine Rückmeldung seit 15 Sekunden", MonitorState.Error);
            if (!modemStatusTimedOut)
            {
                modemStatusTimedOut = true;
                ErrorLogger.Log("Modem-Heartbeat", "Seit mehr als 15 Sekunden wurde kein Modemstatus empfangen.");
            }
        }
        private void MobileConfigCompleted(object sender, DispatchResultEventArgs e)
        {
            if (e.MqttEnabled) main.SetMqttStatus(e.MqttSuccessful ? "Mobilkonfiguration OK" : "Config-Fehler", e.MqttSuccessful ? MonitorState.Ok : MonitorState.Error);
            if (e.TcpEnabled) main.SetTcpStatus(e.TcpSuccessful ? "Mobilkonfiguration OK" : "Config-Fehler", e.TcpSuccessful ? MonitorState.Ok : MonitorState.Error);
            if (e.Error.Length > 0) { ErrorLogger.Log("Mobilkonfiguration", e.Error); main.SetStatus("Config-Fehler - siehe Fehlerprotokoll", MonitorState.Error); }
        }
        private void ScheduleUpdateCheck()
        {
            if (!settings.UpdateEnabled || String.IsNullOrEmpty(settings.UpdateRepository)) return;
            updateTimer = new System.Threading.Timer(delegate { CheckForUpdates(false); }, null, 8000, Math.Max(5, settings.UpdateCheckMinutes) * 60000);
        }
        private void CheckForUpdates(bool manual)
        {
            if (String.IsNullOrEmpty(settings.UpdateRepository))
            {
                main.SetUpdateStatus("Bitte zuerst das GitHub-Repository eintragen und speichern."); return;
            }
            if (Interlocked.Exchange(ref updateCheckPending, 1) != 0)
            {
                if (manual) main.SetUpdateStatus("Eine Updateprüfung läuft bereits."); return;
            }
            main.SetUpdateStatus("GitHub wird geprüft (" + (String.Equals(settings.UpdateChannel, "beta", StringComparison.OrdinalIgnoreCase) ? "Beta" : "Stable") + ") ...");
            GitHubUpdateService.CheckAsync(settings, delegate(UpdateCheckResult result)
            {
                Interlocked.Exchange(ref updateCheckPending, 0);
                if (main.IsDisposed || !main.IsHandleCreated) return;
                main.BeginInvoke((MethodInvoker)delegate
                {
                    if (!String.IsNullOrEmpty(result.Error))
                    {
                        ErrorLogger.Log("GitHub-Update", result.Error);
                        main.SetUpdateStatus("Updateprüfung fehlgeschlagen: " + result.Error);
                        if (manual) MessageBox.Show(result.Error, "Updateprüfung fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    if (!result.HasUpdate)
                    {
                        main.SetUpdateStatus("Die installierte Version ist aktuell (" + GitHubUpdateService.CurrentDisplayVersion + ", " + (String.Equals(settings.UpdateChannel, "beta", StringComparison.OrdinalIgnoreCase) ? "Beta" : "Stable") + ").");
                        if (manual) MessageBox.Show("Es ist kein neueres Release verfügbar.", "Updateprüfung", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    main.SetUpdateStatus("Neue " + (String.Equals(result.Channel, "beta", StringComparison.OrdinalIgnoreCase) ? "Beta" : "Stable") + "-Version verfügbar: " + result.TagName);
                    if (!String.Equals(notifiedUpdateTag, result.TagName, StringComparison.OrdinalIgnoreCase))
                    {
                        notifiedUpdateTag = result.TagName; main.ShowUpdateNotification(result.TagName);
                    }
                    if (manual && MessageBox.Show("Version " + result.TagName + " ist verfügbar. Jetzt installieren und neu starten?", "Update verfügbar", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                        DownloadUpdate(result);
                });
            });
        }
        private void DownloadUpdate(UpdateCheckResult result)
        {
            main.SetUpdateStatus("Version " + result.TagName + " wird heruntergeladen ...");
            GitHubUpdateService.DownloadAndInstallAsync(result, delegate(string error)
            {
                main.BeginInvoke((MethodInvoker)delegate
                {
                    if (!String.IsNullOrEmpty(error))
                    {
                        ErrorLogger.Log("GitHub-Update", error);
                        main.SetUpdateStatus("Installation fehlgeschlagen: " + error);
                        MessageBox.Show(error, "Update fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else { main.SetUpdateStatus("Update wird installiert. Anwendung wird neu gestartet ..."); main.ExitForUpdate(); }
                });
            });
        }
    }
}
