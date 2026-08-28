using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using RimWorld;
using UnityEngine;
using Verse;

using UnityEngine;
using RimWorld;
using Verse;
using APIResult = Ustas.RimAI.Communication.Relations.DiplomacySystem.GameAIInterface.APIResult;
using APICallRecord = Ustas.RimAI.Communication.Relations.DiplomacySystem.GameAIInterface.APICallRecord;
using DialogueApiGoodwillCostResult = Ustas.RimAI.Communication.Relations.DiplomacySystem.GameAIInterface.DialogueApiGoodwillCostResult;
using FactionCooldownEntry = Ustas.RimAI.Communication.Relations.DiplomacySystem.GameAIInterface.FactionCooldownEntry;
using RaidWaveState = Ustas.RimAI.Communication.Relations.DiplomacySystem.GameAIInterface.RaidWaveState;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
    /// <summary>Item airdrop drop execution and shared helpers.</summary>
    internal sealed class GameAIAirdropDrop : GameAIInterfaceCollaborator
    {
        internal GameAIAirdropDrop(GameAIInterface owner) : base(owner)
        {
        }

internal APIResult ExecuteAirdropDrop(
            Faction faction,
            Dictionary<string, object> parameters,
            Map map,
            int budget,
            ThingDefRecord selectedRecord,
            int validatedCount,
            string selectionReason,
            ItemAirdropCandidatePack candidatePack)
        {
            List<Thing> stacks = BuildStacks(selectedRecord.Def, validatedCount, RelationsMod.Instance.InstanceSettings.ItemAirdropMaxStacksPerDrop);
            if (stacks.Count == 0)
            {
                return FailFastAirdrop("stack_build_failed", "Could not create item stacks for airdrop.", faction, parameters);
            }

            int deliveredCount = stacks.Sum(t => t.stackCount);
            if (deliveredCount != validatedCount)
            {
                return FailFastAirdrop(
                    "delivery_quantity_mismatch",
                    $"Prepared airdrop quantity {validatedCount} exceeds stack delivery capacity {deliveredCount}.",
                    faction,
                    parameters,
                    $"def={selectedRecord.DefName},validated={validatedCount},delivered={deliveredCount},maxStacks={RelationsMod.Instance.InstanceSettings.ItemAirdropMaxStacksPerDrop},stackLimit={selectedRecord.Def.stackLimit}");
            }

            if (!TryFindAirdropCell(map, out IntVec3 dropCell))
            {
                if (MapUtility.IsOrbitalBaseMap(map))
                {
                    return FailFastAirdrop("orbital_drop_unavailable", "You are on an orbital base and cannot receive supply drops.", faction, parameters);
                }
                return FailFastAirdrop("dropcell_not_found", "No legal drop cell found near colony center.", faction, parameters);
            }

            DropPodUtility.DropThingsNear(
                dropCell,
                map,
                stacks,
                110,
                canInstaDropDuringInit: false,
                leaveSlag: false,
                canRoofPunch: false);

            string stage3 = $"def={selectedRecord.DefName},count={deliveredCount},budget={budget},reason={selectionReason},drop={dropCell}";
            RecordStageAudit("execute", faction, parameters, stage3);
            Owner.Parts.CooldownOps.RecordAPICall("RequestItemAirdrop", true, stage3);

            string playerTitle = "RimChat_ItemAirdropArrivedTitle".Translate();
            string playerBody = "RimChat_ItemAirdropArrivedBody".Translate(
                faction.Name,
                selectedRecord.Label.CapitalizeFirst(),
                deliveredCount,
                budget);
            Find.LetterStack.ReceiveLetter(playerTitle, playerBody, LetterDefOf.PositiveEvent, new TargetInfo(dropCell, map), faction);

            var payload = new ItemAirdropResultData
            {
                SelectedDefName = selectedRecord.DefName,
                ResolvedLabel = selectedRecord.Label,
                BudgetUsed = budget,
                Quantity = deliveredCount,
                DropCell = dropCell.ToString(),
                FailureCode = string.Empty
            };
            return APIResult.SuccessResult($"Airdrop delivered: {selectedRecord.DefName} x{deliveredCount} (budget {budget})", payload);
        }

internal APIResult BuildSelectionFailure(string code, string message)
        {
            return new APIResult
            {
                Success = false,
                Message = $"[{code}] {message}",
                Data = new ItemAirdropResultData { FailureCode = code }
            };
        }

internal static string NormalizeSelectionFailureReason(string rawReason)
        {
            string normalized = (rawReason ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return "service_error";
            }

            if (normalized.Contains("queue_timeout"))
            {
                return "queue_timeout";
            }

            if (normalized.Contains("timeout"))
            {
                return "timeout";
            }

            if (normalized.StartsWith("http_", StringComparison.Ordinal) ||
                normalized.Contains("connection") ||
                normalized.Contains("data_processing"))
            {
                return normalized;
            }

            return "service_error";
        }

internal static bool IsTimeoutLikeSelectionFailure(string failureReason)
        {
            return string.Equals(failureReason, "timeout", StringComparison.Ordinal) ||
                   string.Equals(failureReason, "queue_timeout", StringComparison.Ordinal);
        }

internal static string BuildSelectionServiceErrorMessage(AIChatClientResponse response, string failureReason)
        {
            if (response == null)
            {
                return "selection request failed: unknown error.";
            }

            string reason = string.IsNullOrWhiteSpace(failureReason) ? "service_error" : failureReason;
            string message = string.IsNullOrWhiteSpace(response.ErrorText)
                ? "selection request failed."
                : response.ErrorText.Trim();
            return $"{message} failureReason={reason},http={response.HttpStatusCode}";
        }

internal void RecordSelectionDebugRecord(
            string requestText,
            string responseText,
            string errorText,
            AIRequestDebugStatus status,
            long durationMs,
            long httpStatusCode,
            DateTime startedAtUtc,
            int promptTokens = 0,
            int completionTokens = 0,
            int totalTokens = 0,
            bool isEstimatedTokens = true)
        {
            AIChatServiceAsync.RecordExternalDebugRecord(
                AIRequestDebugSource.AirdropSelection,
                DialogueUsageChannel.Diplomacy,
                "airdrop_selection",
                status,
                durationMs,
                httpStatusCode,
                promptTokens,
                completionTokens,
                totalTokens,
                isEstimatedTokens,
                requestText,
                responseText,
                errorText,
                startedAtUtc);
        }

internal static string BuildSelectionPrompt(
            ItemAirdropIntent intent,
            ItemAirdropCandidatePack candidatePack,
            int budget,
            RelationsSettings settings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("channel:airdrop_selection");
            sb.AppendLine("Task: choose exactly one candidate and legal count for item airdrop.");
            sb.AppendLine("IMPORTANT: If Need has an explicit quantity (e.g., '50 пемікану' or '50 pemmican'), preserve that quantity in count.");
            sb.AppendLine("IMPORTANT: If Need directly matches a candidate, keep the explicit quantity from Need instead of forcing count=1.");
            sb.AppendLine("Output JSON only:");
            sb.AppendLine("{\"selected_def\":\"<defName>\",\"count\":<int>,\"reason\":\"<short reason>\"}");
            sb.AppendLine($"Need: {intent.NeedText}");
            sb.AppendLine($"Constraints: {intent.ConstraintsText}");
            sb.AppendLine($"Family: {intent.Family}");
            sb.AppendLine($"BudgetSilver: {budget}");
            sb.AppendLine("Rule: If Need has explicit quantity, use it. Otherwise count must be 1..max_legal_count for selected_def.");
            sb.AppendLine("Candidates:");

            int promptCandidateLimit = Math.Min(candidatePack.Candidates.Count, 20);
            for (int i = 0; i < promptCandidateLimit; i++)
            {
                ItemAirdropCandidate candidate = candidatePack.Candidates[i];
                ComputeLegalCountWindow(budget, candidate.Record, candidatePack, settings, out _, out _, out int hardMax);
                sb.AppendLine(
                    $"{i + 1}. def={candidate.Record.DefName},label={candidate.Record.Label},unit={candidate.Price:F1},max_legal_count={hardMax}");
            }

            int omitted = candidatePack.Candidates.Count - promptCandidateLimit;
            if (omitted > 0)
            {
                sb.AppendLine($"... omitted_candidates={omitted}");
            }

            return sb.ToString().Trim();
        }

internal static void ComputeLegalCountWindow(
            int budget,
            ThingDefRecord record,
            RelationsSettings settings,
            out int maxByBudget,
            out int maxBySystem,
            out int hardMax)
        {
            ComputeLegalCountWindow(budget, record, null, settings, out maxByBudget, out maxBySystem, out hardMax);
        }

internal static void ComputeLegalCountWindow(
            int budget,
            ThingDefRecord record,
            ItemAirdropCandidatePack candidatePack,
            RelationsSettings settings,
            out int maxByBudget,
            out int maxBySystem,
            out int hardMax)
        {
            if (record == null)
            {
                maxByBudget = 0;
                maxBySystem = 0;
                hardMax = 0;
                return;
            }

            float safePrice = candidatePack?.ResolveUnitPrice(record) ?? Math.Max(0.01f, record.MarketValue);
            maxByBudget = Mathf.FloorToInt(Math.Max(0, budget) / safePrice);
            maxBySystem = ComputeMaxDeliverableByStacks(record.Def, settings);
            hardMax = Math.Max(0, Math.Min(maxByBudget, maxBySystem));
        }

internal static int ComputeMaxDeliverableByStacks(ThingDef def, RelationsSettings settings)
        {
            return int.MaxValue / 2;
        }

internal static int ResolveFamilyDefaultCount(ItemAirdropNeedFamily family)
        {
            return family switch
            {
                ItemAirdropNeedFamily.Food => 25,
                ItemAirdropNeedFamily.Medicine => 10,
                ItemAirdropNeedFamily.Weapon => 1,
                ItemAirdropNeedFamily.Apparel => 1,
                ItemAirdropNeedFamily.Resource => 75,
                _ => 5
            };
        }

internal string BuildSelectionAuditDetails(
            ItemAirdropSelection selection,
            ItemAirdropCandidatePack candidatePack,
            int budget,
            RelationsSettings settings,
            string countSource,
            int? explicitMaxByBudget,
            int? explicitHardMax)
        {
            int maxByBudget = explicitMaxByBudget ?? -1;
            int hardMax = explicitHardMax ?? -1;
            if (!explicitMaxByBudget.HasValue || !explicitHardMax.HasValue)
            {
                if (TryResolveSelectedRecord(selection, candidatePack, out ThingDefRecord selectedRecord))
                {
                    ComputeLegalCountWindow(budget, selectedRecord, candidatePack, settings, out maxByBudget, out _, out hardMax);
                }
            }

            string maxByBudgetText = maxByBudget >= 0
                ? maxByBudget.ToString(CultureInfo.InvariantCulture)
                : "na";
            string hardMaxText = hardMax >= 0
                ? hardMax.ToString(CultureInfo.InvariantCulture)
                : "na";
            return $"selected={selection?.SelectedDefName ?? "unknown"},count={selection?.Count ?? 0},reason={selection?.Reason ?? "none"},countSource={countSource},hardMax={hardMaxText},maxByBudget={maxByBudgetText}";
        }

internal static bool TryResolveSelectedRecord(
            ItemAirdropSelection selection,
            ItemAirdropCandidatePack candidatePack,
            out ThingDefRecord selectedRecord)
        {
            selectedRecord = null;
            if (selection == null || candidatePack?.Candidates == null)
            {
                return false;
            }

            selectedRecord = candidatePack.Candidates
                .Select(c => c.Record)
                .FirstOrDefault(r => string.Equals(r.DefName, selection.SelectedDefName, StringComparison.OrdinalIgnoreCase));
            return selectedRecord?.Def != null;
        }

internal static string BuildAirdropFailurePlayerMessage(string failureCode, params object[] detailArgs)
        {
            string detail = detailArgs.Length > 0 ? string.Join(" ", detailArgs) : string.Empty;
            if (string.IsNullOrWhiteSpace(detail))
                return "RimChat_ItemAirdropFailedBody".Translate(failureCode, "").ToString();

            // Strip leading [code] prefix from detail — it already comes from BuildPaymentFailure
            if (detail[0] == '[')
            {
                int closeBracket = detail.IndexOf("] ");
                if (closeBracket > 0)
                    detail = detail.Substring(closeBracket + 2).TrimStart();
            }

            return "RimChat_ItemAirdropFailedBody".Translate(failureCode, detail).ToString();
        }

internal APIResult FailFastAirdrop(
            string failureCode,
            string message,
            Faction faction,
            Dictionary<string, object> parameters,
            string diagnostics = "",
            bool sendLetter = true)
        {
            string details = string.IsNullOrWhiteSpace(diagnostics)
                ? $"code={failureCode},msg={message}"
                : $"code={failureCode},msg={message},diag={diagnostics}";
            RecordStageAudit("failed", faction, parameters, details);
            string auditText = $"faction={faction?.Name ?? "unknown"}, code={failureCode}, msg={message}, params={SerializeParameterSummary(parameters)}";
            if (!string.IsNullOrWhiteSpace(diagnostics))
            {
                auditText = $"{auditText}, diag={diagnostics}";
            }

            Owner.Parts.CooldownOps.RecordAPICall("RequestItemAirdrop", false, auditText, message);

            if (sendLetter)
            {
                string playerTitle = "RimChat_ItemAirdropFailedTitle".Translate();
                string playerBody = BuildAirdropFailurePlayerMessage(failureCode, message);
                Find.LetterStack.ReceiveLetter(playerTitle, playerBody, LetterDefOf.NeutralEvent);
            }
            return APIResult.FailureResult($"[{failureCode}] {message}");
        }

internal void RecordStageAudit(string stage, Faction faction, Dictionary<string, object> parameters, string details)
        {
            string text = $"stage={stage},faction={faction?.Name ?? "unknown"},params={SerializeParameterSummary(parameters)},details={details}";
            Owner.Parts.CooldownOps.RecordAPICall("RequestItemAirdrop.Stage", true, text);
        }

internal static int ResolveBudget(Dictionary<string, object> parameters, string scenario, RelationsSettings settings, Map map)
        {
            if (TryReadIntParameter(parameters, "budget_silver", out int directBudget))
            {
                return Mathf.Clamp(directBudget, settings.ItemAirdropMinBudgetSilver, settings.ItemAirdropMaxBudgetSilver);
            }

            if (scenario == "ransom")
            {
                float wealth = map?.wealthWatcher?.WealthTotal ?? 0f;
                int ransomBudget = Mathf.RoundToInt(wealth * settings.ItemAirdropRansomBudgetPercent);
                return Mathf.Clamp(ransomBudget, settings.ItemAirdropMinBudgetSilver, settings.ItemAirdropMaxBudgetSilver);
            }

            int aiBudget = settings.ItemAirdropDefaultAIBudgetSilver;
            return Mathf.Clamp(aiBudget, settings.ItemAirdropMinBudgetSilver, settings.ItemAirdropMaxBudgetSilver);
        }

internal static bool TryFindAirdropCell(Map map, out IntVec3 dropCell)
        {
            IntVec3 vanillaTradeDropSpot = DropCellFinder.TradeDropSpot(map);
            if (vanillaTradeDropSpot.IsValid &&
                vanillaTradeDropSpot.InBounds(map) &&
                vanillaTradeDropSpot.Standable(map) &&
                DropCellFinder.CanPhysicallyDropInto(vanillaTradeDropSpot, map, canRoofPunch: false))
            {
                dropCell = vanillaTradeDropSpot;
                return true;
            }

            IntVec3 center = map.Center;
            return CellFinder.TryFindRandomCellNear(
                center,
                map,
                18,
                c => c.InBounds(map) &&
                     c.Walkable(map) &&
                     c.Standable(map) &&
                     DropCellFinder.CanPhysicallyDropInto(c, map, canRoofPunch: false),
                out dropCell);
        }

internal static List<Thing> BuildStacks(ThingDef def, int totalCount, int maxStacks)
        {
            var result = new List<Thing>();
            int stackLimit = Math.Max(1, def.stackLimit);
            int remaining = Math.Max(0, totalCount);

            // Resolve stuff for MadeFromStuff defs (e.g., apparel, weapons)
            ThingDef stuff = null;
            if (def.MadeFromStuff)
            {
                stuff = def.defaultStuff;
                if (stuff == null)
                {
                    stuff = GenStuff.DefaultStuffFor(def) ?? ThingDefOf.Steel;
                }
            }

            int neededStacks = Math.Max(1, (int)System.Math.Ceiling((double)totalCount / stackLimit));
            int effectiveMaxStacks = Math.Max(maxStacks, neededStacks);

            while (remaining > 0 && result.Count < effectiveMaxStacks)
            {
                Thing thing = ThingMaker.MakeThing(def, stuff);
                int stack = Math.Min(stackLimit, remaining);
                thing.stackCount = stack;
                result.Add(thing);
                remaining -= stack;
            }

            return result;
        }

internal static HashSet<string> ParseCsv(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return new HashSet<string>(
                csv.Split(new[] { ',', ';', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x)),
                StringComparer.OrdinalIgnoreCase);
        }

internal static string ReadString(Dictionary<string, object> parameters, string key)
        {
            if (parameters == null || string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            if (!parameters.TryGetValue(key, out object value) || value == null)
            {
                return string.Empty;
            }

            return value.ToString()?.Trim() ?? string.Empty;
        }

internal static bool TryReadRequiredStringParameter(
            Dictionary<string, object> parameters,
            string key,
            out string value,
            out string valueType,
            out string rawPreview)
        {
            value = string.Empty;
            valueType = "missing";
            rawPreview = "none";
            if (parameters == null || string.IsNullOrWhiteSpace(key) || !parameters.TryGetValue(key, out object raw) || raw == null)
            {
                return false;
            }

            valueType = DescribeParameterType(raw);
            rawPreview = BuildParameterPreview(raw);
            if (!(raw is string text))
            {
                return false;
            }

            value = text.Trim();
            return !string.IsNullOrWhiteSpace(value);
        }

internal static string DescribeParameterType(object value)
        {
            if (value == null)
            {
                return "null";
            }

            return value is string ? "string" : value.GetType().Name;
        }

internal static string BuildParameterPreview(object value)
        {
            string text = value?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                return "empty";
            }

            string singleLine = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return singleLine.Length <= 48 ? singleLine : $"{singleLine.Substring(0, 48)}...";
        }

internal static bool TryReadIntParameter(Dictionary<string, object> parameters, string key, out int value)
        {
            value = 0;
            if (parameters == null || !parameters.TryGetValue(key, out object raw) || raw == null)
            {
                return false;
            }

            if (raw is int intValue)
            {
                value = intValue;
                return true;
            }

            if (raw is long longValue && longValue <= int.MaxValue && longValue >= int.MinValue)
            {
                value = (int)longValue;
                return true;
            }

            return int.TryParse(raw.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

internal static string NormalizeScenario(string scenario)
        {
            string normalized = (scenario ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "trade" => "trade",
                "ransom" => "ransom",
                _ => "general"
            };
        }

internal static string SerializeParameterSummary(Dictionary<string, object> parameters)
        {
            if (parameters == null || parameters.Count == 0)
            {
                return "none";
            }

            return string.Join(",", parameters.Select(kv => $"{kv.Key}={kv.Value}"));
        }

    }

/// <summary>
    /// Dependencies: ThingDefResolver, ItemAirdropSelectionParser, AIChatClient, DropPodUtility, DropCellFinder.
    /// Responsibility: two-phase item airdrop orchestration for request_item_airdrop.
    /// </summary>

public sealed class ItemAirdropResultData
    {
        public string SelectedDefName { get; set; }
        public string ResolvedLabel { get; set; }
        public int BudgetUsed { get; set; }
        public int ShippingCostSilver { get; set; }
        public int PaymentTotalSilver { get; set; }
        public int Quantity { get; set; }
        public string DropCell { get; set; }
        public string FailureCode { get; set; }
    }

    internal struct RequestedCountExtraction
    {
        public bool HasExplicitCount { get; set; }
        public bool HasMultipleCounts { get; set; }
        public int RequestedCount { get; set; }
        public bool HasParameterCount { get; set; }
        public int ParameterCount { get; set; }
    }
}
