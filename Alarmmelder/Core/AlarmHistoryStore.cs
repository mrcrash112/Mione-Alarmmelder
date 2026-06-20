using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace MioneAlarmmelder.Core
{
    public sealed class AlarmHistoryEntry
    {
        public long Id { get; set; }
        public DateTime ReceivedAt { get; set; }
        public bool Acknowledged { get; set; }
        public AlarmMessage Alarm { get; set; }
    }

    public static class AlarmHistoryStore
    {
        private static readonly object Sync = new object();
        private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MioneAlarmmelder");
        private static readonly string FileName = Path.Combine(Folder, "alarm-history.xml");

        public static List<AlarmHistoryEntry> Load(int maximumEntries)
        {
            List<AlarmHistoryEntry> entries = new List<AlarmHistoryEntry>();
            lock (Sync)
            {
                try
                {
                    if (!File.Exists(FileName)) return entries;
                    XmlDocument document = new XmlDocument(); document.Load(FileName);
                    XmlNodeList nodes = document.SelectNodes("/alarms/alarm");
                    for (int i = 0; i < nodes.Count && entries.Count < maximumEntries; i++)
                    {
                        XmlNode node = nodes[i]; long id; long ticks; bool acknowledged;
                        if (!Int64.TryParse(Attribute(node, "id"), out id)) id = DateTime.Now.Ticks + i;
                        if (!Int64.TryParse(Attribute(node, "ticks"), out ticks)) ticks = DateTime.Now.Ticks;
                        if (!Boolean.TryParse(Attribute(node, "acknowledged"), out acknowledged)) acknowledged = false;
                        AlarmMessage alarm = new AlarmMessage
                        {
                            DateText = Child(node, "date"), TimeText = Child(node, "time"), Code = Child(node, "code"),
                            Location = Child(node, "location"), CowNumber = Child(node, "cow"), Priority = Child(node, "priority"), ClearText = Child(node, "text"),
                            Cause = Child(node, "cause"), Solution = Child(node, "solution")
                        };
                        if (String.IsNullOrEmpty(alarm.Priority) || alarm.Priority == "unbekannt") alarm.Priority = "System";
                        entries.Add(new AlarmHistoryEntry { Id = id, ReceivedAt = new DateTime(ticks), Acknowledged = acknowledged, Alarm = alarm });
                    }
                }
                catch (Exception ex) { ErrorLogger.Log("Alarmhistorie", ex); }
            }
            return entries;
        }

        public static void Save(List<AlarmHistoryEntry> entries, int maximumEntries)
        {
            lock (Sync)
            {
                try
                {
                    if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
                    XmlWriterSettings settings = new XmlWriterSettings(); settings.Indent = true; settings.Encoding = Encoding.UTF8;
                    string temporary = FileName + ".tmp";
                    using (XmlWriter writer = XmlWriter.Create(temporary, settings))
                    {
                        writer.WriteStartElement("alarms");
                        int count = Math.Min(entries.Count, maximumEntries);
                        for (int i = 0; i < count; i++)
                        {
                            AlarmHistoryEntry entry = entries[i]; AlarmMessage alarm = entry.Alarm;
                            writer.WriteStartElement("alarm"); writer.WriteAttributeString("id", entry.Id.ToString(CultureInfo.InvariantCulture));
                            writer.WriteAttributeString("ticks", entry.ReceivedAt.Ticks.ToString(CultureInfo.InvariantCulture)); writer.WriteAttributeString("acknowledged", entry.Acknowledged.ToString());
                            Write(writer, "date", alarm.DateText); Write(writer, "time", alarm.TimeText); Write(writer, "code", alarm.Code);
                            Write(writer, "location", alarm.Location); Write(writer, "cow", alarm.CowNumber); Write(writer, "priority", alarm.Priority); Write(writer, "text", alarm.ClearText);
                            Write(writer, "cause", alarm.Cause); Write(writer, "solution", alarm.Solution);
                            writer.WriteEndElement();
                        }
                        writer.WriteEndElement();
                    }
                    if (File.Exists(FileName)) File.Delete(FileName); File.Move(temporary, FileName);
                }
                catch (Exception ex) { ErrorLogger.Log("Alarmhistorie", ex); }
            }
        }

        private static string Attribute(XmlNode node, string name) { XmlAttribute value = node.Attributes[name]; return value == null ? "" : value.Value; }
        private static string Child(XmlNode node, string name) { XmlNode value = node.SelectSingleNode(name); return value == null ? "" : value.InnerText; }
        private static void Write(XmlWriter writer, string name, string value) { writer.WriteElementString(name, value ?? ""); }
    }
}
