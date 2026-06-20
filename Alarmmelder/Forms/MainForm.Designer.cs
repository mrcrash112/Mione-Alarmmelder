using System.Drawing;
using System.Windows.Forms;

namespace MioneAlarmmelder.Forms
{
    partial class MainForm
    {
        private PictureBox logoPicture; private Label titleLabel, versionLabel; private Label statusLabel; private Panel ledPanel;
        private Label mqttStatusLabel, tcpStatusLabel, modemStatusLabel, firmwareStatusLabel; private Panel mqttLedPanel, tcpLedPanel, modemLedPanel, firmwareLedPanel;
        private TabControl tabs; private ListView alarmList; private ListView phoneList;
        private TextBox messagePathBox, alarmSettingsPathBox, priorityPathBox, translationPathBox, alarmCatalogPathBox;
        private CheckBox mqttEnabledBox, tcpEnabledBox, startupBox, alarmProgressBox;
        private TextBox mqttHostBox, mqttPortBox, mqttUserBox, mqttPasswordBox, modemImeiBox;
        private TextBox tcpHostBox, tcpPortBox, pollBox;
        private Button saveButton, testButton, testAlarmButton, urgentTestAlarmButton, pathDialogButton, infoButton;
        private CheckBox updateEnabledBox; private TextBox updateRepositoryBox, updateAssetBox, updateIntervalBox; private ComboBox updateChannelBox;
        private Label updateStatusLabel; private Button updateCheckButton;
        private CheckBox dpProcessEnabledBox; private TextBox dpProcessPathBox, dpProcessPollBox; private Button dpProcessCheckButton; private ListView dpProcessFileList; private Label dpProcessTopicLabel;
        private ListView robotCommandList; private TextBox robotCommandPayloadBox; private Button robotCommandClearButton;
        private TabPage overviewPage;
        private ComboBox alarmViewFilter, alarmPriorityFilter; private Button acknowledgeSelectedButton, acknowledgeAllButton; private NumericUpDown alarmLimitBox;
        private ListView errorList; private Button errorRefreshButton, errorClearButton, errorAcknowledgeSelectedButton, errorAcknowledgeAllButton; private Label errorPathLabel; private ComboBox errorViewFilter; private NumericUpDown errorLimitBox;

        private void InitializeComponent()
        {
            SuspendLayout(); Text = "Mione Alarmmelder"; StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(850, 600); MinimumSize = new Size(780, 560);
            logoPicture = new PictureBox(); logoPicture.Location = new Point(12, 8); logoPicture.Size = new Size(74, 74); logoPicture.SizeMode = PictureBoxSizeMode.Zoom;
            titleLabel = new Label(); titleLabel.Text = "Mione Alarmmelder"; titleLabel.Font = new Font("Segoe UI", 16F, FontStyle.Bold); titleLabel.Location = new Point(100, 18); titleLabel.AutoSize = true;
            versionLabel = new Label(); versionLabel.Text = "Version"; versionLabel.Location = new Point(325, 28); versionLabel.Size = new Size(170, 20); versionLabel.ForeColor = Color.DimGray;
            statusLabel = new Label(); statusLabel.Text = "Wird gestartet ..."; statusLabel.Location = new Point(125, 56); statusLabel.AutoSize = true;
            ledPanel = new Panel(); ledPanel.Location = new Point(104, 54); ledPanel.Size = new Size(14, 14); ledPanel.BackColor = Color.Goldenrod;
            mqttLedPanel = new Panel(); mqttLedPanel.Location = new Point(515, 25); mqttLedPanel.Size = new Size(14, 14); mqttLedPanel.BackColor = Color.Gray;
            mqttStatusLabel = new Label(); mqttStatusLabel.Text = "MQTT: deaktiviert"; mqttStatusLabel.Location = new Point(536, 24); mqttStatusLabel.Size = new Size(285, 20);
            tcpLedPanel = new Panel(); tcpLedPanel.Location = new Point(515, 54); tcpLedPanel.Size = new Size(14, 14); tcpLedPanel.BackColor = Color.Gray;
            tcpStatusLabel = new Label(); tcpStatusLabel.Text = "TCP: deaktiviert"; tcpStatusLabel.Location = new Point(536, 53); tcpStatusLabel.Size = new Size(285, 20);
            modemLedPanel = new Panel(); modemLedPanel.Location = new Point(515, 81); modemLedPanel.Size = new Size(14, 14); modemLedPanel.BackColor = Color.Gray;
            modemStatusLabel = new Label(); modemStatusLabel.Text = "Modem: deaktiviert"; modemStatusLabel.Location = new Point(536, 80); modemStatusLabel.Size = new Size(285, 20);
            firmwareLedPanel = new Panel(); firmwareLedPanel.Location = new Point(104, 84); firmwareLedPanel.Size = new Size(14, 14); firmwareLedPanel.BackColor = Color.Gray;
            firmwareStatusLabel = new Label(); firmwareStatusLabel.Text = "Modem-Firmware: warte auf Status"; firmwareStatusLabel.Location = new Point(125, 82); firmwareStatusLabel.Size = new Size(370, 20);
            tabs = new TabControl(); tabs.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right; tabs.Location = new Point(12, 108); tabs.Size = new Size(826, 434);
            overviewPage = new TabPage("Übersicht"); TabPage paths = new TabPage("Dateipfade"); TabPage transport = new TabPage("Versand"); TabPage dpProcess = new TabPage("Melkroboter"); TabPage robotCommands = new TabPage("Funktionslog"); TabPage updates = new TabPage("Updates"); TabPage errors = new TabPage("Fehlerprotokoll");
            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            alarmList = new ListView(); alarmList.Dock = DockStyle.Fill; alarmList.View = View.Details; alarmList.FullRowSelect = true; alarmList.GridLines = true;
            alarmList.Columns.Add("Zeit", 120); alarmList.Columns.Add("Code", 60); alarmList.Columns.Add("Ort", 105); alarmList.Columns.Add("Kuh", 85); alarmList.Columns.Add("Priorität", 85); alarmList.Columns.Add("Alarmtext", 320);
            Panel alarmTools = new Panel(); alarmTools.Dock = DockStyle.Top; alarmTools.Height = 78;
            Label viewLabel = new Label(); viewLabel.Text = "Anzeige:"; viewLabel.Location = new Point(8, 13); viewLabel.AutoSize = true;
            alarmViewFilter = new ComboBox(); alarmViewFilter.DropDownStyle = ComboBoxStyle.DropDownList; alarmViewFilter.Location = new Point(65, 9); alarmViewFilter.Size = new Size(140, 23);
            Label priorityLabel = new Label(); priorityLabel.Text = "Priorität:"; priorityLabel.Location = new Point(218, 13); priorityLabel.AutoSize = true;
            alarmPriorityFilter = new ComboBox(); alarmPriorityFilter.DropDownStyle = ComboBoxStyle.DropDownList; alarmPriorityFilter.Location = new Point(275, 9); alarmPriorityFilter.Size = new Size(130, 23);
            Label alarmLimitLabel = new Label(); alarmLimitLabel.Text = "Max.:"; alarmLimitLabel.Location = new Point(415, 13); alarmLimitLabel.AutoSize = true;
            alarmLimitBox = new NumericUpDown(); alarmLimitBox.Minimum = 100; alarmLimitBox.Maximum = 10000; alarmLimitBox.Increment = 100; alarmLimitBox.Location = new Point(450, 9); alarmLimitBox.Size = new Size(65, 23);
            acknowledgeSelectedButton = new Button(); acknowledgeSelectedButton.Text = "Auswahl bestätigen"; acknowledgeSelectedButton.Location = new Point(525, 7); acknowledgeSelectedButton.Size = new Size(135, 28);
            acknowledgeAllButton = new Button(); acknowledgeAllButton.Text = "Alle bestätigen"; acknowledgeAllButton.Location = new Point(668, 7); acknowledgeAllButton.Size = new Size(125, 28);
            urgentTestAlarmButton = new Button(); urgentTestAlarmButton.Text = "Testalarm (urgent) senden"; urgentTestAlarmButton.Location = new Point(8, 42); urgentTestAlarmButton.Size = new Size(190, 28); urgentTestAlarmButton.BackColor = Color.MistyRose; urgentTestAlarmButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            alarmTools.Controls.Add(viewLabel); alarmTools.Controls.Add(alarmViewFilter); alarmTools.Controls.Add(priorityLabel); alarmTools.Controls.Add(alarmPriorityFilter); alarmTools.Controls.Add(alarmLimitLabel); alarmTools.Controls.Add(alarmLimitBox); alarmTools.Controls.Add(acknowledgeSelectedButton); alarmTools.Controls.Add(acknowledgeAllButton); alarmTools.Controls.Add(urgentTestAlarmButton);
            overviewPage.Controls.Add(alarmList); overviewPage.Controls.Add(alarmTools);
            messagePathBox = AddPath(paths, "Alarmdatei", 24); alarmSettingsPathBox = AddPath(paths, "Alarm-Einstellungen", 78);
            priorityPathBox = AddPath(paths, "Alarm-Prioritäten", 132); translationPathBox = AddPath(paths, "Übersetzungen", 186);
            alarmCatalogPathBox = AddPath(paths, "Alarmcode-Excel", 240);
            pathDialogButton = new Button(); pathDialogButton.Text = "Pfade prüfen/auswählen"; pathDialogButton.Location = new Point(640, 290); pathDialogButton.Size = new Size(155, 30); paths.Controls.Add(pathDialogButton);
            GroupBox mqtt = new GroupBox(); mqtt.Text = "MQTT 3.1.1"; mqtt.Location = new Point(15, 12); mqtt.Size = new Size(385, 385);
            mqttEnabledBox = AddCheck(mqtt, "MQTT-Versand aktiv", 20); mqttHostBox = AddField(mqtt, "Server", 58); mqttPortBox = AddField(mqtt, "Port", 98);
            mqttUserBox = AddField(mqtt, "Benutzer / Topic", 138); mqttPasswordBox = AddField(mqtt, "Passwort", 178); mqttPasswordBox.UseSystemPasswordChar = true;
            modemImeiBox = AddField(mqtt, "Modem-IMEI", 218);
            pollBox = AddField(mqtt, "Prüfintervall (s)", 258);
            startupBox = AddCheck(mqtt, "Mit Windows starten", 302);
            alarmProgressBox = AddCheck(mqtt, "Alarmfortschritt 30 Sekunden anzeigen", 335);
            GroupBox tcp = new GroupBox(); tcp.Text = "TCP-Socket (JSON + Zeilenumbruch)"; tcp.Location = new Point(415, 12); tcp.Size = new Size(385, 220);
            tcpEnabledBox = AddCheck(tcp, "TCP-Versand aktiv", 20); tcpHostBox = AddField(tcp, "Server / IP", 58); tcpPortBox = AddField(tcp, "Port", 98);
            testAlarmButton = new Button(); testAlarmButton.Text = "Testfehler senden"; testAlarmButton.Location = new Point(650, 400); testAlarmButton.Size = new Size(150, 28); testAlarmButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            testButton = new Button(); testButton.Text = "Verbindung testen"; testButton.Location = new Point(225, 175); testButton.Size = new Size(135, 30); tcp.Controls.Add(testButton);
            phoneList = new ListView(); phoneList.View = View.Details; phoneList.FullRowSelect = true; phoneList.Location = new Point(415, 245); phoneList.Size = new Size(385, 152);
            phoneList.Columns.Add("Rufnummer", 115); phoneList.Columns.Add("Aktiv", 48); phoneList.Columns.Add("Alarmierung", 95);
            phoneList.Columns.Add("Techn. von", 60); phoneList.Columns.Add("bis", 45);
            transport.Controls.Add(mqtt); transport.Controls.Add(tcp); transport.Controls.Add(phoneList); transport.Controls.Add(testAlarmButton);
            dpProcessEnabledBox = AddCheck(dpProcess, "DPProcessControl/Melkroboter per MQTT übertragen", 25);
            dpProcessPathBox = AddUpdateField(dpProcess, "DairyPln-Pfad", 75);
            dpProcessPollBox = AddUpdateField(dpProcess, "Prüfintervall (s)", 120);
            dpProcessTopicLabel = new Label(); dpProcessTopicLabel.Location = new Point(18, 165); dpProcessTopicLabel.Size = new Size(760, 22);
            dpProcessFileList = new ListView(); dpProcessFileList.View = View.Details; dpProcessFileList.FullRowSelect = true; dpProcessFileList.GridLines = true;
            dpProcessFileList.Location = new Point(18, 200); dpProcessFileList.Size = new Size(760, 145); dpProcessFileList.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            dpProcessFileList.Columns.Add("Status", 70); dpProcessFileList.Columns.Add("Datei", 230); dpProcessFileList.Columns.Add("Pfad", 430);
            dpProcessCheckButton = new Button(); dpProcessCheckButton.Text = "Pfade prüfen"; dpProcessCheckButton.Location = new Point(650, 365); dpProcessCheckButton.Size = new Size(128, 30); dpProcessCheckButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            dpProcess.Controls.Add(dpProcessEnabledBox); dpProcess.Controls.Add(dpProcessPathBox); dpProcess.Controls.Add(dpProcessPollBox); dpProcess.Controls.Add(dpProcessTopicLabel); dpProcess.Controls.Add(dpProcessFileList); dpProcess.Controls.Add(dpProcessCheckButton);
            robotCommandList = new ListView(); robotCommandList.View = View.Details; robotCommandList.FullRowSelect = true; robotCommandList.GridLines = true;
            robotCommandList.Location = new Point(12, 12); robotCommandList.Size = new Size(790, 245); robotCommandList.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            robotCommandList.Columns.Add("Zeit", 82); robotCommandList.Columns.Add("Empfang", 175); robotCommandList.Columns.Add("Funktion", 145); robotCommandList.Columns.Add("Parameter", 135); robotCommandList.Columns.Add("Status", 130); robotCommandList.Columns.Add("Result", 65); robotCommandList.Columns.Add("Meldung", 300);
            Label robotCommandPayloadLabel = new Label(); robotCommandPayloadLabel.Text = "Payload / Result:"; robotCommandPayloadLabel.Location = new Point(12, 268); robotCommandPayloadLabel.Size = new Size(120, 20); robotCommandPayloadLabel.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            robotCommandPayloadBox = new TextBox(); robotCommandPayloadBox.Multiline = true; robotCommandPayloadBox.ScrollBars = ScrollBars.Both; robotCommandPayloadBox.ReadOnly = true; robotCommandPayloadBox.WordWrap = false;
            robotCommandPayloadBox.Location = new Point(12, 292); robotCommandPayloadBox.Size = new Size(650, 102); robotCommandPayloadBox.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            robotCommandClearButton = new Button(); robotCommandClearButton.Text = "Liste löschen"; robotCommandClearButton.Location = new Point(678, 364); robotCommandClearButton.Size = new Size(124, 30); robotCommandClearButton.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            robotCommands.Controls.Add(robotCommandList); robotCommands.Controls.Add(robotCommandPayloadLabel); robotCommands.Controls.Add(robotCommandPayloadBox); robotCommands.Controls.Add(robotCommandClearButton);
            updateEnabledBox = AddCheck(updates, "Beim Start automatisch nach Updates suchen", 25);
            updateRepositoryBox = AddUpdateField(updates, "GitHub-Repository", 75); updateAssetBox = AddUpdateField(updates, "Release-Datei", 120);
            updateIntervalBox = AddUpdateField(updates, "Prüfintervall (Min.)", 165);
            Label updateChannelLabel = new Label(); updateChannelLabel.Text = "Update-Kanal"; updateChannelLabel.Location = new Point(18, 214); updateChannelLabel.Size = new Size(150, 23);
            updateChannelBox = new ComboBox(); updateChannelBox.DropDownStyle = ComboBoxStyle.DropDownList; updateChannelBox.Location = new Point(175, 210); updateChannelBox.Size = new Size(180, 23);
            updateChannelBox.Items.Add("Stable"); updateChannelBox.Items.Add("Beta");
            updateStatusLabel = new Label(); updateStatusLabel.Location = new Point(18, 250); updateStatusLabel.Size = new Size(740, 85); updateStatusLabel.Text = "Noch nicht geprüft.";
            updateCheckButton = new Button(); updateCheckButton.Text = "Jetzt nach Updates suchen"; updateCheckButton.Location = new Point(570, 340); updateCheckButton.Size = new Size(220, 32);
            Label updateHelp = new Label(); updateHelp.Location = new Point(18, 350); updateHelp.Size = new Size(760, 50); updateHelp.Text = "Im neuesten öffentlichen GitHub-Release muss die angegebene EXE als Asset hinterlegt sein. Der Release-Tag muss z. B. v1.1.0 lauten.";
            updateHelp.Location = new Point(18, 385); updateHelp.Text = "Stable prüft das neueste öffentliche Release. Beta prüft den GitHub-Release-Tag beta und zeigt Testversionen mit _Beta an.";
            updates.Controls.Add(updateChannelLabel); updates.Controls.Add(updateChannelBox); updates.Controls.Add(updateStatusLabel); updates.Controls.Add(updateCheckButton); updates.Controls.Add(updateHelp);
            Panel errorTools = new Panel(); errorTools.Dock = DockStyle.Top; errorTools.Height = 42;
            Label errorViewLabel = new Label(); errorViewLabel.Text = "Anzeige:"; errorViewLabel.Location = new Point(8, 13); errorViewLabel.AutoSize = true;
            errorViewFilter = new ComboBox(); errorViewFilter.DropDownStyle = ComboBoxStyle.DropDownList; errorViewFilter.Location = new Point(65, 9); errorViewFilter.Size = new Size(140, 23);
            Label errorLimitLabel = new Label(); errorLimitLabel.Text = "Max.:"; errorLimitLabel.Location = new Point(220, 13); errorLimitLabel.AutoSize = true;
            errorLimitBox = new NumericUpDown(); errorLimitBox.Minimum = 100; errorLimitBox.Maximum = 10000; errorLimitBox.Increment = 100; errorLimitBox.Location = new Point(255, 9); errorLimitBox.Size = new Size(70, 23);
            errorAcknowledgeSelectedButton = new Button(); errorAcknowledgeSelectedButton.Text = "Auswahl bestätigen"; errorAcknowledgeSelectedButton.Location = new Point(480, 7); errorAcknowledgeSelectedButton.Size = new Size(145, 28);
            errorAcknowledgeAllButton = new Button(); errorAcknowledgeAllButton.Text = "Alle bestätigen"; errorAcknowledgeAllButton.Location = new Point(635, 7); errorAcknowledgeAllButton.Size = new Size(135, 28);
            errorTools.Controls.Add(errorViewLabel); errorTools.Controls.Add(errorViewFilter); errorTools.Controls.Add(errorLimitLabel); errorTools.Controls.Add(errorLimitBox); errorTools.Controls.Add(errorAcknowledgeSelectedButton); errorTools.Controls.Add(errorAcknowledgeAllButton);
            errorList = new ListView(); errorList.View = View.Details; errorList.FullRowSelect = true; errorList.GridLines = true;
            errorList.Location = new Point(12, 52); errorList.Size = new Size(790, 290); errorList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            errorList.Columns.Add("Zeit", 145); errorList.Columns.Add("Quelle", 130); errorList.Columns.Add("Fehlermeldung", 490);
            errorPathLabel = new Label(); errorPathLabel.Location = new Point(12, 352); errorPathLabel.Size = new Size(540, 42); errorPathLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            errorRefreshButton = new Button(); errorRefreshButton.Text = "Aktualisieren"; errorRefreshButton.Location = new Point(565, 365); errorRefreshButton.Size = new Size(110, 30); errorRefreshButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            errorClearButton = new Button(); errorClearButton.Text = "Protokoll löschen"; errorClearButton.Location = new Point(684, 365); errorClearButton.Size = new Size(118, 30); errorClearButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            errors.Controls.Add(errorList); errors.Controls.Add(errorTools); errors.Controls.Add(errorPathLabel); errors.Controls.Add(errorRefreshButton); errors.Controls.Add(errorClearButton);
            tabs.TabPages.Add(overviewPage); tabs.TabPages.Add(paths); tabs.TabPages.Add(transport); tabs.TabPages.Add(dpProcess); tabs.TabPages.Add(robotCommands); tabs.TabPages.Add(updates); tabs.TabPages.Add(errors);
            saveButton = new Button(); saveButton.Text = "Einstellungen speichern"; saveButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right; saveButton.Location = new Point(650, 555); saveButton.Size = new Size(188, 32); saveButton.Visible = false;
            infoButton = new Button(); infoButton.Text = "Info"; infoButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left; infoButton.Location = new Point(12, 555); infoButton.Size = new Size(100, 32);
            Controls.Add(logoPicture); Controls.Add(titleLabel); Controls.Add(versionLabel); Controls.Add(statusLabel); Controls.Add(ledPanel);
            Controls.Add(mqttLedPanel); Controls.Add(mqttStatusLabel); Controls.Add(tcpLedPanel); Controls.Add(tcpStatusLabel); Controls.Add(modemLedPanel); Controls.Add(modemStatusLabel);
            Controls.Add(firmwareLedPanel); Controls.Add(firmwareStatusLabel);
            Controls.Add(tabs); Controls.Add(saveButton); Controls.Add(infoButton);
            ResumeLayout(false); PerformLayout();
        }

        private TextBox AddPath(Control parent, string label, int top)
        {
            Label l = new Label(); l.Text = label; l.Location = new Point(18, top + 4); l.Size = new Size(145, 23);
            TextBox b = new TextBox(); b.Location = new Point(168, top); b.Size = new Size(625, 23); b.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            parent.Controls.Add(l); parent.Controls.Add(b); return b;
        }
        private TextBox AddField(Control parent, string label, int top)
        {
            Label l = new Label(); l.Text = label; l.Location = new Point(14, top + 4); l.Size = new Size(115, 23);
            TextBox b = new TextBox(); b.Location = new Point(135, top); b.Size = new Size(225, 23); parent.Controls.Add(l); parent.Controls.Add(b); return b;
        }
        private CheckBox AddCheck(Control parent, string text, int top) { CheckBox c = new CheckBox(); c.Text = text; c.Location = new Point(14, top); c.AutoSize = true; parent.Controls.Add(c); return c; }
        private TextBox AddUpdateField(Control parent, string label, int top)
        {
            Label l = new Label(); l.Text = label; l.Location = new Point(18, top + 4); l.Size = new Size(150, 23);
            TextBox b = new TextBox(); b.Location = new Point(175, top); b.Size = new Size(615, 23); parent.Controls.Add(l); parent.Controls.Add(b); return b;
        }
    }
}
