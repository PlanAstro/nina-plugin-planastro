using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlanAstro.NINA {

    public enum ArrivalAction {
        /// <summary>Keep it in the inbox only.</summary>
        InboxOnly,
        /// <summary>Open new arrivals in the Framing Assistant.</summary>
        OpenInFraming
    }

    /// <summary>
    /// Plugin settings, kept in %LOCALAPPDATA%\NINA\PlanAstro\settings.json.
    /// The device token is encrypted with Windows DPAPI for the current Windows user and never stored in plain text.
    /// </summary>
    public class PluginSettings {
        public string BaseUrl { get; set; } = "https://planastro.com";
        public string DeviceName { get; set; } = Environment.MachineName;
        public string Account { get; set; }
        public string EncryptedToken { get; set; }
        public int PollSeconds { get; set; } = 60;
        public ArrivalAction OnArrival { get; set; } = ArrivalAction.OpenInFraming;
        public List<SentTarget> Inbox { get; set; } = new();
    }

    public static class SettingsStore {
        private static readonly string Folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NINA", "PlanAstro");
        private static readonly string FilePath = Path.Combine(Folder, "settings.json");
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("PlanAstro.NINA.v1");

        private static readonly JsonSerializerOptions Json = new() {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static PluginSettings Load() {
            try {
                if (File.Exists(FilePath)) {
                    return JsonSerializer.Deserialize<PluginSettings>(File.ReadAllText(FilePath), Json) ?? new PluginSettings();
                }
            } catch (Exception ex) {
                Log.Error("Could not read PlanAstro settings; starting fresh", ex);
            }
            return new PluginSettings();
        }

        public static void Save(PluginSettings settings) {
            try {
                Directory.CreateDirectory(Folder);
                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(settings, Json));
                File.Move(tmp, FilePath, true);
            } catch (Exception ex) {
                Log.Error("Could not save PlanAstro settings", ex);
            }
        }

        public static string Protect(string token) {
            if (string.IsNullOrEmpty(token)) return null;
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(bytes);
        }

        public static string Unprotect(string encrypted) {
            if (string.IsNullOrEmpty(encrypted)) return null;
            try {
                var bytes = ProtectedData.Unprotect(Convert.FromBase64String(encrypted), Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            } catch (Exception ex) {
                Log.Error("Could not decrypt the PlanAstro device token; pair again", ex);
                return null;
            }
        }
    }

    internal static class Log {
        public static void Info(string message) => global::NINA.Core.Utility.Logger.Info($"[PlanAstro] {message}");
        public static void Error(string message, Exception ex) => global::NINA.Core.Utility.Logger.Error($"[PlanAstro] {message}: {ex.Message}");
    }
}
