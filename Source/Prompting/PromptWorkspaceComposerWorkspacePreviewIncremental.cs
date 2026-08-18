using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Prompting;
using Verse;

using Ustas.RimAI.Communication.Relations.Persistence;

namespace Ustas.RimAI.Communication.Relations.Prompting
{
    internal sealed class PromptWorkspaceComposerWorkspacePreviewIncremental : PromptWorkspaceComposerCollaborator
    {
        internal PromptWorkspaceComposerWorkspacePreviewIncremental(PromptWorkspaceComposer owner) : base(owner)
        {
        }


        internal PromptWorkspaceIncrementalPreviewBuildState CreatePromptWorkspaceIncrementalPreviewBuild(
            RimTalkPromptChannel rootChannel,
            string promptChannel)
        {
            string normalizedChannel = PromptSectionSchemaCatalog.NormalizeWorkspaceChannel(promptChannel, rootChannel);
            var state = new PromptWorkspaceIncrementalPreviewBuildState
            {
                RootChannel = rootChannel,
                PromptChannel = normalizedChannel,
                IncludeNodes = !Owner.IsSectionOnlyChannel(normalizedChannel)
            };
            state.Sections.AddRange(Owner.GetOrderedSectionsForPreview(normalizedChannel));
            if (state.IncludeNodes)
            {
                state.NodeLayouts.AddRange(Owner.GetOrderedNodeLayoutsForPreview(normalizedChannel));
            }

            state.Preview.Stage = PromptWorkspacePreviewBuildStage.Init;
            Owner.UpdateBuildProgress(state);
            Owner.UpdateBuildSignature(state);
            return state;
        }

        internal void StepPromptWorkspaceIncrementalPreviewBuild(PromptWorkspaceIncrementalPreviewBuildState state)
        {
            if (state == null)
            {
                return;
            }

            if (state.Preview.Stage == PromptWorkspacePreviewBuildStage.Completed ||
                state.Preview.Stage == PromptWorkspacePreviewBuildStage.Failed)
            {
                return;
            }

            Owner.StepBuildStateCore(state);
        }

        internal void StepBuildStateCore(PromptWorkspaceIncrementalPreviewBuildState state)
        {
            try
            {
                switch (state.Preview.Stage)
                {
                    case PromptWorkspacePreviewBuildStage.Init:
                        Owner.StepInitStage(state);
                        return;
                    case PromptWorkspacePreviewBuildStage.Sections:
                        Owner.StepSectionStage(state);
                        return;
                    case PromptWorkspacePreviewBuildStage.Nodes:
                        Owner.StepNodeStage(state);
                        return;
                    case PromptWorkspacePreviewBuildStage.Finalize:
                        Owner.StepFinalizeStage(state);
                        return;
                }
            }
            catch (PromptRenderException ex)
            {
                Owner.RecordStepErrorAndAdvance(state, Owner.BuildErrorDiagnostic(ex));
            }
            catch (Exception ex)
            {
                Owner.RecordStepErrorAndAdvance(state, Owner.BuildErrorDiagnostic(ex, state.PromptChannel));
            }
        }

        internal void RecordStepErrorAndAdvance(
            PromptWorkspaceIncrementalPreviewBuildState state,
            PromptWorkspacePreviewErrorDiagnostic diagnostic)
        {
            // Record the error but continue building the preview.
            // Keep the first error diagnostic but don't mark the entire build as failed.
            if (state.Preview.ErrorDiagnostic == null)
            {
                state.Preview.ErrorDiagnostic = diagnostic;
            }

            // Add error block for this stage
            state.Preview.Blocks.Add(new PromptWorkspacePreviewBlock
            {
                Kind = PromptWorkspacePreviewBlockKind.Error,
                PromptChannel = state.PromptChannel,
                Content = "RimChat_PromptWorkspacePreviewBuild_ErrorBody".Translate(
                    diagnostic?.TemplateId ?? string.Empty,
                    diagnostic?.Channel ?? string.Empty,
                    diagnostic?.ErrorLine ?? 0,
                    diagnostic?.ErrorColumn ?? 0,
                    diagnostic?.Message ?? string.Empty).ToString()
            });

            // Advance to the next stage instead of failing entirely
            Owner.AdvanceStageAfterError(state);
        }

        internal void AdvanceStageAfterError(PromptWorkspaceIncrementalPreviewBuildState state)
        {
            switch (state.Preview.Stage)
            {
                case PromptWorkspacePreviewBuildStage.Init:
                    state.Preview.Stage = PromptWorkspacePreviewBuildStage.Sections;
                    break;
                case PromptWorkspacePreviewBuildStage.Sections:
                    state.Preview.Stage = PromptWorkspacePreviewBuildStage.Nodes;
                    break;
                case PromptWorkspacePreviewBuildStage.Nodes:
                    Owner.StepFinalizeStage(state);
                    break;
                default:
                    state.Preview.Stage = PromptWorkspacePreviewBuildStage.Completed;
                    break;
            }
        }

        internal void StepInitStage(PromptWorkspaceIncrementalPreviewBuildState state)
        {
            state.Preview.Blocks.Add(new PromptWorkspacePreviewBlock
            {
                Kind = PromptWorkspacePreviewBlockKind.Context,
                PromptChannel = state.PromptChannel,
                Content = Owner.BuildPromptWorkspaceContextBlock(
                    state.PromptChannel,
                    "manual",
                    "{{ runtime.environment }}")
            });
            Owner.MarkBlockDirty(state, state.Preview.Blocks.Count - 1);
            state.Preview.Stage = state.Sections.Count > 0
                ? PromptWorkspacePreviewBuildStage.Sections
                : state.IncludeNodes && state.NodeLayouts.Count > 0
                    ? PromptWorkspacePreviewBuildStage.Nodes
                    : PromptWorkspacePreviewBuildStage.Finalize;
            Owner.UpdateBuildProgress(state);
            Owner.UpdateBuildSignature(state);
        }

        internal void StepSectionStage(PromptWorkspaceIncrementalPreviewBuildState state)
        {
            if (state.SectionCursor >= state.Sections.Count)
            {
                state.Preview.Stage = state.IncludeNodes && state.NodeLayouts.Count > 0
                    ? PromptWorkspacePreviewBuildStage.Nodes
                    : PromptWorkspacePreviewBuildStage.Finalize;
                Owner.UpdateBuildProgress(state);
                Owner.UpdateBuildSignature(state);
                return;
            }

            Owner.EnsureBuildStateComposeValues(state);
            PromptSectionSchemaItem section = state.Sections[state.SectionCursor];
            string rendered = Owner.RenderPreviewSectionStep(state.RootChannel, state.PromptChannel, section.Id, state.CachedComposeValues);
            if (!string.IsNullOrWhiteSpace(rendered))
            {
                state.RenderedSections.Add(new PromptSectionAggregateSection
                {
                    SectionId = section.Id,
                    SectionLabel = section.EnglishName,
                    Content = rendered.Trim()
                });
            }

            state.SectionCursor++;
            int aggregateBlockIndex = Owner.UpdateSectionAggregatePreviewBlock(state);
            if (aggregateBlockIndex >= 0)
            {
                Owner.MarkBlockDirty(state, aggregateBlockIndex);
            }
            state.Preview.Stage = state.SectionCursor >= state.Sections.Count
                ? state.IncludeNodes && state.NodeLayouts.Count > 0
                    ? PromptWorkspacePreviewBuildStage.Nodes
                    : PromptWorkspacePreviewBuildStage.Finalize
                : PromptWorkspacePreviewBuildStage.Sections;
            Owner.UpdateBuildProgress(state);
            Owner.UpdateBuildSignature(state);
        }

        internal void StepNodeStage(PromptWorkspaceIncrementalPreviewBuildState state)
        {
            if (state.NodeCursor >= state.NodeLayouts.Count)
            {
                state.Preview.Stage = PromptWorkspacePreviewBuildStage.Finalize;
                Owner.UpdateBuildProgress(state);
                Owner.UpdateBuildSignature(state);
                return;
            }

            Owner.EnsureBuildStateComposeValues(state);
            PromptUnifiedNodeLayoutConfig layout = state.NodeLayouts[state.NodeCursor];
            PromptWorkspacePreviewBlock nodeBlock = Owner.RenderPreviewNodeStep(
                state.RootChannel,
                state.PromptChannel,
                layout,
                state.CachedComposeValues);
            if (nodeBlock != null)
            {
                state.Preview.Blocks.Add(nodeBlock);
                Owner.MarkBlockDirty(state, state.Preview.Blocks.Count - 1);
            }

            state.NodeCursor++;
            state.Preview.Stage = state.NodeCursor >= state.NodeLayouts.Count
                ? PromptWorkspacePreviewBuildStage.Finalize
                : PromptWorkspacePreviewBuildStage.Nodes;
            Owner.UpdateBuildProgress(state);
            Owner.UpdateBuildSignature(state);
        }

        internal void StepFinalizeStage(PromptWorkspaceIncrementalPreviewBuildState state)
        {
            Owner.EnsureFooterBlock(state.Preview.Blocks, state.PromptChannel);
            state.Preview.Blocks = Owner.ReorderWorkspacePreviewBlocks(state.Preview.Blocks);
            Owner.InvalidateIncrementalSignatureCache(state);
            state.Preview.UsesSnapshotData = PromptRequestSnapshotCache.HasSnapshotForChannel(state.PromptChannel);
            state.Preview.Stage = PromptWorkspacePreviewBuildStage.Completed;
            Owner.UpdateBuildProgress(state);
            Owner.UpdateBuildSignature(state);
        }

        internal string RenderPreviewSectionStep(
            RimTalkPromptChannel rootChannel,
            string promptChannel,
            string sectionId,
            Dictionary<string, object> cachedComposeValues)
        {
            string template = RelationsMod.Settings?.ResolvePromptSectionText(promptChannel, sectionId) ?? string.Empty;
            bool rawModVariablesSection = Owner.IsRpgModVariablesRawOutputSection(rootChannel, promptChannel, sectionId);
            return rawModVariablesSection
                ? Owner.RenderRawModVariablesSection(
                    template,
                    rootChannel,
                    promptChannel,
                    deterministicPreview: true,
                    scenarioContext: null,
                    environmentConfig: null,
                    additionalValues: null,
                    cachedComposeValues: cachedComposeValues)
                : Owner.RenderUnifiedTemplate(
                    $"prompt_sections.{promptChannel}.{sectionId}",
                    promptChannel,
                    template,
                    rootChannel,
                    deterministicPreview: true,
                    scenarioContext: null,
                    environmentConfig: null,
                    additionalValues: null,
                    cachedComposeValues: cachedComposeValues);
        }

        internal PromptWorkspacePreviewBlock RenderPreviewNodeStep(
            RimTalkPromptChannel rootChannel,
            string promptChannel,
            PromptUnifiedNodeLayoutConfig layout,
            Dictionary<string, object> cachedComposeValues)
        {
            if (layout == null)
            {
                return null;
            }

            string nodeId = layout.NodeId ?? string.Empty;
            string template = RelationsMod.Settings?.ResolvePromptNodeText(promptChannel, nodeId) ?? string.Empty;
            string rendered = Owner.RenderUnifiedTemplate(
                $"prompt_nodes.{promptChannel}.{nodeId}",
                promptChannel,
                template,
                rootChannel,
                deterministicPreview: true,
                scenarioContext: null,
                environmentConfig: null,
                additionalValues: null,
                cachedComposeValues: cachedComposeValues);
            if (!layout.Enabled || string.IsNullOrWhiteSpace(rendered))
            {
                return null;
            }

            return new PromptWorkspacePreviewBlock
            {
                Kind = PromptWorkspacePreviewBlockKind.Node,
                PromptChannel = promptChannel,
                NodeId = nodeId,
                Slot = layout.GetSlot(),
                Order = layout.Order,
                Content = rendered.Trim()
            };
        }

        internal void EnsureBuildStateComposeValues(PromptWorkspaceIncrementalPreviewBuildState state)
        {
            if (state.ComposeValuesInitialized)
            {
                return;
            }

            state.CachedComposeValues = Owner.BuildDeterministicComposeValues(
                state.PromptChannel,
                scenarioContext: null,
                additionalValues: null);
            state.ComposeValuesInitialized = true;
        }

        internal void EnsureFooterBlock(ICollection<PromptWorkspacePreviewBlock> blocks, string promptChannel)
        {
            if (blocks == null || blocks.Any(block => block?.Kind == PromptWorkspacePreviewBlockKind.Footer))
            {
                return;
            }

            blocks.Add(new PromptWorkspacePreviewBlock
            {
                Kind = PromptWorkspacePreviewBlockKind.Footer,
                PromptChannel = promptChannel,
                Content = "</prompt_context>"
            });
        }

        internal int UpdateSectionAggregatePreviewBlock(PromptWorkspaceIncrementalPreviewBuildState state)
        {
            PromptSectionAggregate aggregate = Owner.BuildSectionAggregateSnapshot(state.PromptChannel, state.RenderedSections);
            string content = aggregate?.RenderedText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(content))
            {
                return -1;
            }

            PromptWorkspacePreviewBlock block = Owner.BuildSectionAggregateBlock(state.PromptChannel, content, aggregate);
            List<PromptWorkspacePreviewBlock> blocks = state.Preview.Blocks;
            int index = blocks.FindIndex(item => item?.Kind == PromptWorkspacePreviewBlockKind.SectionAggregate);
            if (index >= 0)
            {
                blocks[index] = block;
                return index;
            }

            blocks.Add(block);
            return blocks.Count - 1;
        }

        internal PromptSectionAggregate BuildSectionAggregateSnapshot(
            string promptChannel,
            IEnumerable<PromptSectionAggregateSection> sections)
        {
            var aggregate = new PromptSectionAggregate
            {
                PromptChannel = promptChannel ?? string.Empty
            };
            aggregate.Sections.AddRange(sections ?? Enumerable.Empty<PromptSectionAggregateSection>());
            aggregate.RenderedText = PromptHierarchyRenderer.Render(
                Owner.BuildMainPromptSectionNodeForAggregate(aggregate.Sections));
            return aggregate;
        }

        internal IReadOnlyList<PromptSectionSchemaItem> GetOrderedSectionsForPreview(string promptChannel)
        {
            List<PromptSectionLayoutConfig> sectionLayouts =
                RelationsMod.Settings?.GetPromptSectionLayouts(promptChannel) ?? new List<PromptSectionLayoutConfig>();
            return PromptSectionSchemaCatalog.GetOrderedMainChainSections(sectionLayouts, enabledOnly: true);
        }

        internal List<PromptUnifiedNodeLayoutConfig> GetOrderedNodeLayoutsForPreview(string promptChannel)
        {
            List<PromptUnifiedNodeLayoutConfig> layouts =
                RelationsMod.Settings?.GetPromptNodeLayouts(promptChannel) ??
                PromptUnifiedNodeSchemaCatalog.GetAllowedNodes(promptChannel)
                    .Select(item => PromptUnifiedNodeLayoutDefaults.BuildDefaultLayout(promptChannel, item.Id))
                    .ToList();
            Owner.EnsureLayoutsContainAllowedNodes(promptChannel, layouts);
            return layouts
                .Where(item => item != null)
                .OrderBy(item => item.GetSlot())
                .ThenBy(item => item.Order)
                .ThenBy(item => item.NodeId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        internal void MarkBuildFailed(
            PromptWorkspaceIncrementalPreviewBuildState state,
            PromptWorkspacePreviewErrorDiagnostic diagnostic)
        {
            state.Preview.ErrorDiagnostic = diagnostic;
            state.Preview.Stage = PromptWorkspacePreviewBuildStage.Failed;
            state.Preview.Blocks.Add(new PromptWorkspacePreviewBlock
            {
                Kind = PromptWorkspacePreviewBlockKind.Error,
                PromptChannel = state.PromptChannel,
                Content = "RimChat_PromptWorkspacePreviewBuild_ErrorBody".Translate(
                    diagnostic?.TemplateId ?? string.Empty,
                    diagnostic?.Channel ?? string.Empty,
                    diagnostic?.ErrorLine ?? 0,
                    diagnostic?.ErrorColumn ?? 0,
                    diagnostic?.Message ?? string.Empty).ToString()
            });
            Owner.EnsureFooterBlock(state.Preview.Blocks, state.PromptChannel);
            state.Preview.Blocks = Owner.ReorderWorkspacePreviewBlocks(state.Preview.Blocks);
            Owner.InvalidateIncrementalSignatureCache(state);
            Owner.UpdateBuildProgress(state);
            Owner.UpdateBuildSignature(state);
        }

        internal PromptWorkspacePreviewErrorDiagnostic BuildErrorDiagnostic(PromptRenderException ex)
        {
            return new PromptWorkspacePreviewErrorDiagnostic
            {
                TemplateId = ex?.TemplateId ?? string.Empty,
                Channel = ex?.Channel ?? string.Empty,
                ErrorCode = (int)(ex?.ErrorCode ?? PromptRenderErrorCode.RuntimeError),
                ErrorLine = ex?.ErrorLine ?? 0,
                ErrorColumn = ex?.ErrorColumn ?? 0,
                Message = ex?.Message ?? string.Empty
            };
        }

        internal PromptWorkspacePreviewErrorDiagnostic BuildErrorDiagnostic(Exception ex, string channel)
        {
            return new PromptWorkspacePreviewErrorDiagnostic
            {
                TemplateId = "prompt_workspace.preview",
                Channel = channel ?? string.Empty,
                ErrorCode = 0,
                ErrorLine = 0,
                ErrorColumn = 0,
                Message = ex?.Message ?? "unknown_error"
            };
        }

        internal void UpdateBuildProgress(PromptWorkspaceIncrementalPreviewBuildState state)
        {
            PromptWorkspaceStructuredPreview preview = state.Preview;
            preview.TotalSections = state.Sections.Count;
            preview.CompletedSections = Math.Min(state.SectionCursor, preview.TotalSections);
            preview.TotalNodes = state.NodeLayouts.Count;
            preview.CompletedNodes = Math.Min(state.NodeCursor, preview.TotalNodes);
            preview.Total = preview.TotalSections + preview.TotalNodes;
            preview.Completed = preview.CompletedSections + preview.CompletedNodes;
            preview.IsFailed = preview.Stage == PromptWorkspacePreviewBuildStage.Failed;
            preview.IsBuilding = preview.Stage != PromptWorkspacePreviewBuildStage.Completed && !preview.IsFailed;
        }

        internal void UpdateBuildSignature(PromptWorkspaceIncrementalPreviewBuildState state)
        {
            PromptWorkspaceStructuredPreview preview = state.Preview;
            string baseSignature = Owner.UpdatePreviewSignatureIncremental(state);
            string progress = "|build:" + (int)preview.Stage +
                ":" + preview.Completed + "/" + preview.Total +
                ":s" + preview.CompletedSections + "/" + preview.TotalSections +
                ":n" + preview.CompletedNodes + "/" + preview.TotalNodes +
                ":failed=" + (preview.IsFailed ? 1 : 0);
            PromptWorkspacePreviewErrorDiagnostic error = preview.ErrorDiagnostic;
            if (error != null)
            {
                progress += ":err=" + error.ErrorCode + ":" + error.ErrorLine + ":" + error.ErrorColumn;
            }

            preview.Signature = baseSignature + progress;
        }

        internal string UpdatePreviewSignatureIncremental(PromptWorkspaceIncrementalPreviewBuildState state)
        {
            PromptWorkspaceStructuredPreview preview = state.Preview;
            if (!state.SignatureCacheInitialized)
            {
                state.DirtyBlockIndices.Clear();
                for (int i = 0; i < preview.Blocks.Count; i++)
                {
                    state.DirtyBlockIndices.Add(i);
                }

                state.SignatureCacheInitialized = true;
            }

            Owner.EnsureBlockSignatureCacheCapacity(state, preview.Blocks.Count);
            foreach (int dirtyIndex in state.DirtyBlockIndices.ToList())
            {
                if (dirtyIndex < 0 || dirtyIndex >= preview.Blocks.Count)
                {
                    continue;
                }

                PromptWorkspacePreviewBlock block = preview.Blocks[dirtyIndex];
                state.BlockSignatureHashes[dirtyIndex] = Owner.ComputePreviewBlockSignatureHash(state, dirtyIndex, block);
            }

            state.DirtyBlockIndices.Clear();
            int aggregateHash = Owner.ComputePreviewAggregateHash(state.PromptChannel, state.BlockSignatureHashes, preview.Blocks.Count);
            return "channel=" + (state.PromptChannel ?? string.Empty) +
                "|agg=" + aggregateHash.ToString("X8") +
                "|blocks=" + preview.Blocks.Count;
        }

        internal void EnsureBlockSignatureCacheCapacity(PromptWorkspaceIncrementalPreviewBuildState state, int count)
        {
            count = Math.Max(0, count);
            while (state.BlockSignatureHashes.Count < count)
            {
                state.BlockSignatureHashes.Add(0);
            }

            if (state.BlockSignatureHashes.Count > count)
            {
                state.BlockSignatureHashes.RemoveRange(count, state.BlockSignatureHashes.Count - count);
            }

            if (state.SubsectionSignatureHashesByBlock.Count == 0)
            {
                return;
            }

            var staleKeys = state.SubsectionSignatureHashesByBlock.Keys
                .Where(key => key < 0 || key >= count)
                .ToList();
            for (int i = 0; i < staleKeys.Count; i++)
            {
                state.SubsectionSignatureHashesByBlock.Remove(staleKeys[i]);
            }
        }

        internal void InvalidateIncrementalSignatureCache(PromptWorkspaceIncrementalPreviewBuildState state)
        {
            state.SignatureCacheInitialized = false;
            state.DirtyBlockIndices.Clear();
            state.SubsectionSignatureHashesByBlock.Clear();
        }

        internal void MarkBlockDirty(PromptWorkspaceIncrementalPreviewBuildState state, int index)
        {
            if (state == null || index < 0)
            {
                return;
            }

            state.DirtyBlockIndices.Add(index);
        }

        internal int ComputePreviewBlockSignatureHash(
            PromptWorkspaceIncrementalPreviewBuildState state,
            int blockIndex,
            PromptWorkspacePreviewBlock block)
        {
            if (block == null)
            {
                return 0;
            }

            int hash = Owner.BeginHash();
            hash = Owner.MixHash(hash, (int)block.Kind);
            hash = Owner.MixHash(hash, Owner.ComputeStableSignatureHash(block.PromptChannel));
            hash = Owner.MixHash(hash, Owner.ComputeStableSignatureHash(block.NodeId));
            hash = Owner.MixHash(hash, (int)block.Slot);
            hash = Owner.MixHash(hash, block.Order);
            hash = Owner.MixHash(hash, Owner.ComputeStableSignatureHash(block.Content));

            List<PromptWorkspacePreviewSubsection> subsections = block.Subsections ?? new List<PromptWorkspacePreviewSubsection>();
            List<int> subsectionCache;
            if (!state.SubsectionSignatureHashesByBlock.TryGetValue(blockIndex, out subsectionCache))
            {
                subsectionCache = new List<int>();
                state.SubsectionSignatureHashesByBlock[blockIndex] = subsectionCache;
            }

            while (subsectionCache.Count < subsections.Count)
            {
                subsectionCache.Add(0);
            }

            if (subsectionCache.Count > subsections.Count)
            {
                subsectionCache.RemoveRange(subsections.Count, subsectionCache.Count - subsections.Count);
            }

            hash = Owner.MixHash(hash, subsections.Count);
            for (int i = 0; i < subsections.Count; i++)
            {
                PromptWorkspacePreviewSubsection subsection = subsections[i];
                int subsectionHash = Owner.ComputePreviewSubsectionSignatureHash(subsection);
                subsectionCache[i] = subsectionHash;
                hash = Owner.MixHash(hash, subsectionHash);
            }

            return hash;
        }

        internal int ComputePreviewSubsectionSignatureHash(PromptWorkspacePreviewSubsection subsection)
        {
            if (subsection == null)
            {
                return 0;
            }

            int hash = Owner.BeginHash();
            hash = Owner.MixHash(hash, Owner.ComputeStableSignatureHash(subsection.SectionId));
            hash = Owner.MixHash(hash, Owner.ComputeStableSignatureHash(subsection.Content));
            return hash;
        }

        internal int ComputePreviewAggregateHash(string channel, List<int> blockHashes, int count)
        {
            int hash = Owner.BeginHash();
            hash = Owner.MixHash(hash, Owner.ComputeStableSignatureHash(channel));
            hash = Owner.MixHash(hash, count);
            for (int i = 0; i < count; i++)
            {
                hash = Owner.MixHash(hash, i);
                hash = Owner.MixHash(hash, blockHashes[i]);
            }

            return hash;
        }

        internal int BeginHash()
        {
            unchecked
            {
                return (int)2166136261;
            }
        }

        internal int MixHash(int hash, int value)
        {
            unchecked
            {
                hash ^= value;
                hash *= 16777619;
                return hash;
            }
        }

        internal int ComputeStableSignatureHash(string text)
        {
            unchecked
            {
                int hash = Owner.BeginHash();
                string source = text ?? string.Empty;
                for (int i = 0; i < source.Length; i++)
                {
                    hash = Owner.MixHash(hash, source[i]);
                }

                return hash;
            }
        }
        }

}
