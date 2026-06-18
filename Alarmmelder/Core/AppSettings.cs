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
        public bool MqttEnabled { get; set; }
        public string MqttHost { get; set; }
        public int MqttPort { get; set; }
        public string MqttTopic { get; set; }
        public string MqttUser { get; set; }
        public string MqttPassword { get; set; }
        public bool TcpEnabled { get; set; }
        public string TcpHost { get; set; }
        public int TcpPort { get; set; }
        public string CustomerId { get; set; }
        public int PollSeconds { get; set; }
        public int HeartbeatSeconds { get; set; }
        public bool StartWithWindows { get; set; }
        public bool UpdateEnabled { get; set; }
        public string UpdateRepository { get; set; }
        public string UpdateAssetName { get; set; }

        public static AppSettings CreateDefault()
        {
            return new AppSettings
            {
                MessageLogPath = @"D:\DairyPln\MessageLog_1.adf",
                AlarmSettingsPath = @"D:\DairyPln\RDM\configuration\preferences\user\alarmssettings.properties",
                PriorityPath = @"D:\DairyPln\RDM\configuration\data\rdm\useralarmpriorities.properties",
                TranslationPath = @"D:\Release\Assets\translations_de.properties",
                MqttHost = "", MqttPort = 1883, MqttTopic = "mione/{kunde}/alarm",
                TcpHost = "", TcpPort = 5000, CustomerId = "", PollSeconds = 2, HeartbeatSeconds = 60,
                UpdateEnabled = true, UpdateRepository = "mrcrash112/Mione-Alarmmelder", UpdateAssetName = "MioneAlarmmelder.exe"
            };
        }

        public string[] MissingFiles()
        {
            System.Collections.Generic.List<string> missing = new System.Collections.Generic.List<string>();
            if (!File.Exists(MessageLogPath)) missing.Add("Alarmdatei");
            if (!File.Exists(AlarmSettingsPath)) missing.Add("Alarm-Einstellungen");
            if (!File.Exists(PriorityPath)) missing.Add("Alarm-Prioritäten");
            if (!File.Exists(TranslationPath)) missing.Add("Übersetzungen");
            return missing.ToArray();
        }
    }
}
