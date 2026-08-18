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

using PendingGenerationContext = Ustas.RimAI.Communication.Relations.NpcDialogue.GameComponent_NpcDialoguePushManager.PendingGenerationContext;

namespace Ustas.RimAI.Communication.Relations.NpcDialogue
{
    internal sealed class NpcDialoguePushSlice2 : GameComponent_NpcDialoguePushManagerCollaborator
    {
        internal NpcDialoguePushSlice2(GameComponent_NpcDialoguePushManager owner) : base(owner)
        {
        }

internal IEnumerator BuildAndSendRoutine(NpcDialogueTriggerContext context, DiplomacyPromptRuntimeSnapshot runtimeSnapshot)
        {
            yield return null; // Defer to next frame

            if (context == null || !Owner.IsValidTargetFaction(context.Faction))
                yield break;

            if (!AIChatServiceAsync.Instance.IsConfigured())
                yield break;

            // Use cached system prompt when available; snapshot revisions ensure freshness
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            List<string> sceneTags = Owner.BuildProactiveSceneTags(context?.Category ?? NpcDialogueCategory.Social);
            string cacheKey = $"{context.Faction.loadID}:{string.Join(",", sceneTags)}:r{currentTick / 1500}";
            string basePrompt;

            if (_systemPromptCache.TryGetValue(cacheKey, out var entry) &&
                currentTick - entry.builtTick < SystemPromptCacheTtlTicks)
            {
                basePrompt = entry.prompt;
            }
            else
            {
                PromptPersistenceService.Instance.Initialize();
                basePrompt = PromptPersistenceService.Instance.BuildFullSystemPrompt(
                    context.Faction,
                    PromptPersistenceService.Instance.LoadConfig(),
                    true,
                    sceneTags,
                    runtimeSnapshot);
                _systemPromptCache[cacheKey] = (currentTick, basePrompt);
            }

            List<ChatMessageData> messages = Owner.BuildGenerationMessagesWithPrompt(context, runtimeSnapshot, basePrompt, sceneTags);

            string requestId = string.Empty;
            requestId = AIChatServiceAsync.Instance.SendChatRequestAsync(
                messages,
                onSuccess: response => Owner.OnGenerationSuccess(requestId, response),
                onError: error => Owner.OnGenerationError(requestId, error),
                usageChannel: DialogueUsageChannel.Diplomacy,
                debugSource: AIRequestDebugSource.NpcPush);

            if (string.IsNullOrEmpty(requestId))
                yield break;

            pendingRequests[requestId] = new PendingGenerationContext
            {
                Context = context,
                Messages = messages,
                Attempt = 1
            };
            factionsWithPendingRequests.Add(context.Faction);
        }

internal void OnGenerationSuccess(string requestId, string response)
        {
            if (string.IsNullOrEmpty(requestId) || !pendingRequests.TryGetValue(requestId, out PendingGenerationContext pending))
            {
                return;
            }

            pendingRequests.Remove(requestId);
            Owner.UpdatePendingFactionIndex(pending.Context?.Faction);
            string message = Owner.SanitizeModelOutput(response);
            if (string.IsNullOrWhiteSpace(message))
            {
                if (Owner.TryDeliverFallbackMessage(pending.Context))
                {
                    return;
                }

                Log.Warning("[RimAI.Relations] Proactive push generation empty after sanitize.");
                return;
            }

            Owner.DeliverMessage(pending.Context, message);
        }

internal void OnGenerationError(string requestId, string error)
        {
            if (string.IsNullOrEmpty(requestId) || !pendingRequests.TryGetValue(requestId, out PendingGenerationContext pending))
            {
                return;
            }

            pendingRequests.Remove(requestId);
            Owner.UpdatePendingFactionIndex(pending.Context?.Faction);
            if (pending.Attempt < 2 && AIChatServiceAsync.Instance.IsConfigured())
            {
                Owner.RetryGeneration(pending);
                return;
            }

            if (Owner.TryDeliverFallbackMessage(pending.Context))
            {
                return;
            }

            Log.Warning($"[RimAI.Relations] Proactive push dropped after retry: {error}");
        }

internal void RetryGeneration(PendingGenerationContext pending)
        {
            string retryId = string.Empty;
            retryId = AIChatServiceAsync.Instance.SendChatRequestAsync(
                pending.Messages,
                onSuccess: response => Owner.OnGenerationSuccess(retryId, response),
                onError: error => Owner.OnGenerationError(retryId, error),
                usageChannel: DialogueUsageChannel.Diplomacy,
                debugSource: AIRequestDebugSource.NpcPush);

            if (string.IsNullOrEmpty(retryId))
            {
                return;
            }

            pendingRequests[retryId] = new PendingGenerationContext
            {
                Context = pending.Context,
                Messages = pending.Messages,
                Attempt = pending.Attempt + 1
            };
            factionsWithPendingRequests.Add(pending.Context.Faction);
        }

internal void UpdatePendingFactionIndex(Faction faction)
        {
            if (faction == null)
            {
                return;
            }

            foreach (var pair in pendingRequests)
            {
                if (pair.Value?.Context?.Faction == faction)
                {
                    return;
                }
            }

            factionsWithPendingRequests.Remove(faction);
        }

internal bool TryGetPromptRuntimeSnapshotOrDefer(
            NpcDialogueTriggerContext context,
            out DiplomacyPromptRuntimeSnapshot snapshot)
        {
            snapshot = null;
            Faction faction = context?.Faction;
            if (!Owner.IsValidTargetFaction(faction))
            {
                return false;
            }

            IDiplomacyPromptSnapshotCache cache = DiplomacyPromptSnapshotCache.Instance;
            cache.RequestWarmup(faction, "npc_push_generation");
            if (cache.TryGetSnapshot(faction, out snapshot))
            {
                return true;
            }

            int currentTick = Find.TickManager?.TicksGame ?? 0;
            if (currentTick > 0)
            {
                Owner.QueueTrigger(context, currentTick + SnapshotRetryDelayTicks, currentTick);
            }
            return false;
        }

internal void DeliverMessage(NpcDialogueTriggerContext context, string text)
        {
            if (context == null || !Owner.IsValidTargetFaction(context.Faction))
            {
                return;
            }

            GameComponent_DiplomacyManager.Instance?.ForcePresenceOnlineForNpcInitiated(context.Faction);

            Owner.AddMessageToSession(context.Faction, text);
            if (!ChoiceLetter_NpcInitiatedDialogue.IsDialogueAlreadyOpen(context.Faction))
            {
                Owner.SendProactiveLetter(context, text);
            }

            int currentTick = Find.TickManager?.TicksGame ?? 0;
            FactionNpcPushState state = Owner.GetOrCreateState(context.Faction);
            state.lastPushTick = currentTick;
            state.lastInteractionTick = currentTick;
            Owner.MarkFactionCandidate(context.Faction, currentTick);
            Owner.RecordFactionDelivery(context.Faction, currentTick);
            if (!context.BypassRateLimit)
            {
                state.nextAllowedTick = currentTick + Rand.RangeInclusive(Owner.GetFactionCooldownMinTicks(), Owner.GetFactionCooldownMaxTicks());
                lastGlobalDeliveredTick = currentTick;
                globalDeliveryTicks.Add(currentTick);
                if (currentTick < globalDeliveryOldestInWindow)
                    globalDeliveryOldestInWindow = currentTick;
            }
        }

internal void AddMessageToSession(Faction faction, string text)
        {
            var diplomacyManager = GameComponent_DiplomacyManager.Instance;
            if (diplomacyManager == null || faction == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string sender = faction.leader?.Name?.ToStringShort ?? faction.Name ?? "Unknown";
            diplomacyManager.HandleInboundFactionMessage(
                faction,
                sender,
                text,
                DialogueMessageType.Normal,
                faction.leader,
                markUnread: true,
                forcePresenceOnline: true);
        }

internal void SendProactiveLetter(NpcDialogueTriggerContext context, string text)
        {
            TaggedString title = Owner.GetLetterTitle(context);
            LetterDef def = Owner.GetLetterDef(context);
            var letter = new ChoiceLetter_NpcInitiatedDialogue();
            letter.AssignLoadID();
            letter.Setup(context.Faction, title, text, def);
            Find.LetterStack.ReceiveLetter(letter, string.Empty, 0, true);
        }

internal TaggedString GetLetterTitle(NpcDialogueTriggerContext context)
        {
            string key = context.Category switch
            {
                NpcDialogueCategory.DiplomacyTask => "RimChat_NpcPush_TitleTask",
                NpcDialogueCategory.WarningThreat => "RimChat_NpcPush_TitleWarning",
                _ => "RimChat_NpcPush_TitleSocial"
            };
            return key.Translate(context.Faction?.Name ?? "Unknown");
        }

internal LetterDef GetLetterDef(NpcDialogueTriggerContext context)
        {
            if (context.Category == NpcDialogueCategory.WarningThreat)
            {
                return context.Severity >= 3 ? LetterDefOf.ThreatBig : LetterDefOf.ThreatSmall;
            }

            return context.Category == NpcDialogueCategory.DiplomacyTask
                ? LetterDefOf.PositiveEvent
                : LetterDefOf.NeutralEvent;
        }

internal List<ChatMessageData> BuildGenerationMessages(
            NpcDialogueTriggerContext context,
            DiplomacyPromptRuntimeSnapshot runtimeSnapshot)
        {
            var messages = new List<ChatMessageData>();
            PromptPersistenceService.Instance.Initialize();
            List<string> sceneTags = Owner.BuildProactiveSceneTags(context?.Category ?? NpcDialogueCategory.Social);
            string basePrompt = PromptPersistenceService.Instance.BuildFullSystemPrompt(
                context.Faction,
                PromptPersistenceService.Instance.LoadConfig(),
                true,
                sceneTags,
                runtimeSnapshot);
            messages.Add(new ChatMessageData { role = "system", content = basePrompt });
            Owner.AppendRecentSessionContext(messages, context.Faction);

            string categoryText = context.Category switch
            {
                NpcDialogueCategory.DiplomacyTask => "diplomacy_or_task",
                NpcDialogueCategory.WarningThreat => "warning_or_threat",
                _ => "casual_social"
            };

            string userPrompt =
                $"Generate one proactive diplomacy message now.\n" +
                $"Category: {categoryText}\n" +
                $"TriggerType: {context.TriggerType}\n" +
                $"Reason: {context.Reason}\n" +
                $"Severity: {context.Severity}\n";

            int rapidDeclineLoss = Owner.GetAccumulatedGoodwillLoss(context.Faction);
            if (rapidDeclineLoss > 30)
            {
                userPrompt += $"\n[DynamicOverride] {rapidDeclineLoss} points of goodwill lost in recent days. The faction's attitude toward the player has deteriorated significantly, making them more inclined to initiate hostile actions or even raids.\n";
            }

            messages.Add(new ChatMessageData { role = "user", content = userPrompt });
            Owner.AppendManualSocialPostPrompt(messages, context);
            return messages;
        }

internal List<ChatMessageData> BuildGenerationMessagesWithPrompt(
            NpcDialogueTriggerContext context,
            DiplomacyPromptRuntimeSnapshot runtimeSnapshot,
            string basePrompt,
            List<string> sceneTags)
        {
            var messages = new List<ChatMessageData>();
            messages.Add(new ChatMessageData { role = "system", content = basePrompt });
            Owner.AppendRecentSessionContext(messages, context.Faction);

            string categoryText = context.Category switch
            {
                NpcDialogueCategory.DiplomacyTask => "diplomacy_or_task",
                NpcDialogueCategory.WarningThreat => "warning_or_threat",
                _ => "casual_social"
            };

            string userPrompt =
                $"Generate one proactive diplomacy message now.\n" +
                $"Category: {categoryText}\n" +
                $"TriggerType: {context.TriggerType}\n" +
                $"Reason: {context.Reason}\n" +
                $"Severity: {context.Severity}\n";

            int rapidDeclineLoss = Owner.GetAccumulatedGoodwillLoss(context.Faction);
            if (rapidDeclineLoss > 30)
            {
                userPrompt += $"\n[DynamicOverride] {rapidDeclineLoss} points of goodwill lost in recent days. The faction's attitude toward the player has deteriorated significantly, making them more inclined to initiate hostile actions or even raids.\n";
            }

            messages.Add(new ChatMessageData { role = "user", content = userPrompt });
            Owner.AppendManualSocialPostPrompt(messages, context);
            return messages;
        }

internal int GetAccumulatedGoodwillLoss(Faction faction)
        {
            if (faction == null)
            {
                return 0;
            }

            int currentTick = Find.TickManager?.TicksGame ?? 0;
            FactionNpcPushState state = Owner.GetOrCreateState(faction);

            if (currentTick - state.lastGoodwillLossRecordTick > TickPerDay)
            {
                return 0;
            }

            return state.accumulatedGoodwillLossLastDay;
        }

internal List<string> BuildProactiveSceneTags(NpcDialogueCategory category)
        {
            var tags = new List<string>();
            switch (category)
            {
                case NpcDialogueCategory.DiplomacyTask:
                    tags.Add("scene:task");
                    break;
                case NpcDialogueCategory.WarningThreat:
                    tags.Add("scene:threat");
                    break;
                default:
                    tags.Add("scene:social");
                    break;
            }

            return tags;
        }

internal void AppendRecentSessionContext(List<ChatMessageData> messages, Faction faction)
        {
            if (messages == null || faction == null)
            {
                return;
            }

            FactionDialogueSession session = GameComponent_DiplomacyManager.Instance?.GetSession(faction);
            if (session?.messages == null || session.messages.Count == 0)
            {
                return;
            }

            int start = Math.Max(0, session.messages.Count - 4);
            for (int i = start; i < session.messages.Count; i++)
            {
                DialogueMessageData msg = session.messages[i];
                messages.Add(new ChatMessageData
                {
                    role = msg.isPlayer ? "user" : "assistant",
                    content = msg.message ?? string.Empty
                });
            }
        }

internal string SanitizeModelOutput(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return string.Empty;
            }

            DialogueResponseEnvelope envelope = DialogueResponseEnvelopeParser.Parse(output, DialogueUsageChannel.Diplomacy);
            if (!envelope.IsValid)
            {
                Log.Warning($"[RimAI.Relations] NPC push envelope parse failed: reason={envelope.FailureReason}. Dropping raw output.");
                return string.Empty;
            }
            string cleaned = envelope.VisibleDialogue;
            string merged = string.Join(" ", (cleaned ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s)));
            if (string.IsNullOrWhiteSpace(merged))
            {
                return string.Empty;
            }

            int hardLimit = RelationsMod.Settings?.ProactiveMessageHardLimit ?? 0;
            if (hardLimit > 0 && merged.Length > hardLimit)
            {
                merged = merged.Substring(0, hardLimit).TrimEnd();
            }

            ImmersionGuardResult guardResult = ImmersionOutputGuard.ValidateVisibleDialogueParts(merged);
            if (!guardResult.IsValid)
            {
                Log.Warning($"[RimAI.Relations] Immersion guard blocked NPC push text: reason={ImmersionOutputGuard.BuildViolationTag(guardResult.ViolationReason)}, snippet={guardResult.ViolationSnippet}");
                return guardResult.VisibleDialogue;
            }

            return guardResult.VisibleDialogue;
        }
    }
}
