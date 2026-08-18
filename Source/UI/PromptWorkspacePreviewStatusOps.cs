using System;
using System.Collections.Generic;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Persistence;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Prompting;

namespace Ustas.RimAI.Communication.Relations.UI
{
    /// <summary>
    /// Structured preview status/progress text helpers.
    /// </summary>
    internal static class PromptWorkspacePreviewStatusOps
    {
        internal static bool ShouldDrawStatus(PromptWorkspaceStructuredPreview preview)
        {
            return preview != null &&
                (preview.IsBuilding || preview.IsFailed || preview.Total > 0 || preview.Stage == PromptWorkspacePreviewBuildStage.Completed);
        }

        internal static float ResolveProgress(PromptWorkspaceStructuredPreview preview)
        {
            if (preview == null)
            {
                return 0f;
            }

            if (preview.Total <= 0)
            {
                return preview.IsBuilding ? 0f : 1f;
            }

            return Mathf.Clamp01((float)preview.Completed / preview.Total);
        }

        internal static string ResolveStatusText(PromptWorkspaceStructuredPreview preview)
        {
            if (preview == null)
            {
                return string.Empty;
            }

            string stage = ResolveStageLabel(preview.Stage);
            if (preview.IsFailed)
            {
                return "RimChat_PromptWorkspacePreviewBuild_StatusFailed"
                    .Translate(stage, preview.Completed, preview.Total)
                    .ToString();
            }

            if (preview.IsBuilding)
            {
                return "RimChat_PromptWorkspacePreviewBuild_StatusBuilding"
                    .Translate(
                        stage,
                        preview.Completed,
                        preview.Total,
                        preview.CompletedSections,
                        preview.TotalSections,
                        preview.CompletedNodes,
                        preview.TotalNodes)
                    .ToString();
            }

            return "RimChat_PromptWorkspacePreviewBuild_StatusCompleted"
                .Translate(preview.Completed, preview.Total)
                .ToString();
        }

        internal static string ResolveStageLabel(PromptWorkspacePreviewBuildStage stage)
        {
            switch (stage)
            {
                case PromptWorkspacePreviewBuildStage.Init:
                    return "RimChat_PromptWorkspacePreviewBuild_StageInit".Translate().ToString();
                case PromptWorkspacePreviewBuildStage.Sections:
                    return "RimChat_PromptWorkspacePreviewBuild_StageSections".Translate().ToString();
                case PromptWorkspacePreviewBuildStage.Nodes:
                    return "RimChat_PromptWorkspacePreviewBuild_StageNodes".Translate().ToString();
                case PromptWorkspacePreviewBuildStage.Finalize:
                    return "RimChat_PromptWorkspacePreviewBuild_StageFinalize".Translate().ToString();
                case PromptWorkspacePreviewBuildStage.Failed:
                    return "RimChat_PromptWorkspacePreviewBuild_StageFailed".Translate().ToString();
                default:
                    return "RimChat_PromptWorkspacePreviewBuild_StageCompleted".Translate().ToString();
            }
        }
    }
}
