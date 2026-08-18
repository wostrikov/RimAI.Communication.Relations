using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Diagnostics;
using Ustas.RimAI.Communication.Relations.Guards;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Ustas.RimAI.Communication.Relations.Relation;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.WorldState;
using UnityEngine;
using APIResult = Ustas.RimAI.Communication.Relations.DiplomacySystem.GameAIInterface.APIResult;
using APICallRecord = Ustas.RimAI.Communication.Relations.DiplomacySystem.GameAIInterface.APICallRecord;
using DialogueApiGoodwillCostResult = Ustas.RimAI.Communication.Relations.DiplomacySystem.GameAIInterface.DialogueApiGoodwillCostResult;
using FactionCooldownEntry = Ustas.RimAI.Communication.Relations.DiplomacySystem.GameAIInterface.FactionCooldownEntry;
using RaidWaveState = Ustas.RimAI.Communication.Relations.DiplomacySystem.GameAIInterface.RaidWaveState;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
    /// <summary>Dialogue goodwill-action APIs for GameAIInterface.</summary>
    internal sealed class GameAIDialogueActionOps : GameAIInterfaceCollaborator
    {
        internal GameAIDialogueActionOps(GameAIInterface owner) : base(owner)
        {
        }

public bool ValidateAIPermission(Faction faction)
        {
            if (faction == null) return false;
            if (faction.IsPlayer) return false;
            if (faction.defeated) return false;
            if (faction.def?.hidden == true)
            {
                if (GameComponent_DiplomacyManager.Instance?.IsHiddenFactionManuallyVisible(faction) != true)
                {
                    return false;
                }
            }

            return true;
        }

public APIResult ExecuteDialogueAction(Faction faction, DialogueGoodwillCost.DialogueActionType actionType)
        {
            EnsureInitialized();

            if (faction == null)
                return APIResult.FailureResult("Faction cannot be null");

            // 1. 检查冷却时间
            if (!CheckDialogueActionCooldown(faction, actionType))
            {
                int remainingTicks = GetDialogueActionCooldownRemaining(faction, actionType);
                float remainingHours = remainingTicks / 2500f;
                return APIResult.FailureResult($"Action is on cooldown. Remaining: {remainingHours:F1} hours");
            }

            // 2. 检查每日限制
            if (!CheckDailyDialogueLimit(faction, actionType, out string limitReason))
            {
                return APIResult.FailureResult($"Daily limit reached: {limitReason}");
            }

            // 3. 计算实际goodwill变化
            int goodwillChange = DialogueGoodwillCost.GetBaseValue(actionType);

            // 4. 执行goodwill变化
            if (goodwillChange != 0)
            {
                int oldGoodwill = faction.PlayerGoodwill;
                faction.TryAffectGoodwillWith(Faction.OfPlayer, goodwillChange, false, true, null);
                int newGoodwill = faction.PlayerGoodwill;
                int actualChange = newGoodwill - oldGoodwill;

                // Record到今日调整
                int currentDayAdjustment = _goodwillAdjustmentsToday.ContainsKey(faction) ? _goodwillAdjustmentsToday[faction] : 0;
                _goodwillAdjustmentsToday[faction] = currentDayAdjustment + actualChange;

                // Recordbehavior
                RecordDialogueAction(faction, actionType, actualChange);

                // Settings冷却
                SetDialogueActionCooldown(faction, actionType);

                // RecordAPI调用
                Owner.Parts.CooldownOps.RecordAPICall("ExecuteDialogueAction", true, 
                    $"faction={faction.Name}, action={actionType}, change={actualChange}");

                // 触发通知 (重大变化)
                if (Math.Abs(actualChange) >= 5)
                {
                    NotifyDialogueActionResult(faction, actionType, actualChange, goodwillChange);
                }

                return APIResult.SuccessResult(
                    $"Executed {DialogueGoodwillCost.GetActionLabel(actionType)}. Goodwill changed by {actualChange}.",
                    new
                    {
                        Action = actionType.ToString(),
                        ActionLabel = DialogueGoodwillCost.GetActionLabel(actionType),
                        GoodwillChange = actualChange,
                        OldGoodwill = oldGoodwill,
                        NewGoodwill = newGoodwill,
                        BaseValue = goodwillChange
                    }
                );
            }
            else
            {
                // 无goodwill变化但仍recordbehavior
                RecordDialogueAction(faction, actionType, 0);
                SetDialogueActionCooldown(faction, actionType);

                return APIResult.SuccessResult(
                    $"Executed {DialogueGoodwillCost.GetActionLabel(actionType)}. No goodwill change.",
                    new
                    {
                        Action = actionType.ToString(),
                        ActionLabel = DialogueGoodwillCost.GetActionLabel(actionType),
                        GoodwillChange = 0
                    }
                );
            }
        }

public APIResult PreviewDialogueActionCost(Faction faction, DialogueGoodwillCost.DialogueActionType actionType)
        {
            if (faction == null)
                return APIResult.FailureResult("Faction cannot be null");

            // 检查whether可执行
            bool canExecute = true;
            string reason = string.Empty;

            // 计算消耗
            int cost = DialogueGoodwillCost.GetBaseValue(actionType);

            // 检查冷却
            bool onCooldown = !CheckDialogueActionCooldown(faction, actionType);
            int remainingCooldown = onCooldown ? GetDialogueActionCooldownRemaining(faction, actionType) : 0;

            // 检查每日限制
            bool withinLimit = CheckDailyDialogueLimit(faction, actionType, out string limitReason);

            return APIResult.SuccessResult(
                $"Cost preview for {DialogueGoodwillCost.GetActionLabel(actionType)}",
                new
                {
                    Action = actionType.ToString(),
                    ActionLabel = DialogueGoodwillCost.GetActionLabel(actionType),
                    CanExecute = canExecute,
                    CannotExecuteReason = reason,
                    BaseCost = cost,
                    FinalCost = cost,
                    OnCooldown = onCooldown,
                    RemainingCooldownTicks = remainingCooldown,
                    RemainingCooldownHours = remainingCooldown / 2500f,
                    WithinDailyLimit = withinLimit,
                    DailyLimitReason = limitReason,
                    CurrentGoodwill = faction.PlayerGoodwill,
                    ExpectedGoodwillAfter = faction.PlayerGoodwill + cost
                }
            );
        }

internal bool CheckDialogueActionCooldown(Faction faction, DialogueGoodwillCost.DialogueActionType actionType)
        {
            EnsureInitialized();

            if (!_dialogueActionCooldowns.TryGetValue(actionType, out var factionCooldowns))
                return true;

            if (!factionCooldowns.TryGetValue(faction, out int nextAvailableTick))
                return true;

            int currentTick = Find.TickManager.TicksGame;
            return currentTick >= nextAvailableTick;
        }

internal int GetDialogueActionCooldownRemaining(Faction faction, DialogueGoodwillCost.DialogueActionType actionType)
        {
            EnsureInitialized();

            if (!_dialogueActionCooldowns.TryGetValue(actionType, out var factionCooldowns))
                return 0;

            if (!factionCooldowns.TryGetValue(faction, out int nextAvailableTick))
                return 0;

            int currentTick = Find.TickManager.TicksGame;
            return Math.Max(0, nextAvailableTick - currentTick);
        }

internal void SetDialogueActionCooldown(Faction faction, DialogueGoodwillCost.DialogueActionType actionType)
        {
            EnsureInitialized();

            int cooldownTicks = DialogueGoodwillCost.GetCooldownTicks(actionType);
            if (cooldownTicks <= 0) return;

            if (!_dialogueActionCooldowns.TryGetValue(actionType, out var factionCooldowns))
            {
                factionCooldowns = new Dictionary<Faction, int>();
                _dialogueActionCooldowns[actionType] = factionCooldowns;
            }

            factionCooldowns[faction] = Find.TickManager.TicksGame + cooldownTicks;
        }

internal bool CheckDailyDialogueLimit(Faction faction, DialogueGoodwillCost.DialogueActionType actionType, out string reason)
        {
            EnsureInitialized();
            reason = "";

            int baseValue = DialogueGoodwillCost.GetBaseValue(actionType);
            bool isCostAction = baseValue < 0;

            // 计算今日该faction的累计消耗/收益
            int todayCost = 0;
            int todayGain = 0;

            foreach (var record in _dialogueActionRecords)
            {
                if (record.FactionName == faction.Name)
                {
                    if (record.GoodwillChange < 0)
                        todayCost += Math.Abs(record.GoodwillChange);
                    else if (record.GoodwillChange > 0)
                        todayGain += record.GoodwillChange;
                }
            }

            // 检查whether超出限制
            if (isCostAction)
            {
                int expectedCost = Math.Abs(DialogueGoodwillCost.GetBaseValue(actionType));
                if (todayCost + expectedCost > Math.Abs(DialogueGoodwillCost.DailyCostLimit))
                {
                    reason = $"今日消耗已达上限 ({todayCost}/{Math.Abs(DialogueGoodwillCost.DailyCostLimit)})";
                    return false;
                }
            }
            else
            {
                int expectedGain = DialogueGoodwillCost.GetBaseValue(actionType);
                if (todayGain + expectedGain > DialogueGoodwillCost.DailyGainLimit)
                {
                    reason = $"今日收益已达上限 ({todayGain}/{DialogueGoodwillCost.DailyGainLimit})";
                    return false;
                }
            }

            return true;
        }

internal void RecordDialogueAction(Faction faction, DialogueGoodwillCost.DialogueActionType actionType, int goodwillChange)
        {
            EnsureInitialized();

            var record = new DialogueActionRecord
            {
                ActionType = actionType,
                GoodwillChange = goodwillChange,
                Tick = Find.TickManager.TicksGame,
                FactionName = faction.Name
            };

            _dialogueActionRecords.Add(record);
        }

public APIResult GetTodayDialogueStats(Faction faction)
        {
            EnsureInitialized();

            if (faction == null)
                return APIResult.FailureResult("Faction cannot be null");

            int totalCost = 0;
            int totalGain = 0;
            var actionCounts = new Dictionary<DialogueGoodwillCost.DialogueActionType, int>();

            foreach (var record in _dialogueActionRecords)
            {
                if (record.FactionName == faction.Name)
                {
                    if (record.GoodwillChange < 0)
                        totalCost += Math.Abs(record.GoodwillChange);
                    else if (record.GoodwillChange > 0)
                        totalGain += record.GoodwillChange;

                    if (!actionCounts.ContainsKey(record.ActionType))
                        actionCounts[record.ActionType] = 0;
                    actionCounts[record.ActionType]++;
                }
            }

            return APIResult.SuccessResult(
                $"Today's dialogue stats for {faction.Name}",
                new
                {
                    FactionName = faction.Name,
                    TotalCost = totalCost,
                    TotalGain = totalGain,
                    CostLimit = Math.Abs(DialogueGoodwillCost.DailyCostLimit),
                    GainLimit = DialogueGoodwillCost.DailyGainLimit,
                    RemainingCostBudget = Math.Abs(DialogueGoodwillCost.DailyCostLimit) - totalCost,
                    RemainingGainBudget = DialogueGoodwillCost.DailyGainLimit - totalGain,
                    ActionCounts = actionCounts
                }
            );
        }

internal void NotifyDialogueActionResult(Faction faction, DialogueGoodwillCost.DialogueActionType actionType, int change, int baseValue)
        {
            string actionLabel = DialogueGoodwillCost.GetActionLabelKey(actionType).Translate();
            string titleKey;
            string messageKey;
            LetterDef letterDef;

            if (change < 0)
            {
                titleKey = "RimChat_DialogueActionCostLetterTitle";
                messageKey = "RimChat_DialogueActionCostLetterBody";
                letterDef = LetterDefOf.NegativeEvent;
            }
            else
            {
                titleKey = "RimChat_DialogueActionGainLetterTitle";
                messageKey = "RimChat_DialogueActionGainLetterBody";
                letterDef = LetterDefOf.PositiveEvent;
            }

            Find.LetterStack.ReceiveLetter(
                titleKey.Translate(),
                messageKey.Translate(
                    faction.Name,
                    actionLabel,
                    Math.Abs(baseValue),
                    Math.Abs(change)),
                letterDef);
        }

    }
}
