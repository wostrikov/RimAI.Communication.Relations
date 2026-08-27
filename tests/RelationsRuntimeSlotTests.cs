using System;
using System.Collections.Generic;
using RimAI.Core.Runtime;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.AI.Runtime;

/// <summary>
/// Phase 9.7 Stage 2 — Relations owns a reloadable slot.
///
/// The properties that matter are: without a candidate the shipped layer
/// answers exactly as before; with one, the candidate answers instead; and
/// Relations versions its own contract, so a Core change cannot invalidate a
/// staged Relations candidate.
/// </summary>
internal static class RelationsRuntimeSlotTests
{
    public static void Run(Action<bool, string> check)
    {
        Registration(check);
        ShippedFallback(check);
        CandidateOverride(check);
        Isolation(check);
    }

    static void Registration(Action<bool, string> check)
    {
        RelationsRuntimeGateway.EnsureRegistered();
        check(RuntimeRegistry.IsRegistered(RelationsRuntimeGateway.ModuleId), "relations module is registered");

        var registration = RuntimeRegistry.Registration(RelationsRuntimeGateway.ModuleId);
        check(registration.ModuleApiVersion == RelationsRuntimeGateway.ApiVersion,
            "relations gates on its own api version");
        check(registration.ModuleApiVersion != RuntimeApi.Version || RuntimeApi.Version == 1,
            "relations version is not Core's by construction");
        check(registration.AssemblyPrefix == "RimAI.Communication.Relations.Runtime.",
            "relations accepts only its own candidate assembly");

        // Calling it again must be safe: the mod entry point and the first
        // policy lookup both call it, and either can run first.
        RelationsRuntimeGateway.EnsureRegistered();
        check(RuntimeRegistry.IsRegistered(RelationsRuntimeGateway.ModuleId), "registration is idempotent");
    }

    static void ShippedFallback(Action<bool, string> check)
    {
        var policy = RelationsRuntimeGateway.Policy;
        check(policy.PolicyMarker == "shipped", "no candidate means the shipped layer answers");
        check(RelationsRuntimeGateway.ActiveBuildId == "shipped", "and says so");

        var request = policy.BuildResponsesRequest(new RelationsProviderRequest
        {
            Model = "gpt-5.6-luna",
            Messages = new List<ChatMessageData>
            {
                new ChatMessageData { role = "system", content = "Be brief." },
                new ChatMessageData { role = "assistant", content = "Hello" },
            },
            MaxOutputTokens = 16,
        });
        check(request.Contains("\"role\":\"assistant\",\"content\":[{\"type\":\"output_text\""),
            "the shipped layer still builds the fixed request shape");

        var extracted = policy.ExtractProviderText(
            "{\"error\":null,\"output\":[{\"content\":[{\"type\":\"output_text\",\"text\":\"Fine\"}]}]}",
            AIProvider.OpenAI);
        check(extracted.IsSuccess && extracted.Content == "Fine", "the shipped layer still extracts text");
        check(policy.IsRetryableEmptyPrimaryText("no_output_text"), "the shipped layer still answers retryability");
    }

    static void CandidateOverride(Action<bool, string> check)
    {
        RelationsRuntimeGateway.EnsureRegistered();
        var slot = RuntimeRegistry.Slot(RelationsRuntimeGateway.ModuleId);
        var activated = slot.TryActivate(
            new FakeRelationsRuntimeFactory(),
            new RuntimeDescriptor("relations-test", RelationsRuntimeGateway.ApiVersion, RuntimeApi.SchemaVersion));
        check(activated.Succeeded, "a relations candidate activates");

        var policy = RelationsRuntimeGateway.Policy;
        check(policy.PolicyMarker == "relations-test", "the candidate answers instead of the shipped layer");
        check(policy.BuildResponsesRequest(new RelationsProviderRequest()) == "candidate-request",
            "and its implementation is the one that runs");
        check(RelationsRuntimeGateway.ActiveBuildId == "relations-test", "the build id is reported");

        // A candidate speaking a different contract version must be refused
        // rather than half-accepted.
        var wrongVersion = slot.TryActivate(
            new FakeRelationsRuntimeFactory(),
            new RuntimeDescriptor("wrong", RelationsRuntimeGateway.ApiVersion + 1, RuntimeApi.SchemaVersion));
        check(!wrongVersion.Succeeded && wrongVersion.ErrorCode == "IncompatibleApi",
            "a candidate on another contract version is refused");
        check(RelationsRuntimeGateway.Policy.PolicyMarker == "relations-test",
            "and the refusal leaves the active candidate in place");
    }

    static void Isolation(Action<bool, string> check)
    {
        // Relations' slot must not be Core's. Sharing one would put every
        // Relations fix back behind Core's version gate.
        var relations = RuntimeRegistry.Slot(RelationsRuntimeGateway.ModuleId);
        check(!ReferenceEquals(relations, RuntimeRegistry.CoreSlot), "relations does not share Core's slot");
        check(RuntimeHostState.CoreRuntime == null || relations.Active != RuntimeHostState.CoreRuntime,
            "and does not share Core's candidate");
    }

    sealed class FakeRelationsRuntimeFactory : IModuleRuntimeFactory
    {
        public IModuleRuntime CreateModuleRuntime(RuntimeDescriptor descriptor) =>
            new FakeRelationsRuntime(descriptor);
    }

    sealed class FakeRelationsRuntime : IModuleRuntime, IRelationsRuntime
    {
        private readonly RuntimeDescriptor _descriptor;

        public FakeRelationsRuntime(RuntimeDescriptor descriptor)
        {
            _descriptor = descriptor;
        }

        public RuntimeDescriptor Descriptor => _descriptor;
        public bool HasActiveOperations => false;
        public string PolicyMarker => _descriptor.RuntimeBuildId;

        public void Initialize(IRuntimeServices services)
        {
        }

        public RuntimeStatus QueryStatus() =>
            new RuntimeStatus(_descriptor.RuntimeBuildId, null, null, RuntimeLoadStatus.Active, DateTimeOffset.UtcNow, 0);

        public RuntimeStateSnapshot ExportHandoff() =>
            new RuntimeStateSnapshot(_descriptor.RuntimeBuildId, Array.Empty<PlannerActivitySnapshot>());

        public void ImportHandoff(RuntimeStateSnapshot snapshot)
        {
        }

        public void Deactivate()
        {
        }

        public string BuildResponsesRequest(RelationsProviderRequest request) => "candidate-request";

        public PrimaryTextExtractionResult ExtractProviderText(string body, AIProvider provider) =>
            new PrimaryTextExtractionResult { IsSuccess = true, Content = "candidate-text" };

        public bool IsRetryableEmptyPrimaryText(string reasonTag) => false;
    }
}
