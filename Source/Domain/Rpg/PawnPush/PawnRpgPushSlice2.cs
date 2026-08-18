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
    internal sealed class PawnRpgPushSlice2 : GameComponent_PawnRpgDialoguePushManagerCollaborator
    {
        internal PawnRpgPushSlice2(GameComponent_PawnRpgDialoguePushManager owner) : base(owner)
        {
        }

internal void ProcessQueuedTriggers(int currentTick)
        {
            using (PerfScope.Measure("RpgPush.QueueProcess.Cleanup"))
                Owner.CleanupExpiredQueue(currentTick);
            if (!Owner.HasConfiguredProtagonists())
            {
                if (queuedTriggers.Count > 0)
                {
                    queuedTriggers.Clear();
                }

                Owner.LogMissingProtagonists(currentTick);
                return;
            }

            int dueCount = 0;
            for (int i = 0; i < queuedTriggers.Count; i++)
            {
                if (queuedTriggers[i]?.dueTick <= currentTick) dueCount++;
            }

            if (dueCount > 1)
            {
                queuedTriggers.Sort((a, b) => (a?.dueTick ?? 0).CompareTo(b?.dueTick ?? 0));
            }

            int processed = 0;
            for (int i = queuedTriggers.Count - 1; i >= 0; i--)
            {
                if (processed >= 3) break;
                QueuedPawnRpgTrigger item = queuedTriggers[i];
                if (item == null || item.dueTick > currentTick) continue;

                if (!Owner.IsValidTargetFaction(item.faction))
                {
                    queuedTriggers.RemoveAt(i);
                    continue;
                }

                PawnRpgTriggerContext context = item.ToContext();

                using (PerfScope.Measure("RpgPush.QueueProcess.PreGate"))
                {
                    if (Owner.IsFactionPending(context.Faction) || Owner.IsPlayerBusy())
                    {
                        item.dueTick = currentTick + BlockedRetryTicks;
                        continue;
                    }

                    int nextAllowed = Owner.GetNextAllowedTickForContext(context, currentTick);
                    if (nextAllowed > currentTick)
                    {
                        item.dueTick = nextAllowed;
                        continue;
                    }
                }

                bool startResult;
                using (PerfScope.Measure("RpgPush.QueueProcess.Generation"))
                    startResult = Owner.TryStartGenerationForContext(context, currentTick);

                if (!startResult)
                {
                    item.dueTick = currentTick + BlockedRetryTicks;
                    continue;
                }

                queuedTriggers.RemoveAt(i);
                processed++;
            }
        }

internal void EvaluateRegularTriggers(int currentTick)
        {
            Owner.CleanupQuestTriggerCache(currentTick);
            if (Owner.IsRpgDeliveryWindowFull(currentTick))
            {
                return;
            }
            float chance = Owner.GetRegularTriggerChance(RelationsMod.Instance?.InstanceSettings?.NpcPushFrequencyMode ?? NpcPushFrequencyMode.Low);
            foreach (Faction faction in Owner.GetActiveCandidateFactionsOnPlayerMaps(currentTick))
            {
                if (Owner.IsFactionPending(faction))
                {
                    continue;
                }

                if (Owner.TryCreateQuestDeadlineContext(faction, currentTick, out PawnRpgTriggerContext questContext))
                {
                    Owner.HandleTriggerContext(questContext, currentTick);
                    continue;
                }

                if (Owner.TryCreateLowMoodContext(faction, currentTick, out PawnRpgTriggerContext moodContext))
                {
                    Owner.HandleTriggerContext(moodContext, currentTick);
                    continue;
                }

                if (Rand.Value > chance)
                {
                    continue;
                }

                var ambientContext = new PawnRpgTriggerContext
                {
                    Faction = faction,
                    TriggerType = NpcDialogueTriggerType.Ambient,
                    Category = NpcDialogueCategory.Social,
                    SourceTag = "ambient",
                    Reason = "ambient_social",
                    Severity = 1,
                    CreatedTick = currentTick
                };
                Owner.HandleTriggerContext(ambientContext, currentTick);
            }

            Owner.EvaluateColonistPairAmbientTriggers(currentTick, chance);
            Owner.EvaluateColonistPairLowMoodTriggers(currentTick);
            Owner.EvaluateHomeEventTriggers(currentTick);
        }

internal void EvaluateThreatTriggers(int currentTick)
        {
            PlayerGameStateCache.Instance.EnsureFresh(currentTick);
            bool hasHostiles = PlayerGameStateCache.Instance.HasHostiles;
            bool hasHive = PlayerGameStateCache.Instance.HasHiveThreat;
            bool hasThreat = hasHostiles || hasHive;

            if (!hasThreat)
            {
                _colonistPairHadThreat = false;
            }

            foreach (Faction faction in Owner.GetActiveCandidateFactionsOnPlayerMaps(currentTick))
            {
                PawnRpgThreatState state = Owner.GetOrCreateThreatState(faction);
                if (!hasThreat)
                {
                    state.hadThreat = false;
                    continue;
                }

                if (state.hadThreat)
                {
                    continue;
                }

                Owner.RegisterThreatStateTrigger(faction, hasHive, hasHostiles);
                state.hadThreat = true;
            }

            if (hasThreat && !_colonistPairHadThreat)
            {
                Owner.EvaluateColonistPairThreatTriggers(currentTick, hasHive, hasHostiles);
                _colonistPairHadThreat = true;
            }
        }

internal bool TryStartGenerationForContext(PawnRpgTriggerContext context, int currentTick)
        {
            if (!Owner.HasConfiguredProtagonists())
            {
                Owner.LogMissingProtagonists(currentTick);
                return false;
            }

            if (GameComponent_PawnRpgDialoguePushManager.IsColonistPairContext(context))
            {
                Pawn initiator, receiver;
                using (PerfScope.Measure("RpgPush.QueueProcess.ResolveColonistPair"))
                {
                    if (!Owner.TryResolveColonistPair(currentTick, out initiator, out receiver))
                        return false;
                }

                using (PerfScope.Measure("RpgPush.QueueProcess.StartGeneration"))
                    Owner.StartGeneration(context, initiator, receiver);
                return true;
            }

            Pawn npcPawn, playerPawn;
            using (PerfScope.Measure("RpgPush.QueueProcess.ResolvePairForFaction"))
            {
                if (!Owner.TryResolvePairForFaction(context.Faction, currentTick, false, false, false, out npcPawn, out playerPawn))
                    return false;
            }

            using (PerfScope.Measure("RpgPush.QueueProcess.StartGeneration"))
                Owner.StartGeneration(context, npcPawn, playerPawn);
            return true;
        }

internal bool TryCreateLowMoodContext(Faction faction, int currentTick, out PawnRpgTriggerContext context)
        {
            context = null;
            Pawn worstMoodNpc = null;
            float worstMood = 1f;
            foreach (Pawn npc in Owner.GetFactionNpcCandidates(faction))
            {
                if (!Owner.TryGetMoodPercent(npc, out float mood) || mood > LowMoodThreshold)
                {
                    continue;
                }

                if (!Owner.HasQualifiedPlayerRelation(npc))
                {
                    continue;
                }

                if (mood < worstMood)
                {
                    worstMood = mood;
                    worstMoodNpc = npc;
                }
            }

            if (worstMoodNpc == null)
            {
                return false;
            }

            context = new PawnRpgTriggerContext
            {
                Faction = faction,
                TriggerType = NpcDialogueTriggerType.Conditional,
                Category = NpcDialogueCategory.Social,
                SourceTag = "low_mood",
                Reason = "low_mood",
                Severity = 1,
                CreatedTick = currentTick,
                Metadata = worstMood.ToString("F3")
            };
            return true;
        }

internal bool TryCreateQuestDeadlineContext(Faction faction, int currentTick, out PawnRpgTriggerContext context)
        {
            context = null;
            if (Find.QuestManager?.QuestsListForReading == null)
            {
                return false;
            }

            Quest quest = Find.QuestManager.QuestsListForReading
                .Where(q => q != null && q.State == QuestState.Ongoing && q.EverAccepted && q.TicksUntilExpiry > 0)
                .Where(q => q.TicksUntilExpiry <= QuestDeadlineWindowTicks && QuestInvolvedFactionsGuard.HasInvolvedFaction(q, faction))
                .OrderBy(q => q.TicksUntilExpiry)
                .FirstOrDefault();
            if (quest == null)
            {
                return false;
            }

            string key = $"{quest.id}:{faction.loadID}";
            if (recentQuestTriggerTicks.TryGetValue(key, out int lastTick) && currentTick - lastTick < QuestTriggerRepeatTicks)
            {
                return false;
            }

            recentQuestTriggerTicks[key] = currentTick;
            context = new PawnRpgTriggerContext
            {
                Faction = faction,
                TriggerType = NpcDialogueTriggerType.Conditional,
                Category = NpcDialogueCategory.DiplomacyTask,
                SourceTag = "quest_deadline",
                Reason = "quest_deadline",
                Severity = quest.TicksUntilExpiry <= TickPerDay / 2 ? 2 : 1,
                CreatedTick = currentTick,
                Metadata = $"{quest.id}|{quest.name}|{quest.TicksUntilExpiry}"
            };
            return true;
        }

internal void CleanupQuestTriggerCache(int currentTick)
        {
            List<string> staleKeys = null;
            foreach (var pair in recentQuestTriggerTicks)
            {
                if (currentTick - pair.Value > QuestDeadlineWindowTicks)
                {
                    staleKeys ??= new List<string>();
                    staleKeys.Add(pair.Key);
                }
            }
            if (staleKeys != null)
            {
                for (int i = 0; i < staleKeys.Count; i++)
                    recentQuestTriggerTicks.Remove(staleKeys[i]);
            }
        }

internal int GetNextAllowedTickForContext(PawnRpgTriggerContext context, int currentTick)
        {
            int nextTick = Owner.GetFactionNpcReadyTick(context?.Faction, currentTick);
            if (!Owner.CanBypassGlobalCooldown(context) && lastColonyDeliveredTick > 0)
            {
                nextTick = Math.Max(nextTick, lastColonyDeliveredTick + ColonyDeliveryCooldownTicks);
            }

            return nextTick;
        }

internal void QueueTrigger(PawnRpgTriggerContext context, int dueTick, int nowTick)
        {
            RelationsSettings settings = RelationsMod.Instance?.InstanceSettings;
            int maxPerFaction = Mathf.Clamp(settings?.NpcQueueMaxPerFaction ?? 3, 1, 10);
            int expireTicks = Mathf.RoundToInt((settings?.NpcQueueExpireHours ?? 12f) * TickPerHour);
            expireTicks = Mathf.Max(expireTicks, TickPerHour);

            int sameFactionCount = 0;
            QueuedPawnRpgTrigger lowestPriority = null;
            int lowestEnqueuedTick = int.MaxValue;
            for (int i = 0; i < queuedTriggers.Count; i++)
            {
                var q = queuedTriggers[i];
                if (q?.faction != context.Faction) continue;
                sameFactionCount++;
                if (q.enqueuedTick < lowestEnqueuedTick)
                {
                    lowestEnqueuedTick = q.enqueuedTick;
                    lowestPriority = q;
                }
            }

            if (sameFactionCount >= maxPerFaction && lowestPriority != null)
            {
                queuedTriggers.Remove(lowestPriority);
            }

            queuedTriggers.Add(QueuedPawnRpgTrigger.FromContext(context, nowTick, dueTick, nowTick + expireTicks));
        }

internal void CleanupExpiredQueue(int currentTick)
        {
            for (int i = queuedTriggers.Count - 1; i >= 0; i--)
            {
                var q = queuedTriggers[i];
                if (q == null || q.faction == null || q.faction.defeated || q.expireTick <= currentTick)
                    queuedTriggers.RemoveAt(i);
            }
        }

internal bool IsFeatureEnabled()
        {
            RelationsSettings settings = RelationsMod.Instance?.InstanceSettings;
            return settings != null && settings.EnablePawnRpgInitiatedDialogue && settings.EnableRPGDialogue;
        }

internal bool IsValidTargetFaction(Faction faction)
        {
            if (faction == null || faction.defeated)
            {
                return false;
            }

            if (faction.IsPlayer || faction == Faction.OfPlayer)
            {
                return true;
            }

            return !(faction.def?.hidden ?? true);
        }

internal void CleanupInvalidState()
        {
            npcPushStates.RemoveAll(s => s == null || s.pawn == null || s.pawn.Destroyed || s.pawn.Dead);
            if (_npcStateByPawn != null)
            {
                var stalePawns = _npcStateByPawn.Keys
                    .Where(p => p == null || p.Destroyed || p.Dead)
                    .ToList();
                foreach (var p in stalePawns)
                    _npcStateByPawn.Remove(p);
            }
            threatStates.RemoveAll(s => s == null || s.faction == null || s.faction.defeated);
            queuedTriggers.RemoveAll(q => q == null || q.faction == null || q.faction.defeated
                || (q.category == NpcDialogueCategory.WarningThreat && !q.bypassCategoryGate));
            proactiveProtagonists ??= new List<PawnRpgProtagonistEntry>();
            proactiveProtagonists.RemoveAll(e => e == null || !e.HasConfiguredIdentifier);
            _cachedProtagonists = null;
        }

internal bool HasConfiguredProtagonists()
        {
            if (proactiveProtagonists == null) return false;
            for (int i = 0; i < proactiveProtagonists.Count; i++)
            {
                if (proactiveProtagonists[i]?.HasConfiguredIdentifier == true)
                    return true;
            }
            return false;
        }

internal List<Pawn> ResolveConfiguredProtagonists()
        {
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            if (_cachedProtagonists != null && _cachedProtagonistsTick == currentTick)
                return _cachedProtagonists;

            if (proactiveProtagonists == null || proactiveProtagonists.Count == 0)
            {
                _cachedProtagonists = new List<Pawn>();
            }
            else
            {
                _cachedProtagonists = proactiveProtagonists
                    .Select(entry => entry?.TryResolvePawn())
                    .Where(pawn => pawn != null)
                    .Distinct()
                    .ToList();
            }
            _cachedProtagonistsTick = currentTick;
            return _cachedProtagonists;
        }

internal bool CanConfigureAsProtagonist(Pawn pawn)
        {
            return pawn != null &&
                   pawn.Faction == Faction.OfPlayer &&
                   !pawn.Destroyed &&
                   !pawn.Dead;
        }

internal static bool IsSamePawn(PawnRpgProtagonistEntry entry, Pawn pawn)
        {
            if (entry == null || pawn == null)
            {
                return false;
            }

            Pawn resolved = entry.TryResolvePawn();
            if (resolved == pawn)
            {
                return true;
            }

            return entry.pawnThingId > 0 && entry.pawnThingId == pawn.thingIDNumber;
        }

internal void LogMissingProtagonists(int currentTick)
        {
            if (currentTick - lastMissingProtagonistLogTick < MissingProtagonistLogIntervalTicks)
            {
                return;
            }

            lastMissingProtagonistLogTick = currentTick;
            Log.Warning("[RimAI.Relations] PawnRPG proactive skipped: protagonist list is empty. Configure protagonists in NPC proactive dialogue settings.");
        }

internal PawnRpgThreatState GetOrCreateThreatState(Faction faction)
        {
            PawnRpgThreatState state = threatStates.FirstOrDefault(s => s?.faction == faction);
            if (state != null)
            {
                return state;
            }

            state = new PawnRpgThreatState { faction = faction };
            threatStates.Add(state);
            return state;
        }

internal float GetRegularTriggerChance(NpcPushFrequencyMode mode)
        {
            return mode switch
            {
                NpcPushFrequencyMode.High => 0.10f,
                NpcPushFrequencyMode.Medium => 0.05f,
                _ => 0f
            };
        }
    }
}
