using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Diagnostics;
using Ustas.RimAI.Communication.Relations.Guards;
using RimWorld;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Core.Communication;

namespace Ustas.RimAI.Communication.Relations.Prompting
{
    internal enum ReflectedCustomVariableKind
    {
        Context = 0,
        Environment = 1,
        Pawn = 2
    }

    internal sealed class ReflectedCustomVariable
    {
        public string LegacyName { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string ModId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ReflectedCustomVariableKind Kind { get; set; }

        public bool MatchesLegacyToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string normalized = token.Trim();
            return string.Equals(normalized, LegacyName, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "pawn." + LegacyName, StringComparison.OrdinalIgnoreCase);
        }

        public PromptRuntimeVariableDefinition ToDefinition(string sourceId, string sourceLabel)
        {
            string description = string.IsNullOrWhiteSpace(Description) ? Path : Description;
            return new PromptRuntimeVariableDefinition(Path, sourceId, sourceLabel, description, true);
        }
    }

    internal static class PromptRuntimeVariableBridge
    {
        private static readonly object LegacyCleanupSyncRoot = new object();
        private static readonly string[] LegacyRimChatContextVariableKeys =
        {
            "rimchat_last_session_summary",
            "rimchat_last_diplomacy_summary",
            "rimchat_last_rpg_summary",
            "rimchat_recent_session_summaries"
        };

        private const string RimChatSummaryVariableName = "rimchat_summary";
        private const int RimChatSummaryMaxChars = 1200;
        private const int RimChatSummaryVariablePriority = 100;

        private static readonly object BridgeInitSyncRoot = new object();
        private static readonly object CustomVariableSnapshotSyncRoot = new object();
        private static readonly object CustomVariableRefreshSyncRoot = new object();
        private static readonly List<ReflectedCustomVariable> CustomVariableSnapshot = new List<ReflectedCustomVariable>();
        private const int CustomVariableRefreshCooldownMs = 1000;
        private static readonly string[] KnownRelationsModIds =
        {
            "ustas.rimai.communication.relations",
            "rimchat",
            "rim_chat",
            "timchat"
        };

        private static bool _legacyCleanupAttempted;
        private static bool _bridgeInitAttempted;
        private static bool _bridgeRuntimeAvailable;
        private static string _bridgeFailureReason = string.Empty;
        private static int _lastCustomVariableRefreshTick = -1;
        private static string _lastCustomVariableTelemetry = string.Empty;

        public static void InitializeBridgeChain()
        {
            if (_bridgeInitAttempted)
            {
                return;
            }

            lock (BridgeInitSyncRoot)
            {
                if (_bridgeInitAttempted)
                {
                    return;
                }

                _bridgeInitAttempted = true;
                if (!IsDependencyAvailable("rimtalk"))
                {
                    _bridgeRuntimeAvailable = false;
                    _bridgeFailureReason = "RimTalk dependency not detected.";
                    return;
                }

                try
                {
                    if (!PromptVariableHostAccess.HasHost)
                    {
                        _bridgeRuntimeAvailable = false;
                        _bridgeFailureReason = "Prompt variable host is not registered.";
                        return;
                    }

                    StrictLegacyCleanup();
                    ValidateRimTalkBridgeSignaturesOrFail();
                    RegisterRimChatSummaryVariable();
                    _bridgeRuntimeAvailable = true;
                    _bridgeFailureReason = string.Empty;
                    RefreshRimTalkCustomVariableSnapshot(force: true);
                }
                catch (Exception ex)
                {
                    _bridgeRuntimeAvailable = false;
                    _bridgeFailureReason = ex.Message ?? "Bridge initialization failed.";
                    DebugLogger.Error($"RimTalk bridge initialization failed. Bridge chain blocked: {_bridgeFailureReason}");
                }
            }
        }

        public static string GetBridgeFailureReason()
        {
            return _bridgeFailureReason ?? string.Empty;
        }

        public static void ValidateRimTalkBridgeSignaturesOrFail()
        {
            if (!PromptVariableHostAccess.HasHost)
            {
                throw new MissingMemberException("Prompt variable host is not registered.");
            }
        }

        public static void StrictLegacyCleanup()
        {
            TryCleanupLegacyRimChatVariables(force: true);
        }

        public static IReadOnlyList<ReflectedCustomVariable> GetRimTalkCustomVariablesSnapshot()
        {
            lock (CustomVariableSnapshotSyncRoot)
            {
                return CustomVariableSnapshot.Select(CloneVariable).ToList();
            }
        }

        public static void RefreshRimTalkCustomVariableSnapshot(bool force = false)
        {
            if (!_bridgeInitAttempted)
            {
                InitializeBridgeChain();
            }

            if (!_bridgeRuntimeAvailable)
            {
                return;
            }

            if (ShouldThrottleCustomVariableRefresh(force))
            {
                return;
            }

            lock (CustomVariableRefreshSyncRoot)
            {
                if (ShouldThrottleCustomVariableRefresh(force))
                {
                    return;
                }

                var host = PromptVariableHostAccess.Current;
                if (host == null)
                {
                    return;
                }

                int rawCount = 0;
                int duplicateCount = 0;
                string sampleType = string.Empty;
                var results = new List<ReflectedCustomVariable>();
                var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    foreach (PromptCustomVariableDescriptor item in host.GetCustomVariables() ?? Array.Empty<PromptCustomVariableDescriptor>())
                    {
                        rawCount++;
                        if (string.IsNullOrWhiteSpace(sampleType) && item != null)
                        {
                            sampleType = nameof(PromptCustomVariableDescriptor);
                        }

                        ReflectedCustomVariable variable = ParseCustomVariableDescriptor(item);
                        if (variable == null || string.IsNullOrWhiteSpace(variable.Path))
                        {
                            continue;
                        }

                        if (!uniquePaths.Add(variable.Path))
                        {
                            duplicateCount++;
                            continue;
                        }

                        results.Add(variable);
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.WarningGated($"Failed to refresh RimTalk custom variable snapshot: {ex.Message}");
                    return;
                }

                if (rawCount > 0 && results.Count == 0)
                {
                    BlockBridgeBySnapshotContractMismatch(rawCount, sampleType);
                    return;
                }

                lock (CustomVariableSnapshotSyncRoot)
                {
                    CustomVariableSnapshot.Clear();
                    CustomVariableSnapshot.AddRange(results);
                    _lastCustomVariableRefreshTick = Environment.TickCount;
                }

                LogCustomVariableSnapshotTelemetry(rawCount, results.Count, duplicateCount, force);
            }
        }

        public static string BuildModVariablesSectionContent()
        {
            List<ReflectedCustomVariable> customVariables = GetCustomVariables()
                .Where(item => item != null)
                .OrderBy(item => item.LegacyName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (customVariables.Count == 0)
            {
                return string.Empty;
            }

            var lines = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < customVariables.Count; i++)
            {
                string token = ResolveRawTokenFromVariable(customVariables[i]);
                if (string.IsNullOrWhiteSpace(token) || !seen.Add(token))
                {
                    continue;
                }

                lines.Add(token);
            }

            return lines.Count == 0 ? string.Empty : string.Join("\n", lines);
        }

        public static string ResolveRawToken(string variablePath)
        {
            string normalized = variablePath?.Trim() ?? string.Empty;
            switch (normalized)
            {
                case "pawn.rimtalk.context":
                    return "{{ context }}";
                case "dialogue.rimtalk.prompt":
                    return "{{ prompt }}";
                case "dialogue.rimtalk.history":
                    return "{{ chat.history }}";
                case "dialogue.rimtalk.history_simplified":
                    return "{{ chat.history_simplified }}";
            }

            ReflectedCustomVariable custom = GetCustomVariables()
                .FirstOrDefault(item => string.Equals(item.Path, normalized, StringComparison.OrdinalIgnoreCase));
            return custom == null
                ? "{{ " + normalized + " }}"
                : ResolveRawTokenFromVariable(custom);
        }

        public static void RegisterRimChatSummaryVariable()
        {
            var host = PromptVariableHostAccess.Current;
            if (host == null)
            {
                throw new MissingMethodException("Prompt variable host is not registered.");
            }

            host.RegisterContextVariable(
                SanitizeModId(KnownRelationsModIds[0]),
                RimChatSummaryVariableName,
                _ => BuildRimChatSummaryAggregateText(),
                "RimChat cross-channel summary aggregate.",
                RimChatSummaryVariablePriority);
        }

        public static string BuildRimChatSummaryAggregateText()
        {
            if (Current.Game == null || Find.FactionManager == null)
            {
                return string.Empty;
            }

            var lines = new List<string>();
            foreach (Faction faction in Find.FactionManager.AllFactionsListForReading)
            {
                if (faction == null || faction.IsPlayer || faction.defeated)
                {
                    continue;
                }

                FactionLeaderMemory memory = LeaderMemoryManager.Instance.GetMemory(faction);
                if (memory == null)
                {
                    continue;
                }

                IEnumerable<CrossChannelSummaryRecord> summaries = (memory.DiplomacySessionSummaries ?? new List<CrossChannelSummaryRecord>())
                    .Concat(memory.RpgDepartSummaries ?? new List<CrossChannelSummaryRecord>())
                    .Where(item => item != null && !string.IsNullOrWhiteSpace(item.SummaryText))
                    .OrderByDescending(item => item.GameTick)
                    .Take(2);

                foreach (CrossChannelSummaryRecord summary in summaries)
                {
                    lines.Add($"[{faction.Name}] {summary.SummaryText.Trim()}");
                }
            }

            if (lines.Count == 0)
            {
                return string.Empty;
            }

            string content = string.Join("\n", lines);
            return TrimToBudget(content, RimChatSummaryMaxChars);
        }

        public static IReadOnlyList<PromptRuntimeVariableDefinition> GetBuiltinRimTalkDefinitions(string sourceId, string sourceLabel)
        {
            return new List<PromptRuntimeVariableDefinition>
            {
                new PromptRuntimeVariableDefinition("pawn.rimtalk.context", sourceId, sourceLabel, "RimChat_TemplateVar_rimtalk_context_Desc", true),
                new PromptRuntimeVariableDefinition("dialogue.rimtalk.prompt", sourceId, sourceLabel, "RimChat_TemplateVar_rimtalk_prompt_Desc", true),
                new PromptRuntimeVariableDefinition("dialogue.rimtalk.history", sourceId, sourceLabel, "RimChat_TemplateVar_rimtalk_history_Desc", true),
                new PromptRuntimeVariableDefinition("dialogue.rimtalk.history_simplified", sourceId, sourceLabel, "RimChat_TemplateVar_rimtalk_history_simplified_Desc", true)
            };
        }

        public static bool IsRimTalkBridgeEnabled()
        {
            InitializeBridgeChain();
            return _bridgeRuntimeAvailable;
        }

        public static void TryCleanupLegacyRimChatVariables(bool force = false)
        {
            if (ShouldSkipLegacyCleanup(force))
            {
                return;
            }

            lock (LegacyCleanupSyncRoot)
            {
                if (ShouldSkipLegacyCleanup(force))
                {
                    return;
                }

                _legacyCleanupAttempted = true;
                CleanupLegacyVariablesInternal();
            }
        }

        public static string GetDescriptionKey(string path)
        {
            switch (path)
            {
                case "ctx.channel":
                    return "RimChat_TemplateVar_ctx_channel_Desc";
                case "ctx.mode":
                    return "RimChat_TemplateVar_ctx_mode_Desc";
                case "system.target_language":
                    return "RimChat_TemplateVar_system_target_language_Desc";
                case "system.game_language":
                    return "RimChat_TemplateVar_system_game_language_Desc";
                case "world.time.hour":
                    return "RimChat_TemplateVar_world_time_hour_Desc";
                case "world.time.day":
                    return "RimChat_TemplateVar_world_time_day_Desc";
                case "world.time.quadrum":
                    return "RimChat_TemplateVar_world_time_quadrum_Desc";
                case "world.time.year":
                    return "RimChat_TemplateVar_world_time_year_Desc";
                case "world.time.season":
                    return "RimChat_TemplateVar_world_time_season_Desc";
                case "world.time.date":
                    return "RimChat_TemplateVar_world_time_date_Desc";
                case "world.weather":
                    return "RimChat_TemplateVar_world_weather_Desc";
                case "world.temperature":
                    return "RimChat_TemplateVar_world_temperature_Desc";
                case "world.faction.name":
                    return "RimChat_TemplateVar_world_faction_name_Desc";
                case "world.faction.description":
                    return "RimChat_TemplateVar_world_faction_description_Desc";
                case "pawn.initiator.name":
                    return "RimChat_TemplateVar_pawn_initiator_name_Desc";
                case "pawn.initiator":
                    return "RimChat_TemplateVar_pawn_initiator_Desc";
                case "pawn.target.name":
                    return "RimChat_TemplateVar_pawn_target_name_Desc";
                case "pawn.target":
                    return "RimChat_TemplateVar_pawn_target_Desc";
                case "pawn.recipient":
                    return "RimChat_TemplateVar_pawn_recipient_Desc";
                case "pawn.recipient.name":
                    return "RimChat_TemplateVar_pawn_recipient_name_Desc";
                case "world.social.origin_type":
                    return "RimChat_TemplateVar_world_social_origin_type_Desc";
                case "world.social.category":
                    return "RimChat_TemplateVar_world_social_category_Desc";
                case "world.social.source_faction":
                    return "RimChat_TemplateVar_world_social_source_faction_Desc";
                case "world.social.target_faction":
                    return "RimChat_TemplateVar_world_social_target_faction_Desc";
                case "world.social.source_label":
                    return "RimChat_TemplateVar_world_social_source_label_Desc";
                case "world.social.credibility_label":
                    return "RimChat_TemplateVar_world_social_credibility_label_Desc";
                case "world.social.credibility_value":
                    return "RimChat_TemplateVar_world_social_credibility_value_Desc";
                case "world.social.fact_lines":
                    return "RimChat_TemplateVar_world_social_fact_lines_Desc";
                case "world.scene_tags":
                    return "RimChat_TemplateVar_scene_tags_Desc";
                case "world.environment_params":
                    return "RimChat_TemplateVar_environment_params_Desc";
                case "world.recent_world_events":
                    return "RimChat_TemplateVar_recent_world_events_Desc";
                case "world.colony_status":
                    return "RimChat_TemplateVar_colony_status_Desc";
                case "world.colony_factions":
                    return "RimChat_TemplateVar_colony_factions_Desc";
                case "world.current_faction_profile":
                    return "RimChat_TemplateVar_current_faction_profile_Desc";
                case "pawn.target.profile":
                    return "RimChat_TemplateVar_rpg_target_profile_Desc";
                case "pawn.initiator.profile":
                    return "RimChat_TemplateVar_rpg_initiator_profile_Desc";
                case "pawn.player.profile":
                    return "RimChat_TemplateVar_player_pawn_profile_Desc";
                case "pawn.player.royalty_summary":
                    return "RimChat_TemplateVar_player_royalty_summary_Desc";
                case "pawn.profile":
                    return "RimChat_TemplateVar_pawn_profile_Desc";
                case "pawn.personality":
                    return "RimChat_TemplateVar_pawn_personality_Desc";
                case "pawn.relation.kinship":
                    return "RimChat_TemplateVar_pawn_relation_kinship_Desc";
                case "pawn.relation.romance_state":
                    return "RimChat_TemplateVar_pawn_relation_romance_state_Desc";
                case "pawn.relation.social_summary":
                    return "RimChat_TemplateVar_pawn_relation_social_summary_Desc";
                case "pawn.speaker.kind":
                    return "RimChat_TemplateVar_pawn_speaker_kind_Desc";
                case "pawn.speaker.default_sound":
                    return "RimChat_TemplateVar_pawn_speaker_default_sound_Desc";
                case "pawn.speaker.animal_sound":
                    return "RimChat_TemplateVar_pawn_speaker_animal_sound_Desc";
                case "pawn.speaker.baby_sound":
                    return "RimChat_TemplateVar_pawn_speaker_baby_sound_Desc";
                case "pawn.speaker.mechanoid_sound":
                    return "RimChat_TemplateVar_pawn_speaker_mechanoid_sound_Desc";
                case "pawn.pronouns.subject":
                    return "RimChat_TemplateVar_pawn_pronouns_subject_Desc";
                case "pawn.pronouns.object":
                    return "RimChat_TemplateVar_pawn_pronouns_object_Desc";
                case "pawn.pronouns.possessive":
                    return "RimChat_TemplateVar_pawn_pronouns_possessive_Desc";
                case "pawn.pronouns.subject_lower":
                    return "RimChat_TemplateVar_pawn_pronouns_subject_lower_Desc";
                case "pawn.pronouns.be_verb":
                    return "RimChat_TemplateVar_pawn_pronouns_be_verb_Desc";
                case "pawn.pronouns.seek_verb":
                    return "RimChat_TemplateVar_pawn_pronouns_seek_verb_Desc";
                case "world.faction_settlement_summary":
                    return "RimChat_TemplateVar_faction_settlement_summary_Desc";
                case "dialogue.summary":
                    return "RimChat_TemplateVar_dialogue_summary_Desc";
                case "dialogue.guidance":
                    return "RimChat_TemplateVar_dialogue_guidance_Desc";
                case "dialogue.intent_hint":
                    return "RimChat_TemplateVar_dialogue_intent_hint_Desc";
                case "dialogue.template_line":
                    return "RimChat_TemplateVar_dialogue_template_line_Desc";
                case "dialogue.example_line":
                    return "RimChat_TemplateVar_dialogue_example_line_Desc";
                case "dialogue.examples":
                    return "RimChat_TemplateVar_dialogue_examples_Desc";
                case "dialogue.action_names":
                    return "RimChat_TemplateVar_dialogue_action_names_Desc";
                case "dialogue.primary_objective":
                    return "RimChat_TemplateVar_dialogue_primary_objective_Desc";
                case "dialogue.optional_followup":
                    return "RimChat_TemplateVar_dialogue_optional_followup_Desc";
                case "dialogue.latest_unresolved_intent":
                    return "RimChat_TemplateVar_dialogue_latest_unresolved_intent_Desc";
                case "dialogue.topic_shift_rule":
                    return "RimChat_TemplateVar_dialogue_topic_shift_rule_Desc";
                case "dialogue.api_limits_body":
                    return "RimChat_TemplateVar_dialogue_api_limits_body_Desc";
                case "dialogue.quest_guidance_body":
                    return "RimChat_TemplateVar_dialogue_quest_guidance_body_Desc";
                case "dialogue.response_contract_body":
                    return "RimChat_TemplateVar_dialogue_response_contract_body_Desc";
                case "dialogue.strategy_player_negotiator_context_body":
                    return "RimChat_TemplateVar_dialogue_strategy_player_negotiator_context_body_Desc";
                case "dialogue.strategy_fact_pack_body":
                    return "RimChat_TemplateVar_dialogue_strategy_fact_pack_body_Desc";
                case "dialogue.strategy_scenario_dossier_body":
                    return "RimChat_TemplateVar_dialogue_strategy_scenario_dossier_body_Desc";
                case "dialogue.mandatory_race_profile_body":
                    return "RimChat_TemplateVar_dialogue_mandatory_race_profile_body_Desc";
                case "pawn.rimtalk.context":
                    return "RimChat_TemplateVar_rimtalk_context_Desc";
                case "dialogue.rimtalk.prompt":
                    return "RimChat_TemplateVar_rimtalk_prompt_Desc";
                case "dialogue.rimtalk.history":
                    return "RimChat_TemplateVar_rimtalk_history_Desc";
                case "dialogue.rimtalk.history_simplified":
                    return "RimChat_TemplateVar_rimtalk_history_simplified_Desc";
                case "system.punctuation.open_paren":
                    return "RimChat_TemplateVar_system_punctuation_open_paren_Desc";
                case "system.punctuation.close_paren":
                    return "RimChat_TemplateVar_system_punctuation_close_paren_Desc";
                default:
                    return string.Empty;
            }
        }

        public static bool IsDependencyAvailable(string token)
        {
            return LoadedModManager.RunningModsListForReading != null &&
                   LoadedModManager.RunningModsListForReading.Exists(mod =>
                       mod != null &&
                       (ContainsToken(mod.PackageIdPlayerFacing, token) || ContainsToken(mod.Name, token)));
        }

        public static List<ReflectedCustomVariable> GetCustomVariables()
        {
            InitializeBridgeChain();
            if (!_bridgeRuntimeAvailable)
            {
                return new List<ReflectedCustomVariable>();
            }

            RefreshRimTalkCustomVariableSnapshot();
            return GetRimTalkCustomVariablesSnapshot().ToList();
        }

        public static bool TryResolveCustomVariableValue(ReflectedCustomVariable variable, PromptRuntimeVariableContext context, out string value)
        {
            value = string.Empty;
            if (variable == null)
            {
                return false;
            }

            if (variable.Kind == ReflectedCustomVariableKind.Pawn)
            {
                return PromptVariableHostAccess.Current?.TryGetPawnVariable(variable.LegacyName, ResolvePrimaryPawn(context), out value) == true;
            }

            if (variable.Kind == ReflectedCustomVariableKind.Environment)
            {
                return PromptVariableHostAccess.Current?.TryGetEnvironmentVariable(variable.LegacyName, ResolveMap(context), out value) == true;
            }

            return PromptVariableHostAccess.Current?.TryGetContextVariable(
                variable.LegacyName,
                ToResolveRequest(context),
                out value) == true;
        }

        public static string BuildRimTalkContextBlock(PromptRuntimeVariableContext context)
        {
            DialogueScenarioContext scenario = context?.ScenarioContext;
            Pawn pawn = ResolvePrimaryPawn(context);
            if (pawn == null)
            {
                return scenario?.Faction?.Name ?? string.Empty;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Pawn: {pawn.LabelShortCap}");
            if (pawn.Faction != null)
            {
                sb.AppendLine("Faction: " + pawn.Faction.Name);
            }

            if (pawn.story?.traits?.allTraits != null && pawn.story.traits.allTraits.Count > 0)
            {
                string traits = string.Join(", ", pawn.story.traits.allTraits.Take(4).Select(item => item?.LabelCap).Where(item => !string.IsNullOrWhiteSpace(item)));
                if (!string.IsNullOrWhiteSpace(traits))
                {
                    sb.AppendLine("Traits: " + traits);
                }
            }

            if (pawn.CurJob != null)
            {
                sb.AppendLine("Job: " + (pawn.CurJob.def?.label ?? string.Empty));
            }

            return sb.ToString().Trim();
        }

        public static string BuildRimTalkPromptBlock(PromptRuntimeVariableContext context)
        {
            var parts = new List<string>();
            string contextBlock = BuildRimTalkContextBlock(context);
            if (!string.IsNullOrWhiteSpace(contextBlock))
            {
                parts.Add(contextBlock);
            }

            Map map = ResolveMap(context);
            if (map != null)
            {
                int ticks = Find.TickManager?.TicksGame ?? 0;
                float longitude = WorldTileGuard.IsValidTile(map.Tile) ? Find.WorldGrid.LongLatOf(map.Tile).x : 0f;
                parts.Add(
                    $"Time: {GenDate.HourOfDay(ticks, longitude)}h, day {GenDate.DayOfQuadrum(ticks, longitude) + 1}, " +
                    $"{GenDate.Quadrum(ticks, longitude).Label()}, year {GenDate.Year(ticks, longitude)}.");
                parts.Add($"Weather: {map.weatherManager?.curWeather?.label ?? "unknown"}, temperature {Mathf.RoundToInt(map.mapTemperature.OutdoorTemp)}C.");
            }

            string history = BuildRimTalkHistoryBlock(context, true);
            if (!string.IsNullOrWhiteSpace(history))
            {
                parts.Add(history);
            }

            return string.Join("\n", parts.Where(item => !string.IsNullOrWhiteSpace(item))).Trim();
        }

        public static string BuildRimTalkHistoryBlock(PromptRuntimeVariableContext context, bool simplified)
        {
            DialogueScenarioContext scenario = context?.ScenarioContext;
            if (scenario == null)
            {
                return string.Empty;
            }

            string text = string.Empty;
            if (scenario.IsRpg && scenario.Target != null)
            {
                bool allowMemoryCompressionScheduling = RpgPromptTurnContextScope.Current?.AllowMemoryCompressionScheduling ?? true;
                bool allowMemoryColdLoad = RpgPromptTurnContextScope.Current?.AllowMemoryColdLoad ?? true;
                text = RpgNpcDialogueArchiveManager.Instance.BuildPromptMemoryBlock(
                    scenario.Target,
                    scenario.Initiator,
                    simplified ? 4 : 8,
                    simplified ? 420 : 900,
                    allowCompressionScheduling: allowMemoryCompressionScheduling,
                    allowCacheLoad: allowMemoryColdLoad);
            }

            text = NormalizeWhitespace(text);
            if (!simplified || text.Length <= 360)
            {
                return text;
            }

            return text.Substring(0, 360).TrimEnd() + "...";
        }

        public static string GetJsonInstruction()
        {
            try
            {
                return PromptVariableHostAccess.Current?.GetJsonInstruction() ?? GetFallbackJsonInstruction();
            }
            catch (Exception ex)
            {
                DebugLogger.WarningGated($"Failed to query RimTalk JSON instruction: {ex.Message}");
                return GetFallbackJsonInstruction();
            }
        }

        public static bool ContainsToken(string value, string token)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static ReflectedCustomVariable ParseCustomVariableDescriptor(PromptCustomVariableDescriptor item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Name))
            {
                return null;
            }

            ReflectedCustomVariableKind kind = ParseKind(item.Kind);
            string normalizedName = NormalizeLegacyName(item.Name, kind);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return null;
            }

            return new ReflectedCustomVariable
            {
                LegacyName = normalizedName,
                Path = BuildNamespacedPath(normalizedName, kind),
                ModId = item.ModId ?? string.Empty,
                Description = item.Description ?? string.Empty,
                Kind = kind
            };
        }

        private static string BuildNamespacedPath(string name, ReflectedCustomVariableKind kind)
        {
            switch (kind)
            {
                case ReflectedCustomVariableKind.Pawn:
                    return "pawn.rimtalk." + name;
                case ReflectedCustomVariableKind.Environment:
                    return "world.rimtalk." + name;
                default:
                    return "dialogue.rimtalk." + name;
            }
        }

        private static string NormalizeLegacyName(string name, ReflectedCustomVariableKind kind)
        {
            string normalized = (name ?? string.Empty).Trim().Replace(" ", "_");
            if (kind == ReflectedCustomVariableKind.Pawn &&
                normalized.StartsWith("pawn.", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring("pawn.".Length);
            }

            return normalized;
        }

        private static ReflectedCustomVariableKind ParseKind(string raw)
        {
            if (int.TryParse(raw, out int numericKind))
            {
                if (numericKind == 1)
                {
                    return ReflectedCustomVariableKind.Environment;
                }

                if (numericKind == 2)
                {
                    return ReflectedCustomVariableKind.Pawn;
                }
            }

            if (string.Equals(raw, "Pawn", StringComparison.OrdinalIgnoreCase))
            {
                return ReflectedCustomVariableKind.Pawn;
            }

            if (string.Equals(raw, "Environment", StringComparison.OrdinalIgnoreCase))
            {
                return ReflectedCustomVariableKind.Environment;
            }

            return ReflectedCustomVariableKind.Context;
        }

        private static bool ShouldThrottleCustomVariableRefresh(bool force)
        {
            if (force)
            {
                return false;
            }

            int now = Environment.TickCount;
            lock (CustomVariableSnapshotSyncRoot)
            {
                if (_lastCustomVariableRefreshTick < 0)
                {
                    return false;
                }

                uint elapsed = unchecked((uint)(now - _lastCustomVariableRefreshTick));
                return elapsed < CustomVariableRefreshCooldownMs;
            }
        }

        private static void BlockBridgeBySnapshotContractMismatch(int rawCount, string sampleType)
        {
            _bridgeRuntimeAvailable = false;
            _bridgeFailureReason = "RimTalk custom variable contract mismatch: no variable could be parsed.";
            lock (CustomVariableSnapshotSyncRoot)
            {
                CustomVariableSnapshot.Clear();
                _lastCustomVariableRefreshTick = Environment.TickCount;
            }

            string typeLabel = string.IsNullOrWhiteSpace(sampleType) ? "unknown" : sampleType;
            Log.Error(
                "[RimAI.Relations] Bridge blocked due to custom-variable contract mismatch. " +
                $"raw_count={rawCount}, sample_type={typeLabel}. " +
                "Please verify RimTalk GetAllCustomVariables() payload shape.");
        }

        private static void LogCustomVariableSnapshotTelemetry(
            int rawCount,
            int parsedCount,
            int duplicateCount,
            bool force)
        {
            string telemetry = $"{rawCount}|{parsedCount}|{duplicateCount}|{force}";
            if (!force && string.Equals(telemetry, _lastCustomVariableTelemetry, StringComparison.Ordinal))
            {
                return;
            }

            _lastCustomVariableTelemetry = telemetry;
            Log.Message(
                "[RimAI.Relations] RimTalk custom variable snapshot refreshed. " +
                $"raw_count={rawCount}, parsed_count={parsedCount}, duplicate_count={duplicateCount}, force={force}.");
        }

        private static PromptVariableResolveRequest ToResolveRequest(PromptRuntimeVariableContext context)
        {
            DialogueScenarioContext scenario = context?.ScenarioContext;
            Pawn pawn = ResolvePrimaryPawn(context);
            var pawns = new List<object>();
            if (scenario?.Initiator != null)
            {
                pawns.Add(scenario.Initiator);
            }

            if (scenario?.Target != null && !pawns.Contains(scenario.Target))
            {
                pawns.Add(scenario.Target);
            }

            return new PromptVariableResolveRequest
            {
                CurrentPawn = pawn,
                Map = ResolveMap(context),
                Pawns = pawns
            };
        }


        private static Pawn ResolvePrimaryPawn(PromptRuntimeVariableContext context)
        {
            return context?.ScenarioContext?.Target ?? context?.ScenarioContext?.Initiator;
        }

        private static Map ResolveMap(PromptRuntimeVariableContext context)
        {
            Pawn pawn = ResolvePrimaryPawn(context);
            return pawn?.MapHeld ?? Find.CurrentMap;
        }

        private static string NormalizeWhitespace(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return string.Join(" ",
                text.Replace("\r\n", "\n")
                    .Replace('\r', '\n')
                    .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(item => item.Trim())
                    .Where(item => item.Length > 0));
        }

        private static string GetFallbackJsonInstruction()
        {
            return "Output gameplay effects only as a trailing {\"actions\":[...]} JSON object. Omit the JSON block when no action is needed. Hard immersion rules for visible dialogue: start directly in-character and never begin with parenthetical notes/metadata (for example \"(重复问候...)\" or \"（状态说明...）\"); do not expose mechanism terms or system values (goodwill/threshold/cooldown/API/system prompt/token/requestId/api_limits/blocked actions); do not output status-panel sentence patterns such as key:123.";
        }

        private static bool ShouldSkipLegacyCleanup(bool force)
        {
            return !force && _legacyCleanupAttempted;
        }

        private static void CleanupLegacyVariablesInternal()
        {
            var host = PromptVariableHostAccess.Current;
            if (host == null)
            {
                return;
            }

            int unregisteredMods = 0;
            foreach (string modId in CollectLegacyRelationsModIds())
            {
                try
                {
                    host.UnregisterMod(modId);
                    unregisteredMods++;
                }
                catch (Exception ex)
                {
                    DebugLogger.WarningGated($"Failed to unregister legacy mod hooks for '{modId}': {ex.Message}");
                }
            }

            int removedRuntimeKeys = host.RemoveRuntimeVariables(key =>
                LegacyRimChatContextVariableKeys.Any(item =>
                    string.Equals(item, key, StringComparison.OrdinalIgnoreCase)) ||
                ContainsToken(key, "rimchat"));

            if (unregisteredMods > 0 || removedRuntimeKeys > 0)
            {
                Log.Message(
                    "[RimAI.Relations] Typed legacy cleanup completed. " +
                    $"mods_unregistered={unregisteredMods}, runtime_keys={removedRuntimeKeys}.");
            }
        }


        private static HashSet<string> CollectLegacyRelationsModIds()
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < KnownRelationsModIds.Length; i++)
            {
                string id = KnownRelationsModIds[i];
                if (!string.IsNullOrWhiteSpace(id))
                {
                    ids.Add(id.Trim());
                    ids.Add(SanitizeModId(id));
                }
            }

            if (LoadedModManager.RunningModsListForReading == null)
            {
                return ids;
            }

            foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
            {
                string packageId = mod?.PackageIdPlayerFacing ?? string.Empty;
                if (!ContainsToken(packageId, "rimchat"))
                {
                    continue;
                }

                ids.Add(packageId.Trim());
                ids.Add(SanitizeModId(packageId));
            }

            return ids;
        }

        private static string ResolveRawTokenFromVariable(ReflectedCustomVariable variable)
        {
            if (variable == null || string.IsNullOrWhiteSpace(variable.Path))
            {
                return string.Empty;
            }

            string rawName = variable.LegacyName?.Trim() ?? string.Empty;
            if (rawName.Length == 0)
            {
                return "{{ " + variable.Path + " }}";
            }

            if (variable.Kind == ReflectedCustomVariableKind.Pawn)
            {
                return "{{ pawn." + rawName + " }}";
            }

            return "{{ " + rawName + " }}";
        }

        private static ReflectedCustomVariable CloneVariable(ReflectedCustomVariable source)
        {
            if (source == null)
            {
                return null;
            }

            return new ReflectedCustomVariable
            {
                LegacyName = source.LegacyName ?? string.Empty,
                Path = source.Path ?? string.Empty,
                ModId = source.ModId ?? string.Empty,
                Description = source.Description ?? string.Empty,
                Kind = source.Kind
            };
        }

        private static string SanitizeModId(string modId)
        {
            if (string.IsNullOrWhiteSpace(modId))
            {
                return "rimchat";
            }

            var sb = new StringBuilder(modId.Length);
            for (int i = 0; i < modId.Length; i++)
            {
                char c = char.ToLowerInvariant(modId[i]);
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                }
            }

            return sb.Length == 0 ? "rimchat" : sb.ToString();
        }

        private static string TrimToBudget(string text, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string normalized = text.Trim();
            if (normalized.Length <= maxChars)
            {
                return normalized;
            }

            if (maxChars <= 3)
            {
                return normalized.Substring(0, maxChars);
            }

            return normalized.Substring(0, maxChars - 3).TrimEnd() + "...";
        }
    }
}
