using System.Collections.Generic;
using Ustas.RimAI.Communication.Relations.Persistence;
using Verse;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Prompting;
using System;
using System.Text;
using System.Reflection;
using RimWorld;
using Verse.AI;

namespace Ustas.RimAI.Communication.Relations.Prompting.Builders
{
    /// <summary>/// Dependencies: PromptPersistenceService hierarchical RPG builder core.
 /// Responsibility: orchestrate RPG prompt build entry without changing output behavior.
 ///</summary>
    internal sealed class RpgPromptBuilder
    {
        internal RpgPromptBuilderParts Parts;

        internal readonly PromptPersistenceService promptService;

        public RpgPromptBuilder(PromptPersistenceService promptService)
        {
            Parts = new RpgPromptBuilderParts(this);
            this.promptService = promptService;
        }

        public string Build(
            Pawn initiator,
            Pawn target,
            bool isProactive,
            IEnumerable<string> additionalSceneTags,
            bool allowMemoryCompressionScheduling = true,
            bool allowMemoryColdLoad = true)
        {
            DialogueScenarioContext scenarioContext = DialogueScenarioContext.CreateRpg(
                initiator,
                target,
                isProactive,
                additionalSceneTags);
            string promptChannel = PromptRuntimeChannels.ResolveRpg(isProactive);
            SystemPromptConfig config = promptService.LoadConfig() ?? promptService.DomainStore.CreateDefaultConfig();
            return promptService.WorkspaceComposer.BuildUnifiedChannelSystemPrompt(
                RimTalkPromptChannel.Rpg,
                promptChannel,
                scenarioContext,
                config?.EnvironmentPrompt,
                null,
                deterministicPreview: false,
                allowMemoryCompressionScheduling: allowMemoryCompressionScheduling,
                allowMemoryColdLoad: allowMemoryColdLoad);
        }

        #region Facade forwards
        internal static readonly FieldInfo NativeActiveAlertsField = RpgPromptBuilderColonyContext.NativeActiveAlertsField;
        internal static readonly FieldInfo JobQueueField = RpgPromptBuilderColonyContext.JobQueueField;
        internal void AppendPlayerColonyContextIfEnabled(StringBuilder sb, Pawn pawn, RpgSceneParamSwitchesConfig switches) => Parts.ColonyContext.AppendPlayerColonyContextIfEnabled(sb, pawn, switches);
        internal List<Map> GetPlayerHomeMaps() => Parts.ColonyContext.GetPlayerHomeMaps();
        internal void AppendPlayerColonyInventorySummary(StringBuilder sb, List<Map> homeMaps) => Parts.ColonyContext.AppendPlayerColonyInventorySummary(sb, homeMaps);
        internal Dictionary<ThingDef, int> AggregateColonyStock(List<Map> homeMaps) => Parts.ColonyContext.AggregateColonyStock(homeMaps);
        internal void AppendPlayerHomeAlerts(StringBuilder sb) => Parts.ColonyContext.AppendPlayerHomeAlerts(sb);
        internal List<string> GetNativeActiveAlerts() => Parts.ColonyContext.GetNativeActiveAlerts();
        internal string BuildNativeAlertLabel(Alert alert) => Parts.ColonyContext.BuildNativeAlertLabel(alert);
        internal void AppendPlayerRecentJobState(StringBuilder sb, Pawn pawn) => Parts.ColonyContext.AppendPlayerRecentJobState(sb, pawn);
        internal string BuildJobSummary(Job job) => Parts.ColonyContext.BuildJobSummary(job);
        internal List<string> GetQueuedJobSummaries(Pawn pawn) => Parts.ColonyContext.GetQueuedJobSummaries(pawn);
        internal Job ExtractQueuedJob(object queued) => Parts.ColonyContext.ExtractQueuedJob(queued);
        internal void AppendPlayerAttributeLevels(StringBuilder sb, Pawn pawn) => Parts.ColonyContext.AppendPlayerAttributeLevels(sb, pawn);
        internal void AppendCapacityPart(List<string> parts, Pawn pawn, PawnCapacityDef capacity) => Parts.ColonyContext.AppendCapacityPart(parts, pawn, capacity);
        public string BuildPawnPersonaBootstrapProfile(Pawn pawn) => Parts.Persona.BuildPawnPersonaBootstrapProfile(pawn);
        internal void AppendPersonaBackstory(StringBuilder sb, Pawn pawn) => Parts.Persona.AppendPersonaBackstory(sb, pawn);
        internal void AppendPersonaTraits(StringBuilder sb, Pawn pawn) => Parts.Persona.AppendPersonaTraits(sb, pawn);
        internal void AppendPersonaCoreSkills(StringBuilder sb, Pawn pawn) => Parts.Persona.AppendPersonaCoreSkills(sb, pawn);
        internal string FormatPersonaSkill(SkillRecord skill) => Parts.Persona.FormatPersonaSkill(skill);
        internal void AppendPersonaFactionContext(StringBuilder sb, Pawn pawn) => Parts.Persona.AppendPersonaFactionContext(sb, pawn);
        internal void AppendRPGPawnInfo(StringBuilder sb, Pawn pawn, bool isTarget, RpgSceneParamSwitchesConfig switches, bool includePlayerSharedColonyContext = true, bool includeStaticProfileDetails = true) => Parts.Persona.AppendRPGPawnInfo(sb, pawn, isTarget, switches, includePlayerSharedColonyContext, includeStaticProfileDetails);
        internal void AppendRpgNeeds(StringBuilder sb, Pawn pawn) => Parts.Persona.AppendRpgNeeds(sb, pawn);
        internal void AppendRpgHediffs(StringBuilder sb, Pawn pawn) => Parts.Persona.AppendRpgHediffs(sb, pawn);
        internal void AppendRpgSkills(StringBuilder sb, Pawn pawn) => Parts.Persona.AppendRpgSkills(sb, pawn);
        internal void AppendRpgEquipment(StringBuilder sb, Pawn pawn) => Parts.Persona.AppendRpgEquipment(sb, pawn);
        internal void AppendRpgGenes(StringBuilder sb, Pawn pawn) => Parts.Persona.AppendRpgGenes(sb, pawn);
        internal void AppendRpgRecentMemories(StringBuilder sb, Pawn pawn) => Parts.Persona.AppendRpgRecentMemories(sb, pawn);
        internal void AppendRPGFactionContext(StringBuilder sb, Pawn pawn) => Parts.Persona.AppendRPGFactionContext(sb, pawn);
        internal string BuildPawnProfileVariableText(Pawn pawn, DialogueScenarioContext context, EnvironmentPromptConfig envConfig) => Parts.ProfileVariables.BuildPawnProfileVariableText(pawn, context, envConfig);
        internal List<string> BuildBasePawnProfileLines(Pawn pawn) => Parts.ProfileVariables.BuildBasePawnProfileLines(pawn);
        internal void AppendRpgProfileExtensions(List<string> lines, Pawn pawn, RpgSceneParamSwitchesConfig switches) => Parts.ProfileVariables.AppendRpgProfileExtensions(lines, pawn, switches);
        internal void AppendRpgProfileExtensions(List<string> lines, Pawn pawn, RpgSceneParamSwitchesConfig switches, Pawn otherPawn) => Parts.ProfileVariables.AppendRpgProfileExtensions(lines, pawn, switches, otherPawn);
        internal void AppendRpgColonyProfileExtensions(List<string> lines, Pawn pawn, RpgSceneParamSwitchesConfig switches, Pawn otherPawn) => Parts.ProfileVariables.AppendRpgColonyProfileExtensions(lines, pawn, switches, otherPawn);
        internal bool IsPawnPrivyToColonyInfo(Pawn pawn, Pawn otherPawn) => Parts.ProfileVariables.IsPawnPrivyToColonyInfo(pawn, otherPawn);
        internal string BuildPairSocialSummary(Pawn initiator, Pawn target, string kinshipValue, string romanceState) => Parts.ProfileVariables.BuildPairSocialSummary(initiator, target, kinshipValue, romanceState);
        internal string BuildPairDirectRelationsSummary(Pawn first, Pawn second) => Parts.ProfileVariables.BuildPairDirectRelationsSummary(first, second);
        internal void AddDirectRelationLabels(HashSet<string> labels, Pawn fromPawn, Pawn toPawn) => Parts.ProfileVariables.AddDirectRelationLabels(labels, fromPawn, toPawn);
        internal string BuildFactionGoodwillSummary(Faction faction) => Parts.ProfileVariables.BuildFactionGoodwillSummary(faction);
        internal string BuildRecentJobStateLine(Pawn pawn) => Parts.ProfileVariables.BuildRecentJobStateLine(pawn);
        internal void AddProfileLineFromBuilder(List<string> lines, Pawn pawn, Action<StringBuilder, Pawn> appendBuilder) => Parts.ProfileVariables.AddProfileLineFromBuilder(lines, pawn, appendBuilder);
        internal void AddProfileLineFromBuilder(List<string> lines, Action<StringBuilder> appendBuilder) => Parts.ProfileVariables.AddProfileLineFromBuilder(lines, appendBuilder);
        #endregion
    }

    internal sealed class RpgPromptBuilderParts
    {
        internal readonly RpgPromptBuilder Owner;
        internal readonly RpgPromptBuilderColonyContext ColonyContext;
        internal readonly RpgPromptBuilderPersona Persona;
        internal readonly RpgPromptBuilderProfileVariables ProfileVariables;
        internal RpgPromptBuilderParts(RpgPromptBuilder owner)
        {
            Owner = owner;
            ColonyContext = new RpgPromptBuilderColonyContext(owner);
            Persona = new RpgPromptBuilderPersona(owner);
            ProfileVariables = new RpgPromptBuilderProfileVariables(owner);
        }
    }
}
