using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Persistence;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Serialization;
using Ustas.RimAI.Core.Storage;
using Ustas.RimAI.Communication.Relations.Diagnostics;

namespace Ustas.RimAI.Communication.Relations.Config
{
    internal sealed class PromptPresetService : IPromptPresetService
    {
        internal PromptPresetServiceParts Parts;

        internal PromptPresetService()
        {
            Parts = new PromptPresetServiceParts(this);
        }

        [Serializable]
        internal sealed class LegacyPromptPresetStoreConfig
        {
            public List<LegacyPromptPresetConfig> Presets = new List<LegacyPromptPresetConfig>();
        }

        [Serializable]
        internal sealed class LegacyPromptPresetConfig
        {
            public LegacyPromptPresetChannelPayloads ChannelPayloads = new LegacyPromptPresetChannelPayloads();
        }

        [Serializable]
        internal sealed class LegacyPromptPresetChannelPayloads
        {
            public RimTalkPromptEntryDefaultsConfig PromptSectionCatalog = RimTalkPromptEntryDefaultsProvider.GetDefaultsSnapshot();
            public bool EnableRimTalkPromptCompat = true;
            public int RimTalkPresetInjectionMaxEntries = RelationsSettings.RimTalkPresetInjectionLimitUnlimited;
            public int RimTalkPresetInjectionMaxChars = RelationsSettings.RimTalkPresetInjectionLimitUnlimited;
            public string RimTalkCompatTemplate = string.Empty;
            public RimTalkChannelCompatConfig RimTalkDiplomacy = null;
            public RimTalkChannelCompatConfig RimTalkRpg = null;
        }

        internal const int CurrentSchemaVersion = 2;
        internal const int LegacyRpgNodeMigrationVersion = 2;
        internal const int LegacySocialNewsNodeMigrationVersion = 3;
        internal const string ImmutableDefaultPresetId = "rimchat_default_preset";
        internal const string ImmutableDefaultPresetName = "Default";
        internal const string PresetStoreFileName = "PromptPresets_Custom.json";
        internal const string CorruptStoreFileSuffix = ".corrupt";
        internal static readonly string ConfigStoreDirectory = Path.Combine(
            GenFilePaths.ConfigFolderPath,
            "Ustas.RimAI.Communication.Relations",
            PromptDomainFileCatalog.PromptFolderName,
            PromptDomainFileCatalog.CustomSubFolderName);

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        internal static string GetStorePath()
        {
            return Path.Combine(ConfigStoreDirectory, PresetStoreFileName);
        }

        internal static string GetLegacyStorePath()
        {
            return PromptDomainFileCatalog.GetCustomPath(PresetStoreFileName);
        }

        

        

        

        

        

        

        

        internal static bool IsImmutableDefaultId(string presetId)
        {
            return string.Equals(presetId, ImmutableDefaultPresetId, StringComparison.Ordinal);
        }

        

        

        

        

        

        

        

        

        

        

        

        

        

        internal static string NormalizeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        

        

        

        

        internal static string ReadDefaultOrEmpty(string fileName)
        {
            return ReadOrEmpty(PromptDomainFileCatalog.GetDefaultPath(fileName));
        }

        

        

        

        

        

        

        

        

        

        #region Facade forwards
        internal static string BuildTimestampPresetName(string prefix, DateTime nowLocal) => PromptPresetServiceDefaultPreset.BuildTimestampPresetName(prefix, nowLocal);
        internal static string ResolveDefaultPresetId(List<PromptPresetConfig> presets) => PromptPresetServiceDefaultPreset.ResolveDefaultPresetId(presets);
        internal static bool IsCanonicalDefaultCandidate(PromptPresetConfig preset, PromptPresetChannelPayloads canonicalPayload) => PromptPresetServiceDefaultPreset.IsCanonicalDefaultCandidate(preset, canonicalPayload);
        internal static PromptPresetConfig SelectEarliestPreset(List<PromptPresetConfig> candidates, List<PromptPresetConfig> allPresets) => PromptPresetServiceDefaultPreset.SelectEarliestPreset(candidates, allPresets);
        internal static DateTime ParseCreatedAtOrMax(string value) => PromptPresetServiceDefaultPreset.ParseCreatedAtOrMax(value);
        internal static int ResolvePresetIndex(List<PromptPresetConfig> presets, string presetId) => PromptPresetServiceDefaultPreset.ResolvePresetIndex(presets, presetId);
        #endregion
    
        #region Cluster forwards
        public PromptPresetStoreConfig LoadAll(RelationsSettings settings) => Parts.Slice1.LoadAll(settings);
        public void SaveAll(PromptPresetStoreConfig store) => Parts.Slice1.SaveAll(store);
        public PromptPresetConfig CreateFromLegacy(RelationsSettings settings, string name) => Parts.Slice1.CreateFromLegacy(settings, name);
        public PromptPresetConfig Duplicate(RelationsSettings settings, PromptPresetConfig source, string name) => Parts.Slice1.Duplicate(settings, source, name);
        public bool Activate(RelationsSettings settings, PromptPresetStoreConfig store, string presetId, out string error) => Parts.Slice1.Activate(settings, store, presetId, out error);
        public bool IsDefaultPreset(PromptPresetStoreConfig store, string presetId) => Parts.Slice1.IsDefaultPreset(store, presetId);
        public bool EnsureEditablePresetForMutation(RelationsSettings settings, PromptPresetStoreConfig store, string selectedPresetId, string forkNamePrefix, out PromptPresetConfig editablePreset, out bool forked, out string error) => Parts.Slice1.EnsureEditablePresetForMutation(settings, store, selectedPresetId, forkNamePrefix, out editablePreset, out forked, out error);
        public bool SyncPresetPayloadFromSettings(RelationsSettings settings, PromptPresetStoreConfig store, string presetId, out string error) => Parts.Slice1.SyncPresetPayloadFromSettings(settings, store, presetId, out error);
        public void ApplyPayloadToSettings(RelationsSettings settings, PromptPresetChannelPayloads payload, bool persistToFiles) => Parts.Slice1.ApplyPayloadToSettings(settings, payload, persistToFiles);
        public bool ExportPreset(string filePath, PromptPresetConfig preset, out string error) => Parts.Slice1.ExportPreset(filePath, preset, out error);
        public bool ImportPreset(string filePath, PromptPresetStoreConfig store, out PromptPresetConfig imported, out string error) => Parts.Slice1.ImportPreset(filePath, store, out imported, out error);
        internal static string NormalizePresetFilePath(string filePath) => PromptPresetSlice1.NormalizePresetFilePath(filePath);
        public List<PromptPresetSummary> BuildSummaries(PromptPresetStoreConfig store) => Parts.Slice1.BuildSummaries(store);
        internal static PromptPresetConfig BuildPresetShell(string name) => PromptPresetSlice1.BuildPresetShell(name);
        internal static void NormalizeStore(RelationsSettings settings, PromptPresetStoreConfig store, bool persistWhenEmpty = true) => PromptPresetSlice2.NormalizeStore(settings, store, persistWhenEmpty);
        internal static void NormalizePreset(PromptPresetConfig preset) => PromptPresetSlice2.NormalizePreset(preset);
        internal static PromptPresetStoreConfig ReadStoreFile(out bool readCorrupted) => PromptPresetSlice2.ReadStoreFile(out readCorrupted);
        internal static PromptPresetStoreConfig TryReadStoreFile(string path, out bool corrupted) => PromptPresetSlice2.TryReadStoreFile(path, out corrupted);
        internal static int CountPresetObjectsHint(string rawJson) => PromptPresetSlice2.CountPresetObjectsHint(rawJson);
        internal static int CountOccurrences(string text, string token) => PromptPresetSlice2.CountOccurrences(text, token);
        internal static bool TryRecoverPresetListFromRawJson(string rawJson, out List<PromptPresetConfig> recovered) => PromptPresetSlice2.TryRecoverPresetListFromRawJson(rawJson, out recovered);
        internal static int FindNextNonStringChar(string text, int start, int endExclusive, char target) => PromptPresetSlice2.FindNextNonStringChar(text, start, endExclusive, target);
        internal static bool TryFindMatchingBracket(string text, int startIndex, char open, char close, out int endIndex) => PromptPresetSlice2.TryFindMatchingBracket(text, startIndex, open, close, out endIndex);
        internal static PromptPresetStoreConfig ChooseRicherStore(PromptPresetStoreConfig primary, PromptPresetStoreConfig legacy) => PromptPresetSlice2.ChooseRicherStore(primary, legacy);
        internal static void QuarantineCorruptedStoreFile(string path, string reason) => PromptPresetSlice2.QuarantineCorruptedStoreFile(path, reason);
        internal static void EnsureStoreDirectory() => PromptPresetSlice2.EnsureStoreDirectory();
        internal static void AtomicWriteText(string path, string tempPath, string content) => PromptPresetSlice3.AtomicWriteText(path, tempPath, content);
        internal static void SaveStoreToPath(string path, PromptPresetStoreConfig store) => PromptPresetSlice3.SaveStoreToPath(path, store);
        internal static void MirrorStoreToLegacyPath(string primaryPath) => PromptPresetSlice3.MirrorStoreToLegacyPath(primaryPath);
        internal static void TryMigrateLegacyStoreToConfigPath() => PromptPresetSlice3.TryMigrateLegacyStoreToConfigPath();
        internal static PromptPresetChannelPayloads CaptureCurrentPayload(RelationsSettings settings) => PromptPresetSlice3.CaptureCurrentPayload(settings);
        internal static PromptPresetConfig CreateCanonicalDefaultPreset(string name) => PromptPresetSlice3.CreateCanonicalDefaultPreset(name);
        internal static void EnforceImmutableDefaultPreset(PromptPresetStoreConfig store) => PromptPresetSlice3.EnforceImmutableDefaultPreset(store);
        internal static PromptPresetChannelPayloads CreateCanonicalDefaultPayload() => PromptPresetSlice3.CreateCanonicalDefaultPayload();
        internal static void NormalizePayload(PromptPresetChannelPayloads payload) => PromptPresetSlice3.NormalizePayload(payload);
        internal static void ApplyLegacyRpgNodeMigrationIfNeeded(PromptPresetConfig preset) => PromptPresetSlice3.ApplyLegacyRpgNodeMigrationIfNeeded(preset);
        internal static void ApplyLegacySocialNewsNodeMigrationIfNeeded(PromptPresetConfig preset) => PromptPresetSlice3.ApplyLegacySocialNewsNodeMigrationIfNeeded(preset);
        internal static int TryOverrideLegacySocialNewsNode(PromptUnifiedCatalog catalog, PromptUnifiedCatalog authoritative, string nodeId, string legacyMarker, string missingMarker = null) => PromptPresetSlice3.TryOverrideLegacySocialNewsNode(catalog, authoritative, nodeId, legacyMarker, missingMarker);
        internal static bool ShouldCreateMigratedPreset(PromptPresetChannelPayloads legacyPayload, PromptPresetChannelPayloads canonicalPayload) => PromptPresetSlice3.ShouldCreateMigratedPreset(legacyPayload, canonicalPayload);
        internal static bool HasMeaningfulLegacyPayload(PromptPresetChannelPayloads payload) => PromptPresetSlice3.HasMeaningfulLegacyPayload(payload);
        internal static PromptUnifiedCatalog LoadCanonicalDefaultUnifiedCatalog() => PromptPresetSlice3.LoadCanonicalDefaultUnifiedCatalog();
        internal static bool ArePayloadsEquivalent(PromptPresetChannelPayloads left, PromptPresetChannelPayloads right) => PromptPresetSlice3.ArePayloadsEquivalent(left, right);
        internal static bool AreUnifiedCatalogsEquivalent(PromptUnifiedCatalog left, PromptUnifiedCatalog right) => PromptPresetSlice4.AreUnifiedCatalogsEquivalent(left, right);
        internal static bool AreChannelConfigsEquivalent(RimTalkChannelCompatConfig left, RimTalkChannelCompatConfig right) => PromptPresetSlice4.AreChannelConfigsEquivalent(left, right);
        internal static bool ArePromptEntriesEquivalent(RimTalkPromptEntryConfig left, RimTalkPromptEntryConfig right) => PromptPresetSlice4.ArePromptEntriesEquivalent(left, right);
        internal static void ApplyPayloadToCustomFiles(PromptPresetChannelPayloads payload) => PromptPresetSlice4.ApplyPayloadToCustomFiles(payload);
        internal static void PersistRpgPromptCustomStore(PromptPresetChannelPayloads payload) => PromptPresetSlice4.PersistRpgPromptCustomStore(payload);
        internal static void ApplyRimTalkCompatSettings(RelationsSettings settings, PromptPresetChannelPayloads payload, bool persistToFiles) => PromptPresetSlice4.ApplyRimTalkCompatSettings(settings, payload, persistToFiles);
        internal static string ReadOrEmpty(string path) => PromptPresetSlice4.ReadOrEmpty(path);
        internal static void WriteIfNotNull(string path, string payload) => PromptPresetSlice4.WriteIfNotNull(path, payload);
        internal static RpgPromptCustomConfig ParseRpgPromptCustomConfig(string json) => PromptPresetSlice4.ParseRpgPromptCustomConfig(json);
        internal static string EnsureUniqueName(List<PromptPresetConfig> presets, string name) => PromptPresetSlice4.EnsureUniqueName(presets, name);
        internal static void ApplyLegacyPayloadsFromStoreJson(PromptPresetStoreConfig store, string rawJson) => PromptPresetSlice4.ApplyLegacyPayloadsFromStoreJson(store, rawJson);
        internal static void ApplyLegacyPayloadFromJson(PromptPresetConfig preset, string rawJson, string sourceId) => PromptPresetSlice4.ApplyLegacyPayloadFromJson(preset, rawJson, sourceId);
        internal static bool ShouldApplyLegacyPayloadOverlay(string rawJson) => PromptPresetSlice4.ShouldApplyLegacyPayloadOverlay(rawJson);
        internal static void ApplyLegacyPayload(PromptPresetConfig preset, LegacyPromptPresetChannelPayloads legacyPayload, string sourceId) => PromptPresetSlice4.ApplyLegacyPayload(preset, legacyPayload, sourceId);
        internal static void ApplyLegacySectionsToUnifiedCatalog(PromptUnifiedCatalog unified, RimTalkPromptEntryDefaultsConfig sections) => PromptPresetSlice4.ApplyLegacySectionsToUnifiedCatalog(unified, sections);
        internal static bool HasMeaningfulPayload(PromptPresetChannelPayloads payload) => PromptPresetSlice4.HasMeaningfulPayload(payload);
        #endregion
}
    internal sealed class PromptPresetSlice1 : PromptPresetServiceCollaborator
    {
        internal PromptPresetSlice1(PromptPresetService owner) : base(owner)
        {
        }

public PromptPresetStoreConfig LoadAll(RelationsSettings settings)
        {
            bool readCorrupted;
            PromptPresetStoreConfig store = PromptPresetService.ReadStoreFile(out readCorrupted);
            if (store == null)
            {
                store = new PromptPresetStoreConfig();
            }

            PromptPresetService.NormalizeStore(settings, store, persistWhenEmpty: !readCorrupted);
            if (readCorrupted)
            {
                Log.Warning("[RimAI.Relations] Prompt preset store load failed due to corruption. Using in-memory defaults without overwriting disk data.");
            }

            return store;
        }

public void SaveAll(PromptPresetStoreConfig store)
        {
            PromptPresetStoreConfig normalized = store ?? new PromptPresetStoreConfig();
            normalized.SchemaVersion = CurrentSchemaVersion;
            normalized.Presets ??= new List<PromptPresetConfig>();
            PromptPresetService.EnforceImmutableDefaultPreset(normalized);
            if (string.IsNullOrWhiteSpace(normalized.DefaultPresetId) ||
                !normalized.Presets.Any(p => string.Equals(p.Id, normalized.DefaultPresetId, StringComparison.Ordinal)))
            {
                normalized.DefaultPresetId = PromptPresetService.ResolveDefaultPresetId(normalized.Presets);
            }

            PromptPresetService.EnsureStoreDirectory();
            string path = PromptPresetService.GetStorePath();
            string tempPath = path + ".tmp";
            string json = ReflectionJsonFieldSerializer.Serialize(normalized, prettyPrint: true);
            ModuleLog.Message($"[RimAI.Relations][PresetDiag] SaveAll begin. presets={normalized.Presets.Count}, active={normalized.ActivePresetId}, default={normalized.DefaultPresetId}, path={path}");
            if (normalized.Presets.Count > 0 &&
                json.IndexOf("\"Presets\"", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("[RimAI.Relations] Prompt preset store serialization dropped preset list. Save aborted.");
            }

            try
            {
                PromptPresetService.AtomicWriteText(path, tempPath, json);
                PromptPresetService.MirrorStoreToLegacyPath(path);
                ModuleLog.Message($"[RimAI.Relations][PresetDiag] SaveAll done. presets={normalized.Presets.Count}, bytes={new FileInfo(path).Length}");
            }
            finally
            {
                try
                {
                    if (LocalStorage.Current.FileExists(tempPath))
                    {
                        LocalStorage.Current.DeleteFile(tempPath);
                    }
                }
                catch
                {
                    // Best-effort cleanup; keep original failure reason.
                }
            }
        }

public PromptPresetConfig CreateFromLegacy(RelationsSettings settings, string name)
        {
            settings?.FlushPromptEditorsToStorageForPreset(persistToFiles: false);
            PromptPresetConfig preset = PromptPresetService.BuildPresetShell(name);
            preset.ChannelPayloads = PromptPresetService.CaptureCurrentPayload(settings);
            return preset;
        }

public PromptPresetConfig Duplicate(RelationsSettings settings, PromptPresetConfig source, string name)
        {
            PromptPresetConfig duplicated = PromptPresetService.BuildPresetShell(name);
            duplicated.ChannelPayloads = source?.ChannelPayloads?.Clone() ?? PromptPresetService.CaptureCurrentPayload(settings);
            return duplicated;
        }

public bool Activate(RelationsSettings settings, PromptPresetStoreConfig store, string presetId, out string error)
        {
            error = string.Empty;
            if (settings == null)
            {
                error = "Settings unavailable.";
                return false;
            }

            PromptPresetConfig target = store?.Presets?.FirstOrDefault(p => string.Equals(p.Id, presetId, StringComparison.Ordinal));
            if (target == null)
            {
                error = "Preset not found.";
                return false;
            }

            try
            {
                Owner.ApplyPayloadToSettings(settings, target.ChannelPayloads, persistToFiles: true);
                target.IsActive = true;
                if (!PromptPresetService.IsImmutableDefaultId(target.Id))
                {
                    target.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
                }

                if (store?.Presets != null)
                {
                    for (int i = 0; i < store.Presets.Count; i++)
                    {
                        PromptPresetConfig preset = store.Presets[i];
                        if (!string.Equals(preset.Id, target.Id, StringComparison.Ordinal))
                        {
                            preset.IsActive = false;
                        }
                    }
                }

                if (store != null)
                {
                    store.ActivePresetId = target.Id;
                }

                settings.RefreshPromptEditorStateFromStorage();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

public bool IsDefaultPreset(PromptPresetStoreConfig store, string presetId)
        {
            if (store?.Presets == null || store.Presets.Count == 0 || string.IsNullOrWhiteSpace(presetId))
            {
                return false;
            }

            return PromptPresetService.IsImmutableDefaultId(presetId);
        }

public bool EnsureEditablePresetForMutation(
            RelationsSettings settings,
            PromptPresetStoreConfig store,
            string selectedPresetId,
            string forkNamePrefix,
            out PromptPresetConfig editablePreset,
            out bool forked,
            out string error)
        {
            editablePreset = null;
            forked = false;
            error = string.Empty;
            if (settings == null)
            {
                error = "Settings unavailable.";
                return false;
            }

            if (store?.Presets == null || store.Presets.Count == 0)
            {
                error = "Preset store unavailable.";
                return false;
            }

            PromptPresetConfig selected = store.Presets.FirstOrDefault(p => string.Equals(p.Id, selectedPresetId, StringComparison.Ordinal))
                                        ?? store.Presets.FirstOrDefault(p => string.Equals(p.Id, store.ActivePresetId, StringComparison.Ordinal))
                                        ?? store.Presets.FirstOrDefault(p => p.IsActive)
                                        ?? store.Presets[0];
            if (selected == null)
            {
                error = "Selected preset unavailable.";
                return false;
            }

            if (!Owner.IsDefaultPreset(store, selected.Id))
            {
                editablePreset = selected;
                return true;
            }

            string prefix = string.IsNullOrWhiteSpace(forkNamePrefix) ? "Custom" : forkNamePrefix.Trim();
            string autoForkName = PromptPresetService.EnsureUniqueName(store.Presets, PromptPresetService.BuildTimestampPresetName(prefix, DateTime.Now));
            PromptPresetConfig forkPreset = Owner.Duplicate(settings, selected, autoForkName);
            if (forkPreset == null)
            {
                error = "Failed to create fork preset.";
                return false;
            }

            store.Presets.Add(forkPreset);
            if (!Owner.Activate(settings, store, forkPreset.Id, out string activateError))
            {
                store.Presets.RemoveAll(p => string.Equals(p.Id, forkPreset.Id, StringComparison.Ordinal));
                error = activateError ?? "Failed to activate fork preset.";
                return false;
            }

            Owner.SaveAll(store);
            editablePreset = forkPreset;
            forked = true;
            return true;
        }

public bool SyncPresetPayloadFromSettings(
            RelationsSettings settings,
            PromptPresetStoreConfig store,
            string presetId,
            out string error)
        {
            error = string.Empty;
            if (settings == null)
            {
                error = "Settings unavailable.";
                return false;
            }

            if (store?.Presets == null || store.Presets.Count == 0)
            {
                error = "Preset store unavailable.";
                return false;
            }

            PromptPresetConfig target = store.Presets.FirstOrDefault(p => string.Equals(p.Id, presetId, StringComparison.Ordinal))
                                      ?? store.Presets.FirstOrDefault(p => string.Equals(p.Id, store.ActivePresetId, StringComparison.Ordinal))
                                      ?? store.Presets.FirstOrDefault(p => p.IsActive)
                                      ?? store.Presets[0];
            if (target == null)
            {
                error = "Preset not found.";
                return false;
            }

            if (PromptPresetService.IsImmutableDefaultId(target.Id))
            {
                error = "Default preset is read-only.";
                return false;
            }

            target.ChannelPayloads = PromptPresetService.CaptureCurrentPayload(settings);
            target.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
            return true;
        }

public void ApplyPayloadToSettings(RelationsSettings settings, PromptPresetChannelPayloads payload, bool persistToFiles)
        {
            if (settings == null)
            {
                return;
            }

            PromptPresetChannelPayloads data = payload?.Clone() ?? PromptPresetService.CreateCanonicalDefaultPayload();
            PromptPresetService.NormalizePayload(data);
            if (persistToFiles)
            {
                PromptPresetService.ApplyPayloadToCustomFiles(data);
                PromptPresetService.PersistRpgPromptCustomStore(data);
            }

            PromptPresetService.ApplyRimTalkCompatSettings(settings, data, persistToFiles);
        }

public bool ExportPreset(string filePath, PromptPresetConfig preset, out string error)
        {
            error = string.Empty;
            if (preset == null)
            {
                error = "Preset is null.";
                return false;
            }

            try
            {
                string normalizedPath = PromptPresetService.NormalizePresetFilePath(filePath);
                if (string.IsNullOrWhiteSpace(normalizedPath))
                {
                    error = "File path is empty.";
                    return false;
                }

                string dir = Path.GetDirectoryName(normalizedPath);
                if (!string.IsNullOrWhiteSpace(dir) && !LocalStorage.Current.DirectoryExists(dir))
                {
                    LocalStorage.Current.CreateDirectory(dir);
                }

                string json = ReflectionJsonFieldSerializer.Serialize(preset, prettyPrint: true);
                if (json.IndexOf("\"ChannelPayloads\"", StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException("[RimAI.Relations] Prompt preset export serialization dropped channel payloads.");
                }

                LocalStorage.Current.WriteAllText(normalizedPath, json);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

public bool ImportPreset(string filePath, PromptPresetStoreConfig store, out PromptPresetConfig imported, out string error)
        {
            imported = null;
            error = string.Empty;
            string normalizedPath = PromptPresetService.NormalizePresetFilePath(filePath);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                error = "File path is empty.";
                return false;
            }

            if (!LocalStorage.Current.FileExists(normalizedPath))
            {
                error = "File not found.";
                return false;
            }

            try
            {
                string json = LocalStorage.Current.ReadAllText(normalizedPath);
                if (json.IndexOf("\"UnifiedPromptCatalog\"", StringComparison.Ordinal) < 0)
                {
                    error = "Unsupported legacy preset format. Please export with unified preset schema.";
                    return false;
                }

                if (!ReflectionJsonFieldDeserializer.TryDeserialize(json, out PromptPresetConfig parsed) ||
                    parsed == null)
                {
                    error = "Invalid preset file.";
                    return false;
                }

                PromptPresetService.ApplyLegacyPayloadFromJson(parsed, json, "preset.import");
                PromptPresetService.NormalizePreset(parsed);
                parsed.Id = Guid.NewGuid().ToString("N");
                parsed.IsActive = false;
                parsed.Name = PromptPresetService.EnsureUniqueName(store?.Presets, parsed.Name);
                parsed.CreatedAtUtc = DateTime.UtcNow.ToString("o");
                parsed.UpdatedAtUtc = parsed.CreatedAtUtc;
                imported = parsed;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

internal static string NormalizePresetFilePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return string.Empty;
            }

            return filePath.Trim().Trim('"');
        }

public List<PromptPresetSummary> BuildSummaries(PromptPresetStoreConfig store)
        {
            var list = new List<PromptPresetSummary>();
            List<PromptPresetConfig> presets = store?.Presets ?? new List<PromptPresetConfig>();
            for (int i = 0; i < presets.Count; i++)
            {
                PromptPresetConfig preset = presets[i];
                PromptPresetChannelPayloads payload = preset.ChannelPayloads ?? new PromptPresetChannelPayloads();
                list.Add(new PromptPresetSummary
                {
                    Id = preset.Id,
                    Name = preset.Name,
                    IsActive = preset.IsActive,
                    IsDefault = Owner.IsDefaultPreset(store, preset.Id),
                    DiplomacyChars = (payload.Diplomacy?.SystemPromptCustomJson?.Length ?? 0) +
                                     (payload.Diplomacy?.DialoguePromptCustomJson?.Length ?? 0),
                    RpgChars = payload.Rpg?.PawnPromptCustomJson?.Length ?? 0,
                    PromptSectionChars = PromptDomainJsonUtility.Serialize(payload.UnifiedPromptCatalog, prettyPrint: false)?.Length ?? 0
                });
            }

            return list;
        }

internal static PromptPresetConfig BuildPresetShell(string name)
        {
            string now = DateTime.UtcNow.ToString("o");
            return new PromptPresetConfig
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = string.IsNullOrWhiteSpace(name) ? "Preset" : name.Trim(),
                IsActive = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                ChannelPayloads = new PromptPresetChannelPayloads()
            };
        }
    }

    

    

    

    internal sealed class PromptPresetServiceParts
    {
        internal readonly PromptPresetService Owner;
        internal readonly PromptPresetServiceDefaultPreset DefaultPreset;
        internal readonly PromptPresetSlice1 Slice1;
        internal readonly PromptPresetSlice2 Slice2;
        internal readonly PromptPresetSlice3 Slice3;
        internal readonly PromptPresetSlice4 Slice4;
        internal PromptPresetServiceParts(PromptPresetService owner)
        {
            Owner = owner;
            DefaultPreset = new PromptPresetServiceDefaultPreset(owner);
            Slice1 = new PromptPresetSlice1(owner);
            Slice2 = new PromptPresetSlice2(owner);
            Slice3 = new PromptPresetSlice3(owner);
            Slice4 = new PromptPresetSlice4(owner);
        }
    }


}
