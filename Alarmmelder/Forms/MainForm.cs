using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using MioneAlarmmelder.Core;
using MioneAlarmmelder.Transport;

namespace MioneAlarmmelder.Forms
{
    public partial class MainForm : Form
    {
        private readonly NotifyIcon trayIcon; private readonly ContextMenuStrip trayMenu;
        private readonly FirebaseAuthService firebaseAuthService;
        private AppSettings settings; private bool allowClose;
        private bool overviewUpdateHighlight;
        private List<AlarmHistoryEntry> alarmHistory;
        private int alarmSortColumn; private bool alarmSortAscending = false;
        private ToolTip alarmToolTip; private long toolTipAlarmId = -1;
        private AlarmProgressForm alarmProgressForm;
        private ContextMenuStrip errorListMenu; private ToolStripMenuItem errorCopyMessageItem;
        private ContextMenuStrip robotCommandMenu; private ToolStripMenuItem robotCommandCopyItem;
        private List<ErrorLogEntry> errorEntries = new List<ErrorLogEntry>(); private int errorSortColumn; private bool errorSortAscending = false;
        private int firebaseLoginPending;
        public event EventHandler SettingsSaved;
        public event EventHandler UpdateCheckRequested;
        public event EventHandler TestAlarmRequested;
        public event EventHandler UrgentTestAlarmRequested;

        public MainForm(AppSettings value) : this(value, null) { }

        public MainForm(AppSettings value, FirebaseAuthService authService)
        {
            settings = value;
            firebaseAuthService = authService ?? new FirebaseAuthService(settings);
            InitializeComponent(); ErrorLogger.ConfigureMaximum(settings.ErrorHistoryLimit); alarmHistory = AlarmHistoryStore.Load(settings.AlarmHistoryLimit); LoadLogo(); LoadFields();
            alarmViewFilter.Items.Add("Nur unbestätigte"); alarmViewFilter.Items.Add("Alle Alarme"); alarmViewFilter.SelectedIndex = 0;
            alarmPriorityFilter.Items.Add("Alle Prioritäten"); alarmPriorityFilter.Items.Add("attention"); alarmPriorityFilter.Items.Add("urgent");
            alarmPriorityFilter.Items.Add("messages"); alarmPriorityFilter.Items.Add("technical"); alarmPriorityFilter.Items.Add("message"); alarmPriorityFilter.Items.Add("System"); alarmPriorityFilter.SelectedIndex = 0;
            alarmLimitBox.Value = settings.AlarmHistoryLimit;
            errorViewFilter.Items.Add("Nur unbestätigte"); errorViewFilter.Items.Add("Alle Fehler"); errorViewFilter.SelectedIndex = 0; errorLimitBox.Value = settings.ErrorHistoryLimit;
            saveButton.Click += SaveClick; testButton.Click += TestClick; pathDialogButton.Click += PathDialogClick;
            infoButton.Click += delegate { using (InfoForm info = new InfoForm()) info.ShowDialog(this); };
            testAlarmButton.Click += TestAlarmClick;
            urgentTestAlarmButton.Click += delegate { if (UrgentTestAlarmRequested != null) UrgentTestAlarmRequested(this, EventArgs.Empty); };
            updateCheckButton.Click += delegate { if (ReadFields() && UpdateCheckRequested != null) UpdateCheckRequested(this, EventArgs.Empty); };
            dpProcessCheckButton.Click += delegate { RenderDpProcessFiles(); };
            firebasePasswordLoginButton.Click += FirebasePasswordLoginClick;
            firebaseGoogleLoginButton.Click += FirebaseGoogleLoginClick;
            firebaseAppleLoginButton.Click += FirebaseAppleLoginClick;
            firebaseSmsLoginButton.Click += FirebaseSmsLoginClick;
            firebaseEmailCodeLoginButton.Click += FirebaseEmailCodeLoginClick;
            firebaseRefreshButton.Click += FirebaseRefreshClick;
            firebaseSignOutButton.Click += FirebaseSignOutClick;
            robotCommandClearButton.Click += delegate { robotCommandList.Items.Clear(); robotCommandPayloadBox.Clear(); };
            robotCommandList.SelectedIndexChanged += RobotCommandSelectionChanged;
            robotCommandMenu = new ContextMenuStrip(); robotCommandCopyItem = new ToolStripMenuItem("Funktionslog kopieren");
            robotCommandCopyItem.Click += delegate { CopySelectedRobotCommands(); };
            robotCommandMenu.Opening += delegate { robotCommandCopyItem.Enabled = robotCommandList.SelectedItems.Count > 0; };
            robotCommandMenu.Items.Add(robotCommandCopyItem); robotCommandList.ContextMenuStrip = robotCommandMenu; robotCommandPayloadBox.ContextMenuStrip = robotCommandMenu;
            robotCommandList.KeyDown += RobotCommandListKeyDown; robotCommandPayloadBox.KeyDown += RobotCommandPayloadKeyDown;
            errorRefreshButton.Click += delegate { LoadErrorLog(); };
            errorClearButton.Click += ErrorClearClick;
            ErrorLogger.ErrorLogged += ErrorWasLogged;
            tabs.SelectedIndexChanged += delegate { UpdateActionButtons(); };
            tabs.DrawItem += DrawTab;
            alarmList.ColumnClick += AlarmColumnClick; alarmViewFilter.SelectedIndexChanged += delegate { RenderAlarmList(); };
            alarmPriorityFilter.SelectedIndexChanged += delegate { RenderAlarmList(); };
            acknowledgeSelectedButton.Click += AcknowledgeSelectedClick; acknowledgeAllButton.Click += AcknowledgeAllClick;
            alarmLimitBox.ValueChanged += AlarmLimitChanged;
            errorList.ColumnClick += ErrorColumnClick; errorViewFilter.SelectedIndexChanged += delegate { RenderErrorList(); };
            errorListMenu = new ContextMenuStrip(); errorCopyMessageItem = new ToolStripMenuItem("Fehlermeldung kopieren");
            errorCopyMessageItem.Click += delegate { CopySelectedErrorMessages(); };
            errorListMenu.Opening += delegate { errorCopyMessageItem.Enabled = errorList.SelectedItems.Count > 0; };
            errorListMenu.Items.Add(errorCopyMessageItem); errorList.ContextMenuStrip = errorListMenu;
            errorList.KeyDown += ErrorListKeyDown;
            errorAcknowledgeSelectedButton.Click += ErrorAcknowledgeSelectedClick; errorAcknowledgeAllButton.Click += ErrorAcknowledgeAllClick;
            errorLimitBox.ValueChanged += ErrorLimitChanged;
            alarmToolTip = new ToolTip(); alarmToolTip.AutoPopDelay = 15000; alarmToolTip.InitialDelay = 300; alarmToolTip.ReshowDelay = 100; alarmToolTip.ToolTipTitle = "Alarmhilfe";
            alarmList.MouseMove += AlarmListMouseMove; alarmList.MouseLeave += delegate { ClearAlarmToolTip(); };
            FormClosing += MainFormClosing; Resize += Resized;
            trayMenu = new ContextMenuStrip(); trayMenu.Items.Add("Öffnen", null, delegate { ShowWindow(); });
            trayMenu.Items.Add("Beenden", null, delegate { allowClose = true; Close(); });
            trayIcon = new NotifyIcon(); trayIcon.Text = "Mione Alarmmelder - startet"; trayIcon.ContextMenuStrip = trayMenu; trayIcon.Visible = true;
            trayIcon.DoubleClick += delegate { ShowWindow(); }; SetStatus("Wird gestartet ...", MonitorState.Waiting); ResetConnectionStatus();
            versionLabel.Text = "Version " + GitHubUpdateService.CurrentVersionLabel;
            errorPathLabel.Text = "Datei: " + ErrorLogger.FilePath; LoadErrorLog();
            RenderDpProcessFiles();
            UpdateActionButtons(); overviewUpdateHighlight = HasUnacknowledgedUpdate(); RenderAlarmList();
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
            if (String.IsNullOrEmpty(alarm.Priority) || String.Equals(alarm.Priority, "unbekannt", StringComparison.OrdinalIgnoreCase)) alarm.Priority = "System";
            AlarmHistoryEntry entry = new AlarmHistoryEntry { Id = DateTime.UtcNow.Ticks, ReceivedAt = DateTime.Now, Acknowledged = false, Alarm = alarm };
            alarmHistory.Insert(0, entry); while (alarmHistory.Count > settings.AlarmHistoryLimit) alarmHistory.RemoveAt(alarmHistory.Count - 1);
            AlarmHistoryStore.Save(alarmHistory, settings.AlarmHistoryLimit); RenderAlarmList();
        }

        private void RenderAlarmList()
        {
            if (alarmHistory == null) return;
            List<AlarmHistoryEntry> visible = new List<AlarmHistoryEntry>();
            bool onlyOpen = alarmViewFilter.SelectedIndex <= 0;
            string priority = alarmPriorityFilter.SelectedIndex <= 0 ? "" : alarmPriorityFilter.SelectedItem.ToString();
            for (int i = 0; i < alarmHistory.Count; i++)
            {
                AlarmHistoryEntry entry = alarmHistory[i];
                if (onlyOpen && entry.Acknowledged) continue;
                if (priority.Length > 0 && !String.Equals(entry.Alarm.Priority, priority, StringComparison.OrdinalIgnoreCase)) continue;
                visible.Add(entry);
            }
            visible.Sort(CompareAlarmEntries); alarmList.BeginUpdate(); alarmList.Items.Clear();
            for (int i = 0; i < visible.Count; i++)
            {
                AlarmHistoryEntry entry = visible[i]; AlarmMessage alarm = entry.Alarm;
                ListViewItem item = new ListViewItem(alarm.DateText + " " + alarm.TimeText); item.SubItems.Add(alarm.Code);
                item.SubItems.Add(alarm.Location); item.SubItems.Add(alarm.CowNumber); item.SubItems.Add(alarm.Priority); item.SubItems.Add(alarm.ClearText);
                item.Tag = entry; item.BackColor = entry.Acknowledged ? Color.White : Color.LightCoral; alarmList.Items.Add(item);
            }
            alarmList.EndUpdate(); UpdateColumnHeadings();
        }

        private int CompareAlarmEntries(AlarmHistoryEntry left, AlarmHistoryEntry right)
        {
            int result;
            if (alarmSortColumn == 0) result = left.ReceivedAt.CompareTo(right.ReceivedAt);
            else if (alarmSortColumn == 1 || alarmSortColumn == 3) result = CompareNumericText(AlarmColumnValue(left.Alarm, alarmSortColumn), AlarmColumnValue(right.Alarm, alarmSortColumn));
            else result = String.Compare(AlarmColumnValue(left.Alarm, alarmSortColumn), AlarmColumnValue(right.Alarm, alarmSortColumn), StringComparison.CurrentCultureIgnoreCase);
            return alarmSortAscending ? result : -result;
        }
        private static int CompareNumericText(string left, string right)
        {
            long leftNumber, rightNumber;
            if (Int64.TryParse(left, out leftNumber) && Int64.TryParse(right, out rightNumber)) return leftNumber.CompareTo(rightNumber);
            return String.Compare(left, right, StringComparison.CurrentCultureIgnoreCase);
        }
        private static string AlarmColumnValue(AlarmMessage alarm, int column)
        {
            if (column == 1) return alarm.Code; if (column == 2) return alarm.Location; if (column == 3) return alarm.CowNumber;
            if (column == 4) return alarm.Priority; return alarm.ClearText;
        }
        private void AlarmColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (alarmSortColumn == e.Column) alarmSortAscending = !alarmSortAscending; else { alarmSortColumn = e.Column; alarmSortAscending = true; }
            RenderAlarmList();
        }
        private void UpdateColumnHeadings()
        {
            string[] names = new string[] { "Zeit", "Code", "Ort", "Kuh", "Priorität", "Alarmtext" };
            for (int i = 0; i < names.Length; i++) alarmList.Columns[i].Text = names[i] + (i == alarmSortColumn ? (alarmSortAscending ? " ▲" : " ▼") : "");
        }
        private void AcknowledgeSelectedClick(object sender, EventArgs e)
        {
            if (alarmList.SelectedItems.Count == 0) { MessageBox.Show("Bitte mindestens einen Alarm auswählen.", "Alarm bestätigen", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            for (int i = 0; i < alarmList.SelectedItems.Count; i++) { AlarmHistoryEntry entry = alarmList.SelectedItems[i].Tag as AlarmHistoryEntry; if (entry != null) entry.Acknowledged = true; }
            AlarmHistoryStore.Save(alarmHistory, settings.AlarmHistoryLimit); RefreshAcknowledgementState();
        }
        private void AcknowledgeAllClick(object sender, EventArgs e)
        {
            for (int i = 0; i < alarmHistory.Count; i++) alarmHistory[i].Acknowledged = true;
            AlarmHistoryStore.Save(alarmHistory, settings.AlarmHistoryLimit); RefreshAcknowledgementState();
        }
        private void RefreshAcknowledgementState()
        {
            overviewUpdateHighlight = HasUnacknowledgedUpdate(); tabs.Invalidate(); RenderAlarmList();
        }
        private bool HasUnacknowledgedUpdate()
        {
            for (int i = 0; i < alarmHistory.Count; i++) if (!alarmHistory[i].Acknowledged && String.Equals(alarmHistory[i].Alarm.Code, "UPDATE", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
        private void AlarmLimitChanged(object sender, EventArgs e)
        {
            settings.AlarmHistoryLimit = (int)alarmLimitBox.Value; while (alarmHistory.Count > settings.AlarmHistoryLimit) alarmHistory.RemoveAt(alarmHistory.Count - 1);
            AlarmHistoryStore.Save(alarmHistory, settings.AlarmHistoryLimit); SettingsStore.Save(settings); RenderAlarmList();
        }
        private void AlarmListMouseMove(object sender, MouseEventArgs e)
        {
            ListViewHitTestInfo hit = alarmList.HitTest(e.Location); if (hit.Item == null || hit.SubItem == null) { ClearAlarmToolTip(); return; }
            int subItemIndex = -1; for (int i = 0; i < hit.Item.SubItems.Count; i++) if (Object.ReferenceEquals(hit.Item.SubItems[i], hit.SubItem)) { subItemIndex = i; break; }
            AlarmHistoryEntry entry = hit.Item.Tag as AlarmHistoryEntry;
            if (subItemIndex != 5 || entry == null || (String.IsNullOrEmpty(entry.Alarm.Cause) && String.IsNullOrEmpty(entry.Alarm.Solution))) { ClearAlarmToolTip(); return; }
            if (toolTipAlarmId == entry.Id) return;
            string text = "";
            if (!String.IsNullOrEmpty(entry.Alarm.Cause)) text = "Mögliche Ursache:\r\n" + entry.Alarm.Cause;
            if (!String.IsNullOrEmpty(entry.Alarm.Solution)) text += (text.Length > 0 ? "\r\n\r\n" : "") + "Mögliche Lösung:\r\n" + entry.Alarm.Solution;
            toolTipAlarmId = entry.Id; alarmToolTip.Show(text, alarmList, e.X + 15, e.Y + 15, 15000);
        }
        private void ClearAlarmToolTip()
        {
            if (toolTipAlarmId == -1) return; toolTipAlarmId = -1; alarmToolTip.Hide(alarmList);
        }

        public void SetMqttStatus(string text, MonitorState state) { SetTransportStatus(mqttLedPanel, mqttStatusLabel, "System-MQTT", text, state); }
        public void SetBackupMqttStatus(string text, MonitorState state) { SetTransportStatus(backupLedPanel, backupStatusLabel, "Backup-MQTT", text, state); }
        public void SetTcpStatus(string text, MonitorState state) { SetTransportStatus(tcpLedPanel, tcpStatusLabel, "TCP", text, state); }
        public void SetModemStatus(string transport, string text, MonitorState state)
        {
            if (InvokeRequired) { BeginInvoke((MethodInvoker)delegate { SetModemStatus(transport, text, state); }); return; }
            modemLedPanel.BackColor = StateColor(state);
            modemStatusLabel.Text = "Modem" + (String.IsNullOrEmpty(transport) ? "" : " (" + transport + ")") + ": " + ShortText(text, 30);
            modemStatusLabel.Tag = text;
        }
        public void SetFirmwareStatus(string text, MonitorState state)
        {
            if (InvokeRequired) { BeginInvoke((MethodInvoker)delegate { SetFirmwareStatus(text, state); }); return; }
            firmwareLedPanel.BackColor = StateColor(state);
            firmwareStatusLabel.Text = "Modem-Firmware: " + ShortText(String.IsNullOrEmpty(text) ? "warte auf Status" : text, 52);
            firmwareStatusLabel.Tag = text;
        }

        public void ResetConnectionStatus()
        {
            bool systemReady = settings.SystemMqttReady;
            bool backupReady = settings.BackupMqttConfigured;
            SetMqttStatus(systemReady ? "Online" : "Offline",
                systemReady ? MonitorState.Ok : MonitorState.Disabled);
            SetBackupMqttStatus(backupReady ? "bereit" : "deaktiviert",
                backupReady ? MonitorState.Waiting : MonitorState.Disabled);
            SetTcpStatus(settings.TcpEnabled ? "bereit" : "deaktiviert", settings.TcpEnabled ? MonitorState.Waiting : MonitorState.Disabled);
            SetModemStatus(settings.TcpEnabled ? "Socket" : systemReady ? "MQTT" : "",
                settings.TcpEnabled ? "wird geprüft" : systemReady ? "warte auf Rückmeldung" : "deaktiviert",
                settings.TcpEnabled || systemReady ? MonitorState.Waiting : MonitorState.Disabled);
            SetFirmwareStatus(settings.TcpEnabled || systemReady ? "warte auf Status" : "deaktiviert",
                settings.TcpEnabled || systemReady ? MonitorState.Waiting : MonitorState.Disabled);
        }

        public void SetUpdateStatus(string text)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(SetUpdateStatus), text); return; }
            updateStatusLabel.Text = text;
        }

        public void ShowUpdateNotification(string tagName)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(ShowUpdateNotification), tagName); return; }
            AlarmMessage notice = new AlarmMessage
            {
                DateText = DateTime.Now.ToString("dd.MM.yy"), TimeText = DateTime.Now.ToString("HH:mm:ss"), Code = "UPDATE",
                Location = "GitHub", CowNumber = "0", Priority = "message",
                ClearText = "Neue Programmversion " + tagName + " verfügbar. Installation im Tab Updates starten."
            };
            AddAlarm(notice); overviewUpdateHighlight = true; tabs.Invalidate();
        }

        public void ExitForUpdate() { allowClose = true; Close(); }

        private void LoadErrorLog()
        {
            if (InvokeRequired) { BeginInvoke((MethodInvoker)LoadErrorLog); return; }
            errorEntries = new List<ErrorLogEntry>(ErrorLogger.ReadRecent(settings.ErrorHistoryLimit)); RenderErrorList();
        }

        private void ErrorWasLogged(object sender, ErrorLoggedEventArgs e)
        {
            if (InvokeRequired) { BeginInvoke(new EventHandler<ErrorLoggedEventArgs>(ErrorWasLogged), sender, e); return; }
            LoadErrorLog();
        }

        private void ErrorClearClick(object sender, EventArgs e)
        {
            if (MessageBox.Show("Fehlerprotokoll wirklich löschen?", "Fehlerprotokoll", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            ErrorLogger.Clear(); errorEntries.Clear(); RenderErrorList();
        }

        private void RenderErrorList()
        {
            List<ErrorLogEntry> visible = new List<ErrorLogEntry>(); bool onlyOpen = errorViewFilter.SelectedIndex <= 0;
            for (int i = 0; i < errorEntries.Count; i++) if (!onlyOpen || !errorEntries[i].Acknowledged) visible.Add(errorEntries[i]);
            visible.Sort(CompareErrorEntries); errorList.BeginUpdate(); errorList.Items.Clear();
            for (int i = 0; i < visible.Count; i++)
            {
                ErrorLogEntry entry = visible[i]; ListViewItem item = new ListViewItem(entry.Time.ToString("dd.MM.yyyy HH:mm:ss"));
                item.SubItems.Add(entry.Source); item.SubItems.Add(entry.Message); item.Tag = entry; item.BackColor = entry.Acknowledged ? Color.White : Color.LightCoral; errorList.Items.Add(item);
            }
            errorList.EndUpdate(); string[] names = new string[] { "Zeit", "Quelle", "Fehlermeldung" };
            for (int i = 0; i < names.Length; i++) errorList.Columns[i].Text = names[i] + (i == errorSortColumn ? (errorSortAscending ? " ▲" : " ▼") : "");
        }
        private int CompareErrorEntries(ErrorLogEntry left, ErrorLogEntry right)
        {
            int result = errorSortColumn == 0 ? left.Time.CompareTo(right.Time) : String.Compare(errorSortColumn == 1 ? left.Source : left.Message, errorSortColumn == 1 ? right.Source : right.Message, StringComparison.CurrentCultureIgnoreCase);
            return errorSortAscending ? result : -result;
        }
        private void ErrorColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (errorSortColumn == e.Column) errorSortAscending = !errorSortAscending; else { errorSortColumn = e.Column; errorSortAscending = true; } RenderErrorList();
        }
        private void ErrorListKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.C)
            {
                CopySelectedErrorMessages();
                e.Handled = true;
            }
        }
        private void CopySelectedErrorMessages()
        {
            if (errorList.SelectedItems.Count == 0) return;
            List<string> messages = new List<string>();
            for (int i = 0; i < errorList.SelectedItems.Count; i++)
            {
                ErrorLogEntry entry = errorList.SelectedItems[i].Tag as ErrorLogEntry;
                if (entry != null && entry.Message.Length > 0) messages.Add(entry.Message);
            }
            if (messages.Count > 0) Clipboard.SetText(String.Join(Environment.NewLine, messages.ToArray()));
        }
        private void ErrorAcknowledgeSelectedClick(object sender, EventArgs e)
        {
            if (errorList.SelectedItems.Count == 0) { MessageBox.Show("Bitte mindestens einen Fehler auswählen.", "Fehler bestätigen", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            long[] ids = new long[errorList.SelectedItems.Count]; for (int i = 0; i < ids.Length; i++) ids[i] = ((ErrorLogEntry)errorList.SelectedItems[i].Tag).Id;
            ErrorLogger.Acknowledge(ids); LoadErrorLog();
        }
        private void ErrorAcknowledgeAllClick(object sender, EventArgs e) { ErrorLogger.AcknowledgeAll(); LoadErrorLog(); }
        private void ErrorLimitChanged(object sender, EventArgs e)
        {
            settings.ErrorHistoryLimit = (int)errorLimitBox.Value; ErrorLogger.ConfigureMaximum(settings.ErrorHistoryLimit); SettingsStore.Save(settings); LoadErrorLog();
        }

        public void SetPhones(MobileNumberConfig[] phones)
        {
            if (InvokeRequired) { BeginInvoke(new Action<MobileNumberConfig[]>(SetPhones), new object[] { phones }); return; }
            phoneList.Items.Clear(); for (int i = 0; i < phones.Length; i++) { ListViewItem item = new ListViewItem(phones[i].Number); item.SubItems.Add(phones[i].Active ? "Ja" : "Nein"); item.SubItems.Add(phones[i].AlarmModeText); item.SubItems.Add(phones[i].TechnicalAlarmMessagingFromText); item.SubItems.Add(phones[i].TechnicalAlarmMessagingUntilText); phoneList.Items.Add(item); }
        }

        public void RefreshFirebaseFields()
        {
            if (InvokeRequired) { BeginInvoke((MethodInvoker)RefreshFirebaseFields); return; }
            LoadFirebaseFields();
        }

        public void SetFirebaseStatus(string text, MonitorState state)
        {
            if (InvokeRequired) { BeginInvoke((MethodInvoker)delegate { SetFirebaseStatus(text, state); }); return; }
            firebaseLedPanel.BackColor = StateColor(state);
            firebaseStatusBox.Text = text;
        }

        private void LoadFirebaseFields()
        {
            firebaseEmailBox.Text = settings.FirebaseEmail;
            firebasePhoneBox.Text = settings.FirebasePhoneNumber;
            firebaseUidBox.Text = settings.FirebaseUid;
            firebaseDisplayNameBox.Text = settings.FirebaseDisplayName;
            firebaseProviderBox.Text = settings.FirebaseProviderId;
            string topicRoot = String.IsNullOrEmpty(settings.MqttTopicRoot) ? "<system-id>" : settings.MqttTopicRoot;
            dpProcessTopicLabel.Text = "MQTT-Topic: " + topicRoot + "/Melkroboter";
            FirebaseAuthSession activeSession = firebaseAuthService == null ? null : firebaseAuthService.CurrentSession;
            if (activeSession != null) SetFirebaseStatus("Session aktiv\r\n" + BuildFirebaseSessionSummary(activeSession), MonitorState.Ok);
            else if (!String.IsNullOrEmpty(settings.FirebaseUid)) SetFirebaseStatus("System-ID gespeichert, neu anmelden nötig.", MonitorState.Error);
            else SetFirebaseStatus("Keine aktive Firebase-Session.", MonitorState.Disabled);
        }

        private static string BuildFirebaseSessionSummary(FirebaseAuthSession value)
        {
            List<string> parts = new List<string>();
            if (value == null) return "";
            if (!String.IsNullOrEmpty(value.Uid)) parts.Add("UID " + value.Uid);
            if (!String.IsNullOrEmpty(value.Email)) parts.Add(value.Email);
            return parts.Count == 0 ? "unbekannt" : String.Join(" | ", parts.ToArray());
        }

        private void LoadFields()
        {
            messagePathBox.Text = settings.MessageLogPath; alarmSettingsPathBox.Text = settings.AlarmSettingsPath;
            priorityPathBox.Text = settings.PriorityPath; translationPathBox.Text = settings.TranslationPath; alarmCatalogPathBox.Text = settings.AlarmCatalogPath;
            mqttEnabledBox.Checked = settings.MqttEnabled; mqttHostBox.Text = settings.MqttHost; mqttPortBox.Text = settings.MqttPort.ToString();
            mqttUserBox.Text = settings.MqttUser; mqttPasswordBox.Text = settings.MqttPassword; modemImeiBox.Text = settings.ModemImei;
            tcpEnabledBox.Checked = settings.TcpEnabled; tcpHostBox.Text = settings.TcpHost; tcpPortBox.Text = settings.TcpPort.ToString();
            pollBox.Text = settings.PollSeconds.ToString(); startupBox.Checked = settings.StartWithWindows;
            alarmProgressBox.Checked = settings.ShowAlarmProgress;
            updateEnabledBox.Checked = settings.UpdateEnabled; updateRepositoryBox.Text = settings.UpdateRepository; updateAssetBox.Text = settings.UpdateAssetName;
            updateIntervalBox.Text = settings.UpdateCheckMinutes.ToString();
            updateChannelBox.SelectedIndex = String.Equals(settings.UpdateChannel, "beta", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            dpProcessEnabledBox.Checked = settings.DpProcessEnabled; dpProcessPathBox.Text = settings.DpProcessPath; dpProcessPollBox.Text = settings.DpProcessPollSeconds.ToString();
            LoadFirebaseFields();
        }

        private bool ReadFields()
        {
            int mqttPort = settings.MqttPort, tcpPort, poll, updateMinutes, dpProcessPoll;
            if ((mqttEnabledBox.Checked && (!Int32.TryParse(mqttPortBox.Text, out mqttPort) || mqttPort < 1 || mqttPort > 65535)) ||
                !Int32.TryParse(tcpPortBox.Text, out tcpPort) || tcpPort < 1 || tcpPort > 65535 ||
                !Int32.TryParse(pollBox.Text, out poll) || poll < 1)
            { MessageBox.Show("Bitte gültige Ports und ein Prüfintervall ab 1 Sekunde eingeben.", "Ungültige Eingabe", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
            if (!mqttEnabledBox.Checked)
            {
                int backupPort;
                if (Int32.TryParse(mqttPortBox.Text, out backupPort) && backupPort >= 1 && backupPort <= 65535) mqttPort = backupPort;
            }
            settings.MessageLogPath = messagePathBox.Text.Trim(); settings.AlarmSettingsPath = alarmSettingsPathBox.Text.Trim();
            settings.PriorityPath = priorityPathBox.Text.Trim(); settings.TranslationPath = translationPathBox.Text.Trim(); settings.AlarmCatalogPath = alarmCatalogPathBox.Text.Trim();
            settings.MqttEnabled = mqttEnabledBox.Checked; settings.MqttHost = mqttHostBox.Text.Trim(); settings.MqttPort = mqttPort;
            settings.MqttUser = mqttUserBox.Text.Trim(); settings.MqttPassword = mqttPasswordBox.Text; settings.ModemImei = modemImeiBox.Text.Trim();
            settings.TcpEnabled = tcpEnabledBox.Checked; settings.TcpHost = tcpHostBox.Text.Trim(); settings.TcpPort = tcpPort;
            settings.PollSeconds = poll; settings.StartWithWindows = startupBox.Checked;
            settings.ShowAlarmProgress = alarmProgressBox.Checked;
            settings.UpdateEnabled = updateEnabledBox.Checked; settings.UpdateRepository = updateRepositoryBox.Text.Trim(); settings.UpdateAssetName = updateAssetBox.Text.Trim();
            settings.UpdateChannel = updateChannelBox.SelectedIndex == 1 ? "beta" : "stable";
            if (!Int32.TryParse(updateIntervalBox.Text, out updateMinutes) || updateMinutes < 5) { MessageBox.Show("Das Update-Prüfintervall muss mindestens 5 Minuten betragen.", "Ungültige Eingabe", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
            settings.UpdateCheckMinutes = updateMinutes;
            if (!Int32.TryParse(dpProcessPollBox.Text, out dpProcessPoll) || dpProcessPoll < 5) { MessageBox.Show("Das Melkroboter-Prüfintervall muss mindestens 5 Sekunden betragen.", "Ungültige Eingabe", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
            settings.DpProcessEnabled = dpProcessEnabledBox.Checked; settings.DpProcessPath = dpProcessPathBox.Text.Trim(); settings.DpProcessPollSeconds = dpProcessPoll;
            settings.FirebaseEmail = firebaseEmailBox.Text.Trim(); settings.FirebasePhoneNumber = firebasePhoneBox.Text.Trim();
            if (settings.UpdateEnabled && settings.UpdateRepository.Length > 0 && settings.UpdateRepository.IndexOf('/') < 1) { MessageBox.Show("Das GitHub-Repository muss im Format Besitzer/Repository angegeben werden.", "Ungültige Updatequelle", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
            if (settings.UpdateAssetName.Length == 0) settings.UpdateAssetName = "MioneAlarmmelder-*.zip"; return true;
        }

        private void RenderDpProcessFiles()
        {
            if (dpProcessFileList == null) return;
            dpProcessFileList.Items.Clear();
            MilkingRobotFileCheck[] checks = MilkingRobotPublisher.CheckFiles(dpProcessPathBox.Text);
            for (int i = 0; i < checks.Length; i++)
            {
                ListViewItem item = new ListViewItem(checks[i].Exists ? "OK" : "Fehlt");
                item.SubItems.Add(checks[i].Name); item.SubItems.Add(checks[i].Path);
                item.BackColor = checks[i].Exists ? Color.White : Color.MistyRose;
                dpProcessFileList.Items.Add(item);
            }
            string user = String.IsNullOrEmpty(settings.SystemMqttTopicRoot) ? "<system-id>" : settings.SystemMqttTopicRoot;
            dpProcessTopicLabel.Text = "MQTT-Topic: " + user + "/Melkroboter";
        }

        private void SaveClick(object sender, EventArgs e)
        {
            if (!ReadFields()) return; string[] missing = settings.MissingFiles();
            if (missing.Length > 0) { MessageBox.Show("Nicht gefunden: " + String.Join(", ", missing), "Dateien fehlen", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            if (settings.MqttEnabled && (settings.MqttHost.Length == 0 || settings.MqttUser.Length == 0))
            { MessageBox.Show("Für den 2. MQTT-Pfad müssen Server und Benutzer/Topic angegeben sein.", "Verbindungsdaten fehlen", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            if (settings.TcpEnabled && settings.TcpHost.Length == 0)
            { MessageBox.Show("Für TCP muss der Server angegeben sein.", "Verbindungsdaten fehlen", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            SettingsStore.Save(settings); StartupRegistration.Apply(settings.StartWithWindows);
            ResetConnectionStatus();
            if (SettingsSaved != null) SettingsSaved(this, EventArgs.Empty);
            MessageBox.Show("Einstellungen gespeichert.", "Mione Alarmmelder", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void TestClick(object sender, EventArgs e)
        {
            if (!ReadFields()) return;
            if (!settings.SystemMqttReady && !settings.BackupMqttConfigured && !settings.TcpEnabled)
            { MessageBox.Show("Bitte zuerst System-, Backup-MQTT oder TCP aktivieren.", "Verbindungstest", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            Cursor = Cursors.WaitCursor; string errors = "";
            if (settings.SystemMqttReady)
            {
                SetMqttStatus("wird geprüft", MonitorState.Sending);
            }
            else SetMqttStatus("Offline", MonitorState.Disabled);
            if (settings.BackupMqttConfigured)
            {
                SetBackupMqttStatus("wird geprüft", MonitorState.Sending);
            }
            else SetBackupMqttStatus("deaktiviert", MonitorState.Disabled);
            if (settings.SystemMqttReady || settings.BackupMqttConfigured)
            {
                MqttRoutePublishResult mqtt = MqttRoutePublisher.Publish(settings, "Alarmfunktionen/Alarm", "{\"test\":true}", false);
                if (settings.SystemMqttReady)
                {
                    if (mqtt.SystemSuccessful) SetMqttStatus("Online", MonitorState.Ok);
                    else
                    {
                        SetMqttStatus("Offline", MonitorState.Error);
                        if (!String.IsNullOrEmpty(mqtt.SystemError))
                        {
                            ErrorLogger.Log("System-MQTT-Verbindungstest", mqtt.SystemError);
                            errors += "System-MQTT: " + mqtt.SystemError + "\r\n";
                        }
                    }
                }
                if (settings.BackupMqttConfigured)
                {
                    if (mqtt.BackupSuccessful) SetBackupMqttStatus("Online", MonitorState.Ok);
                    else
                    {
                        SetBackupMqttStatus("Offline", MonitorState.Error);
                        if (!String.IsNullOrEmpty(mqtt.BackupError))
                        {
                            ErrorLogger.Log("Backup-MQTT-Verbindungstest", mqtt.BackupError);
                            errors += "Backup-MQTT: " + mqtt.BackupError + "\r\n";
                        }
                    }
                }
            }
            if (settings.TcpEnabled)
            {
                SetTcpStatus("wird geprüft", MonitorState.Sending);
                try { TcpPublisher.Publish(settings.TcpHost, settings.TcpPort, "{\"test\":true}"); SetTcpStatus("verbunden", MonitorState.Ok); }
                catch (Exception ex) { ErrorLogger.Log("TCP-Verbindungstest", ex); SetTcpStatus("Fehler", MonitorState.Error); errors += "TCP: " + ex.Message + "\r\n"; }
            }
            else SetTcpStatus("deaktiviert", MonitorState.Disabled);
            Cursor = Cursors.Default;
            MessageBox.Show(errors.Length == 0 ? "Alle aktivierten Verbindungen wurden erfolgreich geprüft." : errors.Trim(), "Verbindungstest", MessageBoxButtons.OK, errors.Length == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        }

        private void TestAlarmClick(object sender, EventArgs e)
        {
            if (!ReadFields()) return;
            if (!settings.SystemMqttReady && !settings.BackupMqttConfigured && !settings.TcpEnabled) { MessageBox.Show("Bitte zuerst System-, Backup-MQTT oder TCP aktivieren.", "Testfehler", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            if (TestAlarmRequested != null) TestAlarmRequested(this, EventArgs.Empty);
        }

        private bool ReadFirebaseContactFields()
        {
            settings.FirebaseEmail = firebaseEmailBox.Text.Trim();
            settings.FirebasePhoneNumber = firebasePhoneBox.Text.Trim();
            return true;
        }

        private void LaunchFirebaseLogin(string mode, string title)
        {
            if (!ReadFirebaseContactFields()) return;
            if (Interlocked.Exchange(ref firebaseLoginPending, 1) != 0)
            {
                MessageBox.Show("Es läuft bereits eine Firebase-Anmeldung.", "Firebase Login", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            SetFirebaseStatus(title + " wird gestartet ...", MonitorState.Sending);
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    FirebaseAuthSession session = firebaseAuthService.SignInInteractive(mode, settings.FirebaseEmail, settings.FirebasePhoneNumber);
                    if (session == null) throw new InvalidOperationException("Der Firebase-Login wurde abgebrochen.");
                    if (IsDisposed || !IsHandleCreated) return;
                    try { BeginInvoke((MethodInvoker)delegate { FirebaseLoginSucceeded(title); }); } catch { }
                }
                catch (Exception ex)
                {
                    ErrorLogger.Log("Firebase Login", ex);
                    if (IsDisposed || !IsHandleCreated) return;
                    try { BeginInvoke((MethodInvoker)delegate { FirebaseLoginFailed(title, ex); }); } catch { }
                }
                finally
                {
                    Interlocked.Exchange(ref firebaseLoginPending, 0);
                }
            });
        }

        private void FirebaseLoginSucceeded(string title)
        {
            LoadFirebaseFields();
            RenderDpProcessFiles();
            SetFirebaseStatus(title + " erfolgreich.", MonitorState.Ok);
            ResetConnectionStatus();
            if (SettingsSaved != null) SettingsSaved(this, EventArgs.Empty);
        }

        private void FirebaseLoginFailed(string title, Exception ex)
        {
            SetFirebaseStatus(title + " fehlgeschlagen.", MonitorState.Error);
            MessageBox.Show(ex.Message, "Firebase Login", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void FirebasePasswordLoginClick(object sender, EventArgs e) { LaunchFirebaseLogin("password", "E-Mail / Passwort"); }
        private void FirebaseGoogleLoginClick(object sender, EventArgs e) { LaunchFirebaseLogin("google", "Google"); }
        private void FirebaseAppleLoginClick(object sender, EventArgs e) { LaunchFirebaseLogin("apple", "Apple"); }
        private void FirebaseSmsLoginClick(object sender, EventArgs e) { LaunchFirebaseLogin("sms", "SMS"); }
        private void FirebaseEmailCodeLoginClick(object sender, EventArgs e) { LaunchFirebaseLogin("emaillink", "E-Mail & Code"); }

        private void FirebaseRefreshClick(object sender, EventArgs e)
        {
            if (!ReadFirebaseContactFields()) return;
            if (!settings.HasFirebaseSession)
            {
                MessageBox.Show(String.IsNullOrEmpty(settings.FirebaseUid) ? "Es ist noch keine Firebase-Session gespeichert." : "Die System-ID ist gespeichert, aber die Firebase-Session muss neu angemeldet werden.", "Firebase Login", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            SetFirebaseStatus("Firebase-Session wird erneuert ...", MonitorState.Sending);
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    FirebaseAuthSession session = firebaseAuthService.RefreshSession();
                    if (session == null) throw new InvalidOperationException("Keine Firebase-Session vorhanden.");
                    if (IsDisposed || !IsHandleCreated) return;
                    BeginInvoke((MethodInvoker)delegate
                    {
                        LoadFirebaseFields();
                        RenderDpProcessFiles();
                        SetFirebaseStatus("Firebase-Session erneuert.", MonitorState.Ok);
                        ResetConnectionStatus();
                    });
                }
                catch (Exception ex)
                {
                    ErrorLogger.Log("Firebase Refresh", ex);
                    if (IsDisposed || !IsHandleCreated) return;
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            firebaseAuthService.InvalidateCurrentSession();
                            RefreshFirebaseFields();
                            SetFirebaseStatus("Firebase-Session konnte nicht erneuert werden.", MonitorState.Error);
                            MessageBox.Show(ex.Message, "Firebase Refresh", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        });
                    }
                    catch { }
                }
            });
        }

        private void FirebaseSignOutClick(object sender, EventArgs e)
        {
            firebaseAuthService.SignOut();
            LoadFirebaseFields();
            RenderDpProcessFiles();
            ResetConnectionStatus();
            SetFirebaseStatus("Firebase-Session abgemeldet.", MonitorState.Disabled);
            if (SettingsSaved != null) SettingsSaved(this, EventArgs.Empty);
        }

        private void PathDialogClick(object sender, EventArgs e) { if (!ReadFields()) return; using (PathSettingsForm f = new PathSettingsForm(settings)) if (f.ShowDialog(this) == DialogResult.OK) { LoadFields(); if (SettingsSaved != null) SettingsSaved(this, EventArgs.Empty); } }
        public void ShowAlarmProgress(AlarmProgressEvent value)
        {
            if (InvokeRequired) { BeginInvoke(new Action<AlarmProgressEvent>(ShowAlarmProgress), value); return; }
            if (!settings.ShowAlarmProgress || value == null) return;
            if (alarmProgressForm == null || alarmProgressForm.IsDisposed) alarmProgressForm = new AlarmProgressForm();
            alarmProgressForm.UpdateProgress(value);
        }

        public void AddMilkingRobotCommand(MilkingRobotCommandEventArgs value)
        {
            if (InvokeRequired) { BeginInvoke(new Action<MilkingRobotCommandEventArgs>(AddMilkingRobotCommand), value); return; }
            if (value == null || robotCommandList == null) return;
            MilkingRobotCommand command = value.Command;
            string parameters = "";
            if (command != null)
            {
                if (!String.IsNullOrEmpty(command.BoxNumber)) parameters += "box=" + command.BoxNumber;
                if (!String.IsNullOrEmpty(command.RobotPosition)) parameters += (parameters.Length > 0 ? ", " : "") + "position=" + command.RobotPosition;
                if (!String.IsNullOrEmpty(command.SamplingBox)) parameters += (parameters.Length > 0 ? ", " : "") + "sampling=" + command.SamplingBox;
                if (!String.IsNullOrEmpty(command.FeedingType)) parameters += (parameters.Length > 0 ? ", " : "") + "feeding=" + command.FeedingType;
            }
            ListViewItem item = new ListViewItem(value.Time.ToString("HH:mm:ss"));
            item.SubItems.Add(value.ReceivedTopic);
            item.SubItems.Add(command == null ? "" : command.Name);
            item.SubItems.Add(parameters);
            item.SubItems.Add(value.State);
            item.SubItems.Add(value.ResultPublished ? "gesendet" : "Fehler");
            item.SubItems.Add(value.Message);
            item.Tag = "Empfangen von " + value.ReceivedTopic + Environment.NewLine + value.Payload + Environment.NewLine + Environment.NewLine +
                "Result nach " + value.ResultTopic + " (" + (value.ResultPublished ? "gesendet" : "nicht gesendet") + ")" + Environment.NewLine + value.ResultPayload;
            item.ToolTipText = String.Equals(value.State, "pendingNativeBridge", StringComparison.OrdinalIgnoreCase) ? "Befehl ist angekommen und validiert. Die native DPProcessControl-Uebergabe ist noch nicht aktiv." : value.Message;
            item.BackColor = value.ResultPublished && String.Equals(value.State, "pendingNativeBridge", StringComparison.OrdinalIgnoreCase) ? Color.LightYellow :
                value.ResultPublished && String.Equals(value.State, "invalidParameters", StringComparison.OrdinalIgnoreCase) ? Color.MistyRose :
                value.ResultPublished && String.Equals(value.State, "invalidCommand", StringComparison.OrdinalIgnoreCase) ? Color.MistyRose :
                value.ResultPublished ? Color.White : Color.LightCoral;
            robotCommandList.Items.Insert(0, item);
            while (robotCommandList.Items.Count > 300) robotCommandList.Items.RemoveAt(robotCommandList.Items.Count - 1);
            robotCommandPayloadBox.Text = item.Tag as string;
        }

        private void RobotCommandSelectionChanged(object sender, EventArgs e)
        {
            if (robotCommandList.SelectedItems.Count == 0) return;
            robotCommandPayloadBox.Text = robotCommandList.SelectedItems[0].Tag as string;
        }

        private void RobotCommandListKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.C)
            {
                CopySelectedRobotCommands();
                e.Handled = true;
            }
        }

        private void RobotCommandPayloadKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.C && robotCommandPayloadBox.SelectionLength == 0)
            {
                CopySelectedRobotCommands();
                e.Handled = true;
            }
        }

        private void CopySelectedRobotCommands()
        {
            if (robotCommandList.SelectedItems.Count == 0)
            {
                if (robotCommandPayloadBox.Text.Length > 0) Clipboard.SetText(robotCommandPayloadBox.Text);
                return;
            }
            List<string> values = new List<string>();
            for (int i = 0; i < robotCommandList.SelectedItems.Count; i++)
            {
                string text = robotCommandList.SelectedItems[i].Tag as string;
                if (!String.IsNullOrEmpty(text)) values.Add(text);
            }
            if (values.Count > 0) Clipboard.SetText(String.Join(Environment.NewLine + Environment.NewLine, values.ToArray()));
        }

        private void MainFormClosing(object sender, FormClosingEventArgs e) { if (!allowClose && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); trayIcon.ShowBalloonTip(1500, "Mione Alarmmelder", "Die Überwachung läuft im Hintergrund weiter.", ToolTipIcon.Info); } else { ErrorLogger.ErrorLogged -= ErrorWasLogged; if (alarmToolTip != null) alarmToolTip.Dispose(); if (alarmProgressForm != null) alarmProgressForm.Dispose(); if (errorListMenu != null) errorListMenu.Dispose(); if (robotCommandMenu != null) robotCommandMenu.Dispose(); trayIcon.Visible = false; trayIcon.Dispose(); } }
        private void Resized(object sender, EventArgs e) { if (WindowState == FormWindowState.Minimized) Hide(); }
        private void ShowWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); }
        private void UpdateActionButtons() { saveButton.Visible = tabs.SelectedIndex == 1 || tabs.SelectedIndex == 2 || tabs.SelectedIndex == 3 || tabs.SelectedIndex == 4 || tabs.SelectedIndex == 6; }
        private void DrawTab(object sender, DrawItemEventArgs e)
        {
            Rectangle bounds = tabs.GetTabRect(e.Index);
            Color color = e.Index == 0 && overviewUpdateHighlight ? Color.Gold : (e.Index == tabs.SelectedIndex ? Color.White : SystemColors.Control);
            using (Brush brush = new SolidBrush(color)) e.Graphics.FillRectangle(brush, bounds);
            TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, Font, bounds, Color.Black, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        private void SetTransportStatus(Panel panel, Label label, string name, string text, MonitorState state)
        {
            if (InvokeRequired) { BeginInvoke((MethodInvoker)delegate { SetTransportStatus(panel, label, name, text, state); }); return; }
            panel.BackColor = StateColor(state);
            label.Text = name + ": " + ShortText(text, 38); label.Tag = text;
        }
        private static Color StateColor(MonitorState state) { return state == MonitorState.Ok ? Color.LimeGreen : state == MonitorState.Error ? Color.Red : state == MonitorState.Sending ? Color.DeepSkyBlue : state == MonitorState.Disabled ? Color.Gray : Color.Goldenrod; }
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
