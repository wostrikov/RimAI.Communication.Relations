using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using Verse;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Relation;

namespace Ustas.RimAI.Communication.Relations.AI
{
    public class AIActionExecutor
    {
        internal AIActionExecutorParts Parts;

        internal readonly Faction faction;
        internal readonly GameAIInterface gameInterface;
        internal readonly bool applyDialogueApiGoodwillCost;

        public AIActionExecutor(Faction faction, bool applyDialogueApiGoodwillCost = false)
        {
            Parts = new AIActionExecutorParts(this);
            this.faction = faction;
            this.gameInterface = GameAIInterface.Instance;
            this.applyDialogueApiGoodwillCost = applyDialogueApiGoodwillCost;
        }

        internal ActionResult ExecuteRequestItemAirdrop(AIAction action) => Parts.ItemAirdrop.ExecuteRequestItemAirdrop(action);
        internal ActionResult ExecutePayPrisonerRansom(AIAction action) => Parts.PrisonerRansom.ExecutePayPrisonerRansom(action);

        public ActionResult ExecuteAction(AIAction action)
        {
            return ActionResult.Failure("AIActionExecutor is retired. Use RelationsInteractionAdapter.");
        }

        [System.Obsolete("Production diplomacy uses RelationsInteractionAdapter.")]
        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        internal static int ReadIntParameterOrDefault(Dictionary<string, object> parameters, string key, int defaultValue)
        {
            return TryReadIntParameter(parameters, key, out int value) ? value : defaultValue;
        }

        

        
    
        #region Cluster forwards
        internal ActionResult ExecuteActionLegacy(AIAction action) => Parts.Slice1.ExecuteActionLegacy(action);
        internal ActionResult ApplyDialogueApiGoodwillCostIfNeeded(AIAction action, ActionResult result) => Parts.Slice1.ApplyDialogueApiGoodwillCostIfNeeded(action, result);
        internal static bool ShouldApplyDialogueApiGoodwillCost(AIAction action) => AIActionExecutorSlice1.ShouldApplyDialogueApiGoodwillCost(action);
        internal static bool TryReadApplyGoodwillCostParameter(Dictionary<string, object> parameters, out bool value) => AIActionExecutorSlice1.TryReadApplyGoodwillCostParameter(parameters, out value);
        internal static string BuildDialogueApiCostDetail(AIAction action) => AIActionExecutorSlice1.BuildDialogueApiCostDetail(action);
        internal static DialogueGoodwillCost.DialogueActionType ResolveAidDialogueCostType(AIAction action) => AIActionExecutorSlice1.ResolveAidDialogueCostType(action);
        internal static string ReadDetail(Dictionary<string, object> parameters, params string[] keys) => AIActionExecutorSlice1.ReadDetail(parameters, keys);
        internal bool IsFeatureEnabled(string actionType) => Parts.Slice1.IsFeatureEnabled(actionType);
        internal ActionResult ExecuteTriggerIncident(AIAction action) => Parts.Slice1.ExecuteTriggerIncident(action);
        internal ActionResult ExecuteCreateQuest(AIAction action) => Parts.Slice1.ExecuteCreateQuest(action);
        internal string BuildCreateQuestFailureMessage(QuestValidationResult validation, Dictionary<string, object> parameters) => Parts.Slice1.BuildCreateQuestFailureMessage(validation, parameters);
        internal ActionResult ExecuteAdjustGoodwill(AIAction action) => Parts.Slice1.ExecuteAdjustGoodwill(action);
        internal static int TryReadGoodwillChangeFromResult(object resultData, int fallbackAmount) => AIActionExecutorSlice1.TryReadGoodwillChangeFromResult(resultData, fallbackAmount);
        internal ActionResult ExecuteSendGift(AIAction action) => Parts.Slice1.ExecuteSendGift(action);
        internal ActionResult ExecuteRequestAid(AIAction action) => Parts.Slice2.ExecuteRequestAid(action);
        internal ActionResult ExecuteDeclareWar(AIAction action) => Parts.Slice2.ExecuteDeclareWar(action);
        internal ActionResult ExecuteMakePeace(AIAction action) => Parts.Slice2.ExecuteMakePeace(action);
        internal ActionResult ExecuteRequestCaravan(AIAction action) => Parts.Slice2.ExecuteRequestCaravan(action);
        internal ActionResult ExecuteRequestVisitor(AIAction action) => Parts.Slice2.ExecuteRequestVisitor(action);
        internal ActionResult ExecuteRejectRequest(AIAction action) => Parts.Slice2.ExecuteRejectRequest(action);
        internal ActionResult ExecuteRequestRaid(AIAction action) => Parts.Slice2.ExecuteRequestRaid(action);
        internal ActionResult ExecuteRequestRaidCallEveryone(AIAction action) => Parts.Slice2.ExecuteRequestRaidCallEveryone(action);
        internal ActionResult ExecuteRequestRaidWaves(AIAction action) => Parts.Slice2.ExecuteRequestRaidWaves(action);
        internal static string ReadStringParameterOrDefault(Dictionary<string, object> parameters, string key, string defaultValue) => AIActionExecutorSlice2.ReadStringParameterOrDefault(parameters, key, defaultValue);
        internal static bool TryReadIntParameter(Dictionary<string, object> parameters, string key, out int value) => AIActionExecutorSlice2.TryReadIntParameter(parameters, key, out value);
        internal static bool TryReadFloatParameter(Dictionary<string, object> parameters, string key, out float value) => AIActionExecutorSlice2.TryReadFloatParameter(parameters, key, out value);
        #endregion
}
    internal sealed class AIActionExecutorSlice1 : AIActionExecutorCollaborator
    {
        internal AIActionExecutorSlice1(AIActionExecutor owner) : base(owner)
        {
        }

internal ActionResult ExecuteActionLegacy(AIAction action)
        {
            if (action == null)
            {
                return ActionResult.Failure("Action is null");
            }

            Log.Message($"[RimAI.Relations] Executing AI action: {action.ActionType}");
            if (action.Parameters == null)
            {
                action.Parameters = new Dictionary<string, object>();
            }

            if (!gameInterface.ValidateAIPermission(faction))
            {
                return ActionResult.Failure("AI does not have permission to interact with this faction");
            }

            if (!ApiActionEligibilityService.IsFeatureEnabled(action.ActionType))
            {
                return ActionResult.Failure($"Feature {action.ActionType} is disabled in settings");
            }

            var validation = ApiActionEligibilityService.Instance.ValidateActionExecution(faction, action.ActionType, action.Parameters);
            if (!validation.Allowed)
            {
                return ActionResult.Failure(validation.Message);
            }

            try
            {
                ActionResult result = action.ActionType switch
                {
                    AIActionNames.AdjustGoodwill => Owner.ExecuteAdjustGoodwill(action),
                    AIActionNames.SendGift => Owner.ExecuteSendGift(action),
                    AIActionNames.RequestAid => Owner.ExecuteRequestAid(action),
                    AIActionNames.DeclareWar => Owner.ExecuteDeclareWar(action),
                    AIActionNames.MakePeace => Owner.ExecuteMakePeace(action),
                    AIActionNames.RequestCaravan => Owner.ExecuteRequestCaravan(action),
                    AIActionNames.RequestVisitor => Owner.ExecuteRequestVisitor(action),
                    AIActionNames.RequestRaid => Owner.ExecuteRequestRaid(action),
                    AIActionNames.RequestItemAirdrop => Owner.ExecuteRequestItemAirdrop(action),
                    AIActionNames.RequestInfo => ActionResult.Failure("request_info must be handled by diplomacy dialogue pipeline."),
                    AIActionNames.PayPrisonerRansom => Owner.ExecutePayPrisonerRansom(action),
                    AIActionNames.RejectRequest => Owner.ExecuteRejectRequest(action),
                    AIActionNames.TriggerIncident => Owner.ExecuteTriggerIncident(action),
                    AIActionNames.CreateQuest => Owner.ExecuteCreateQuest(action),
                    AIActionNames.SendImage => ActionResult.Failure("send_image must be handled by diplomacy dialogue pipeline."),
                    AIActionNames.RequestRaidCallEveryone => Owner.ExecuteRequestRaidCallEveryone(action),
                    AIActionNames.RequestRaidWaves => Owner.ExecuteRequestRaidWaves(action),
                    _ => ActionResult.Failure($"Unknown action type: {action.ActionType}")
                };

                return Owner.ApplyDialogueApiGoodwillCostIfNeeded(action, result);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Relations] Error executing action {action.ActionType}: {ex}");
                return ActionResult.Failure($"Execution error: {ex.Message}");
            }
        }

internal ActionResult ApplyDialogueApiGoodwillCostIfNeeded(AIAction action, ActionResult result)
        {
            if (!applyDialogueApiGoodwillCost || action == null || result == null || !result.IsSuccess)
            {
                return result;
            }

            if (!AIActionExecutor.ShouldApplyDialogueApiGoodwillCost(action))
            {
                return result;
            }

            DialogueGoodwillCost.DialogueActionType? costType = action.ActionType switch
            {
                AIActionNames.RequestAid => AIActionExecutor.ResolveAidDialogueCostType(action),
                AIActionNames.RequestCaravan => DialogueGoodwillCost.DialogueActionType.RequestCaravan,
                _ => null
            };

            if (!costType.HasValue)
            {
                return result;
            }

            string detail = AIActionExecutor.BuildDialogueApiCostDetail(action);
            var costResult = gameInterface.ApplySuccessfulDialogueApiGoodwillCost(faction, costType.Value, action.ActionType, detail);
            if (!costResult.Success)
            {
                Log.Warning($"[RimAI.Relations] Fixed dialogue API goodwill cost failed for {action.ActionType}: {costResult.Message}");
                return result;
            }

            var costData = costResult.Data as GameAIInterface.DialogueApiGoodwillCostResult;
            result.Data = new ActionExecutionDetails
            {
                ApiData = result.Data,
                DialogueCost = costData
            };

            if (!string.IsNullOrWhiteSpace(costResult.Message))
            {
                result.Message = $"{result.Message} {costResult.Message}".Trim();
            }

            return result;
        }

internal static bool ShouldApplyDialogueApiGoodwillCost(AIAction action)
        {
            if (action == null)
            {
                return false;
            }

            switch (action.ActionType)
            {
                case AIActionNames.RequestAid:
                case AIActionNames.RequestCaravan:
                    return AIActionExecutor.TryReadApplyGoodwillCostParameter(action.Parameters, out bool shouldApply)
                        ? shouldApply
                        : false;
                case AIActionNames.CreateQuest:
                    return false;
                default:
                    return true;
            }
        }

internal static bool TryReadApplyGoodwillCostParameter(Dictionary<string, object> parameters, out bool value)
        {
            value = false;
            if (parameters == null ||
                !parameters.TryGetValue("apply_goodwill_cost", out object raw) ||
                raw == null)
            {
                return false;
            }

            switch (raw)
            {
                case bool boolValue:
                    value = boolValue;
                    return true;
                case byte byteValue:
                    if (byteValue == 0 || byteValue == 1)
                    {
                        value = byteValue == 1;
                        return true;
                    }
                    return false;
                case short shortValue:
                    if (shortValue == 0 || shortValue == 1)
                    {
                        value = shortValue == 1;
                        return true;
                    }
                    return false;
                case int intValue:
                    if (intValue == 0 || intValue == 1)
                    {
                        value = intValue == 1;
                        return true;
                    }
                    return false;
                case long longValue:
                    if (longValue == 0L || longValue == 1L)
                    {
                        value = longValue == 1L;
                        return true;
                    }
                    return false;
            }

            string text = raw.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (bool.TryParse(text, out bool parsedBool))
            {
                value = parsedBool;
                return true;
            }

            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedInt) &&
                (parsedInt == 0 || parsedInt == 1))
            {
                value = parsedInt == 1;
                return true;
            }

            return false;
        }

internal static string BuildDialogueApiCostDetail(AIAction action)
        {
            if (action?.Parameters == null)
            {
                return string.Empty;
            }

            return action.ActionType switch
            {
                AIActionNames.RequestAid => AIActionExecutor.ReadDetail(action.Parameters, "type"),
                AIActionNames.RequestCaravan => AIActionExecutor.ReadDetail(action.Parameters, "type", "goods"),
                AIActionNames.CreateQuest => AIActionExecutor.ReadDetail(action.Parameters, "questDefName"),
                _ => string.Empty
            };
        }

internal static DialogueGoodwillCost.DialogueActionType ResolveAidDialogueCostType(AIAction action)
        {
            string aidType = AIActionExecutor.ReadStringParameterOrDefault(action?.Parameters, "type", "Military");
            switch ((aidType ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "medical":
                    return DialogueGoodwillCost.DialogueActionType.RequestMedicalAid;
                case "resources":
                case "resource":
                    return DialogueGoodwillCost.DialogueActionType.RequestResourceAid;
                default:
                    return DialogueGoodwillCost.DialogueActionType.RequestMilitaryAid;
            }
        }

internal static string ReadDetail(Dictionary<string, object> parameters, params string[] keys)
        {
            if (parameters == null || keys == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < keys.Length; i++)
            {
                if (parameters.TryGetValue(keys[i], out object value) && value != null)
                {
                    string text = value.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }

            return string.Empty;
        }

internal bool IsFeatureEnabled(string actionType)
        {
            if (RelationsMod.Instance == null) return false;
            var settings = RelationsMod.Instance.InstanceSettings;
            if (settings == null) return false;

            return actionType switch
            {
                AIActionNames.AdjustGoodwill => settings.EnableAIGoodwillAdjustment,
                AIActionNames.SendGift => settings.EnableAIGiftSending,
                AIActionNames.RequestAid => settings.EnableAIAidRequest,
                AIActionNames.DeclareWar => settings.EnableAIWarDeclaration,
                AIActionNames.MakePeace => settings.EnableAIPeaceMaking,
                AIActionNames.RequestCaravan => settings.EnableAITradeCaravan,
                AIActionNames.RequestVisitor => settings.EnableAITradeCaravan,
                AIActionNames.RequestRaid => settings.EnableAIRaidRequest,
                AIActionNames.RequestRaidCallEveryone => settings.EnableAIRaidRequest,
                AIActionNames.RequestRaidWaves => settings.EnableAIRaidRequest,
                AIActionNames.RequestItemAirdrop => settings.EnableAIItemAirdrop,
                AIActionNames.RequestInfo => settings.EnablePrisonerRansom,
                AIActionNames.PayPrisonerRansom => settings.EnablePrisonerRansom,
                AIActionNames.RejectRequest => true,
                AIActionNames.TriggerIncident => true,
                AIActionNames.CreateQuest => true,
                AIActionNames.SendImage => false,
                AIActionNames.ExitDialogue => settings.EnableFactionPresenceStatus,
                AIActionNames.GoOffline => settings.EnableFactionPresenceStatus,
                AIActionNames.SetDnd => settings.EnableFactionPresenceStatus,
                _ => false
            };
        }

internal ActionResult ExecuteTriggerIncident(AIAction action)
        {
            if (!action.Parameters.TryGetValue("defName", out object defNameObj) || string.IsNullOrEmpty(defNameObj?.ToString()))
            {
                return ActionResult.Failure("Missing 'defName' parameter for TriggerIncident");
            }

            string defName = defNameObj.ToString();
            float points = -1f;
            AIActionExecutor.TryReadFloatParameter(action.Parameters, "amount", out points);

            var result = gameInterface.TriggerIncident(faction, defName, points);
            if (result.Success)
            {
                return ActionResult.Success(result.Message, result.Data);
            }
            else
            {
                return ActionResult.Failure(result.Message);
            }
        }

internal ActionResult ExecuteCreateQuest(AIAction action)
        {
            if (!action.Parameters.TryGetValue("questDefName", out object questDefObj) || string.IsNullOrEmpty(questDefObj?.ToString()))
            {
                return ActionResult.Failure("create_quest requires parameter 'questDefName' from the currently injected allowed list.");
            }

            string questDefName = questDefObj.ToString();
            action.Parameters["askerFaction"] = faction;
            action.Parameters["faction"] = faction;

            var questValidation = ApiActionEligibilityService.Instance.ValidateCreateQuest(faction, questDefName, action.Parameters);
            if (!questValidation.Allowed)
            {
                return ActionResult.Failure(Owner.BuildCreateQuestFailureMessage(questValidation, action.Parameters));
            }

            var result = gameInterface.CreateQuest(questValidation.NormalizedQuestDefName, action.Parameters);
            return result.Success
                ? ActionResult.Success(result.Message, result.Data)
                : ActionResult.Failure(result.Message);
        }

internal string BuildCreateQuestFailureMessage(QuestValidationResult validation, Dictionary<string, object> parameters)
        {
            string reason = validation?.Message ?? "create_quest validation failed.";
            List<string> allowedQuestDefs = ApiActionEligibilityService.Instance.GetAvailableQuestDefsForFaction(faction, parameters);
            if (allowedQuestDefs == null || allowedQuestDefs.Count == 0)
            {
                return reason + " No eligible questDefName is currently available for this faction.";
            }

            return reason + " Allowed questDefName values for current faction: " + string.Join(", ", allowedQuestDefs) + ".";
        }

internal ActionResult ExecuteAdjustGoodwill(AIAction action)
        {
            if (!AIActionExecutor.TryReadIntParameter(action.Parameters, "amount", out int amount))
            {
                return ActionResult.Failure("Missing or invalid 'amount' parameter");
            }

            string reason = AIActionExecutor.ReadStringParameterOrDefault(action.Parameters, "reason", "Diplomatic dialogue");

            int cooldownSeconds = gameInterface.GetRemainingCooldownSeconds(faction, "AdjustGoodwill");
            if (cooldownSeconds > 0)
            {
                return ActionResult.Failure($"AdjustGoodwill is on cooldown for {faction.Name}. Remaining: {cooldownSeconds} seconds");
            }

            var result = gameInterface.AdjustGoodwill(faction, amount, reason);

            if (result.Success)
            {
                int actualChange = AIActionExecutor.TryReadGoodwillChangeFromResult(result.Data, amount);
                DiplomacySystem.DiplomacyNotificationManager.SendAIAdjustGoodwillNotification(faction, actualChange);
                return ActionResult.Success(result.Message, result.Data);
            }
            else
            {
                return ActionResult.Failure(result.Message);
            }
        }

internal static int TryReadGoodwillChangeFromResult(object resultData, int fallbackAmount)
        {
            if (resultData == null)
            {
                return fallbackAmount;
            }

            var changeProperty = resultData.GetType().GetProperty("Change");
            if (changeProperty == null)
            {
                return fallbackAmount;
            }

            object rawValue = changeProperty.GetValue(resultData, null);
            if (rawValue is int change)
            {
                return change;
            }

            return fallbackAmount;
        }

internal ActionResult ExecuteSendGift(AIAction action)
        {
            int silver = AIActionExecutor.ReadIntParameterOrDefault(action.Parameters, "silver", 500);
            int goodwillGain = AIActionExecutor.ReadIntParameterOrDefault(action.Parameters, "goodwill_gain", 5);

            int cooldownSeconds = gameInterface.GetRemainingCooldownSeconds(faction, "SendGift");
            if (cooldownSeconds > 0)
            {
                return ActionResult.Failure($"SendGift is on cooldown for {faction.Name}. Remaining: {cooldownSeconds} seconds");
            }

            if (silver <= 0)
            {
                return ActionResult.Failure("send_gift requires silver greater than 0 and player confirmation in diplomacy dialogue.");
            }

            if (goodwillGain < 0)
            {
                return ActionResult.Failure("send_gift goodwill_gain must be non-negative.");
            }

            return ActionResult.Failure("send_gift must be handled by diplomacy dialogue confirmation pipeline.");
        }

        internal ActionResult ExecuteRequestItemAirdrop(AIAction action) => Parts.ItemAirdrop.ExecuteRequestItemAirdrop(action);
        internal ActionResult ExecutePayPrisonerRansom(AIAction action) => Parts.PrisonerRansom.ExecutePayPrisonerRansom(action);
    }

    internal sealed class AIActionExecutorParts
    {
        internal readonly AIActionExecutor Owner;
        internal readonly AIActionExecutorItemAirdrop ItemAirdrop;
        internal readonly AIActionExecutorPrisonerRansom PrisonerRansom;
        internal readonly AIActionExecutorSlice1 Slice1;
        internal readonly AIActionExecutorSlice2 Slice2;
        internal AIActionExecutorParts(AIActionExecutor owner)
        {
            Owner = owner;
            ItemAirdrop = new AIActionExecutorItemAirdrop(owner);
            PrisonerRansom = new AIActionExecutorPrisonerRansom(owner);
            Slice1 = new AIActionExecutorSlice1(owner);
            Slice2 = new AIActionExecutorSlice2(owner);
        }
    }


    public class ActionResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }

        public static ActionResult Success(string message, object data = null)
        {
            return new ActionResult { IsSuccess = true, Message = message, Data = data };
        }

        public static ActionResult Failure(string message)
        {
            return new ActionResult { IsSuccess = false, Message = message };
        }
    }

    public class ActionExecutionDetails
    {
        public object ApiData { get; set; }
        public GameAIInterface.DialogueApiGoodwillCostResult DialogueCost { get; set; }
    }

}
