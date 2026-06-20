using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
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

                StartDpProcessControlIfNeeded(root);
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
                    if (process.ExitCode == 0) return new DairyPlanCommandResult(true, "forwardedToDairyPlan", text.Length == 0 ? "Befehl wurde an DPProcessControl uebergeben." : text);
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

        private static void StartDpProcessControlIfNeeded(string root)
        {
            try
            {
                if (Process.GetProcessesByName("DPProcessControl").Length > 0) return;
                string open = Path.Combine(root, "open.exe");
                string exe = Path.Combine(root, "DPProcessControl.exe");
                ProcessStartInfo start = new ProcessStartInfo();
                start.WorkingDirectory = root; start.UseShellExecute = false; start.CreateNoWindow = true;
                if (File.Exists(open)) { start.FileName = open; start.Arguments = "DPProcessControl.exe"; }
                else { start.FileName = exe; start.Arguments = ""; }
                Process.Start(start);
                System.Threading.Thread.Sleep(3000);
            }
            catch (Exception ex)
            {
                ErrorLogger.Log("DPProcessControl-Start", ex);
            }
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
