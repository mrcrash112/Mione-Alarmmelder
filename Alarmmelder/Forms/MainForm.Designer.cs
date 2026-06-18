using System.Drawing;
using System.Windows.Forms;

namespace MioneAlarmmelder.Forms
{
    partial class MainForm
    {
        private PictureBox logoPicture; private Label titleLabel; private Label statusLabel; private Panel ledPanel;
        private Label mqttStatusLabel, tcpStatusLabel; private Panel mqttLedPanel, tcpLedPanel;
        private TabControl tabs; private ListView alarmList; private ListView phoneList;
        private TextBox messagePathBox, alarmSettingsPathBox, priorityPathBox, translationPathBox;
        private CheckBox mqttEnabledBox, tcpEnabledBox, startupBox;
        private TextBox mqttHostBox, mqttPortBox, mqttTopicBox, mqttUserBox, mqttPasswordBox;
        private TextBox tcpHostBox, tcpPortBox, customerBox, pollBox, heartbeatBox;
        private Button saveButton, testButton, pathDialogButton;
        private CheckBox updateEnabledBox; private TextBox updateRepositoryBox, updateAssetBox;
        private Label updateStatusLabel, currentVersionLabel; private Button updateCheckButton;

        private void InitializeComponent()
        {
            SuspendLayout(); Text = "Mione Alarmmelder"; StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(850, 600); MinimumSize = new Size(780, 560);
            logoPicture = new PictureBox(); logoPicture.Location = new Point(12, 8); logoPicture.Size = new Size(74, 74); logoPicture.SizeMode = PictureBoxSizeMode.Zoom;
            titleLabel = new Label(); titleLabel.Text = "Mione Alarmmelder"; titleLabel.Font = new Font("Segoe UI", 16F, FontStyle.Bold); titleLabel.Location = new Point(100, 18); titleLabel.AutoSize = true;
            statusLabel = new Label(); statusLabel.Text = "Wird gestartet ..."; statusLabel.Location = new Point(125, 56); statusLabel.AutoSize = true;
            ledPanel = new Panel(); ledPanel.Location = new Point(104, 54); ledPanel.Size = new Size(14, 14); ledPanel.BackColor = Color.Goldenrod;
            mqttLedPanel = new Panel(); mqttLedPanel.Location = new Point(515, 25); mqttLedPanel.Size = new Size(14, 14); mqttLedPanel.BackColor = Color.Gray;
            mqttStatusLabel = new Label(); mqttStatusLabel.Text = "MQTT: deaktiviert"; mqttStatusLabel.Location = new Point(536, 24); mqttStatusLabel.Size = new Size(285, 20);
            tcpLedPanel = new Panel(); tcpLedPanel.Location = new Point(515, 54); tcpLedPanel.Size = new Size(14, 14); tcpLedPanel.BackColor = Color.Gray;
            tcpStatusLabel = new Label(); tcpStatusLabel.Text = "TCP: deaktiviert"; tcpStatusLabel.Location = new Point(536, 53); tcpStatusLabel.Size = new Size(285, 20);
            tabs = new TabControl(); tabs.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right; tabs.Location = new Point(12, 90); tabs.Size = new Size(826, 452);
            TabPage overview = new TabPage("Übersicht"); TabPage paths = new TabPage("Dateipfade"); TabPage transport = new TabPage("Versand"); TabPage updates = new TabPage("Updates");
            alarmList = new ListView(); alarmList.Dock = DockStyle.Fill; alarmList.View = View.Details; alarmList.FullRowSelect = true; alarmList.GridLines = true;
            alarmList.Columns.Add("Zeit", 120); alarmList.Columns.Add("Code", 65); alarmList.Columns.Add("Ort", 115); alarmList.Columns.Add("Priorität", 90); alarmList.Columns.Add("Alarmtext", 380);
            overview.Controls.Add(alarmList);
            messagePathBox = AddPath(paths, "Alarmdatei", 24); alarmSettingsPathBox = AddPath(paths, "Alarm-Einstellungen", 78);
            priorityPathBox = AddPath(paths, "Alarm-Prioritäten", 132); translationPathBox = AddPath(paths, "Übersetzungen", 186);
            pathDialogButton = new Button(); pathDialogButton.Text = "Pfade prüfen/auswählen"; pathDialogButton.Location = new Point(640, 235); pathDialogButton.Size = new Size(155, 30); paths.Controls.Add(pathDialogButton);
            GroupBox mqtt = new GroupBox(); mqtt.Text = "MQTT 3.1.1"; mqtt.Location = new Point(15, 12); mqtt.Size = new Size(385, 385);
            mqttEnabledBox = AddCheck(mqtt, "MQTT-Versand aktiv", 20); mqttHostBox = AddField(mqtt, "Server", 58); mqttPortBox = AddField(mqtt, "Port", 98);
            mqttTopicBox = AddField(mqtt, "Topic", 138); mqttUserBox = AddField(mqtt, "Benutzername", 178); mqttPasswordBox = AddField(mqtt, "Passwort", 218); mqttPasswordBox.UseSystemPasswordChar = true;
            customerBox = AddField(mqtt, "Kundenzuordnung", 258); pollBox = AddField(mqtt, "Prüfintervall (s)", 298);
            startupBox = AddCheck(mqtt, "Mit Windows starten", 342);
            GroupBox tcp = new GroupBox(); tcp.Text = "TCP-Socket (JSON + Zeilenumbruch)"; tcp.Location = new Point(415, 12); tcp.Size = new Size(385, 220);
            tcpEnabledBox = AddCheck(tcp, "TCP-Versand aktiv", 20); tcpHostBox = AddField(tcp, "Server / IP", 58); tcpPortBox = AddField(tcp, "Port", 98);
            heartbeatBox = AddField(tcp, "Heartbeat (s)", 138);
            testButton = new Button(); testButton.Text = "Verbindung testen"; testButton.Location = new Point(225, 175); testButton.Size = new Size(135, 30); tcp.Controls.Add(testButton);
            phoneList = new ListView(); phoneList.View = View.Details; phoneList.FullRowSelect = true; phoneList.Location = new Point(415, 245); phoneList.Size = new Size(385, 152);
            phoneList.Columns.Add("Rufnummer", 245); phoneList.Columns.Add("Aktiv", 100);
            transport.Controls.Add(mqtt); transport.Controls.Add(tcp); transport.Controls.Add(phoneList);
            updateEnabledBox = AddCheck(updates, "Beim Start automatisch nach Updates suchen", 25);
            updateRepositoryBox = AddUpdateField(updates, "GitHub-Repository", 75); updateAssetBox = AddUpdateField(updates, "Release-Datei", 120);
            currentVersionLabel = new Label(); currentVersionLabel.Location = new Point(18, 178); currentVersionLabel.Size = new Size(740, 22);
            updateStatusLabel = new Label(); updateStatusLabel.Location = new Point(18, 210); updateStatusLabel.Size = new Size(740, 70); updateStatusLabel.Text = "Noch nicht geprüft.";
            updateCheckButton = new Button(); updateCheckButton.Text = "Jetzt nach Updates suchen"; updateCheckButton.Location = new Point(570, 300); updateCheckButton.Size = new Size(220, 32);
            Label updateHelp = new Label(); updateHelp.Location = new Point(18, 350); updateHelp.Size = new Size(760, 50); updateHelp.Text = "Im neuesten öffentlichen GitHub-Release muss die angegebene EXE als Asset hinterlegt sein. Der Release-Tag muss z. B. v1.1.0 lauten.";
            updates.Controls.Add(currentVersionLabel); updates.Controls.Add(updateStatusLabel); updates.Controls.Add(updateCheckButton); updates.Controls.Add(updateHelp);
            tabs.TabPages.Add(overview); tabs.TabPages.Add(paths); tabs.TabPages.Add(transport); tabs.TabPages.Add(updates);
            saveButton = new Button(); saveButton.Text = "Einstellungen speichern"; saveButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right; saveButton.Location = new Point(650, 555); saveButton.Size = new Size(188, 32);
            Controls.Add(logoPicture); Controls.Add(titleLabel); Controls.Add(statusLabel); Controls.Add(ledPanel);
            Controls.Add(mqttLedPanel); Controls.Add(mqttStatusLabel); Controls.Add(tcpLedPanel); Controls.Add(tcpStatusLabel);
            Controls.Add(tabs); Controls.Add(saveButton);
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
