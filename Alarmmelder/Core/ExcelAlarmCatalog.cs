using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Packaging;
using System.Text;
using System.Xml;

namespace MioneAlarmmelder.Core
{
    public sealed class ExcelAlarmInfo
    {
        public string EnglishDescription { get; set; }
        public string Description { get; set; }
        public string Cause { get; set; }
        public string Solution { get; set; }
    }

    public static class ExcelAlarmCatalog
    {
        public static Dictionary<string, ExcelAlarmInfo> Read(string path)
        {
            Dictionary<string, ExcelAlarmInfo> result = new Dictionary<string, ExcelAlarmInfo>(StringComparer.OrdinalIgnoreCase);
            using (Package package = Package.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                List<string> sharedStrings = ReadSharedStrings(package);
                foreach (PackagePart part in package.GetParts())
                {
                    string uri = part.Uri.OriginalString.ToLowerInvariant();
                    if (!uri.StartsWith("/xl/worksheets/sheet") || !uri.EndsWith(".xml")) continue;
                    ReadWorksheet(part, sharedStrings, result);
                }
            }
            return result;
        }

        private static List<string> ReadSharedStrings(Package package)
        {
            List<string> values = new List<string>(); Uri uri = PackUriHelper.CreatePartUri(new Uri("/xl/sharedStrings.xml", UriKind.Relative));
            if (!package.PartExists(uri)) return values;
            XmlDocument document = Load(package.GetPart(uri)); XmlNodeList strings = document.SelectNodes("//*[local-name()='si']");
            for (int i = 0; i < strings.Count; i++) values.Add(CombinedText(strings[i])); return values;
        }

        private static void ReadWorksheet(PackagePart part, List<string> sharedStrings, Dictionary<string, ExcelAlarmInfo> result)
        {
            XmlDocument document = Load(part); XmlNodeList rows = document.SelectNodes("//*[local-name()='sheetData']/*[local-name()='row']");
            for (int i = 0; i < rows.Count; i++)
            {
                string code = "", englishDescription = "", description = "", cause = "", solution = ""; XmlNodeList cells = rows[i].SelectNodes("*[local-name()='c']");
                for (int c = 0; c < cells.Count; c++)
                {
                    int column = ColumnNumber(Attribute(cells[c], "r"));
                    if (column != 1 && column != 5 && column != 6 && column != 8 && column != 10) continue;
                    string value = CellValue(cells[c], sharedStrings);
                    if (column == 1) code = NormalizeCode(value); else if (column == 5) englishDescription = value.Trim(); else if (column == 6) description = value.Trim(); else if (column == 8) cause = value.Trim(); else solution = value.Trim();
                }
                if (code.Length == 0 || !ContainsDigit(code)) continue;
                ExcelAlarmInfo current;
                if (!result.TryGetValue(code, out current)) { current = new ExcelAlarmInfo(); result[code] = current; }
                if (englishDescription.Length > 0) current.EnglishDescription = englishDescription;
                if (description.Length > 0) current.Description = description;
                if (cause.Length > 0) current.Cause = cause;
                if (solution.Length > 0) current.Solution = solution;
            }
        }

        private static XmlDocument Load(PackagePart part)
        {
            XmlDocument document = new XmlDocument(); using (Stream stream = part.GetStream(FileMode.Open, FileAccess.Read)) document.Load(stream); return document;
        }
        private static string CellValue(XmlNode cell, List<string> sharedStrings)
        {
            string type = Attribute(cell, "t");
            if (type == "inlineStr") { XmlNode inline = cell.SelectSingleNode("*[local-name()='is']"); return inline == null ? "" : CombinedText(inline); }
            XmlNode value = cell.SelectSingleNode("*[local-name()='v']"); if (value == null) return "";
            if (type == "s") { int index; return Int32.TryParse(value.InnerText, out index) && index >= 0 && index < sharedStrings.Count ? sharedStrings[index] : ""; }
            return value.InnerText;
        }
        private static string CombinedText(XmlNode node)
        {
            StringBuilder text = new StringBuilder(); XmlNodeList nodes = node.SelectNodes(".//*[local-name()='t']");
            for (int i = 0; i < nodes.Count; i++) text.Append(nodes[i].InnerText); return text.ToString();
        }
        private static string NormalizeCode(string value)
        {
            string trimmed = (value ?? "").Trim(); decimal number;
            if (Decimal.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out number) && number == Decimal.Truncate(number)) return Decimal.Truncate(number).ToString(CultureInfo.InvariantCulture);
            return trimmed;
        }
        private static int ColumnNumber(string reference)
        {
            int result = 0;
            for (int i = 0; i < reference.Length && Char.IsLetter(reference[i]); i++) result = result * 26 + (Char.ToUpperInvariant(reference[i]) - 'A' + 1);
            return result;
        }
        private static bool ContainsDigit(string value) { for (int i = 0; i < value.Length; i++) if (Char.IsDigit(value[i])) return true; return false; }
        private static string Attribute(XmlNode node, string name) { XmlAttribute attribute = node.Attributes[name]; return attribute == null ? "" : attribute.Value; }
    }
}
