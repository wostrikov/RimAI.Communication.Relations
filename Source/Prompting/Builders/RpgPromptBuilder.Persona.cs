using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Relation;
using Ustas.RimAI.Communication.Relations.Guards;
using Ustas.RimAI.Communication.Relations.WorldState;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.Prompting.Builders;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Prompting.Transfer;
using Ustas.RimAI.Communication.Relations.Serialization;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting.Diplomacy;

namespace Ustas.RimAI.Communication.Relations.Prompting.Builders
{
    internal sealed partial class RpgPromptBuilder
    {        public string BuildPawnPersonaBootstrapProfile(Pawn pawn)
        {
            if (pawn == null)
            {
                return "No pawn context.";
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== PERSONA PROFILE (PERSONALITY ONLY) ===");
            sb.AppendLine($"Name: {pawn.Name?.ToStringFull ?? pawn.LabelShort}");
            sb.AppendLine($"Kind: {pawn.KindLabel}");
            sb.AppendLine($"Gender: {pawn.gender}");
            sb.AppendLine($"Age: {pawn.ageTracker?.AgeBiologicalYears}");
            AppendPersonaBackstory(sb, pawn);
            AppendPersonaTraits(sb, pawn);
            AppendPersonaCoreSkills(sb, pawn);
            AppendPersonaFactionContext(sb, pawn);
            sb.AppendLine("Excluded Signals: Health, needs, mood, wounds, equipment, genes, temporary events.");

            return sb.ToString().Trim();
        }

        internal void AppendPersonaBackstory(StringBuilder sb, Pawn pawn)
        {
            if (sb == null || pawn?.story == null)
            {
                return;
            }

            string childhood = pawn.story.Childhood?.title;
            string adulthood = pawn.story.Adulthood?.title;
            if (!string.IsNullOrWhiteSpace(childhood))
            {
                sb.AppendLine($"Backstory (Child): {childhood}");
            }

            if (!string.IsNullOrWhiteSpace(adulthood))
            {
                sb.AppendLine($"Backstory (Adult): {adulthood}");
            }
        }

        internal void AppendPersonaTraits(StringBuilder sb, Pawn pawn)
        {
            if (sb == null || pawn?.story?.traits?.allTraits == null)
            {
                return;
            }

            List<string> traits = pawn.story.traits.allTraits
                .Select(t => t?.Label)
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Take(6)
                .ToList();
            if (traits.Count > 0)
            {
                sb.AppendLine($"Traits: {string.Join(", ", traits)}");
            }
        }

        internal void AppendPersonaCoreSkills(StringBuilder sb, Pawn pawn)
        {
            if (sb == null || pawn?.skills?.skills == null)
            {
                return;
            }

            List<string> topSkills = pawn.skills.skills
                .Where(skill => skill?.def != null)
                .OrderByDescending(skill => skill.Level)
                .Take(4)
                .Select(FormatPersonaSkill)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();
            if (topSkills.Count > 0)
            {
                sb.AppendLine($"Core Skills: {string.Join(", ", topSkills)}");
            }
        }

        internal string FormatPersonaSkill(SkillRecord skill)
        {
            if (skill?.def == null)
            {
                return string.Empty;
            }

            string passion = string.Empty;
            if (skill.passion == Passion.Major)
            {
                passion = " (major passion)";
            }
            else if (skill.passion == Passion.Minor)
            {
                passion = " (minor passion)";
            }

            return $"{skill.def.skillLabel}:{skill.Level}{passion}";
        }

        internal void AppendPersonaFactionContext(StringBuilder sb, Pawn pawn)
        {
            if (sb == null || pawn?.Faction == null)
            {
                return;
            }

            Faction faction = pawn.Faction;
            if (faction.IsPlayer)
            {
                sb.AppendLine("Faction: Player Colony");
            }
            else
            {
                sb.AppendLine($"Faction: {faction.Name} ({faction.def?.label})");
                sb.AppendLine($"Faction Relation with Player: {faction.PlayerGoodwill} ({promptService.DiplomacyBuilder.GetRelationLabel(faction.PlayerGoodwill)})");
            }

            if (faction.leader == pawn)
            {
                sb.AppendLine("Faction Role: Leader");
            }

            if (faction.ideos?.PrimaryIdeo != null)
            {
                sb.AppendLine($"Primary Ideology: {faction.ideos.PrimaryIdeo.name}");
            }
        }

        internal void AppendRPGPawnInfo(
            StringBuilder sb,
            Pawn pawn,
            bool isTarget,
            RpgSceneParamSwitchesConfig switches,
            bool includePlayerSharedColonyContext = true,
            bool includeStaticProfileDetails = true)
        {
            if (pawn == null)
            {
                return;
            }

            var effectiveSwitches = switches ?? new RpgSceneParamSwitchesConfig();
            sb.AppendLine(isTarget ? "=== CHARACTER STATUS (YOU) ===" : "=== CHARACTER STATUS (INTERLOCUTOR) ===");
            sb.AppendLine($"Name: {pawn.Name?.ToStringFull ?? pawn.LabelShort}");
            sb.AppendLine($"Kind: {pawn.KindLabel}");
            sb.AppendLine($"Gender: {pawn.gender}");
            sb.AppendLine($"Age: {pawn.ageTracker?.AgeBiologicalYears}");
            
            if (includeStaticProfileDetails && pawn.story != null)
            {
                sb.AppendLine($"Backstory (Child): {pawn.story.Childhood?.title}");
                sb.AppendLine($"Backstory (Adult): {pawn.story.Adulthood?.title}");
                if (pawn.story.traits?.allTraits != null)
                {
                    sb.AppendLine($"Traits: {string.Join(", ", pawn.story.traits.allTraits.Select(t => t.Label))}");
                }
            }

            if (pawn.needs?.mood != null)
            {
                sb.AppendLine($"Current Mood: {pawn.needs.mood.CurLevelPercentage:P0}");
            }

            if (pawn.health != null)
            {
                sb.AppendLine($"Health Summary: {pawn.health.summaryHealth.SummaryHealthPercent:P0}");
            }

            if (effectiveSwitches.IncludeNeeds)
            {
                AppendRpgNeeds(sb, pawn);
            }

            if (effectiveSwitches.IncludeHediffs)
            {
                AppendRpgHediffs(sb, pawn);
            }

            if (includeStaticProfileDetails && effectiveSwitches.IncludeSkills)
            {
                AppendRpgSkills(sb, pawn);
            }

            if (includeStaticProfileDetails && effectiveSwitches.IncludeEquipment)
            {
                AppendRpgEquipment(sb, pawn);
            }

            if (includeStaticProfileDetails && effectiveSwitches.IncludeGenes)
            {
                AppendRpgGenes(sb, pawn);
            }

            if (includeStaticProfileDetails && effectiveSwitches.IncludeRecentEvents)
            {
                AppendRpgRecentMemories(sb, pawn);
            }

            if (includePlayerSharedColonyContext)
            {
                AppendPlayerColonyContextIfEnabled(sb, pawn, effectiveSwitches);
            }
            
            sb.AppendLine();
        }

        internal void AppendRpgNeeds(StringBuilder sb, Pawn pawn)
        {
            if (pawn?.needs?.AllNeeds == null)
            {
                return;
            }

            List<string> values = pawn.needs.AllNeeds
                .Where(need => need != null && need.def != null)
                .Take(6)
                .Select(need => $"{need.def.label}:{need.CurLevelPercentage:P0}")
                .ToList();

            if (values.Count > 0)
            {
                sb.AppendLine($"Needs: {string.Join(", ", values)}");
            }
        }

        internal void AppendRpgHediffs(StringBuilder sb, Pawn pawn)
        {
            if (pawn?.health?.hediffSet?.hediffs == null)
            {
                return;
            }

            List<string> values = pawn.health.hediffSet.hediffs
                .Where(h => h != null && h.Visible)
                .Take(6)
                .Select(h => h.LabelCap)
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .ToList();

            if (values.Count > 0)
            {
                sb.AppendLine($"Visible Conditions: {string.Join(", ", values)}");
            }
        }

        internal void AppendRpgSkills(StringBuilder sb, Pawn pawn)
        {
            if (pawn?.skills?.skills == null)
            {
                return;
            }

            List<string> values = pawn.skills.skills
                .Where(skill => skill != null && skill.def != null)
                .OrderByDescending(skill => skill.Level)
                .Take(6)
                .Select(skill => $"{skill.def.skillLabel}:{skill.Level}")
                .ToList();

            if (values.Count > 0)
            {
                sb.AppendLine($"Top Skills: {string.Join(", ", values)}");
            }
        }

        internal void AppendRpgEquipment(StringBuilder sb, Pawn pawn)
        {
            List<string> parts = new List<string>();

            string primary = pawn?.equipment?.Primary?.LabelCap;
            if (!string.IsNullOrWhiteSpace(primary))
            {
                parts.Add($"Primary={primary}");
            }

            if (pawn?.apparel?.WornApparel != null)
            {
                string worn = string.Join(", ", pawn.apparel.WornApparel
                    .Take(4)
                    .Select(apparel => apparel?.LabelCap)
                    .Where(label => !string.IsNullOrWhiteSpace(label)));
                if (!string.IsNullOrWhiteSpace(worn))
                {
                    parts.Add($"Worn={worn}");
                }
            }

            if (parts.Count > 0)
            {
                sb.AppendLine($"Equipment: {string.Join(" | ", parts)}");
            }
        }

        internal void AppendRpgGenes(StringBuilder sb, Pawn pawn)
        {
            object genesObj = pawn?.genes;
            if (genesObj == null)
            {
                return;
            }

            var genesProperty = genesObj.GetType().GetProperty("GenesListForReading");
            if (genesProperty == null)
            {
                return;
            }

            var enumerable = genesProperty.GetValue(genesObj) as System.Collections.IEnumerable;
            if (enumerable == null)
            {
                return;
            }

            List<string> values = new List<string>();
            foreach (object gene in enumerable)
            {
                if (gene == null)
                {
                    continue;
                }

                string label = gene.GetType().GetProperty("LabelCap")?.GetValue(gene)?.ToString();
                if (string.IsNullOrWhiteSpace(label))
                {
                    object defObj = gene.GetType().GetProperty("def")?.GetValue(gene);
                    label = defObj?.GetType().GetProperty("label")?.GetValue(defObj)?.ToString();
                }

                if (!string.IsNullOrWhiteSpace(label))
                {
                    values.Add(label);
                }

                if (values.Count >= 8)
                {
                    break;
                }
            }

            if (values.Count > 0)
            {
                sb.AppendLine($"Genes: {string.Join(", ", values)}");
            }
        }

        internal void AppendRpgRecentMemories(StringBuilder sb, Pawn pawn)
        {
            var memories = pawn?.needs?.mood?.thoughts?.memories?.Memories;
            if (memories == null)
            {
                return;
            }

            List<string> values = memories
                .Where(memory => memory != null)
                .OrderBy(memory => memory.age)
                .Select(memory => memory.LabelCap)
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Distinct()
                .Take(5)
                .ToList();

            if (values.Count > 0)
            {
                sb.AppendLine($"Recent Memories: {string.Join(", ", values)}");
            }
        }

        internal void AppendRPGFactionContext(StringBuilder sb, Pawn pawn)
        {
            if (pawn.Faction == null) return;
            bool isTarget = pawn.IsColonist || pawn.IsPrisoner || pawn.IsSlave; // Roughly
            sb.AppendLine(isTarget ? "=== YOUR FACTION CONTEXT ===" : "=== INTERLOCUTOR FACTION CONTEXT ===");
            if (pawn.Faction.IsPlayer)
            {
                sb.AppendLine("Faction: Player Colony (Your own people)");
            }
            else
            {
                sb.AppendLine($"Faction: {pawn.Faction.Name} ({pawn.Faction.def?.label})");
                sb.AppendLine($"Faction Relations with Player: {pawn.Faction.PlayerGoodwill} ({promptService.DiplomacyBuilder.GetRelationLabel(pawn.Faction.PlayerGoodwill)})");
            }
            
            if (pawn.Faction.ideos?.PrimaryIdeo != null)
            {
                sb.AppendLine($"Primary Ideology: {pawn.Faction.ideos.PrimaryIdeo.name}");
            }
            sb.AppendLine();
        }

    }
}
