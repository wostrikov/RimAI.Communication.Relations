using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Diagnostics;
using Ustas.RimAI.Communication.Relations.WorldState;
using RimWorld;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Prompting.Diplomacy;

namespace Ustas.RimAI.Communication.Relations.NpcDialogue
{
    internal sealed class NpcDialoguePushSlice3 : GameComponent_NpcDialoguePushManagerCollaborator
    {
        internal NpcDialoguePushSlice3(GameComponent_NpcDialoguePushManager owner) : base(owner)
        {
        }

internal void QueueTrigger(NpcDialogueTriggerContext context, int dueTick, int nowTick)
        {
            RelationsSettings settings = RelationsMod.Instance?.InstanceSettings;
            int maxPerFaction = Mathf.Clamp(settings?.NpcQueueMaxPerFaction ?? 3, 1, 10);
            int expireTicks = Mathf.RoundToInt((settings?.NpcQueueExpireHours ?? 12f) * TickPerHour);
            expireTicks = Mathf.Max(expireTicks, TickPerHour);

            int sameFactionCount = 0;
            QueuedNpcDialogueTrigger lowestPriority = null;
            int lowestScore = int.MaxValue;
            for (int i = 0; i < queuedTriggers.Count; i++)
            {
                var q = queuedTriggers[i];
                if (q?.faction != context.Faction) continue;
                sameFactionCount++;
                int score = GameComponent_NpcDialoguePushManager.GetQueueItemPriority(q);
                if (score < lowestScore)
                {
                    lowestScore = score;
                    lowestPriority = q;
                }
            }

            if (sameFactionCount >= maxPerFaction && lowestPriority != null)
            {
                queuedTriggers.Remove(lowestPriority);
                if (!queuedTriggers.Exists(q => q != null && q.faction == lowestPriority.faction))
                {
                    factionsInQueue.Remove(lowestPriority.faction);
                }
                Owner.LogThrottleDebug($"queue evict lowest priority: faction={context.Faction?.Name}, evicted={lowestPriority.sourceTag}");
            }

            var item = QueuedNpcDialogueTrigger.FromContext(
                context,
                nowTick,
                dueTick,
                nowTick + expireTicks);
            queuedTriggers.Add(item);
            factionsInQueue.Add(context.Faction);
            Owner.MarkFactionCandidate(context.Faction, nowTick);
            Owner.LogThrottleDebug($"queue add: faction={context.Faction?.Name}, due={dueTick}, expire={nowTick + expireTicks}, reason={context.SourceTag}");
        }

internal static int GetQueueItemPriority(QueuedNpcDialogueTrigger item)
        {
            if (item == null) return 0;

            int categoryScore = item.category switch
            {
                NpcDialogueCategory.WarningThreat => 100,
                NpcDialogueCategory.DiplomacyTask => 50,
                _ => 10
            };

            int severityScore = item.severity * 5;

            return categoryScore + severityScore;
        }

internal void CleanupExpiredQueue(int currentTick)
        {
            var removedFactions = new HashSet<Faction>();
            queuedTriggers.RemoveAll(q =>
            {
                if (q == null || q.faction == null || q.faction.defeated || q.expireTick <= currentTick)
                {
                    if (q?.faction != null) removedFactions.Add(q.faction);
                    return true;
                }
                return false;
            });

            foreach (Faction f in removedFactions)
            {
                if (!queuedTriggers.Exists(q => q != null && q.faction == f))
                {
                    factionsInQueue.Remove(f);
                }
            }
        }

public int CancelQueuedTriggersForFaction(Faction faction, string reason = "manual")
        {
            if (faction == null)
            {
                return 0;
            }

            int removed = queuedTriggers.RemoveAll(q => q != null && q.faction == faction);
            if (removed > 0)
            {
                factionsInQueue.Remove(faction);
                Owner.LogThrottleDebug($"queue clear: faction={faction.Name}, removed={removed}, reason={reason}");
            }
            return removed;
        }

internal bool ShouldRespectCooldown(NpcDialogueTriggerContext context, int currentTick)
        {
            if (context == null || context.Faction == null || Owner.CanBypassCooldown(context))
            {
                return false;
            }

            return Owner.GetOrCreateState(context.Faction).nextAllowedTick > currentTick;
        }

internal int GetReinitiateCooldownRemainingTicks(Faction faction, int currentTick)
        {
            if (faction == null)
            {
                return 0;
            }

            FactionDialogueSession session = GameComponent_DiplomacyManager.Instance?.GetSession(faction);
            if (session == null || !session.isConversationEndedByNpc)
            {
                return 0;
            }

            return Math.Max(0, session.GetReinitiateRemainingTicks(currentTick));
        }

internal int GetGlobalNextAllowedTick(int currentTick)
        {
            if (lastGlobalDeliveredTick <= 0)
            {
                return currentTick;
            }

            return lastGlobalDeliveredTick + Owner.GetGlobalDeliveryCooldownTicks();
        }

internal bool IsGlobalWindowLimitReached(int currentTick)
        {
            RelationsSettings settings = RelationsMod.Instance?.InstanceSettings;
            int maxMessages = settings?.NpcGlobalMaxMessagesPerWindow ?? 3;
            float windowHours = settings?.NpcGlobalWindowHours ?? 24f;
            int windowTicks = Mathf.RoundToInt(windowHours * TickPerHour);

            globalDeliveryOldestInWindow = int.MaxValue;
            for (int i = globalDeliveryTicks.Count - 1; i >= 0; i--)
            {
                if (currentTick - globalDeliveryTicks[i] > windowTicks)
                {
                    globalDeliveryTicks.RemoveAt(i);
                }
                else if (globalDeliveryTicks[i] < globalDeliveryOldestInWindow)
                {
                    globalDeliveryOldestInWindow = globalDeliveryTicks[i];
                }
            }

            return globalDeliveryTicks.Count >= maxMessages;
        }

internal int GetGlobalWindowNextAvailableTick(int currentTick)
        {
            if (globalDeliveryTicks.Count == 0)
            {
                return currentTick;
            }

            RelationsSettings settings = RelationsMod.Instance?.InstanceSettings;
            float windowHours = settings?.NpcGlobalWindowHours ?? 24f;
            int windowTicks = Mathf.RoundToInt(windowHours * TickPerHour);

            return globalDeliveryOldestInWindow + windowTicks;
        }

internal bool IsFactionWindowFull(Faction faction, int currentTick)
        {
            if (faction == null) return false;
            int key = faction.loadID;
            if (!factionDeliveryTicks.TryGetValue(key, out var ticks))
            {
                ticks = new List<int>();
                factionDeliveryTicks[key] = ticks;
            }
            for (int i = ticks.Count - 1; i >= 0; i--)
            {
                if (currentTick - ticks[i] > FactionWindowTicks)
                    ticks.RemoveAt(i);
            }
            return ticks.Count >= FactionWindowMaxMessages;
        }

internal void RecordFactionDelivery(Faction faction, int currentTick)
        {
            if (faction == null) return;
            int key = faction.loadID;
            if (!factionDeliveryTicks.TryGetValue(key, out var ticks))
            {
                ticks = new List<int>();
                factionDeliveryTicks[key] = ticks;
            }
            ticks.Add(currentTick);
        }

internal bool CanBypassCooldown(NpcDialogueTriggerContext context)
        {
            if (context == null)
            {
                return false;
            }

            if (context.BypassRateLimit)
            {
                return true;
            }

            if (context.TriggerType == NpcDialogueTriggerType.Causal &&
                context.Category == NpcDialogueCategory.WarningThreat &&
                context.Severity >= 3)
            {
                int currentTick = Find.TickManager?.TicksGame ?? 0;
                if (!Owner.IsBypassHardLimitReached(currentTick))
                {
                    return true;
                }
            }

            return false;
        }

internal bool IsBypassHardLimitReached(int currentTick)
        {
            const int bypassWindowHours = 6;
            const int maxBypassPerWindow = 2;
            int windowTicks = bypassWindowHours * TickPerHour;

            int recentBypassCount = globalDeliveryTicks.Count(t => currentTick - t <= windowTicks);
            return recentBypassCount >= maxBypassPerWindow;
        }

internal bool IsFactionUnavailable(Faction faction)
        {
            if (!Owner.IsValidTargetFaction(faction))
            {
                return true;
            }

            FactionPresenceStatus status = GameComponent_DiplomacyManager.Instance?.GetPresenceStatus(faction)
                ?? FactionPresenceStatus.Online;
            return status != FactionPresenceStatus.Online;
        }

internal bool IsValidTargetFaction(Faction faction)
        {
            if (faction == null) return false;
            return !GameComponent_DiplomacyManager.ShouldExcludeFactionFromAI(faction);
        }

internal bool IsPlayerBusy()
        {
            RelationsSettings settings = RelationsMod.Instance?.InstanceSettings;
            if (settings == null)
            {
                return false;
            }

            int currentTick = Find.TickManager?.TicksGame ?? 0;
            PlayerGameStateCache.Instance.EnsureFresh(currentTick);

            if (settings.EnableBusyByDrafted && PlayerGameStateCache.Instance.HasDrafted)
            {
                return true;
            }

            if (settings.EnableBusyByHostiles && PlayerGameStateCache.Instance.HasHostiles)
            {
                return true;
            }

            return settings.EnableBusyByClickRate && clickTicks.Count >= ClickBusyThreshold;
        }

internal void TrackClickSignal(int currentTick)
        {
            RelationsSettings settings = RelationsMod.Instance?.InstanceSettings;
            if (settings?.EnableBusyByClickRate != true)
            {
                clickTicks.Clear();
                return;
            }

            while (clickTicks.Count > 0 && currentTick - clickTicks.Peek() > ClickWindowTicks)
            {
                clickTicks.Dequeue();
            }
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

internal List<Faction> GetActiveCandidateFactions(int currentTick)
        {
            Owner.MaintainCandidateCache(currentTick);
            _reusableCandidateResults.Clear();
            foreach (Faction faction in activeCandidateFactions)
            {
                if (!Owner.IsValidTargetFaction(faction))
                {
                    continue;
                }

                if (Owner.IsCandidateStillActive(faction, currentTick))
                {
                    _reusableCandidateResults.Add(faction);
                }
            }

            if (_reusableCandidateResults.Count > MaxCandidateFactions)
            {
                _reusableCandidateResults.Sort((a, b) =>
                {
                    int ta = candidateTouchTicks.TryGetValue(a, out int va) ? va : 0;
                    int tb = candidateTouchTicks.TryGetValue(b, out int vb) ? vb : 0;
                    return tb.CompareTo(ta);
                });
                _reusableCandidateResults.RemoveRange(MaxCandidateFactions, _reusableCandidateResults.Count - MaxCandidateFactions);
            }

            return _reusableCandidateResults;
        }

internal FactionNpcPushState GetOrCreateState(Faction faction)
        {
            if (factionPushStatesByFaction.TryGetValue(faction, out FactionNpcPushState state))
            {
                return state;
            }

            state = new FactionNpcPushState
            {
                faction = faction,
                lastInteractionTick = Find.TickManager?.TicksGame ?? 0
            };
            factionPushStates.Add(state);
            factionPushStatesByFaction[faction] = state;
            return state;
        }

internal void CleanupInvalidState()
        {
            factionPushStates.RemoveAll(s =>
                s == null ||
                s.faction == null ||
                s.faction.defeated ||
                GameComponent_DiplomacyManager.ShouldExcludeFactionFromAI(s.faction));
            queuedTriggers.RemoveAll(q =>
                q == null ||
                q.faction == null ||
                q.faction.defeated ||
                (q.category == NpcDialogueCategory.WarningThreat && !q.bypassCategoryGate) ||
                GameComponent_DiplomacyManager.ShouldExcludeFactionFromAI(q.faction));
        }

internal void RebuildAllRuntimeIndexes()
        {
            factionPushStatesByFaction.Clear();
            for (int i = 0; i < factionPushStates.Count; i++)
            {
                var s = factionPushStates[i];
                if (s?.faction != null)
                    factionPushStatesByFaction[s.faction] = s;
            }

            factionsInQueue.Clear();
            for (int i = 0; i < queuedTriggers.Count; i++)
            {
                var q = queuedTriggers[i];
                if (q?.faction != null)
                    factionsInQueue.Add(q.faction);
            }

            factionsWithPendingRequests.Clear();
            foreach (var pair in pendingRequests)
            {
                var f = pair.Value?.Context?.Faction;
                if (f != null)
                    factionsWithPendingRequests.Add(f);
            }
        }

internal void MaintainCandidateCache(int currentTick)
        {
            if (currentTick - lastCandidateSessionSyncTick >= CandidateSessionSyncIntervalTicks)
            {
                Owner.SyncCandidateCacheFromRecentSessions(currentTick);
                lastCandidateSessionSyncTick = currentTick;
            }

            if (currentTick - lastCandidateCacheMaintenanceTick < CandidateCacheMaintenanceIntervalTicks)
            {
                return;
            }

            lastCandidateCacheMaintenanceTick = currentTick;
            if (activeCandidateFactions.Count == 0)
            {
                return;
            }

            var stale = new List<Faction>();
            foreach (Faction faction in activeCandidateFactions)
            {
                if (!Owner.IsCandidateStillActive(faction, currentTick))
                {
                    stale.Add(faction);
                }
            }

            foreach (Faction faction in stale)
            {
                activeCandidateFactions.Remove(faction);
                candidateTouchTicks.Remove(faction);
            }
        }
    }
}
