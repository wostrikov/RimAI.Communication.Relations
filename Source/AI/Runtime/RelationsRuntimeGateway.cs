using System;
using System.Collections.Generic;
using RimAI.Core.Runtime;

namespace Ustas.RimAI.Communication.Relations.AI.Runtime
{
    /// <summary>
    /// Where Relations asks for its provider policy. Callers must come through
    /// here rather than calling the parser classes directly, or a staged
    /// candidate would sit there answering nobody.
    ///
    /// Never cache what this returns: it is only valid until the next swap.
    /// </summary>
    public static class RelationsRuntimeGateway
    {
        public const string ModuleId = "relations";

        /// <summary>
        /// Bumped only when the shape of <see cref="IRelationsRuntime"/> changes.
        /// Independent of Core's version on purpose: a Core contract change must
        /// not invalidate a staged Relations candidate, nor the reverse.
        /// </summary>
        public const int ApiVersion = 1;

        public const string AssemblyPrefix = "RimAI.Communication.Relations.Runtime.";

        public const string FactoryTypeName =
            "Ustas.RimAI.Communication.Relations.Runtime.RelationsRuntimeFactory";

        private static readonly ModuleRuntimeRegistration Registration =
            new ModuleRuntimeRegistration(ModuleId, ApiVersion, AssemblyPrefix, FactoryTypeName);

        private static RuntimeSlot slot;

        // Not exposed as a singleton: it holds nothing, so there is no shared
        // state to reach for, and one private instance keeps the ambient-access
        // surface at zero.
        private static readonly IRelationsRuntime Shipped = new RelationsShippedRuntime();

        /// <summary>
        /// Idempotent, so every entry point that might run first may call it.
        /// </summary>
        public static void EnsureRegistered()
        {
            if (slot != null)
            {
                return;
            }

            slot = RuntimeRegistry.Register(Registration);

            // Relations registers from its own mod constructor, which can run
            // after the host has booted, so the host's boot pass may never have
            // seen this module. Ask for the staged candidate here and a fresh
            // game session comes up on it the same way core does.
            RuntimeHostState.TryActivateStagedCandidate(ModuleId);
        }

        /// <summary>
        /// The staged candidate, or the implementation that ships in this
        /// assembly. Both answer the same questions; the marker says which one
        /// did, so a log line can never leave that ambiguous.
        /// </summary>
        public static IRelationsRuntime Policy
        {
            get
            {
                EnsureRegistered();
                IRelationsRuntime active = slot.ActiveAs<IRelationsRuntime>();
                return active ?? Shipped;
            }
        }

        public static string ActiveBuildId
        {
            get
            {
                EnsureRegistered();
                IModuleRuntime active = slot.Active;
                return active == null ? "shipped" : active.Descriptor.RuntimeBuildId;
            }
        }
    }

    /// <summary>
    /// The implementation that ships in the mod. The candidate compiles the
    /// same source files, so this is not a second copy of the logic that can
    /// drift — it is the same logic, reached without a staged candidate.
    /// </summary>
    internal sealed class RelationsShippedRuntime : IRelationsRuntime
    {
        public string PolicyMarker => "shipped";

        public string BuildResponsesRequest(RelationsProviderRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");
            return OpenAIProviderAdapter.BuildResponsesRequest(
                request.Model,
                request.Messages ?? new List<ChatMessageData>(),
                request.MaxOutputTokens);
        }

        public PrimaryTextExtractionResult ExtractProviderText(string body, AIProvider provider) =>
            RelationsProviderTextExtractor.Extract(body, provider);

        public bool IsRetryableEmptyPrimaryText(string reasonTag) =>
            RelationsProviderTextExtractor.IsRetryableEmptyPrimaryText(reasonTag);
    }
}
