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
    /// Structured preview block classification helpers.
    /// </summary>
    internal static class PromptWorkspacePreviewBlockOps
    {
        internal static bool HasSubsections(PromptWorkspacePreviewBlock block)
        {
            return block != null &&
                   block.Kind == PromptWorkspacePreviewBlockKind.SectionAggregate &&
                   block.Subsections != null &&
                   block.Subsections.Count > 0;
        }

        internal static Color ResolveHeaderColor(PromptWorkspacePreviewBlock block)
        {
            PromptWorkspacePreviewBlockKind kind = block?.Kind ?? PromptWorkspacePreviewBlockKind.Node;
            switch (kind)
            {
                case PromptWorkspacePreviewBlockKind.Context:
                    return PromptWorkspaceStructuredPreviewRenderer.BlockBgSystemRules;
                case PromptWorkspacePreviewBlockKind.SectionAggregate:
                    return PromptWorkspaceStructuredPreviewRenderer.BlockBgCharacter;
                case PromptWorkspacePreviewBlockKind.Footer:
                    return PromptWorkspaceStructuredPreviewRenderer.BlockBgGeneric;
                case PromptWorkspacePreviewBlockKind.Error:
                    return PromptWorkspaceStructuredPreviewRenderer.BlockBgActionRules;
                default:
                    return PromptWorkspaceStructuredPreviewRenderer.BlockBgOutputSpec;
            }
        }
    }
}
