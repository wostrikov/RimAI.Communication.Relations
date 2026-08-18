using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.Diagnostics;
using RimWorld;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Context;

using Ustas.RimAI.Communication.Relations.Persistence;

namespace Ustas.RimAI.Communication.Relations.Prompting
{
    /// <summary>
    /// Dependencies: unified prompt catalog, prompt template renderer, and workspace preview models.
    /// Responsibility: provide one shared composer for workbench preview and side-channel runtime prompts.
    /// </summary>
internal sealed class PromptWorkspaceComposer
    {
        internal PromptWorkspaceComposerParts Parts;
        internal readonly PromptPersistenceService host;

        internal PromptWorkspaceComposer(PromptPersistenceService host)
        {
            this.host = host ?? throw new System.ArgumentNullException(nameof(host));
            Parts = new PromptWorkspaceComposerParts(this);
        }

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        // Build the base compose values (without template-specific injection) for reuse across sections.
        // Excludes: InjectRuntimeNodeBodies, ctx.channel, ctx.mode, MergeAdditionalValues, RecordSnapshot
        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        
    
        #region Cluster forwards
        internal string BuildUnifiedChannelSystemPrompt(RimTalkPromptChannel rootChannel, string promptChannel, DialogueScenarioContext scenarioContext, EnvironmentPromptConfig environmentConfig, IReadOnlyDictionary<string, object> additionalValues = null, string payloadTag = "", string payloadText = "", bool deterministicPreview = false, bool allowMemoryCompressionScheduling = true, bool allowMemoryColdLoad = true, DiplomacyPromptRuntimeSnapshot runtimeSnapshot = null) => Parts.Slice1.BuildUnifiedChannelSystemPrompt(rootChannel, promptChannel, scenarioContext, environmentConfig, additionalValues, payloadTag, payloadText, deterministicPreview, allowMemoryCompressionScheduling, allowMemoryColdLoad, runtimeSnapshot);
        internal string ApplyRuntimePromptPostProcessing(string prompt, RimTalkPromptChannel rootChannel, string promptChannel, bool deterministicPreview) => Parts.Slice1.ApplyRuntimePromptPostProcessing(prompt, rootChannel, promptChannel, deterministicPreview);
        internal string InjectDialogueStyleDirective(string prompt, RimTalkPromptChannel rootChannel, string promptChannel) => Parts.Slice1.InjectDialogueStyleDirective(prompt, rootChannel, promptChannel);
        internal string DeduplicatePromptAuthorityLines(string prompt) => Parts.Slice1.DeduplicatePromptAuthorityLines(prompt);
        internal bool IsDuplicateAuthorityLine(string trimmedLine) => Parts.Slice1.IsDuplicateAuthorityLine(trimmedLine);
        internal bool IsSocialCirclePostChannel(string promptChannel) => Parts.Slice1.IsSocialCirclePostChannel(promptChannel);
        internal PromptWorkspaceComposeResult ComposePromptWorkspace(RimTalkPromptChannel rootChannel, string promptChannel, bool includeNodes, bool deterministicPreview, DialogueScenarioContext scenarioContext, EnvironmentPromptConfig environmentConfig, IReadOnlyDictionary<string, object> additionalValues) => Parts.Slice1.ComposePromptWorkspace(rootChannel, promptChannel, includeNodes, deterministicPreview, scenarioContext, environmentConfig, additionalValues);
        internal void AddRuntimeMandatoryRaceProfileBlock(ICollection<PromptWorkspacePreviewBlock> blocks, string promptChannel, DialogueScenarioContext scenarioContext) => Parts.Slice1.AddRuntimeMandatoryRaceProfileBlock(blocks, promptChannel, scenarioContext);
        internal string ResolveWorkspaceContextEnvironmentText(RimTalkPromptChannel rootChannel, string normalizedChannel, DialogueScenarioContext scenarioContext) => Parts.Slice1.ResolveWorkspaceContextEnvironmentText(rootChannel, normalizedChannel, scenarioContext);
        internal void AddRuntimeDiplomacySupplementBlocks(ICollection<PromptWorkspacePreviewBlock> blocks, string promptChannel, DialogueScenarioContext scenarioContext, IReadOnlyDictionary<string, object> additionalValues) => Parts.Slice2.AddRuntimeDiplomacySupplementBlocks(blocks, promptChannel, scenarioContext, additionalValues);
        internal Pawn TryResolvePlayerNegotiator(IReadOnlyDictionary<string, object> additionalValues) => Parts.Slice2.TryResolvePlayerNegotiator(additionalValues);
        internal void AddRuntimeRpgMemorySupplementBlocks(ICollection<PromptWorkspacePreviewBlock> blocks, string promptChannel, DialogueScenarioContext scenarioContext) => Parts.Slice2.AddRuntimeRpgMemorySupplementBlocks(blocks, promptChannel, scenarioContext);
        internal void TryAddSingleTextNodeBlock(ICollection<PromptWorkspacePreviewBlock> blocks, string promptChannel, string nodeId, string content, PromptUnifiedNodeSlot slot, int order) => Parts.Slice2.TryAddSingleTextNodeBlock(blocks, promptChannel, nodeId, content, slot, order);
        internal PromptSectionAggregate BuildPromptSectionAggregateForCompose(RimTalkPromptChannel rootChannel, string promptChannel, bool deterministicPreview, DialogueScenarioContext scenarioContext, EnvironmentPromptConfig environmentConfig, IReadOnlyDictionary<string, object> additionalValues, Dictionary<string, object> cachedComposeValues = null) => Parts.Slice2.BuildPromptSectionAggregateForCompose(rootChannel, promptChannel, deterministicPreview, scenarioContext, environmentConfig, additionalValues, cachedComposeValues);
        internal IReadOnlyList<PromptSectionSchemaItem> GetOrderedSectionsForCompose(string promptChannel) => Parts.Slice2.GetOrderedSectionsForCompose(promptChannel);
        internal bool IsRpgModVariablesRawOutputSection(RimTalkPromptChannel rootChannel, string promptChannel, string sectionId) => Parts.Slice2.IsRpgModVariablesRawOutputSection(rootChannel, promptChannel, sectionId);
        internal string RenderRawModVariablesSection(string template, RimTalkPromptChannel rootChannel, string promptChannel, bool deterministicPreview, DialogueScenarioContext scenarioContext, EnvironmentPromptConfig environmentConfig, IReadOnlyDictionary<string, object> additionalValues, Dictionary<string, object> cachedComposeValues = null) => Parts.Slice2.RenderRawModVariablesSection(template, rootChannel, promptChannel, deterministicPreview, scenarioContext, environmentConfig, additionalValues, cachedComposeValues);
        internal string ConvertRawModVariableValueToText(object value) => Parts.Slice2.ConvertRawModVariableValueToText(value);
        internal List<ResolvedPromptNodePlacement> BuildPromptNodePlacementsForCompose(RimTalkPromptChannel rootChannel, string promptChannel, bool deterministicPreview, DialogueScenarioContext scenarioContext, EnvironmentPromptConfig environmentConfig, IReadOnlyDictionary<string, object> additionalValues, Dictionary<string, object> cachedComposeValues = null) => Parts.Slice2.BuildPromptNodePlacementsForCompose(rootChannel, promptChannel, deterministicPreview, scenarioContext, environmentConfig, additionalValues, cachedComposeValues);
        internal bool ShouldSuppressDiplomacyFallbackRoleNode(string normalizedChannel, DialogueScenarioContext scenarioContext) => Parts.Slice2.ShouldSuppressDiplomacyFallbackRoleNode(normalizedChannel, scenarioContext);
        internal void EnsureLayoutsContainAllowedNodes(string promptChannel, ICollection<PromptUnifiedNodeLayoutConfig> layouts) => Parts.Slice3.EnsureLayoutsContainAllowedNodes(promptChannel, layouts);
        internal bool TryBuildRuntimeAlignedPreviewNodePlacements(RimTalkPromptChannel rootChannel, string promptChannel, bool deterministicPreview, DialogueScenarioContext scenarioContext, out List<ResolvedPromptNodePlacement> placements) => Parts.Slice3.TryBuildRuntimeAlignedPreviewNodePlacements(rootChannel, promptChannel, deterministicPreview, scenarioContext, out placements);
        internal bool IsRuntimeMainChainChannel(string promptChannel) => Parts.Slice3.IsRuntimeMainChainChannel(promptChannel);
        internal DialogueScenarioContext CreateDeterministicPreviewScenarioContext(RimTalkPromptChannel rootChannel, string promptChannel) => Parts.Slice3.CreateDeterministicPreviewScenarioContext(rootChannel, promptChannel);
        internal bool IsProactivePromptChannel(string promptChannel) => Parts.Slice3.IsProactivePromptChannel(promptChannel);
        internal bool IsSectionOnlyChannel(string promptChannel) => Parts.Slice3.IsSectionOnlyChannel(promptChannel);
        internal string RenderUnifiedTemplate(string templateId, string promptChannel, string templateText, RimTalkPromptChannel rootChannel, bool deterministicPreview, DialogueScenarioContext scenarioContext, EnvironmentPromptConfig environmentConfig, IReadOnlyDictionary<string, object> additionalValues, Dictionary<string, object> cachedComposeValues = null) => Parts.Slice3.RenderUnifiedTemplate(templateId, promptChannel, templateText, rootChannel, deterministicPreview, scenarioContext, environmentConfig, additionalValues, cachedComposeValues);
        internal Dictionary<string, object> BuildCachedComposeValues(string promptChannel, RimTalkPromptChannel rootChannel, DialogueScenarioContext scenarioContext, EnvironmentPromptConfig environmentConfig) => Parts.Slice3.BuildCachedComposeValues(promptChannel, rootChannel, scenarioContext, environmentConfig);
        internal string RenderUnifiedTemplateLenient(string templateId, string promptChannel, string templateText, RimTalkPromptChannel rootChannel, bool deterministicPreview, DialogueScenarioContext scenarioContext, EnvironmentPromptConfig environmentConfig, IReadOnlyDictionary<string, object> additionalValues, Dictionary<string, object> cachedComposeValues = null) => Parts.Slice3.RenderUnifiedTemplateLenient(templateId, promptChannel, templateText, rootChannel, deterministicPreview, scenarioContext, environmentConfig, additionalValues, cachedComposeValues);
        internal Dictionary<string, object> BuildRuntimeComposeValues(string templateId, string renderChannel, string promptChannel, DialogueScenarioContext scenarioContext, EnvironmentPromptConfig environmentConfig, IReadOnlyDictionary<string, object> additionalValues) => Parts.Slice3.BuildRuntimeComposeValues(templateId, renderChannel, promptChannel, scenarioContext, environmentConfig, additionalValues);
        internal string BuildScenarioSignature(DialogueScenarioContext context) => Parts.Slice3.BuildScenarioSignature(context);
        internal void InjectRuntimeNodeBodies(IDictionary<string, object> values, string templateId, string promptChannel, DialogueScenarioContext scenarioContext) => Parts.Slice3.InjectRuntimeNodeBodies(values, templateId, promptChannel, scenarioContext);
        internal void ValidateRuntimePromptComposition(PromptWorkspaceComposeResult composed) => Parts.Slice4.ValidateRuntimePromptComposition(composed);
        internal IReadOnlyList<string> GetRequiredRuntimeNodeIds(string promptChannel) => Parts.Slice4.GetRequiredRuntimeNodeIds(promptChannel);
        internal string FindEnabledNodeContent(IEnumerable<ResolvedPromptNodePlacement> placements, string nodeId) => Parts.Slice4.FindEnabledNodeContent(placements, nodeId);
        internal string FindPreviewBlockContent(IEnumerable<PromptWorkspacePreviewBlock> blocks, string nodeId) => Parts.Slice4.FindPreviewBlockContent(blocks, nodeId);
        internal bool RequiresMandatoryRaceProfileBlock(string promptChannel) => Parts.Slice4.RequiresMandatoryRaceProfileBlock(promptChannel);
        internal Dictionary<string, object> BuildDeterministicComposeValues(string promptChannel, DialogueScenarioContext scenarioContext, IReadOnlyDictionary<string, object> additionalValues) => Parts.Slice4.BuildDeterministicComposeValues(promptChannel, scenarioContext, additionalValues);
        internal Dictionary<string, object> TryBuildFromSnapshot(string promptChannel) => Parts.Slice4.TryBuildFromSnapshot(promptChannel);
        internal Dictionary<string, object> TryBuildFromLastKnown(string promptChannel) => Parts.Slice4.TryBuildFromLastKnown(promptChannel);
        internal void MergeAdditionalValues(IDictionary<string, object> target, IReadOnlyDictionary<string, object> additionalValues) => Parts.Slice4.MergeAdditionalValues(target, additionalValues);
        internal PromptHierarchyNode BuildMainPromptSectionNodeForAggregate(IEnumerable<PromptSectionAggregateSection> sections) => Parts.Slice4.BuildMainPromptSectionNodeForAggregate(sections);
        internal string ResolveTemplateRenderChannel(string promptChannel, RimTalkPromptChannel rootChannel, DialogueScenarioContext scenarioContext) => Parts.Slice4.ResolveTemplateRenderChannel(promptChannel, rootChannel, scenarioContext);
        internal string ResolvePromptModeForCompose(DialogueScenarioContext scenarioContext, string promptChannel) => Parts.Slice4.ResolvePromptModeForCompose(scenarioContext, promptChannel);
        internal string InjectPromptPayloadBlock(string promptText, string payloadTag, string payloadText) => Parts.Slice4.InjectPromptPayloadBlock(promptText, payloadTag, payloadText);
        internal string SanitizePayloadTag(string payloadTag) => Parts.Slice4.SanitizePayloadTag(payloadTag);
        internal string EscapePromptXml(string value) => Parts.Slice4.EscapePromptXml(value);
        #endregion

        #region Facade forwards
        internal PromptHierarchyNode BuildMainChainPromptSectionNode(RimTalkPromptChannel rootChannel, SystemPromptConfig config, DialogueScenarioContext context, EnvironmentPromptConfig environmentConfig) => Parts.SectionAggregates.BuildMainChainPromptSectionNode(rootChannel, config, context, environmentConfig);
        internal PromptHierarchyNode BuildPromptSectionAggregateNode(SystemPromptConfig config, string promptChannel, DialogueScenarioContext context, EnvironmentPromptConfig environmentConfig) => Parts.SectionAggregates.BuildPromptSectionAggregateNode(config, promptChannel, context, environmentConfig);
        internal string BuildPromptSectionAggregatePreview(RimTalkPromptChannel rootChannel, string promptChannel) => Parts.SectionAggregates.BuildPromptSectionAggregatePreview(rootChannel, promptChannel);
        internal PromptWorkspaceStructuredPreview BuildPromptWorkspaceStructuredSectionPreview(RimTalkPromptChannel rootChannel, string promptChannel) => Parts.SectionAggregates.BuildPromptWorkspaceStructuredSectionPreview(rootChannel, promptChannel);
        internal PromptWorkspaceStructuredPreview BuildPromptWorkspaceStructuredLayoutPreview(RimTalkPromptChannel rootChannel, string promptChannel, out List<ResolvedPromptNodePlacement> placements) => Parts.SectionAggregates.BuildPromptWorkspaceStructuredLayoutPreview(rootChannel, promptChannel, out placements);
        internal string BuildPromptWorkspaceLayoutPreview(RimTalkPromptChannel rootChannel, string promptChannel, out List<ResolvedPromptNodePlacement> placements) => Parts.SectionAggregates.BuildPromptWorkspaceLayoutPreview(rootChannel, promptChannel, out placements);
        internal string BuildPromptWorkspaceContextBlock(string normalizedChannel) => Parts.SectionAggregates.BuildPromptWorkspaceContextBlock(normalizedChannel);
        internal string BuildPromptWorkspaceContextBlock(string normalizedChannel, string mode, string environment) => Parts.SectionAggregates.BuildPromptWorkspaceContextBlock(normalizedChannel, mode, environment);
        internal PromptSectionAggregate BuildPromptSectionAggregateForPreview(RimTalkPromptChannel rootChannel, string promptChannel) => Parts.SectionAggregates.BuildPromptSectionAggregateForPreview(rootChannel, promptChannel);
        internal PromptWorkspacePreviewBlock BuildSectionAggregateBlock(string promptChannel, string content, PromptSectionAggregate aggregate) => Parts.SectionAggregates.BuildSectionAggregateBlock(promptChannel, content, aggregate);
        internal IEnumerable<PromptWorkspacePreviewSubsection> BuildSectionAggregateSubsections(PromptSectionAggregate aggregate) => Parts.SectionAggregates.BuildSectionAggregateSubsections(aggregate);
        internal void AddPromptWorkspaceNodeBlocks(ICollection<PromptWorkspacePreviewBlock> blocks, IEnumerable<ResolvedPromptNodePlacement> placements, PromptUnifiedNodeSlot slot) => Parts.SectionAggregates.AddPromptWorkspaceNodeBlocks(blocks, placements, slot);
        internal string WrapNodeContentWithXml(string nodeId, string content) => Parts.SectionAggregates.WrapNodeContentWithXml(nodeId, content);
        internal string NormalizeNodeIdToXmlTag(string nodeId) => Parts.SectionAggregates.NormalizeNodeIdToXmlTag(nodeId);
        internal string IndentMultilineContent(string content, int spaces) => Parts.SectionAggregates.IndentMultilineContent(content, spaces);
        internal List<PromptWorkspacePreviewBlock> ReorderWorkspacePreviewBlocks(IEnumerable<PromptWorkspacePreviewBlock> blocks) => Parts.SectionAggregates.ReorderWorkspacePreviewBlocks(blocks);
        internal string BuildPreviewSignature(string normalizedChannel, IEnumerable<PromptWorkspacePreviewBlock> blocks) => Parts.SectionAggregates.BuildPreviewSignature(normalizedChannel, blocks);
        internal string BuildTextSignature(string text) => Parts.SectionAggregates.BuildTextSignature(text);
        internal int ComputeStableHash(string text) => Parts.SectionAggregates.ComputeStableHash(text);
        internal string RenderStructuredPreviewAsText(PromptWorkspaceStructuredPreview preview) => Parts.SectionAggregates.RenderStructuredPreviewAsText(preview);
        internal string RenderPromptSectionAggregateSection(string promptChannel, string sectionId, string templateText, DialogueScenarioContext context, EnvironmentPromptConfig environmentConfig) => Parts.SectionAggregates.RenderPromptSectionAggregateSection(promptChannel, sectionId, templateText, context, environmentConfig);
        internal RimTalkPromptEntryDefaultsConfig GetRuntimePromptSectionCatalog(SystemPromptConfig config) => Parts.SectionAggregates.GetRuntimePromptSectionCatalog(config);
        internal bool SyncLegacyPromptMirrorsFromSections(SystemPromptConfig config) => Parts.SectionAggregates.SyncLegacyPromptMirrorsFromSections(config);
        internal string BuildLegacyPromptMirrorText(string promptChannel, params string[] sectionIds) => Parts.SectionAggregates.BuildLegacyPromptMirrorText(promptChannel, sectionIds);
        internal PromptWorkspaceIncrementalPreviewBuildState CreatePromptWorkspaceIncrementalPreviewBuild(RimTalkPromptChannel rootChannel, string promptChannel) => Parts.WorkspacePreviewIncremental.CreatePromptWorkspaceIncrementalPreviewBuild(rootChannel, promptChannel);
        internal void StepPromptWorkspaceIncrementalPreviewBuild(PromptWorkspaceIncrementalPreviewBuildState state) => Parts.WorkspacePreviewIncremental.StepPromptWorkspaceIncrementalPreviewBuild(state);
        internal void StepBuildStateCore(PromptWorkspaceIncrementalPreviewBuildState state) => Parts.WorkspacePreviewIncremental.StepBuildStateCore(state);
        internal void RecordStepErrorAndAdvance(PromptWorkspaceIncrementalPreviewBuildState state, PromptWorkspacePreviewErrorDiagnostic diagnostic) => Parts.WorkspacePreviewIncremental.RecordStepErrorAndAdvance(state, diagnostic);
        internal void AdvanceStageAfterError(PromptWorkspaceIncrementalPreviewBuildState state) => Parts.WorkspacePreviewIncremental.AdvanceStageAfterError(state);
        internal void StepInitStage(PromptWorkspaceIncrementalPreviewBuildState state) => Parts.WorkspacePreviewIncremental.StepInitStage(state);
        internal void StepSectionStage(PromptWorkspaceIncrementalPreviewBuildState state) => Parts.WorkspacePreviewIncremental.StepSectionStage(state);
        internal void StepNodeStage(PromptWorkspaceIncrementalPreviewBuildState state) => Parts.WorkspacePreviewIncremental.StepNodeStage(state);
        internal void StepFinalizeStage(PromptWorkspaceIncrementalPreviewBuildState state) => Parts.WorkspacePreviewIncremental.StepFinalizeStage(state);
        internal string RenderPreviewSectionStep(RimTalkPromptChannel rootChannel, string promptChannel, string sectionId, Dictionary<string, object> cachedComposeValues) => Parts.WorkspacePreviewIncremental.RenderPreviewSectionStep(rootChannel, promptChannel, sectionId, cachedComposeValues);
        internal PromptWorkspacePreviewBlock RenderPreviewNodeStep(RimTalkPromptChannel rootChannel, string promptChannel, PromptUnifiedNodeLayoutConfig layout, Dictionary<string, object> cachedComposeValues) => Parts.WorkspacePreviewIncremental.RenderPreviewNodeStep(rootChannel, promptChannel, layout, cachedComposeValues);
        internal void EnsureBuildStateComposeValues(PromptWorkspaceIncrementalPreviewBuildState state) => Parts.WorkspacePreviewIncremental.EnsureBuildStateComposeValues(state);
        internal void EnsureFooterBlock(ICollection<PromptWorkspacePreviewBlock> blocks, string promptChannel) => Parts.WorkspacePreviewIncremental.EnsureFooterBlock(blocks, promptChannel);
        internal int UpdateSectionAggregatePreviewBlock(PromptWorkspaceIncrementalPreviewBuildState state) => Parts.WorkspacePreviewIncremental.UpdateSectionAggregatePreviewBlock(state);
        internal PromptSectionAggregate BuildSectionAggregateSnapshot(string promptChannel, IEnumerable<PromptSectionAggregateSection> sections) => Parts.WorkspacePreviewIncremental.BuildSectionAggregateSnapshot(promptChannel, sections);
        internal IReadOnlyList<PromptSectionSchemaItem> GetOrderedSectionsForPreview(string promptChannel) => Parts.WorkspacePreviewIncremental.GetOrderedSectionsForPreview(promptChannel);
        internal List<PromptUnifiedNodeLayoutConfig> GetOrderedNodeLayoutsForPreview(string promptChannel) => Parts.WorkspacePreviewIncremental.GetOrderedNodeLayoutsForPreview(promptChannel);
        internal void MarkBuildFailed(PromptWorkspaceIncrementalPreviewBuildState state, PromptWorkspacePreviewErrorDiagnostic diagnostic) => Parts.WorkspacePreviewIncremental.MarkBuildFailed(state, diagnostic);
        internal PromptWorkspacePreviewErrorDiagnostic BuildErrorDiagnostic(PromptRenderException ex) => Parts.WorkspacePreviewIncremental.BuildErrorDiagnostic(ex);
        internal PromptWorkspacePreviewErrorDiagnostic BuildErrorDiagnostic(Exception ex, string channel) => Parts.WorkspacePreviewIncremental.BuildErrorDiagnostic(ex, channel);
        internal void UpdateBuildProgress(PromptWorkspaceIncrementalPreviewBuildState state) => Parts.WorkspacePreviewIncremental.UpdateBuildProgress(state);
        internal void UpdateBuildSignature(PromptWorkspaceIncrementalPreviewBuildState state) => Parts.WorkspacePreviewIncremental.UpdateBuildSignature(state);
        internal string UpdatePreviewSignatureIncremental(PromptWorkspaceIncrementalPreviewBuildState state) => Parts.WorkspacePreviewIncremental.UpdatePreviewSignatureIncremental(state);
        internal void EnsureBlockSignatureCacheCapacity(PromptWorkspaceIncrementalPreviewBuildState state, int count) => Parts.WorkspacePreviewIncremental.EnsureBlockSignatureCacheCapacity(state, count);
        internal void InvalidateIncrementalSignatureCache(PromptWorkspaceIncrementalPreviewBuildState state) => Parts.WorkspacePreviewIncremental.InvalidateIncrementalSignatureCache(state);
        internal void MarkBlockDirty(PromptWorkspaceIncrementalPreviewBuildState state, int index) => Parts.WorkspacePreviewIncremental.MarkBlockDirty(state, index);
        internal int ComputePreviewBlockSignatureHash(PromptWorkspaceIncrementalPreviewBuildState state, int blockIndex, PromptWorkspacePreviewBlock block) => Parts.WorkspacePreviewIncremental.ComputePreviewBlockSignatureHash(state, blockIndex, block);
        internal int ComputePreviewSubsectionSignatureHash(PromptWorkspacePreviewSubsection subsection) => Parts.WorkspacePreviewIncremental.ComputePreviewSubsectionSignatureHash(subsection);
        internal int ComputePreviewAggregateHash(string channel, List<int> blockHashes, int count) => Parts.WorkspacePreviewIncremental.ComputePreviewAggregateHash(channel, blockHashes, count);
        internal int BeginHash() => Parts.WorkspacePreviewIncremental.BeginHash();
        internal int MixHash(int hash, int value) => Parts.WorkspacePreviewIncremental.MixHash(hash, value);
        internal int ComputeStableSignatureHash(string text) => Parts.WorkspacePreviewIncremental.ComputeStableSignatureHash(text);
        #endregion
}
    internal sealed class PromptWorkspaceComposerParts
    {
        internal readonly PromptWorkspaceComposer Owner;
        internal readonly PromptWorkspaceComposerSectionAggregates SectionAggregates;
        internal readonly PromptWorkspaceComposerWorkspacePreviewIncremental WorkspacePreviewIncremental;
        internal readonly PromptWorkspaceSlice1 Slice1;
        internal readonly PromptWorkspaceSlice2 Slice2;
        internal readonly PromptWorkspaceSlice3 Slice3;
        internal readonly PromptWorkspaceSlice4 Slice4;
        internal PromptWorkspaceComposerParts(PromptWorkspaceComposer owner)
        {
            Owner = owner;
            SectionAggregates = new PromptWorkspaceComposerSectionAggregates(owner);
            WorkspacePreviewIncremental = new PromptWorkspaceComposerWorkspacePreviewIncremental(owner);
            Slice1 = new PromptWorkspaceSlice1(owner);
            Slice2 = new PromptWorkspaceSlice2(owner);
            Slice3 = new PromptWorkspaceSlice3(owner);
            Slice4 = new PromptWorkspaceSlice4(owner);
        }
    }


    internal sealed class PromptWorkspaceComposeResult
    {
        public string PromptChannel = string.Empty;
        public PromptSectionAggregate Aggregate;
        public List<ResolvedPromptNodePlacement> Placements = new List<ResolvedPromptNodePlacement>();
        public PromptWorkspaceStructuredPreview Preview = new PromptWorkspaceStructuredPreview();
    }

}
