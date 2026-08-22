using System;
using HarmonyLib;
using Ustas.RimAI.Communication.Relations.Comp;
using Ustas.RimAI.Communication.Relations.Integration;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Core.Composition;
using Ustas.RimAI.Core.Diagnostics;
using Ustas.RimAI.Core.Handshake;
using Ustas.RimAI.Core.Modules;
using Ustas.RimAI.Core.Relations;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Module;

/// <summary>
/// Module composition root for RimAI.Communication.Relations. Owns handshake
/// activation, Harmony, and Core RelationsApplication wiring. Residual
/// diplomacy/prompt *.Instance services remain temporary ambient state.
/// </summary>
public sealed class RelationsComposition : IRimAiModuleComposition
{
    public static RelationsComposition Current { get; } = new();

    public string ModuleId => RimAiModuleIds.Relations;

    public bool IsStarted { get; private set; }

    public void Start()
    {
        if (IsStarted)
            return;

        var settings = RelationsMod.Settings;
        settings?.EnsureRpgPromptTextsLoaded();
        settings?.EnsurePawnPersonalityTokenForRpgChannelsSafe();
        RefreshDefaultPresetSnapshotOnStartup(settings);

        FactionPromptManager.Instance.Initialize();

        var harmony = new Harmony("ustas.rimai.communication.relations");
        Patches.HarmonyPatchStartupSelfCheck.Run();
        harmony.PatchAll();
        Patches.CommsConsolePatch.Initialize(harmony);
        Patches.QuestGenPatch.Initialize(harmony);

        LongEventHandler.ExecuteWhenFinished(PawnDialogueCompDefInjector.EnsureInjected);

        RelationsApplicationAccess.Register(new RelationsApplication());
        RelationsPipelineProbe.Register();
        RimAIModuleRegistry.Current.Register(new RimAIModuleDescriptor(
            "relations",
            "RimAI.Communication.Relations",
            "RimAI.Communication.Relations",
            "Communication",
            "RimAI.Communication"));
        RimAISettingsContributionRegistry.Current.Register(new DelegateSettingsContributor(
            "relations",
            "Relations",
            RimAISettingsSection.Module,
            20,
            listing => DrawCoreRelationsSummary((Listing_Standard)listing),
            "communication",
            "relations"));
        RimAiLog.Info(RimAiLogCategory.Relations, "[RimAI.Relations] Mod initialized successfully.");
        IsStarted = true;
    }

    public void Stop()
    {
        IsStarted = false;
    }

    static void DrawCoreRelationsSummary(Listing_Standard listing)
    {
        listing.Label("RimAI.Settings.TextAiOwnedByCore".Translate());
        listing.Label(AI.OpenAIProviderAdapter.CredentialDisplay);
        listing.Gap(6f);
        listing.Label("RimAI.Settings.RelationsModuleHint".Translate());
    }

    static void RefreshDefaultPresetSnapshotOnStartup(RelationsSettings? settings)
    {
        if (settings is null)
            return;

        try
        {
            IPromptPresetService presetService = new PromptPresetService();
            PromptPresetStoreConfig store = presetService.LoadAll(settings);
            presetService.SaveAll(store);
        }
        // RimAI.catch-boundary: TEMPORARY_EXPLICIT_EXCEPTION — Relations preset refresh is best-effort at boot
        catch (Exception ex)
        {
            RimAiLog.Warning(
                RimAiLogCategory.Relations,
                "[RimAI.Relations] Default preset refresh on startup failed",
                ex);
        }
    }
}
