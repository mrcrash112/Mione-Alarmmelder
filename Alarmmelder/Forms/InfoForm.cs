using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using MioneAlarmmelder.Core;

namespace MioneAlarmmelder.Forms
{
    public sealed class InfoForm : Form
    {
        public InfoForm()
        {
            Text = "Info - Mione Alarmmelder"; StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(590, 440); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            PictureBox logo = new PictureBox(); logo.Image = EmbeddedImages.LoadLogo(); logo.SizeMode = PictureBoxSizeMode.Zoom; logo.Location = new Point(20, 20); logo.Size = new Size(165, 165);
            Label title = new Label(); title.Text = "Elektrotechnik Jozefowicz"; title.Font = new Font("Segoe UI", 15F, FontStyle.Bold); title.Location = new Point(210, 24); title.AutoSize = true;
            Label owner = AddLabel("Thorsten Jozefowicz\r\nBergen 15\r\n46487 Wesel\r\nDeutschland", 210, 62, 340, 78);
            Label phoneTitle = AddLabel("Telefon:", 210, 150, 80, 22); LinkLabel phone = AddLink("01575 1623434", "tel:+4915751623434", 300, 150, 220);
            Label mailTitle = AddLabel("E-Mail:", 210, 178, 80, 22); LinkLabel mail = AddLink("kontakt@elektrotechnik-jozefowicz.de", "mailto:kontakt@elektrotechnik-jozefowicz.de", 300, 178, 270);
            Label webTitle = AddLabel("Website:", 210, 206, 80, 22); LinkLabel web = AddLink("elektrotechnik-jozefowicz.de", "https://elektrotechnik-jozefowicz.de", 300, 206, 260);
            Label details = AddLabel("Tätigkeitsbereich\r\nElektrotechnische Anlagen, Wärme- und Klimaanlagen sowie Melktechnik.\r\n\r\nUSt-IdNr.: DE294221984\r\n\r\nMione Alarmmelder Version " + GitHubUpdateService.CurrentVersionLabel, 20, 245, 550, 135);
            Button close = new Button(); close.Text = "Schließen"; close.Location = new Point(455, 392); close.Size = new Size(110, 30); close.DialogResult = DialogResult.OK;
            Controls.Add(logo); Controls.Add(title); Controls.Add(owner); Controls.Add(phoneTitle); Controls.Add(phone); Controls.Add(mailTitle); Controls.Add(mail); Controls.Add(webTitle); Controls.Add(web); Controls.Add(details); Controls.Add(close);
            AcceptButton = close; CancelButton = close;
        }

        private Label AddLabel(string text, int left, int top, int width, int height)
        {
            Label label = new Label(); label.Text = text; label.Location = new Point(left, top); label.Size = new Size(width, height); return label;
        }
        private LinkLabel AddLink(string text, string target, int left, int top, int width)
        {
            LinkLabel link = new LinkLabel(); link.Text = text; link.Location = new Point(left, top); link.Size = new Size(width, 22);
            link.LinkClicked += delegate { try { Process.Start(target); } catch (Exception ex) { ErrorLogger.Log("Info-Link", ex); } }; return link;
        }
    }
}
