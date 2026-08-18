using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Verse;
using Ustas.RimAI.Communication.Relations.Serialization;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Core.Storage;

namespace Ustas.RimAI.Communication.Relations.Prompting.Transfer
{
    internal sealed class PromptBundleTransfer
    {

        private readonly PromptPersistenceService host;

        private PromptBundleImportFailure _lastPromptBundleImportFailure = PromptBundleImportFailure.None;
        private string _lastPromptBundleImportErrorCode = string.Empty;

        internal PromptBundleImportFailure LastFailure => _lastPromptBundleImportFailure;
        internal string LastErrorCode => _lastPromptBundleImportErrorCode ?? string.Empty;

        internal void ResetPromptBundleImportFailure()
        {
            _lastPromptBundleImportFailure = PromptBundleImportFailure.None;
            _lastPromptBundleImportErrorCode = string.Empty;
        }

        internal void SetPromptBundleImportFailure(PromptBundleImportFailure failure, string errorCode)
        {
            _lastPromptBundleImportFailure = failure;
            _lastPromptBundleImportErrorCode = errorCode ?? string.Empty;
        }

        internal PromptBundleTransfer(PromptPersistenceService host)
        {
            this.host = host ?? throw new System.ArgumentNullException(nameof(host));
        }
        internal PromptBundleConfig CreatePromptBundle(SystemPromptConfig config)
        {
            return CreatePromptBundle(config, PromptBundleModuleCatalog.All);
        }

        internal PromptBundleConfig CreatePromptBundle(
            SystemPromptConfig config,
            IEnumerable<PromptBundleModule> includedModules)
        {
            HashSet<PromptBundleModule> selected = NormalizeModuleSelection(includedModules, includeAllWhenEmpty: true);
            RelationsSettings settings = RelationsMod.Settings;

            var bundle = new PromptBundleConfig
            {
                BundleVersion = 2,
                IncludedModules = PromptBundleModuleCatalog.ToStorageTokens(selected),
                SystemPrompt = selected.Contains(PromptBundleModule.SystemPrompt)
                    ? host.DomainStore.BuildSystemPromptDomain(config)
                    : null,
                SystemPromptJson = string.Empty,
                DiplomacyDialoguePrompt = selected.Contains(PromptBundleModule.DiplomacyPrompt)
                    ? host.DomainStore.BuildDiplomacyPromptDomain(config)
                    : null,
                DiplomacyDialoguePromptJson = string.Empty,
                PawnDialoguePrompt = selected.Contains(PromptBundleModule.RpgPrompt)
                    ? RpgPromptCustomStore.LoadOrDefault()
                    : null,
                PawnDialoguePromptJson = string.Empty,
                SocialCirclePrompt = selected.Contains(PromptBundleModule.SocialCirclePrompt)
                    ? host.DomainStore.BuildSocialCirclePromptDomain(config)
                    : null,
                SocialCirclePromptJson = string.Empty,
                FactionPromptsJson = selected.Contains(PromptBundleModule.FactionPrompts)
                    ? FactionPromptManager.Instance.ExportConfigsToJson(prettyPrint: true)
                    : string.Empty,
                RimTalkSummaryHistoryLimit = settings?.GetRimTalkSummaryHistoryLimitClamped() ?? 10,
                PromptSectionCatalog = settings?.GetPromptSectionCatalogClone() ?? RimTalkPromptEntryDefaultsProvider.GetDefaultsSnapshot(),
                PromptSectionCatalogJson = string.Empty,
                HasUnifiedPromptCatalogPayload = true,
                UnifiedPromptCatalog = settings?.GetPromptUnifiedCatalogClone() ?? PromptUnifiedCatalogProvider.LoadMerged(),
                UnifiedPromptCatalogJson = string.Empty
            };

            bundle.SystemPromptJson = SerializeBundleSection(bundle.SystemPrompt);
            bundle.DiplomacyDialoguePromptJson = SerializeBundleSection(bundle.DiplomacyDialoguePrompt);
            bundle.PawnDialoguePromptJson = SerializeBundleSection(bundle.PawnDialoguePrompt);
            bundle.SocialCirclePromptJson = SerializeBundleSection(bundle.SocialCirclePrompt);
            bundle.PromptSectionCatalogJson = SerializeBundleSection(bundle.PromptSectionCatalog);
            bundle.UnifiedPromptCatalogJson = SerializeBundleSection(bundle.UnifiedPromptCatalog);

            return bundle;
        }

        internal bool TryParsePromptBundle(string json, out PromptBundleConfig bundle)
        {
            return TryParsePromptBundle(json, out bundle, out _);
        }

        internal bool TryValidatePromptBundleImportEnvelope(
            string json,
            out PromptBundleImportFailure failure,
            out string errorCode)
        {
            return PromptBundleEnvelope.TryValidate(json, out failure, out errorCode);
        }

        internal bool TryParsePromptBundle(
            string json,
            out PromptBundleConfig bundle,
            out HashSet<PromptBundleModule> includedModules)
        {
            includedModules = new HashSet<PromptBundleModule>();
            if (!PromptDomainJsonUtility.TryDeserialize(json, out bundle) || bundle == null)
            {
                return false;
            }

            HydratePromptBundleSectionsFromRawJson(bundle);
            bundle.SystemPrompt ??= new SystemPromptDomainConfig();
            bundle.DiplomacyDialoguePrompt ??= new DiplomacyDialoguePromptDomainConfig();
            bundle.PawnDialoguePrompt ??= new RpgPromptCustomConfig();
            bundle.SocialCirclePrompt ??= new SocialCirclePromptDomainConfig();
            bundle.PromptSectionCatalog ??= RimTalkPromptEntryDefaultsProvider.GetDefaultsSnapshot();
            bundle.UnifiedPromptCatalog ??= PromptUnifiedCatalog.CreateFallback();
            bundle.UnifiedPromptCatalog.NormalizeWith(PromptUnifiedCatalog.CreateFallback());

            if (bundle.BundleVersion <= 1 || bundle.IncludedModules == null || bundle.IncludedModules.Count == 0)
            {
                includedModules = NormalizeModuleSelection(PromptBundleModuleCatalog.All, includeAllWhenEmpty: true);
            }
            else
            {
                foreach (string token in bundle.IncludedModules)
                {
                    if (PromptBundleModuleCatalog.TryParseStorageToken(token, out PromptBundleModule module))
                    {
                        includedModules.Add(module);
                    }
                }

                if (includedModules.Count == 0)
                {
                    includedModules = NormalizeModuleSelection(PromptBundleModuleCatalog.All, includeAllWhenEmpty: true);
                }
            }
            bundle.PromptSectionCatalog = PromptLegacyCompatMigration.ApplyLegacyPayloadToPromptSections(
                bundle.PromptSectionCatalog,
                json,
                "bundle");
            bundle.HasUnifiedPromptCatalogPayload =
                PromptJsonText.ContainsJsonKey(json, "UnifiedPromptCatalog") ||
                PromptJsonText.ContainsJsonKey(json, "UnifiedPromptCatalogJson");

            if (bundle.RimTalkSummaryHistoryLimit <= 0)
            {
                bundle.RimTalkSummaryHistoryLimit = bundle.PawnDialoguePrompt?.RimTalkSummaryHistoryLimit ?? 10;
            }

            bundle.IncludedModules = PromptBundleModuleCatalog.ToStorageTokens(includedModules);
            bundle.BundleVersion = Math.Max(bundle.BundleVersion, 1);
            return true;
        }

        private static string SerializeBundleSection<TPayload>(TPayload payload) where TPayload : class
        {
            if (payload == null)
            {
                return string.Empty;
            }

            try
            {
                return PromptDomainJsonUtility.Serialize(payload, prettyPrint: false) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        internal void HydratePromptBundleSectionsFromRawJson(PromptBundleConfig bundle)
        {
            if (bundle == null)
            {
                return;
            }

            TryDeserializeBundleSection(bundle.SystemPromptJson, ref bundle.SystemPrompt);
            TryDeserializeBundleSection(bundle.DiplomacyDialoguePromptJson, ref bundle.DiplomacyDialoguePrompt);
            TryDeserializeBundleSection(bundle.PawnDialoguePromptJson, ref bundle.PawnDialoguePrompt);
            TryDeserializeBundleSection(bundle.SocialCirclePromptJson, ref bundle.SocialCirclePrompt);
            TryDeserializeBundleSection(bundle.PromptSectionCatalogJson, ref bundle.PromptSectionCatalog);
            TryDeserializeBundleSection(bundle.UnifiedPromptCatalogJson, ref bundle.UnifiedPromptCatalog);
        }

        private static void TryDeserializeBundleSection<TPayload>(string json, ref TPayload target)
            where TPayload : class, new()
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            if (PromptDomainJsonUtility.TryDeserialize(json, out TPayload payload) && payload != null)
            {
                target = payload;
            }
        }

        internal HashSet<PromptBundleModule> NormalizeModuleSelection(
            IEnumerable<PromptBundleModule> modules,
            bool includeAllWhenEmpty)
        {
            var set = modules != null
                ? new HashSet<PromptBundleModule>(modules)
                : new HashSet<PromptBundleModule>();
            if (set.Count == 0 && includeAllWhenEmpty)
            {
                foreach (PromptBundleModule module in PromptBundleModuleCatalog.All)
                {
                    set.Add(module);
                }
            }

            return set;
        }

        internal bool TryBuildPromptBundleImportPreview(string filePath, out PromptBundleImportPreview preview)
        {
            ResetPromptBundleImportFailure();
            preview = null;
            if (string.IsNullOrWhiteSpace(filePath))
            {
                SetPromptBundleImportFailure(PromptBundleImportFailure.EmptyPath, PromptBundleImportErrorCodes.EmptyPath);
                return false;
            }

            if (!LocalStorage.Current.FileExists(filePath))
            {
                SetPromptBundleImportFailure(PromptBundleImportFailure.FileNotFound, PromptBundleImportErrorCodes.FileNotFound);
                return false;
            }

            try
            {
                string json = LocalStorage.Current.ReadAllText(filePath, System.Text.Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    SetPromptBundleImportFailure(PromptBundleImportFailure.EmptyFile, PromptBundleImportErrorCodes.EmptyFile);
                    return false;
                }

                if (!PromptBundleEnvelope.TryValidate(json, out PromptBundleImportFailure envelopeFailure, out string envelopeErrorCode))
                {
                    SetPromptBundleImportFailure(envelopeFailure, envelopeErrorCode);
                    Log.Warning($"[RimAI.Relations][{envelopeErrorCode}] Import preview rejected non-bundle file: {filePath}");
                    return false;
                }

                if (!TryParsePromptBundle(json, out PromptBundleConfig bundle, out HashSet<PromptBundleModule> includedModules))
                {
                    SetPromptBundleImportFailure(PromptBundleImportFailure.InvalidBundlePayload, PromptBundleImportErrorCodes.InvalidBundlePayload);
                    Log.Warning($"[RimAI.Relations][{PromptBundleImportErrorCodes.InvalidBundlePayload}] Import preview failed to parse bundle payload: {filePath}");
                    return false;
                }

                preview = new PromptBundleImportPreview
                {
                    FilePath = filePath,
                    BundleVersion = bundle.BundleVersion,
                    AvailableModules = includedModules.OrderBy(item => (int)item).ToList()
                };

                foreach (PromptBundleModule module in preview.AvailableModules)
                {
                    try
                    {
                        preview.ModuleSummaries[module] = BuildModuleSummary(bundle, module);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[RimAI.Relations] Failed to build import-preview summary for module {module}: {ex.Message}");
                        preview.ModuleSummaries[module] = "RimChat_PromptBundleSummary_Unavailable".Translate().ToString();
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                SetPromptBundleImportFailure(PromptBundleImportFailure.UnexpectedException, PromptBundleImportErrorCodes.UnexpectedException);
                Log.Warning($"[RimAI.Relations][{PromptBundleImportErrorCodes.UnexpectedException}] Failed to build prompt-bundle import preview: {ex.Message}");
                preview = null;
                return false;
            }
        }

        internal string BuildModuleSummary(PromptBundleConfig bundle, PromptBundleModule module)
        {
            switch (module)
            {
                case PromptBundleModule.SystemPrompt:
                    return bundle?.SystemPrompt == null
                        ? "RimChat_PromptBundleSummary_Unavailable".Translate().ToString()
                        : "RimChat_PromptBundleSummary_System".Translate(bundle.SystemPrompt.ConfigName ?? "Default").ToString();
                case PromptBundleModule.DiplomacyPrompt:
                    return bundle?.DiplomacyDialoguePrompt == null
                        ? "RimChat_PromptBundleSummary_Unavailable".Translate().ToString()
                        : "RimChat_PromptBundleSummary_Diplomacy".Translate(
                            bundle.DiplomacyDialoguePrompt.ApiActions?.Count ?? 0,
                            bundle.DiplomacyDialoguePrompt.DecisionRules?.Count ?? 0).ToString();
                case PromptBundleModule.RpgPrompt:
                    return bundle?.PawnDialoguePrompt == null
                        ? "RimChat_PromptBundleSummary_Unavailable".Translate().ToString()
                        : "RimChat_PromptBundleSummary_Rpg".Translate(
                            (bundle.PawnDialoguePrompt.RoleSetting ?? string.Empty).Length,
                            (bundle.PawnDialoguePrompt.DialogueStyle ?? string.Empty).Length).ToString();
                case PromptBundleModule.SocialCirclePrompt:
                    return bundle?.SocialCirclePrompt == null
                        ? "RimChat_PromptBundleSummary_Unavailable".Translate().ToString()
                        : "RimChat_PromptBundleSummary_Social".Translate(
                            bundle.SocialCirclePrompt.PublishPublicPostAction?.ActionName ?? "publish_public_post").ToString();
                case PromptBundleModule.FactionPrompts:
                    int count = FactionPromptJsonUtility.FromJson(bundle?.FactionPromptsJson ?? string.Empty)?.Configs?.Count ?? 0;
                    return "RimChat_PromptBundleSummary_Faction".Translate(count).ToString();
                default:
                    return "RimChat_PromptBundleSummary_Unavailable".Translate().ToString();
            }
        }

        internal void SavePromptBundle(PromptBundleConfig bundle)
        {
            HashSet<PromptBundleModule> included = NormalizeModuleSelection(PromptBundleModuleCatalog.All, includeAllWhenEmpty: true);
            if (bundle?.IncludedModules != null && bundle.IncludedModules.Count > 0)
            {
                included.Clear();
                foreach (string token in bundle.IncludedModules)
                {
                    if (PromptBundleModuleCatalog.TryParseStorageToken(token, out PromptBundleModule parsed))
                    {
                        included.Add(parsed);
                    }
                }

                if (included.Count == 0)
                {
                    included = NormalizeModuleSelection(PromptBundleModuleCatalog.All, includeAllWhenEmpty: true);
                }
            }

            SavePromptBundle(bundle, included);
        }

        internal void SavePromptBundle(PromptBundleConfig bundle, IEnumerable<PromptBundleModule> selectedModules)
        {
            if (bundle == null)
            {
                return;
            }

            HashSet<PromptBundleModule> selected = NormalizeModuleSelection(selectedModules, includeAllWhenEmpty: false);
            if (selected.Count == 0)
            {
                return;
            }

            PromptDomainFileCatalog.EnsureCustomDirectoryExists();

            if (selected.Contains(PromptBundleModule.SystemPrompt))
            {
                PromptDomainJsonUtility.WriteToFile(
                    PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.SystemPromptCustomFileName),
                    bundle.SystemPrompt ?? new SystemPromptDomainConfig());
            }

            if (selected.Contains(PromptBundleModule.DiplomacyPrompt))
            {
                PromptDomainJsonUtility.WriteToFile(
                    PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.DiplomacyPromptCustomFileName),
                    bundle.DiplomacyDialoguePrompt ?? new DiplomacyDialoguePromptDomainConfig());
            }

            if (selected.Contains(PromptBundleModule.SocialCirclePrompt))
            {
                PromptDomainJsonUtility.WriteToFile(
                    PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.SocialCirclePromptCustomFileName),
                    bundle.SocialCirclePrompt ?? new SocialCirclePromptDomainConfig());
            }

            RpgPromptCustomConfig currentRpg = RpgPromptCustomStore.LoadOrDefault() ?? new RpgPromptCustomConfig();
            RpgPromptCustomConfig mergedRpg = PromptDomainJsonUtility.Clone(currentRpg);
            bool shouldSaveRpg = false;

            if (selected.Contains(PromptBundleModule.RpgPrompt))
            {
                mergedRpg = PromptDomainJsonUtility.Clone(bundle.PawnDialoguePrompt ?? new RpgPromptCustomConfig());
                mergedRpg ??= new RpgPromptCustomConfig();
                mergedRpg.RimTalkSummaryHistoryLimit = bundle.RimTalkSummaryHistoryLimit > 0
                    ? bundle.RimTalkSummaryHistoryLimit
                    : currentRpg.RimTalkSummaryHistoryLimit;
                if (bundle.PromptSectionCatalog != null)
                {
                    RelationsMod.Settings?.ImportLegacySectionCatalogToUnifiedCatalog(
                        bundle.PromptSectionCatalog,
                        "bundle.import",
                        persistToFiles: true);
                }

                shouldSaveRpg = true;
            }

            if (shouldSaveRpg)
            {
                RpgPromptCustomStore.Save(mergedRpg);
            }

            if (selected.Contains(PromptBundleModule.FactionPrompts) &&
                !string.IsNullOrWhiteSpace(bundle.FactionPromptsJson))
            {
                FactionPromptManager.Instance.ImportConfigsFromJson(bundle.FactionPromptsJson);
            }

            bool shouldApplyUnified = selected.Contains(PromptBundleModule.SystemPrompt) ||
                                      selected.Contains(PromptBundleModule.DiplomacyPrompt) ||
                                      selected.Contains(PromptBundleModule.RpgPrompt) ||
                                      selected.Contains(PromptBundleModule.SocialCirclePrompt);
            if (shouldApplyUnified && bundle.HasUnifiedPromptCatalogPayload)
            {
                PromptUnifiedCatalog unified = bundle.UnifiedPromptCatalog?.Clone() ?? PromptUnifiedCatalog.CreateFallback();
                unified.NormalizeWith(PromptUnifiedCatalog.CreateFallback());
                PromptUnifiedCatalogProvider.SaveCustom(unified);
            }
        }
        public bool ExportConfig(string filePath)
        {
            return ExportConfig(filePath, PromptBundleModuleCatalog.All);
        }

        internal bool ExportConfig(string filePath, IEnumerable<PromptBundleModule> selectedModules)
        {
            try
            {
                if (!TryPrepareExportPath(filePath, out string normalizedPath))
                {
                    return false;
                }

                if (host.DomainStore.CachedConfig == null)
                {
                    host.DomainStore.CachedConfig = host.DomainStore.LoadConfig();
                }

                PromptBundleConfig bundle = CreatePromptBundle(host.DomainStore.CachedConfig, selectedModules);
                string json = PromptDomainJsonUtility.Serialize(bundle, prettyPrint: true);
                LocalStorage.Current.WriteAllText(normalizedPath, json, Encoding.UTF8);
                Log.Message($"[RimAI.Relations] Exported config to: {normalizedPath}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Relations] Failed to export config: {ex}");
                return false;
            }
        }

        public bool ImportConfig(string filePath)
        {
            return ImportConfig(filePath, null);
        }

        internal bool ImportConfig(string filePath, IEnumerable<PromptBundleModule> selectedModules)
        {
            ResetPromptBundleImportFailure();
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    SetPromptBundleImportFailure(PromptBundleImportFailure.EmptyPath, PromptBundleImportErrorCodes.EmptyPath);
                    Log.Warning($"[RimAI.Relations][{PromptBundleImportErrorCodes.EmptyPath}] Import path is empty.");
                    return false;
                }

                if (!LocalStorage.Current.FileExists(filePath))
                {
                    SetPromptBundleImportFailure(PromptBundleImportFailure.FileNotFound, PromptBundleImportErrorCodes.FileNotFound);
                    Log.Warning($"[RimAI.Relations][{PromptBundleImportErrorCodes.FileNotFound}] Import file not found: {filePath}");
                    return false;
                }

                string json = LocalStorage.Current.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    SetPromptBundleImportFailure(PromptBundleImportFailure.EmptyFile, PromptBundleImportErrorCodes.EmptyFile);
                    Log.Warning($"[RimAI.Relations][{PromptBundleImportErrorCodes.EmptyFile}] Import file is empty: {filePath}");
                    return false;
                }

                if (!PromptBundleEnvelope.TryValidate(json, out PromptBundleImportFailure envelopeFailure, out string envelopeErrorCode))
                {
                    SetPromptBundleImportFailure(envelopeFailure, envelopeErrorCode);
                    Log.Warning($"[RimAI.Relations][{envelopeErrorCode}] Reject non-bundle import file: {filePath}");
                    return false;
                }

                if (!TryParsePromptBundle(json, out PromptBundleConfig bundle, out HashSet<PromptBundleModule> includedModules))
                {
                    SetPromptBundleImportFailure(PromptBundleImportFailure.InvalidBundlePayload, PromptBundleImportErrorCodes.InvalidBundlePayload);
                    Log.Warning($"[RimAI.Relations][{PromptBundleImportErrorCodes.InvalidBundlePayload}] Failed to parse Prompt Bundle payload: {filePath}");
                    return false;
                }

                HashSet<PromptBundleModule> modulesToApply = ResolveImportSelection(selectedModules, includedModules);
                if (modulesToApply.Count == 0)
                {
                    SetPromptBundleImportFailure(PromptBundleImportFailure.NoModuleOverlap, PromptBundleImportErrorCodes.NoModuleOverlap);
                    Log.Warning($"[RimAI.Relations][{PromptBundleImportErrorCodes.NoModuleOverlap}] Import skipped because no overlapping module was selected.");
                    return false;
                }

                SavePromptBundle(bundle, modulesToApply);
                host.DomainStore.CachedConfig = null;
                host.DomainStore.CachedConfigWriteTimeUtc = DateTime.MinValue;
                Log.Message($"[RimAI.Relations] Imported config from: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                SetPromptBundleImportFailure(PromptBundleImportFailure.UnexpectedException, PromptBundleImportErrorCodes.UnexpectedException);
                Log.Error($"[RimAI.Relations][{PromptBundleImportErrorCodes.UnexpectedException}] Failed to import config: {ex}");
                return false;
            }
        }

        internal bool TryGetImportPreview(string filePath, out PromptBundleImportPreview preview)
        {
            return TryBuildPromptBundleImportPreview(filePath, out preview);
        }

        internal bool TryPrepareExportPath(string filePath, out string normalizedPath)
        {
            normalizedPath = filePath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                Log.Warning("[RimAI.Relations] Export path is empty.");
                return false;
            }

            try
            {
                string directory = Path.GetDirectoryName(normalizedPath);
                if (!string.IsNullOrWhiteSpace(directory) && !LocalStorage.Current.DirectoryExists(directory))
                {
                    LocalStorage.Current.CreateDirectory(directory);
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Relations] Invalid export path '{normalizedPath}': {ex.Message}");
                return false;
            }
        }

        internal HashSet<PromptBundleModule> ResolveImportSelection(
            IEnumerable<PromptBundleModule> selectedModules,
            HashSet<PromptBundleModule> includedModules)
        {
            if (includedModules == null || includedModules.Count == 0)
            {
                return new HashSet<PromptBundleModule>();
            }

            if (selectedModules == null)
            {
                return new HashSet<PromptBundleModule>(includedModules);
            }

            return new HashSet<PromptBundleModule>(
                selectedModules.Where(item => includedModules.Contains(item)));
        }

    }
}
