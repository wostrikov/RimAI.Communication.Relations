using System;
using System.Runtime.CompilerServices;
using Ustas.RimAI.Communication.Relations.Config;

namespace Ustas.RimAI.Communication.Relations.UI;

/// <summary>Per-settings UI session. Scroll/search/page objects are not persisted on ModSettings.</summary>
internal sealed class RelationsSettingsPages
{
    static readonly ConditionalWeakTable<RelationsSettings, RelationsSettingsPages> Table = new();

    RelationsSettingsPages(RelationsSettings settings)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Tooltips = new RelationsSettingsTooltips();
        ApiHeader = new RelationsApiHeaderUx(this);
        ApiUsability = new RelationsApiUsabilitySection(this);
        ProviderCloud = new RelationsProviderCloudSection(this);
        ProviderConnection = new RelationsProviderConnectionSection(this);
        ProviderFaction = new RelationsProviderFactionPrompts(this);
        Provider = new RelationsProviderSettingsPage(this);
        GameplayUx = new RelationsGameplayUxSection(this);
        RpgDialogue = new RelationsGameplayRpgDialogueSection(this);
        NpcPush = new RelationsNpcPushSettingsSection(this);
        SocialCircle = new RelationsSocialCircleSettingsSection(this);
        Gameplay = new RelationsGameplaySettingsPage(this);
        CustomVariables = new RelationsPromptCustomVariables(this);
        PromptLegacy = new RelationsPromptLegacyEditors(this);
        PromptEnvironment = new RelationsPromptEnvironmentEditors(this);
        PromptSocialCircle = new RelationsPromptSocialCircleEditors(this);
        PromptTemplates = new RelationsPromptTemplateEditors(this);
        PromptQuickActions = new RelationsPromptQuickActions(this);
        PromptEditorActions = new RelationsPromptWorkspaceEditorActions(this);
        PromptPresetsUi = new RelationsPromptWorkspacePresetInteractions(this);
        PromptModuleTransfer = new RelationsPromptWorkspaceModuleTransfer(this);
        PromptNodeLayout = new RelationsPromptWorkspaceNodeLayout(this);
        PromptWorkspace = new RelationsPromptSectionWorkspace(this);
        PromptWorkbench = new RelationsPromptWorkbenchFramework(this);
        PromptSeedImport = new RelationsPromptEntrySeedImport(this);
        VariableBrowser = new RelationsRimTalkVariableBrowser(this);
        RimTalkBridge = new RelationsRimTalkBridgePage(this);
        RimTalkTab = new RelationsRimTalkTabPage(this);
        RpgEditors = new RelationsRpgPromptEditorsPage(this);
        RpgFieldEditors = new RelationsRpgPromptFieldEditors(this);
        RpgCompatUi = new RelationsRpgRimTalkCompatUi(this);
        Prompt = new RelationsPromptSettingsPage(this);
        Image = new RelationsImageSettingsPage(this);
        GameplayActions = new RelationsGameplayActionSettings(Gameplay);
        ProviderCloudFetch = new RelationsProviderCloudModelFetch(ProviderCloud);
        RimTalkEntries = new RelationsRimTalkEntryList(RimTalkTab);
        RimTalkTemplates = new RelationsRimTalkTemplateEditors(RimTalkTab);
        PromptWorkbenchPresets = new RelationsPromptWorkbenchPresets(PromptWorkbench);
        PromptLegacyApi = new RelationsPromptLegacyApiEditors(PromptLegacy);
        PromptLegacyRules = new RelationsPromptLegacyRuleEditors(PromptLegacy);
        PromptLegacyPreview = new RelationsPromptLegacyPreview(PromptLegacy);
        PromptLegacyValidation = new RelationsPromptLegacyValidation(PromptLegacy);
        PromptLegacyIo = new RelationsPromptLegacyIo(PromptLegacy);
        PromptWorkspaceChrome = new RelationsPromptWorkspaceChrome(PromptWorkspace);
        PromptWorkspacePreviewUi = new RelationsPromptWorkspacePreviewUi(PromptWorkspace);
        PromptWorkspaceBuffers = new RelationsPromptWorkspaceBuffers(PromptWorkspace);
    }

    internal RelationsSettings Settings { get; }
    internal RelationsSettingsTooltips Tooltips { get; }
    internal RelationsApiHeaderUx ApiHeader { get; }
    internal RelationsApiUsabilitySection ApiUsability { get; }
    internal RelationsProviderCloudSection ProviderCloud { get; }
    internal RelationsProviderConnectionSection ProviderConnection { get; }
    internal RelationsProviderFactionPrompts ProviderFaction { get; }
    internal RelationsProviderSettingsPage Provider { get; }
    internal RelationsGameplayUxSection GameplayUx { get; }
    internal RelationsGameplayRpgDialogueSection RpgDialogue { get; }
    internal RelationsNpcPushSettingsSection NpcPush { get; }
    internal RelationsSocialCircleSettingsSection SocialCircle { get; }
    internal RelationsGameplaySettingsPage Gameplay { get; }
    internal RelationsPromptCustomVariables CustomVariables { get; }
    internal RelationsPromptLegacyEditors PromptLegacy { get; }
    internal RelationsPromptEnvironmentEditors PromptEnvironment { get; }
    internal RelationsPromptSocialCircleEditors PromptSocialCircle { get; }
    internal RelationsPromptTemplateEditors PromptTemplates { get; }
    internal RelationsPromptQuickActions PromptQuickActions { get; }
    internal RelationsPromptWorkspaceEditorActions PromptEditorActions { get; }
    internal RelationsPromptWorkspacePresetInteractions PromptPresetsUi { get; }
    internal RelationsPromptWorkspaceModuleTransfer PromptModuleTransfer { get; }
    internal RelationsPromptWorkspaceNodeLayout PromptNodeLayout { get; }
    internal RelationsPromptSectionWorkspace PromptWorkspace { get; }
    internal RelationsPromptWorkbenchFramework PromptWorkbench { get; }
    internal RelationsPromptEntrySeedImport PromptSeedImport { get; }
    internal RelationsRimTalkVariableBrowser VariableBrowser { get; }
    internal RelationsRimTalkBridgePage RimTalkBridge { get; }
    internal RelationsRimTalkTabPage RimTalkTab { get; }
    internal RelationsRpgPromptEditorsPage RpgEditors { get; }
    internal RelationsRpgPromptFieldEditors RpgFieldEditors { get; }
    internal RelationsRpgRimTalkCompatUi RpgCompatUi { get; }
    internal RelationsPromptSettingsPage Prompt { get; }
    internal RelationsImageSettingsPage Image { get; }

    internal RelationsGameplayActionSettings GameplayActions { get; }
    internal RelationsProviderCloudModelFetch ProviderCloudFetch { get; }
    internal RelationsRimTalkEntryList RimTalkEntries { get; }
    internal RelationsRimTalkTemplateEditors RimTalkTemplates { get; }
    internal RelationsPromptWorkbenchPresets PromptWorkbenchPresets { get; }
    internal RelationsPromptLegacyApiEditors PromptLegacyApi { get; }
    internal RelationsPromptLegacyRuleEditors PromptLegacyRules { get; }
    internal RelationsPromptLegacyPreview PromptLegacyPreview { get; }
    internal RelationsPromptLegacyValidation PromptLegacyValidation { get; }
    internal RelationsPromptLegacyIo PromptLegacyIo { get; }
    internal RelationsPromptWorkspaceChrome PromptWorkspaceChrome { get; }
    internal RelationsPromptWorkspacePreviewUi PromptWorkspacePreviewUi { get; }
    internal RelationsPromptWorkspaceBuffers PromptWorkspaceBuffers { get; }

    internal static RelationsSettingsPages For(RelationsSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));
        return Table.GetValue(settings, key => new RelationsSettingsPages(key));
    }
}
