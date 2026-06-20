using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace MioneAlarmmelder.Core
{
    public static class GitHubUpdateService
    {
        public static void CheckAsync(AppSettings settings, Action<UpdateCheckResult> completed)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                UpdateCheckResult result;
                try { result = Check(settings); }
                catch (Exception ex) { result = UpdateCheckResult.Failed(ex.Message); }
                completed(result);
            });
        }

        public static void DownloadAndInstallAsync(UpdateCheckResult update, Action<string> completed)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    EnableTls12();
                    string extension = update.IsZip ? ".zip" : ".exe";
                    string download = Path.Combine(Path.GetTempPath(), "MioneAlarmmelder-" + Guid.NewGuid().ToString("N") + extension);
                    using (WebClient client = new WebClient())
                    {
                        client.Headers.Add("User-Agent", "MioneAlarmmelder-Updater"); client.DownloadFile(update.DownloadUrl, download);
                    }
                    VerifyDigest(download, update.Digest);
                    if (update.IsZip)
                    {
                        string package = Path.Combine(Path.GetTempPath(), "MioneAlarmmelder-Paket-" + Guid.NewGuid().ToString("N"));
                        ExtractZip(download, package);
                        string packageRoot = FindPackageRoot(package);
                        if (String.IsNullOrEmpty(packageRoot)) throw new FileNotFoundException("Im Update-Paket wurde keine MioneAlarmmelder.exe gefunden.");
                        StartPackageReplacement(packageRoot, download);
                    }
                    else StartReplacement(download);
                    completed("");
                }
                catch (Exception ex) { completed(ex.Message); }
            });
        }

        public static Version CurrentVersion { get { return Assembly.GetExecutingAssembly().GetName().Version; } }

        private static UpdateCheckResult Check(AppSettings settings)
        {
            if (String.IsNullOrEmpty(settings.UpdateRepository) || settings.UpdateRepository.IndexOf('/') < 1)
                throw new InvalidOperationException("Bitte ein GitHub-Repository im Format Besitzer/Repository eintragen.");
            EnableTls12();
            string endpoint = "https://api.github.com/repos/" + settings.UpdateRepository.Trim().Trim('/') + "/releases/latest";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(endpoint);
            request.UserAgent = "MioneAlarmmelder-Updater"; request.Accept = "application/vnd.github+json"; request.Timeout = 15000;
            string json;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8)) json = reader.ReadToEnd();
            Dictionary<string, object> release = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
            if (release == null) throw new InvalidDataException("Ungültige Antwort von GitHub.");
            string tag = Value(release, "tag_name"); Version version = ParseVersion(tag);
            UpdateCheckResult result = new UpdateCheckResult(); result.TagName = tag; result.LatestVersion = version;
            result.HasUpdate = version.CompareTo(CurrentVersion) > 0;
            if (!result.HasUpdate) return result;
            object assetsValue; object[] assets = release.TryGetValue("assets", out assetsValue) ? assetsValue as object[] : null;
            if (assets == null) throw new InvalidDataException("Das GitHub-Release enthält keine Assets.");
            Dictionary<string, object> selected = SelectAsset(assets, settings.UpdateAssetName);
            if (selected == null) throw new FileNotFoundException("Release-Asset nicht gefunden: " + settings.UpdateAssetName);
            result.AssetName = Value(selected, "name"); result.DownloadUrl = Value(selected, "browser_download_url"); result.Digest = Value(selected, "digest");
            result.IsZip = result.AssetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
            return result;
        }

        private static Version ParseVersion(string tag)
        {
            string value = (tag ?? "").Trim(); if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase)) value = value.Substring(1);
            int dash = value.IndexOf('-'); if (dash >= 0) value = value.Substring(0, dash);
            try { return new Version(value); }
            catch (Exception) { throw new InvalidDataException("Ungültige Release-Version: " + tag); }
        }
        private static string Value(Dictionary<string, object> values, string key) { object value; return values.TryGetValue(key, out value) && value != null ? value.ToString() : ""; }
        private static void EnableTls12() { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; }
        private static Dictionary<string, object> SelectAsset(object[] assets, string configuredName)
        {
            Dictionary<string, object> fallbackZip = null; Dictionary<string, object> fallbackExe = null;
            for (int i = 0; i < assets.Length; i++)
            {
                Dictionary<string, object> asset = assets[i] as Dictionary<string, object>; if (asset == null) continue;
                string name = Value(asset, "name");
                if (MatchesAssetName(name, configuredName)) return asset;
                if (fallbackZip == null && name.StartsWith("MioneAlarmmelder-", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) fallbackZip = asset;
                if (fallbackExe == null && String.Equals(name, "MioneAlarmmelder.exe", StringComparison.OrdinalIgnoreCase)) fallbackExe = asset;
            }
            return fallbackZip ?? fallbackExe;
        }
        private static bool MatchesAssetName(string name, string pattern)
        {
            if (String.IsNullOrEmpty(pattern)) return false;
            if (pattern.IndexOf('*') < 0) return String.Equals(name, pattern, StringComparison.OrdinalIgnoreCase);
            string[] parts = pattern.Split('*'); int position = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0) continue;
                int index = name.IndexOf(parts[i], position, StringComparison.OrdinalIgnoreCase);
                if (index < 0 || (i == 0 && index != 0)) return false;
                position = index + parts[i].Length;
            }
            string last = parts[parts.Length - 1];
            return last.Length == 0 || name.EndsWith(last, StringComparison.OrdinalIgnoreCase);
        }
        private static void VerifyDigest(string file, string digest)
        {
            if (String.IsNullOrEmpty(digest) || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) return;
            byte[] hash; using (FileStream stream = File.OpenRead(file)) using (SHA256 sha = SHA256.Create()) hash = sha.ComputeHash(stream);
            StringBuilder actual = new StringBuilder(); for (int i = 0; i < hash.Length; i++) actual.Append(hash[i].ToString("x2"));
            if (!String.Equals(actual.ToString(), digest.Substring(7), StringComparison.OrdinalIgnoreCase)) { File.Delete(file); throw new InvalidDataException("Die SHA-256-Prüfsumme des Updates ist ungültig."); }
        }
        private static void StartReplacement(string download)
        {
            string target = Application.ExecutablePath;
            string script = Path.Combine(Path.GetTempPath(), "MioneAlarmmelder-Update-" + Guid.NewGuid().ToString("N") + ".cmd");
            using (StreamWriter writer = new StreamWriter(script, false, Encoding.Default))
            {
                writer.WriteLine("@echo off"); writer.WriteLine("ping 127.0.0.1 -n 4 >nul");
                writer.WriteLine("copy /Y \"" + download + "\" \"" + target + "\" >nul");
                writer.WriteLine("if errorlevel 1 (pause & exit /b 1)"); writer.WriteLine("del /Q \"" + download + "\"");
                writer.WriteLine("start \"\" \"" + target + "\""); writer.WriteLine("del /Q \"%~f0\"");
            }
            ProcessStartInfo info = new ProcessStartInfo(script); info.UseShellExecute = true; info.WindowStyle = ProcessWindowStyle.Hidden; Process.Start(info);
        }
        private static void ExtractZip(string zipFile, string targetFolder)
        {
            Directory.CreateDirectory(targetFolder);
            Type shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null) throw new NotSupportedException("ZIP-Updates benötigen die Windows-ZIP-Unterstützung.");
            object shell = Activator.CreateInstance(shellType);
            object source = Invoke(shell, "NameSpace", zipFile);
            object target = Invoke(shell, "NameSpace", targetFolder);
            if (source == null || target == null) throw new InvalidDataException("Das Update-ZIP konnte nicht geöffnet werden.");
            object items = Invoke(source, "Items");
            Invoke(target, "CopyHere", items, 16);
            DateTime timeout = DateTime.Now.AddSeconds(30);
            while (DateTime.Now < timeout)
            {
                if (!String.IsNullOrEmpty(FindPackageRoot(targetFolder))) return;
                Thread.Sleep(500);
            }
            throw new TimeoutException("Das Update-ZIP konnte nicht rechtzeitig entpackt werden.");
        }
        private static object Invoke(object target, string method, params object[] args)
        {
            return target.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, target, args);
        }
        private static string FindPackageRoot(string folder)
        {
            string direct = Path.Combine(folder, "MioneAlarmmelder.exe"); if (File.Exists(direct)) return folder;
            string[] files = Directory.GetFiles(folder, "MioneAlarmmelder.exe", SearchOption.AllDirectories);
            return files.Length == 0 ? "" : Path.GetDirectoryName(files[0]);
        }
        private static void StartPackageReplacement(string packageFolder, string download)
        {
            string target = Application.ExecutablePath; string targetFolder = Path.GetDirectoryName(target);
            string script = Path.Combine(Path.GetTempPath(), "MioneAlarmmelder-Update-" + Guid.NewGuid().ToString("N") + ".cmd");
            using (StreamWriter writer = new StreamWriter(script, false, Encoding.Default))
            {
                writer.WriteLine("@echo off"); writer.WriteLine("ping 127.0.0.1 -n 4 >nul");
                writer.WriteLine("xcopy /E /I /Y \"" + packageFolder + "\\*\" \"" + targetFolder + "\\\" >nul");
                writer.WriteLine("if errorlevel 1 (pause & exit /b 1)");
                writer.WriteLine("del /Q \"" + download + "\"");
                writer.WriteLine("rmdir /S /Q \"" + packageFolder + "\"");
                writer.WriteLine("start \"\" \"" + target + "\""); writer.WriteLine("del /Q \"%~f0\"");
            }
            ProcessStartInfo info = new ProcessStartInfo(script); info.UseShellExecute = true; info.WindowStyle = ProcessWindowStyle.Hidden; Process.Start(info);
        }
    }

    public sealed class UpdateCheckResult
    {
        public bool HasUpdate { get; set; } public Version LatestVersion { get; set; } public string TagName { get; set; }
        public string DownloadUrl { get; set; } public string Digest { get; set; } public string AssetName { get; set; } public bool IsZip { get; set; } public string Error { get; set; }
        public static UpdateCheckResult Failed(string error) { UpdateCheckResult result = new UpdateCheckResult(); result.Error = error; return result; }
    }
}
