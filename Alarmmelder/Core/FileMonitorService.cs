using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace MioneAlarmmelder.Core
{
    public sealed class FileMonitorService : IDisposable
    {
        private readonly object sync = new object();
        private AppSettings settings;
        private Timer timer;
        private bool polling;
        private string lastAlarmLine;
        private DateTime logStamp = DateTime.MinValue;
        private long logLength = -1;
        private bool logSizeChanged;
        private DateTime settingsStamp = DateTime.MinValue;
        private DateTime priorityStamp = DateTime.MinValue;
        private DateTime translationStamp = DateTime.MinValue;
        private Dictionary<string, string> phones = new Dictionary<string, string>();
        private Dictionary<string, string> active = new Dictionary<string, string>();
        private Dictionary<string, string> priorities = new Dictionary<string, string>();
        private Dictionary<string, string> translations = new Dictionary<string, string>();

        public event EventHandler<AlarmEventArgs> AlarmFound;
        public event EventHandler<MonitorStatusEventArgs> StatusChanged;
        public event EventHandler PhoneSettingsChanged;

        public FileMonitorService(AppSettings settings) { this.settings = settings; }

        public void Start()
        {
            ReloadReferenceFiles(true);
            lastAlarmLine = ReadLastNonEmptyLine(settings.MessageLogPath);
            CaptureLogState();
            timer = new Timer(Poll, null, 0, Math.Max(1, settings.PollSeconds) * 1000);
        }

        public void ApplySettings(AppSettings value)
        {
            lock (sync)
            {
                settings = value; lastAlarmLine = null;
                settingsStamp = priorityStamp = translationStamp = DateTime.MinValue;
                ReloadReferenceFiles(true);
                lastAlarmLine = ReadLastNonEmptyLine(settings.MessageLogPath);
                CaptureLogState();
                if (timer != null) timer.Change(0, Math.Max(1, settings.PollSeconds) * 1000);
            }
        }

        public KeyValuePair<string, bool>[] GetPhones()
        {
            lock (sync)
            {
                List<KeyValuePair<string, bool>> result = new List<KeyValuePair<string, bool>>();
                for (int i = 1; i <= 5; i++)
                {
                    string key = i.ToString(); string number;
                    phones.TryGetValue(key, out number);
                    bool enabled = i == 1 || String.Equals(GetValue(active, key), "true", StringComparison.OrdinalIgnoreCase);
                    if (!String.IsNullOrEmpty(number)) result.Add(new KeyValuePair<string, bool>(number, enabled));
                }
                return result.ToArray();
            }
        }

        private void Poll(object state)
        {
            lock (sync)
            {
                if (polling) return; polling = true;
                try
                {
                    ReloadReferenceFiles(false);
                    if (LogChanged())
                    {
                        string line = ReadLastNonEmptyLine(settings.MessageLogPath);
                        if (line != null && (logSizeChanged || !String.Equals(line, lastAlarmLine, StringComparison.Ordinal)))
                        {
                            lastAlarmLine = line;
                            AlarmMessage alarm;
                            if (AlarmMessage.TryParse(line, out alarm))
                            {
                                alarm.Priority = FindPriority(alarm.Code);
                                alarm.ClearText = FormatTranslation(alarm);
                                OnAlarmFound(new AlarmEventArgs(alarm, GetActiveNumbers()));
                            }
                            else OnStatus("Ungültige Alarmzeile: " + line, MonitorState.Error);
                        }
                    }
                }
                catch (Exception ex) { OnStatus(ex.Message, MonitorState.Error); }
                finally { polling = false; }
            }
        }

        private void ReloadReferenceFiles(bool force)
        {
            bool phoneChanged = false;
            if (force || Changed(settings.AlarmSettingsPath, ref settingsStamp))
            {
                Dictionary<string, string> source = PropertiesFile.Read(settings.AlarmSettingsPath);
                phones.Clear(); active.Clear();
                for (int i = 1; i <= 5; i++)
                {
                    string id = i.ToString();
                    phones[id] = PropertiesFile.Get(source, "string.preferences.user.alarmssettings.telephone.number." + id, "");
                    active[id] = PropertiesFile.Get(source, "boolean.preferences.user.alarmssettings.telephone.number.active." + id, i == 1 ? "true" : "false");
                }
                phoneChanged = true;
            }
            if (force || Changed(settings.PriorityPath, ref priorityStamp)) priorities = PropertiesFile.Read(settings.PriorityPath);
            if (force || Changed(settings.TranslationPath, ref translationStamp)) translations = PropertiesFile.Read(settings.TranslationPath);
            if (phoneChanged && PhoneSettingsChanged != null) PhoneSettingsChanged(this, EventArgs.Empty);
        }

        private static bool Changed(string path, ref DateTime previous)
        {
            DateTime current = File.GetLastWriteTimeUtc(path);
            if (current == previous) return false; previous = current; return true;
        }

        private void CaptureLogState()
        {
            FileInfo info = new FileInfo(settings.MessageLogPath);
            logStamp = info.LastWriteTimeUtc; logLength = info.Length;
        }

        private bool LogChanged()
        {
            FileInfo info = new FileInfo(settings.MessageLogPath);
            DateTime stamp = info.LastWriteTimeUtc; long length = info.Length;
            logSizeChanged = length != logLength;
            bool changed = stamp != logStamp || length != logLength;
            logStamp = stamp; logLength = length; return changed;
        }

        private string FindPriority(string code)
        {
            string exact = "string.data.rdm.alarmmessage.priority." + code;
            string value; return priorities.TryGetValue(exact, out value) ? value : "unbekannt";
        }

        private string FormatTranslation(AlarmMessage alarm)
        {
            string text;
            if (!translations.TryGetValue("message." + alarm.Code, out text) && !translations.TryGetValue("messages." + alarm.Code, out text)) return "Alarmtext nicht gefunden";
            return text.Replace("{0}", alarm.DateText + " " + alarm.TimeText).Replace("{1}", ExtractNumber(alarm.Location));
        }

        private static string ExtractNumber(string location)
        {
            if (String.IsNullOrEmpty(location)) return "";
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < location.Length; i++) if (Char.IsDigit(location[i]) || location[i] == '-') b.Append(location[i]);
            return b.Length == 0 ? location : b.ToString();
        }

        private string[] GetActiveNumbers()
        {
            List<string> result = new List<string>();
            for (int i = 1; i <= 5; i++)
            {
                string id = i.ToString(), number = GetValue(phones, id);
                bool enabled = i == 1 || String.Equals(GetValue(active, id), "true", StringComparison.OrdinalIgnoreCase);
                if (enabled && !String.IsNullOrEmpty(number)) result.Add(number);
            }
            return result.ToArray();
        }

        private static string GetValue(Dictionary<string, string> source, string key) { string value; return source.TryGetValue(key, out value) ? value : ""; }
        private static string ReadLastNonEmptyLine(string path)
        {
            string last = null;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                long start = Math.Max(0, stream.Length - 65536);
                stream.Seek(start, SeekOrigin.Begin);
                using (StreamReader reader = new StreamReader(stream, Encoding.Default, true))
                {
                    if (start > 0) reader.ReadLine();
                    string line; while ((line = reader.ReadLine()) != null) if (line.Trim().Length > 0) last = line.Trim();
                }
            }
            return last;
        }
        private void OnAlarmFound(AlarmEventArgs e) { if (AlarmFound != null) AlarmFound(this, e); }
        private void OnStatus(string text, MonitorState state) { if (StatusChanged != null) StatusChanged(this, new MonitorStatusEventArgs(text, state)); }
        public void Dispose() { if (timer != null) { timer.Dispose(); timer = null; } }
    }

    public enum MonitorState { Disabled, Waiting, Ok, Sending, Error }
    public sealed class AlarmEventArgs : EventArgs { public AlarmMessage Alarm { get; private set; } public string[] PhoneNumbers { get; private set; } public AlarmEventArgs(AlarmMessage a, string[] p) { Alarm = a; PhoneNumbers = p; } }
    public sealed class MonitorStatusEventArgs : EventArgs { public string Text { get; private set; } public MonitorState State { get; private set; } public MonitorStatusEventArgs(string t, MonitorState s) { Text = t; State = s; } }
}
