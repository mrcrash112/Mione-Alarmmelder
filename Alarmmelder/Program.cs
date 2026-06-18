using System;
using System.Threading;
using System.Windows.Forms;

namespace MioneAlarmmelder
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "MioneAlarmmelder.SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("Der Mione Alarmmelder läuft bereits.", "Mione Alarmmelder",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new AlarmApplicationContext());
            }
        }
    }
}
