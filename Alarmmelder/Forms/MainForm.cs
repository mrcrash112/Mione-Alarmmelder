using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MioneAlarmmelder.Core;
using MioneAlarmmelder.Transport;

namespace MioneAlarmmelder.Forms
{
    public partial class MainForm : Form
    {
        private readonly NotifyIcon trayIcon; private readonly ContextMenuStrip trayMenu;
        private AppSettings settings; private bool allowClose;
        public event EventHandler SettingsSaved;
        public event EventHandler UpdateCheckRequested;

        public MainForm(AppSettings value)
        {
            settings = value; InitializeComponent(); LoadLogo(); LoadFields();
            saveButton.Click += SaveClick; testButton.Click += TestClick; pathDialogButton.Click += PathDialogClick;
            updateCheckButton.Click += delegate { if (ReadFields() && UpdateCheckRequested != null) UpdateCheckRequested(this, EventArgs.Empty); };
            FormClosing += MainFormClosing; Resize += Resized;
            trayMenu = new ContextMenuStrip(); trayMenu.Items.Add("Öffnen", null, delegate { ShowWindow(); });
            trayMenu.Items.Add("Beenden", null, delegate { allowClose = true; Close(); });
            trayIcon = new NotifyIcon(); trayIcon.Text = "Mione Alarmmelder - startet"; trayIcon.ContextMenuStrip = trayMenu; trayIcon.Visible = true;
            trayIcon.DoubleClick += delegate { ShowWindow(); }; SetStatus("Wird gestartet ...", MonitorState.Waiting); ResetConnectionStatus();
        }

        public void SetStatus(string text, MonitorState state)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string, MonitorState>(SetStatus), text, state); return; }
            Color color = state == MonitorState.Ok ? Color.LimeGreen : state == MonitorState.Error ? Color.Red : state == MonitorState.Sending ? Color.DeepSkyBlue : Color.Goldenrod;
            statusLabel.Text = text; ledPanel.BackColor = color;
            Icon old = trayIcon.Icon; trayIcon.Icon = CreateLedIcon(color); if (old != null) old.Dispose();
            trayIcon.Text = ShortText("Mione Alarmmelder - " + text, 63);
        }

        public void AddAlarm(AlarmMessage alarm)
        {
            if (InvokeRequired) { BeginInvoke(new Action<AlarmMessage>(AddAlarm), alarm); return; }
            ListViewItem item = new ListViewItem(alarm.DateText + " " + alarm.TimeText); item.SubItems.Add(alarm.Code);
            item.SubItems.Add(alarm.Location); item.SubItems.Add(alarm.Priority); item.SubItems.Add(alarm.ClearText); alarmList.Items.Insert(0, item);
            while (alarmList.Items.Count > 200) alarmList.Items.RemoveAt(alarmList.Items.Count - 1);
        }

        public void SetMqttStatus(string text, MonitorState state) { SetTransportStatus(mqttLedPanel, mqttStatusLabel, "MQTT", text, state); }
        public void SetTcpStatus(string text, MonitorState state) { SetTransportStatus(tcpLedPanel, tcpStatusLabel, "TCP", text, state); }

        public void ResetConnectionStatus()
        {
            SetMqttStatus(settings.MqttEnabled ? "bereit" : "deaktiviert", settings.MqttEnabled ? MonitorState.Waiting : MonitorState.Disabled);
            SetTcpStatus(settings.TcpEnabled ? "bereit" : "deaktiviert", settings.TcpEnabled ? MonitorState.Waiting : MonitorState.Disabled);
        }

        public void SetUpdateStatus(string text)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(SetUpdateStatus), text); return; }
            updateStatusLabel.Text = text;
        }

        public void ExitForUpdate() { allowClose = true; Close(); }

        public void SetPhones(KeyValuePair<string, bool>[] phones)
        {
            if (InvokeRequired) { BeginInvoke(new Action<KeyValuePair<string, bool>[]>(SetPhones), new object[] { phones }); return; }
            phoneList.Items.Clear(); for (int i = 0; i < phones.Length; i++) { ListViewItem item = new ListViewItem(phones[i].Key); item.SubItems.Add(phones[i].Value ? "Ja" : "Nein"); phoneList.Items.Add(item); }
        }

        private void LoadFields()
        {
            messagePathBox.Text = settings.MessageLogPath; alarmSettingsPathBox.Text = settings.AlarmSettingsPath;
            priorityPathBox.Text = settings.PriorityPath; translationPathBox.Text = settings.TranslationPath;
            mqttEnabledBox.Checked = settings.MqttEnabled; mqttHostBox.Text = settings.MqttHost; mqttPortBox.Text = settings.MqttPort.ToString();
            mqttTopicBox.Text = settings.MqttTopic; mqttUserBox.Text = settings.MqttUser; mqttPasswordBox.Text = settings.MqttPassword;
            tcpEnabledBox.Checked = settings.TcpEnabled; tcpHostBox.Text = settings.TcpHost; tcpPortBox.Text = settings.TcpPort.ToString();
            customerBox.Text = settings.CustomerId; pollBox.Text = settings.PollSeconds.ToString(); heartbeatBox.Text = settings.HeartbeatSeconds.ToString(); startupBox.Checked = settings.StartWithWindows;
            updateEnabledBox.Checked = settings.UpdateEnabled; updateRepositoryBox.Text = settings.UpdateRepository; updateAssetBox.Text = settings.UpdateAssetName;
            currentVersionLabel.Text = "Installierte Version: " + GitHubUpdateService.CurrentVersion;
        }

        private bool ReadFields()
        {
            int mqttPort, tcpPort, poll, heartbeat;
            if (!Int32.TryParse(mqttPortBox.Text, out mqttPort) || mqttPort < 1 || mqttPort > 65535 ||
                !Int32.TryParse(tcpPortBox.Text, out tcpPort) || tcpPort < 1 || tcpPort > 65535 ||
                !Int32.TryParse(pollBox.Text, out poll) || poll < 1 || !Int32.TryParse(heartbeatBox.Text, out heartbeat) || heartbeat < 10)
            { MessageBox.Show("Bitte gültige Ports, ein Prüfintervall ab 1 Sekunde und einen Heartbeat ab 10 Sekunden eingeben.", "Ungültige Eingabe", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
            settings.MessageLogPath = messagePathBox.Text.Trim(); settings.AlarmSettingsPath = alarmSettingsPathBox.Text.Trim();
            settings.PriorityPath = priorityPathBox.Text.Trim(); settings.TranslationPath = translationPathBox.Text.Trim();
            settings.MqttEnabled = mqttEnabledBox.Checked; settings.MqttHost = mqttHostBox.Text.Trim(); settings.MqttPort = mqttPort;
            settings.MqttTopic = mqttTopicBox.Text.Trim(); settings.MqttUser = mqttUserBox.Text.Trim(); settings.MqttPassword = mqttPasswordBox.Text;
            settings.TcpEnabled = tcpEnabledBox.Checked; settings.TcpHost = tcpHostBox.Text.Trim(); settings.TcpPort = tcpPort;
            settings.CustomerId = customerBox.Text.Trim(); settings.PollSeconds = poll; settings.HeartbeatSeconds = heartbeat; settings.StartWithWindows = startupBox.Checked;
            settings.UpdateEnabled = updateEnabledBox.Checked; settings.UpdateRepository = updateRepositoryBox.Text.Trim(); settings.UpdateAssetName = updateAssetBox.Text.Trim();
            if (settings.UpdateEnabled && settings.UpdateRepository.Length > 0 && settings.UpdateRepository.IndexOf('/') < 1) { MessageBox.Show("Das GitHub-Repository muss im Format Besitzer/Repository angegeben werden.", "Ungültige Updatequelle", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
            if (settings.UpdateAssetName.Length == 0) settings.UpdateAssetName = "MioneAlarmmelder.exe"; return true;
        }

        private void SaveClick(object sender, EventArgs e)
        {
            if (!ReadFields()) return; string[] missing = settings.MissingFiles();
            if (missing.Length > 0) { MessageBox.Show("Nicht gefunden: " + String.Join(", ", missing), "Dateien fehlen", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            if ((settings.MqttEnabled && (settings.MqttHost.Length == 0 || settings.MqttTopic.Length == 0)) || (settings.TcpEnabled && settings.TcpHost.Length == 0))
            { MessageBox.Show("Für aktivierte Versandwege müssen Server und MQTT-Topic angegeben sein.", "Verbindungsdaten fehlen", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            SettingsStore.Save(settings); StartupRegistration.Apply(settings.StartWithWindows);
            ResetConnectionStatus();
            if (SettingsSaved != null) SettingsSaved(this, EventArgs.Empty);
            MessageBox.Show("Einstellungen gespeichert.", "Mione Alarmmelder", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void TestClick(object sender, EventArgs e)
        {
            if (!ReadFields()) return;
            if (!settings.MqttEnabled && !settings.TcpEnabled) { MessageBox.Show("Aktivieren Sie zuerst MQTT und/oder TCP.", "Verbindungstest", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            Cursor = Cursors.WaitCursor; string errors = "";
            if (settings.MqttEnabled)
            {
                SetMqttStatus("wird geprüft", MonitorState.Sending);
                try { MqttPublisher.Publish(settings.MqttHost, settings.MqttPort, settings.MqttUser, settings.MqttPassword, settings.MqttTopic.Replace("{kunde}", settings.CustomerId), "{\"test\":true}"); SetMqttStatus("verbunden", MonitorState.Ok); }
                catch (Exception ex) { SetMqttStatus("Fehler: " + ex.Message, MonitorState.Error); errors += "MQTT: " + ex.Message + "\r\n"; }
            }
            else SetMqttStatus("deaktiviert", MonitorState.Disabled);
            if (settings.TcpEnabled)
            {
                SetTcpStatus("wird geprüft", MonitorState.Sending);
                try { TcpPublisher.Publish(settings.TcpHost, settings.TcpPort, "{\"test\":true}"); SetTcpStatus("verbunden", MonitorState.Ok); }
                catch (Exception ex) { SetTcpStatus("Fehler: " + ex.Message, MonitorState.Error); errors += "TCP: " + ex.Message + "\r\n"; }
            }
            else SetTcpStatus("deaktiviert", MonitorState.Disabled);
            Cursor = Cursors.Default;
            MessageBox.Show(errors.Length == 0 ? "Alle aktivierten Verbindungen wurden erfolgreich geprüft." : errors.Trim(), "Verbindungstest", MessageBoxButtons.OK, errors.Length == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        }

        private void PathDialogClick(object sender, EventArgs e) { if (!ReadFields()) return; using (PathSettingsForm f = new PathSettingsForm(settings)) if (f.ShowDialog(this) == DialogResult.OK) { LoadFields(); if (SettingsSaved != null) SettingsSaved(this, EventArgs.Empty); } }
        private void MainFormClosing(object sender, FormClosingEventArgs e) { if (!allowClose && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); trayIcon.ShowBalloonTip(1500, "Mione Alarmmelder", "Die Überwachung läuft im Hintergrund weiter.", ToolTipIcon.Info); } else { trayIcon.Visible = false; trayIcon.Dispose(); } }
        private void Resized(object sender, EventArgs e) { if (WindowState == FormWindowState.Minimized) Hide(); }
        private void ShowWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); }
        private void SetTransportStatus(Panel panel, Label label, string name, string text, MonitorState state)
        {
            if (InvokeRequired) { BeginInvoke((MethodInvoker)delegate { SetTransportStatus(panel, label, name, text, state); }); return; }
            panel.BackColor = state == MonitorState.Ok ? Color.LimeGreen : state == MonitorState.Error ? Color.Red : state == MonitorState.Sending ? Color.DeepSkyBlue : state == MonitorState.Disabled ? Color.Gray : Color.Goldenrod;
            label.Text = name + ": " + ShortText(text, 38); label.Tag = text;
        }
        private void LoadLogo() { logoPicture.Image = EmbeddedImages.LoadLogo(); }
        private static string ShortText(string text, int length) { return text.Length <= length ? text : text.Substring(0, length); }
        private static Icon CreateLedIcon(Color color)
        {
            using (Bitmap bitmap = new Bitmap(16, 16)) using (Graphics g = Graphics.FromImage(bitmap))
            { g.Clear(Color.Transparent); using (Brush outer = new SolidBrush(Color.FromArgb(50, 50, 50))) g.FillEllipse(outer, 0, 0, 15, 15); using (Brush led = new SolidBrush(color)) g.FillEllipse(led, 3, 3, 9, 9); IntPtr h = bitmap.GetHicon(); Icon icon = (Icon)Icon.FromHandle(h).Clone(); DestroyIcon(h); return icon; }
        }
        [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool DestroyIcon(IntPtr handle);
    }
}
