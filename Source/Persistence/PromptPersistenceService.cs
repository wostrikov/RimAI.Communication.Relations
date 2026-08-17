using System;
using System.Collections.Generic;
using System.IO;
using RimWorld;
using Verse;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.Prompting.Builders;
using Ustas.RimAI.Communication.Relations.Prompting.Diplomacy;
using Ustas.RimAI.Communication.Relations.Prompting.Transfer;

namespace Ustas.RimAI.Communication.Relations.Persistence
{
    /// <summary>
    /// Dependencies: prompt config store, bundle transfer, domain builders, context assembler, snapshot service.
    /// Responsibility: facade/coordinator for Relations prompt persistence and composition.
    /// </summary>
    public class PromptPersistenceService : IPromptConfigStore, Prompting.IRelationsPromptBuilder
    {
        private static PromptPersistenceService _instance;
        public static PromptPersistenceService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new PromptPersistenceService();
                }
                return _instance;
            }
        }

        public const string PromptFolderName = PromptDomainFileCatalog.PromptFolderName;
        public const string CustomSubFolderName = PromptDomainFileCatalog.CustomSubFolderName;
        internal const int ExpandMemoryPawnMemoryMaxCharsDefault = RelationsContextAssembler.ExpandMemoryPawnMemoryMaxCharsDefault;
        internal const int ExpandMemoryPawnMemoryMaxCharsMin = RelationsContextAssembler.ExpandMemoryPawnMemoryMaxCharsMin;
        internal const int ExpandMemoryPawnMemoryMaxCharsMax = RelationsContextAssembler.ExpandMemoryPawnMemoryMaxCharsMax;
        internal const int ExpandMemoryPawnMemoryMaxEntriesDefault = RelationsContextAssembler.ExpandMemoryPawnMemoryMaxEntriesDefault;
        internal const int ExpandMemoryPawnMemoryMaxEntriesMin = RelationsContextAssembler.ExpandMemoryPawnMemoryMaxEntriesMin;
        internal const int ExpandMemoryPawnMemoryMaxEntriesMax = RelationsContextAssembler.ExpandMemoryPawnMemoryMaxEntriesMax;
        internal const int ExpandMemoryPawnMemoryMaxEntriesPerLayer = RelationsContextAssembler.ExpandMemoryPawnMemoryMaxEntriesPerLayer;

        private bool _isInitialized;
        private readonly PromptConfigStore _configStore;

        internal PromptDomainStore DomainStore { get; }
        internal PromptBundleTransfer BundleTransfer { get; }
        internal DiplomacyPromptBuilder DiplomacyBuilder { get; }
        internal DiplomacyStrategyPromptBuilder DiplomacyStrategyBuilder { get; }
        internal RpgPromptBuilder RpgBuilder { get; }
        internal RelationsContextAssembler ContextAssembler { get; }
        internal PromptWorkspaceComposer WorkspaceComposer { get; }
        internal PromptTemplateVariableService TemplateVariables { get; }
        internal PromptSnapshotService SnapshotService { get; }
        internal PromptConfigNormalization Normalization { get; }
        internal PromptNodeSupport NodeSupport { get; }

        private PromptPersistenceService()
        {
            DomainStore = new PromptDomainStore(this);
            BundleTransfer = new PromptBundleTransfer(this);
            DiplomacyBuilder = new DiplomacyPromptBuilder(this);
            DiplomacyStrategyBuilder = new DiplomacyStrategyPromptBuilder(this);
            RpgBuilder = new RpgPromptBuilder(this);
            ContextAssembler = new RelationsContextAssembler(this);
            WorkspaceComposer = new PromptWorkspaceComposer(this);
            TemplateVariables = new PromptTemplateVariableService(this);
            SnapshotService = new PromptSnapshotService(this);
            Normalization = new PromptConfigNormalization(this);
            NodeSupport = new PromptNodeSupport(this);
            _configStore = new PromptConfigStore(() => ConfigFilePath, () => DomainStore.EnsureDirectoryExists());
        }

        public string BasePath
        {
            get
            {
                string path = PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.SystemPromptCustomFileName);
                return Path.GetDirectoryName(path) ?? string.Empty;
            }
        }

        public string ConfigFilePath => Path.Combine(BasePath, PromptDomainFileCatalog.SystemPromptCustomFileName);

        public void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            try
            {
                DomainStore.EnsureDirectoryExists();
                DomainStore.LoadConfig();
                _isInitialized = true;
                Log.Message($"[RimAI.Relations] PromptPersistenceService initialized, config path: {ConfigFilePath}");
            }
            catch (PromptRenderException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Relations] Failed to initialize PromptPersistenceService: {ex}");
                DomainStore.CreateDefaultConfig();
            }
        }

        public string GetConfigFilePath() => ConfigFilePath;

        public bool ConfigExists() => DomainStore.ConfigExists();

        public SystemPromptConfig LoadConfig() => DomainStore.LoadConfig();

        public SystemPromptConfig LoadConfigReadOnly() => DomainStore.LoadConfigReadOnly();

        public bool RepairAndRewritePromptDomains() => DomainStore.RepairAndRewritePromptDomains();

        public void SaveConfig(SystemPromptConfig config) => DomainStore.SaveConfig(config);

        public void ResetToDefault() => DomainStore.ResetToDefault();

        public bool ExportConfig(string filePath) => BundleTransfer.ExportConfig(filePath);

        internal bool ExportConfig(string filePath, IEnumerable<PromptBundleModule> selectedModules) =>
            BundleTransfer.ExportConfig(filePath, selectedModules);

        public bool ImportConfig(string filePath) => BundleTransfer.ImportConfig(filePath);

        internal bool ImportConfig(string filePath, IEnumerable<PromptBundleModule> selectedModules) =>
            BundleTransfer.ImportConfig(filePath, selectedModules);

        internal bool TryGetImportPreview(string filePath, out PromptBundleImportPreview preview) =>
            BundleTransfer.TryGetImportPreview(filePath, out preview);

        internal PromptBundleImportFailure GetLastPromptBundleImportFailure() => BundleTransfer.LastFailure;

        internal string GetLastPromptBundleImportErrorCode() => BundleTransfer.LastErrorCode;

        internal PromptTemplateAutoRewriteResult GetLastSchemaRewriteResult() => Normalization.LastSchemaRewriteResult;

        internal SystemPromptConfig ParseJsonToConfigInternal(string json, string sourceContext = "unknown") =>
            DomainStore.ParseJsonToConfigInternal(json, sourceContext);

        public string BuildFullSystemPrompt(Faction faction, SystemPromptConfig config, bool isProactive, IEnumerable<string> additionalSceneTags)
        {
            return DiplomacyBuilder.Build(faction, config, isProactive, additionalSceneTags);
        }

        public string BuildFullSystemPrompt(
            Faction faction,
            SystemPromptConfig config,
            bool isProactive,
            IEnumerable<string> additionalSceneTags,
            DiplomacyPromptRuntimeSnapshot runtimeSnapshot)
        {
            return DiplomacyBuilder.Build(faction, config, isProactive, additionalSceneTags, null, runtimeSnapshot);
        }

        public string BuildFullSystemPrompt(
            Faction faction,
            SystemPromptConfig config,
            bool isProactive,
            IEnumerable<string> additionalSceneTags,
            Pawn playerNegotiator)
        {
            return DiplomacyBuilder.Build(faction, config, isProactive, additionalSceneTags, playerNegotiator);
        }

        public string BuildFullSystemPrompt(
            Faction faction,
            SystemPromptConfig config,
            bool isProactive,
            IEnumerable<string> additionalSceneTags,
            Pawn playerNegotiator,
            DiplomacyPromptRuntimeSnapshot runtimeSnapshot)
        {
            return DiplomacyBuilder.Build(
                faction,
                config,
                isProactive,
                additionalSceneTags,
                playerNegotiator,
                runtimeSnapshot);
        }

        public string BuildDiplomacyStrategySystemPrompt(
            Faction faction,
            SystemPromptConfig config,
            IEnumerable<string> additionalSceneTags,
            DiplomacyStrategyPromptContext strategyContext)
        {
            return DiplomacyStrategyBuilder.Build(faction, config, additionalSceneTags, strategyContext);
        }

        public string BuildRPGFullSystemPrompt(
            Pawn initiator,
            Pawn target,
            bool isProactive,
            IEnumerable<string> additionalSceneTags,
            bool allowMemoryCompressionScheduling = true,
            bool allowMemoryColdLoad = true)
        {
            return RpgBuilder.Build(
                initiator,
                target,
                isProactive,
                additionalSceneTags,
                allowMemoryCompressionScheduling,
                allowMemoryColdLoad);
        }

        internal string BuildUnifiedChannelSystemPrompt(
            RimTalkPromptChannel rootChannel,
            string promptChannel,
            DialogueScenarioContext scenarioContext,
            EnvironmentPromptConfig environmentConfig,
            IReadOnlyDictionary<string, object> additionalValues = null,
            string payloadTag = "",
            string payloadText = "",
            bool deterministicPreview = false,
            bool allowMemoryCompressionScheduling = true,
            bool allowMemoryColdLoad = true,
            DiplomacyPromptRuntimeSnapshot runtimeSnapshot = null)
        {
            return WorkspaceComposer.BuildUnifiedChannelSystemPrompt(
                rootChannel,
                promptChannel,
                scenarioContext,
                environmentConfig,
                additionalValues,
                payloadTag,
                payloadText,
                deterministicPreview,
                allowMemoryCompressionScheduling,
                allowMemoryColdLoad,
                runtimeSnapshot);
        }

        internal PromptWorkspaceIncrementalPreviewBuildState CreatePromptWorkspaceIncrementalPreviewBuild(
            RimTalkPromptChannel rootChannel,
            string promptChannel)
        {
            return WorkspaceComposer.CreatePromptWorkspaceIncrementalPreviewBuild(rootChannel, promptChannel);
        }

        internal void StepPromptWorkspaceIncrementalPreviewBuild(PromptWorkspaceIncrementalPreviewBuildState state)
        {
            WorkspaceComposer.StepPromptWorkspaceIncrementalPreviewBuild(state);
        }

        public IReadOnlyList<PromptTemplateVariableDefinition> GetTemplateVariableDefinitions() =>
            TemplateVariables.GetTemplateVariableDefinitions();

        public TemplateVariableValidationResult ValidateTemplateVariables(string templateText) =>
            TemplateVariables.ValidateTemplateVariables(templateText);

        public TemplateVariableValidationResult ValidateTemplateVariables(
            string templateText,
            IEnumerable<string> additionalKnownVariables)
        {
            return TemplateVariables.ValidateTemplateVariables(templateText, additionalKnownVariables);
        }

        internal TemplateVariableValidationResult ValidateTemplateVariables(
            string templateText,
            TemplateVariableValidationContext validationContext)
        {
            return TemplateVariables.ValidateTemplateVariables(templateText, validationContext);
        }

        internal string BuildEnvironmentPromptBlocks(SystemPromptConfig config, DialogueScenarioContext context) =>
            ContextAssembler.BuildEnvironmentPromptBlocks(config, context);

        internal string BuildEnvironmentPromptBlocksWithDiagnostics(
            SystemPromptConfig config,
            DialogueScenarioContext context,
            out EnvironmentPromptBuildDiagnostics diagnostics)
        {
            return ContextAssembler.BuildEnvironmentPromptBlocksWithDiagnostics(config, context, out diagnostics);
        }

        internal Pawn ResolveBestPlayerNegotiator(Pawn preferred) => ContextAssembler.ResolveBestPlayerNegotiator(preferred);

        internal string BuildPlayerPawnContextForPrompt(Faction faction, Pawn preferredNegotiator) =>
            ContextAssembler.BuildPlayerPawnContextForPrompt(faction, preferredNegotiator);

        internal string BuildPlayerRoyaltySummaryForPrompt(Faction faction, Pawn preferredNegotiator) =>
            ContextAssembler.BuildPlayerRoyaltySummaryForPrompt(faction, preferredNegotiator);

        internal string BuildFactionSettlementSummaryForPrompt(Faction faction) =>
            ContextAssembler.BuildFactionSettlementSummaryForPrompt(faction);

        public string BuildPawnPersonaBootstrapProfile(Pawn pawn) => RpgBuilder.BuildPawnPersonaBootstrapProfile(pawn);

        internal IDisposable PushRuntimeSnapshotScope(DiplomacyPromptRuntimeSnapshot snapshot) =>
            SnapshotService.PushRuntimeSnapshotScope(snapshot);

        internal DiplomacyPromptRuntimeSnapshot BuildRuntimeSnapshotForFaction(
            Faction faction,
            Pawn preferredNegotiator,
            int builtTick,
            int memoryRevision,
            int worldEventRevision,
            long promptFilesStampUtcTicks,
            int settingsSignature)
        {
            return SnapshotService.BuildRuntimeSnapshotForFaction(
                faction,
                preferredNegotiator,
                builtTick,
                memoryRevision,
                worldEventRevision,
                promptFilesStampUtcTicks,
                settingsSignature);
        }

        internal Dictionary<string, object> BuildStrategyRuntimeValuesOrThrow(DiplomacyStrategyPromptContext strategyContext)
        {
            return DiplomacyStrategyRuntimeValues.BuildOrThrow(strategyContext);
        }
    }
}
