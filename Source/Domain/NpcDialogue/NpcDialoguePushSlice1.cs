using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Settings;
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
    internal sealed class NpcDialoguePushSlice1 : GameComponent_NpcDialoguePushManagerCollaborator
    {
        internal NpcDialoguePushSlice1(GameComponent_NpcDialoguePushManager owner) : base(owner)
        {
        }

public void RegisterLowQualityTradeTrigger(Faction faction, int lowQualityCount, QualityCategory worstQuality)
        {
            if (!Owner.IsValidTargetFaction(faction) || lowQualityCount <= 0)
            {
                return;
            }

            int severity = worstQuality <= QualityCategory.Awful ? 3 : 2;
            string reason = $"low_quality_trade:{lowQualityCount}:{worstQuality}";
            Owner.EnqueueIncoming(new NpcDialogueTriggerContext
            {
                Faction = faction,
                TriggerType = NpcDialogueTriggerType.Causal,
                Category = NpcDialogueCategory.WarningThreat,
                SourceTag = "trade_quality",
                Severity = severity,
                Reason = reason,
                CreatedTick = Find.TickManager?.TicksGame ?? 0
            });
        }

public void RegisterGoodwillShiftTrigger(Faction faction, int goodwillDelta, string reason, bool likelyHostile)
        {
            if (!Owner.IsValidTargetFaction(faction) || !RelationsProactiveEmitPolicy.AllowCausalGoodwillShift(goodwillDelta))
            {
                return;
            }

            NpcDialogueCategory category = goodwillDelta < 0
                ? NpcDialogueCategory.WarningThreat
                : NpcDialogueCategory.DiplomacyTask;
            int severity = likelyHostile ? 3 : (goodwillDelta < 0 ? 2 : 1);

            Owner.EnqueueIncoming(new NpcDialogueTriggerContext
            {
                Faction = faction,
                TriggerType = NpcDialogueTriggerType.Causal,
                Category = category,
                SourceTag = "goodwill_shift",
                Severity = severity,
                Reason = reason ?? string.Empty,
                CreatedTick = Find.TickManager?.TicksGame ?? 0,
                GoodwillDelta = goodwillDelta
            });

            if (goodwillDelta < 0)
            {
                Owner.AccumulateGoodwillLoss(faction, goodwillDelta);
            }
        }

public void RegisterCustomTrigger(NpcDialogueTriggerContext context)
        {
            if (context == null || context.Faction == null)
            {
                return;
            }
            Owner.EnqueueIncoming(context);
        }

internal void AccumulateGoodwillLoss(Faction faction, int goodwillDelta)
        {
            if (faction == null)
            {
                return;
            }

            int currentTick = Find.TickManager?.TicksGame ?? 0;
            FactionNpcPushState state = Owner.GetOrCreateState(faction);

            if (currentTick - state.lastGoodwillLossRecordTick > TickPerDay)
            {
                state.accumulatedGoodwillLossLastDay = 0;
            }

            state.accumulatedGoodwillLossLastDay += Math.Abs(goodwillDelta);
            state.lastGoodwillLossRecordTick = currentTick;
        }

public bool DebugForceRandomProactiveDialogue()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.FactionManager == null || Find.TickManager == null)
            {
                return false;
            }
            if (!AIChatServiceAsync.Instance.IsConfigured())
            {
                return false;
            }

            List<Faction> candidates = Find.FactionManager.AllFactions
                .Where(Owner.IsValidTargetFaction)
                .ToList();
            if (candidates.Count == 0)
            {
                return false;
            }

            Faction faction = candidates.RandomElement();
            var category = (NpcDialogueCategory)Rand.RangeInclusive(0, 1);
            int severity = category == NpcDialogueCategory.WarningThreat ? Rand.RangeInclusive(1, 3) : 1;
            var context = new NpcDialogueTriggerContext
            {
                Faction = faction,
                TriggerType = NpcDialogueTriggerType.Causal,
                Category = category,
                SourceTag = "debug_force",
                Reason = "manual_debug_trigger",
                Severity = severity,
                CreatedTick = Find.TickManager.TicksGame,
                BypassRateLimit = true,
                BypassCategoryGate = true,
                BypassPlayerBusyGate = true
            };

            Owner.GetOrCreateState(faction).lastInteractionTick = context.CreatedTick;
            Owner.HandleTriggerContext(context, context.CreatedTick);
            return true;
        }

internal void EnqueueIncoming(NpcDialogueTriggerContext context)
        {
            if (context == null || context.Faction == null)
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
                NpcDialogueTriggerContext context = incomingTriggers.Dequeue();
                Owner.HandleTriggerContext(context, currentTick);
            }
        }

internal void HandleTriggerContext(NpcDialogueTriggerContext context, int currentTick)
        {
            if (context == null || !Owner.IsValidTargetFaction(context.Faction))
            {
                return;
            }
            if (context.Category == NpcDialogueCategory.WarningThreat && !context.BypassCategoryGate)
            {
                return;
            }

            FactionNpcPushState state = Owner.GetOrCreateState(context.Faction);
            state.lastInteractionTick = currentTick;
            Owner.MarkFactionCandidate(context.Faction, currentTick);
            if (context.GoodwillDelta <= -10f)
            {
                state.lastNegativeSpikeTick = currentTick;
            }

            int dueTick = currentTick;
            if (context.TriggerType == NpcDialogueTriggerType.Causal)
            {
                dueTick += Rand.RangeInclusive(CausalMinDelayTicks, CausalMaxDelayTicks);
            }

            if (Owner.IsFactionPending(context.Faction))
            {
                dueTick = Math.Max(dueTick, currentTick + 300);
            }

            if (Owner.ShouldRespectCooldown(context, currentTick))
            {
                dueTick = Math.Max(dueTick, state.nextAllowedTick);
                Owner.LogThrottleDebug($"faction_cooldown gate: faction={context.Faction?.Name}, due={dueTick}, now={currentTick}");
            }

            if (!context.BypassRateLimit)
            {
                int globalNextAllowedTick = Owner.GetGlobalNextAllowedTick(currentTick);
                dueTick = Math.Max(dueTick, globalNextAllowedTick);
                if (globalNextAllowedTick > currentTick)
                {
                    Owner.LogThrottleDebug($"global_cooldown gate: faction={context.Faction?.Name}, due={globalNextAllowedTick}, now={currentTick}");
                }

                if (Owner.IsGlobalWindowLimitReached(currentTick))
                {
                    int windowNextTick = Owner.GetGlobalWindowNextAvailableTick(currentTick);
                    dueTick = Math.Max(dueTick, windowNextTick);
                    Owner.LogThrottleDebug($"global_window gate: faction={context.Faction?.Name}, due={windowNextTick}, now={currentTick}");
                }
            }

            if (Owner.IsFactionWindowFull(context.Faction, currentTick))
            {
                dueTick = Math.Max(dueTick, currentTick + FactionWindowTicks / FactionWindowMaxMessages);
                Owner.LogThrottleDebug($"faction_window gate: faction={context.Faction?.Name}");
            }

            int reinitiateRemainingTicks = context.BypassRateLimit
                ? 0
                : Owner.GetReinitiateCooldownRemainingTicks(context.Faction, currentTick);
            if (reinitiateRemainingTicks > 0)
            {
                dueTick = Math.Max(dueTick, currentTick + reinitiateRemainingTicks);
            }

            bool bypassBusyGate = context.BypassRateLimit || context.BypassPlayerBusyGate;
            if ((!bypassBusyGate && Owner.IsPlayerBusy()) || Owner.IsFactionUnavailable(context.Faction))
            {
                dueTick = Math.Max(dueTick, currentTick + 300);
            }

            if (dueTick <= currentTick)
            {
                Owner.StartGeneration(context);
                return;
            }

            Owner.QueueTrigger(context, dueTick, currentTick);
        }

internal void ProcessQueuedTriggers(int currentTick)
        {
            Owner.CleanupExpiredQueue(currentTick);

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
                var item = queuedTriggers[i];
                if (item == null || item.dueTick > currentTick) continue;

                if (!Owner.IsValidTargetFaction(item.faction))
                {
                    queuedTriggers.RemoveAt(i);
                    factionsInQueue.Remove(item.faction);
                    continue;
                }

                NpcDialogueTriggerContext context = item.ToContext();
                if (Owner.IsFactionPending(context.Faction))
                {
                    continue;
                }

                bool bypassBusyGate = context.BypassRateLimit || context.BypassPlayerBusyGate;
                if ((!bypassBusyGate && Owner.IsPlayerBusy()) || Owner.IsFactionUnavailable(context.Faction))
                {
                    int busyDelayTicks = 600;
                    item.dueTick = currentTick + busyDelayTicks;
                    int extendBy = busyDelayTicks + TickPerHour;
                    item.expireTick = Math.Max(item.expireTick, currentTick + extendBy);
                    Owner.LogThrottleDebug($"queue busy defer: faction={context.Faction?.Name}, newDue={item.dueTick}, newExpire={item.expireTick}");
                    continue;
                }

                if (Owner.ShouldRespectCooldown(context, currentTick))
                {
                    FactionNpcPushState state = Owner.GetOrCreateState(context.Faction);
                    item.dueTick = Math.Max(item.dueTick, state.nextAllowedTick);
                    Owner.LogThrottleDebug($"queue faction_cooldown gate: faction={context.Faction?.Name}, due={item.dueTick}, now={currentTick}");
                    continue;
                }

                if (!context.BypassRateLimit)
                {
                    int globalNextAllowedTick = Owner.GetGlobalNextAllowedTick(currentTick);
                    if (globalNextAllowedTick > currentTick)
                    {
                        item.dueTick = Math.Max(item.dueTick, globalNextAllowedTick);
                        Owner.LogThrottleDebug($"queue global_cooldown gate: faction={context.Faction?.Name}, due={item.dueTick}, now={currentTick}");
                        continue;
                    }

                    if (Owner.IsGlobalWindowLimitReached(currentTick))
                    {
                        int windowNextTick = Owner.GetGlobalWindowNextAvailableTick(currentTick);
                        item.dueTick = Math.Max(item.dueTick, windowNextTick);
                        Owner.LogThrottleDebug($"queue global_window gate: faction={context.Faction?.Name}, due={item.dueTick}, now={currentTick}");
                        continue;
                    }
                }

                int reinitiateRemainingTicks = context.BypassRateLimit
                    ? 0
                    : Owner.GetReinitiateCooldownRemainingTicks(context.Faction, currentTick);
                if (reinitiateRemainingTicks > 0)
                {
                    item.dueTick = Math.Max(item.dueTick, currentTick + reinitiateRemainingTicks);
                    continue;
                }

                queuedTriggers.RemoveAt(i);
                factionsInQueue.Remove(item.faction);
                Owner.StartGeneration(context);
                processed++;
            }
        }

internal void EvaluateRegularTriggers(int currentTick)
        {
            RelationsSettings settings = RelationsMod.Instance?.InstanceSettings;
            if (!RelationsProactiveEmitPolicy.AllowRegularSweep(settings != null && settings.EnableNpcInitiatedDialogue))
            {
                return;
            }

            float chance = Owner.GetRegularTriggerChance(settings.NpcPushFrequencyMode);
            List<Faction> candidates = Owner.GetActiveCandidateFactions(currentTick);
            foreach (Faction faction in candidates)
            {
                bool chancePassed = Rand.Value <= chance;
                if (!RelationsProactiveEmitPolicy.ShouldEmit(
                    true,
                    Owner.IsFactionPending(faction),
                    cooldownBlocked: false,
                    chancePassed))
                {
                    continue;
                }

                var context = Owner.BuildRegularTrigger(faction, currentTick);
                if (context == null || Owner.ShouldRespectCooldown(context, currentTick))
                {
                    continue;
                }

                Owner.HandleTriggerContext(context, currentTick);
            }
        }

internal NpcDialogueTriggerContext BuildRegularTrigger(Faction faction, int currentTick)
        {
            if (!Owner.IsValidTargetFaction(faction))
            {
                return null;
            }

            int goodwill = faction.PlayerGoodwill;
            NpcDialogueCategory category;
            NpcDialogueTriggerType triggerType;
            int severity = 1;
            string reason = "regular_check";

            var kind = RelationsProactiveEmitPolicy.ClassifyRegular(goodwill);
            if (kind == RelationsProactiveKind.Skip)
            {
                return null;
            }
            else if (kind == RelationsProactiveKind.FriendlyDiplomacy)
            {
                category = NpcDialogueCategory.DiplomacyTask;
                triggerType = NpcDialogueTriggerType.Conditional;
                reason = "friendly_relationship";
            }
            else
            {
                category = NpcDialogueCategory.Social;
                triggerType = NpcDialogueTriggerType.Ambient;
                reason = "ambient_social";
            }

            return new NpcDialogueTriggerContext
            {
                Faction = faction,
                TriggerType = triggerType,
                Category = category,
                Severity = severity,
                Reason = reason,
                SourceTag = "regular",
                CreatedTick = currentTick
            };
        }

internal void StartGeneration(NpcDialogueTriggerContext context)
        {
            if (context == null || !Owner.IsValidTargetFaction(context.Faction))
            {
                return;
            }

            if (!AIChatServiceAsync.Instance.IsConfigured())
            {
                Log.Warning($"[RimAI.Relations] Proactive push dropped (AI not configured): {context.Faction.Name}");
                return;
            }

            if (!Owner.TryGetPromptRuntimeSnapshotOrDefer(context, out DiplomacyPromptRuntimeSnapshot runtimeSnapshot))
            {
                return;
            }

            // Defer prompt building to a coroutine to avoid blocking GameComponentTick
            AIChatServiceAsync.Instance.StartCoroutine(Owner.BuildAndSendRoutine(context, runtimeSnapshot));
        }
    }
}
