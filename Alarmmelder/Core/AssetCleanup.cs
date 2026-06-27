using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace MioneAlarmmelder.Core
{
    public static class AssetCleanup
    {
        private static readonly string[] CanonicalAssets = new string[]
        {
            "MioneDairyPlanBridge.jar",
            "translations_de.properties",
            "Mione_AlarmCodes_UK_DE.xlsx"
        };

        public static void CleanDuplicateAssets()
        {
            try
            {
                string assetsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
                if (!Directory.Exists(assetsDir)) return;
                for (int i = 0; i < CanonicalAssets.Length; i++) NormalizeAsset(assetsDir, CanonicalAssets[i]);
            }
            catch (Exception ex)
            {
                ErrorLogger.Log("Asset-Bereinigung", ex);
            }
        }

        private static void NormalizeAsset(string assetsDir, string canonicalName)
        {
            string canonicalPath = Path.Combine(assetsDir, canonicalName);
            string baseName = Path.GetFileNameWithoutExtension(canonicalName);
            string extension = Path.GetExtension(canonicalName);
            Regex duplicatePattern = new Regex("^" + Regex.Escape(baseName) + "(?:\\s+\\d+)?" + Regex.Escape(extension) + "$", RegexOptions.IgnoreCase);

            List<string> candidates = new List<string>();
            string[] files = Directory.GetFiles(assetsDir, "*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                string fileName = Path.GetFileName(files[i]);
                if (duplicatePattern.IsMatch(fileName)) candidates.Add(files[i]);
            }

            if (candidates.Count == 0) return;

            string winner = PickNewest(candidates);
            if (String.IsNullOrEmpty(winner)) return;

            if (!String.Equals(winner, canonicalPath, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(winner, canonicalPath, true);
                try { File.SetLastWriteTimeUtc(canonicalPath, File.GetLastWriteTimeUtc(winner)); } catch { }
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                string candidate = candidates[i];
                if (String.Equals(candidate, canonicalPath, StringComparison.OrdinalIgnoreCase)) continue;
                try { File.Delete(candidate); } catch { }
            }
        }

        private static string PickNewest(List<string> candidates)
        {
            string winner = "";
            DateTime winnerTime = DateTime.MinValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                string candidate = candidates[i];
                DateTime writeTime;
                try { writeTime = File.GetLastWriteTimeUtc(candidate); }
                catch { writeTime = DateTime.MinValue; }
                if (String.IsNullOrEmpty(winner) || writeTime > winnerTime || (writeTime == winnerTime && candidate.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)))
                {
                    winner = candidate;
                    winnerTime = writeTime;
                }
            }
            return winner;
        }
    }
}
