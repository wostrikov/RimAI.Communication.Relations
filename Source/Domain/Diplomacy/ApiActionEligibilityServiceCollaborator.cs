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
        internal abstract class ApiActionEligibilityServiceCollaborator
    {
        internal readonly ApiActionEligibilityService Owner;

        protected ApiActionEligibilityServiceCollaborator(ApiActionEligibilityService owner)
        {
            Owner = owner;
        }

        protected ApiActionEligibilityServiceParts Parts => Owner.Parts;
        protected const int QuestReportCacheTtl = 300;
        protected static string[] SupportedActions => ApiActionEligibilityService.SupportedActions;
        protected static string[] SupportedQuestDefs => ApiActionEligibilityService.SupportedQuestDefs;
        protected static HashSet<string> BanditCampAllowedFactionDefs => ApiActionEligibilityService.BanditCampAllowedFactionDefs;
        protected static HashSet<string> MerchantFactionDefs => ApiActionEligibilityService.MerchantFactionDefs;
        protected static HashSet<string> HighRiskQuestTemplates => ApiActionEligibilityService.HighRiskQuestTemplates;
        protected const int PeaceTalkOnlyMinGoodwill = -50;
        protected const int MakePeaceReenabledMinGoodwill = -20;
        protected const string PeaceTalkQuestDefName = "OpportunitySite_PeaceTalks";
        protected const string OrbitalTraderContextParameterKey = "orbital_trader_context";
        protected const string DialogueSourceParameterKey = "dialogue_source";
        protected const string OrbitalTraderDialogueSource = "orbital_trader";
        protected const string ExplicitChallengeRequestParameterKey = "explicit_challenge_request";
        protected const string BestowingCeremonyQuestDefName = "BestowingCeremony";
        protected Dictionary<int, (FactionQuestAvailabilityReport report, int tick)> _questReportCache => Owner._questReportCache;
    }

}
