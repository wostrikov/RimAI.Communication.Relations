using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Persistence;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Prompting
{
    using PromptTemplateReferenceCandidate = UserDefinedPromptVariableService.PromptTemplateReferenceCandidate;
    internal static class UserDefinedPromptVariableSlice2
    {
internal static void NormalizePawnRules(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings)
        {
            var normalizedRules = new List<PawnPromptVariableRuleConfig>();
            int order = 0;
            foreach (PawnPromptVariableRuleConfig item in settings.UserDefinedPromptVariablePawnRules)
            {
                if (item == null)
                {
                    continue;
                }

                item.Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id.Trim();
                item.VariableKey = UserDefinedPromptVariableService.NormalizeKey(item.VariableKey);
                item.NameExact = UserDefinedPromptVariableRuleMatcher.NormalizePawnName(item.NameExact);
                item.FactionDefName = item.FactionDefName?.Trim() ?? string.Empty;
                item.RaceDefName = item.RaceDefName?.Trim() ?? string.Empty;
                item.Gender = item.Gender?.Trim() ?? string.Empty;
                item.AgeStage = item.AgeStage?.Trim() ?? string.Empty;
                item.TraitsAny = UserDefinedPromptVariableRuleMatcher.NormalizeValues(item.TraitsAny);
                item.TraitsAll = UserDefinedPromptVariableRuleMatcher.NormalizeValues(item.TraitsAll);
                item.XenotypeDefName = item.XenotypeDefName?.Trim() ?? string.Empty;
                item.PlayerControlled = UserDefinedPromptVariableService.NormalizeBoolToken(item.PlayerControlled);
                item.TemplateText = item.TemplateText ?? string.Empty;
                item.Order = item.Order >= 0 ? item.Order : order;
                if (string.IsNullOrWhiteSpace(item.VariableKey) ||
                    UserDefinedPromptVariableService.FindVariableByKey(item.VariableKey, settings) == null)
                {
                    continue;
                }

                normalizedRules.Add(item);
                order++;
            }

            settings.UserDefinedPromptVariablePawnRules = normalizedRules
                .OrderBy(item => item.Order)
                .ToList();
        }

internal static void AddDependencies(Dictionary<string, HashSet<string>> graph, string key, string templateText)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (!graph.TryGetValue(key, out HashSet<string> deps))
            {
                deps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                graph[key] = deps;
            }

            TemplateVariableValidationResult validation = PromptPersistenceService.Instance.ValidateTemplateVariables(templateText ?? string.Empty);
            foreach (string used in validation.UsedVariables)
            {
                if (!UserDefinedPromptVariableService.IsUserDefinedPath(used))
                {
                    continue;
                }

                string dependencyKey = UserDefinedPromptVariableService.ExtractKeyFromPath(used);
                if (!string.IsNullOrWhiteSpace(dependencyKey))
                {
                    deps.Add(dependencyKey);
                }
            }
        }

internal static bool TryFindCycle(
            string current,
            Dictionary<string, HashSet<string>> graph,
            HashSet<string> visiting,
            HashSet<string> visited,
            List<string> path,
            out List<string> cycle)
        {
            cycle = null;
            if (visiting.Contains(current))
            {
                int start = path.FindIndex(item => string.Equals(item, current, StringComparison.OrdinalIgnoreCase));
                if (start >= 0)
                {
                    cycle = path.Skip(start).Concat(new[] { current }).ToList();
                    return true;
                }

                cycle = new List<string> { current, current };
                return true;
            }

            if (!visited.Add(current))
            {
                return false;
            }

            visiting.Add(current);
            path.Add(current);
            if (graph.TryGetValue(current, out HashSet<string> deps))
            {
                foreach (string dependency in deps)
                {
                    if (UserDefinedPromptVariableService.TryFindCycle(dependency, graph, visiting, visited, path, out cycle))
                    {
                        return true;
                    }
                }
            }

            visiting.Remove(current);
            path.RemoveAt(path.Count - 1);
            return false;
        }

internal static IEnumerable<PromptTemplateReferenceCandidate> EnumerateReferenceCandidates(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings)
        {
            foreach (RimTalkPromptChannel channel in Enum.GetValues(typeof(RimTalkPromptChannel)).Cast<RimTalkPromptChannel>())
            {
                RimTalkChannelCompatConfig compat = settings.GetRimTalkChannelConfigClone(channel);
                yield return new PromptTemplateReferenceCandidate(
                    $"compat:{channel}",
                    $"Compat Template / {channel}",
                    compat?.CompatTemplate ?? string.Empty);
            }

            yield return new PromptTemplateReferenceCandidate(
                "persona_copy",
                "RimTalk Persona Copy",
                settings.GetRimTalkPersonaCopyTemplateOrDefault());

            RimTalkPromptEntryDefaultsConfig catalog = settings.GetPromptSectionCatalogClone();
            foreach (RimTalkPromptChannelDefaultsConfig channelConfig in catalog?.Channels ?? Enumerable.Empty<RimTalkPromptChannelDefaultsConfig>())
            {
                foreach (RimTalkPromptSectionDefaultConfig section in channelConfig?.Sections ?? Enumerable.Empty<RimTalkPromptSectionDefaultConfig>())
                {
                    yield return new PromptTemplateReferenceCandidate(
                        $"section:{channelConfig.PromptChannel}:{section.SectionId}",
                        $"Prompt Section / {channelConfig.PromptChannel} / {section.SectionId}",
                        section.Content ?? string.Empty);
                }
            }

            foreach (UserDefinedPromptVariableConfig variable in UserDefinedPromptVariableService.GetVariables(settings))
            {
                string key = UserDefinedPromptVariableService.NormalizeKey(variable?.Key);
                yield return new PromptTemplateReferenceCandidate(
                    $"custom:{key}:default",
                    $"Custom Variable / {key} / default",
                    variable?.DefaultTemplateText ?? string.Empty);

                foreach (FactionPromptVariableRuleConfig rule in UserDefinedPromptVariableService.GetFactionRulesForKey(key, settings))
                {
                    yield return new PromptTemplateReferenceCandidate(
                        $"custom:{key}:faction:{rule.Order}",
                        $"Custom Variable / {key} / faction:{rule.FactionDefName}",
                        rule.TemplateText ?? string.Empty);
                }

                foreach (PawnPromptVariableRuleConfig rule in UserDefinedPromptVariableService.GetPawnRulesForKey(key, settings))
                {
                    yield return new PromptTemplateReferenceCandidate(
                        $"custom:{key}:pawn:{rule.Order}",
                        $"Custom Variable / {key} / pawn",
                        rule.TemplateText ?? string.Empty);
                }
            }
        }

internal static string NormalizeBoolToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string normalized = value.Trim().ToLowerInvariant();
            return normalized == "true" || normalized == "false"
                ? normalized
                : string.Empty;
        }

internal static string BuildSuggestedDescription(string key)
        {
            switch (UserDefinedPromptVariableService.NormalizeKey(key))
            {
                case "pawn_personality_override":
                    return "RimChat_CustomVariableSuggestedDescription_PawnPersonalityOverride".Translate().ToString();
                case "pawn_personality_append":
                    return "RimChat_CustomVariableSuggestedDescription_PawnPersonalityAppend".Translate().ToString();
                case "faction_tone":
                    return "RimChat_CustomVariableSuggestedDescription_FactionTone".Translate().ToString();
                case "faction_attitude_text":
                    return "RimChat_CustomVariableSuggestedDescription_FactionAttitude".Translate().ToString();
                case "pawn_speaking_style":
                    return "RimChat_CustomVariableSuggestedDescription_PawnSpeakingStyle".Translate().ToString();
                case "relationship_flavor":
                    return "RimChat_CustomVariableSuggestedDescription_RelationshipFlavor".Translate().ToString();
                default:
                    return string.Empty;
            }
        }

internal static string BuildSuggestedTemplate(string key)
        {
            switch (UserDefinedPromptVariableService.NormalizeKey(key))
            {
                case "pawn_personality_override":
                    return "{{ pawn.personality }}";
                case "pawn_personality_append":
                    return string.Empty;
                case "faction_tone":
                    return "{{ world.faction.name }} should sound measured, goal-oriented, and consistent with faction culture.";
                case "faction_attitude_text":
                    return "Attitude toward the player should reflect current diplomacy, recent actions, and strategic needs.";
                case "pawn_speaking_style":
                    return "Keep wording short, natural, and aligned with the pawn's current personality.";
                case "relationship_flavor":
                    return "Reflect relationship warmth, distance, trust, or tension through tone and word choice.";
                default:
                    return string.Empty;
            }
        }
    }
}
