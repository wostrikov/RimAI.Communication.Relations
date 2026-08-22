using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Relation;
using Ustas.RimAI.Communication.Relations.WorldState;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
    /// <summary>/// Centralized API action eligibility and quest template validation.
 ///</summary>
    public sealed class ApiActionEligibilityService
    {
        internal ApiActionEligibilityServiceParts Parts;

        internal static ApiActionEligibilityService _instance;
        public static ApiActionEligibilityService Instance => _instance ?? (_instance = new ApiActionEligibilityService());

        internal readonly Dictionary<int, (FactionQuestAvailabilityReport report, int tick)> _questReportCache =
            new Dictionary<int, (FactionQuestAvailabilityReport, int)>();
        internal const int QuestReportCacheTtl = 300;

        internal static readonly string[] SupportedActions =
        {
            "adjust_goodwill",
            "send_gift",
            "request_aid",
            "declare_war",
            "make_peace",
            "request_caravan",
            "request_visitor",
            "request_raid",
            "request_raid_call_everyone",
            "request_raid_waves",
            "request_item_airdrop",
            "request_info",
            "pay_prisoner_ransom",
            "trigger_incident",
            "create_quest",
            "send_image",
            "reject_request",
            "publish_public_post",
            "exit_dialogue",
            "go_offline",
            "set_dnd"
        };

        internal static readonly string[] SupportedQuestDefs =
        {
            "OpportunitySite_ItemStash",
            "TradeRequest",
            "OpportunitySite_PeaceTalks",
            "PawnLend",
            "ThreatReward_Raid_MiscReward",
            "Hospitality_Refugee"
        };

        internal static readonly HashSet<string> BanditCampAllowedFactionDefs = new HashSet<string>
        {
            "Empire",
            "OutlanderCivil",
            "OutlanderRough"
        };

        internal static readonly HashSet<string> MerchantFactionDefs = new HashSet<string>(StringComparer.Ordinal)
        {
            "OutlanderCivil",
            "OutlanderRough"
        };

        // Safety-first policy: disable templates with recurring technical failures in runtime.
        internal static readonly HashSet<string> HighRiskQuestTemplates = new HashSet<string>(StringComparer.Ordinal)
        {
            "OpportunitySite_ItemStash",
            "AncientComplex_Mission",
            "Mission_BanditCamp"
        };

        internal const int PeaceTalkOnlyMinGoodwill = -50;
        internal const int MakePeaceReenabledMinGoodwill = -20;
        internal const string PeaceTalkQuestDefName = "OpportunitySite_PeaceTalks";
        internal const string OrbitalTraderContextParameterKey = "orbital_trader_context";
        internal const string DialogueSourceParameterKey = "dialogue_source";
        internal const string OrbitalTraderDialogueSource = "orbital_trader";
        internal const string ExplicitChallengeRequestParameterKey = "explicit_challenge_request";
        internal const string BestowingCeremonyQuestDefName = "BestowingCeremony";

        internal ApiActionEligibilityService()
        {
            Parts = new ApiActionEligibilityServiceParts(this);
        }

        

        

        

        

        

        

        

        

        public List<QuestTemplateEligibility> GetQuestEligibilityReport(Faction faction, Dictionary<string, object> parameters = null)
        {
            return GetFactionQuestAvailabilityReport(faction, parameters).EvaluatedQuestDefs;
        }

        public List<string> GetAvailableQuestDefsForFaction(Faction faction, Dictionary<string, object> parameters = null)
        {
            return GetFactionQuestAvailabilityReport(faction, parameters).AllowedQuestDefs;
        }

        

        

        

        

        

        

        

        

        

        

        

        

        internal static bool HasSettlement(Faction faction)
        {
            return Find.WorldObjects?.Settlements != null && Find.WorldObjects.Settlements.Any(s => s.Faction == faction);
        }

        internal static bool HasFactionLeader(Faction faction)
        {
            return faction?.leader != null || HasSettlement(faction);
        }

        

        

        

        

        

        

        

        

        
    
        #region Cluster forwards
        public FactionQuestAvailabilityReport GetFactionQuestAvailabilityReport(Faction faction, Dictionary<string, object> parameters = null) => Parts.Slice1.GetFactionQuestAvailabilityReport(faction, parameters);
        public Dictionary<string, ActionValidationResult> GetAllowedActions(Faction faction, bool lightweight = false) => Parts.Slice1.GetAllowedActions(faction, lightweight);
        public ActionValidationResult ValidateActionExecution(Faction faction, string actionType, Dictionary<string, object> parameters, bool lightweight = false) => Parts.Slice1.ValidateActionExecution(faction, actionType, parameters, lightweight);
        internal static string TryReadStringParameter(Dictionary<string, object> parameters, string key) => ApiActionEligibilitySlice1.TryReadStringParameter(parameters, key);
        internal static bool TryReadBoolParameter(Dictionary<string, object> parameters, string key, out bool value) => ApiActionEligibilitySlice1.TryReadBoolParameter(parameters, key, out value);
        internal static bool TryReadPositiveIntParameter(Dictionary<string, object> parameters, string key, out int value) => ApiActionEligibilitySlice1.TryReadPositiveIntParameter(parameters, key, out value);
        internal static bool TryReadPaymentItemsArray(Dictionary<string, object> parameters, out IEnumerable<object> items) => ApiActionEligibilitySlice1.TryReadPaymentItemsArray(parameters, out items);
        public QuestValidationResult ValidateCreateQuest(Faction faction, string questDefName, Dictionary<string, object> parameters) => Parts.Slice1.ValidateCreateQuest(faction, questDefName, parameters);
        public bool IsOrbitalTraderDialogueContext(Faction faction, Dictionary<string, object> parameters = null) => Parts.Slice1.IsOrbitalTraderDialogueContext(faction, parameters);
        internal Dictionary<string, object> NormalizeQuestParameters(Faction faction, Dictionary<string, object> parameters) => Parts.Slice2.NormalizeQuestParameters(faction, parameters);
        internal ActionValidationResult ValidateCreateQuestActionAvailability(Faction faction, Dictionary<string, object> parameters) => Parts.Slice2.ValidateCreateQuestActionAvailability(faction, parameters);
        internal QuestTemplateEligibility EvaluateQuestTemplateAvailability(Faction faction, string questDefName, Dictionary<string, object> parameters) => Parts.Slice2.EvaluateQuestTemplateAvailability(faction, questDefName, parameters);
        internal bool TryProbeQuestGeneration(Faction faction, string questDefName, Dictionary<string, object> parameters, out string code, out string message) => Parts.Slice2.TryProbeQuestGeneration(faction, questDefName, parameters, out code, out message);
        internal bool TryValidateQuestTemplateForFaction(Faction faction, string questDefName, Dictionary<string, object> parameters, out string code, out string message) => Parts.Slice2.TryValidateQuestTemplateForFaction(faction, questDefName, parameters, out code, out message);
        internal bool IsOrbitalTraderSettlementQuestBlocked(Faction faction, string questDefName, Dictionary<string, object> parameters) => Parts.Slice2.IsOrbitalTraderSettlementQuestBlocked(faction, questDefName, parameters);
        internal static bool IsMerchantTradeRequestBlocked(Faction faction, string questDefName) => ApiActionEligibilitySlice2.IsMerchantTradeRequestBlocked(faction, questDefName);
        internal static ActionValidationResult ValidateMakePeaceGoodwillPolicy(Faction faction) => ApiActionEligibilitySlice2.ValidateMakePeaceGoodwillPolicy(faction);
        internal static ActionValidationResult ValidatePeaceTalkOnlyQuestPolicy(Faction faction, string questDefName) => ApiActionEligibilitySlice2.ValidatePeaceTalkOnlyQuestPolicy(faction, questDefName);
        internal static bool IsInPeaceTalkOnlyRange(Faction faction) => ApiActionEligibilitySlice2.IsInPeaceTalkOnlyRange(faction);
        internal static bool ShouldBypassProjectedGoodwillFloorForQuest(Faction faction, string questDefName) => ApiActionEligibilitySlice2.ShouldBypassProjectedGoodwillFloorForQuest(faction, questDefName);
        internal static bool IsEnabledImageTemplate(string templateId) => ApiActionEligibilitySlice2.IsEnabledImageTemplate(templateId);
        internal static string GetDefaultEnabledImageTemplateId() => ApiActionEligibilitySlice2.GetDefaultEnabledImageTemplateId();
        internal static string ResolveExistingImageTemplateId(string requestedTemplateId) => ApiActionEligibilitySlice2.ResolveExistingImageTemplateId(requestedTemplateId);
        internal static ActionValidationResult ValidateCooldown(Faction faction, string methodName, string code) => ApiActionEligibilitySlice2.ValidateCooldown(faction, methodName, code);
        internal static ActionValidationResult ValidateRaidCallEveryoneAvailability(Faction faction, Dictionary<string, object> parameters, bool checkCooldown) => ApiActionEligibilitySlice3.ValidateRaidCallEveryoneAvailability(faction, parameters, checkCooldown);
        internal static bool HasExplicitChallengeRequest(Dictionary<string, object> parameters) => ApiActionEligibilitySlice3.HasExplicitChallengeRequest(parameters);
        internal static bool HasRecentRaidIntentForFaction(Faction faction, int windowDays) => ApiActionEligibilitySlice3.HasRecentRaidIntentForFaction(faction, windowDays);
        internal static bool IsAncientQuestTemplateName(string questDefName) => ApiActionEligibilitySlice3.IsAncientQuestTemplateName(questDefName);
        internal static bool IsFeatureEnabled(string actionType) => ApiActionEligibilitySlice3.IsFeatureEnabled(actionType);
        #endregion
}
    internal sealed class ApiActionEligibilityServiceParts
    {
        internal readonly ApiActionEligibilityService Owner;
        internal readonly ApiActionEligibilitySlice1 Slice1;
        internal readonly ApiActionEligibilitySlice2 Slice2;
        internal readonly ApiActionEligibilitySlice3 Slice3;
        internal ApiActionEligibilityServiceParts(ApiActionEligibilityService owner)
        {
            Owner = owner;
            Slice1 = new ApiActionEligibilitySlice1(owner);
            Slice2 = new ApiActionEligibilitySlice2(owner);
            Slice3 = new ApiActionEligibilitySlice3(owner);
        }
    }


    public class ActionValidationResult
    {
        public bool Allowed { get; set; }
        public string Code { get; set; }
        public string Message { get; set; }
        public int RemainingSeconds { get; set; }

        public static ActionValidationResult AllowedResult()
        {
            return new ActionValidationResult { Allowed = true, Code = "allowed", Message = "Allowed", RemainingSeconds = 0 };
        }

        public static ActionValidationResult Denied(string code, string message, int remainingSeconds = 0)
        {
            return new ActionValidationResult
            {
                Allowed = false,
                Code = code ?? "denied",
                Message = message ?? "Action denied",
                RemainingSeconds = Math.Max(0, remainingSeconds)
            };
        }
    }

    public class QuestValidationResult : ActionValidationResult
    {
        public string NormalizedQuestDefName { get; set; }

        public static QuestValidationResult AllowedResult(string questDefName)
        {
            return new QuestValidationResult
            {
                Allowed = true,
                Code = "allowed",
                Message = "Allowed",
                NormalizedQuestDefName = questDefName
            };
        }

        public new static QuestValidationResult Denied(string code, string message, int remainingSeconds = 0)
        {
            return new QuestValidationResult
            {
                Allowed = false,
                Code = code ?? "denied",
                Message = message ?? "Quest denied",
                RemainingSeconds = Math.Max(0, remainingSeconds),
                NormalizedQuestDefName = null
            };
        }
    }

    public class QuestTemplateEligibility
    {
        public string QuestDefName { get; set; }
        public bool Allowed { get; set; }
        public string Code { get; set; }
        public string Message { get; set; }
        public QuestEligibilityStage Stage { get; set; }
    }

    public sealed class FactionQuestAvailabilityReport
    {
        public Faction Faction { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public ActionValidationResult ActionValidation { get; set; } = ActionValidationResult.AllowedResult();
        public List<QuestTemplateEligibility> EvaluatedQuestDefs { get; set; } = new List<QuestTemplateEligibility>();
        public List<string> AllowedQuestDefs => EvaluatedQuestDefs
            .Where(x => x != null && x.Allowed)
            .Select(x => x.QuestDefName)
            .ToList();

        public QuestTemplateEligibility Find(string questDefName)
        {
            return EvaluatedQuestDefs.FirstOrDefault(x => string.Equals(x?.QuestDefName, questDefName, StringComparison.Ordinal));
        }
    }

    public enum QuestEligibilityStage
    {
        RuleValidation = 0,
        GenerationProbe = 1
    }
}


