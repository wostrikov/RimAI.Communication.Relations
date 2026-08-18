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

using RecentEventIntelItem = Ustas.RimAI.Communication.Relations.Context.RelationsContextAssembler.RecentEventIntelItem;
using RecentEventSelectionResult = Ustas.RimAI.Communication.Relations.Context.RelationsContextAssembler.RecentEventSelectionResult;

namespace Ustas.RimAI.Communication.Relations.Context
{
    internal sealed class RelationsContextSlice1 : RelationsContextAssemblerCollaborator
    {
        internal RelationsContextSlice1(RelationsContextAssembler owner) : base(owner)
        {
        }

internal string BuildEnvironmentPromptBlocksWithDiagnostics(
            SystemPromptConfig config,
            DialogueScenarioContext context,
            out EnvironmentPromptBuildDiagnostics diagnostics)
        {
            diagnostics = new EnvironmentPromptBuildDiagnostics();
            return Owner.BuildEnvironmentPromptBlocksInternal(config, context, diagnostics);
        }

internal string BuildEnvironmentPromptBlocksInternal(
            SystemPromptConfig config,
            DialogueScenarioContext context,
            EnvironmentPromptBuildDiagnostics diagnostics)
        {
            if (config?.EnvironmentPrompt == null || context == null)
            {
                return string.Empty;
            }

            if (host.SnapshotService.TryGetScopedRuntimeSnapshotForFaction(context.Faction, out DiplomacyPromptRuntimeSnapshot snapshot) &&
                !string.IsNullOrWhiteSpace(snapshot.EnvironmentPromptBlock))
            {
                return snapshot.EnvironmentPromptBlock;
            }

            var env = config.EnvironmentPrompt;
            var sb = new StringBuilder();

            if (env.Worldview?.Enabled == true && !string.IsNullOrWhiteSpace(env.Worldview.Content))
            {
                sb.AppendLine(env.Worldview.Content.Trim());
                sb.AppendLine();
            }

            Owner.AppendEnvironmentContextBlock(sb, env, context);
            Owner.AppendRecentWorldEventIntel(sb, env, context);

            if (!(env.SceneSystem?.Enabled ?? false) || env.SceneEntries == null || env.SceneEntries.Count == 0)
            {
                return sb.ToString();
            }

            HashSet<string> tags = Owner.BuildScenarioTags(context, env.SceneSystem.PresetTagsEnabled);
            if (diagnostics != null)
            {
                diagnostics.ScenarioTags.AddRange(tags.OrderBy(tag => tag));
            }

            int maxPerScene = env.SceneSystem.MaxSceneChars > 0 ? env.SceneSystem.MaxSceneChars : int.MaxValue;
            int maxTotalSceneChars = env.SceneSystem.MaxTotalChars > 0 ? env.SceneSystem.MaxTotalChars : int.MaxValue;
            int totalSceneChars = 0;
            int appendedCount = 0;

            var orderedEntries = env.SceneEntries
                .Where(entry => entry != null)
                .OrderByDescending(entry => entry.Priority)
                .ThenBy(entry => entry.Name ?? string.Empty)
                .ToList();

            foreach (ScenePromptEntryConfig entry in orderedEntries)
            {
                EnvironmentSceneEntryDiagnostic sceneDiag = null;
                if (diagnostics != null)
                {
                    sceneDiag = new EnvironmentSceneEntryDiagnostic
                    {
                        Id = entry.Id ?? string.Empty,
                        Name = string.IsNullOrWhiteSpace(entry.Name) ? "UnnamedScene" : entry.Name.Trim(),
                        Priority = entry.Priority
                    };
                    diagnostics.SceneEntries.Add(sceneDiag);
                }

                if (!entry.Enabled)
                {
                    if (sceneDiag != null)
                    {
                        sceneDiag.SkipReason = "disabled";
                    }
                    continue;
                }

                bool channelMatched = context.IsRpg ? entry.ApplyToRPG : entry.ApplyToDiplomacy;
                if (sceneDiag != null)
                {
                    sceneDiag.ChannelMatched = channelMatched;
                }

                if (!channelMatched)
                {
                    if (sceneDiag != null)
                    {
                        sceneDiag.SkipReason = "channel_filtered";
                    }
                    continue;
                }

                bool tagsMatched = Owner.EntryMatchesTags(entry, tags);
                if (sceneDiag != null)
                {
                    sceneDiag.TagsMatched = tagsMatched;
                }

                if (!tagsMatched)
                {
                    if (sceneDiag != null)
                    {
                        sceneDiag.SkipReason = "tag_filtered";
                    }
                    continue;
                }

                string content = entry.Content?.Trim() ?? string.Empty;
                if (content.Length == 0)
                {
                    if (sceneDiag != null)
                    {
                        sceneDiag.SkipReason = "empty";
                    }
                    continue;
                }

                content = host.TemplateVariables.RenderTemplateVariables(content, context, env, out List<string> usedVariables, out List<string> unknownVariables);
                if (sceneDiag != null)
                {
                    sceneDiag.UsedVariables.AddRange(usedVariables);
                    sceneDiag.UnknownVariables.AddRange(unknownVariables);
                }

                if (content.Length == 0)
                {
                    if (sceneDiag != null)
                    {
                        sceneDiag.SkipReason = "empty_after_render";
                    }
                    continue;
                }

                int originalChars = content.Length;
                if (content.Length > maxPerScene)
                {
                    content = content.Substring(0, maxPerScene);
                    if (sceneDiag != null)
                    {
                        sceneDiag.TruncatedByPerSceneLimit = true;
                    }
                }

                int remain = maxTotalSceneChars - totalSceneChars;
                if (remain <= 0)
                {
                    if (sceneDiag != null)
                    {
                        sceneDiag.SkipReason = "total_limit_exceeded";
                    }
                    if (diagnostics == null)
                    {
                        break;
                    }
                    continue;
                }

                if (content.Length > remain)
                {
                    content = content.Substring(0, remain);
                    if (sceneDiag != null)
                    {
                        sceneDiag.TruncatedByTotalLimit = true;
                    }
                }

                if (content.Length == 0)
                {
                    if (sceneDiag != null)
                    {
                        sceneDiag.SkipReason = "empty_after_limit";
                    }
                    continue;
                }

                if (appendedCount == 0)
                {
                    sb.AppendLine("=== SCENE PROMPT LAYERS ===");
                }

                string name = string.IsNullOrWhiteSpace(entry.Name) ? "UnnamedScene" : entry.Name.Trim();
                sb.AppendLine($"[{name}]");
                sb.AppendLine(content);
                sb.AppendLine();
                appendedCount++;
                totalSceneChars += content.Length;

                if (sceneDiag != null)
                {
                    sceneDiag.Included = true;
                    sceneDiag.OriginalChars = originalChars;
                    sceneDiag.AppliedChars = content.Length;
                    sceneDiag.SkipReason = string.Empty;
                }
            }

            return sb.ToString();
        }

internal void AppendEnvironmentContextBlock(StringBuilder sb, EnvironmentPromptConfig env, DialogueScenarioContext context)
        {
            EnvironmentContextSwitchesConfig switches = env?.EnvironmentContextSwitches;
            if (!(switches?.Enabled ?? false))
            {
                return;
            }

            Map map = Owner.ResolveEnvironmentMap(context);
            if (map == null)
            {
                return;
            }

            if (!Owner.TryResolveFocusCell(map, context, out IntVec3 focusCell))
            {
                return;
            }

            List<string> lines = Owner.BuildEnvironmentContextLines(map, focusCell, context, switches);
            if (lines.Count == 0)
            {
                return;
            }

            sb.AppendLine("=== ENVIRONMENT PARAMETERS ===");
            foreach (string line in lines)
            {
                sb.AppendLine(line);
            }
            sb.AppendLine();
        }

internal void AppendRecentWorldEventIntel(StringBuilder sb, EnvironmentPromptConfig env, DialogueScenarioContext context)
        {
            if (sb == null)
            {
                return;
            }

            if (!Owner.TryCollectRecentEventIntelItems(env, context, out List<RecentEventIntelItem> items))
            {
                return;
            }

            EventIntelPromptConfig intel = env?.EventIntelPrompt ?? new EventIntelPromptConfig();
            int maxItems = Mathf.Clamp(intel.MaxInjectedItems, 1, 50);
            int maxChars = Mathf.Clamp(intel.MaxInjectedChars, 200, 12000);
            RecentEventSelectionResult selection = Owner.SelectRecentEventIntelLines(items, maxItems, maxChars);
            if (selection.SelectedLines.Count == 0)
            {
                return;
            }

            sb.AppendLine("=== RECENT WORLD EVENTS & BATTLE INTEL ===");
            for (int i = 0; i < selection.SelectedLines.Count; i++)
            {
                sb.AppendLine(selection.SelectedLines[i]);
            }

            if (selection.OmittedCount > 0)
            {
                List<string> digestLines = Owner.BuildRecentEventDigestLines(items, selection);
                for (int i = 0; i < digestLines.Count; i++)
                {
                    sb.AppendLine(digestLines[i]);
                }
            }

            sb.AppendLine();
        }

internal string BuildRecentWorldEventIntelCompactDigest(
            EnvironmentPromptConfig env,
            DialogueScenarioContext context,
            int maxItems = 2,
            int maxChars = 260)
        {
            if (!Owner.TryCollectRecentEventIntelItems(env, context, out List<RecentEventIntelItem> items))
            {
                return string.Empty;
            }

            RecentEventSelectionResult selection = Owner.SelectRecentEventIntelLines(
                items,
                Mathf.Clamp(maxItems, 1, 6),
                Mathf.Clamp(maxChars, 120, 1200));
            if (selection.SelectedLines.Count == 0)
            {
                return string.Empty;
            }

            string latestDigest = string.Join(" | ", selection.SelectedLines
                .Take(2)
                .Select(Owner.BuildCompactDigestEntry)
                .Where(line => !string.IsNullOrWhiteSpace(line)));
            string typeDigest = Owner.BuildTypeDigest(items);
            string topicDigest = Owner.BuildTopicDigest(items);
            string trendDigest = Owner.BuildTrendDigest(items);

            var sb = new StringBuilder();
            sb.Append("See <environment> for full event details. ");
            sb.Append("Digest: ");
            if (!string.IsNullOrWhiteSpace(latestDigest))
            {
                sb.Append("latest=");
                sb.Append(latestDigest);
                sb.Append("; ");
            }

            sb.Append("total=");
            sb.Append(items.Count);
            if (selection.OmittedCount > 0)
            {
                sb.Append(", omitted=");
                sb.Append(selection.OmittedCount);
            }

            sb.Append("; types=");
            sb.Append(typeDigest);
            sb.Append("; topics=");
            sb.Append(topicDigest);
            sb.Append("; trend=");
            sb.Append(trendDigest);
            return Owner.ClampPromptBlock(sb.ToString().Trim(), 420);
        }

internal bool TryCollectRecentEventIntelItems(
            EnvironmentPromptConfig env,
            DialogueScenarioContext context,
            out List<RecentEventIntelItem> items)
        {
            items = new List<RecentEventIntelItem>();
            EventIntelPromptConfig intel = env?.EventIntelPrompt;
            if (intel == null || !intel.Enabled || context == null)
            {
                return false;
            }

            if (context.IsRpg && !intel.ApplyToRpg)
            {
                return false;
            }

            if (!context.IsRpg && !intel.ApplyToDiplomacy)
            {
                return false;
            }

            WorldEventLedgerComponent ledger = WorldEventLedgerComponent.Instance;
            if (ledger == null)
            {
                return false;
            }

            Faction observer = context.Faction ?? context.Target?.Faction ?? context.Initiator?.Faction;
            if (intel.IncludeMapEvents)
            {
                List<WorldEventRecord> mapEvents = ledger.GetRecentWorldEvents(observer, intel.DaysWindow, includePublic: true, includeDirect: true);
                for (int i = 0; i < mapEvents.Count; i++)
                {
                    WorldEventRecord record = mapEvents[i];
                    if (record == null || string.IsNullOrWhiteSpace(record.Summary))
                    {
                        continue;
                    }

                    items.Add(new RecentEventIntelItem
                    {
                        Tick = record.OccurredTick,
                        Category = "MapEvent",
                        Summary = record.Summary.Trim(),
                        EventType = string.IsNullOrWhiteSpace(record.EventType) ? "map_event" : record.EventType
                    });
                }
            }

            if (intel.IncludeRaidBattleReports)
            {
                List<RaidBattleReportRecord> reports = ledger.GetRecentRaidBattleReports(observer, intel.DaysWindow, includeDirect: true);
                for (int i = 0; i < reports.Count; i++)
                {
                    RaidBattleReportRecord report = reports[i];
                    if (report == null || string.IsNullOrWhiteSpace(report.Summary))
                    {
                        continue;
                    }

                    items.Add(new RecentEventIntelItem
                    {
                        Tick = report.BattleEndTick,
                        Category = "BattleIntel",
                        Summary = report.Summary.Trim(),
                        EventType = "battle_intel"
                    });
                }
            }

            items = items
                .OrderByDescending(item => item.Tick)
                .ToList();
            return items.Count > 0;
        }
    }
}
