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
    internal sealed class DiplomacyManagerSlice1 : GameComponent_DiplomacyManagerCollaborator
    {
        internal DiplomacyManagerSlice1(GameComponent_DiplomacyManager owner) : base(owner)
        {
        }

public static bool ShouldExcludeFactionFromAI(Faction faction)
        {
            if (faction == null) return true;
            if (faction.IsPlayer || faction.defeated) return true;
            if (faction.def?.hidden ?? true) return true;

            string csv = RelationsMod.Settings?.FactionExclusionDefNamesCsv;
            if (string.IsNullOrWhiteSpace(csv)) return false;

            HashSet<string> excluded = GameComponent_DiplomacyManager.ParseFactionExclusionCsv(csv);
            return faction.def != null && excluded.Contains(faction.def.defName);
        }

internal static HashSet<string> ParseFactionExclusionCsv(string csv)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(csv)) return set;

            string[] tokens = csv.Split(new[] { ',', ';', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                string trimmed = tokens[i].Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                    set.Add(trimmed);
            }
            return set;
        }

internal void InitializeAIControlledFactions()
        {
            foreach (var f in Find.FactionManager.AllFactions)
            {
                if (GameComponent_DiplomacyManager.ShouldExcludeFactionFromAI(f)) continue;
                aiControlledFactions.Add(f);
            }
        }

internal void InitializeDialogueSessions()
        {
            foreach (var f in Find.FactionManager.AllFactions)
            {
                if (GameComponent_DiplomacyManager.ShouldExcludeFactionFromAI(f)) continue;
                Owner.GetOrCreateSession(f);
            }
        }

internal void InitializePresenceStates()
        {
            foreach (var f in Find.FactionManager.AllFactions)
            {
                if (GameComponent_DiplomacyManager.ShouldExcludeFactionFromAI(f)) continue;
                Owner.GetOrCreatePresenceState(f);
            }
        }

internal void CleanupInvalidSessions()
        {
            dialogueSessions.RemoveAll(s => s.faction == null || s.faction.defeated);
            Owner.RebuildDialogueSessionIndex();
        }

internal void CleanupInvalidPresenceStates()
        {
            presenceStates.RemoveAll(s => s.faction == null || s.faction.defeated);
            Owner.RebuildPresenceStateIndex();
        }

internal void RebuildDialogueSessionIndex()
        {
            dialogueSessionsByFaction.Clear();
            if (dialogueSessions == null) return;
            for (int i = 0; i < dialogueSessions.Count; i++)
            {
                var session = dialogueSessions[i];
                if (session?.faction != null)
                    dialogueSessionsByFaction[session.faction] = session;
            }
        }

internal void RebuildPresenceStateIndex()
        {
            presenceStatesByFaction.Clear();
            if (presenceStates == null) return;
            for (int i = 0; i < presenceStates.Count; i++)
            {
                var state = presenceStates[i];
                if (state?.faction != null)
                    presenceStatesByFaction[state.faction] = state;
            }
        }

public FactionDialogueSession GetOrCreateSession(Faction faction)
        {
            if (faction == null) return null;

            if (dialogueSessionsByFaction.TryGetValue(faction, out var session))
                return session;

            session = new FactionDialogueSession(faction);
            dialogueSessions.Add(session);
            dialogueSessionsByFaction[faction] = session;
            ModuleLog.Message($"[RimAI.Relations] Created dialogue session for {faction.Name}");
            return session;
        }

public FactionDialogueSession GetSession(Faction faction)
        {
            if (faction == null) return null;
            dialogueSessionsByFaction.TryGetValue(faction, out var session);
            return session;
        }

public bool HandleInboundFactionMessage(
            Faction faction,
            string sender,
            string message,
            DialogueMessageType messageType,
            Pawn speakerPawn = null,
            bool markUnread = true,
            bool forcePresenceOnline = true)
        {
            if (faction == null || string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            FactionDialogueSession session = Owner.GetOrCreateSession(faction);
            if (session == null)
            {
                return false;
            }

            if (forcePresenceOnline)
            {
                Owner.ForcePresenceOnlineForNpcInitiated(faction);
                Owner.EnsureConversationReopenedOnInbound(session, faction);
            }
            else
            {
                Owner.EnsureConversationReopenedOnInbound(session, faction);
            }

            string resolvedSender = string.IsNullOrWhiteSpace(sender)
                ? (faction.leader?.Name?.ToStringShort ?? faction.Name ?? "Unknown")
                : sender;
            session.AddMessage(resolvedSender, message, false, messageType, speakerPawn);
            session.hasUnreadMessages = markUnread;
            LeaderMemoryManager.Instance?.UpdateFromDialogue(faction, session.messages);
            return true;
        }

internal void EnsureConversationReopenedOnInbound(
            FactionDialogueSession session,
            Faction faction)
        {
            if (session == null || !session.isConversationEndedByNpc)
            {
                return;
            }

            session.ReinitiateConversation();

            // Keep an explicit audit trail when inbound messages reopen an ended dialogue.
            session.AddMessage(
                "System",
                "RimChat_ConversationReinitiatedByNpc".Translate().ToString(),
                false,
                DialogueMessageType.System);
        }

public FactionPresenceState GetOrCreatePresenceState(Faction faction)
        {
            if (faction == null) return null;

            if (presenceStatesByFaction.TryGetValue(faction, out var state))
                return state;

            state = new FactionPresenceState(faction);
            presenceStates.Add(state);
            presenceStatesByFaction[faction] = state;
            return state;
        }

public FactionPresenceState GetPresenceState(Faction faction)
        {
            if (faction == null) return null;
            presenceStatesByFaction.TryGetValue(faction, out var state);
            return state;
        }

public FactionPresenceStatus GetPresenceStatus(Faction faction)
        {
            var state = Owner.GetOrCreatePresenceState(faction);
            return state?.status ?? FactionPresenceStatus.Online;
        }

public void ForcePresenceOnlineForNpcInitiated(Faction faction)
        {
            if (faction == null)
            {
                return;
            }

            FactionPresenceState state = Owner.GetOrCreatePresenceState(faction);
            if (state == null)
            {
                return;
            }

            bool wasUnavailable = state.status != FactionPresenceStatus.Online;
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            state.status = FactionPresenceStatus.Online;
            state.lastReason = string.Empty;
            state.forcedOfflineUntilTick = 0;
            state.doNotDisturbUntilTick = 0;
            int cacheTicks = Owner.GetPresenceCacheTicks();
            state.cacheUntilTick = currentTick + cacheTicks;
            state.lastResolvedTick = currentTick;
            if (wasUnavailable)
            {
                NpcDialogue.GameComponent_NpcDialoguePushManager.Instance?.CancelQueuedTriggersForFaction(
                    faction,
                    "presence_recovered_force_online");
            }
        }

public void RefreshPresenceOnDialogueOpen(Faction faction)
        {
            var state = Owner.GetOrCreatePresenceState(faction);
            if (state == null) return;

            FactionPresenceStatus previousStatus = state.status;
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            Owner.EnforcePresenceForcedDurationCaps(state, currentTick);
            if (!Owner.IsPresenceEnabled())
            {
                state.status = FactionPresenceStatus.Online;
                state.lastReason = string.Empty;
                state.lastResolvedTick = currentTick;
                GameComponent_DiplomacyManager.HandlePresenceRecoveryQueueCleanup(faction, previousStatus, state.status);
                return;
            }

            bool forcedExpired = state.forcedOfflineUntilTick > 0 && state.forcedOfflineUntilTick <= currentTick;
            bool doNotDisturbExpired = state.doNotDisturbUntilTick > 0 && state.doNotDisturbUntilTick <= currentTick;
            if (forcedExpired || doNotDisturbExpired)
            {
                state.status = FactionPresenceStatus.Online;
                state.lastReason = string.Empty;
                state.forcedOfflineUntilTick = 0;
                state.doNotDisturbUntilTick = 0;
                state.lastResolvedTick = currentTick;
                state.cacheUntilTick = currentTick + Owner.GetPresenceCacheTicks();
                GameComponent_DiplomacyManager.HandlePresenceRecoveryQueueCleanup(faction, previousStatus, state.status);
                return;
            }

            if (state.IsForcedOffline(currentTick))
            {
                state.status = FactionPresenceStatus.Offline;
                state.lastResolvedTick = currentTick;
                return;
            }

            if (state.IsDoNotDisturb(currentTick))
            {
                state.status = FactionPresenceStatus.DoNotDisturb;
                state.lastResolvedTick = currentTick;
                return;
            }

            if (state.IsCacheValid(currentTick))
            {
                GameComponent_DiplomacyManager.HandlePresenceRecoveryQueueCleanup(faction, previousStatus, state.status);
                return;
            }

            state.status = Owner.EvaluateScheduledPresence(faction, currentTick, out string reason);
            state.lastReason = reason ?? string.Empty;
            state.lastResolvedTick = currentTick;
            GameComponent_DiplomacyManager.HandlePresenceRecoveryQueueCleanup(faction, previousStatus, state.status);
        }

internal static void HandlePresenceRecoveryQueueCleanup(
            Faction faction,
            FactionPresenceStatus previousStatus,
            FactionPresenceStatus currentStatus)
        {
            if (faction == null || previousStatus == FactionPresenceStatus.Online || currentStatus != FactionPresenceStatus.Online)
            {
                return;
            }

            NpcDialogue.GameComponent_NpcDialoguePushManager.Instance?.CancelQueuedTriggersForFaction(
                faction,
                "presence_recovered_refresh");
        }

internal void EnforcePresenceForcedDurationCaps(FactionPresenceState state, int currentTick)
        {
            if (state == null)
            {
                return;
            }

            int forcedOfflineCapTick = currentTick + Owner.GetPresenceForcedOfflineTicks();
            if (state.forcedOfflineUntilTick > forcedOfflineCapTick)
            {
                state.forcedOfflineUntilTick = forcedOfflineCapTick;
            }

            int doNotDisturbCapTick = currentTick + Owner.GetPresenceDoNotDisturbTicks();
            if (state.doNotDisturbUntilTick > doNotDisturbCapTick)
            {
                state.doNotDisturbUntilTick = doNotDisturbCapTick;
            }
        }

public void RefreshPresenceForFactions(IEnumerable<Faction> factions)
        {
            if (factions == null) return;
            foreach (var faction in factions)
            {
                Owner.RefreshPresenceOnDialogueOpen(faction);
            }
        }

public void LockPresenceCacheOnDialogueClose(Faction faction)
        {
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            int cacheTicks = Owner.GetPresenceCacheTicks();
            if (currentTick <= 0 || cacheTicks <= 0) return;

            var state = Owner.GetOrCreatePresenceState(faction);
            if (state == null) return;
            if (state.cacheUntilTick > currentTick)
            {
                return;
            }
            if (state.forcedOfflineUntilTick > currentTick)
            {
                state.cacheUntilTick = Math.Max(state.cacheUntilTick, state.forcedOfflineUntilTick);
                return;
            }

            if (state.doNotDisturbUntilTick > currentTick)
            {
                state.cacheUntilTick = Math.Max(state.cacheUntilTick, state.doNotDisturbUntilTick);
                return;
            }
            state.cacheUntilTick = Math.Max(state.cacheUntilTick, currentTick + cacheTicks);
        }

public void LockPresenceCacheOnDialogueClose(IEnumerable<Faction> factions)
        {
            if (factions == null) return;
            foreach (var faction in factions)
            {
                Owner.LockPresenceCacheOnDialogueClose(faction);
            }
        }

public void ApplyPresenceAction(Faction faction, string actionType, string reason, FactionDialogueSession session)
        {
            if (faction == null || string.IsNullOrEmpty(actionType)) return;

            int currentTick = Find.TickManager?.TicksGame ?? 0;
            var state = Owner.GetOrCreatePresenceState(faction);
            if (state == null) return;

            string normalizedReason = reason ?? string.Empty;
            switch (actionType)
            {
                case "exit_dialogue":
                    if (session == null || !session.isConversationEndedByNpc)
                    {
                        session?.MarkConversationEnded(normalizedReason, true, 1 * GenDate.TicksPerHour);
                    }
                    break;
                case "go_offline":
                    state.status = FactionPresenceStatus.Offline;
                    state.lastReason = normalizedReason;
                    state.lastResolvedTick = currentTick;
                    state.forcedOfflineUntilTick = currentTick + Owner.GetPresenceForcedOfflineTicks();
                    state.doNotDisturbUntilTick = 0;
                    state.cacheUntilTick = Math.Max(state.cacheUntilTick, state.forcedOfflineUntilTick);
                    session?.MarkConversationEnded(normalizedReason, false);
                    NpcDialogue.GameComponent_NpcDialoguePushManager.Instance?.CancelQueuedTriggersForFaction(faction);
                    break;
                case "set_dnd":
                    state.status = FactionPresenceStatus.DoNotDisturb;
                    state.lastReason = normalizedReason;
                    state.lastResolvedTick = currentTick;
                    state.forcedOfflineUntilTick = 0;
                    state.doNotDisturbUntilTick = currentTick + Owner.GetPresenceDoNotDisturbTicks();
                    state.cacheUntilTick = Math.Max(state.cacheUntilTick, state.doNotDisturbUntilTick);
                    session?.MarkConversationEnded(normalizedReason, false);
                    NpcDialogue.GameComponent_NpcDialoguePushManager.Instance?.CancelQueuedTriggersForFaction(faction);
                    break;
            }
        }

public bool HasUnreadMessages(Faction faction)
        {
            var session = Owner.GetSession(faction);
            return session?.hasUnreadMessages ?? false;
        }

public List<Faction> GetFactionsWithDialogue()
        {
            var result = new List<Faction>();
            for (int i = 0; i < dialogueSessions.Count; i++)
            {
                var s = dialogueSessions[i];
                if (s.faction != null && !s.faction.defeated && s.messages.Count > 0)
                    result.Add(s.faction);
            }
            return result;
        }
    }
}
