using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MioneAlarmmelder.Core
{
    public static class ErrorLogger
    {
        private static readonly object Sync = new object();
        private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MioneAlarmmelder");
        private static readonly string LogFile = Path.Combine(Folder, "errors.log");
        private static string lastError = ""; private static DateTime lastErrorTime = DateTime.MinValue;
        public static event EventHandler<ErrorLoggedEventArgs> ErrorLogged;
        public static string FilePath { get { return LogFile; } }

        public static void Log(string source, string message)
        {
            ErrorLogEntry entry = new ErrorLogEntry(DateTime.Now, Clean(source), Clean(message));
            try
            {
                lock (Sync)
                {
                    string signature = entry.Source + "|" + entry.Message;
                    if (signature == lastError && DateTime.Now.Subtract(lastErrorTime).TotalSeconds < 30) return;
                    lastError = signature; lastErrorTime = DateTime.Now;
                    if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
                    if (File.Exists(LogFile) && new FileInfo(LogFile).Length > 2 * 1024 * 1024)
                    {
                        string old = LogFile + ".old"; if (File.Exists(old)) File.Delete(old); File.Move(LogFile, old);
                    }
                    using (StreamWriter writer = new StreamWriter(LogFile, true, Encoding.UTF8))
                        writer.WriteLine(entry.Time.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + entry.Source + "\t" + entry.Message);
                }
            }
            catch { }
            if (ErrorLogged != null) ErrorLogged(null, new ErrorLoggedEventArgs(entry));
        }

        public static void Log(string source, Exception exception) { Log(source, exception == null ? "Unbekannter Fehler" : exception.ToString()); }

        public static ErrorLogEntry[] ReadRecent(int maximum)
        {
            List<ErrorLogEntry> result = new List<ErrorLogEntry>();
            try
            {
                lock (Sync)
                {
                    if (!File.Exists(LogFile)) return result.ToArray();
                    string[] lines = File.ReadAllLines(LogFile, Encoding.UTF8);
                    for (int i = lines.Length - 1; i >= 0 && result.Count < maximum; i--)
                    {
                        string[] parts = lines[i].Split(new char[] { '\t' }, 3);
                        DateTime time; if (parts.Length == 3 && DateTime.TryParse(parts[0], out time)) result.Add(new ErrorLogEntry(time, parts[1], parts[2]));
                    }
                }
            }
            catch { }
            return result.ToArray();
        }

        public static void Clear()
        {
            lock (Sync) { try { if (File.Exists(LogFile)) File.Delete(LogFile); } catch { } }
        }
        private static string Clean(string value) { return (value ?? "").Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim(); }
    }

    public sealed class ErrorLogEntry
    {
        public DateTime Time { get; private set; } public string Source { get; private set; } public string Message { get; private set; }
        public ErrorLogEntry(DateTime time, string source, string message) { Time = time; Source = source; Message = message; }
    }
    public sealed class ErrorLoggedEventArgs : EventArgs
    {
        public ErrorLogEntry Entry { get; private set; } public ErrorLoggedEventArgs(ErrorLogEntry entry) { Entry = entry; }
    }
}
