using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Net;
using System.Text;
using System.Xml;
using MioneAlarmmelder.Core;

namespace MioneAlarmmelder.Transport
{
    public sealed class DairyPlanCommandBridge
    {
        private readonly AppSettings settings;
        public DairyPlanCommandBridge(AppSettings settings) { this.settings = settings; }

        public DairyPlanCommandResult Execute(MilkingRobotCommand command)
        {
            try
            {
                string root = NormalizeRoot(settings.DpProcessPath);
                string rdmJar = Path.Combine(root, "rdm-manager.jar");
                string ior = Path.Combine(root, @"RDM\CORBA\DP_RDM_COM.ior");
                string bridgeJar = Path.Combine(AppDirectory(), @"Assets\MioneDairyPlanBridge.jar");
                if (!File.Exists(rdmJar)) return DairyPlanCommandResult.Fail("nativeBridgeUnavailable", "rdm-manager.jar wurde nicht gefunden: " + rdmJar);
                if (!File.Exists(ior)) return DairyPlanCommandResult.Fail("nativeBridgeUnavailable", "DP_RDM_COM.ior wurde nicht gefunden: " + ior);
                if (!File.Exists(bridgeJar)) return DairyPlanCommandResult.Fail("nativeBridgeUnavailable", "MioneDairyPlanBridge.jar wurde nicht gefunden: " + bridgeJar);

                DairyPlanCommandResult waitResult = WaitForDpProcessControl();
                if (waitResult != null) return waitResult;

                BoxStateSnapshot beforeState = RequiresBoxStateConfirmation(command) ? ReadBoxState(command.BoxNumber) : null;
                string java = FindJava(root);
                string classPath = rdmJar + Path.PathSeparator + bridgeJar;
                string args = "-cp " + Quote(classPath) + " MioneDairyPlanBridge --ior " + Quote(ior) + " --command " + Quote(command.Name) +
                    Optional("--box", command.BoxNumber) + Optional("--robot-position", command.RobotPosition) +
                    Optional("--sampling-box", command.SamplingBox) + Optional("--feeding-type", command.FeedingType);

                ProcessStartInfo start = new ProcessStartInfo();
                start.FileName = java; start.Arguments = args; start.WorkingDirectory = root;
                start.UseShellExecute = false; start.CreateNoWindow = true; start.RedirectStandardOutput = true; start.RedirectStandardError = true;
                using (Process process = Process.Start(start))
                {
                    if (!process.WaitForExit(15000))
                    {
                        try { process.Kill(); } catch { }
                        return DairyPlanCommandResult.Fail("nativeBridgeTimeout", "DairyPlan-Bridge hat nach 15 Sekunden nicht geantwortet.");
                    }
                    string output = ReadToEnd(process.StandardOutput);
                    string error = ReadToEnd(process.StandardError);
                    string text = (output + "\r\n" + error).Trim();
                    if (text.IndexOf("org/omg/", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("org.omg.", StringComparison.OrdinalIgnoreCase) >= 0)
                        text += "\r\nJava Runtime ohne CORBA erkannt. Bitte Java 6 bis Java 8 fuer die DairyPlan-Bridge verwenden.";
                    if (process.ExitCode == 0)
                    {
                        DairyPlanCommandResult result = new DairyPlanCommandResult(true, "forwardedToDairyPlan", text.Length == 0 ? "Befehl wurde an DPProcessControl uebergeben." : text);
                        if (beforeState != null)
                        {
                            BoxStateSnapshot afterState = WaitForBoxStateChange(command.BoxNumber, beforeState, 12000);
                            if (afterState == null)
                                return DairyPlanCommandResult.Fail("nativeBridgeNoEffect", "DairyPlan hat den Befehl angenommen, aber fuer Box " + command.BoxNumber + " ist keine sichtbare Zustandsaenderung aufgetreten.");
                            result = new DairyPlanCommandResult(true, "forwardedToDairyPlan", result.Message + "\r\nBox " + command.BoxNumber + " Zustand aktualisiert: " + afterState.Summary);
                        }
                        return result;
                    }
                    return DairyPlanCommandResult.Fail("nativeBridgeError", "DairyPlan-Bridge Fehler " + process.ExitCode + ": " + text);
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.Log("DairyPlan-Bridge", ex);
                return DairyPlanCommandResult.Fail("nativeBridgeError", ex.Message);
            }
        }

        private static string FindJava(string dairyPlanRoot)
        {
            string[] candidates = new string[]
            {
                Path.Combine(dairyPlanRoot, @"bin\java.exe"),
                Path.Combine(dairyPlanRoot, @"jre\bin\java.exe"),
                Path.Combine(dairyPlanRoot, @"Java\bin\java.exe"),
                "java.exe",
                "java"
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i].IndexOf(Path.DirectorySeparatorChar) < 0 && candidates[i].IndexOf(Path.AltDirectorySeparatorChar) < 0) return candidates[i];
                if (File.Exists(candidates[i])) return candidates[i];
            }
            return "java.exe";
        }

        private static DairyPlanCommandResult WaitForDpProcessControl()
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(60);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    if (Process.GetProcessesByName("DPProcessControl").Length > 0) return null;
                }
                catch (Exception ex)
                {
                    ErrorLogger.Log("DPProcessControl-Pruefung", ex);
                    return DairyPlanCommandResult.Fail("nativeBridgeUnavailable", "DPProcessControl konnte nicht geprueft werden: " + ex.Message);
                }
                System.Threading.Thread.Sleep(1000);
            }
            return DairyPlanCommandResult.Fail("nativeBridgeUnavailable", "DPProcessControl laeuft noch nicht. Der Alarmmelder startet DairyPlan nicht selbst und wartet auf den Systemstart.");
        }

        private static string AppDirectory()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        private static string NormalizeRoot(string path)
        {
            return String.IsNullOrEmpty(path) ? @"D:\DairyPln" : path.Trim().TrimEnd('\\', '/');
        }

        private static string Optional(string name, string value)
        {
            return String.IsNullOrEmpty(value) ? "" : " " + name + " " + Quote(value);
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }

        private static string ReadToEnd(StreamReader reader)
        {
            StringBuilder b = new StringBuilder();
            char[] buffer = new char[1024]; int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0) b.Append(buffer, 0, read);
            return b.ToString();
        }

        private static bool RequiresBoxStateConfirmation(MilkingRobotCommand command)
        {
            if (command == null || String.IsNullOrEmpty(command.BoxNumber)) return false;
            return String.Equals(command.Name, "enableBox", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(command.Name, "disableBox", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(command.Name, "startShortCleaning", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(command.Name, "stopShortCleaning", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(command.Name, "stopMilking", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(command.Name, "setManualMilkingOneBox", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(command.Name, "setAutomaticMilkingOneBox", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(command.Name, "startAugerCalibration", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(command.Name, "stopAugerCalibration", StringComparison.OrdinalIgnoreCase);
        }

        private static BoxStateSnapshot ReadBoxState(string boxNumber)
        {
            if (String.IsNullOrEmpty(boxNumber)) return null;
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://localhost:8080/RdmDataService.svc/BoxInfos");
                request.Timeout = 1500; request.ReadWriteTimeout = 1500; request.Accept = "application/atom+xml,application/xml,text/xml";
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                {
                    XmlDocument document = new XmlDocument(); document.Load(stream);
                    XmlNodeList entries = document.GetElementsByTagName("entry");
                    for (int i = 0; i < entries.Count; i++)
                    {
                        BoxStateSnapshot snapshot = ReadBoxState(entries[i], boxNumber);
                        if (snapshot != null) return snapshot;
                    }
                    XmlNodeList properties = document.GetElementsByTagName("properties", "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata");
                    for (int i = 0; i < properties.Count; i++)
                    {
                        BoxStateSnapshot snapshot = ReadBoxState(properties[i], boxNumber);
                        if (snapshot != null) return snapshot;
                    }
                }
            }
            catch { }
            return null;
        }

        private static BoxStateSnapshot ReadBoxState(XmlNode node, string boxNumber)
        {
            if (node == null) return null;
            if (!String.Equals(ChildText(node, "BoxNumber"), boxNumber, StringComparison.OrdinalIgnoreCase)) return null;
            return new BoxStateSnapshot
            {
                BoxNumber = ChildText(node, "BoxNumber"),
                AttachmentStatus = ChildText(node, "AttachmentStatus"),
                OperationStatus = ChildText(node, "OperationStatus"),
                OperationStatusText = ChildText(node, "OperationStatusText"),
                BoxStatus = ChildText(node, "BoxStatus"),
                BoxStatusText = ChildText(node, "BoxStatusText")
            };
        }

        private static BoxStateSnapshot WaitForBoxStateChange(string boxNumber, BoxStateSnapshot before, int timeoutMilliseconds)
        {
            DateTime until = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
            while (DateTime.UtcNow <= until)
            {
                BoxStateSnapshot current = ReadBoxState(boxNumber);
                if (current != null && !String.Equals(current.Signature, before.Signature, StringComparison.OrdinalIgnoreCase)) return current;
                System.Threading.Thread.Sleep(500);
            }
            return null;
        }

        private static string ChildText(XmlNode node, string localName)
        {
            if (node == null) return "";
            if (String.Equals(node.LocalName, localName, StringComparison.OrdinalIgnoreCase)) return node.InnerText.Trim();
            for (int i = 0; i < node.ChildNodes.Count; i++)
            {
                string value = ChildText(node.ChildNodes[i], localName);
                if (value.Length > 0) return value;
            }
            return "";
        }
    }

    internal sealed class BoxStateSnapshot
    {
        public string BoxNumber;
        public string AttachmentStatus;
        public string OperationStatus;
        public string OperationStatusText;
        public string BoxStatus;
        public string BoxStatusText;

        public string Signature
        {
            get { return BoxNumber + "|" + AttachmentStatus + "|" + OperationStatus + "|" + OperationStatusText + "|" + BoxStatus + "|" + BoxStatusText; }
        }

        public string Summary
        {
            get { return "Attachment=" + AttachmentStatus + ", Operation=" + OperationStatus + ", BoxStatus=" + BoxStatus + ", Text=" + BoxStatusText; }
        }
    }

    public sealed class DairyPlanCommandResult
    {
        public bool Success { get; private set; }
        public string State { get; private set; }
        public string Message { get; private set; }
        public DairyPlanCommandResult(bool success, string state, string message) { Success = success; State = state; Message = message; }
        public static DairyPlanCommandResult Fail(string state, string message) { return new DairyPlanCommandResult(false, state, message); }
    }
}
