using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.NpcDialogue;
using Ustas.RimAI.Communication.Relations.Diagnostics;
using Ustas.RimAI.Communication.Relations.Guards;
using Ustas.RimAI.Communication.Relations.WorldState;
using RimWorld;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Relations.PawnRpgPush
{
    internal sealed class PawnRpgPushSlice1 : GameComponent_PawnRpgDialoguePushManagerCollaborator
    {
        internal PawnRpgPushSlice1(GameComponent_PawnRpgDialoguePushManager owner) : base(owner)
        {
        }

public void RegisterTradeCompletedTrigger(Faction faction, int soldCount, int boughtCount)
        {
            if (!Owner.IsValidTargetFaction(faction) || soldCount <= 0 && boughtCount <= 0)
            {
                return;
            }

            Owner.EnqueueIncoming(new PawnRpgTriggerContext
            {
                Faction = faction,
                TriggerType = NpcDialogueTriggerType.Causal,
                Category = NpcDialogueCategory.DiplomacyTask,
                SourceTag = "trade_completed",
                Reason = "trade_completed",
                Severity = 1,
                CreatedTick = Find.TickManager?.TicksGame ?? 0,
                Metadata = $"{soldCount}|{boughtCount}"
            });
        }

public void RegisterGoodwillShiftTrigger(Faction faction, int goodwillDelta, string reason, bool likelyHostile)
        {
            if (!Owner.IsValidTargetFaction(faction) || Math.Abs(goodwillDelta) < 10)
            {
                return;
            }

            NpcDialogueCategory category = goodwillDelta < 0
                ? NpcDialogueCategory.WarningThreat
                : NpcDialogueCategory.DiplomacyTask;
            int severity = likelyHostile ? 3 : (goodwillDelta < 0 ? 2 : 1);
            Owner.EnqueueIncoming(new PawnRpgTriggerContext
            {
                Faction = faction,
                TriggerType = NpcDialogueTriggerType.Causal,
                Category = category,
                SourceTag = "goodwill_shift",
                Reason = reason ?? string.Empty,
                Severity = severity,
                CreatedTick = Find.TickManager?.TicksGame ?? 0,
                Metadata = goodwillDelta.ToString()
            });
        }

public void RegisterThreatStateTrigger(Faction faction, bool hasHive, bool hasHostiles)
        {
            if (!Owner.IsValidTargetFaction(faction) || !hasHive && !hasHostiles)
            {
                return;
            }

            Owner.EnqueueIncoming(new PawnRpgTriggerContext
            {
                Faction = faction,
                TriggerType = NpcDialogueTriggerType.Causal,
                Category = NpcDialogueCategory.WarningThreat,
                SourceTag = hasHive ? "hive_nearby" : "hostiles_nearby",
                Reason = hasHive ? "hive_warning" : "hostile_warning",
                Severity = hasHive ? 3 : 2,
                CreatedTick = Find.TickManager?.TicksGame ?? 0
            });
        }

public void RegisterPlayerLeftClick()
        {
            RelationsSettings settings = RelationsMod.Instance?.InstanceSettings;
            if (settings?.EnableBusyByClickRate != true || Find.TickManager == null)
            {
                return;
            }

            clickTicks.Enqueue(Find.TickManager.TicksGame);
        }

public bool DebugForcePawnRpgProactiveDialogue()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.TickManager == null)
            {
                Log.Warning("[RimAI.Relations] DebugForcePawnRpg: Not in playing state.");
                return false;
            }

            if (!AI.AIChatServiceAsync.Instance.IsConfigured())
            {
                Log.Warning("[RimAI.Relations] DebugForcePawnRpg: AI not configured.");
                return false;
            }

            int now = Find.TickManager.TicksGame;
            if (!Owner.HasConfiguredProtagonists())
            {
                Owner.LogMissingProtagonists(now);
                return false;
            }

            // Path 1: NPC → colonist
            IReadOnlyCollection<Faction> factions = Owner.GetActiveCandidateFactionsOnPlayerMaps(now);
            if (factions.Count > 0)
            {
                foreach (Faction faction in factions.InRandomOrder().ToList())
                {
                    if (!Owner.TryResolvePairForFaction(faction, now, true, true, true, out Pawn npcPawn, out Pawn playerPawn))
                    {
                        continue;
                    }

                    Log.Message($"[RimAI.Relations] DebugForcePawnRpg: NPC path resolved: NPC={npcPawn.LabelShortCap}, Player={playerPawn.LabelShortCap}");
                    var context = new PawnRpgTriggerContext
                    {
                        Faction = faction,
                        TriggerType = NpcDialogueTriggerType.Causal,
                        Category = NpcDialogueCategory.Social,
                        SourceTag = "debug_force",
                        Reason = "manual_debug_trigger",
                        Severity = 1,
                        CreatedTick = now
                    };
                    Owner.StartGeneration(context, npcPawn, playerPawn);
                    return true;
                }
            }

            // Path 2: colonist → colonist (fallback)
            if (Owner.TryResolveColonistPair(now, out Pawn initiator, out Pawn receiver, bypassAvailability: true))
            {
                Log.Message($"[RimAI.Relations] DebugForcePawnRpg: Colonist path resolved: Initiator={initiator.LabelShortCap}, Receiver={receiver.LabelShortCap}");
                var context = new PawnRpgTriggerContext
                {
                    Faction = Faction.OfPlayer,
                    TriggerType = NpcDialogueTriggerType.Causal,
                    Category = NpcDialogueCategory.Social,
                    SourceTag = "debug_force_colonist",
                    Reason = "manual_debug_trigger",
                    Severity = 1,
                    CreatedTick = now
                };
                Owner.StartGeneration(context, initiator, receiver);
                return true;
            }

            Log.Warning("[RimAI.Relations] DebugForcePawnRpg: Both paths failed. No valid pair found.");
            return false;
        }

public bool TryAddRpgProactiveProtagonist(Pawn pawn)
        {
            if (!Owner.CanConfigureAsProtagonist(pawn))
            {
                return false;
            }

            if (Owner.ContainsRpgProactiveProtagonist(pawn))
            {
                return true;
            }

            if (Owner.GetConfiguredProtagonistCount() >= Owner.GetRpgProactiveProtagonistCap())
            {
                return false;
            }

            proactiveProtagonists.Add(PawnRpgProtagonistEntry.FromPawn(pawn));
            _cachedProtagonists = null;
            return true;
        }

public bool RemoveRpgProactiveProtagonist(Pawn pawn)
        {
            if (pawn == null || proactiveProtagonists == null || proactiveProtagonists.Count == 0)
            {
                return false;
            }

            int before = proactiveProtagonists.Count;
            proactiveProtagonists.RemoveAll(entry => GameComponent_PawnRpgDialoguePushManager.IsSamePawn(entry, pawn));
            _cachedProtagonists = null;
            return proactiveProtagonists.Count < before;
        }

public void ClearRpgProactiveProtagonists()
        {
            proactiveProtagonists.Clear();
            _cachedProtagonists = null;
        }

public int GetConfiguredProtagonistCount()
        {
            if (proactiveProtagonists == null)
            {
                return 0;
            }

            return proactiveProtagonists.Count(entry => entry?.HasConfiguredIdentifier == true);
        }

public int GetRpgProactiveProtagonistCap()
        {
            RelationsSettings settings = RelationsMod.Instance?.InstanceSettings;
            int configured = settings?.PawnRpgProtagonistCap ?? 20;
            return Mathf.Clamp(configured, 1, 100);
        }

internal void AutoSelectDefaultProtagonist()
        {
            if (proactiveProtagonists == null || proactiveProtagonists.Count > 0) return;

            Pawn best = GameComponent_PawnRpgDialoguePushManager.FindBestSkillColonist();
            if (best != null)
            {
                proactiveProtagonists.Add(PawnRpgProtagonistEntry.FromPawn(best));
                _cachedProtagonists = null;
                Log.Message($"[RimAI.Relations] Auto-selected default protagonist: {best.LabelShortCap} (highest skills)");
            }
        }

internal static Pawn FindBestSkillColonist()
        {
            Pawn best = null;
            int bestScore = -1;
            foreach (Map map in Find.Maps)
            {
                if (map == null) continue;
                foreach (Pawn p in map.mapPawns.FreeColonistsSpawned)
                {
                    if (p?.skills == null || p.Dead || p.Destroyed || p.IsPrisoner || p.Faction != Faction.OfPlayer) continue;
                    int score = 0;
                    foreach (SkillRecord skill in p.skills.skills)
                    {
                        score += skill.Level;
                    }
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = p;
                    }
                }
            }
            return best;
        }

public void SetRpgProactiveProtagonistCap(int value)
        {
            RelationsSettings settings = RelationsMod.Instance?.InstanceSettings;
            if (settings == null)
            {
                return;
            }

            settings.PawnRpgProtagonistCap = Mathf.Clamp(value, 1, 100);
        }

public List<Pawn> GetEligibleRpgProactiveTargetsOnMap(Map map)
        {
            if (map?.mapPawns?.AllPawnsSpawned == null)
            {
                return new List<Pawn>();
            }

            List<Pawn> protagonists = Owner.ResolveConfiguredProtagonists();
            List<Pawn> result = new List<Pawn>(protagonists.Count);
            HashSet<Pawn> seen = new HashSet<Pawn>();
            for (int i = 0; i < protagonists.Count; i++)
            {
                Pawn pawn = protagonists[i];
                if (pawn != null && Owner.IsEligiblePlayerPawn(pawn) && pawn.Map == map && seen.Add(pawn))
                    result.Add(pawn);
            }
            return result;
        }

internal void ClearTransientState()
        {
            incomingTriggers.Clear();
            pendingRequests.Clear();
            factionsWithPendingRequests.Clear();
            clickTicks.Clear();
            recentQuestTriggerTicks.Clear();
            recentMessageHashes.Clear();
            rpgDeliveryTicks.Clear();
            recentEventDeliveries.Clear();
        }

internal bool IsRpgDeliveryWindowFull(int currentTick)
        {
            for (int i = rpgDeliveryTicks.Count - 1; i >= 0; i--)
            {
                if (currentTick - rpgDeliveryTicks[i] > RpgWindowTicks)
                    rpgDeliveryTicks.RemoveAt(i);
            }
            return rpgDeliveryTicks.Count >= RpgWindowMaxMessages;
        }

internal void CleanupExpiredMessageHashes(int currentTick)
        {
            if (recentMessageHashes == null || recentMessageHashes.Count == 0) return;
            List<string> expiredKeys = null;
            foreach (var kv in recentMessageHashes)
            {
                if (currentTick - kv.Value > MessageDedupWindowTicks)
                {
                    expiredKeys ??= new List<string>();
                    expiredKeys.Add(kv.Key);
                }
            }
            if (expiredKeys != null)
            {
                for (int i = 0; i < expiredKeys.Count; i++)
                    recentMessageHashes.Remove(expiredKeys[i]);
            }
        }

internal static string ComputeContentHash(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            string normalized = text.Trim().ToLowerInvariant();
            // Collapse multiple whitespace into single space
            var sb = new System.Text.StringBuilder(normalized.Length);
            bool lastWasSpace = false;
            foreach (char c in normalized)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (!lastWasSpace) sb.Append(' ');
                    lastWasSpace = true;
                }
                else
                {
                    sb.Append(c);
                    lastWasSpace = false;
                }
            }
            return sb.ToString().GetHashCode().ToString();
        }

internal void EnqueueIncoming(PawnRpgTriggerContext context)
        {
            if (context == null || !Owner.IsValidTargetFaction(context.Faction))
            {
                return;
            }

            incomingTriggers.Enqueue(context);
        }

internal void DrainIncomingTriggers(int currentTick)
        {
            int safeguard = 0;
            while (incomingTriggers.Count > 0 && safeguard++ < 200)
            {
                PawnRpgTriggerContext context = incomingTriggers.Dequeue();
                Owner.HandleTriggerContext(context, currentTick);
            }
        }

internal void HandleTriggerContext(PawnRpgTriggerContext context, int currentTick)
        {
            if (context == null || !Owner.IsValidTargetFaction(context.Faction))
            {
                return;
            }

            if (!Owner.HasConfiguredProtagonists())
            {
                Owner.LogMissingProtagonists(currentTick);
                return;
            }

            int dueTick = currentTick;
            if (context.TriggerType == NpcDialogueTriggerType.Causal)
            {
                dueTick += Rand.RangeInclusive(CausalMinDelayTicks, CausalMaxDelayTicks);
            }

            dueTick = Math.Max(dueTick, Owner.GetNextAllowedTickForContext(context, currentTick));
            if (Owner.IsFactionPending(context.Faction) || Owner.IsPlayerBusy())
            {
                dueTick = Math.Max(dueTick, currentTick + BlockedRetryTicks);
            }

            if (dueTick <= currentTick && Owner.TryStartGenerationForContext(context, currentTick))
            {
                return;
            }

            Owner.QueueTrigger(context, Math.Max(dueTick, currentTick + BlockedRetryTicks), currentTick);
        }
    }
}
