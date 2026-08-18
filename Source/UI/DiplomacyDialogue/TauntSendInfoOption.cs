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


internal sealed class TauntSendInfoOption
{
    public TauntSendInfoOption(
        string labelKey,
        string descriptionKey,
        string raidLabelKey,
        string forcedActionType,
        bool requiresConfirmation,
        bool requiresRandomWaves = false,
        bool explicitChallengeRequest = false)
    {
        LabelKey = labelKey;
        DescriptionKey = descriptionKey;
        RaidLabelKey = raidLabelKey;
        ForcedActionType = forcedActionType;
        RequiresConfirmation = requiresConfirmation;
        RequiresRandomWaves = requiresRandomWaves;
        ExplicitChallengeRequest = explicitChallengeRequest;
    }

    public string LabelKey { get; }

    public string DescriptionKey { get; }

    public string RaidLabelKey { get; }

    public string ForcedActionType { get; }

    public bool RequiresConfirmation { get; }

    public bool RequiresRandomWaves { get; }

    public bool ExplicitChallengeRequest { get; }
}

