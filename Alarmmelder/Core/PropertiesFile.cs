using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MioneAlarmmelder.Core
{
    public static class PropertiesFile
    {
        public static Dictionary<string, string> Read(string path)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (StreamReader reader = new StreamReader(stream, Encoding.GetEncoding(1252), true))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("!")) continue;
                    int equals = line.IndexOf('=');
                    if (equals < 0) continue;
                    result[line.Substring(0, equals).Trim()] = line.Substring(equals + 1).Trim();
                }
            }
            return result;
        }

        public static string Get(Dictionary<string, string> values, string key, string fallback)
        {
            string value; return values.TryGetValue(key, out value) ? value : fallback;
        }
    }
}
