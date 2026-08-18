using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.Guards;
using RimWorld;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Context;

using Ustas.RimAI.Communication.Relations.Persistence;

namespace Ustas.RimAI.Communication.Relations.Prompting
{
internal sealed class PromptTemplateVariableService
    {
        internal PromptTemplateVariableServiceParts Parts;

        internal readonly PromptPersistenceService host;

        internal PromptTemplateVariableService(PromptPersistenceService host)
        {
            Parts = new PromptTemplateVariableServiceParts(this);
            this.host = host ?? throw new System.ArgumentNullException(nameof(host));
        }

        internal static readonly Regex TemplateVariableRegex = new Regex(@"\{\{\s*([a-zA-Z0-9_.]+)\s*\}\}", RegexOptions.Compiled);
        internal static readonly HashSet<string> AllowedTemplateVariableNamespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ctx",
            "pawn",
            "world",
            "dialogue",
            "system"
        };

        

        public TemplateVariableValidationResult ValidateTemplateVariables(string templateText)
        {
            return ValidateTemplateVariables(templateText, TemplateVariableValidationContext.CreateDefault());
        }

        

        

        

        

        

        

        

        

        

        

        

        

        

        internal readonly struct RpgRelationSnapshot
        {
            public static readonly RpgRelationSnapshot Empty = new RpgRelationSnapshot(string.Empty, string.Empty, string.Empty, string.Empty);

            public RpgRelationSnapshot(string kinship, string romanceState, string socialSummary, string guidance)
            {
                Kinship = kinship ?? string.Empty;
                RomanceState = romanceState ?? string.Empty;
                SocialSummary = socialSummary ?? string.Empty;
                Guidance = guidance ?? string.Empty;
            }

            public string Kinship { get; }
            public string RomanceState { get; }
            public string SocialSummary { get; }
            public string Guidance { get; }
        }

        internal int BuildWorldTimeHourVariableValue(DialogueScenarioContext context)
        {
            return GenDate.HourOfDay(GetAbsoluteTicks(), GetLongitude(context));
        }

        internal int BuildWorldTimeDayVariableValue(DialogueScenarioContext context)
        {
            return GenDate.DayOfQuadrum(GetAbsoluteTicks(), GetLongitude(context)) + 1;
        }

        internal string BuildWorldTimeQuadrumVariableValue(DialogueScenarioContext context)
        {
            return GenDate.Quadrum(GetAbsoluteTicks(), GetLongitude(context)).Label();
        }

        internal int BuildWorldTimeYearVariableValue(DialogueScenarioContext context)
        {
            return GenDate.Year(GetAbsoluteTicks(), GetLongitude(context));
        }

        

        internal string BuildWorldTimeDateVariableValue(DialogueScenarioContext context)
        {
            return GenDate.DateFullStringAt(GetAbsoluteTicks(), GetLongLat(context));
        }

        

        

        internal int GetAbsoluteTicks()
        {
            return Find.TickManager?.TicksAbs ?? 0;
        }

        internal float GetLongitude(DialogueScenarioContext context)
        {
            return GetLongLat(context).x;
        }

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        
    
        #region Cluster forwards
        public IReadOnlyList<PromptTemplateVariableDefinition> GetTemplateVariableDefinitions() => Parts.Slice1.GetTemplateVariableDefinitions();
        public TemplateVariableValidationResult ValidateTemplateVariables(string templateText, IEnumerable<string> additionalKnownVariables) => Parts.Slice1.ValidateTemplateVariables(templateText, additionalKnownVariables);
        internal TemplateVariableValidationResult ValidateTemplateVariables(string templateText, TemplateVariableValidationContext validationContext) => Parts.Slice1.ValidateTemplateVariables(templateText, validationContext);
        internal string RenderTemplateVariables(string templateText, DialogueScenarioContext context, EnvironmentPromptConfig envConfig, out List<string> usedVariables, out List<string> unknownVariables) => Parts.Slice1.RenderTemplateVariables(templateText, context, envConfig, out usedVariables, out unknownVariables);
        internal PromptRenderContext BuildTemplateRenderContext(string templateId, string channel, DialogueScenarioContext context, EnvironmentPromptConfig envConfig) => Parts.Slice1.BuildTemplateRenderContext(templateId, channel, context, envConfig);
        internal Dictionary<string, object> BuildTemplateVariableValues(string templateId, string channel, DialogueScenarioContext context, EnvironmentPromptConfig envConfig) => Parts.Slice1.BuildTemplateVariableValues(templateId, channel, context, envConfig);
        internal string NormalizeTemplateVariableName(string rawName) => Parts.Slice1.NormalizeTemplateVariableName(rawName);
        internal bool IsNamespacedVariablePath(string variableName) => Parts.Slice1.IsNamespacedVariablePath(variableName);
        internal void TryCollectScribanDiagnostic(string templateText, IEnumerable<string> variablePaths, TemplateVariableValidationResult result) => Parts.Slice1.TryCollectScribanDiagnostic(templateText, variablePaths, result);
        internal object ResolveTemplateVariableValue(string variableName, DialogueScenarioContext context, EnvironmentPromptConfig envConfig) => Parts.Slice1.ResolveTemplateVariableValue(variableName, context, envConfig);
        internal string ResolveDialoguePrimaryObjectiveVariableValue(DialogueScenarioContext context) => Parts.Slice1.ResolveDialoguePrimaryObjectiveVariableValue(context);
        internal string ResolveDialogueOptionalFollowupVariableValue(DialogueScenarioContext context) => Parts.Slice2.ResolveDialogueOptionalFollowupVariableValue(context);
        internal string ResolveDialogueLatestUnresolvedIntentVariableValue(DialogueScenarioContext context) => Parts.Slice2.ResolveDialogueLatestUnresolvedIntentVariableValue(context);
        internal RpgRelationSnapshot ResolveRpgRelationSnapshot(DialogueScenarioContext context) => Parts.Slice2.ResolveRpgRelationSnapshot(context);
        internal string BuildWorldTimeSeasonVariableValue(DialogueScenarioContext context) => Parts.Slice2.BuildWorldTimeSeasonVariableValue(context);
        internal string BuildWorldWeatherVariableValue(DialogueScenarioContext context) => Parts.Slice2.BuildWorldWeatherVariableValue(context);
        internal string BuildWorldTemperatureVariableValue(DialogueScenarioContext context) => Parts.Slice2.BuildWorldTemperatureVariableValue(context);
        internal Vector2 GetLongLat(DialogueScenarioContext context) => Parts.Slice2.GetLongLat(context);
        internal string BuildSceneTagsVariableText(DialogueScenarioContext context) => Parts.Slice2.BuildSceneTagsVariableText(context);
        internal string BuildEnvironmentParamsVariableText(DialogueScenarioContext context, EnvironmentPromptConfig envConfig) => Parts.Slice2.BuildEnvironmentParamsVariableText(context, envConfig);
        internal string BuildRecentWorldEventsVariableText(DialogueScenarioContext context, EnvironmentPromptConfig envConfig) => Parts.Slice2.BuildRecentWorldEventsVariableText(context, envConfig);
        internal string BuildEnvironmentSnapshotVariableText(IEnumerable<string> lines, int maxItems, int maxChars) => Parts.Slice2.BuildEnvironmentSnapshotVariableText(lines, maxItems, maxChars);
        internal string BuildColonyStatusVariableText() => Parts.Slice2.BuildColonyStatusVariableText();
        internal string BuildColonyFactionsVariableText() => Parts.Slice2.BuildColonyFactionsVariableText();
        internal string BuildCurrentFactionProfileVariableText(DialogueScenarioContext context) => Parts.Slice2.BuildCurrentFactionProfileVariableText(context);
        internal string BuildFactionDescriptionVariableText(DialogueScenarioContext context) => Parts.Slice2.BuildFactionDescriptionVariableText(context);
        internal string BuildFactionRelationTowardPlayerText(Faction faction, Faction playerFaction) => Parts.Slice2.BuildFactionRelationTowardPlayerText(faction, playerFaction);
        internal int? TryGetGoodwillTowardPlayer(Faction faction) => Parts.Slice2.TryGetGoodwillTowardPlayer(faction);
        internal string BuildPawnPersonalityVariableText(DialogueScenarioContext context) => Parts.Slice2.BuildPawnPersonalityVariableText(context);
        internal string BuildPlayerPawnProfileVariableText(DialogueScenarioContext context) => Parts.Slice2.BuildPlayerPawnProfileVariableText(context);
        internal string BuildPlayerRoyaltySummaryVariableText(DialogueScenarioContext context) => Parts.Slice2.BuildPlayerRoyaltySummaryVariableText(context);
        internal string BuildFactionSettlementSummaryVariableText(DialogueScenarioContext context) => Parts.Slice2.BuildFactionSettlementSummaryVariableText(context);
        internal string BuildFactionRelationBandVariableValue(DialogueScenarioContext context) => Parts.Slice2.BuildFactionRelationBandVariableValue(context);
        internal string BuildPawnTraitsSummaryVariableValue(DialogueScenarioContext context) => Parts.Slice2.BuildPawnTraitsSummaryVariableValue(context);
        internal string BuildFactionIdeologySummaryVariableValue(DialogueScenarioContext context) => Parts.Slice2.BuildFactionIdeologySummaryVariableValue(context);
        internal string BuildFactionTechLevelVariableValue(DialogueScenarioContext context) => Parts.Slice2.BuildFactionTechLevelVariableValue(context);
        internal string BuildSocialDiplomacyStanceVariableValue(DialogueScenarioContext context) => Parts.Slice2.BuildSocialDiplomacyStanceVariableValue(context);
        internal string BuildAvailableActionNamesVariableValue(DialogueScenarioContext context) => Parts.Slice2.BuildAvailableActionNamesVariableValue(context);
        internal string BuildResponseContractBodyVariableValue(DialogueScenarioContext context) => Parts.Slice2.BuildResponseContractBodyVariableValue(context);
        #endregion
}
    internal sealed class PromptTemplateVariableSlice2 : PromptTemplateVariableServiceCollaborator
    {
        internal PromptTemplateVariableSlice2(PromptTemplateVariableService owner) : base(owner)
        {
        }

internal string ResolveDialogueOptionalFollowupVariableValue(DialogueScenarioContext context)
        {
            if (context?.IsRpg == true)
            {
                return "After completing the primary objective, optionally add one relevant follow-up.";
            }

            return string.Empty;
        }

internal string ResolveDialogueLatestUnresolvedIntentVariableValue(DialogueScenarioContext context)
        {
            if (context?.IsRpg != true || context.Target == null || context.Initiator == null)
            {
                return string.Empty;
            }

            return RpgNpcDialogueArchiveManager.Instance.BuildUnresolvedIntentSummary(context.Target, context.Initiator) ?? string.Empty;
        }

internal PromptTemplateVariableService.RpgRelationSnapshot ResolveRpgRelationSnapshot(DialogueScenarioContext context)
        {
            if (context?.IsRpg != true || context.Initiator == null || context.Target == null)
            {
                return PromptTemplateVariableService.RpgRelationSnapshot.Empty;
            }

            bool kinship = host.NodeSupport.HasAnyBloodRelationBetweenPair(context.Initiator, context.Target);
            string kinshipValue = kinship ? "yes" : "no";
            string romanceState = host.NodeSupport.ResolvePairRomanceState(context.Initiator, context.Target);
            string guidance = host.NodeSupport.BuildRpgKinshipBoundaryGuidanceText(
                RelationsMod.Settings,
                context.Initiator,
                context.Target,
                context) ?? string.Empty;
            string socialSummary = host.RpgBuilder.BuildPairSocialSummary(context.Initiator, context.Target, kinshipValue, romanceState);
            return new PromptTemplateVariableService.RpgRelationSnapshot(kinshipValue, romanceState, socialSummary, guidance);
        }

internal string BuildWorldTimeSeasonVariableValue(DialogueScenarioContext context)
        {
            Map map = host.ContextAssembler.ResolveEnvironmentMap(context);
            return map != null ? GenLocalDate.Season(map).Label() : Season.Undefined.Label();
        }

internal string BuildWorldWeatherVariableValue(DialogueScenarioContext context)
        {
            Map map = host.ContextAssembler.ResolveEnvironmentMap(context);
            return map?.weatherManager?.curWeather?.label ?? "Unknown";
        }

internal string BuildWorldTemperatureVariableValue(DialogueScenarioContext context)
        {
            Map map = host.ContextAssembler.ResolveEnvironmentMap(context);
            return map == null
                ? "Unknown"
                : Mathf.RoundToInt(map.mapTemperature?.OutdoorTemp ?? 0f).ToString();
        }

internal Vector2 GetLongLat(DialogueScenarioContext context)
        {
            Map map = host.ContextAssembler.ResolveEnvironmentMap(context);
            if (map == null || !WorldTileGuard.IsValidTile(map.Tile))
            {
                return Vector2.zero;
            }

            return Find.WorldGrid.LongLatOf(map.Tile);
        }

internal string BuildSceneTagsVariableText(DialogueScenarioContext context)
        {
            HashSet<string> tags = host.ContextAssembler.BuildScenarioTags(context, includePresetTags: true);
            if (tags == null || tags.Count == 0)
            {
                return "none";
            }

            return string.Join(", ", tags.OrderBy(tag => tag));
        }

internal string BuildEnvironmentParamsVariableText(DialogueScenarioContext context, EnvironmentPromptConfig envConfig)
        {
            Map map = host.ContextAssembler.ResolveEnvironmentMap(context);
            if (map == null)
            {
                return "No map context.";
            }

            if (!host.ContextAssembler.TryResolveFocusCell(map, context, out IntVec3 focusCell))
            {
                return "No focus cell.";
            }

            EnvironmentContextSwitchesConfig switches = envConfig?.EnvironmentContextSwitches ?? new EnvironmentContextSwitchesConfig();
            List<string> lines = host.ContextAssembler.BuildEnvironmentContextLines(map, focusCell, context, switches);
            if (lines == null || lines.Count == 0)
            {
                return "No environment parameters.";
            }

            string snapshot = Owner.BuildEnvironmentSnapshotVariableText(lines, maxItems: 5, maxChars: 220);
            if (string.IsNullOrWhiteSpace(snapshot))
            {
                return "See <environment> for full environment details.";
            }

            return "See <environment> for full environment details. Snapshot: " + snapshot;
        }

internal string BuildRecentWorldEventsVariableText(DialogueScenarioContext context, EnvironmentPromptConfig envConfig)
        {
            var clonedEnv = envConfig?.Clone() ?? new EnvironmentPromptConfig();
            if (clonedEnv.EventIntelPrompt == null)
            {
                clonedEnv.EventIntelPrompt = new EventIntelPromptConfig();
            }

            clonedEnv.EventIntelPrompt.Enabled = true;
            clonedEnv.EventIntelPrompt.ApplyToDiplomacy = true;
            clonedEnv.EventIntelPrompt.ApplyToRpg = true;
            string digest = host.ContextAssembler.BuildRecentWorldEventIntelCompactDigest(
                clonedEnv,
                context,
                maxItems: 2,
                maxChars: 260);
            return string.IsNullOrWhiteSpace(digest) ? "No recent world events." : digest;
        }

internal string BuildEnvironmentSnapshotVariableText(
            IEnumerable<string> lines,
            int maxItems,
            int maxChars)
        {
            List<string> source = lines?
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Trim())
                .ToList() ?? new List<string>();
            if (source.Count == 0)
            {
                return string.Empty;
            }

            string[] preferredPrefixes =
            {
                "Time:", "Date:", "Season:", "Weather:", "Location:", "Terrain:", "MapWealth:"
            };
            var selected = new List<string>();
            for (int i = 0; i < preferredPrefixes.Length; i++)
            {
                string prefix = preferredPrefixes[i];
                string match = source.FirstOrDefault(line => line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(match) && !selected.Contains(match))
                {
                    selected.Add(match);
                    if (selected.Count >= maxItems)
                    {
                        break;
                    }
                }
            }

            for (int i = 0; i < source.Count && selected.Count < maxItems; i++)
            {
                string line = source[i];
                if (!selected.Contains(line))
                {
                    selected.Add(line);
                }
            }

            string snapshot = string.Join(" | ", selected);
            if (snapshot.Length <= maxChars)
            {
                return snapshot;
            }

            return snapshot.Substring(0, Math.Max(16, maxChars)).TrimEnd() + "...";
        }

internal string BuildColonyStatusVariableText()
        {
            List<Map> homeMaps = Find.Maps?.Where(map => map != null && map.IsPlayerHome).ToList();
            if (homeMaps == null || homeMaps.Count == 0)
            {
                return "No active colony.";
            }

            int colonists = homeMaps.Sum(map => map.mapPawns?.FreeColonists?.Count ?? 0);
            int wealth = (int)homeMaps.Sum(map => map.wealthWatcher?.WealthTotal ?? 0f);
            string colonyName = Faction.OfPlayer?.Name ?? "Player Colony";
            int absTicks = Find.TickManager?.TicksAbs ?? 0;
            Vector2 longLat = WorldTileGuard.IsValidTile(homeMaps[0].Tile) ? Find.WorldGrid.LongLatOf(homeMaps[0].Tile) : Vector2.zero;
            string dateText = GenDate.DateFullStringAt(absTicks, longLat);
            return $"Colony: {colonyName}\nHomeMaps: {homeMaps.Count}\nColonists: {colonists}\nTotalWealth: {wealth}\nDate: {dateText}";
        }

internal string BuildColonyFactionsVariableText()
        {
            IEnumerable<Faction> factions = Find.FactionManager?.AllFactionsVisible?
                .Where(faction => faction != null && !faction.IsPlayer && !faction.defeated)
                .OrderByDescending(faction => faction.PlayerGoodwill)
                .Take(12);
            if (factions == null)
            {
                return "No known factions.";
            }

            var lines = new List<string>();
            foreach (Faction faction in factions)
            {
                string relation = faction.RelationKindWith(Faction.OfPlayer).ToString();
                lines.Add($"- {faction.Name}: goodwill={faction.PlayerGoodwill}, relation={relation}, tech={faction.def?.techLevel}");
            }

            return lines.Count == 0 ? "No known factions." : string.Join("\n", lines);
        }

internal string BuildCurrentFactionProfileVariableText(DialogueScenarioContext context)
        {
            Faction faction = context?.Faction ?? context?.Target?.Faction ?? context?.Initiator?.Faction;
            if (faction == null)
            {
                return "No faction context.";
            }

            Faction playerFaction = Faction.OfPlayer;
            string leader = faction.leader?.Name?.ToStringFull ?? "Unknown";
            string relation = Owner.BuildFactionRelationTowardPlayerText(faction, playerFaction);
            int? goodwill = faction == playerFaction || faction.IsPlayer
                ? null
                : Owner.TryGetGoodwillTowardPlayer(faction);
            string goodwillText = goodwill.HasValue ? goodwill.Value.ToString() : "N/A";
            return $"Faction: {faction.Name}\nDef: {faction.def?.defName}\nTech: {faction.def?.techLevel}\nGoodwill: {goodwillText}\nRelation: {relation}\nLeader: {leader}";
        }

internal string BuildFactionDescriptionVariableText(DialogueScenarioContext context)
        {
            Faction faction = context?.Faction ?? context?.Target?.Faction ?? context?.Initiator?.Faction;
            if (faction?.def == null)
            {
                return "No faction context.";
            }

            string prompt = FactionPromptManager.Instance.GetPrompt(faction);
            return string.IsNullOrWhiteSpace(prompt)
                ? "No faction prompt configured."
                : prompt.Trim();
        }

internal string BuildFactionRelationTowardPlayerText(Faction faction, Faction playerFaction)
        {
            if (faction == null || playerFaction == null)
            {
                return "Unknown";
            }

            if (faction == playerFaction || faction.IsPlayer)
            {
                return "Same faction (ally relation).";
            }

            return faction.RelationKindWith(playerFaction).ToString();
        }

internal int? TryGetGoodwillTowardPlayer(Faction faction)
        {
            Faction playerFaction = Faction.OfPlayer;
            if (faction == null || playerFaction == null || faction == playerFaction || faction.IsPlayer)
            {
                return null;
            }

            try
            {
                return faction.PlayerGoodwill;
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Relations] Failed to resolve faction goodwill for '{faction.Name ?? "Unknown"}': {ex.Message}");
                return null;
            }
        }

internal string BuildPawnPersonalityVariableText(DialogueScenarioContext context)
        {
            Pawn primary = context?.Target ?? context?.Initiator;
            if (primary == null)
            {
                return "No pawn context.";
            }

            GameComponent_RPGManager manager =
                GameComponent_RPGManager.Instance ?? Current.Game?.GetComponent<GameComponent_RPGManager>();
            string text = manager?.ResolveEffectivePawnPersonalityPrompt(primary, allowGenerateFallback: true) ?? string.Empty;
            return string.IsNullOrWhiteSpace(text)
                ? "No personality context."
                : text.Trim();
        }

internal string BuildPlayerPawnProfileVariableText(DialogueScenarioContext context)
        {
            Faction faction = context?.Faction ?? context?.Target?.Faction ?? context?.Initiator?.Faction;
            Pawn preferred = context?.Initiator != null && context.Initiator.Faction == Faction.OfPlayer
                ? context.Initiator
                : null;
            string text = host.ContextAssembler.BuildPlayerPawnContextForPrompt(faction, preferred);
            return string.IsNullOrWhiteSpace(text) ? "No player pawn context." : text;
        }

internal string BuildPlayerRoyaltySummaryVariableText(DialogueScenarioContext context)
        {
            Faction faction = context?.Faction ?? context?.Target?.Faction ?? context?.Initiator?.Faction;
            Pawn preferred = context?.Initiator != null && context.Initiator.Faction == Faction.OfPlayer
                ? context.Initiator
                : null;
            string text = host.ContextAssembler.BuildPlayerRoyaltySummaryForPrompt(faction, preferred);
            return string.IsNullOrWhiteSpace(text) ? "No empire royalty context." : text;
        }

internal string BuildFactionSettlementSummaryVariableText(DialogueScenarioContext context)
        {
            Faction faction = context?.Faction ?? context?.Target?.Faction ?? context?.Initiator?.Faction;
            string text = host.ContextAssembler.BuildFactionSettlementSummaryForPrompt(faction);
            return string.IsNullOrWhiteSpace(text) ? "No settlement context." : text;
        }

internal string BuildFactionRelationBandVariableValue(DialogueScenarioContext context)
        {
            Faction faction = context?.Faction ?? context?.Target?.Faction ?? context?.Initiator?.Faction;
            if (faction == null || faction == Faction.OfPlayer)
            {
                return "PlayerFaction";
            }

            return faction.PlayerRelationKind.ToString();
        }

internal string BuildPawnTraitsSummaryVariableValue(DialogueScenarioContext context)
        {
            Pawn target = context?.Target;
            if (target?.story?.traits == null)
            {
                return "No traits context.";
            }

            var traitStrings = new List<string>();
            foreach (Trait trait in target.story.traits.allTraits)
            {
                if (trait == null)
                {
                    continue;
                }

                string label = trait.Label ?? string.Empty;
                if (label.Length > 0)
                {
                    traitStrings.Add(label);
                }
            }

            return traitStrings.Count > 0 ? string.Join(", ", traitStrings) : "No traits.";
        }

internal string BuildFactionIdeologySummaryVariableValue(DialogueScenarioContext context)
        {
            Faction faction = context?.Faction ?? context?.Target?.Faction ?? context?.Initiator?.Faction;
            Ideo ideo = faction?.ideos?.PrimaryIdeo;
            if (ideo == null)
            {
                return "No ideology.";
            }

            return ideo.name ?? "Unknown ideology";
        }

internal string BuildFactionTechLevelVariableValue(DialogueScenarioContext context)
        {
            Faction faction = context?.Faction ?? context?.Target?.Faction ?? context?.Initiator?.Faction;
            TechLevel techLevel = faction?.def?.techLevel ?? TechLevel.Undefined;
            return techLevel.ToString();
        }

internal string BuildSocialDiplomacyStanceVariableValue(DialogueScenarioContext context)
        {
            Faction faction = context?.Faction ?? context?.Target?.Faction ?? context?.Initiator?.Faction;
            if (faction == null || faction == Faction.OfPlayer)
            {
                return "Self";
            }

            FactionRelationKind relationKind = faction.PlayerRelationKind;
            int goodwill = faction.PlayerGoodwill;
            return $"{relationKind} (Goodwill: {goodwill})";
        }

internal string BuildAvailableActionNamesVariableValue(DialogueScenarioContext context)
        {
            return "adjust_goodwill, send_gift, request_aid, request_caravan, request_visitor, "
                 + "request_raid, request_item_airdrop, request_info, pay_prisoner_ransom, "
                 + "create_quest, trigger_incident, exit_dialogue, go_offline, set_dnd, "
                 + "reject_request, publish_public_post";
        }

internal string BuildResponseContractBodyVariableValue(DialogueScenarioContext context)
        {
            return "Return exactly one JSON object. Required key: visible_dialogue. "
                 + "Optional key: actions (array of {action, parameters} objects). "
                 + "visible_dialogue must be a single in-character line. "
                 + "If making an execution commitment, include matching action in actions array.";
        }
    }

    internal sealed class PromptTemplateVariableServiceParts
    {
        internal readonly PromptTemplateVariableService Owner;
        internal readonly PromptTemplateVariableSlice1 Slice1;
        internal readonly PromptTemplateVariableSlice2 Slice2;
        internal PromptTemplateVariableServiceParts(PromptTemplateVariableService owner)
        {
            Owner = owner;
            Slice1 = new PromptTemplateVariableSlice1(owner);
            Slice2 = new PromptTemplateVariableSlice2(owner);
        }
    }

}
