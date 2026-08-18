using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting;
using RimWorld;
using Verse;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Prompting.Diplomacy;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
    /// <summary>/// Dependencies: AIChatServiceAsync, native prompt fail-fast exceptions, social news seed factory, social news JSON parser, leader-memory services.
 /// Responsibility: queue, track, finalize social-news generation requests, and mirror published post summaries into faction leader memory.
 ///</summary>
        internal sealed class DiplomacyManagerSocialCircleNewsRequests : GameComponent_DiplomacyManagerCollaborator
    {
        internal DiplomacyManagerSocialCircleNewsRequests(GameComponent_DiplomacyManager owner) : base(owner)
        {
        }


        internal const int MaxPendingSocialNewsRequests = 4;
        internal const int FailedOriginAutoRetryDays = 1;
        internal const int SnapshotRetryDelayTicks = 250;
        internal readonly Dictionary<string, PendingSocialNewsRequest> pendingSocialNewsRequests =
            new Dictionary<string, PendingSocialNewsRequest>();
        internal readonly List<DeferredSocialNewsSeed> deferredSocialNewsSeeds =
            new List<DeferredSocialNewsSeed>();
        internal readonly Dictionary<string, DeferredSocialNewsSeed> deferredSocialNewsSeedsByKey =
            new Dictionary<string, DeferredSocialNewsSeed>();

        internal void ClearSocialTransientState()
        {
            pendingSocialNewsRequests.Clear();
            deferredSocialNewsSeeds.Clear();
            deferredSocialNewsSeedsByKey.Clear();
        }

        public void ProcessDeferredSocialNewsSeeds(int currentTick)
        {
            if (deferredSocialNewsSeeds.Count == 0) return;
            if (!Owner.IsSocialCircleEnabled() || currentTick <= 0)
            {
                return;
            }

            if (!Owner.CanGenerateSocialNews())
            {
                return;
            }

            int processed = 0;
            for (int i = deferredSocialNewsSeeds.Count - 1; i >= 0 && processed < 3; i--)
            {
                DeferredSocialNewsSeed item = deferredSocialNewsSeeds[i];
                if (item == null || item.DueTick > currentTick)
                {
                    continue;
                }

                deferredSocialNewsSeeds.RemoveAt(i);
                if (item.Key != null)
                    deferredSocialNewsSeedsByKey.Remove(item.Key);
                if (item.Seed == null || !item.Seed.IsValid())
                {
                    continue;
                }

                Owner.TryQueueNewsSeed(item.Seed, currentTick, item.AllowFailedRetry);
                processed++;
                if (pendingSocialNewsRequests.Count >= MaxPendingSocialNewsRequests)
                {
                    break;
                }
            }
        }

        internal bool TryQueueNextScheduledNews(DebugGenerateReason reason, int currentTick, bool bypassSimulationToggle)
        {
            if (!bypassSimulationToggle && !(Ustas.RimAI.Communication.Relations.Module.RelationsMod.Instance?.InstanceSettings?.EnableAISimulationNews ?? true))
            {
                return false;
            }

            if (!Owner.CanGenerateSocialNews())
            {
                return false;
            }

            bool allowFailedRetry = reason == DebugGenerateReason.ManualButton;
            SocialNewsSeed seed = Owner.SelectNextScheduledSeed(allowFailedRetry, currentTick);
            if (seed == null)
            {
                return false;
            }

            seed.DebugReason = reason;
            return Owner.TryQueueNewsSeed(seed, currentTick, allowFailedRetry);
        }

        internal bool TryQueueNextScheduledNews(
            DebugGenerateReason reason,
            int currentTick,
            bool bypassSimulationToggle,
            out SocialForceGenerateFailureReason failureReason)
        {
            failureReason = SocialForceGenerateFailureReason.Unknown;

            if (!bypassSimulationToggle && !(Ustas.RimAI.Communication.Relations.Module.RelationsMod.Instance?.InstanceSettings?.EnableAISimulationNews ?? true))
            {
                failureReason = SocialForceGenerateFailureReason.Disabled;
                return false;
            }

            if (!Owner.CanGenerateSocialNews(out failureReason))
            {
                return false;
            }

            bool allowFailedRetry = reason == DebugGenerateReason.ManualButton;
            SocialNewsSeed seed = Owner.SelectNextScheduledSeed(allowFailedRetry, currentTick);
            if (seed == null)
            {
                failureReason = SocialForceGenerateFailureReason.NoAvailableSeed;
                return false;
            }

            seed.DebugReason = reason;
            bool queued = Owner.TryQueueNewsSeed(seed, currentTick, allowFailedRetry);
            if (!queued)
            {
                failureReason = SocialForceGenerateFailureReason.Unknown;
            }

            return queued;
        }

        internal bool TryQueueNewsSeed(SocialNewsSeed seed, int currentTick, bool allowFailedRetry = false)
        {
            return Owner.TryQueueNewsSeed(
                seed,
                currentTick,
                out _,
                out _,
                allowFailedRetry);
        }

        internal bool TryQueueNewsSeed(
            SocialNewsSeed seed,
            int currentTick,
            out string requestId,
            out SocialPostEnqueueFailureReason failureReason,
            bool allowFailedRetry = false)
        {
            requestId = string.Empty;
            failureReason = SocialPostEnqueueFailureReason.Unknown;

            if (seed == null || !seed.IsValid())
            {
                Log.Warning($"[RimAI.Relations] Social news seed invalid. origin_type={seed?.OriginType}, origin_key={seed?.OriginKey ?? "null"}, facts_count={seed?.Facts?.Count ?? 0}, occurred_tick={seed?.OccurredTick ?? -1}");
                failureReason = SocialPostEnqueueFailureReason.InvalidSeed;
                return false;
            }

            if (!Owner.CanGenerateSocialNews(out SocialForceGenerateFailureReason forceFailure))
            {
                failureReason = GameComponent_DiplomacyManager.MapForceFailureToEnqueueFailure(forceFailure);
                return false;
            }

            if (Owner.IsOriginBlocked(seed, allowFailedRetry, currentTick))
            {
                failureReason = SocialPostEnqueueFailureReason.OriginBlocked;
                return false;
            }

            if (!Owner.TryResolvePromptSnapshotOrDefer(seed, currentTick, allowFailedRetry, out DiplomacyPromptRuntimeSnapshot snapshot))
            {
                failureReason = SocialPostEnqueueFailureReason.None;
                return true;
            }

            List<ChatMessageData> messages;
            try
            {
                messages = SocialNewsPromptBuilder.BuildMessages(seed, snapshot);
                Log.Message(
                    "[RimAI.Relations][SocialNewsPrompt] "
                    + $"origin_type={seed.OriginType}, origin_key={seed.OriginKey ?? string.Empty}, "
                    + $"source_faction={seed.SourceFaction?.Name ?? "None"}, target_faction={seed.TargetFaction?.Name ?? "None"}, "
                    + $"facts={GameComponent_DiplomacyManager.BuildResponsePreview(string.Join(" | ", (seed.Facts ?? new List<string>()).Where(item => !string.IsNullOrWhiteSpace(item))), 800)}, "
                    + $"prompt_input={GameComponent_DiplomacyManager.BuildResponsePreview(SocialNewsPromptBuilder.BuildPromptInputPayloadForDebug(seed), 1000)}");
            }
            catch (PromptRenderException ex)
            {
                Log.Warning(
                    "[RimAI.Relations] Social news prompt render failed. " +
                    $"requestId=not_dispatched, debugSource={AIRequestDebugSource.SocialNews}, stage=scriban_render_error, " +
                    $"origin_type={seed.OriginType}, origin_key={seed.OriginKey ?? string.Empty}, " +
                    $"template_id={ex.TemplateId}, error_code={ex.ErrorCode}, line={ex.ErrorLine}, column={ex.ErrorColumn}, " +
                    $"error={ex.Message}");
                socialCircleState.MarkOriginState(seed.OriginType, seed.OriginKey, SocialNewsGenerationState.Failed, currentTick);
                Owner.AddSocialGenerationMessage(seed, false, SocialPostGenerationFailureReason.PromptRenderIncompatible);
                failureReason = SocialPostEnqueueFailureReason.PromptRenderIncompatible;
                return false;
            }

            socialCircleState.MarkOriginState(seed.OriginType, seed.OriginKey, SocialNewsGenerationState.Pending, currentTick);
            string localRequestId = string.Empty;
            localRequestId = AIChatServiceAsync.Instance.SendChatRequestAsync(
                messages,
                onSuccess: response => Owner.OnSocialNewsRequestSuccess(localRequestId, response),
                onError: error => Owner.OnSocialNewsRequestError(localRequestId, error),
                usageChannel: DialogueUsageChannel.Diplomacy,
                debugSource: AIRequestDebugSource.SocialNews);
            requestId = localRequestId;
            if (string.IsNullOrEmpty(localRequestId))
            {
                socialCircleState.MarkOriginState(seed.OriginType, seed.OriginKey, SocialNewsGenerationState.Failed, currentTick);
                failureReason = SocialPostEnqueueFailureReason.RequestDispatchFailed;
                return false;
            }

            pendingSocialNewsRequests[localRequestId] = new PendingSocialNewsRequest
            {
                Seed = seed,
                QueuedTick = currentTick
            };
            failureReason = SocialPostEnqueueFailureReason.None;
            return true;
        }

        internal bool TryResolvePromptSnapshotOrDefer(
            SocialNewsSeed seed,
            int currentTick,
            bool allowFailedRetry,
            out DiplomacyPromptRuntimeSnapshot snapshot)
        {
            snapshot = null;
            Faction snapshotFaction = seed?.SourceFaction ?? seed?.TargetFaction;
            if (snapshotFaction == null)
            {
                return true;
            }

            IDiplomacyPromptSnapshotCache cache = DiplomacyPromptSnapshotCache.Instance;
            cache.RequestWarmup(snapshotFaction, "social_news_seed");
            if (cache.TryGetSnapshot(snapshotFaction, out snapshot))
            {
                return true;
            }

            Owner.EnqueueDeferredSocialNewsSeed(seed, currentTick + SnapshotRetryDelayTicks, allowFailedRetry);
            return false;
        }

        internal void EnqueueDeferredSocialNewsSeed(SocialNewsSeed seed, int dueTick, bool allowFailedRetry)
        {
            if (seed == null || !seed.IsValid())
            {
                Log.Warning($"[RimAI.Relations] Deferred social news seed dropped (invalid). origin_type={seed?.OriginType}, origin_key={seed?.OriginKey ?? "null"}");
                return;
            }

            string key = GameComponent_DiplomacyManager.BuildDeferredSocialSeedKey(seed);
            if (deferredSocialNewsSeedsByKey.TryGetValue(key, out DeferredSocialNewsSeed existing))
            {
                existing.DueTick = Math.Min(existing.DueTick, dueTick);
                existing.AllowFailedRetry |= allowFailedRetry;
                return;
            }

            var item = new DeferredSocialNewsSeed
            {
                Key = key,
                Seed = seed,
                DueTick = dueTick,
                AllowFailedRetry = allowFailedRetry
            };
            deferredSocialNewsSeeds.Add(item);
            deferredSocialNewsSeedsByKey[key] = item;
        }

        internal static string BuildDeferredSocialSeedKey(SocialNewsSeed seed)
        {
            if (!string.IsNullOrWhiteSpace(seed?.OriginKey))
            {
                return $"{seed.OriginType}:{seed.OriginKey}";
            }

            int summaryHash = GenText.StableStringHash(seed?.Summary ?? string.Empty);
            string sourceId = seed?.SourceFaction?.GetUniqueLoadID() ?? "none";
            string targetId = seed?.TargetFaction?.GetUniqueLoadID() ?? "none";
            return $"{seed?.OriginType}:{sourceId}:{targetId}:{summaryHash}";
        }

        internal bool CanGenerateSocialNews()
        {
            return AIChatServiceAsync.Instance != null
                && AIChatServiceAsync.Instance.IsConfigured()
                && pendingSocialNewsRequests.Count < MaxPendingSocialNewsRequests;
        }

        internal bool CanGenerateSocialNews(out SocialForceGenerateFailureReason failureReason)
        {
            failureReason = SocialForceGenerateFailureReason.Unknown;

            if (AIChatServiceAsync.Instance == null || !AIChatServiceAsync.Instance.IsConfigured())
            {
                failureReason = SocialForceGenerateFailureReason.AiUnavailable;
                return false;
            }

            if (pendingSocialNewsRequests.Count >= MaxPendingSocialNewsRequests)
            {
                failureReason = SocialForceGenerateFailureReason.QueueFull;
                return false;
            }

            return true;
        }

        internal SocialNewsSeed SelectNextScheduledSeed(bool allowFailedRetry, int currentTick)
        {
            return SocialNewsSeedFactory.CollectScheduledSeeds()
                .FirstOrDefault(seed =>
                    !Owner.HasPublishedOrigin(seed) &&
                    !Owner.IsOriginBlocked(seed, allowFailedRetry, currentTick));
        }

        internal bool IsOriginBlocked(SocialNewsSeed seed, bool allowFailedRetry, int currentTick)
        {
            SocialProcessedOrigin entry = Owner.FindProcessedOrigin(seed);
            if (entry == null)
            {
                return false;
            }

            if (entry.State != SocialNewsGenerationState.Failed)
            {
                return true;
            }

            if (allowFailedRetry)
            {
                return false;
            }

            int retryTicks = FailedOriginAutoRetryDays * GenDate.TicksPerDay;
            return currentTick - entry.ProcessedTick < retryTicks;
        }

        internal SocialProcessedOrigin FindProcessedOrigin(SocialNewsSeed seed)
        {
            if (seed == null || string.IsNullOrWhiteSpace(seed.OriginKey))
            {
                return null;
            }

            return socialCircleState.ProcessedOrigins?.FirstOrDefault(item =>
                item != null &&
                item.OriginType == seed.OriginType &&
                string.Equals(item.OriginKey, seed.OriginKey, System.StringComparison.Ordinal));
        }

        internal bool HasPublishedOrigin(SocialNewsSeed seed)
        {
            if (seed == null || string.IsNullOrWhiteSpace(seed.OriginKey))
                return false;
            return socialCircleState.PublishedPostOriginKeys.Contains(
                $"{(int)seed.OriginType}:{seed.OriginKey}");
        }

        internal void OnSocialNewsRequestSuccess(string requestId, string response)
        {
            if (!Owner.TryTakePendingSocialRequest(requestId, out PendingSocialNewsRequest pending))
            {
                return;
            }

            int currentTick = Find.TickManager?.TicksGame ?? pending.QueuedTick;
            if (!SocialNewsJsonParser.TryParse(
                    response,
                    out SocialNewsDraft draft,
                    out string error,
                    pending.Seed?.PrimaryClaim ?? string.Empty,
                    pending.Seed?.QuoteAttributionHint ?? string.Empty))
            {
                Log.Warning(
                    "[RimAI.Relations] Social news generation failed to parse. " +
                    $"requestId={requestId ?? string.Empty}, debugSource={AIRequestDebugSource.SocialNews}, stage=parse_fail, " +
                    $"error={error}, response_preview={GameComponent_DiplomacyManager.BuildResponsePreview(response, 260)}");
                socialCircleState.MarkOriginState(pending.Seed.OriginType, pending.Seed.OriginKey, SocialNewsGenerationState.Failed, currentTick);
                Owner.AddSocialGenerationMessage(pending.Seed, false, SocialPostGenerationFailureReason.ParseFailed);
                return;
            }

            PublicSocialPost post = SocialCircleService.CreatePostFromDraft(pending.Seed, draft);
            Log.Message(
                "[RimAI.Relations][SocialNewsDraft] "
                + $"origin_type={pending.Seed?.OriginType.ToString() ?? "Unknown"}, origin_key={pending.Seed?.OriginKey ?? string.Empty}, "
                + $"location_name={draft?.LocationName ?? string.Empty}, quote_attribution={draft?.QuoteAttribution ?? string.Empty}, "
                + $"headline={GameComponent_DiplomacyManager.BuildResponsePreview(draft?.Headline ?? string.Empty, 160)}, lead={GameComponent_DiplomacyManager.BuildResponsePreview(draft?.Lead ?? string.Empty, 220)}, "
                + $"quote={GameComponent_DiplomacyManager.BuildResponsePreview(draft?.Quote ?? string.Empty, 220)}");
            if (post == null || Owner.HasPublishedOrigin(pending.Seed))
            {
                socialCircleState.MarkOriginState(pending.Seed.OriginType, pending.Seed.OriginKey, SocialNewsGenerationState.Failed, currentTick);
                Owner.AddSocialGenerationMessage(pending.Seed, false, SocialPostGenerationFailureReason.InvalidDraft);
                return;
            }

            Owner.AddCompletedSocialPost(post, pending.Seed, currentTick);
            Owner.AddSocialGenerationMessage(pending.Seed, true);
        }

        internal void OnSocialNewsRequestError(string requestId, string error)
        {
            if (!Owner.TryTakePendingSocialRequest(requestId, out PendingSocialNewsRequest pending))
            {
                return;
            }

            int currentTick = Find.TickManager?.TicksGame ?? pending.QueuedTick;
            Log.Warning($"[RimAI.Relations] Social news generation failed: {error}");
            socialCircleState.MarkOriginState(pending.Seed.OriginType, pending.Seed.OriginKey, SocialNewsGenerationState.Failed, currentTick);
            Owner.AddSocialGenerationMessage(pending.Seed, false, SocialPostGenerationFailureReason.AiError);
        }

        internal static string BuildResponsePreview(string response, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                return string.Empty;
            }

            string normalized = response
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();
            if (normalized.Length <= maxLength)
            {
                return normalized;
            }

            return normalized.Substring(0, Math.Max(0, maxLength)) + "...";
        }

        internal bool TryTakePendingSocialRequest(string requestId, out PendingSocialNewsRequest pending)
        {
            pending = null;
            if (string.IsNullOrEmpty(requestId) || !pendingSocialNewsRequests.TryGetValue(requestId, out pending))
            {
                return false;
            }

            pendingSocialNewsRequests.Remove(requestId);
            return pending?.Seed != null;
        }

        internal void AddCompletedSocialPost(PublicSocialPost post, SocialNewsSeed seed, int currentTick)
        {
            socialCircleState.Posts.Add(post);
            if (!string.IsNullOrWhiteSpace(post.OriginKey))
                socialCircleState.PublishedPostOriginKeys.Add($"{(int)post.OriginType}:{post.OriginKey}");
            socialPostsCacheDirty = true;
            socialPostListVersion++;
            Owner.TrimSocialPosts();
            if (seed.ApplyDiplomaticImpact)
            {
                SocialCircleService.ApplyDialogueConsequences(socialCircleState, post);
            }

            if (GameComponent_DiplomacyManager.ShouldSendSocialNewsLetter(post))
            {
                Owner.TrySendSocialNewsLetter(post);
            }
            Owner.MirrorSocialPostSummaryToLeaderMemories(post, currentTick);
            socialCircleState.MarkOriginState(seed.OriginType, seed.OriginKey, SocialNewsGenerationState.Completed, currentTick);
        }

        internal static bool ShouldSendSocialNewsLetter(PublicSocialPost post)
        {
            return post != null && post.OriginType != SocialNewsOriginType.PlayerManual;
        }

        internal void MirrorSocialPostSummaryToLeaderMemories(PublicSocialPost post, int fallbackTick)
        {
            if (!GameComponent_DiplomacyManager.ShouldMirrorSocialPostSummary(post))
            {
                return;
            }

            string summary = GameComponent_DiplomacyManager.BuildSocialPostSummaryText(post);
            if (string.IsNullOrWhiteSpace(summary))
            {
                return;
            }

            int tick = post.CreatedTick > 0 ? post.CreatedTick : fallbackTick;
            string contentHash = GameComponent_DiplomacyManager.BuildSocialPostContentHash(post, tick);
            foreach (Faction targetFaction in Owner.GetSummaryMirrorTargetFactions())
            {
                CrossChannelSummaryRecord record = GameComponent_DiplomacyManager.CreateSocialPostSummaryRecord(post, targetFaction, summary, tick, contentHash);
                LeaderMemoryManager.Instance.AddDiplomacySessionSummary(
                    targetFaction,
                    record,
                    DialogueSummaryService.MaxSummaryPoolPerType);
            }
        }

        internal static bool ShouldMirrorSocialPostSummary(PublicSocialPost post)
        {
            return post != null
                && post.OriginType != SocialNewsOriginType.DiplomacySummary;
        }

        internal List<Faction> GetSummaryMirrorTargetFactions()
        {
            return Owner.GetEligibleSocialFactions()
                .Where(faction => !faction.IsPlayer)
                .ToList();
        }

        internal static string BuildSocialPostSummaryText(PublicSocialPost post)
        {
            string headline = post?.Headline?.Trim() ?? string.Empty;
            string lead = post?.Lead?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(headline) && !string.IsNullOrWhiteSpace(lead))
            {
                return $"{headline} {lead}";
            }

            if (!string.IsNullOrWhiteSpace(headline))
            {
                return headline;
            }

            if (!string.IsNullOrWhiteSpace(lead))
            {
                return lead;
            }

            return post?.Content?.Trim() ?? string.Empty;
        }

        internal static string BuildSocialPostContentHash(PublicSocialPost post, int tick)
        {
            if (!string.IsNullOrWhiteSpace(post?.PostId))
            {
                return $"social-post:{post.PostId}";
            }

            return $"social-post:{post?.OriginType}:{post?.OriginKey}:{tick}";
        }

        internal static CrossChannelSummaryRecord CreateSocialPostSummaryRecord(
            PublicSocialPost post,
            Faction targetFaction,
            string summary,
            int tick,
            string contentHash)
        {
            return new CrossChannelSummaryRecord
            {
                Source = CrossChannelSummarySource.DiplomacySession,
                FactionId = targetFaction?.GetUniqueLoadID() ?? string.Empty,
                PawnLoadId = -1,
                PawnName = string.Empty,
                SummaryText = summary,
                KeyFacts = GameComponent_DiplomacyManager.BuildSocialPostSummaryFacts(post),
                GameTick = tick,
                Confidence = 0.70f,
                ContentHash = contentHash ?? string.Empty,
                IsLlmFallback = false,
                CreatedTimestamp = System.DateTime.UtcNow.Ticks
            };
        }

        internal static List<string> BuildSocialPostSummaryFacts(PublicSocialPost post)
        {
            string sourceName = post?.SourceFaction?.Name ?? "Unknown";
            string targetName = post?.TargetFaction?.Name ?? "None";
            string postId = string.IsNullOrWhiteSpace(post?.PostId) ? "none" : post.PostId;
            return new List<string>
            {
                $"post_id: {postId}",
                $"origin: {post?.OriginType.ToString() ?? "Unknown"}",
                $"category: {SocialCircleService.GetCategoryLabel(post?.Category ?? SocialPostCategory.Diplomatic)}",
                $"sentiment: {post?.Sentiment ?? 0}",
                $"source: {sourceName}",
                $"target: {targetName}"
            };
        }

        internal void TrySendSocialNewsLetter(PublicSocialPost post)
        {
            if (post == null || Find.LetterStack == null)
            {
                return;
            }

            string source = post.SourceFaction?.Name;
            if (string.IsNullOrWhiteSpace(source))
            {
                source = "RimChat_SocialNoLeader".Translate();
            }

            string category = SocialCircleService.GetCategoryLabel(post.Category);
            string title = "RimChat_SocialNewsLetterTitle".Translate(source, category);
            string headline = string.IsNullOrWhiteSpace(post.Headline) ? post.Content ?? string.Empty : post.Headline;
            string lead = string.IsNullOrWhiteSpace(post.Lead) ? string.Empty : post.Lead;
            string body = "RimChat_SocialNewsLetterBody".Translate(headline, lead);
            Find.LetterStack.ReceiveLetter(title, body, GameComponent_DiplomacyManager.ResolveSocialNewsLetterDef(post));
        }

        internal static LetterDef ResolveSocialNewsLetterDef(PublicSocialPost post)
        {
            if (post == null)
            {
                return LetterDefOf.NeutralEvent;
            }

            if (post.Sentiment <= -2)
            {
                return LetterDefOf.ThreatBig;
            }

            if (post.Sentiment == -1)
            {
                return LetterDefOf.ThreatSmall;
            }

            if (post.Sentiment >= 1)
            {
                return LetterDefOf.PositiveEvent;
            }

            return LetterDefOf.NeutralEvent;
        }

        internal sealed class PendingSocialNewsRequest
        {
            public SocialNewsSeed Seed;
            public int QueuedTick;
        }

        internal sealed class DeferredSocialNewsSeed
        {
            public string Key = string.Empty;
            public SocialNewsSeed Seed;
            public int DueTick;
            public bool AllowFailedRetry;
        }

        internal static SocialPostEnqueueFailureReason MapForceFailureToEnqueueFailure(SocialForceGenerateFailureReason failureReason)
        {
            switch (failureReason)
            {
                case SocialForceGenerateFailureReason.Disabled:
                    return SocialPostEnqueueFailureReason.Disabled;
                case SocialForceGenerateFailureReason.AiUnavailable:
                    return SocialPostEnqueueFailureReason.AiUnavailable;
                case SocialForceGenerateFailureReason.QueueFull:
                    return SocialPostEnqueueFailureReason.QueueFull;
                default:
                    return SocialPostEnqueueFailureReason.Unknown;
            }
        }
        }

}
