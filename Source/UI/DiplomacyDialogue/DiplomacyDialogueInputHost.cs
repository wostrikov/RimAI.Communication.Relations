using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Guards;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.PawnRpgPush;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class DiplomacyDialogueInputHost : DiplomacyDialogueCollaborator
{
    internal DiplomacyDialogueInputHost(Dialog_DiplomacyDialogue owner) : base(owner) { }

internal float inputHostBlockedUntilRealtime = -1f;



internal bool ShouldRenderInputAsReadOnly(SendGateState sendGate)
{
    return sendGate.IsHardBlocked || sendGate.IsSoftBlocked;
}



internal bool IsAiTurnInputHostOwned()
{
    if (session == null)
    {
        return false;
    }

    return session.isWaitingForResponse ||
           session.HasPendingImageRequests() ||
           Owner.Parts.Input.HasActiveNpcTypewriter() ||
           Owner.Parts.StrategyUi.strategySuggestionRequestPending;
}



internal void RefreshInputHostReactivationBarrier(bool aiTurnOwnsInputHost)
{
    if (!aiTurnOwnsInputHost)
    {
        return;
    }

    inputHostBlockedUntilRealtime = Time.realtimeSinceStartup + DiplomacyDialogueInput.InputHostReactivationStabilizationSeconds;
}



internal bool IsInputHostReactivationStabilizing()
{
    return inputHostBlockedUntilRealtime > 0f &&
           Time.realtimeSinceStartup < inputHostBlockedUntilRealtime;
}



internal string BuildAiTurnInputLockReason()
{
    return Owner.Parts.Feedback.BuildAiTurnStatusText();
}
}
