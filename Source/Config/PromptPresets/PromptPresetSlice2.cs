using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Persistence;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Serialization;
using Ustas.RimAI.Core.Storage;

namespace Ustas.RimAI.Communication.Relations.Config
{
internal sealed class PromptPresetSlice2 : PromptPresetServiceCollaborator
    {
        internal PromptPresetSlice2(PromptPresetService owner) : base(owner)
        {
        }

internal static void NormalizeStore(RelationsSettings settings, PromptPresetStoreConfig store, bool persistWhenEmpty = true)
        {
            store.SchemaVersion = CurrentSchemaVersion;
            store.Presets ??= new List<PromptPresetConfig>();
            for (int i = 0; i < store.Presets.Count; i++)
            {
                PromptPresetService.NormalizePreset(store.Presets[i]);
                if (!PromptPresetService.IsImmutableDefaultId(store.Presets[i].Id) &&
                    !PromptPresetService.HasMeaningfulPayload(store.Presets[i].ChannelPayloads))
                {
                    store.Presets[i].ChannelPayloads = PromptPresetService.CaptureCurrentPayload(settings);
                    store.Presets[i].UpdatedAtUtc = DateTime.UtcNow.ToString("o");
                }

                if (!PromptPresetService.IsImmutableDefaultId(store.Presets[i].Id))
                {
                    PromptPresetService.ApplyLegacyRpgNodeMigrationIfNeeded(store.Presets[i]);
                    PromptPresetService.ApplyLegacySocialNewsNodeMigrationIfNeeded(store.Presets[i]);
                }
            }

            PromptPresetService.EnforceImmutableDefaultPreset(store);

            if (store.Presets.Count == 0)
            {
                PromptPresetService factory = new PromptPresetService();
                PromptPresetConfig canonical = PromptPresetService.CreateCanonicalDefaultPreset(ImmutableDefaultPresetName);
                canonical.IsActive = true;
                store.Presets.Add(canonical);
                store.DefaultPresetId = canonical.Id;
                PromptPresetChannelPayloads legacyPayload = PromptPresetService.CaptureCurrentPayload(settings);
                if (PromptPresetService.ShouldCreateMigratedPreset(legacyPayload, canonical.ChannelPayloads))
                {
                    PromptPresetConfig migrated = PromptPresetService.BuildPresetShell("Migrated");
                    migrated.ChannelPayloads = legacyPayload;
                    store.Presets.Add(migrated);
                }

                store.ActivePresetId = canonical.Id;
                if (persistWhenEmpty)
                {
                    factory.SaveAll(store);
                }
                return;
            }

            PromptPresetConfig active = store.Presets.FirstOrDefault(p => string.Equals(p.Id, store.ActivePresetId, StringComparison.Ordinal))
                                      ?? store.Presets.FirstOrDefault(p => p.IsActive)
                                      ?? store.Presets[0];
            store.ActivePresetId = active.Id;
            if (string.IsNullOrWhiteSpace(store.DefaultPresetId) ||
                !store.Presets.Any(p => string.Equals(p.Id, store.DefaultPresetId, StringComparison.Ordinal)))
            {
                store.DefaultPresetId = PromptPresetService.ResolveDefaultPresetId(store.Presets);
            }
            store.DefaultPresetId = ImmutableDefaultPresetId;

            for (int i = 0; i < store.Presets.Count; i++)
            {
                store.Presets[i].IsActive = string.Equals(store.Presets[i].Id, active.Id, StringComparison.Ordinal);
            }
        }

internal static void NormalizePreset(PromptPresetConfig preset)
        {
            if (preset == null)
            {
                return;
            }

            preset.Id = string.IsNullOrWhiteSpace(preset.Id) ? Guid.NewGuid().ToString("N") : preset.Id.Trim();
            preset.Name = string.IsNullOrWhiteSpace(preset.Name) ? "Preset" : preset.Name.Trim();
            preset.ChannelPayloads ??= new PromptPresetChannelPayloads();
            PromptPresetService.NormalizePayload(preset.ChannelPayloads);

            if (string.IsNullOrWhiteSpace(preset.CreatedAtUtc))
            {
                preset.CreatedAtUtc = DateTime.UtcNow.ToString("o");
            }

            if (string.IsNullOrWhiteSpace(preset.UpdatedAtUtc))
            {
                preset.UpdatedAtUtc = preset.CreatedAtUtc;
            }
        }

internal static PromptPresetStoreConfig ReadStoreFile(out bool readCorrupted)
        {
            readCorrupted = false;
            PromptPresetService.TryMigrateLegacyStoreToConfigPath();
            string primaryPath = PromptPresetService.GetStorePath();
            string legacyPath = PromptPresetService.GetLegacyStorePath();

            bool primaryCorrupted;
            PromptPresetStoreConfig primaryStore = PromptPresetService.TryReadStoreFile(primaryPath, out primaryCorrupted);
            if (primaryCorrupted)
            {
                readCorrupted = true;
            }

            bool hasLegacy = !string.IsNullOrWhiteSpace(legacyPath) &&
                             !string.Equals(primaryPath, legacyPath, StringComparison.OrdinalIgnoreCase) &&
                             LocalStorage.Current.FileExists(legacyPath);
            bool legacyCorrupted = false;
            PromptPresetStoreConfig legacyStore = null;
            if (hasLegacy)
            {
                legacyStore = PromptPresetService.TryReadStoreFile(legacyPath, out legacyCorrupted);
                if (legacyCorrupted)
                {
                    readCorrupted = true;
                }
            }

            PromptPresetStoreConfig chosen = PromptPresetService.ChooseRicherStore(primaryStore, legacyStore);
            if (chosen == null)
            {
                return null;
            }

            // If legacy has richer data than primary, self-heal primary for next launch.
            if (hasLegacy && ReferenceEquals(chosen, legacyStore))
            {
                try
                {
                    PromptPresetService.EnsureStoreDirectory();
                    PromptPresetService.SaveStoreToPath(primaryPath, chosen);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimAI.Relations] Failed to self-heal preset store from legacy path: {ex.Message}");
                }
            }

            return chosen;
        }

internal static PromptPresetStoreConfig TryReadStoreFile(string path, out bool corrupted)
        {
            corrupted = false;
            if (string.IsNullOrWhiteSpace(path) || !LocalStorage.Current.FileExists(path))
            {
                return null;
            }

            try
            {
                string json = LocalStorage.Current.ReadAllText(path);
                PromptPresetStoreConfig store = JsonUtility.FromJson<PromptPresetStoreConfig>(json);
                if (store == null)
                {
                    corrupted = true;
                    Log.Warning($"[RimAI.Relations] Prompt preset store JSON parsed to null. path={path}");
                    PromptPresetService.QuarantineCorruptedStoreFile(path, "json parsed to null");
                    return null;
                }

                if (json.IndexOf("\"Presets\"", StringComparison.Ordinal) < 0 &&
                    (json.IndexOf("\"ActivePresetId\"", StringComparison.Ordinal) >= 0 ||
                     json.IndexOf("\"DefaultPresetId\"", StringComparison.Ordinal) >= 0))
                {
                    Log.Warning($"[RimAI.Relations] Prompt preset store file has IDs but missing preset list. path={path}");
                }

                int rawPresetCountHint = PromptPresetService.CountPresetObjectsHint(json);
                int parsedPresetCount = store.Presets?.Count ?? 0;
                if (rawPresetCountHint > parsedPresetCount &&
                    PromptPresetService.TryRecoverPresetListFromRawJson(json, out List<PromptPresetConfig> recovered) &&
                    recovered.Count >= rawPresetCountHint)
                {
                    store.Presets = recovered;
                    Log.Warning(
                        $"[RimAI.Relations][PresetDiag] Recovered preset list from raw JSON. " +
                        $"path={path}, parsed={parsedPresetCount}, recovered={recovered.Count}");
                }

                PromptPresetService.ApplyLegacyPayloadsFromStoreJson(store, json);
                Log.Message($"[RimAI.Relations][PresetDiag] ReadStore success. path={path}, presets={store.Presets?.Count ?? 0}");
                return store;
            }
            catch (Exception ex)
            {
                corrupted = true;
                PromptPresetService.QuarantineCorruptedStoreFile(path, ex.Message);
                Log.Warning($"[RimAI.Relations] Failed to read prompt preset store: {ex.Message}. path={path}");
                return null;
            }
        }

internal static int CountPresetObjectsHint(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return 0;
            }

            return PromptPresetService.CountOccurrences(rawJson, "\"CreatedAtUtc\"");
        }

internal static int CountOccurrences(string text, string token)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(token))
            {
                return 0;
            }

            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }

            return count;
        }

internal static bool TryRecoverPresetListFromRawJson(string rawJson, out List<PromptPresetConfig> recovered)
        {
            recovered = new List<PromptPresetConfig>();
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return false;
            }

            int keyIndex = rawJson.IndexOf("\"Presets\"", StringComparison.Ordinal);
            if (keyIndex < 0)
            {
                return false;
            }

            int arrayStart = rawJson.IndexOf('[', keyIndex);
            if (arrayStart < 0)
            {
                return false;
            }

            if (!PromptPresetService.TryFindMatchingBracket(rawJson, arrayStart, '[', ']', out int arrayEnd))
            {
                return false;
            }

            int cursor = arrayStart + 1;
            while (cursor < arrayEnd)
            {
                int objStart = PromptPresetService.FindNextNonStringChar(rawJson, cursor, arrayEnd, '{');
                if (objStart < 0 || objStart >= arrayEnd)
                {
                    break;
                }

                if (!PromptPresetService.TryFindMatchingBracket(rawJson, objStart, '{', '}', out int objEnd))
                {
                    return false;
                }

                string objectJson = rawJson.Substring(objStart, objEnd - objStart + 1);
                PromptPresetConfig parsed = null;
                ReflectionJsonFieldDeserializer.TryDeserialize(objectJson, out parsed);

                if (parsed != null)
                {
                    recovered.Add(parsed);
                }

                cursor = objEnd + 1;
            }

            return recovered.Count > 0;
        }

internal static int FindNextNonStringChar(string text, int start, int endExclusive, char target)
        {
            bool inString = false;
            bool escaped = false;
            for (int i = Math.Max(0, start); i < endExclusive; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (c == '\\')
                    {
                        escaped = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == target)
                {
                    return i;
                }
            }

            return -1;
        }

internal static bool TryFindMatchingBracket(string text, int startIndex, char open, char close, out int endIndex)
        {
            endIndex = -1;
            if (string.IsNullOrEmpty(text) || startIndex < 0 || startIndex >= text.Length || text[startIndex] != open)
            {
                return false;
            }

            bool inString = false;
            bool escaped = false;
            int depth = 0;
            for (int i = startIndex; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (c == '\\')
                    {
                        escaped = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == open)
                {
                    depth++;
                    continue;
                }

                if (c == close)
                {
                    depth--;
                    if (depth == 0)
                    {
                        endIndex = i;
                        return true;
                    }
                }
            }

            return false;
        }

internal static PromptPresetStoreConfig ChooseRicherStore(
            PromptPresetStoreConfig primary,
            PromptPresetStoreConfig legacy)
        {
            if (primary == null)
            {
                return legacy;
            }

            if (legacy == null)
            {
                return primary;
            }

            int primaryCount = primary.Presets?.Count ?? 0;
            int legacyCount = legacy.Presets?.Count ?? 0;
            if (legacyCount > primaryCount)
            {
                return legacy;
            }

            if (primaryCount > legacyCount)
            {
                return primary;
            }

            return primary;
        }

internal static void QuarantineCorruptedStoreFile(string path, string reason)
        {
            if (string.IsNullOrWhiteSpace(path) || !LocalStorage.Current.FileExists(path))
            {
                return;
            }

            string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            string corruptPath = path + CorruptStoreFileSuffix + "." + timestamp;
            int suffix = 1;
            while (LocalStorage.Current.FileExists(corruptPath))
            {
                suffix++;
                corruptPath = path + CorruptStoreFileSuffix + "." + timestamp + "." + suffix;
            }

            try
            {
                LocalStorage.Current.MoveFile(path, corruptPath);
                Log.Error($"[RimAI.Relations] Quarantined corrupted preset store: {corruptPath}. reason={reason}");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Relations] Failed to quarantine corrupted preset store '{path}': {ex.Message}. reason={reason}");
            }
        }

internal static void EnsureStoreDirectory()
        {
            string path = PromptPresetService.GetStorePath();
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && !LocalStorage.Current.DirectoryExists(dir))
            {
                LocalStorage.Current.CreateDirectory(dir);
            }
        }
    }
}
