using System;
using System.Collections.Generic;
using RimAI.Core.Runtime;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.AI.Runtime;

namespace Ustas.RimAI.Communication.Relations.Runtime
{
    /// <summary>
    /// Entry point the loader looks up by exact type name. Its name and
    /// namespace are part of the module registration, so renaming either is a
    /// contract change, not a refactor.
    /// </summary>
    public sealed class RelationsRuntimeFactory : IModuleRuntimeFactory
    {
        public IModuleRuntime CreateModuleRuntime(RuntimeDescriptor descriptor) =>
            new RelationsModuleRuntime(descriptor);
    }

    /// <summary>
    /// Relations' reloadable provider and parser layer.
    ///
    /// The work is done by the same source files the stable assembly compiles,
    /// linked into this project — this class only carries the slot lifecycle and
    /// forwards. Keeping it that thin is the point: the thing being hot-swapped
    /// is the parser, and anything else living here would be a second place to
    /// look when a fix does not take effect.
    /// </summary>
    public sealed class RelationsModuleRuntime : IModuleRuntime, IRelationsRuntime
    {
        private readonly RuntimeDescriptor _descriptor;
        private bool _deactivated;

        public RelationsModuleRuntime(RuntimeDescriptor descriptor)
        {
            _descriptor = descriptor ?? throw new ArgumentNullException("descriptor");
        }

        public RuntimeDescriptor Descriptor => _descriptor;

        /// <summary>
        /// Every operation is a pure function that returns before it yields, so
        /// this candidate never holds work across a swap and never blocks one.
        /// </summary>
        public bool HasActiveOperations => false;

        public string PolicyMarker => _descriptor.RuntimeBuildId;

        public void Initialize(IRuntimeServices services)
        {
            // Nothing to bind. The layer is stateless by construction, which is
            // exactly why it is safe to replace mid-session.
            _ = services;
        }

        public RuntimeStatus QueryStatus() =>
            new RuntimeStatus(
                _deactivated ? null : _descriptor.RuntimeBuildId,
                null,
                null,
                _deactivated ? RuntimeLoadStatus.None : RuntimeLoadStatus.Active,
                DateTimeOffset.UtcNow,
                0);

        public RuntimeStateSnapshot ExportHandoff() =>
            new RuntimeStateSnapshot(_descriptor.RuntimeBuildId, Array.Empty<PlannerActivitySnapshot>());

        public void ImportHandoff(RuntimeStateSnapshot snapshot)
        {
            // Stateless: there is nothing to carry over from the candidate this
            // one replaces.
            _ = snapshot;
        }

        public void Deactivate()
        {
            _deactivated = true;
        }

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
