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
    internal sealed class NpcDialoguePushSlice4 : GameComponent_NpcDialoguePushManagerCollaborator
    {
        internal NpcDialoguePushSlice4(GameComponent_NpcDialoguePushManager owner) : base(owner)
        {
        }

internal void SyncCandidateCacheFromRecentSessions(int currentTick)
        {
            GameComponent_DiplomacyManager manager = GameComponent_DiplomacyManager.Instance;
            if (manager == null)
            {
                return;
            }

            foreach (Faction faction in manager.GetFactionsWithDialogue())
            {
                FactionDialogueSession session = manager.GetSession(faction);
                if (session == null || currentTick - session.lastInteractionTick > RecentInteractionWindowTicks)
                {
                    continue;
                }

                Owner.MarkFactionCandidate(faction, session.lastInteractionTick);
            }
        }

internal bool IsCandidateStillActive(Faction faction, int currentTick)
        {
            if (!Owner.IsValidTargetFaction(faction))
            {
                return false;
            }

            if (Owner.IsFactionPending(faction) || factionsInQueue.Contains(faction))
            {
                return true;
            }

            if (candidateTouchTicks.TryGetValue(faction, out int touchedTick) &&
                currentTick - touchedTick <= RecentInteractionWindowTicks)
            {
                return true;
            }

            if (factionPushStatesByFaction.TryGetValue(faction, out FactionNpcPushState state) &&
                currentTick - state.lastInteractionTick <= RecentInteractionWindowTicks)
            {
                Owner.MarkFactionCandidate(faction, state.lastInteractionTick);
                return true;
            }

            FactionDialogueSession session = GameComponent_DiplomacyManager.Instance?.GetSession(faction);
            if (session != null && currentTick - session.lastInteractionTick <= RecentInteractionWindowTicks)
            {
                Owner.MarkFactionCandidate(faction, session.lastInteractionTick);
                return true;
            }

            return false;
        }

internal void MarkFactionCandidate(Faction faction, int tick)
        {
            if (!Owner.IsValidTargetFaction(faction))
            {
                return;
            }

            activeCandidateFactions.Add(faction);
            if (!candidateTouchTicks.TryGetValue(faction, out int existing) || tick > existing)
            {
                candidateTouchTicks[faction] = tick;
            }
        }

internal void RebuildCandidateCache()
        {
            activeCandidateFactions.Clear();
            candidateTouchTicks.Clear();

            int currentTick = Find.TickManager?.TicksGame ?? 0;
            foreach (FactionNpcPushState state in factionPushStates)
            {
                if (state?.faction == null)
                {
                    continue;
                }

                Owner.MarkFactionCandidate(state.faction, state.lastInteractionTick);
            }

            foreach (QueuedNpcDialogueTrigger queued in queuedTriggers)
            {
                if (queued?.faction == null)
                {
                    continue;
                }

                Owner.MarkFactionCandidate(queued.faction, currentTick);
            }

            Owner.SyncCandidateCacheFromRecentSessions(currentTick);
        }

internal int GetGlobalDeliveryCooldownTicks()
        {
            RelationsSettings settings = RelationsMod.Instance?.InstanceSettings;
            float hours = settings?.NpcGlobalDeliveryCooldownHours ?? 6f;
            return Mathf.Max(TickPerHour, Mathf.RoundToInt(hours * TickPerHour));
        }

internal int GetFactionCooldownMinTicks()
        {
            RelationsSettings settings = RelationsMod.Instance?.InstanceSettings;
            int days = settings?.NpcFactionCooldownMinDays ?? 3;
            return Mathf.Max(TickPerDay, days * TickPerDay);
        }

internal int GetFactionCooldownMaxTicks()
        {
            RelationsSettings settings = RelationsMod.Instance?.InstanceSettings;
            int minDays = settings?.NpcFactionCooldownMinDays ?? 3;
            int maxDays = settings?.NpcFactionCooldownMaxDays ?? 7;
            int resolved = Math.Max(minDays, maxDays);
            return Mathf.Max(TickPerDay, resolved * TickPerDay);
        }

internal void LogThrottleDebug(string message)
        {
            RelationsSettings settings = RelationsMod.Instance?.InstanceSettings;
            if (settings?.EnableNpcPushThrottleDebugLog != true)
            {
                return;
            }

            ModuleLog.Message($"[RimAI.Relations][NpcPushThrottle] {message}");
        }

internal bool TryDeliverFallbackMessage(NpcDialogueTriggerContext context)
        {
            if (context == null || !context.BypassRateLimit || string.IsNullOrWhiteSpace(context.Reason))
            {
                return false;
            }

            Owner.DeliverMessage(context, context.Reason.Trim());
            return true;
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
