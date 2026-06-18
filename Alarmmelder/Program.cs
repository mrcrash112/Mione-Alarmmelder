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
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) { Core.ErrorLogger.Log("Programm", e.Exception); };
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e) { Core.ErrorLogger.Log("Programm", e.ExceptionObject as Exception == null ? "Unbehandelter Fehler" : e.ExceptionObject.ToString()); };
                Application.Run(new AlarmApplicationContext());
            }
        }
    }
}
