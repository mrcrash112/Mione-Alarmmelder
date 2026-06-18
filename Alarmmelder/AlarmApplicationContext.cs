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
        private System.Threading.Timer heartbeatTimer, updateTimer; private int heartbeatPending;

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
            main.Show(); Start(); ScheduleUpdateCheck();
        }

        private void Start()
        {
            if (settings.MissingFiles().Length > 0) { main.SetStatus("Dateipfade unvollständig", MonitorState.Error); return; }
            dispatcher = new AlarmDispatcher(settings); dispatcher.Completed += DispatchCompleted; dispatcher.HeartbeatCompleted += HeartbeatCompleted;
            monitor = new FileMonitorService(settings); monitor.StatusChanged += MonitorStatusChanged; monitor.AlarmFound += AlarmFound; monitor.PhoneSettingsChanged += PhonesChanged;
            try
            {
                monitor.Start(); main.SetPhones(monitor.GetPhones()); main.SetStatus("Überwachung aktiv", MonitorState.Ok);
                heartbeatTimer = new System.Threading.Timer(HeartbeatTick, null, 5000, Math.Max(10, settings.HeartbeatSeconds) * 1000);
            }
            catch (Exception ex) { ErrorLogger.Log("Dateiüberwachung", ex); main.SetStatus("Fehler - siehe Fehlerprotokoll", MonitorState.Error); }
        }

        private void Restart() { Stop(); settings = SettingsStore.Load(); Start(); ScheduleUpdateCheck(); }
        private void Stop()
        {
            if (heartbeatTimer != null) { heartbeatTimer.Dispose(); heartbeatTimer = null; }
            if (updateTimer != null) { updateTimer.Dispose(); updateTimer = null; }
            Interlocked.Exchange(ref heartbeatPending, 0);
            if (monitor != null) { monitor.Dispose(); monitor = null; } dispatcher = null;
        }
        private void MonitorStatusChanged(object sender, MonitorStatusEventArgs e) { if (e.State == MonitorState.Error) { ErrorLogger.Log("Dateiüberwachung", e.Text); main.SetStatus("Fehler - siehe Fehlerprotokoll", MonitorState.Error); } else main.SetStatus(e.Text, e.State); }
        private void PhonesChanged(object sender, EventArgs e) { if (monitor != null) main.SetPhones(monitor.GetPhones()); }
        private void AlarmFound(object sender, AlarmEventArgs e)
        {
            main.AddAlarm(e.Alarm); main.SetStatus("Alarm " + e.Alarm.Code + " wird versendet", MonitorState.Sending);
            if (settings.MqttEnabled) main.SetMqttStatus("sendet Alarm " + e.Alarm.Code, MonitorState.Sending);
            if (settings.TcpEnabled) main.SetTcpStatus("sendet Alarm " + e.Alarm.Code, MonitorState.Sending);
            if (dispatcher != null) dispatcher.Dispatch(e.Alarm, e.PhoneNumbers);
        }
        private void SendTestAlarm()
        {
            if (monitor == null || dispatcher == null) { ErrorLogger.Log("Testfehler", "Überwachung oder Versand ist nicht gestartet."); main.SetStatus("Testfehler nicht möglich", MonitorState.Error); return; }
            System.Collections.Generic.KeyValuePair<string, bool>[] phones = monitor.GetPhones();
            System.Collections.Generic.List<string> activeNumbers = new System.Collections.Generic.List<string>();
            for (int i = 0; i < phones.Length; i++) if (phones[i].Value) activeNumbers.Add(phones[i].Key);
            AlarmMessage alarm = new AlarmMessage
            {
                DateText = DateTime.Now.ToString("dd.MM.yy"), TimeText = DateTime.Now.ToString("HH:mm:ss"), Code = "TEST",
                Location = "Test", CowNumber = "0", Priority = "technical", ClearText = "Dies ist ein Testfehler zur Prüfung der Alarmübertragung."
            };
            main.AddAlarm(alarm); main.SetStatus("Testfehler wird versendet", MonitorState.Sending);
            if (settings.MqttEnabled) main.SetMqttStatus("sendet Testfehler", MonitorState.Sending);
            if (settings.TcpEnabled) main.SetTcpStatus("sendet Testfehler", MonitorState.Sending);
            dispatcher.Dispatch(alarm, activeNumbers.ToArray());
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
            if (Interlocked.Exchange(ref heartbeatPending, 1) != 0) return;
            if (settings.MqttEnabled) main.SetMqttStatus("Heartbeat wird gesendet", MonitorState.Sending);
            if (settings.TcpEnabled) main.SetTcpStatus("Heartbeat wird gesendet", MonitorState.Sending);
            dispatcher.DispatchHeartbeat();
        }
        private void HeartbeatCompleted(object sender, DispatchResultEventArgs e)
        {
            Interlocked.Exchange(ref heartbeatPending, 0);
            main.SetMqttStatus(e.MqttEnabled ? (e.MqttSuccessful ? "Heartbeat OK" : "Heartbeat-Fehler") : "deaktiviert",
                e.MqttEnabled ? (e.MqttSuccessful ? MonitorState.Ok : MonitorState.Error) : MonitorState.Disabled);
            main.SetTcpStatus(e.TcpEnabled ? (e.TcpSuccessful ? "Heartbeat OK" : "Heartbeat-Fehler") : "deaktiviert",
                e.TcpEnabled ? (e.TcpSuccessful ? MonitorState.Ok : MonitorState.Error) : MonitorState.Disabled);
            if (e.Error.Length > 0) { ErrorLogger.Log("Heartbeat", e.Error); main.SetStatus("Heartbeat-Fehler - siehe Fehlerprotokoll", MonitorState.Error); }
        }
        private void ScheduleUpdateCheck()
        {
            if (!settings.UpdateEnabled || String.IsNullOrEmpty(settings.UpdateRepository)) return;
            updateTimer = new System.Threading.Timer(delegate { CheckForUpdates(false); }, null, 8000, System.Threading.Timeout.Infinite);
        }
        private void CheckForUpdates(bool manual)
        {
            if (String.IsNullOrEmpty(settings.UpdateRepository))
            {
                main.SetUpdateStatus("Bitte zuerst das GitHub-Repository eintragen und speichern."); return;
            }
            main.SetUpdateStatus("GitHub wird geprüft ...");
            GitHubUpdateService.CheckAsync(settings, delegate(UpdateCheckResult result)
            {
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
                        main.SetUpdateStatus("Die installierte Version ist aktuell (" + GitHubUpdateService.CurrentVersion + ").");
                        if (manual) MessageBox.Show("Es ist kein neueres Release verfügbar.", "Updateprüfung", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    main.SetUpdateStatus("Neue Version verfügbar: " + result.TagName);
                    if (MessageBox.Show("Version " + result.TagName + " ist verfügbar. Jetzt installieren und neu starten?", "Update verfügbar", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
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
