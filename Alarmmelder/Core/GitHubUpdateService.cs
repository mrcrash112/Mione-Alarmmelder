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
                    string download = Path.Combine(Path.GetTempPath(), "MioneAlarmmelder-" + Guid.NewGuid().ToString("N") + ".exe");
                    using (WebClient client = new WebClient())
                    {
                        client.Headers.Add("User-Agent", "MioneAlarmmelder-Updater"); client.DownloadFile(update.DownloadUrl, download);
                    }
                    VerifyDigest(download, update.Digest);
                    StartReplacement(download);
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
            for (int i = 0; i < assets.Length; i++)
            {
                Dictionary<string, object> asset = assets[i] as Dictionary<string, object>;
                if (asset != null && String.Equals(Value(asset, "name"), settings.UpdateAssetName, StringComparison.OrdinalIgnoreCase))
                { result.DownloadUrl = Value(asset, "browser_download_url"); result.Digest = Value(asset, "digest"); break; }
            }
            if (String.IsNullOrEmpty(result.DownloadUrl)) throw new FileNotFoundException("Release-Asset nicht gefunden: " + settings.UpdateAssetName);
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
    }

    public sealed class UpdateCheckResult
    {
        public bool HasUpdate { get; set; } public Version LatestVersion { get; set; } public string TagName { get; set; }
        public string DownloadUrl { get; set; } public string Digest { get; set; } public string Error { get; set; }
        public static UpdateCheckResult Failed(string error) { UpdateCheckResult result = new UpdateCheckResult(); result.Error = error; return result; }
    }
}
