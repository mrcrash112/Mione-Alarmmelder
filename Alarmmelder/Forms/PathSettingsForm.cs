using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using MioneAlarmmelder.Core;

namespace MioneAlarmmelder.Forms
{
    public sealed class PathSettingsForm : Form
    {
        private readonly AppSettings settings;
        private TextBox messagePath, alarmPath, priorityPath, translationPath, alarmCatalogPath;

        public PathSettingsForm(AppSettings value)
        {
            settings = value; Text = "DairyPlan-Dateipfade"; StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(720, 450); MinimizeBox = false; MaximizeBox = false; FormBorderStyle = FormBorderStyle.FixedDialog;
            PictureBox logo = new PictureBox(); logo.Location = new Point(14, 12); logo.Size = new Size(76, 76); logo.SizeMode = PictureBoxSizeMode.Zoom; LoadLogo(logo);
            Label title = new Label(); title.Text = "DairyPlan-Dateien auswählen"; title.Font = new Font(Font, FontStyle.Bold); title.Location = new Point(105, 27); title.AutoSize = true;
            Label help = new Label(); help.Text = "Mindestens eine benötigte Datei wurde nicht gefunden. Bitte korrigieren Sie die Pfade."; help.Location = new Point(105, 52); help.Size = new Size(580, 36);
            Controls.Add(logo); Controls.Add(title); Controls.Add(help);
            messagePath = AddPathRow("Alarmdatei", value.MessageLogPath, 105);
            alarmPath = AddPathRow("Alarm-Einstellungen", value.AlarmSettingsPath, 160);
            priorityPath = AddPathRow("Alarm-Prioritäten", value.PriorityPath, 215);
            translationPath = AddPathRow("Übersetzungen", value.TranslationPath, 270);
            alarmCatalogPath = AddPathRow("Alarmcode-Excel", value.AlarmCatalogPath, 325);
            Button save = new Button(); save.Text = "Speichern"; save.Location = new Point(585, 400); save.Size = new Size(110, 30); save.Click += SaveClick;
            Controls.Add(save); AcceptButton = save;
        }

        private TextBox AddPathRow(string label, string value, int top)
        {
            Label l = new Label(); l.Text = label; l.Location = new Point(20, top + 4); l.Size = new Size(140, 23);
            TextBox box = new TextBox(); box.Text = value; box.Location = new Point(165, top); box.Size = new Size(475, 23);
            Button browse = new Button(); browse.Text = "..."; browse.Location = new Point(650, top - 1); browse.Size = new Size(45, 25);
            browse.Click += delegate { using (OpenFileDialog d = new OpenFileDialog()) { d.FileName = box.Text; if (d.ShowDialog(this) == DialogResult.OK) box.Text = d.FileName; } };
            Controls.Add(l); Controls.Add(box); Controls.Add(browse); return box;
        }

        private void SaveClick(object sender, EventArgs e)
        {
            settings.MessageLogPath = messagePath.Text.Trim(); settings.AlarmSettingsPath = alarmPath.Text.Trim();
            settings.PriorityPath = priorityPath.Text.Trim(); settings.TranslationPath = translationPath.Text.Trim(); settings.AlarmCatalogPath = alarmCatalogPath.Text.Trim();
            string[] missing = settings.MissingFiles();
            if (missing.Length > 0) { MessageBox.Show("Nicht gefunden: " + String.Join(", ", missing), "Dateien fehlen", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            SettingsStore.Save(settings); DialogResult = DialogResult.OK; Close();
        }
        private static void LoadLogo(PictureBox box) { box.Image = EmbeddedImages.LoadLogo(); }
    }
}
