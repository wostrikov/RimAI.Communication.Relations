using System;
using System.IO;
using System.Text;
using Verse;
using Ustas.RimAI.Core.Storage;

namespace Ustas.RimAI.Communication.Relations.Serialization
{
    /// <summary>
    /// Dependencies: System.IO, UTF-8 encoding, and RelationsJson.
    /// Responsibility: load, overlay, clone, and persist prompt-domain JSON payloads.
    /// </summary>
    internal static class PromptDomainJsonUtility
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        public static T LoadMerged<T>(string defaultPath, string customPath) where T : class, new()
        {
            T defaultPayload = LoadSingle<T>(defaultPath);
            if (!string.IsNullOrWhiteSpace(customPath) && LocalStorage.Current.FileExists(customPath))
            {
                T customPayload = LoadSingle<T>(customPath);
                if (customPayload != null)
                {
                    return customPayload;
                }
            }

            return defaultPayload ?? new T();
        }

        public static T LoadSingle<T>(string path) where T : class, new()
        {
            if (string.IsNullOrWhiteSpace(path) || !LocalStorage.Current.FileExists(path))
            {
                return new T();
            }

            try
            {
                string json = LocalStorage.Current.ReadAllText(path, Encoding.UTF8);
                return RelationsJson.FromJson<T>(json) ?? new T();
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Relations] Failed to load prompt-domain JSON from {path}: {ex.Message}");
                return new T();
            }
        }

        public static bool TryDeserialize<T>(string json, out T payload) where T : class, new()
        {
            payload = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                payload = RelationsJson.FromJson<T>(json) ?? new T();
                return true;
            }
            catch (FormatException)
            {
                payload = null;
                return false;
            }
        }

        public static string Serialize<T>(T payload, bool prettyPrint = true) where T : class
        {
            return RelationsJson.ToJson(payload, prettyPrint);
        }

        public static void WriteToFile<T>(string path, T payload, bool prettyPrint = true) where T : class
        {
            if (string.IsNullOrWhiteSpace(path) || payload == null)
            {
                return;
            }

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && !LocalStorage.Current.DirectoryExists(directory))
            {
                LocalStorage.Current.CreateDirectory(directory);
            }

            LocalStorage.Current.WriteAllText(path, Serialize(payload, prettyPrint), Utf8NoBom);
        }

        public static T Clone<T>(T payload) where T : class, new()
        {
            if (payload == null)
            {
                return new T();
            }

            return RelationsJson.FromJson<T>(RelationsJson.ToJson(payload)) ?? new T();
        }
    }
}
