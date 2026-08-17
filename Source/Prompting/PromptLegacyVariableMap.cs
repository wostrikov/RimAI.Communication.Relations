using System;
using System.Collections.Generic;

namespace Ustas.RimAI.Communication.Relations.Prompting
{
    /// <summary>
    /// Dependencies: none.
    /// Responsibility: map current RimAI un-namespaced placeholders to canonical namespaced paths.
    /// </summary>
    internal static class PromptLegacyVariableMap
    {
        public static readonly IReadOnlyDictionary<string, string> CurrentRimAiAliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["scene_tags"] = "world.scene_tags",
                ["environment_params"] = "world.environment_params",
                ["recent_world_events"] = "world.recent_world_events",
                ["colony_status"] = "world.colony_status",
                ["colony_factions"] = "world.colony_factions",
                ["current_faction_profile"] = "world.current_faction_profile",
                ["rpg_target_profile"] = "pawn.target.profile",
                ["rpg_initiator_profile"] = "pawn.initiator.profile",
                ["player_pawn_profile"] = "pawn.player.profile",
                ["player_royalty_summary"] = "pawn.player.royalty_summary",
                ["faction_settlement_summary"] = "world.faction_settlement_summary",
                ["channel"] = "ctx.channel",
                ["mode"] = "ctx.mode",
                ["target_language"] = "system.target_language",
                ["game_language"] = "system.game_language",
                ["faction_name"] = "world.faction.name",
                ["initiator_name"] = "pawn.initiator.name",
                ["target_name"] = "pawn.target.name",
                ["primary_objective"] = "dialogue.primary_objective",
                ["optional_followup"] = "dialogue.optional_followup",
                ["latest_unresolved_intent"] = "dialogue.latest_unresolved_intent",
                ["topic_shift_rule"] = "dialogue.topic_shift_rule",
                ["api_limits_body"] = "dialogue.api_limits_body",
                ["quest_guidance_body"] = "dialogue.quest_guidance_body",
                ["response_contract_body"] = "dialogue.response_contract_body",
                ["kinship"] = "pawn.relation.kinship",
                ["romance_state"] = "pawn.relation.romance_state",
                ["guidance"] = "dialogue.guidance",
                ["origin_type"] = "world.social.origin_type",
                ["category"] = "world.social.category",
                ["source_faction"] = "world.social.source_faction",
                ["target_faction"] = "world.social.target_faction",
                ["summary"] = "dialogue.summary",
                ["intent_hint"] = "dialogue.intent_hint",
                ["source_label"] = "world.social.source_label",
                ["credibility_label"] = "world.social.credibility_label",
                ["credibility_value"] = "world.social.credibility_value",
                ["fact_lines"] = "world.social.fact_lines",
                ["speaker_kind"] = "pawn.speaker.kind",
                ["default_sound"] = "pawn.speaker.default_sound",
                ["animal_sound"] = "pawn.speaker.animal_sound",
                ["baby_sound"] = "pawn.speaker.baby_sound",
                ["mechanoid_sound"] = "pawn.speaker.mechanoid_sound",
                ["open_paren"] = "system.punctuation.open_paren",
                ["close_paren"] = "system.punctuation.close_paren",
                ["template_line"] = "dialogue.template_line",
                ["example_line"] = "dialogue.example_line",
                ["subject_pronoun"] = "pawn.pronouns.subject",
                ["object_pronoun"] = "pawn.pronouns.object",
                ["possessive_pronoun"] = "pawn.pronouns.possessive",
                ["profile"] = "pawn.profile",
                ["subject_pronoun_lower"] = "pawn.pronouns.subject_lower",
                ["be_verb"] = "pawn.pronouns.be_verb",
                ["seek_verb"] = "pawn.pronouns.seek_verb",
                ["examples"] = "dialogue.examples",
                ["action_names"] = "dialogue.action_names"
            };

        public static readonly string[] DeletedDonorAliases =
        {
            "context",
            "prompt",
            "chat.history",
            "chat.history_simplified",
            "json.format",
            "system.rimtalk.json_format"
        };

        public static bool TryMap(string token, out string namespacedPath)
        {
            namespacedPath = string.Empty;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            if (!CurrentRimAiAliases.TryGetValue(token.Trim(), out string mapped) ||
                string.IsNullOrWhiteSpace(mapped))
            {
                return false;
            }

            namespacedPath = mapped;
            return true;
        }
    }
}
