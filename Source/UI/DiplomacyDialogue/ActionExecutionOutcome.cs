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



internal sealed class ActionExecutionOutcome
{
    public AIAction Action { get; private set; }
    public bool IsSuccess { get; private set; }
    public string Message { get; private set; }
    public object Data { get; private set; }

    public static ActionExecutionOutcome Success(AIAction action, string message, object data = null)
    {
        return new ActionExecutionOutcome
        {
            Action = action,
            IsSuccess = true,
            Message = message ?? string.Empty,
            Data = data
        };
    }

    public static ActionExecutionOutcome Failure(AIAction action, string message)
    {
        return new ActionExecutionOutcome
        {
            Action = action,
            IsSuccess = false,
            Message = message ?? string.Empty,
            Data = null
        };
    }
}

