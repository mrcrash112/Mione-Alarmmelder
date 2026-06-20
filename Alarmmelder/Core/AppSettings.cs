using System;
using System.IO;

namespace MioneAlarmmelder.Core
{
    public sealed class AppSettings
    {
        public string MessageLogPath { get; set; }
        public string AlarmSettingsPath { get; set; }
        public string PriorityPath { get; set; }
        public string TranslationPath { get; set; }
        public string AlarmCatalogPath { get; set; }
        public bool MqttEnabled { get; set; }
        public string MqttHost { get; set; }
        public int MqttPort { get; set; }
        public string MqttUser { get; set; }
        public string MqttPassword { get; set; }
        public string ModemImei { get; set; }
        public bool TcpEnabled { get; set; }
        public string TcpHost { get; set; }
        public int TcpPort { get; set; }
        public bool ShowAlarmProgress { get; set; }
        public int PollSeconds { get; set; }
        public bool StartWithWindows { get; set; }
        public bool UpdateEnabled { get; set; }
        public string UpdateRepository { get; set; }
        public string UpdateAssetName { get; set; }
        public string UpdateChannel { get; set; }
        public int UpdateCheckMinutes { get; set; }
        public int AlarmHistoryLimit { get; set; }
        public int ErrorHistoryLimit { get; set; }
        public bool DpProcessEnabled { get; set; }
        public string DpProcessPath { get; set; }
        public int DpProcessPollSeconds { get; set; }

        public static AppSettings CreateDefault()
        {
            return new AppSettings
            {
                MessageLogPath = @"D:\DairyPln\MessageLog_1.adf",
                AlarmSettingsPath = @"D:\DairyPln\RDM\configuration\preferences\user\alarmssettings.properties",
                PriorityPath = @"D:\DairyPln\RDM\configuration\data\rdm\useralarmpriorities.properties",
                TranslationPath = @"D:\Release\Assets\translations_de.properties",
                AlarmCatalogPath = @"D:\Release\Assets\Mione_AlarmCodes_UK_DE.xlsx",
                MqttHost = "", MqttPort = 1883, ModemImei = "",
                TcpHost = "", TcpPort = 5000, ShowAlarmProgress = true, PollSeconds = 2,
                UpdateEnabled = true, UpdateRepository = "mrcrash112/Mione-Alarmmelder", UpdateAssetName = "MioneAlarmmelder-*.zip", UpdateChannel = "stable", UpdateCheckMinutes = 60,
                AlarmHistoryLimit = 2500, ErrorHistoryLimit = 2500,
                DpProcessEnabled = false, DpProcessPath = @"D:\DairyPln", DpProcessPollSeconds = 30
            };
        }

        public string[] MissingFiles()
        {
            System.Collections.Generic.List<string> missing = new System.Collections.Generic.List<string>();
            if (!File.Exists(MessageLogPath)) missing.Add("Alarmdatei");
            if (!File.Exists(AlarmSettingsPath)) missing.Add("Alarm-Einstellungen");
            if (!File.Exists(PriorityPath)) missing.Add("Alarm-Prioritäten");
            if (!File.Exists(TranslationPath)) missing.Add("Übersetzungen");
            if (!File.Exists(AlarmCatalogPath)) missing.Add("Alarmcode-Arbeitsmappe");
            return missing.ToArray();
        }
    }
}
