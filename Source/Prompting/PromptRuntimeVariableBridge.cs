using System;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Prompting
{
    /// <summary>
    /// Dependencies: loaded RimWorld mods and canonical variable metadata.
    /// Responsibility: resolve localization keys and detect optional current context contributors.
    /// </summary>
    internal static class PromptRuntimeVariableBridge
    {
        public static void RefreshRimTalkCustomVariableSnapshot(bool force = false)
        {
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

        private static bool ContainsToken(string value, string token)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
