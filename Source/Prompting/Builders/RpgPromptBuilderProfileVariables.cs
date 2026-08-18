using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ustas.RimAI.Communication.Relations.Config;
using RimWorld;
using Verse;
using Ustas.RimAI.Communication.Relations.Context;

using Ustas.RimAI.Communication.Relations.Persistence;

namespace Ustas.RimAI.Communication.Relations.Prompting.Builders
{
    /// <summary>
    /// Dependencies: DialogueScenarioContext, RpgSceneParamSwitchesConfig, pawn relation/job runtime APIs.
    /// Responsibility: build expanded RPG pawn profile and bilateral social summary prompt variables.
    /// </summary>
    internal sealed class RpgPromptBuilderProfileVariables : RpgPromptBuilderCollaborator
    {
        internal RpgPromptBuilderProfileVariables(RpgPromptBuilder owner) : base(owner)
        {
        }


        internal string BuildPawnProfileVariableText(Pawn pawn, DialogueScenarioContext context, EnvironmentPromptConfig envConfig)
        {
            if (pawn == null)
            {
                return "No pawn context.";
            }

            List<string> lines = Owner.BuildBasePawnProfileLines(pawn);
            if (context?.IsRpg == true)
            {
                // Determine the other pawn in the dialogue for FOV filtering
                Pawn otherPawn = null;
                if (context.Initiator != null && context.Target != null)
                {
                    otherPawn = context.Initiator == pawn ? context.Target : context.Initiator;
                }

                Owner.AppendRpgProfileExtensions(
                    lines,
                    pawn,
                    envConfig?.RpgSceneParamSwitches ?? new RpgSceneParamSwitchesConfig(),
                    otherPawn);
            }

            return string.Join("\n", lines);
        }

        internal List<string> BuildBasePawnProfileLines(Pawn pawn)
        {
            float mood = pawn.needs?.mood?.CurLevelPercentage ?? -1f;
            float health = pawn.health?.summaryHealth?.SummaryHealthPercent ?? -1f;
            string moodText = mood >= 0f ? $"{mood:P0}" : "N/A";
            string healthText = health >= 0f ? $"{health:P0}" : "N/A";
            return new List<string>
            {
                $"Name: {pawn.LabelShortCap}",
                $"Kind: {pawn.KindLabel}",
                $"Faction: {pawn.Faction?.Name ?? "None"}",
                $"Mood: {moodText}",
                $"Health: {healthText}"
            };
        }

        internal void AppendRpgProfileExtensions(
            List<string> lines,
            Pawn pawn,
            RpgSceneParamSwitchesConfig switches)
        {
            Owner.AppendRpgProfileExtensions(lines, pawn, switches, null);
        }

        internal void AppendRpgProfileExtensions(
            List<string> lines,
            Pawn pawn,
            RpgSceneParamSwitchesConfig switches,
            Pawn otherPawn)
        {
            if (switches.IncludeRecentJobState)
            {
                string jobLine = Owner.BuildRecentJobStateLine(pawn);
                if (!string.IsNullOrWhiteSpace(jobLine))
                {
                    lines.Add(jobLine);
                }
            }

            if (switches.IncludeNeeds)
            {
                Owner.AddProfileLineFromBuilder(lines, pawn, Owner.AppendRpgNeeds);
            }

            if (switches.IncludeHediffs)
            {
                Owner.AddProfileLineFromBuilder(lines, pawn, Owner.AppendRpgHediffs);
            }

            if (switches.IncludeRecentEvents)
            {
                Owner.AddProfileLineFromBuilder(lines, pawn, Owner.AppendRpgRecentMemories);
            }

            if (switches.IncludeGenes)
            {
                Owner.AddProfileLineFromBuilder(lines, pawn, Owner.AppendRpgGenes);
            }

            if (switches.IncludeAttributeLevels)
            {
                Owner.AddProfileLineFromBuilder(lines, pawn, Owner.AppendPlayerAttributeLevels);
            }

            Owner.AppendRpgColonyProfileExtensions(lines, pawn, switches, otherPawn);
        }

        internal void AppendRpgColonyProfileExtensions(
            List<string> lines,
            Pawn pawn,
            RpgSceneParamSwitchesConfig switches,
            Pawn otherPawn)
        {
            if (pawn?.Faction != Faction.OfPlayer || switches == null)
            {
                return;
            }

            // FOV gate: colony inventory/alerts are private to the player faction.
            // If the other pawn in the dialogue is not privy to colony info,
            // suppress colony-wide data to prevent omniscient information leakage.
            if (!Owner.IsPawnPrivyToColonyInfo(pawn, otherPawn))
            {
                return;
            }

            if (switches.IncludeColonyInventorySummary)
            {
                Owner.AddProfileLineFromBuilder(lines, sb =>
                {
                    List<Map> homeMaps = Owner.GetPlayerHomeMaps();
                    if (homeMaps.Count > 0)
                    {
                        Owner.AppendPlayerColonyInventorySummary(sb, homeMaps);
                    }
                });
            }

            if (switches.IncludeHomeAlerts)
            {
                Owner.AddProfileLineFromBuilder(lines, Owner.AppendPlayerHomeAlerts);
            }
        }

        /// <summary>
        /// Determine whether colony-private information should be visible
        /// in a dialogue involving both pawn and otherPawn.
        /// Colony inventory and alerts are only shared when both participants
        /// are members of the player faction (or the other pawn is absent).
        /// A prisoner, hostile, or foreign faction member should not see
        /// colony stock levels or active alerts.
        /// </summary>
        internal bool IsPawnPrivyToColonyInfo(Pawn pawn, Pawn otherPawn)
        {
            // No other participant: no restriction
            if (otherPawn == null)
            {
                return true;
            }

            // Other pawn is also a player faction member: share colony info
            if (otherPawn.Faction == Faction.OfPlayer && !otherPawn.IsPrisoner)
            {
                return true;
            }

            // Other pawn is a prisoner, hostile, or foreign faction: restrict
            return false;
        }

        internal string BuildPairSocialSummary(Pawn initiator, Pawn target, string kinshipValue, string romanceState)
        {
            if (initiator == null || target == null)
            {
                return string.Empty;
            }

            string initiatorName = initiator.LabelShortCap ?? "Initiator";
            string targetName = target.LabelShortCap ?? "Target";
            int initiatorOpinion = initiator.relations?.OpinionOf(target) ?? 0;
            int targetOpinion = target.relations?.OpinionOf(initiator) ?? 0;
            string directRelations = Owner.BuildPairDirectRelationsSummary(initiator, target);
            string initiatorGoodwill = Owner.BuildFactionGoodwillSummary(initiator.Faction);
            string targetGoodwill = Owner.BuildFactionGoodwillSummary(target.Faction);
            return
                $"Opinions: {initiatorName}->{targetName}={initiatorOpinion}, {targetName}->{initiatorName}={targetOpinion}; " +
                $"DirectRelations: {directRelations}; Kinship={kinshipValue}; Romance={romanceState}; " +
                $"FactionGoodwillToPlayer: {initiatorName}={initiatorGoodwill}, {targetName}={targetGoodwill}.";
        }

        internal string BuildPairDirectRelationsSummary(Pawn first, Pawn second)
        {
            var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Owner.AddDirectRelationLabels(labels, first, second);
            Owner.AddDirectRelationLabels(labels, second, first);
            return labels.Count == 0
                ? "none"
                : string.Join(", ", labels.OrderBy(item => item));
        }

        internal void AddDirectRelationLabels(HashSet<string> labels, Pawn fromPawn, Pawn toPawn)
        {
            if (labels == null || fromPawn?.relations?.DirectRelations == null || toPawn == null)
            {
                return;
            }

            for (int i = 0; i < fromPawn.relations.DirectRelations.Count; i++)
            {
                DirectPawnRelation relation = fromPawn.relations.DirectRelations[i];
                if (relation?.otherPawn != toPawn || relation.def == null)
                {
                    continue;
                }

                bool isFemale = toPawn.gender == Gender.Female;
                bool hasLabelFemale = !string.IsNullOrEmpty(relation.def.labelFemale);
                string label = isFemale && hasLabelFemale
                    ? relation.def.labelFemale
                    : relation.def.label ?? relation.def.defName;
                if (!string.IsNullOrWhiteSpace(label))
                {
                    labels.Add(label.Trim());
                }
            }
        }

        internal string BuildFactionGoodwillSummary(Faction faction)
        {
            if (faction == null)
            {
                return "N/A";
            }

            if (faction == Faction.OfPlayer || faction.IsPlayer)
            {
                return "player";
            }

            return faction.PlayerGoodwill.ToString();
        }

        internal string BuildRecentJobStateLine(Pawn pawn)
        {
            if (pawn?.jobs == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            string current = Owner.BuildJobSummary(pawn.CurJob);
            if (!string.IsNullOrWhiteSpace(current))
            {
                parts.Add($"Current={current}");
            }

            string duty = pawn.mindState?.duty?.def?.defName;
            if (!string.IsNullOrWhiteSpace(duty))
            {
                parts.Add($"Duty={duty}");
            }

            List<string> queued = Owner.GetQueuedJobSummaries(pawn);
            if (queued.Count > 0)
            {
                parts.Add($"Queued={string.Join(" -> ", queued)}");
            }

            return parts.Count == 0
                ? string.Empty
                : $"Recent Job State: {string.Join(" | ", parts)}";
        }

        internal void AddProfileLineFromBuilder(
            List<string> lines,
            Pawn pawn,
            Action<StringBuilder, Pawn> appendBuilder)
        {
            if (lines == null || pawn == null || appendBuilder == null)
            {
                return;
            }

            var sb = new StringBuilder();
            appendBuilder(sb, pawn);
            string text = sb.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                lines.Add(text);
            }
        }

        internal void AddProfileLineFromBuilder(
            List<string> lines,
            Action<StringBuilder> appendBuilder)
        {
            if (lines == null || appendBuilder == null)
            {
                return;
            }

            var sb = new StringBuilder();
            appendBuilder(sb);
            string text = sb.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                lines.Add(text);
            }
        }
        }

}
