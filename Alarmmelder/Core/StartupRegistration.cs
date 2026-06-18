using Microsoft.Win32;
using System;
using System.Windows.Forms;

namespace MioneAlarmmelder.Core
{
    public static class StartupRegistration
    {
        public static void Apply(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (key == null) return;
                if (enabled) key.SetValue("MioneAlarmmelder", "\"" + Application.ExecutablePath + "\"");
                else key.DeleteValue("MioneAlarmmelder", false);
            }
        }
    }
}
