using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;
using MioneAlarmmelder.Core;

namespace MioneAlarmmelder.Transport
{
    public sealed class MilkingRobotPublisher
    {
        private readonly AppSettings settings;
        private readonly object snapshotLock = new object();
        private Dictionary<string, string> translations;
        private string translationsPath = "";
        private DateTime translationsStampUtc = DateTime.MinValue;
        private readonly object translationsLock = new object();
        private readonly FileCache<string> runningCache = new FileCache<string>();
        private readonly FileCache<string> pidCache = new FileCache<string>();
        private readonly FileCache<Dictionary<string, string>> amsStatusCache = new FileCache<Dictionary<string, string>>();
        private readonly FileCache<Dictionary<string, string>> systemCleaningCache = new FileCache<Dictionary<string, string>>();
        private readonly FileCache<Dictionary<string, string>> amsCleaningCache = new FileCache<Dictionary<string, string>>();
        private readonly FileCache<string> areaCountersCache = new FileCache<string>();
        private readonly FileCache<string> robotCoordinatesCache = new FileCache<string>();
        private static readonly string[] RequiredFiles = new string[]
        {
            "DPProcessControl.exe",
            "RDM_DP_Com.dll",
            "RDM_DP_Com_CORBA.dll",
            "RDM_DP_Com_Server.dll",
            "DP_RDM_Link.dll",
            "RDM_JNI_DB.dll",
            @"RDM\CORBA\DP_RDM_COM.ior"
        };

        private static readonly string[,] Functions = new string[,]
        {
            { "initializeRobot", "Roboter initialisieren", "PSU_Command.initializeRobot", "" },
            { "initializeSystem", "System initialisieren", "PSU_Command.initializeSystem", "" },
            { "enableBox", "Automatik Eingangstor", "PSU_Command.enableBox", "boxNumber" },
            { "disableBox", "Eingangstor schließen", "PSU_Command.disableBox", "boxNumber" },
            { "startAutomaticOperation", "Automatikbetrieb starten", "PSU_Command.startAutomaticOperation", "" },
            { "stopAutomaticOperation", "Automatikbetrieb stoppen", "PSU_Command.stopAutomaticOperation", "" },
            { "startSystemCleaning", "Systemreinigung starten", "PSU_Command.startSystemCleaning", "" },
            { "stopSystemCleaning", "Systemreinigung stoppen", "PSU_Command.stopSystemCleaning", "" },
            { "startShortCleaning", "Kurzreinigung starten", "PSU_Command.startShortCleaning", "boxNumber" },
            { "stopShortCleaning", "Kurzreinigung stoppen", "PSU_Command.stopShortCleaning", "boxNumber" },
            { "stopMilking", "Melkvorgang abbrechen", "PSU_Command.stopMilking", "boxNumber" },
            { "setManualMilkingOneBox", "Box auf manuelles Melken setzen", "PSU_Command.setManualMilkingOneBox", "boxNumber" },
            { "setAutomaticMilkingOneBox", "Box auf automatisches Melken setzen", "PSU_Command.setAutomaticMilkingOneBox", "boxNumber" },
            { "moveRobotToPosition", "Roboterposition anfahren", "PSU_Command.moveRobotToPosition", "robotPosition" },
            { "startAugerCalibration", "Dosierer-Kalibrierung starten", "PSU_Command.startAugerCalibration", "boxNumber,feedingType" },
            { "stopAugerCalibration", "Dosierer-Kalibrierung stoppen", "PSU_Command.stopAugerCalibration", "boxNumber" },
            { "startPreparationWaterTanks", "Wassertanks vorbereiten", "PSU_Command.startPreparationWaterTanks", "" },
            { "resetAlarm", "Alarm zuruecksetzen", "PSU_Command.resetAlarm", "" },
            { "startSamplingSession", "Probenahme starten", "DPPC_Command.startSamplingSession", "" },
            { "stopSamplingSession", "Probenahme stoppen", "DPPC_Command.stopSamplingSession", "" },
            { "pauseSampling", "Probenahme pausieren", "DPPC_Command.pauseSampling", "samplingBox" },
            { "resumeSampling", "Probenahme fortsetzen", "DPPC_Command.resumeSampling", "samplingBox" }
        };

        public MilkingRobotPublisher(AppSettings settings) { this.settings = settings; }

        public static MilkingRobotFileCheck[] CheckFiles(string dairyPlanPath)
        {
            List<MilkingRobotFileCheck> result = new List<MilkingRobotFileCheck>();
            string root = NormalizeRoot(dairyPlanPath);
            for (int i = 0; i < RequiredFiles.Length; i++)
            {
                string path = Path.Combine(root, RequiredFiles[i]);
                result.Add(new MilkingRobotFileCheck(RequiredFiles[i], path, File.Exists(path)));
            }
            return result.ToArray();
        }

        public void Publish()
        {
            if (!settings.DpProcessEnabled || !settings.SystemMqttReady) return;
            MilkingRobotBoxInfo[] boxes = FetchBoxInfos();
            MqttRoutePublisher.Publish(settings, "Melkroboter", BuildSnapshotJson(boxes), true);
            MqttRoutePublisher.Publish(settings, "Melkroboter/Boxen", BuildBoxesJson(boxes), true);
            MqttRoutePublisher.Publish(settings, "Melkroboter/Funktionen", BuildFunctionsJson(), true);
        }

        private string BuildSnapshotJson(MilkingRobotBoxInfo[] boxes)
        {
            string root = NormalizeRoot(settings.DpProcessPath);
            MilkingRobotFileCheck[] checks = CheckFiles(root);
            StringBuilder b = new StringBuilder();
            b.Append('{');
            Add(b, "type", "melkroboter"); b.Append(',');
            Add(b, "timestampUtc", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")); b.Append(',');
            Add(b, "dairyPlanPath", root); b.Append(',');
            b.Append("\"communicationEnabled\":").Append(settings.DpProcessEnabled ? "true" : "false").Append(',');
            b.Append("\"communicationReady\":").Append(AllFound(checks) ? "true" : "false").Append(',');
            AppendChecks(b, checks); b.Append(',');
            b.Append("\"data\":{");
            Add(b, "rdmRunning", ReadTextCached(Path.Combine(root, @"RDM\running.tdm"), 400, runningCache)); b.Append(',');
            Add(b, "rdmPid", ReadTextCached(Path.Combine(root, @"RDM\pid.tdm"), 80, pidCache)); b.Append(',');
            AddObject(b, "amsStatus", ReadPropertiesCached(Path.Combine(root, @"RDM\configuration\preferences\user\amsstatus.properties"), amsStatusCache)); b.Append(',');
            AddObject(b, "systemCleaning", ReadPropertiesCached(Path.Combine(root, @"RDM\configuration\data\rdm\systemcleaning.properties"), systemCleaningCache)); b.Append(',');
            AddObject(b, "amsCleaning", ReadPropertiesCached(Path.Combine(root, @"RDM\configuration\data\rdm\amscleaning.properties"), amsCleaningCache)); b.Append(',');
            Add(b, "areaCountersXml", ReadTextCached(Path.Combine(root, "AreaCounters.xml"), 24000, areaCountersCache)); b.Append(',');
            Add(b, "robotCurrentCoordinatesCsv", ReadTextCached(Path.Combine(root, "RobotCurrentCoordinates.csv"), 24000, robotCoordinatesCache)); b.Append(',');
            AppendBoxes(b, boxes);
            b.Append("},");
            AppendFunctions(b);
            b.Append('}');
            return b.ToString();
        }

        private string BuildBoxesJson(MilkingRobotBoxInfo[] boxes)
        {
            StringBuilder b = new StringBuilder();
            b.Append('{');
            Add(b, "type", "melkroboterBoxen"); b.Append(',');
            Add(b, "timestampUtc", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")); b.Append(',');
            AppendBoxes(b, boxes);
            b.Append('}');
            return b.ToString();
        }

        private static string BuildFunctionsJson()
        {
            StringBuilder b = new StringBuilder();
            b.Append('{');
            Add(b, "type", "melkroboterFunctions"); b.Append(',');
            Add(b, "timestampUtc", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")); b.Append(',');
            Add(b, "commandTopic", "<firebase_system_id>/Melkroboter/Command"); b.Append(',');
            Add(b, "resultTopic", "<firebase_system_id>/Melkroboter/Result"); b.Append(',');
            Add(b, "commandExample", "{\"requestId\":\"1\",\"command\":\"stopMilking\",\"boxNumber\":1}"); b.Append(',');
            AppendFunctions(b);
            b.Append('}');
            return b.ToString();
        }

        private static void AppendChecks(StringBuilder b, MilkingRobotFileCheck[] checks)
        {
            b.Append("\"requiredFiles\":[");
            for (int i = 0; i < checks.Length; i++)
            {
                if (i > 0) b.Append(',');
                b.Append('{'); Add(b, "name", checks[i].Name); b.Append(','); Add(b, "path", checks[i].Path); b.Append(',');
                b.Append("\"exists\":").Append(checks[i].Exists ? "true" : "false").Append('}');
            }
            b.Append(']');
        }

        private static void AppendFunctions(StringBuilder b)
        {
            b.Append("\"functions\":[");
            for (int i = 0; i < Functions.GetLength(0); i++)
            {
                if (i > 0) b.Append(',');
                b.Append('{'); Add(b, "name", Functions[i, 0]); b.Append(','); Add(b, "label", Functions[i, 1]); b.Append(',');
                Add(b, "source", Functions[i, 2]); b.Append(',');
                AppendParameters(b, Functions[i, 3]); b.Append(',');
                Add(b, "payloadExample", BuildPayloadExample(Functions[i, 0], Functions[i, 3])); b.Append('}');
            }
            b.Append(']');
        }

        public static bool IsKnownFunction(string name)
        {
            for (int i = 0; i < Functions.GetLength(0); i++)
                if (String.Equals(Functions[i, 0], name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static string[] RequiredParametersFor(string name)
        {
            for (int i = 0; i < Functions.GetLength(0); i++)
            {
                if (!String.Equals(Functions[i, 0], name, StringComparison.OrdinalIgnoreCase)) continue;
                if (String.IsNullOrEmpty(Functions[i, 3])) return new string[0];
                string[] parts = Functions[i, 3].Split(',');
                for (int p = 0; p < parts.Length; p++) parts[p] = parts[p].Trim();
                return parts;
            }
            return new string[0];
        }

        public static string BuildCommandResultJson(MilkingRobotCommand command, bool ok, string state, string message)
        {
            StringBuilder b = new StringBuilder();
            b.Append('{');
            Add(b, "type", "melkroboterCommandResult"); b.Append(',');
            Add(b, "timestampUtc", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")); b.Append(',');
            b.Append("\"ok\":").Append(ok ? "true" : "false").Append(',');
            Add(b, "state", state); b.Append(',');
            Add(b, "message", message); b.Append(',');
            Add(b, "requestId", command == null ? "" : command.RequestId); b.Append(',');
            Add(b, "command", command == null ? "" : command.Name); b.Append(',');
            Add(b, "boxNumber", command == null ? "" : command.BoxNumber); b.Append(',');
            Add(b, "robotPosition", command == null ? "" : command.RobotPosition); b.Append(',');
            Add(b, "samplingBox", command == null ? "" : command.SamplingBox); b.Append(',');
            Add(b, "feedingType", command == null ? "" : command.FeedingType); b.Append(',');
            AppendRequiredParameterNames(b, command == null ? new string[0] : RequiredParametersFor(command.Name));
            b.Append('}');
            return b.ToString();
        }

        private static void AppendRequiredParameterNames(StringBuilder b, string[] parameters)
        {
            b.Append("\"requiredParameters\":[");
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i > 0) b.Append(',');
                AddValue(b, parameters[i]);
            }
            b.Append(']');
        }

        private static void AppendParameters(StringBuilder b, string parameterList)
        {
            b.Append("\"parameters\":[");
            if (!String.IsNullOrEmpty(parameterList))
            {
                string[] parts = parameterList.Split(',');
                for (int i = 0; i < parts.Length; i++)
                {
                    if (i > 0) b.Append(',');
                    string name = parts[i].Trim();
                    b.Append('{'); Add(b, "name", name); b.Append(','); Add(b, "type", "number"); b.Append(','); b.Append("\"required\":true"); b.Append('}');
                }
            }
            b.Append(']');
        }

        private static string BuildPayloadExample(string commandName, string parameterList)
        {
            StringBuilder b = new StringBuilder();
            b.Append("{\"requestId\":\"1\",\"command\":\"").Append(commandName).Append('"');
            if (!String.IsNullOrEmpty(parameterList))
            {
                string[] parts = parameterList.Split(',');
                for (int i = 0; i < parts.Length; i++)
                {
                    string name = parts[i].Trim();
                    if (name.Length == 0) continue;
                    b.Append(",\"").Append(name).Append("\":1");
                }
            }
            b.Append('}');
            return b.ToString();
        }

        private static void AppendBoxes(StringBuilder b, MilkingRobotBoxInfo[] boxes)
        {
            b.Append("\"boxes\":[");
            for (int i = 0; i < boxes.Length; i++)
            {
                if (i > 0) b.Append(',');
                b.Append('{');
                Add(b, "source", boxes[i].Source); b.Append(',');
                Add(b, "boxNumber", boxes[i].BoxNumber); b.Append(',');
                Add(b, "cowNumber", boxes[i].CowNumber); b.Append(',');
                Add(b, "attachmentStatus", boxes[i].AttachmentStatus); b.Append(',');
                Add(b, "operationStatus", boxes[i].OperationStatus); b.Append(',');
                Add(b, "boxStatus", boxes[i].BoxStatus); b.Append(',');
                Add(b, "expectedMilkYield", boxes[i].ExpectedMilkYield); b.Append(',');
                Add(b, "milkYield", boxes[i].MilkYield);
                b.Append('}');
            }
            b.Append(']');
        }

        private MilkingRobotBoxInfo[] FetchBoxInfos()
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://localhost:8080/RdmDataService.svc/BoxInfos");
                request.Timeout = 1500; request.ReadWriteTimeout = 1500; request.Accept = "application/atom+xml,application/xml,text/xml";
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                {
                    XmlDocument document = new XmlDocument(); document.Load(stream);
                    List<MilkingRobotBoxInfo> boxes = new List<MilkingRobotBoxInfo>();
                    XmlNodeList entries = document.GetElementsByTagName("entry");
                    for (int i = 0; i < entries.Count; i++)
                    {
                        MilkingRobotBoxInfo box = ReadBoxInfo(entries[i]);
                        if (box.BoxNumber.Length > 0) boxes.Add(box);
                    }
                    if (boxes.Count == 0)
                    {
                        XmlNodeList properties = document.GetElementsByTagName("properties", "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata");
                        for (int i = 0; i < properties.Count; i++)
                        {
                            MilkingRobotBoxInfo box = ReadBoxInfo(properties[i]);
                            if (box.BoxNumber.Length > 0) boxes.Add(box);
                        }
                    }
                    return boxes.ToArray();
                }
            }
            catch { return new MilkingRobotBoxInfo[0]; }
        }

        private MilkingRobotBoxInfo ReadBoxInfo(XmlNode node)
        {
            string operationStatus = ChildTextAny(node, "OperationStatus", "operationStatus", "OperationState", "operationState");
            string operationStatusText = ResolveOperationStatusText(operationStatus, ChildTextAny(node, "OperationStatusText", "operationStatusText", "OperationText", "operationText"));
            string boxStatus = ChildTextAny(node, "BoxStatus", "boxStatus");
            string boxStatusText = ResolveBoxStatusText(boxStatus, ChildTextAny(node, "BoxStatusText", "boxStatusText"));
            if (boxStatusText.Length == 0) boxStatusText = ResolveBoxStatusText(boxStatus, ChildTextAny(node, "BoxStatus", "boxStatus"));
            return new MilkingRobotBoxInfo
            {
                Source = "RdmDataService/BoxInfos",
                BoxNumber = ChildText(node, "BoxNumber"),
                CowNumber = ChildText(node, "CowNumber"),
                AttachmentStatus = ChildText(node, "AttachmentStatus"),
                OperationStatus = operationStatus,
                OperationStatusText = operationStatusText,
                BoxStatus = boxStatus,
                BoxStatusText = boxStatusText,
                ExpectedMilkYield = ChildText(node, "ExpectedMilkYield"),
                MilkYield = ChildText(node, "MilkYield")
            };
        }

        private static string ChildTextAny(XmlNode node, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string value = ChildText(node, names[i]);
                if (!String.IsNullOrEmpty(value)) return value;
            }
            return "";
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

        private string ReadTextCached(string path, int maximumCharacters, FileCache<string> cache)
        {
            lock (snapshotLock)
            {
                return LoadCached(path, cache, delegate { return ReadText(path, maximumCharacters); }, "");
            }
        }

        private Dictionary<string, string> ReadPropertiesCached(string path, FileCache<Dictionary<string, string>> cache)
        {
            lock (snapshotLock)
            {
                return LoadCached(path, cache, delegate { return PropertiesFile.Read(path); }, new Dictionary<string, string>());
            }
        }

        private T LoadCached<T>(string path, FileCache<T> cache, Func<T> loader, T missingValue)
        {
            DateTime stamp = GetFileStamp(path);
            if (!String.Equals(cache.Path, path, StringComparison.OrdinalIgnoreCase) || cache.StampUtc != stamp || object.Equals(cache.Value, null))
            {
                cache.Path = path;
                cache.StampUtc = stamp;
                cache.Value = stamp == DateTime.MinValue ? missingValue : loader();
            }
            return cache.Value;
        }

        private static DateTime GetFileStamp(string path)
        {
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
        }

        private Dictionary<string, string> LoadTranslations()
        {
            string path = settings == null ? "" : settings.TranslationPath;
            if (String.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            DateTime stamp = File.GetLastWriteTimeUtc(path);
            lock (translationsLock)
            {
                if (translations == null || !String.Equals(path, translationsPath, StringComparison.OrdinalIgnoreCase) || stamp != translationsStampUtc)
                {
                    translations = PropertiesFile.Read(path);
                    translationsPath = path;
                    translationsStampUtc = stamp;
                }
                return translations;
            }
        }

        private string Translate(string key)
        {
            if (String.IsNullOrEmpty(key)) return "";
            Dictionary<string, string> values = LoadTranslations();
            string value;
            return values.TryGetValue(key, out value) ? value : "";
        }

        private string ResolveOperationStatusText(string rawValue, string existingText)
        {
            string text = Trim(existingText);
            if (text.Length > 0) return text;

            string raw = Trim(rawValue);
            if (raw.Length == 0) return "";

            string translated = Translate("Text.Enum.BoxOperationState." + ResolveOperationStateKey(raw));
            if (translated.Length > 0) return translated;

            return raw;
        }

        private string ResolveBoxStatusText(string rawValue, string existingText)
        {
            string text = Trim(existingText);
            if (text.Length > 0) return text;

            string raw = Trim(rawValue);
            if (raw.Length == 0) return "";

            string translated = Translate("Text.Enum.BoxStatus." + ResolveBoxStatusKey(raw));
            if (translated.Length > 0) return translated;

            return raw;
        }

        private static string ResolveOperationStateKey(string rawValue)
        {
            int code;
            if (Int32.TryParse(rawValue, out code))
            {
                switch (code)
                {
                    case 0: return "Offline";
                    case 1: return "Initialize";
                    case 2: return "Idle";
                    case 3: return "Manual";
                    case 4: return "Automatic";
                    case 5: return "Emergency";
                    case 6: return "EmergencyStop";
                    case 7: return "Error";
                }
            }

            switch (NormalizeToken(rawValue))
            {
                case "OFFLINE": return "Offline";
                case "INIT":
                case "INITIALIZE":
                case "INITIALISING":
                case "INITIALIZING": return "Initialize";
                case "IDLE": return "Idle";
                case "MANUAL": return "Manual";
                case "AUTOMATIC": return "Automatic";
                case "EMERGENCY": return "Emergency";
                case "EMERGENCYSTOP": return "EmergencyStop";
                case "ERROR": return "Error";
            }

            return rawValue;
        }

        private static string ResolveBoxStatusKey(string rawValue)
        {
            int code;
            if (Int32.TryParse(rawValue, out code))
            {
                switch (code)
                {
                    case 0: return "OOO";
                    case 1: return "Initializing";
                    case 2: return "Blocked";
                    case 3: return "RequestEntranceGate";
                    case 4: return "OpenEntranceGate";
                    case 5: return "WaitForCow";
                    case 6: return "CowIdentified";
                    case 7: return "ClaimRobot";
                    case 8: return "WaitForRobot";
                    case 9: return "TakeCluster";
                    case 10: return "StartAttachment";
                    case 11: return "ManualAttachment";
                    case 12: return "FinishAttachment";
                    case 13: return "Milking";
                    case 14: return "DetachCluster";
                    case 15: return "ReleaseCow";
                    case 16: return "ShortClean";
                    case 17: return "Cleaning";
                    case 18: return "Disabled";
                    case 19: return "Unknown";
                }
            }

            switch (NormalizeToken(rawValue))
            {
                case "OOO": return "OOO";
                case "INITIALISING":
                case "INITIALIZING":
                case "INIT": return "Initializing";
                case "BLOCKED": return "Blocked";
                case "REQUESTENTRANCEGATE": return "RequestEntranceGate";
                case "OPENENTRANCEGATE": return "OpenEntranceGate";
                case "WAITFORCOW": return "WaitForCow";
                case "COWIDENTIFIED": return "CowIdentified";
                case "CLAIMROBOT": return "ClaimRobot";
                case "WAITFORROBOT": return "WaitForRobot";
                case "TAKECLUSTER": return "TakeCluster";
                case "STARTATTACHMENT": return "StartAttachment";
                case "MANUALATTACHMENT": return "ManualAttachment";
                case "FINISHATTACHMENT": return "FinishAttachment";
                case "MILKING": return "Milking";
                case "DEATACHCLUSTER":
                case "DETACHCLUSTER": return "DetachCluster";
                case "RELEASECOW": return "ReleaseCow";
                case "SHORTCLEAN":
                case "SHORTCLEANING": return "ShortClean";
                case "CLEANING": return "Cleaning";
                case "DISABLED": return "Disabled";
                case "UNKNOWN": return "Unknown";
            }

            return rawValue;
        }

        private static string NormalizeToken(string value)
        {
            if (String.IsNullOrEmpty(value)) return "";
            StringBuilder b = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (Char.IsLetterOrDigit(c)) b.Append(Char.ToUpperInvariant(c));
            }
            return b.ToString();
        }

        private static string Trim(string value)
        {
            return String.IsNullOrEmpty(value) ? "" : value.Trim();
        }

        private static string ReadText(string path, int maximumCharacters)
        {
            try
            {
                if (!File.Exists(path)) return "";
                string value = File.ReadAllText(path, Encoding.Default);
                return value.Length <= maximumCharacters ? value : value.Substring(0, maximumCharacters);
            }
            catch { return ""; }
        }

        private static void AddObject(StringBuilder b, string name, Dictionary<string, string> values)
        {
            AddName(b, name); b.Append('{'); bool first = true;
            foreach (KeyValuePair<string, string> pair in values)
            {
                if (!first) b.Append(',');
                first = false; Add(b, pair.Key, pair.Value);
            }
            b.Append('}');
        }

        private static void Add(StringBuilder b, string name, string value) { AddName(b, name); AddValue(b, value); }
        private static void AddName(StringBuilder b, string name) { AddValue(b, name); b.Append(':'); }
        private static void AddValue(StringBuilder b, string value)
        {
            b.Append('"'); string text = value ?? "";
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"' || c == '\\') b.Append('\\').Append(c);
                else if (c == '\r') b.Append("\\r");
                else if (c == '\n') b.Append("\\n");
                else if (c == '\t') b.Append("\\t");
                else if (c < 32) b.Append(' ');
                else b.Append(c);
            }
            b.Append('"');
        }

        private static bool AllFound(MilkingRobotFileCheck[] checks)
        {
            for (int i = 0; i < checks.Length; i++) if (!checks[i].Exists) return false;
            return true;
        }

        private static string NormalizeRoot(string path)
        {
            return String.IsNullOrEmpty(path) ? @"D:\DairyPln" : path.Trim().TrimEnd('\\', '/');
        }

        private sealed class FileCache<T>
        {
            public string Path = "";
            public DateTime StampUtc = DateTime.MinValue;
            public T Value;
        }
    }

    public sealed class MilkingRobotFileCheck
    {
        public string Name { get; private set; }
        public string Path { get; private set; }
        public bool Exists { get; private set; }
        public MilkingRobotFileCheck(string name, string path, bool exists) { Name = name; Path = path; Exists = exists; }
    }

    public sealed class MilkingRobotBoxInfo
    {
        public string Source;
        public string BoxNumber;
        public string CowNumber;
        public string AttachmentStatus;
        public string OperationStatus;
        public string OperationStatusText;
        public string BoxStatus;
        public string BoxStatusText;
        public string ExpectedMilkYield;
        public string MilkYield;
    }
}
