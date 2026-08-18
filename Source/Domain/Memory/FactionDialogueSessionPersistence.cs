using System.Collections.Generic;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Memory
{
    /// <summary>
    /// Persistence (ExposeData) for FactionDialogueSession save fields.
    /// </summary>
    internal static class FactionDialogueSessionPersistence
    {
        internal static void ExposeData(FactionDialogueSession session)
        {
            Scribe_References.Look(ref session.faction, "faction");
            Scribe_Collections.Look(ref session.messages, "messages", LookMode.Deep);
            Scribe_Values.Look(ref session.lastInteractionTick, "lastInteractionTick", 0);
            Scribe_Values.Look(ref session.hasUnreadMessages, "hasUnreadMessages", false);
            Scribe_Values.Look(ref session.isConversationEndedByNpc, "isConversationEndedByNpc", false);
            Scribe_Values.Look(ref session.allowReinitiate, "allowReinitiate", false);
            Scribe_Values.Look(ref session.conversationEndReason, "conversationEndReason", "");
            Scribe_Values.Look(ref session.conversationEndedTick, "conversationEndedTick", 0);
            Scribe_Values.Look(ref session.reinitiateAvailableTick, "reinitiateAvailableTick", 0);
            Scribe_Values.Look(ref session.lastSummarizedMessageIndex, "lastSummarizedMessageIndex", 0);
            Scribe_Values.Look(ref session.messageVersion, "messageVersion", 0);
            Scribe_Values.Look(ref session.lastAirdropCounterofferDefName, "lastAirdropCounterofferDefName", string.Empty);
            Scribe_Values.Look(ref session.lastAirdropCounterofferCount, "lastAirdropCounterofferCount", 0);
            Scribe_Values.Look(ref session.lastAirdropCounterofferSilver, "lastAirdropCounterofferSilver", 0);
            Scribe_Values.Look(ref session.lastAirdropCounterofferReason, "lastAirdropCounterofferReason", string.Empty);
            Scribe_Values.Look(ref session.lastAirdropCounterofferTick, "lastAirdropCounterofferTick", 0);
            Scribe_Values.Look(ref session.hasPendingRansomBatchSelection, "hasPendingRansomBatchSelection", false);
            Scribe_Values.Look(ref session.pendingRansomBatchGroupId, "pendingRansomBatchGroupId", string.Empty);
            Scribe_Collections.Look(ref session.pendingRansomBatchTargetPawnLoadIds, "pendingRansomBatchTargetPawnLoadIds", LookMode.Value);
            Scribe_Values.Look(ref session.pendingRansomBatchTotalCurrentAskSilver, "pendingRansomBatchTotalCurrentAskSilver", 0);
            Scribe_Values.Look(ref session.pendingRansomBatchTotalMinOfferSilver, "pendingRansomBatchTotalMinOfferSilver", 0);
            Scribe_Values.Look(ref session.pendingRansomBatchTotalMaxOfferSilver, "pendingRansomBatchTotalMaxOfferSilver", 0);
            session.pendingRansomBatchTargetPawnLoadIds ??= new List<int>();
        }
    }
}
