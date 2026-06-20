using System;
using System.Drawing;
using System.Windows.Forms;
using MioneAlarmmelder.Transport;

namespace MioneAlarmmelder.Forms
{
    public sealed class AlarmProgressForm : Form
    {
        private readonly Label title;
        private readonly Label alarmText;
        private readonly ListView events;
        private readonly Timer closeTimer;

        public AlarmProgressForm()
        {
            Text = "Alarmierung"; FormBorderStyle = FormBorderStyle.FixedToolWindow; TopMost = true;
            ShowInTaskbar = false; Size = new Size(520, 250); MaximizeBox = false; MinimizeBox = false;
            title = new Label(); title.Location = new Point(14, 12); title.Size = new Size(480, 24);
            title.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            alarmText = new Label(); alarmText.Location = new Point(14, 40); alarmText.Size = new Size(480, 38);
            events = new ListView(); events.Location = new Point(14, 83); events.Size = new Size(480, 120);
            events.View = View.Details; events.FullRowSelect = true; events.GridLines = true;
            events.Columns.Add("Zeit", 70); events.Columns.Add("Rufnummer", 125);
            events.Columns.Add("Vorgang", 75); events.Columns.Add("Status", 105); events.Columns.Add("Weg", 55);
            Controls.Add(title); Controls.Add(alarmText); Controls.Add(events);
            closeTimer = new Timer(); closeTimer.Interval = 30000; closeTimer.Tick += delegate { Hide(); closeTimer.Stop(); };
        }

        public void UpdateProgress(AlarmProgressEvent value)
        {
            title.Text = "Alarm " + value.AlarmCode;
            alarmText.Text = value.AlarmText;
            string status = value.Status == "starting" ? "wird ausgeführt" : value.Status == "succeeded" ? "erfolgreich" : "Fehler";
            ListViewItem item = new ListViewItem(DateTime.Now.ToString("HH:mm:ss"));
            item.SubItems.Add(value.Number); item.SubItems.Add(value.Action); item.SubItems.Add(status); item.SubItems.Add(value.Source);
            item.ForeColor = value.Status == "failed" ? Color.Firebrick : value.Status == "succeeded" ? Color.DarkGreen : Color.DarkBlue;
            events.Items.Insert(0, item); while (events.Items.Count > 20) events.Items.RemoveAt(events.Items.Count - 1);
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(area.Right - Width - 12, area.Bottom - Height - 12);
            if (!Visible) Show();
            BringToFront(); closeTimer.Stop(); closeTimer.Start();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); closeTimer.Stop(); return; }
            base.OnFormClosing(e);
        }
    }
}
