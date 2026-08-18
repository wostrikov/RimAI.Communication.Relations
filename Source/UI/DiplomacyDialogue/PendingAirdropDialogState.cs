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



internal sealed class PendingAirdropDialogState
{
    public FactionDialogueSession Session;
    public Faction Faction;
    public ItemAirdropPreparedTradeData PreparedTrade;
    public Dictionary<string, object> BaseParameters;
    public List<PendingAirdropSelectionCandidate> PendingCandidates;
    public float ReadyAtRealtime = -1f;
    public bool DelayStarted;
    public bool WaitingForTypewriterLogged;
    public bool DelayWindowLogged;
    public float TypewriterWaitStartRealtime = -1f;
}

