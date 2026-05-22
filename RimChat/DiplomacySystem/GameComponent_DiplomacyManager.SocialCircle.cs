using System;
using System.Collections.Generic;
using System.Linq;
using RimChat.Core;
using RimChat.Memory;
using RimWorld;
using Verse;

namespace RimChat.DiplomacySystem
{
    /// <summary>/// Dependencies: social-circle services, social action resolver, RimWorld faction APIs.
 /// Responsibility: own social-circle state, public APIs, and schedule-based news polling.
 ///</summary>
    public partial class GameComponent_DiplomacyManager
    {
        private const int MaxSocialPosts = 200;
        private SocialCircleState socialCircleState = new SocialCircleState();
        private List<PublicSocialPost> cachedSortedPosts = new List<PublicSocialPost>();
        private bool socialPostsCacheDirty = true;
        private int socialPostListVersion = 0;
        private List<Faction> cachedEligibleFactions;
        private int eligibleFactionsCacheTick = -1;
        private const int EligibleFactionsCacheIntervalTicks = 60000;

        private void InitializeSocialCircleOnNewGame()
        {
            EnsureSocialCircleState();
            socialCircleState.Posts.Clear();
            socialCircleState.ActionIntents.Clear();
            socialCircleState.FactionActionCooldowns.Clear();
            socialCircleState.ProcessedOrigins.Clear();
            socialCircleState.ScheduledEvents.Clear();
            socialCircleState.LastReadPostId = string.Empty;
            ClearSocialTransientState();
            socialPostsCacheDirty = true;
            socialPostListVersion++;
            ScheduleNextSocialPost(Find.TickManager?.TicksGame ?? 0);
        }

        private void InitializeSocialCircleOnLoadedGame()
        {
            EnsureSocialCircleState();
            socialCircleState.CleanupInvalidEntries();
            socialCircleState.ClearPendingOrigins();
            ClearSocialTransientState();
            socialPostsCacheDirty = true;
            socialPostListVersion++;
            EnsureNextSocialPostTick(Find.TickManager?.TicksGame ?? 0);
        }

        public void ProcessSocialCircleTick()
        {
            if (!IsSocialCircleEnabled())
            {
                return;
            }

            EnsureSocialCircleState();
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            if (currentTick <= 0)
            {
                return;
            }

            EnsureNextSocialPostTick(currentTick);
            TryGenerateScheduledSocialPost(currentTick);
            SocialCircleActionResolver.ResolveAndExecute(this, socialCircleState, currentTick);
            TryProcessAiToAiInteraction(currentTick);
        }

        private void OnSocialCircleDailyReset()
        {
            EnsureSocialCircleState();
            SocialCircleService.DecayIntents(socialCircleState);
            socialCircleState.CleanupInvalidEntries();
        }

        public bool IsSocialCircleEnabled()
        {
            return RimChatMod.Instance?.InstanceSettings?.EnableSocialCircle ?? true;
        }

        public bool ForceGeneratePublicPost(DebugGenerateReason reason = DebugGenerateReason.ManualButton)
        {
            if (!IsSocialCircleEnabled())
            {
                return false;
            }

            int currentTick = Find.TickManager?.TicksGame ?? 0;
            bool queued = TryQueueNextScheduledNews(reason, currentTick, true);
            if (queued)
            {
                ScheduleNextSocialPost(currentTick);
            }

            return queued;
        }

        public bool TryForceGeneratePublicPost(
            DebugGenerateReason reason,
            out SocialForceGenerateFailureReason failureReason)
        {
            failureReason = SocialForceGenerateFailureReason.Unknown;

            if (!IsSocialCircleEnabled())
            {
                failureReason = SocialForceGenerateFailureReason.Disabled;
                return false;
            }

            int currentTick = Find.TickManager?.TicksGame ?? 0;
            bool queued = TryQueueNextScheduledNews(reason, currentTick, true, out failureReason);

            if (!queued && failureReason == SocialForceGenerateFailureReason.NoAvailableSeed)
            {
                WorldState.WorldEventLedgerComponent.Instance?.CollectNow();
                queued = TryQueueNextScheduledNews(reason, currentTick, true, out failureReason);
            }

            if (queued)
            {
                ScheduleNextSocialPost(currentTick);
            }

            return queued;
        }

        public bool EnqueuePublicPost(
            Faction sourceFaction,
            Faction targetFaction,
            SocialPostCategory category,
            int sentiment,
            string summary,
            bool isFromPlayerDialogue,
            string intentHint = "",
            DebugGenerateReason reason = DebugGenerateReason.DialogueExplicit)
        {
            return EnqueuePublicPost(
                sourceFaction,
                targetFaction,
                category,
                sentiment,
                summary,
                isFromPlayerDialogue,
                out _,
                intentHint,
                reason);
        }

        public bool EnqueuePublicPost(
            Faction sourceFaction,
            Faction targetFaction,
            SocialPostCategory category,
            int sentiment,
            string summary,
            bool isFromPlayerDialogue,
            out SocialPostEnqueueResult enqueueResult,
            string intentHint = "",
            DebugGenerateReason reason = DebugGenerateReason.DialogueExplicit)
        {
            enqueueResult = new SocialPostEnqueueResult
            {
                Triggered = true,
                FailureReason = SocialPostEnqueueFailureReason.Unknown
            };

            if (!IsSocialCircleEnabled())
            {
                enqueueResult.FailureReason = SocialPostEnqueueFailureReason.Disabled;
                return false;
            }

            if (sourceFaction == null)
            {
                enqueueResult.FailureReason = SocialPostEnqueueFailureReason.MissingSourceFaction;
                return false;
            }

            if (sourceFaction.defeated)
            {
                enqueueResult.FailureReason = SocialPostEnqueueFailureReason.SourceFactionDefeated;
                return false;
            }

            if (isFromPlayerDialogue && !(RimChatMod.Instance?.InstanceSettings?.EnablePlayerInfluenceNews ?? true))
            {
                enqueueResult.FailureReason = SocialPostEnqueueFailureReason.PlayerInfluenceDisabled;
                return false;
            }

            SocialNewsSeed seed = SocialNewsSeedFactory.CreateDialogueSeed(
                sourceFaction,
                targetFaction,
                category,
                sentiment,
                summary,
                reason == DebugGenerateReason.DialogueKeyword,
                intentHint,
                reason);

            enqueueResult.OriginType = seed?.OriginType ?? SocialNewsOriginType.Unknown;
            enqueueResult.OriginKey = seed?.OriginKey ?? string.Empty;

            bool queued = TryQueueNewsSeed(
                seed,
                Find.TickManager?.TicksGame ?? 0,
                out string requestId,
                out SocialPostEnqueueFailureReason failureReason);
            enqueueResult.Queued = queued;
            enqueueResult.RequestId = requestId ?? string.Empty;
            enqueueResult.FailureReason = queued ? SocialPostEnqueueFailureReason.None : failureReason;
            return queued;
        }

        public bool TryCreateKeywordDialoguePost(Faction sourceFaction, string playerMessage, string aiResponse)
        {
            return TryCreateKeywordDialoguePost(sourceFaction, playerMessage, aiResponse, out _);
        }

        public bool TryCreateKeywordDialoguePost(
            Faction sourceFaction,
            string playerMessage,
            string aiResponse,
            out SocialPostEnqueueResult enqueueResult)
        {
            enqueueResult = new SocialPostEnqueueResult
            {
                Triggered = false,
                FailureReason = SocialPostEnqueueFailureReason.KeywordNotMatched
            };

            if (!IsSocialCircleEnabled())
            {
                enqueueResult.FailureReason = SocialPostEnqueueFailureReason.Disabled;
                return false;
            }

            if (sourceFaction == null)
            {
                enqueueResult.FailureReason = SocialPostEnqueueFailureReason.MissingSourceFaction;
                return false;
            }

            if (sourceFaction.defeated)
            {
                enqueueResult.FailureReason = SocialPostEnqueueFailureReason.SourceFactionDefeated;
                return false;
            }

            if (!(RimChatMod.Instance?.InstanceSettings?.EnablePlayerInfluenceNews ?? true))
            {
                enqueueResult.FailureReason = SocialPostEnqueueFailureReason.PlayerInfluenceDisabled;
                return false;
            }

            bool matched = SocialCircleService.TryAnalyzeDialogueKeywords(
                playerMessage,
                aiResponse,
                out SocialPostCategory category,
                out int sentiment,
                out string intentHint);
            if (!matched)
            {
                Log.Message($"[RimChat] KeywordDialoguePost skipped: keywords not matched. playerMsgLen={playerMessage?.Length ?? 0}, aiResponseLen={aiResponse?.Length ?? 0}");
                return false;
            }

            enqueueResult.Triggered = true;
            Faction targetFaction = ResolveMentionedFaction($"{playerMessage} {aiResponse}", sourceFaction);
            string summary = SocialNewsSeedFactory.TryBuildFactionDialoguePublicClaim(
                sourceFaction,
                category,
                sentiment,
                aiResponse,
                intentHint,
                targetFaction);
            if (string.IsNullOrWhiteSpace(summary))
            {
                Log.Warning($"[RimChat] KeywordDialoguePost: summary is null/empty after matching keywords. category={category}, sentiment={sentiment}");
                string targetLabel = targetFaction != null ? $"与{targetFaction.Name}" : string.Empty;
                summary = $"{sourceFaction.Name}{targetLabel}就当前局势发表了公开声明。";
            }
            bool postResult = EnqueuePublicPost(
                sourceFaction,
                targetFaction,
                category,
                sentiment,
                summary,
                true,
                out enqueueResult,
                intentHint,
                DebugGenerateReason.DialogueKeyword);
            Log.Message($"[RimChat] KeywordDialoguePost enqueue: result={postResult}, category={category}, sentiment={sentiment}, failureReason={enqueueResult.FailureReason}");
            return postResult;
        }

        public Faction ResolveSocialTargetFaction(string token, Faction sourceFaction = null)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            string normalized = token.Trim();
            return GetEligibleSocialFactions()
                .FirstOrDefault(faction =>
                    faction != sourceFaction &&
                    (string.Equals(faction.Name, normalized, StringComparison.OrdinalIgnoreCase)
                     || string.Equals(faction.def?.defName, normalized, StringComparison.OrdinalIgnoreCase)
                     || string.Equals(faction.def?.label, normalized, StringComparison.OrdinalIgnoreCase)));
        }

        public List<PublicSocialPost> GetSocialPosts(int maxCount = MaxSocialPosts)
        {
            EnsureSocialCircleState();
            int count = Math.Max(1, maxCount);
            if (socialPostsCacheDirty)
            {
                cachedSortedPosts.Clear();
                for (int i = 0; i < socialCircleState.Posts.Count; i++)
                {
                    var post = socialCircleState.Posts[i];
                    if (post != null)
                        cachedSortedPosts.Add(post);
                }
                cachedSortedPosts.Sort((a, b) => (b?.CreatedTick ?? 0).CompareTo(a?.CreatedTick ?? 0));
                socialPostsCacheDirty = false;
            }
            if (cachedSortedPosts.Count <= count)
                return new List<PublicSocialPost>(cachedSortedPosts);
            return cachedSortedPosts.GetRange(0, count);
        }

        public int GetSocialPostListVersion()
        {
            return socialPostListVersion;
        }

        public int GetUnreadSocialPostCount()
        {
            EnsureSocialCircleState();
            if (socialCircleState.Posts.Count == 0)
            {
                return 0;
            }

            if (string.IsNullOrEmpty(socialCircleState.LastReadPostId))
            {
                return socialCircleState.Posts.Count;
            }

            int index = socialCircleState.Posts.FindLastIndex(post => post.PostId == socialCircleState.LastReadPostId);
            return index < 0 ? socialCircleState.Posts.Count : Math.Max(0, socialCircleState.Posts.Count - index - 1);
        }

        public void MarkSocialPostsRead()
        {
            EnsureSocialCircleState();
            if (socialCircleState.Posts.Count == 0)
            {
                socialCircleState.LastReadPostId = string.Empty;
                return;
            }

            socialCircleState.LastReadPostId = socialCircleState.Posts[socialCircleState.Posts.Count - 1].PostId;
        }

        private void EnsureSocialCircleState()
        {
            if (socialCircleState == null)
            {
                socialCircleState = new SocialCircleState();
            }

            socialCircleState.Posts = socialCircleState.Posts ?? new List<PublicSocialPost>();
            socialCircleState.ActionIntents = socialCircleState.ActionIntents ?? new List<SocialActionIntent>();
            socialCircleState.FactionActionCooldowns = socialCircleState.FactionActionCooldowns ?? new List<SocialFactionActionCooldown>();
            socialCircleState.ProcessedOrigins = socialCircleState.ProcessedOrigins ?? new List<SocialProcessedOrigin>();
            socialCircleState.ScheduledEvents = socialCircleState.ScheduledEvents ?? new List<ScheduledSocialEventRecord>();
        }

        private void EnsureNextSocialPostTick(int currentTick)
        {
            if (socialCircleState.NextPostTick > currentTick)
            {
                return;
            }

            ScheduleNextSocialPost(currentTick);
        }

        private void ScheduleNextSocialPost(int currentTick)
        {
            socialCircleState.NextPostTick = currentTick + SocialCircleService.CalculateNextIntervalTicks(RimChatMod.Instance?.InstanceSettings);
        }

        private const int NoSeedRetryIntervalTicks = 18000;

        private void TryGenerateScheduledSocialPost(int currentTick)
        {
            if (currentTick < socialCircleState.NextPostTick)
            {
                return;
            }

            bool generated = TryQueueNextScheduledNews(DebugGenerateReason.Scheduled, currentTick, false);
            if (generated)
            {
                ScheduleNextSocialPost(currentTick);
            }
            else
            {
                socialCircleState.NextPostTick = currentTick + NoSeedRetryIntervalTicks;
            }
        }

        private void TrimSocialPosts()
        {
            if (socialCircleState.Posts.Count <= MaxSocialPosts)
            {
                return;
            }

            int removeCount = socialCircleState.Posts.Count - MaxSocialPosts;
            socialCircleState.Posts.RemoveRange(0, removeCount);
            socialPostsCacheDirty = true;
            socialPostListVersion++;
        }

        private List<Faction> GetEligibleSocialFactions()
        {
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            if (cachedEligibleFactions != null && currentTick - eligibleFactionsCacheTick < EligibleFactionsCacheIntervalTicks)
                return cachedEligibleFactions;

            cachedEligibleFactions = Find.FactionManager.AllFactions
                .Where(faction => faction != null && !faction.defeated && !faction.def.hidden)
                .ToList();
            eligibleFactionsCacheTick = currentTick;
            return cachedEligibleFactions;
        }

        private Faction ResolveMentionedFaction(string text, Faction sourceFaction)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            string normalized = text.ToLowerInvariant();
            foreach (Faction faction in GetEligibleSocialFactions())
            {
                if (faction == sourceFaction)
                {
                    continue;
                }

                string factionNameLower = faction.Name?.ToLowerInvariant();
                if (!string.IsNullOrEmpty(factionNameLower) && normalized.Contains(factionNameLower))
                {
                    return faction;
                }

                string factionDefLabelLower = faction.def?.label?.ToLowerInvariant();
                if (!string.IsNullOrEmpty(factionDefLabelLower) && normalized.Contains(factionDefLabelLower))
                {
                    return faction;
                }
            }

            return null;
        }

        private void AddSocialSystemMessage(Faction sourceFaction, string message)
        {
            if (sourceFaction == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            HandleInboundFactionMessage(
                sourceFaction,
                "System",
                message,
                DialogueMessageType.System,
                null,
                markUnread: false,
                forcePresenceOnline: true);
        }

        public void RecordScheduledSocialEvent(
            ScheduledSocialEventType eventType,
            Faction sourceFaction,
            Faction targetFaction,
            string summary,
            string detail,
            int value,
            string sourceKey)
        {
            if (eventType == ScheduledSocialEventType.Unknown || string.IsNullOrWhiteSpace(sourceKey))
            {
                return;
            }

            EnsureSocialCircleState();
            socialCircleState.AddScheduledEvent(new ScheduledSocialEventRecord
            {
                EventType = eventType,
                SourceKey = sourceKey,
                OccurredTick = Find.TickManager?.TicksGame ?? 0,
                SourceFaction = sourceFaction,
                TargetFaction = targetFaction,
                Summary = summary?.Trim() ?? string.Empty,
                Detail = detail?.Trim() ?? string.Empty,
                Value = value
            });
        }

        public List<ScheduledSocialEventRecord> GetRecentScheduledSocialEvents(int daysWindow)
        {
            EnsureSocialCircleState();
            int safeDays = Math.Max(1, daysWindow);
            int nowTick = Find.TickManager?.TicksGame ?? 0;
            int minTick = nowTick - (safeDays * GenDate.TicksPerDay);
            return socialCircleState.GetRecentScheduledEvents(minTick);
        }

        private void AddSocialGenerationMessage(
            SocialNewsSeed seed,
            bool success,
            SocialPostGenerationFailureReason failureReason = SocialPostGenerationFailureReason.None)
        {
            if (seed?.SourceFaction == null)
            {
                return;
            }

            if (success)
            {
                AddSocialSystemMessage(seed.SourceFaction, "RimChat_SocialActionGenerated".Translate());
                return;
            }

            string reasonLabel = GetSocialFailureReasonLabel(failureReason);
            AddSocialSystemMessage(seed.SourceFaction, "RimChat_SocialActionFailedReason".Translate(reasonLabel));
        }

        public static string GetSocialFailureReasonLabel(SocialPostEnqueueFailureReason reason)
        {
            return GetSocialFailureReasonKey(reason).Translate();
        }

        public static string GetSocialFailureReasonLabel(SocialPostGenerationFailureReason reason)
        {
            return GetSocialFailureReasonKey(reason).Translate();
        }

        private static string GetSocialFailureReasonKey(SocialPostEnqueueFailureReason reason)
        {
            switch (reason)
            {
                case SocialPostEnqueueFailureReason.Disabled:
                    return "RimChat_SocialFailureReason_disabled";
                case SocialPostEnqueueFailureReason.PlayerInfluenceDisabled:
                    return "RimChat_SocialFailureReason_player_influence_disabled";
                case SocialPostEnqueueFailureReason.MissingSourceFaction:
                case SocialPostEnqueueFailureReason.SourceFactionDefeated:
                    return "RimChat_SocialFailureReason_missing_source_faction";
                case SocialPostEnqueueFailureReason.AiUnavailable:
                    return "RimChat_SocialFailureReason_ai_unavailable";
                case SocialPostEnqueueFailureReason.QueueFull:
                    return "RimChat_SocialFailureReason_queue_full";
                case SocialPostEnqueueFailureReason.InvalidSeed:
                    return "RimChat_SocialFailureReason_invalid_seed";
                case SocialPostEnqueueFailureReason.OriginBlocked:
                    return "RimChat_SocialFailureReason_origin_blocked";
                case SocialPostEnqueueFailureReason.RequestDispatchFailed:
                    return "RimChat_SocialFailureReason_request_dispatch_failed";
                case SocialPostEnqueueFailureReason.KeywordNotMatched:
                    return "RimChat_SocialFailureReason_keyword_not_matched";
                case SocialPostEnqueueFailureReason.PromptRenderIncompatible:
                    return "RimChat_SocialFailureReason_prompt_render_incompatible";
                default:
                    return "RimChat_SocialFailureReason_unknown";
            }
        }

        private static string GetSocialFailureReasonKey(SocialPostGenerationFailureReason reason)
        {
            switch (reason)
            {
                case SocialPostGenerationFailureReason.ParseFailed:
                    return "RimChat_SocialFailureReason_parse_failed";
                case SocialPostGenerationFailureReason.AiError:
                    return "RimChat_SocialFailureReason_ai_error";
                case SocialPostGenerationFailureReason.InvalidDraft:
                    return "RimChat_SocialFailureReason_invalid_draft";
                case SocialPostGenerationFailureReason.PromptRenderIncompatible:
                    return "RimChat_SocialFailureReason_prompt_render_incompatible";
                default:
                    return "RimChat_SocialFailureReason_unknown";
            }
        }

        // ── AI-to-AI faction interaction ──

        private void TryProcessAiToAiInteraction(int currentTick)
        {
            if (currentTick - _lastAiToAiGenerationTick < AiToAiGenerationIntervalTicks) return;
            _lastAiToAiGenerationTick = currentTick;

            if (Rand.Value < 0.15f)
                TryGenerateAiToAiSocialPost(DebugGenerateReason.Scheduled, currentTick);
        }

        public bool TryGenerateAiToAiSocialPost(DebugGenerateReason reason, int currentTick)
        {
            if (!IsSocialCircleEnabled()) return false;
            if (!CanGenerateSocialNews()) return false;

            List<Faction> candidates = GetEligibleSocialFactions()
                .Where(f => !f.IsPlayer && !f.defeated)
                .ToList();
            if (candidates.Count < 2) return false;

            Faction sourceFaction = candidates.RandomElement();
            Faction targetFaction = candidates
                .Where(f => f != sourceFaction)
                .RandomElement();
            if (sourceFaction == null || targetFaction == null) return false;

            SocialPostCategory category = PickRandomAiToAiCategory(sourceFaction, targetFaction);
            int sentiment = PickRandomAiToAiSentiment(sourceFaction, targetFaction, category);
            string summary = BuildAiToAiSummary(sourceFaction, targetFaction, category, sentiment);

            bool success = EnqueuePublicPost(
                sourceFaction,
                targetFaction,
                category,
                sentiment,
                summary,
                false,
                out _,
                "ai_to_ai_interaction",
                reason);

            if (success)
                Log.Message($"[RimChat] AI-to-AI post generated: {sourceFaction.Name} -> {targetFaction.Name}, category={category}, sentiment={sentiment}");

            return success;
        }

        private static SocialPostCategory PickRandomAiToAiCategory(Faction source, Faction target)
        {
            var relation = source.RelationKindWith(target);
            float roll = Rand.Value;
            if (relation == FactionRelationKind.Hostile)
                return roll < 0.6f ? SocialPostCategory.Military : SocialPostCategory.Diplomatic;
            if (relation == FactionRelationKind.Ally)
                return roll < 0.4f ? SocialPostCategory.Diplomatic : (roll < 0.75f ? SocialPostCategory.Economic : SocialPostCategory.Military);
            return SocialPostCategory.Diplomatic;
        }

        private static int PickRandomAiToAiSentiment(Faction source, Faction target, SocialPostCategory category)
        {
            var relation = source.RelationKindWith(target);
            if (relation == FactionRelationKind.Hostile) return Rand.RangeInclusive(-2, -1);
            if (relation == FactionRelationKind.Ally) return Rand.RangeInclusive(0, 2);
            return Rand.RangeInclusive(-1, 1);
        }

        private static string BuildAiToAiSummary(Faction source, Faction target, SocialPostCategory category, int sentiment)
        {
            if (sentiment >= 1)
                return $"{source.Name} and {target.Name} strengthen their ties amidst the shifting balance of power.";
            if (sentiment <= -1)
                return $"{source.Name} condemns {target.Name}'s actions, further straining relations between the two factions.";
            return $"{source.Name} and {target.Name} are engaged in quiet diplomatic maneuvering.";
        }
    }
}
