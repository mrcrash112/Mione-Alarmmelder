using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace MioneAlarmmelder.Core
{
    public static class SettingsStore
    {
        private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MioneAlarmmelder");
        private static readonly string FileName = Path.Combine(Folder, "settings.xml");
        private static readonly object SyncRoot = new object();

        public static AppSettings Load()
        {
            AppSettings s = AppSettings.CreateDefault();
            if (!File.Exists(FileName)) return s;
            try
            {
                XmlDocument d = new XmlDocument(); d.Load(FileName);
                s.MessageLogPath = Get(d, "MessageLogPath", s.MessageLogPath);
                s.AlarmSettingsPath = Get(d, "AlarmSettingsPath", s.AlarmSettingsPath);
                s.PriorityPath = Get(d, "PriorityPath", s.PriorityPath);
                s.TranslationPath = Get(d, "TranslationPath", s.TranslationPath);
                s.AlarmCatalogPath = Get(d, "AlarmCatalogPath", s.AlarmCatalogPath);
                s.MqttEnabled = GetBool(d, "MqttEnabled", false); s.MqttHost = Get(d, "MqttHost", "");
                s.MqttPort = GetInt(d, "MqttPort", 1883);
                s.MqttUser = Get(d, "MqttUser", ""); s.MqttPassword = Unprotect(Get(d, "MqttPassword", ""));
                s.ModemImei = Get(d, "ModemImei", Get(d, "ModemSerialNumber", ""));
                s.FirebaseApiKey = Get(d, "FirebaseApiKey", "");
                s.FirebaseAuthDomain = Get(d, "FirebaseAuthDomain", "");
                s.FirebaseProjectId = Get(d, "FirebaseProjectId", "");
                s.FirebaseUid = Get(d, "FirebaseUid", "");
                s.FirebaseEmail = Get(d, "FirebaseEmail", "");
                s.FirebaseDisplayName = Get(d, "FirebaseDisplayName", "");
                s.FirebaseProviderId = Get(d, "FirebaseProviderId", "");
                s.FirebasePhoneNumber = Get(d, "FirebasePhoneNumber", "");
                s.FirebaseRefreshToken = Unprotect(Get(d, "FirebaseRefreshToken", ""));
                s.TcpEnabled = GetBool(d, "TcpEnabled", false); s.TcpHost = Get(d, "TcpHost", "");
                s.TcpPort = GetInt(d, "TcpPort", 5000);
                s.ShowAlarmProgress = GetBool(d, "ShowAlarmProgress", true);
                s.PollSeconds = Math.Max(5, GetInt(d, "PollSeconds", 5));
                s.StartWithWindows = GetBool(d, "StartWithWindows", false);
                s.UpdateEnabled = GetBool(d, "UpdateEnabled", true);
                s.UpdateRepository = Get(d, "UpdateRepository", "mrcrash112/Mione-Alarmmelder");
                s.UpdateAssetName = Get(d, "UpdateAssetName", "MioneAlarmmelder-*.zip");
                s.UpdateChannel = Get(d, "UpdateChannel", "stable").ToLowerInvariant() == "beta" ? "beta" : "stable";
                s.UpdateCheckMinutes = Math.Max(5, GetInt(d, "UpdateCheckMinutes", 60));
                s.AlarmHistoryLimit = Limit(GetInt(d, "AlarmHistoryLimit", 2500)); s.ErrorHistoryLimit = Limit(GetInt(d, "ErrorHistoryLimit", 2500));
                s.DpProcessEnabled = GetBool(d, "DpProcessEnabled", false);
                s.DpProcessPath = Get(d, "DpProcessPath", @"D:\DairyPln");
                s.DpProcessPollSeconds = Math.Max(5, GetInt(d, "DpProcessPollSeconds", 30));
                MigrateOldDefaults(s);
            }
            catch { return AppSettings.CreateDefault(); }
            return s;
        }

        public static void Save(AppSettings s)
        {
            lock (SyncRoot)
            {
                if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
                XmlWriterSettings ws = new XmlWriterSettings(); ws.Indent = true; ws.Encoding = Encoding.UTF8;
                using (XmlWriter w = XmlWriter.Create(FileName, ws))
                {
                    w.WriteStartElement("settings");
                    Write(w, "MessageLogPath", s.MessageLogPath); Write(w, "AlarmSettingsPath", s.AlarmSettingsPath);
                    Write(w, "PriorityPath", s.PriorityPath); Write(w, "TranslationPath", s.TranslationPath);
                    Write(w, "AlarmCatalogPath", s.AlarmCatalogPath);
                    Write(w, "MqttEnabled", s.MqttEnabled.ToString()); Write(w, "MqttHost", s.MqttHost);
                    Write(w, "MqttPort", s.MqttPort.ToString());
                    Write(w, "MqttUser", s.MqttUser); Write(w, "MqttPassword", Protect(s.MqttPassword));
                    Write(w, "ModemImei", s.ModemImei);
                    Write(w, "FirebaseApiKey", s.FirebaseApiKey);
                    Write(w, "FirebaseAuthDomain", s.FirebaseAuthDomain);
                    Write(w, "FirebaseProjectId", s.FirebaseProjectId);
                    Write(w, "FirebaseUid", s.FirebaseUid);
                    Write(w, "FirebaseEmail", s.FirebaseEmail);
                    Write(w, "FirebaseDisplayName", s.FirebaseDisplayName);
                    Write(w, "FirebaseProviderId", s.FirebaseProviderId);
                    Write(w, "FirebasePhoneNumber", s.FirebasePhoneNumber);
                    Write(w, "FirebaseRefreshToken", Protect(s.FirebaseRefreshToken));
                    Write(w, "TcpEnabled", s.TcpEnabled.ToString()); Write(w, "TcpHost", s.TcpHost);
                    Write(w, "TcpPort", s.TcpPort.ToString());
                    Write(w, "ShowAlarmProgress", s.ShowAlarmProgress.ToString());
                    Write(w, "PollSeconds", s.PollSeconds.ToString());
                    Write(w, "StartWithWindows", s.StartWithWindows.ToString());
                    Write(w, "UpdateEnabled", s.UpdateEnabled.ToString()); Write(w, "UpdateRepository", s.UpdateRepository);
                    Write(w, "UpdateAssetName", s.UpdateAssetName);
                    Write(w, "UpdateChannel", s.UpdateChannel);
                    Write(w, "UpdateCheckMinutes", s.UpdateCheckMinutes.ToString());
                    Write(w, "AlarmHistoryLimit", s.AlarmHistoryLimit.ToString()); Write(w, "ErrorHistoryLimit", s.ErrorHistoryLimit.ToString());
                    Write(w, "DpProcessEnabled", s.DpProcessEnabled.ToString());
                    Write(w, "DpProcessPath", s.DpProcessPath);
                    Write(w, "DpProcessPollSeconds", s.DpProcessPollSeconds.ToString());
                    w.WriteEndElement();
                }
            }
        }

        private static string Get(XmlDocument d, string name, string fallback) { XmlNode n = d.SelectSingleNode("/settings/" + name); return n == null ? fallback : n.InnerText; }
        private static bool GetBool(XmlDocument d, string n, bool f) { bool v; return Boolean.TryParse(Get(d, n, ""), out v) ? v : f; }
        private static int GetInt(XmlDocument d, string n, int f) { int v; return Int32.TryParse(Get(d, n, ""), out v) ? v : f; }
        private static int Limit(int value) { return Math.Max(100, Math.Min(10000, value)); }
        private static void Write(XmlWriter w, string n, string v) { w.WriteElementString(n, v ?? ""); }
        private static string Protect(string value) { if (String.IsNullOrEmpty(value)) return ""; return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser)); }
        private static string Unprotect(string value) { try { return String.IsNullOrEmpty(value) ? "" : Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value), null, DataProtectionScope.CurrentUser)); } catch { return ""; } }
        private static void MigrateOldDefaults(AppSettings s)
        {
            if (String.Equals(s.MessageLogPath, @"D:\Dairyplan\MessagesLog_1.adf", StringComparison.OrdinalIgnoreCase)) s.MessageLogPath = @"D:\DairyPln\MessageLog_1.adf";
            if (String.Equals(s.AlarmSettingsPath, @"D:\DairyPln\RDM\configuration\preferences\user\alarmsettings", StringComparison.OrdinalIgnoreCase)) s.AlarmSettingsPath = @"D:\DairyPln\RDM\configuration\preferences\user\alarmssettings.properties";
            if (String.Equals(s.PriorityPath, @"D:\DairyPln\RDM\configuration\data\rdm\useralarmpriorities", StringComparison.OrdinalIgnoreCase)) s.PriorityPath = @"D:\DairyPln\RDM\configuration\data\rdm\useralarmpriorities.properties";
            string oldTranslation = Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets"), "translations_de.properties");
            if (String.Equals(s.TranslationPath, oldTranslation, StringComparison.OrdinalIgnoreCase)) s.TranslationPath = @"D:\Release\Assets\translations_de.properties";
            if (String.Equals(s.UpdateAssetName, "MioneAlarmmelder.exe", StringComparison.OrdinalIgnoreCase)) s.UpdateAssetName = "MioneAlarmmelder-*.zip";
        }
    }
}
