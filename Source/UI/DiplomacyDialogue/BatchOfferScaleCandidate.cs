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



internal sealed class BatchOfferScaleCandidate
{
    public BatchOfferScaleCandidate(
        AIAction action,
        int index,
        int weight,
        int normalizedOffer,
        double fractionRemainder)
    {
        Action = action;
        Index = Math.Max(0, index);
        Weight = Math.Max(1, weight);
        NormalizedOffer = Math.Max(1, normalizedOffer);
        FractionRemainder = Math.Max(0d, fractionRemainder);
    }

    public AIAction Action { get; }
    public int Index { get; }
    public int Weight { get; }
    public int NormalizedOffer { get; set; }
    public double FractionRemainder { get; }
}

