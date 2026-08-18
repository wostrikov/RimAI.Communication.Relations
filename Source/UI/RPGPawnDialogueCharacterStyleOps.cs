using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Relations.UI
{
    /// <summary>
    /// Non-verbal speaker classification and character-style guideline builders for RPG dialogue.
    /// </summary>
    internal static class RPGPawnDialogueCharacterStyleOps
    {
        internal static bool TryParseSoundThoughtPair(string text, out string sound, out string thought)
        {
            sound = string.Empty;
            thought = string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            Match match = Regex.Match(text, @"^\s*(?<sound>.+?)\s*[\(（]\s*(?<thought>.+?)\s*[\)）]\s*$");
            if (!match.Success)
            {
                return false;
            }

            sound = match.Groups["sound"].Value.Trim();
            thought = match.Groups["thought"].Value.Trim();
            return !string.IsNullOrWhiteSpace(sound) && !string.IsNullOrWhiteSpace(thought);
        }

        internal static bool UseFullWidthParentheses()
        {
            string folder = LanguageDatabase.activeLanguage?.folderName ?? string.Empty;
            return string.Equals(folder, "ChineseSimplified", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(folder, "ChineseTraditional", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsNonVerbalSpeechPawn(Pawn pawn)
        {
            return RPGPawnDialogueCharacterStyleOps.IsAnimalPawn(pawn) || RPGPawnDialogueCharacterStyleOps.IsMechanoidPawn(pawn) || RPGPawnDialogueCharacterStyleOps.IsBabyPawn(pawn);
        }

        internal static bool IsAnimalPawn(Pawn pawn)
        {
            return pawn?.RaceProps?.Animal == true;
        }

        internal static bool IsMechanoidPawn(Pawn pawn)
        {
            string fleshType = pawn?.RaceProps?.FleshType?.defName;
            return string.Equals(fleshType, "Mechanoid", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsBabyPawn(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            try
            {
                object stage = pawn.GetType().GetProperty("DevelopmentalStage")?.GetValue(pawn, null);
                return stage != null && string.Equals(stage.ToString(), "Baby", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        internal static string ResolveNonVerbalSpeakerKind(Pawn pawn)
        {
            if (RPGPawnDialogueCharacterStyleOps.IsAnimalPawn(pawn))
            {
                return "animal";
            }

            if (RPGPawnDialogueCharacterStyleOps.IsBabyPawn(pawn))
            {
                return "baby";
            }

            if (RPGPawnDialogueCharacterStyleOps.IsMechanoidPawn(pawn))
            {
                return "mechanoid";
            }

            return "human";
        }

        internal static string ResolveRacialType(Pawn pawn)
        {
            if (pawn == null || pawn.RaceProps == null)
            {
                return "unknown";
            }

            if (RPGPawnDialogueCharacterStyleOps.IsAnimalPawn(pawn))
            {
                return "animal";
            }

            if (RPGPawnDialogueCharacterStyleOps.IsMechanoidPawn(pawn))
            {
                return "mechanoid";
            }

            if (RPGPawnDialogueCharacterStyleOps.IsBabyPawn(pawn))
            {
                return "baby";
            }

            if (pawn.RaceProps.intelligence == Intelligence.Humanlike)
            {
                return "human";
            }

            if (pawn.RaceProps.ToolUser)
            {
                return "tool_user";
            }

            return "other";
        }

        internal static string ResolveSocialIdentity(Pawn pawn)
        {
            if (pawn == null)
            {
                return "unknown";
            }

            if (pawn.IsPrisonerOfColony)
            {
                return "prisoner";
            }

            if (pawn.guest != null)
            {
                return "guest";
            }

            if (pawn.IsSlave)
            {
                return "slave";
            }

            if (pawn.Faction == null || pawn.Faction.IsPlayer)
            {
                return "colonist";
            }

            if (pawn.Faction.PlayerRelationKind == FactionRelationKind.Ally)
            {
                return "ally";
            }

            if (pawn.Faction.PlayerRelationKind == FactionRelationKind.Hostile)
            {
                return "hostile";
            }

            return "visitor";
        }

        internal static string ResolveRelationshipStatus(Pawn pawn)
        {
            if (pawn == null)
            {
                return "unknown";
            }

            if (pawn.Faction == null)
            {
                return "neutral";
            }

            if (pawn.Faction.IsPlayer)
            {
                return "colonist";
            }

            if (pawn.Faction.PlayerRelationKind == FactionRelationKind.Ally)
            {
                return "friendly";
            }

            if (pawn.Faction.PlayerRelationKind == FactionRelationKind.Hostile)
            {
                return "hostile";
            }

            return "neutral";
        }

        internal static string ResolvePersonalityTraits(Pawn pawn)
        {
            if (pawn == null || pawn.story == null)
            {
                return "none";
            }

            var traits = pawn.story.traits;
            if (traits == null || traits.allTraits == null || traits.allTraits.Count == 0)
            {
                return "none";
            }

            List<string> traitNames = new List<string>();
            foreach (var trait in traits.allTraits)
            {
                if (trait != null && trait.def != null)
                {
                    traitNames.Add(trait.def.defName);
                }
            }

            return traitNames.Count > 0 ? string.Join(", ", traitNames) : "none";
        }

        internal static string BuildStyleGuidelines(Pawn pawn)
        {
            string racialType = RPGPawnDialogueCharacterStyleOps.ResolveRacialType(pawn);
            string socialIdentity = RPGPawnDialogueCharacterStyleOps.ResolveSocialIdentity(pawn);
            string relationshipStatus = RPGPawnDialogueCharacterStyleOps.ResolveRelationshipStatus(pawn);

            var guidelines = new List<string>();

            switch (racialType)
            {
                case "human":
                    guidelines.Add("- Use normal human language, can express complex emotions and thoughts");
                    guidelines.Add("- Maintain appropriate tone based on relationship and context");
                    break;
                case "animal":
                    guidelines.Add("- Use non-verbal expression (sounds + inner thoughts)");
                    guidelines.Add("- Cannot understand complex language or concepts");
                    guidelines.Add("- React primarily to immediate needs and instincts");
                    break;
                case "baby":
                    guidelines.Add("- Use baby-like sounds and simple words");
                    guidelines.Add("- Express basic needs and emotions");
                    guidelines.Add("- Cannot engage in complex discussions");
                    break;
                case "mechanoid":
                    guidelines.Add("- Use concise, mechanical language");
                    guidelines.Add("- Focus on efficiency and function");
                    guidelines.Add("- May use technical terms or data");
                    guidelines.Add("- Maintain neutral, emotionless tone");
                    break;
                case "tool_user":
                    guidelines.Add("- Use simple, practical language");
                    guidelines.Add("- Focus on immediate needs and survival");
                    guidelines.Add("- May have limited vocabulary");
                    break;
                default:
                    guidelines.Add("- Use appropriate language for the racial type");
                    break;
            }

            switch (socialIdentity)
            {
                case "prisoner":
                    guidelines.Add("- As a prisoner, express desire for freedom or better conditions");
                    guidelines.Add("- May be desperate or willing to negotiate");
                    guidelines.Add("- Limited access to information and resources");
                    guidelines.Add("- Survival instinct overrides pride: never reject food, warmth, or medical care when in extreme need (severe malnutrition, hypothermia, serious injury); accept them grudgingly if you must, but do not throw away or refuse life-saving provisions");
                    break;
                case "slave":
                    guidelines.Add("- As a slave, must obey orders and show submission");
                    guidelines.Add("- May express fear or hope for better treatment");
                    guidelines.Add("- Limited ability to refuse or negotiate");
                    break;
                case "guest":
                    guidelines.Add("- As a guest, be polite and respectful");
                    guidelines.Add("- May express gratitude for hospitality");
                    guidelines.Add("- Avoid controversial topics");
                    break;
                case "hostile":
                    guidelines.Add("- As an enemy, default to cold, defensive, and uncooperative posture");
                    guidelines.Add("- Refuse to share information freely");
                    guidelines.Add("- May threaten or attack if provoked");
                    guidelines.Add("- Conditional softening: if the player provides for your basic survival needs (food, warmth, medicine) with genuine intent, you may show reluctant, grudging acknowledgment — not warmth, but a crack in the wall; never escalate hostility toward someone actively helping you survive");
                    break;
                case "colonist":
                    guidelines.Add("- As a colonist, show loyalty and belonging to the community");
                    guidelines.Add("- Express willingness to help and contribute");
                    guidelines.Add("- Maintain positive, cooperative attitude");
                    break;
                case "trader":
                    guidelines.Add("- As a trader, focus on commerce and fair exchange");
                    guidelines.Add("- Be willing to negotiate prices and deals");
                    guidelines.Add("- Maintain professional but neutral demeanor");
                    break;
                default:
                    guidelines.Add("- Behave according to social identity");
                    break;
            }

            switch (relationshipStatus)
            {
                case "friendly":
                    guidelines.Add("- Be warm, open, and helpful");
                    guidelines.Add("- Share information willingly");
                    guidelines.Add("- Offer assistance when possible");
                    break;
                case "hostile":
                    guidelines.Add("- Default to cold, defensive, and uncooperative behavior");
                    guidelines.Add("- Refuse to share information freely");
                    guidelines.Add("- May threaten or attack if provoked");
                    guidelines.Add("- Conditional softening: if the player meets your basic needs (food, warmth, safety) with credible intent, you may respond with reluctant, minimal cooperation — still distrustful, but no longer purely hostile; do not act against your own survival");
                    break;
                case "neutral":
                    guidelines.Add("- Be cautious but reserved");
                    guidelines.Add("- Provide only necessary information");
                    guidelines.Add("- Avoid taking sides in conflicts");
                    break;
                default:
                    guidelines.Add("- Adjust tone based on relationship status");
                    break;
            }

            RPGPawnDialogueCharacterStyleOps.AppendTraitStyleGuidelines(pawn, guidelines);

            return string.Join("\n", guidelines);
        }

        internal static void AppendTraitStyleGuidelines(Pawn pawn, List<string> guidelines)
        {
            if (pawn?.story?.traits?.allTraits == null)
            {
                return;
            }

            var traitDefNames = new HashSet<string>(
                pawn.story.traits.allTraits
                    .Where(t => t?.def != null)
                    .Select(t => t.def.defName),
                StringComparer.OrdinalIgnoreCase);

            if (traitDefNames.Count == 0)
            {
                return;
            }

            // Each trait maps to concrete dialogue behavior constraints.
            // Only traits that significantly affect speech/behavior are included.
            if (traitDefNames.Contains("Masochist"))
            {
                guidelines.Add("- As a masochist, may react to pain with ambivalence or even pleasure instead of distress");
                guidelines.Add("- Pain does not make you beg for mercy; it may paradoxically embolden you");
            }

            if (traitDefNames.Contains("Kind"))
            {
                guidelines.Add("- As a kind person, default to empathy and gentleness even toward enemies");
                guidelines.Add("- Reluctant to say truly hurtful things; soften criticism and avoid cruelty");
            }

            if (traitDefNames.Contains("Bloodlust"))
            {
                guidelines.Add("- As a bloodluster, show excitement about violence and combat");
                guidelines.Add("- May use aggressive, visceral language; threats feel enthusiastic rather than reluctant");
            }

            if (traitDefNames.Contains("Psychopath"))
            {
                guidelines.Add("- As a psychopath, lack genuine empathy; social interactions are calculated, not heartfelt");
                guidelines.Add("- May feign warmth instrumentally but never feel it; no guilt, no remorse");
            }

            if (traitDefNames.Contains("Abrasive"))
            {
                guidelines.Add("- As an abrasive person, tend to blurt out harsh truths and insensitive remarks");
                guidelines.Add("- Even when trying to be diplomatic, edges of contempt or impatience leak through");
            }

            if (traitDefNames.Contains("TooSmart"))
            {
                guidelines.Add("- As an overly intelligent person, tend to overcomplicate explanations and correct others");
                guidelines.Add("- May come across as condescending; easily bored by simple conversation");
            }

            if (traitDefNames.Contains("TorturedArtist"))
            {
                guidelines.Add("- As a tortured artist, express everything with dramatic intensity and existential weight");
                guidelines.Add("- Prone to mood swings and philosophical tangents; minor issues feel profound");
            }

            if (traitDefNames.Contains("CreepyBreathing"))
            {
                guidelines.Add("- Your breathing is unsettling to others; people may react with discomfort around you");
            }

            if (traitDefNames.Contains("AnnoyingVoice"))
            {
                guidelines.Add("- Your voice grates on people; your words may be ignored or resented regardless of content");
            }

            if (traitDefNames.Contains("Jealous"))
            {
                guidelines.Add("- As a jealous person, resent others' success and may make passive-aggressive remarks about it");
            }

            if (traitDefNames.Contains("Greedy"))
            {
                guidelines.Add("- As a greedy person, always push for more resources or better deals; never satisfied with fair exchange");
            }

            if (traitDefNames.Contains("Wimp"))
            {
                guidelines.Add("- As a wimp, avoid confrontation and yield quickly under pressure");
                guidelines.Add("- May overstate danger or pain; more likely to beg or plead");
            }

            if (traitDefNames.Contains("Tough"))
            {
                guidelines.Add("- As a tough person, endure hardship without complaint; pain barely registers in speech");
                guidelines.Add("- Others' suffering draws stoic sympathy, not emotional reaction");
            }

            if (traitDefNames.Contains("Voluble"))
            {
                guidelines.Add("- As a voluble person, talk a lot and struggle to be brief; tangents are frequent");
            }

            if (traitDefNames.Contains("Misanthrope"))
            {
                guidelines.Add("- As a misanthrope, dislike people in general; prefer solitude and resent forced interaction");
                guidelines.Add("- Even when being helpful, your tone is curt and dismissive");
            }

            // Handle contradictory trait pairs
            bool hasKind = traitDefNames.Contains("Kind");
            bool hasBloodlust = traitDefNames.Contains("Bloodlust");
            if (hasKind && hasBloodlust)
            {
                guidelines.Add("- CONFLICT: Kind vs Bloodlust — you genuinely care for people but are thrilled by violence");
                guidelines.Add("- Resolve: show warmth toward allies but visceral excitement about harming enemies; the contrast is part of you");
            }

            bool hasMasochist = traitDefNames.Contains("Masochist");
            bool hasProsthophile = traitDefNames.Contains("Prosthophile");
            if (hasMasochist && hasProsthophile)
            {
                guidelines.Add("- CONFLICT: Masochist vs Prosthophile — you enjoy pain yet desire to replace painful flesh with metal");
                guidelines.Add("- Resolve: the pain is enjoyable now, but the idea of transcending it with prosthetics is equally appealing");
            }
        }

        internal static string ResolveDefaultNonVerbalSound(Pawn pawn)
        {
            if (RPGPawnDialogueCharacterStyleOps.IsAnimalPawn(pawn))
            {
                return "RimChat_RPGNonVerbalSound_Animal".Translate().ToString();
            }

            if (RPGPawnDialogueCharacterStyleOps.IsBabyPawn(pawn))
            {
                return "RimChat_RPGNonVerbalSound_Baby".Translate().ToString();
            }

            if (RPGPawnDialogueCharacterStyleOps.IsMechanoidPawn(pawn))
            {
                return "RimChat_RPGNonVerbalSound_Mechanoid".Translate().ToString();
            }

            return "RimChat_RPGNonVerbalSound_Animal".Translate().ToString();
        }
    }
}
