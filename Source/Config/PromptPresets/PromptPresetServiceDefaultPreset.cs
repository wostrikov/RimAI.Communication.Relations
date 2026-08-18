using System;
using System.Collections.Generic;
using System.Linq;

namespace Ustas.RimAI.Communication.Relations.Config
{
    /// <summary>
    /// Dependencies: preset payload equivalence helpers and canonical default payload builder.
    /// Responsibility: resolve stable default preset identity and timestamped auto-fork naming.
    /// </summary>
        internal sealed class PromptPresetServiceDefaultPreset : PromptPresetServiceCollaborator
    {
        internal PromptPresetServiceDefaultPreset(PromptPresetService owner) : base(owner)
        {
        }


        internal static string BuildTimestampPresetName(string prefix, DateTime nowLocal)
        {
            string stem = string.IsNullOrWhiteSpace(prefix) ? "Custom" : prefix.Trim();
            return $"{stem} {nowLocal:yyyyMMdd-HHmmss}";
        }

        internal static string ResolveDefaultPresetId(List<PromptPresetConfig> presets)
        {
            List<PromptPresetConfig> all = presets?.Where(p => p != null).ToList() ?? new List<PromptPresetConfig>();
            if (all.Count == 0)
            {
                return string.Empty;
            }

            PromptPresetConfig immutable = all.FirstOrDefault(p =>
                string.Equals(p?.Id, ImmutableDefaultPresetId, StringComparison.Ordinal));
            if (immutable != null)
            {
                return immutable.Id;
            }

            PromptPresetChannelPayloads canonicalPayload = PromptPresetService.CreateCanonicalDefaultPayload();
            List<PromptPresetConfig> candidates = all
                .Where(p => PromptPresetService.IsCanonicalDefaultCandidate(p, canonicalPayload))
                .ToList();
            PromptPresetConfig selected = candidates.Count > 0
                ? PromptPresetService.SelectEarliestPreset(candidates, all)
                : PromptPresetService.SelectEarliestPreset(all, all);
            return selected?.Id ?? all[0].Id;
        }

        internal static bool IsCanonicalDefaultCandidate(PromptPresetConfig preset, PromptPresetChannelPayloads canonicalPayload)
        {
            if (preset?.ChannelPayloads == null)
            {
                return false;
            }

            PromptPresetChannelPayloads left = preset.ChannelPayloads.Clone();
            PromptPresetChannelPayloads right = canonicalPayload?.Clone() ?? PromptPresetService.CreateCanonicalDefaultPayload();
            PromptPresetService.NormalizePayload(left);
            PromptPresetService.NormalizePayload(right);
            return PromptPresetService.ArePayloadsEquivalent(left, right);
        }

        internal static PromptPresetConfig SelectEarliestPreset(
            List<PromptPresetConfig> candidates,
            List<PromptPresetConfig> allPresets)
        {
            List<PromptPresetConfig> all = candidates?.Where(p => p != null).ToList() ?? new List<PromptPresetConfig>();
            if (all.Count == 0)
            {
                return null;
            }

            return all
                .OrderBy(p => PromptPresetService.ParseCreatedAtOrMax(p.CreatedAtUtc))
                .ThenBy(p => PromptPresetService.ResolvePresetIndex(allPresets, p.Id))
                .FirstOrDefault();
        }

        internal static DateTime ParseCreatedAtOrMax(string value)
        {
            if (DateTime.TryParse(value, out DateTime parsed))
            {
                return parsed;
            }

            return DateTime.MaxValue;
        }

        internal static int ResolvePresetIndex(List<PromptPresetConfig> presets, string presetId)
        {
            if (presets == null || string.IsNullOrWhiteSpace(presetId))
            {
                return int.MaxValue;
            }

            int index = presets.FindIndex(p => string.Equals(p?.Id, presetId, StringComparison.Ordinal));
            return index < 0 ? int.MaxValue : index;
        }
        }

}
