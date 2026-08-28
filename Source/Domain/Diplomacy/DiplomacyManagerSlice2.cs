using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.Diagnostics;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting.Diplomacy;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
    internal sealed class DiplomacyManagerSlice2 : GameComponent_DiplomacyManagerCollaborator
    {
        internal DiplomacyManagerSlice2(GameComponent_DiplomacyManager owner) : base(owner)
        {
        }

internal void ProcessPeriodicDiplomacySnapshots()
        {
            if (dialogueSessions == null) return;

            // Collect pending snapshots synchronously (only cheap index updates).
            // The expensive traversal (GetLastNegotiatorForSession + I/O) is fully offloaded.
            var pendingSessions = new List<FactionDialogueSession>();
            var pendingLastIndices = new List<int>();
            for (int i = 0; i < dialogueSessions.Count; i++)
            {
                var session = dialogueSessions[i];
                if (session == null || session.faction == null || session.faction.defeated) continue;
                if (session.messages == null || session.messages.Count <= session.lastSummarizedMessageIndex) continue;

                pendingSessions.Add(session);
                pendingLastIndices.Add(session.lastSummarizedMessageIndex);
                session.lastSummarizedMessageIndex = session.messages.Count;
            }

            if (pendingSessions.Count == 0) return;

            var archive = RpgNpcDialogueArchiveManager.Instance;
            LongEventHandler.QueueLongEvent(() =>
            {
                for (int i = 0; i < pendingSessions.Count; i++)
                {
                    var session = pendingSessions[i];
                    if (session?.faction == null || session.messages == null) continue;
                    int lastIndex = pendingLastIndices[i];
                    Pawn negotiator = Owner.GetLastNegotiatorForSession(session);
                    archive.RecordDiplomacySummary(negotiator, session.faction, session.messages, lastIndex);
                }
            }, "RimChat_DiplomacySnapshot", false, null);
        }

internal Pawn GetLastNegotiatorForSession(FactionDialogueSession session)
        {
            if (session?.messages == null) return null;
            for (int i = session.messages.Count - 1; i >= 0; i--)
            {
                var msg = session.messages[i];
                if (msg == null) continue;
                Pawn speaker = msg.ResolveSpeakerPawn();
                if (speaker != null && !speaker.Destroyed && !speaker.Dead)
                {
                    return speaker;
                }
            }
            return null;
        }

public void ProcessDelayedEvents()
        {
            int currentTick = Find.TickManager?.TicksGame ?? -1;
            if (currentTick >= 0 && lastProcessedDelayedEventsTick == currentTick)
            {
                return;
            }

            if (isProcessingDelayedEvents)
            {
                return;
            }

            if (delayedEvents == null)
            {
                delayedEvents = new List<DelayedDiplomacyEvent>();
            }

            isProcessingDelayedEvents = true;
            lastProcessedDelayedEventsTick = currentTick;
            try
            {
                for (int i = delayedEvents.Count - 1; i >= 0; i--)
                {
                    DelayedDiplomacyEvent evt = delayedEvents[i];
                    if (evt == null || evt.Faction == null || evt.Faction.defeated)
                    {
                        delayedEvents.RemoveAt(i);
                        continue;
                    }

                    if (!evt.ShouldExecute())
                    {
                        continue;
                    }

                    bool success = evt.Execute();
                    if (success)
                    {
                        delayedEvents.RemoveAt(i);
                        continue;
                    }

                    bool noRetryPolicy = evt.EventType == DelayedEventType.RaidCallEveryone;
                    if (!noRetryPolicy && evt.CanRetry())
                    {
                        int retryDelay = Rand.Range(1500, 3000);
                        evt.ScheduleRetry(retryDelay);
                        Log.Warning($"[RimAI.Relations] Delayed {evt.EventType} from {evt.Faction?.Name} failed; retry {evt.RetryCount}/{evt.MaxRetryCount} at tick {evt.NextRetryTick}.");
                    }
                    else
                    {
                        string policyNote = noRetryPolicy ? " (no-retry policy)" : string.Empty;
                        Log.Error($"[RimAI.Relations] Delayed {evt.EventType} from {evt.Faction?.Name ?? "null"} failed after {evt.RetryCount} retries and was discarded{policyNote}. ExecuteTick={evt.ExecuteTick}, CurrentTick={currentTick}, Faction.defeated={evt.Faction?.defeated}, Faction.def={evt.Faction?.def?.defName}.");
                        delayedEvents.RemoveAt(i);
                    }
                }
            }
            finally
            {
                Owner.FlushPendingDelayedEvents();
                isProcessingDelayedEvents = false;
            }
        }

public void AddDelayedEvent(DelayedDiplomacyEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            if (delayedEvents == null)
                delayedEvents = new List<DelayedDiplomacyEvent>();

            if (isProcessingDelayedEvents)
            {
                delayedEventsPendingAdd.Add(evt);
            }
            else
            {
                delayedEvents.Add(evt);
            }
            ModuleLog.Message($"[RimAI.Relations] Scheduled delayed {evt.EventType} from {evt.Faction?.Name} at tick {evt.ExecuteTick}");
        }

internal void FlushPendingDelayedEvents()
        {
            if (delayedEventsPendingAdd.Count == 0)
            {
                return;
            }

            delayedEvents.AddRange(delayedEventsPendingAdd);
            delayedEventsPendingAdd.Clear();
        }

internal void DailyReset()
        {
            GameAIInterface.Instance?.DailyReset();
            Owner.OnSocialCircleDailyReset();

            ModuleLog.Message("[RimAI.Relations] Daily reset completed.");
        }

internal void ProcessAIDecisions()
        {
        }

internal int GetPresenceCacheTicks()
        {
            float cacheHours = RelationsMod.Instance?.InstanceSettings?.PresenceCacheHours ?? 8f;
            return Math.Max(0, Mathf.RoundToInt(cacheHours * 2500f));
        }

internal void MigrateLegacyRaidCallEveryoneEvents(int currentTick)
        {
            if (delayedEvents == null || delayedEvents.Count == 0)
            {
                return;
            }

            int windowStartTick = currentTick + (16 * 2500);
            int windowEndTick = currentTick + (30 * 2500);
            foreach (DelayedDiplomacyEvent evt in delayedEvents)
            {
                if (evt == null || evt.EventType != DelayedEventType.RaidCallEveryone)
                {
                    continue;
                }

                bool changed = false;
                Faction evtFaction = evt.Faction;
                bool neutralOrBetter = evtFaction != null && evtFaction.RelationKindWith(Faction.OfPlayer) != FactionRelationKind.Hostile;
                if (neutralOrBetter && evt.CallEveryoneAction != CallEveryoneActionKind.MilitaryAidCustom)
                {
                    evt.CallEveryoneAction = CallEveryoneActionKind.MilitaryAidCustom;
                    changed = true;
                }

                if (evt.ExecuteTick < windowStartTick || evt.ExecuteTick > windowEndTick)
                {
                    evt.ExecuteTick = windowStartTick + Rand.Range(0, 14 * 2500);
                    changed = true;
                }

                if (evt.MaxRetryCount != 0 || evt.NextRetryTick > 0)
                {
                    evt.MaxRetryCount = 0;
                    evt.RetryCount = 0;
                    evt.NextRetryTick = 0;
                    changed = true;
                }

                if (changed)
                {
                    ModuleLog.Message($"[RimAI.Relations] Migrated legacy RaidCallEveryone event from {evtFaction?.Name ?? "Unknown"}: executeTick={evt.ExecuteTick}, action={evt.CallEveryoneAction}, maxRetry={evt.MaxRetryCount}");
                }
            }
        }

internal FactionPresenceStatus EvaluateScheduledPresence(Faction faction, int currentTick, out string reason)
        {
            int currentHour = Owner.GetCurrentHourOfDay();
            int dayIndex = currentTick / 60000;
            int cacheKey = dayIndex * 100 + currentHour;

            if (presenceEvalCacheKey.TryGetValue(faction, out int cachedKey) && cachedKey == cacheKey &&
                presenceEvalCacheResult.TryGetValue(faction, out FactionPresenceStatus cachedStatus))
            {
                reason = "schedule_cached";
                return cachedStatus;
            }

            reason = "schedule";
            TechLevel techLevel = faction?.def?.techLevel ?? TechLevel.Undefined;
            Owner.GetPresenceScheduleForTechLevel(techLevel, out int startHour, out int durationHours);
            int scheduleOffset = Owner.GetScheduleOffsetHours(faction, dayIndex);
            startHour = Owner.ModHour(startHour + scheduleOffset);
            bool isOnline = Owner.IsHourWithinWindow(currentHour, startHour, durationHours);

            if (!isOnline)
            {
                float offWindowChance = Owner.GetOffWindowOnlineChance(techLevel);
                if (offWindowChance > 0f &&
                    Owner.GetDeterministicRoll(faction, dayIndex, currentHour + 97) < offWindowChance)
                {
                    isOnline = true;
                    reason = "off_window_chance";
                }
            }

            if (isOnline && Owner.IsNightBiasEnabled() && Owner.IsInNightWindow(currentHour))
            {
                float offlineBias = Mathf.Clamp01(RelationsMod.Instance?.InstanceSettings?.PresenceNightOfflineBias ?? 0.65f);
                if (Owner.GetDeterministicRoll(faction, dayIndex, currentHour) < offlineBias)
                {
                    isOnline = false;
                    reason = "night_bias";
                }
            }

            FactionPresenceStatus result = isOnline ? FactionPresenceStatus.Online : FactionPresenceStatus.Offline;
            presenceEvalCacheKey[faction] = cacheKey;
            presenceEvalCacheResult[faction] = result;
            return result;
        }

internal int GetCurrentHourOfDay()
        {
            var map = Find.CurrentMap ?? Find.AnyPlayerHomeMap;
            if (map != null)
            {
                return GenLocalDate.HourOfDay(map);
            }

            int ticksAbs = Find.TickManager?.TicksAbs ?? 0;
            return Mathf.FloorToInt((ticksAbs / 2500f) % 24f);
        }

internal void GetPresenceScheduleForTechLevel(TechLevel techLevel, out int startHour, out int durationHours)
        {
            var settings = RelationsMod.Instance?.InstanceSettings;
            bool useAdvanced = settings?.PresenceUseAdvancedProfiles ?? false;
            if (!useAdvanced)
            {
                switch (techLevel)
                {
                    case TechLevel.Neolithic:
                        startHour = 8;
                        durationHours = 8;
                        return;
                    case TechLevel.Medieval:
                        startHour = 8;
                        durationHours = 10;
                        return;
                    case TechLevel.Industrial:
                        startHour = 7;
                        durationHours = 14;
                        return;
                    case TechLevel.Spacer:
                        startHour = 6;
                        durationHours = 18;
                        return;
                    case TechLevel.Ultra:
                    case TechLevel.Archotech:
                        startHour = 4;
                        durationHours = 20;
                        return;
                    default:
                        startHour = 7;
                        durationHours = 12;
                        return;
                }
            }

            switch (techLevel)
            {
                case TechLevel.Neolithic:
                    startHour = settings?.PresenceOnlineStart_Neolithic ?? 10;
                    durationHours = settings?.PresenceOnlineDuration_Neolithic ?? 6;
                    break;
                case TechLevel.Medieval:
                    startHour = settings?.PresenceOnlineStart_Medieval ?? 9;
                    durationHours = settings?.PresenceOnlineDuration_Medieval ?? 8;
                    break;
                case TechLevel.Industrial:
                    startHour = settings?.PresenceOnlineStart_Industrial ?? 8;
                    durationHours = settings?.PresenceOnlineDuration_Industrial ?? 12;
                    break;
                case TechLevel.Spacer:
                    startHour = settings?.PresenceOnlineStart_Spacer ?? 7;
                    durationHours = settings?.PresenceOnlineDuration_Spacer ?? 16;
                    break;
                case TechLevel.Ultra:
                    startHour = settings?.PresenceOnlineStart_Ultra ?? 6;
                    durationHours = settings?.PresenceOnlineDuration_Ultra ?? 18;
                    break;
                case TechLevel.Archotech:
                    startHour = settings?.PresenceOnlineStart_Archotech ?? 6;
                    durationHours = settings?.PresenceOnlineDuration_Archotech ?? 18;
                    break;
                default:
                    startHour = settings?.PresenceOnlineStart_Default ?? 8;
                    durationHours = settings?.PresenceOnlineDuration_Default ?? 10;
                    break;
            }

            startHour = Mathf.Clamp(startHour, 0, 23);
            durationHours = Mathf.Clamp(durationHours, 1, 24);
        }

internal bool IsHourWithinWindow(int hour, int startHour, int durationHours)
        {
            hour = Mathf.Clamp(hour, 0, 23);
            startHour = Mathf.Clamp(startHour, 0, 23);
            durationHours = Mathf.Clamp(durationHours, 1, 24);
            if (durationHours >= 24) return true;

            int endHour = (startHour + durationHours) % 24;
            if (startHour < endHour)
            {
                return hour >= startHour && hour < endHour;
            }

            return hour >= startHour || hour < endHour;
        }

internal bool IsInNightWindow(int hour)
        {
            var settings = RelationsMod.Instance?.InstanceSettings;
            int nightStart = Mathf.Clamp(settings?.PresenceNightStartHour ?? 22, 0, 23);
            int nightEnd = Mathf.Clamp(settings?.PresenceNightEndHour ?? 6, 0, 23);

            if (nightStart == nightEnd)
            {
                return true;
            }

            if (nightStart < nightEnd)
            {
                return hour >= nightStart && hour < nightEnd;
            }

            return hour >= nightStart || hour < nightEnd;
        }

internal float GetDeterministicRoll(Faction faction, int dayIndex, int hour)
        {
            int seed = Gen.HashCombineInt(faction?.loadID ?? 0, dayIndex);
            seed = Gen.HashCombineInt(seed, hour);
            Rand.PushState(seed);
            float value = Rand.Value;
            Rand.PopState();
            return value;
        }

internal int GetScheduleOffsetHours(Faction faction, int dayIndex)
        {
            int seed = Gen.HashCombineInt(faction?.loadID ?? 0, dayIndex);
            Rand.PushState(seed);
            int offset = Rand.RangeInclusive(-2, 2);
            Rand.PopState();
            return offset;
        }

internal int ModHour(int hour)
        {
            hour %= 24;
            if (hour < 0)
            {
                hour += 24;
            }
            return hour;
        }

internal float GetOffWindowOnlineChance(TechLevel techLevel)
        {
            switch (techLevel)
            {
                case TechLevel.Neolithic:
                    return 0.05f;
                case TechLevel.Medieval:
                    return 0.08f;
                case TechLevel.Industrial:
                    return 0.12f;
                case TechLevel.Spacer:
                    return 0.18f;
                case TechLevel.Ultra:
                case TechLevel.Archotech:
                    return 0.25f;
                default:
                    return 0.10f;
            }
        }

internal static string GetTempPeaceKey(Faction a, Faction b)
        {
            var ids = new[] { a.loadID.ToString(), b.loadID.ToString() };
            Array.Sort(ids);
            return string.Join(":", ids);
        }

public void ApplyTempCrossFactionPeace(Faction a, Faction b, int untilTick)
        {
            if (a == null || b == null || a == b) return;
            FactionRelationKind currentKind = a.RelationKindWith(b);
            if (currentKind != FactionRelationKind.Hostile) return;

            string key = GameComponent_DiplomacyManager.GetTempPeaceKey(a, b);
            if (!tempFactionRelations.originalRelations.ContainsKey(key))
                tempFactionRelations.originalRelations[key] = currentKind;

            a.SetRelationDirect(b, FactionRelationKind.Neutral);
            tempFactionRelations.restoreAtTick = Math.Max(tempFactionRelations.restoreAtTick, untilTick);
        }

public void TryRestoreTempFactionRelations(int currentTick)
        {
            if (tempFactionRelations?.originalRelations == null || tempFactionRelations.originalRelations.Count == 0) return;
            if (tempFactionRelations.restoreAtTick <= 0) return;
            if (currentTick < tempFactionRelations.restoreAtTick) return;

            ModuleLog.Message($"[RimAI.Relations] Restoring {tempFactionRelations.originalRelations.Count} temporary faction peace overrides at tick {currentTick}");
            foreach (var kv in tempFactionRelations.originalRelations)
            {
                string[] ids = kv.Key.Split(':');
                if (ids.Length != 2) continue;
                Faction fa = Find.FactionManager?.AllFactions?.FirstOrDefault(f => f?.loadID.ToString() == ids[0]);
                Faction fb = Find.FactionManager?.AllFactions?.FirstOrDefault(f => f?.loadID.ToString() == ids[1]);
                if (fa == null || fb == null || fa.defeated || fb.defeated) continue;
                fa.SetRelationDirect(fb, kv.Value);
            }
            tempFactionRelations.originalRelations.Clear();
            tempFactionRelations.restoreAtTick = 0;
        }
    }
}
