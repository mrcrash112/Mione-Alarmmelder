using System;
using System.Drawing;
using System.Windows.Forms;
using MioneAlarmmelder.Core;

namespace MioneAlarmmelder.Forms
{
    public sealed class SplashForm : Form
    {
        private Timer timer;

        public SplashForm()
        {
            FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(900, 506); ShowInTaskbar = false; BackColor = Color.FromArgb(0, 42, 72);
            PictureBox image = new PictureBox(); image.Dock = DockStyle.Fill; image.SizeMode = PictureBoxSizeMode.Zoom;
            image.Image = EmbeddedImages.LoadBanner();
            Controls.Add(image);
            timer = new Timer(); timer.Interval = 1800; timer.Tick += delegate { timer.Stop(); Close(); }; timer.Start();
        }
    }
}
