using System;

namespace Ustas.RimAI.Communication.Relations.Prompting
{
    /// <summary>
    /// Dependencies: none.
    /// Responsibility: own the canonical namespaced prompt variable paths used by current RimAI templates.
    /// </summary>
    internal static class PromptCanonicalVariablePaths
    {
        public const string CoreSourceId = "rimai.relations.core";
        public const string CoreSourceLabel = "RimAI Relations";

        public static readonly string[] All =
        {
            "ctx.channel", "ctx.mode", "system.target_language", "system.game_language",
            "world.time.hour", "world.time.day", "world.time.quadrum", "world.time.year", "world.time.season", "world.time.date",
            "world.weather", "world.temperature",
            "world.faction.name", "world.faction.description", "world.scene_tags", "world.environment_params", "world.recent_world_events",
            "world.colony_status", "world.colony_factions", "world.current_faction_profile", "world.faction_settlement_summary",
            "world.social.origin_type", "world.social.category", "world.social.source_faction", "world.social.target_faction",
            "world.social.source_label", "world.social.credibility_label", "world.social.credibility_value", "world.social.fact_lines",
            "pawn.initiator", "pawn.target", "pawn.initiator.name", "pawn.target.name", "pawn.target.profile",
            "pawn.recipient", "pawn.recipient.name",
            "pawn.initiator.profile", "pawn.player.profile", "pawn.player.royalty_summary", "pawn.relation.kinship",
            "pawn.relation.romance_state", "pawn.relation.social_summary", "pawn.speaker.kind", "pawn.speaker.default_sound", "pawn.speaker.animal_sound",
            "pawn.speaker.baby_sound", "pawn.speaker.mechanoid_sound", "pawn.pronouns.subject", "pawn.pronouns.object",
            "pawn.pronouns.possessive", "pawn.pronouns.subject_lower", "pawn.pronouns.be_verb", "pawn.pronouns.seek_verb",
            "pawn.profile", "pawn.personality", "dialogue.summary", "dialogue.guidance", "dialogue.intent_hint",
            "dialogue.template_line", "dialogue.example_line", "dialogue.examples", "dialogue.action_names",
            "dialogue.primary_objective", "dialogue.optional_followup", "dialogue.latest_unresolved_intent",
            "dialogue.topic_shift_rule", "dialogue.api_limits_body", "dialogue.quest_guidance_body",
            "dialogue.response_contract_body", "dialogue.strategy_player_negotiator_context_body",
            "dialogue.strategy_fact_pack_body", "dialogue.strategy_scenario_dossier_body", "dialogue.mandatory_race_profile_body",
            "system.punctuation.open_paren", "system.punctuation.close_paren"
        };

        public static bool Contains(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string normalized = path.Trim();
            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i], normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
