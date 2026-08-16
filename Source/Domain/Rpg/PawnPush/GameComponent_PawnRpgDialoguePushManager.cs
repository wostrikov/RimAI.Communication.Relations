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
    /// <summary>/// Dependencies: AIChatServiceAsync, RimChat settings, Verse.GameComponent.
 /// Responsibility: Orchestrate PawnRPG proactive trigger intake, queueing, throttling, generation and delivery.
 ///</summary>
    public partial class GameComponent_PawnRpgDialoguePushManager : GameComponent
    {
        private sealed class PendingGenerationContext
        {
            public PawnRpgTriggerContext Context;
            public Pawn NpcPawn;
            public Pawn PlayerPawn;
            public List<ChatMessageData> Messages;
            public int Attempt;
        }

        private const int TickPerHour = 2500;
        private const int TickPerDay = 60000;
        private const int RegularEvaluationInterval = 36000;
        private const int QueueProcessInterval = 600;
        private const int IncomingDrainInterval = 120;
        private const int ThreatScanInterval = 600;
        private const int ClickWindowTicks = 360;
        private const int ClickBusyThreshold = 12;
        private const int CausalMinDelayTicks = 250;
        private const int CausalMaxDelayTicks = 1000;
        private const int NpcEvaluateCooldownTicks = 150000;
        private const int ColonyDeliveryCooldownTicks = TickPerHour * 3;
        private const int ColonistPairCooldownTicks = TickPerHour;
        private const int BlockedRetryTicks = 300;
        private const int MissingProtagonistLogIntervalTicks = 6000;
        private const float LowMoodThreshold = 0.30f;
        private const int QuestDeadlineWindowTicks = TickPerDay;
        private const int QuestTriggerRepeatTicks = 15000;
        private const int MessageDedupWindowTicks = 150000;
        private const int RpgWindowMaxMessages = 1;
        private const int RpgWindowTicks = 60000;
        private const int HomeEventCooldownTicks = 150000;
        private const int EventDedupWindowTicks = 75000;

        public static GameComponent_PawnRpgDialoguePushManager Instance;

        private List<PawnRpgNpcPushState> npcPushStates = new List<PawnRpgNpcPushState>();
        private Dictionary<Pawn, PawnRpgNpcPushState> _npcStateByPawn;
        private List<PawnRpgThreatState> threatStates = new List<PawnRpgThreatState>();
        private List<QueuedPawnRpgTrigger> queuedTriggers = new List<QueuedPawnRpgTrigger>();
        private List<PawnRpgProtagonistEntry> proactiveProtagonists = new List<PawnRpgProtagonistEntry>();

        private readonly Queue<PawnRpgTriggerContext> incomingTriggers = new Queue<PawnRpgTriggerContext>();
        private readonly Dictionary<string, PendingGenerationContext> pendingRequests = new Dictionary<string, PendingGenerationContext>();
        private readonly HashSet<Faction> factionsWithPendingRequests = new HashSet<Faction>();
        private readonly Queue<int> clickTicks = new Queue<int>();
        private readonly Dictionary<string, int> recentQuestTriggerTicks = new Dictionary<string, int>();
        private Dictionary<string, int> recentMessageHashes = new Dictionary<string, int>();
        private readonly List<int> rpgDeliveryTicks = new List<int>();
        private Dictionary<string, int> recentEventDeliveries = new Dictionary<string, int>();
        private int lastHomeEventTriggerTick = -1;
        private int lastColonyDeliveredTick = -ColonyDeliveryCooldownTicks;
        private int lastColonistPairDeliveredTick = -ColonyDeliveryCooldownTicks;
        private bool _colonistPairHadThreat;
        private int lastMissingProtagonistLogTick = -MissingProtagonistLogIntervalTicks;

        // Per-tick cache to avoid repeated ResolveConfiguredProtagonists() allocations
        private List<Pawn> _cachedProtagonists;
        private int _cachedProtagonistsTick = -1;

        // Per-tick cache to avoid repeated GetFactionNpcCandidates() scans
        private Dictionary<Faction, List<Pawn>> _cachedFactionNpcs;
        private int _cachedFactionNpcsTick = -1;

        // System prompt cache keyed by pawn-pair + scene tags, TTL-limited
        private const int SystemPromptCacheTtlTicks = 3000;
        private readonly Dictionary<string, (int builtTick, string prompt)> _systemPromptCache =
            new Dictionary<string, (int builtTick, string prompt)>();

        public GameComponent_PawnRpgDialoguePushManager(Game game) : base()
        {
            Instance = this;
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            Instance = this;
            ClearTransientState();
            AutoSelectDefaultProtagonist();
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            Instance = this;
            ClearTransientState();
            CleanupInvalidState();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            try
            {
                Scribe_Collections.Look(ref npcPushStates, "pawnRpgNpcPushStates", LookMode.Deep);
                Scribe_Collections.Look(ref threatStates, "pawnRpgThreatStates", LookMode.Deep);
                Scribe_Collections.Look(ref queuedTriggers, "pawnRpgQueuedTriggers", LookMode.Deep);
                Scribe_Collections.Look(ref proactiveProtagonists, "pawnRpgProactiveProtagonists", LookMode.Deep);
                Scribe_Values.Look(ref lastColonyDeliveredTick, "pawnRpgLastColonyDeliveredTick", -ColonyDeliveryCooldownTicks);
                Scribe_Values.Look(ref lastColonistPairDeliveredTick, "pawnRpgLastColonistPairDeliveredTick", -ColonyDeliveryCooldownTicks);
                Scribe_Values.Look(ref _colonistPairHadThreat, "pawnRpgColonistPairHadThreat", false);
                Scribe_Values.Look(ref lastHomeEventTriggerTick, "lastHomeEventTriggerTick", -1);
                Scribe_Collections.Look(ref recentEventDeliveries, "recentEventDeliveries", LookMode.Value, LookMode.Value);
                if (Scribe.mode == LoadSaveMode.Saving)
                {
                    CleanupExpiredMessageHashes(Find.TickManager?.TicksGame ?? 0);
                }
                Scribe_Collections.Look(ref recentMessageHashes, "recentMessageHashes", LookMode.Value, LookMode.Value);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Relations] Error loading PawnRpg data from save: {ex.Message}\n{ex.StackTrace}");
                npcPushStates ??= new List<PawnRpgNpcPushState>();
                threatStates ??= new List<PawnRpgThreatState>();
                queuedTriggers ??= new List<QueuedPawnRpgTrigger>();
                proactiveProtagonists ??= new List<PawnRpgProtagonistEntry>();
                recentMessageHashes ??= new Dictionary<string, int>();
                recentEventDeliveries ??= new Dictionary<string, int>();
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                npcPushStates ??= new List<PawnRpgNpcPushState>();
                threatStates ??= new List<PawnRpgThreatState>();
                queuedTriggers ??= new List<QueuedPawnRpgTrigger>();
                proactiveProtagonists ??= new List<PawnRpgProtagonistEntry>();
                recentMessageHashes ??= new Dictionary<string, int>();
                recentEventDeliveries ??= new Dictionary<string, int>();
                _cachedProtagonists = null;
                _npcStateByPawn = npcPushStates
                    .Where(s => s?.pawn != null)
                    .GroupBy(s => s.pawn)
                    .ToDictionary(g => g.Key, g => g.First());
                CleanupInvalidState();
                AutoSelectDefaultProtagonist();
            }
        }

        public override void GameComponentTick()
        {
            _cachedProtagonists = null;
            _cachedFactionNpcs = null;

            if (Current.ProgramState != ProgramState.Playing || Find.TickManager == null)
            {
                return;
            }

            int currentTick = Find.TickManager.TicksGame;
            TrackClickSignal(currentTick);

            if (!IsFeatureEnabled())
            {
                return;
            }

            // First-tick fallback: auto-select protagonist if list is still empty
            if (proactiveProtagonists == null || proactiveProtagonists.Count == 0)
            {
                AutoSelectDefaultProtagonist();
            }

            if (currentTick % IncomingDrainInterval == 0)
            {
                using (PerfScope.Measure("RpgPush.Drain"))
                    DrainIncomingTriggers(currentTick);
            }

            if (currentTick % QueueProcessInterval == 0)
            {
                using (PerfScope.Measure("RpgPush.QueueProcess"))
                    ProcessQueuedTriggers(currentTick);
            }

            if (currentTick % ThreatScanInterval == 0)
            {
                using (PerfScope.Measure("RpgPush.EvaluateThreat"))
                    EvaluateThreatTriggers(currentTick);
            }

            if (currentTick % RegularEvaluationInterval == 0)
            {
                using (PerfScope.Measure("RpgPush.EvaluateRegular"))
                    EvaluateRegularTriggers(currentTick);
            }
        }

        public void RegisterTradeCompletedTrigger(Faction faction, int soldCount, int boughtCount)
        {
            if (!IsValidTargetFaction(faction) || soldCount <= 0 && boughtCount <= 0)
            {
                return;
            }

            EnqueueIncoming(new PawnRpgTriggerContext
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
            if (!IsValidTargetFaction(faction) || Math.Abs(goodwillDelta) < 10)
            {
                return;
            }

            NpcDialogueCategory category = goodwillDelta < 0
                ? NpcDialogueCategory.WarningThreat
                : NpcDialogueCategory.DiplomacyTask;
            int severity = likelyHostile ? 3 : (goodwillDelta < 0 ? 2 : 1);
            EnqueueIncoming(new PawnRpgTriggerContext
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
            if (!IsValidTargetFaction(faction) || !hasHive && !hasHostiles)
            {
                return;
            }

            EnqueueIncoming(new PawnRpgTriggerContext
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
            if (!HasConfiguredProtagonists())
            {
                LogMissingProtagonists(now);
                return false;
            }

            // Path 1: NPC → colonist
            IReadOnlyCollection<Faction> factions = GetActiveCandidateFactionsOnPlayerMaps(now);
            if (factions.Count > 0)
            {
                foreach (Faction faction in factions.InRandomOrder().ToList())
                {
                    if (!TryResolvePairForFaction(faction, now, true, true, true, out Pawn npcPawn, out Pawn playerPawn))
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
                    StartGeneration(context, npcPawn, playerPawn);
                    return true;
                }
            }

            // Path 2: colonist → colonist (fallback)
            if (TryResolveColonistPair(now, out Pawn initiator, out Pawn receiver, bypassAvailability: true))
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
                StartGeneration(context, initiator, receiver);
                return true;
            }

            Log.Warning("[RimAI.Relations] DebugForcePawnRpg: Both paths failed. No valid pair found.");
            return false;
        }

        public List<Pawn> GetRpgProactiveProtagonists()
        {
            return ResolveConfiguredProtagonists();
        }

        public bool ContainsRpgProactiveProtagonist(Pawn pawn)
        {
            return ResolveConfiguredProtagonists().Contains(pawn);
        }

        public bool TryAddRpgProactiveProtagonist(Pawn pawn)
        {
            if (!CanConfigureAsProtagonist(pawn))
            {
                return false;
            }

            if (ContainsRpgProactiveProtagonist(pawn))
            {
                return true;
            }

            if (GetConfiguredProtagonistCount() >= GetRpgProactiveProtagonistCap())
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
            proactiveProtagonists.RemoveAll(entry => IsSamePawn(entry, pawn));
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

        /// <summary>
        /// Auto-select the colonist with the highest total skills as default protagonist.
        /// Called on PostLoadInit when protagonist list is empty (backward compatibility).
        /// </summary>
        private void AutoSelectDefaultProtagonist()
        {
            if (proactiveProtagonists == null || proactiveProtagonists.Count > 0) return;

            Pawn best = FindBestSkillColonist();
            if (best != null)
            {
                proactiveProtagonists.Add(PawnRpgProtagonistEntry.FromPawn(best));
                _cachedProtagonists = null;
                Log.Message($"[RimAI.Relations] Auto-selected default protagonist: {best.LabelShortCap} (highest skills)");
            }
        }

        private static Pawn FindBestSkillColonist()
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

            List<Pawn> protagonists = ResolveConfiguredProtagonists();
            List<Pawn> result = new List<Pawn>(protagonists.Count);
            HashSet<Pawn> seen = new HashSet<Pawn>();
            for (int i = 0; i < protagonists.Count; i++)
            {
                Pawn pawn = protagonists[i];
                if (pawn != null && IsEligiblePlayerPawn(pawn) && pawn.Map == map && seen.Add(pawn))
                    result.Add(pawn);
            }
            return result;
        }

        private void ClearTransientState()
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

        private bool IsRpgDeliveryWindowFull(int currentTick)
        {
            for (int i = rpgDeliveryTicks.Count - 1; i >= 0; i--)
            {
                if (currentTick - rpgDeliveryTicks[i] > RpgWindowTicks)
                    rpgDeliveryTicks.RemoveAt(i);
            }
            return rpgDeliveryTicks.Count >= RpgWindowMaxMessages;
        }

        private void RecordRpgDelivery(int currentTick)
        {
            rpgDeliveryTicks.Add(currentTick);
        }

        private void CleanupExpiredMessageHashes(int currentTick)
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

        private static string ComputeContentHash(string text)
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

        private void EnqueueIncoming(PawnRpgTriggerContext context)
        {
            if (context == null || !IsValidTargetFaction(context.Faction))
            {
                return;
            }

            incomingTriggers.Enqueue(context);
        }

        private void DrainIncomingTriggers(int currentTick)
        {
            int safeguard = 0;
            while (incomingTriggers.Count > 0 && safeguard++ < 200)
            {
                PawnRpgTriggerContext context = incomingTriggers.Dequeue();
                HandleTriggerContext(context, currentTick);
            }
        }

        private void HandleTriggerContext(PawnRpgTriggerContext context, int currentTick)
        {
            if (context == null || !IsValidTargetFaction(context.Faction))
            {
                return;
            }

            if (!HasConfiguredProtagonists())
            {
                LogMissingProtagonists(currentTick);
                return;
            }

            int dueTick = currentTick;
            if (context.TriggerType == NpcDialogueTriggerType.Causal)
            {
                dueTick += Rand.RangeInclusive(CausalMinDelayTicks, CausalMaxDelayTicks);
            }

            dueTick = Math.Max(dueTick, GetNextAllowedTickForContext(context, currentTick));
            if (IsFactionPending(context.Faction) || IsPlayerBusy())
            {
                dueTick = Math.Max(dueTick, currentTick + BlockedRetryTicks);
            }

            if (dueTick <= currentTick && TryStartGenerationForContext(context, currentTick))
            {
                return;
            }

            QueueTrigger(context, Math.Max(dueTick, currentTick + BlockedRetryTicks), currentTick);
        }

        private void ProcessQueuedTriggers(int currentTick)
        {
            using (PerfScope.Measure("RpgPush.QueueProcess.Cleanup"))
                CleanupExpiredQueue(currentTick);
            if (!HasConfiguredProtagonists())
            {
                if (queuedTriggers.Count > 0)
                {
                    queuedTriggers.Clear();
                }

                LogMissingProtagonists(currentTick);
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

                if (!IsValidTargetFaction(item.faction))
                {
                    queuedTriggers.RemoveAt(i);
                    continue;
                }

                PawnRpgTriggerContext context = item.ToContext();

                using (PerfScope.Measure("RpgPush.QueueProcess.PreGate"))
                {
                    if (IsFactionPending(context.Faction) || IsPlayerBusy())
                    {
                        item.dueTick = currentTick + BlockedRetryTicks;
                        continue;
                    }

                    int nextAllowed = GetNextAllowedTickForContext(context, currentTick);
                    if (nextAllowed > currentTick)
                    {
                        item.dueTick = nextAllowed;
                        continue;
                    }
                }

                bool startResult;
                using (PerfScope.Measure("RpgPush.QueueProcess.Generation"))
                    startResult = TryStartGenerationForContext(context, currentTick);

                if (!startResult)
                {
                    item.dueTick = currentTick + BlockedRetryTicks;
                    continue;
                }

                queuedTriggers.RemoveAt(i);
                processed++;
            }
        }

        private void EvaluateRegularTriggers(int currentTick)
        {
            CleanupQuestTriggerCache(currentTick);
            if (IsRpgDeliveryWindowFull(currentTick))
            {
                return;
            }
            float chance = GetRegularTriggerChance(RelationsMod.Instance?.InstanceSettings?.NpcPushFrequencyMode ?? NpcPushFrequencyMode.Low);
            foreach (Faction faction in GetActiveCandidateFactionsOnPlayerMaps(currentTick))
            {
                if (IsFactionPending(faction))
                {
                    continue;
                }

                if (TryCreateQuestDeadlineContext(faction, currentTick, out PawnRpgTriggerContext questContext))
                {
                    HandleTriggerContext(questContext, currentTick);
                    continue;
                }

                if (TryCreateLowMoodContext(faction, currentTick, out PawnRpgTriggerContext moodContext))
                {
                    HandleTriggerContext(moodContext, currentTick);
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
                HandleTriggerContext(ambientContext, currentTick);
            }

            EvaluateColonistPairAmbientTriggers(currentTick, chance);
            EvaluateColonistPairLowMoodTriggers(currentTick);
            EvaluateHomeEventTriggers(currentTick);
        }

        private void EvaluateThreatTriggers(int currentTick)
        {
            PlayerGameStateCache.Instance.EnsureFresh(currentTick);
            bool hasHostiles = PlayerGameStateCache.Instance.HasHostiles;
            bool hasHive = PlayerGameStateCache.Instance.HasHiveThreat;
            bool hasThreat = hasHostiles || hasHive;

            if (!hasThreat)
            {
                _colonistPairHadThreat = false;
            }

            foreach (Faction faction in GetActiveCandidateFactionsOnPlayerMaps(currentTick))
            {
                PawnRpgThreatState state = GetOrCreateThreatState(faction);
                if (!hasThreat)
                {
                    state.hadThreat = false;
                    continue;
                }

                if (state.hadThreat)
                {
                    continue;
                }

                RegisterThreatStateTrigger(faction, hasHive, hasHostiles);
                state.hadThreat = true;
            }

            if (hasThreat && !_colonistPairHadThreat)
            {
                EvaluateColonistPairThreatTriggers(currentTick, hasHive, hasHostiles);
                _colonistPairHadThreat = true;
            }
        }

        private bool TryStartGenerationForContext(PawnRpgTriggerContext context, int currentTick)
        {
            if (!HasConfiguredProtagonists())
            {
                LogMissingProtagonists(currentTick);
                return false;
            }

            if (IsColonistPairContext(context))
            {
                Pawn initiator, receiver;
                using (PerfScope.Measure("RpgPush.QueueProcess.ResolveColonistPair"))
                {
                    if (!TryResolveColonistPair(currentTick, out initiator, out receiver))
                        return false;
                }

                using (PerfScope.Measure("RpgPush.QueueProcess.StartGeneration"))
                    StartGeneration(context, initiator, receiver);
                return true;
            }

            Pawn npcPawn, playerPawn;
            using (PerfScope.Measure("RpgPush.QueueProcess.ResolvePairForFaction"))
            {
                if (!TryResolvePairForFaction(context.Faction, currentTick, false, false, false, out npcPawn, out playerPawn))
                    return false;
            }

            using (PerfScope.Measure("RpgPush.QueueProcess.StartGeneration"))
                StartGeneration(context, npcPawn, playerPawn);
            return true;
        }

        private bool TryCreateLowMoodContext(Faction faction, int currentTick, out PawnRpgTriggerContext context)
        {
            context = null;
            Pawn worstMoodNpc = null;
            float worstMood = 1f;
            foreach (Pawn npc in GetFactionNpcCandidates(faction))
            {
                if (!TryGetMoodPercent(npc, out float mood) || mood > LowMoodThreshold)
                {
                    continue;
                }

                if (!HasQualifiedPlayerRelation(npc))
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

        private bool TryCreateQuestDeadlineContext(Faction faction, int currentTick, out PawnRpgTriggerContext context)
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

        private void CleanupQuestTriggerCache(int currentTick)
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

        private int GetNextAllowedTickForContext(PawnRpgTriggerContext context, int currentTick)
        {
            int nextTick = GetFactionNpcReadyTick(context?.Faction, currentTick);
            if (!CanBypassGlobalCooldown(context) && lastColonyDeliveredTick > 0)
            {
                nextTick = Math.Max(nextTick, lastColonyDeliveredTick + ColonyDeliveryCooldownTicks);
            }

            return nextTick;
        }

        private bool CanBypassGlobalCooldown(PawnRpgTriggerContext context)
        {
            return context != null && context.Category == NpcDialogueCategory.WarningThreat;
        }

        private void QueueTrigger(PawnRpgTriggerContext context, int dueTick, int nowTick)
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

        private void CleanupExpiredQueue(int currentTick)
        {
            for (int i = queuedTriggers.Count - 1; i >= 0; i--)
            {
                var q = queuedTriggers[i];
                if (q == null || q.faction == null || q.faction.defeated || q.expireTick <= currentTick)
                    queuedTriggers.RemoveAt(i);
            }
        }

        private bool IsFeatureEnabled()
        {
            RelationsSettings settings = RelationsMod.Instance?.InstanceSettings;
            return settings != null && settings.EnablePawnRpgInitiatedDialogue && settings.EnableRPGDialogue;
        }

        private bool IsValidTargetFaction(Faction faction)
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

        private bool IsFactionPending(Faction faction)
        {
            return faction != null && factionsWithPendingRequests.Contains(faction);
        }

        private void CleanupInvalidState()
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

        private bool HasConfiguredProtagonists()
        {
            if (proactiveProtagonists == null) return false;
            for (int i = 0; i < proactiveProtagonists.Count; i++)
            {
                if (proactiveProtagonists[i]?.HasConfiguredIdentifier == true)
                    return true;
            }
            return false;
        }

        private List<Pawn> ResolveConfiguredProtagonists()
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

        private bool CanConfigureAsProtagonist(Pawn pawn)
        {
            return pawn != null &&
                   pawn.Faction == Faction.OfPlayer &&
                   !pawn.Destroyed &&
                   !pawn.Dead;
        }

        private static bool IsSamePawn(PawnRpgProtagonistEntry entry, Pawn pawn)
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

        private void LogMissingProtagonists(int currentTick)
        {
            if (currentTick - lastMissingProtagonistLogTick < MissingProtagonistLogIntervalTicks)
            {
                return;
            }

            lastMissingProtagonistLogTick = currentTick;
            Log.Warning("[RimAI.Relations] PawnRPG proactive skipped: protagonist list is empty. Configure protagonists in NPC proactive dialogue settings.");
        }

        private PawnRpgThreatState GetOrCreateThreatState(Faction faction)
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

        private float GetRegularTriggerChance(NpcPushFrequencyMode mode)
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

