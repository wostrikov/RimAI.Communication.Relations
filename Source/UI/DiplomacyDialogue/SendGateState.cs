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



internal readonly struct SendGateState
{
    public readonly bool CanSendNow;
    public readonly bool IsHardBlocked;
    public readonly bool IsSoftBlocked;
    public readonly bool ShowReinitiateButton;
    public readonly string BlockedReason;

    public SendGateState(
        bool canSendNow,
        bool isHardBlocked,
        bool isSoftBlocked,
        bool showReinitiateButton,
        string blockedReason)
    {
        CanSendNow = canSendNow;
        IsHardBlocked = isHardBlocked;
        IsSoftBlocked = isSoftBlocked;
        ShowReinitiateButton = showReinitiateButton;
        BlockedReason = blockedReason;
    }
}

