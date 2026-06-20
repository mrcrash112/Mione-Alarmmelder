using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace MioneAlarmmelder.Core
{
    public static class ErrorLogger
    {
        private static readonly object Sync = new object();
        private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MioneAlarmmelder");
        private static readonly string LogFile = Path.Combine(Folder, "errors.log");
        private static string lastError = ""; private static DateTime lastErrorTime = DateTime.MinValue; private static int maximumEntries = 2500;
        public static event EventHandler<ErrorLoggedEventArgs> ErrorLogged;
        public static string FilePath { get { return LogFile; } }

        public static void ConfigureMaximum(int maximum)
        {
            lock (Sync) { maximumEntries = Math.Max(100, Math.Min(10000, maximum)); TrimUnsafe(); }
        }

        public static void Log(string source, string message)
        {
            ErrorLogEntry entry = new ErrorLogEntry(DateTime.UtcNow.Ticks, DateTime.Now, Clean(source), Clean(message), false);
            try
            {
                lock (Sync)
                {
                    string signature = entry.Source + "|" + entry.Message;
                    if (signature == lastError && DateTime.Now.Subtract(lastErrorTime).TotalSeconds < 30) return;
                    lastError = signature; lastErrorTime = DateTime.Now;
                    if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
                    using (StreamWriter writer = new StreamWriter(LogFile, true, Encoding.UTF8)) writer.WriteLine(Serialize(entry));
                    TrimUnsafe();
                }
            }
            catch { }
            if (ErrorLogged != null) ErrorLogged(null, new ErrorLoggedEventArgs(entry));
        }

        public static void Log(string source, Exception exception) { Log(source, exception == null ? "Unbekannter Fehler" : exception.ToString()); }

        public static ErrorLogEntry[] ReadRecent(int maximum)
        {
            lock (Sync)
            {
                List<ErrorLogEntry> all = ReadAllUnsafe(); List<ErrorLogEntry> result = new List<ErrorLogEntry>();
                for (int i = all.Count - 1; i >= 0 && result.Count < maximum; i--) result.Add(all[i]); return result.ToArray();
            }
        }

        public static void Acknowledge(long[] ids)
        {
            lock (Sync)
            {
                Dictionary<long, bool> selected = new Dictionary<long, bool>(); for (int i = 0; i < ids.Length; i++) selected[ids[i]] = true;
                List<ErrorLogEntry> entries = ReadAllUnsafe(); for (int i = 0; i < entries.Count; i++) if (selected.ContainsKey(entries[i].Id)) entries[i].Acknowledged = true;
                WriteAllUnsafe(entries);
            }
        }
        public static void AcknowledgeAll()
        {
            lock (Sync) { List<ErrorLogEntry> entries = ReadAllUnsafe(); for (int i = 0; i < entries.Count; i++) entries[i].Acknowledged = true; WriteAllUnsafe(entries); }
        }
        public static void Clear() { lock (Sync) { try { if (File.Exists(LogFile)) File.Delete(LogFile); } catch { } } }

        private static void TrimUnsafe()
        {
            List<ErrorLogEntry> entries = ReadAllUnsafe(); if (entries.Count <= maximumEntries) return;
            entries.RemoveRange(0, entries.Count - maximumEntries); WriteAllUnsafe(entries);
        }
        private static List<ErrorLogEntry> ReadAllUnsafe()
        {
            List<ErrorLogEntry> result = new List<ErrorLogEntry>();
            try
            {
                if (!File.Exists(LogFile)) return result; string[] lines = File.ReadAllLines(LogFile, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string[] parts = lines[i].Split(new char[] { '\t' }); DateTime time; long id; bool acknowledged;
                    if (parts.Length >= 5 && Int64.TryParse(parts[0], out id) && DateTime.TryParse(parts[1], out time) && Boolean.TryParse(parts[2], out acknowledged))
                        result.Add(new ErrorLogEntry(id, time, parts[3], parts[4], acknowledged));
                    else if (parts.Length >= 3 && DateTime.TryParse(parts[0], out time)) result.Add(new ErrorLogEntry(time.Ticks + i, time, parts[1], parts[2], false));
                }
            }
            catch { }
            return result;
        }
        private static void WriteAllUnsafe(List<ErrorLogEntry> entries)
        {
            try
            {
                if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
                using (StreamWriter writer = new StreamWriter(LogFile, false, Encoding.UTF8)) for (int i = 0; i < entries.Count; i++) writer.WriteLine(Serialize(entries[i]));
            }
            catch { }
        }
        private static string Serialize(ErrorLogEntry entry) { return entry.Id.ToString(CultureInfo.InvariantCulture) + "\t" + entry.Time.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + entry.Acknowledged + "\t" + entry.Source + "\t" + entry.Message; }
        private static string Clean(string value) { return (value ?? "").Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim(); }
    }

    public sealed class ErrorLogEntry
    {
        public long Id { get; private set; } public DateTime Time { get; private set; } public string Source { get; private set; } public string Message { get; private set; } public bool Acknowledged { get; set; }
        public ErrorLogEntry(long id, DateTime time, string source, string message, bool acknowledged) { Id = id; Time = time; Source = source; Message = message; Acknowledged = acknowledged; }
    }
    public sealed class ErrorLoggedEventArgs : EventArgs
    {
        public ErrorLogEntry Entry { get; private set; } public ErrorLoggedEventArgs(ErrorLogEntry entry) { Entry = entry; }
    }
}
