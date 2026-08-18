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



internal sealed class BatchRansomExecutionPlan
{
    private BatchRansomExecutionPlan()
    {
    }

    public bool IsActive { get; private set; }
    public bool IsValid { get; private set; }
    public string ValidationMessage { get; private set; } = string.Empty;
    public List<AIAction> RansomActions { get; private set; } = new List<AIAction>();
    private Dictionary<AIAction, int> actionTargetIds = new Dictionary<AIAction, int>();
    public PendingRansomBatchSelection BatchSelection { get; private set; }

    public static BatchRansomExecutionPlan Inactive()
    {
        return new BatchRansomExecutionPlan
        {
            IsActive = false,
            IsValid = true
        };
    }

    public static BatchRansomExecutionPlan Invalid(List<AIAction> ransomActions, string message)
    {
        return new BatchRansomExecutionPlan
        {
            IsActive = true,
            IsValid = false,
            ValidationMessage = string.IsNullOrWhiteSpace(message)
                ? "RimChat_RansomSystemUnavailableSystem".Translate().ToString()
                : message,
            RansomActions = ransomActions ?? new List<AIAction>()
        };
    }

    public static BatchRansomExecutionPlan Valid(
        List<AIAction> ransomActions,
        Dictionary<AIAction, int> actionTargetIds,
        PendingRansomBatchSelection batchSelection)
    {
        return new BatchRansomExecutionPlan
        {
            IsActive = true,
            IsValid = true,
            RansomActions = ransomActions ?? new List<AIAction>(),
            actionTargetIds = actionTargetIds ?? new Dictionary<AIAction, int>(),
            BatchSelection = batchSelection
        };
    }

    public bool TryGetTargetPawnLoadId(AIAction action, out int targetPawnLoadId)
    {
        targetPawnLoadId = 0;
        return action != null &&
            actionTargetIds != null &&
            actionTargetIds.TryGetValue(action, out targetPawnLoadId) &&
            targetPawnLoadId > 0;
    }
}

