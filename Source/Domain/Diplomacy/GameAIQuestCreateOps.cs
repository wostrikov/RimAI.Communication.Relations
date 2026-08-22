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
    /// <summary>Create-quest APIs for GameAIInterface.</summary>
    internal sealed class GameAIQuestCreateOps : GameAIInterfaceCollaborator
    {
        internal GameAIQuestCreateOps(GameAIInterface owner) : base(owner)
        {
        }

public APIResult CreateSimpleQuest(Faction faction, string title, string description, string rewardDescription, string callbackId, int durationTicks = 60000)
        {
            var parameters = new Dictionary<string, object>
            {
                { "title", title },
                { "description", description },
                { "rewardDescription", rewardDescription },
                { "callbackId", callbackId },
                { "askerFaction", faction },
                { "durationTicks", durationTicks }
            };

            var result = CreateQuest("RimChat_AIQuest", parameters);
            // Cooldown is set inside CreateQuest if successful
            return result;
        }

public APIResult CreateQuest(string questDefName, Dictionary<string, object> parameters)
        {
            if (string.IsNullOrEmpty(questDefName))
                return APIResult.FailureResult("Quest defName cannot be null");

            Faction faction = null;
            try
            {
            bool isItemStashQuest = string.Equals(questDefName, "OpportunitySite_ItemStash", StringComparison.Ordinal);
            if (parameters != null && parameters.TryGetValue("askerFaction", out object fObj))
            {
                if (fObj is Faction f) faction = f;
                else if (fObj is string s) faction = ResolveParameter("faction", s) as Faction;
            }
            
            if (faction == null && parameters != null && parameters.TryGetValue("faction", out object fObj2))
            {
                if (fObj2 is Faction f2) faction = f2;
                else if (fObj2 is string s2) faction = ResolveParameter("faction", s2) as Faction;
            }

            if (faction == null)
            {
                DebugLogger.WarningGated($"CreateQuest: Could not resolve faction from parameters. Quest '{questDefName}' might fallback to Empire.");
            }
            else if (RelationsMod.Instance?.InstanceSettings?.EnableDebugLogging ?? false)
            {
                DebugLogger.Debug($"CreateQuest: Using faction context '{faction.Name}' (Def: {faction.def.defName})");
            }

            var questValidation = ApiActionEligibilityService.Instance.ValidateCreateQuest(faction, questDefName, parameters);
            if (!questValidation.Allowed)
            {
                DebugLogger.WarningGated($"CreateQuest denied. def='{questDefName}', faction='{faction?.Name ?? "Unknown"}', code='{questValidation.Code}', message='{questValidation.Message}'");
                return APIResult.FailureResult(questValidation.Message);
            }
            questDefName = questValidation.NormalizedQuestDefName;

            QuestScriptDef questDef = DefDatabase<QuestScriptDef>.GetNamedSilentFail(questDefName);
            if (questDef == null)
                return APIResult.FailureResult($"Quest template '{questDefName}' missing");

                if (!QuestSlatePrebuilder.TryBuild(faction, questDef, parameters, out global::RimWorld.QuestGen.Slate slate, out string prebuildCode, out string prebuildMessage))
                {
                    DebugLogger.WarningGated($"CreateQuest prebuild failed. def='{questDefName}', faction='{faction?.Name ?? "Unknown"}', code='{prebuildCode}', message='{prebuildMessage}'");
                    return APIResult.FailureResult(prebuildMessage);
                }

                Quest quest;
                try
                {
                    Ustas.RimAI.Communication.Relations.Patches.QuestGenPatch.LockSlateVariables = true;
                    quest = global::RimWorld.QuestGen.QuestGen.Generate(questDef, slate);
                }
                finally
                {
                    Ustas.RimAI.Communication.Relations.Patches.QuestGenPatch.LockSlateVariables = false;
                }

                if (!QuestGenerationProbe.TryValidateGeneratedQuest(quest, questDef, slate, out string publicationCode, out string publicationMessage))
                {
                    DebugLogger.WarningGated($"CreateQuest publication probe failed. def='{questDefName}', faction='{faction?.Name ?? "Unknown"}', code='{publicationCode}', message='{publicationMessage}'");
                    return APIResult.FailureResult(publicationMessage);
                }

                Find.QuestManager.Add(quest);
                global::RimWorld.QuestUtility.SendLetterQuestAvailable(quest);

                string logMsg = $"Quest '{questDefName}' created";

                Owner.Parts.CooldownOps.RecordAPICall("CreateQuest", true, $"defName={questDefName}, paramsCount={parameters.Count}");

                Owner.Parts.CooldownOps.SetCooldown(faction, "CreateQuest");

                return APIResult.SuccessResult(
                    logMsg,
                    new
                    {
                        QuestDefName = questDefName,
                        Faction = faction?.Name ?? "Unknown"
                    });
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Error creating quest {questDefName}: {ex}");
                DebugLogger.WarningGated($"CreateQuest technical failure. def='{questDefName}', faction='{faction?.Name ?? "Unknown"}'. No fallback quest will be generated.");
                return APIResult.FailureResult($"Quest generation error: {ex.Message}");
            }
        }

internal string ValidateAndFixQuestDef(string questDefName, Faction faction)
        {
            return questDefName;
        }

internal object ResolveParameter(string key, object value)
        {
            if (value == null) return null;

            if (!(value is string strValue)) return value;

            if (key.ToLower().Contains("faction"))
            {
                Faction faction = Find.FactionManager.AllFactions.FirstOrDefault(f => f.Name == strValue || f.def.defName == strValue);
                if (faction != null) return faction;
            }

            if (key.ToLower().Contains("pawn") || key.ToLower() == "asker")
            {
                Pawn pawn = PawnsFinder.AllMapsWorldAndTemporary_Alive.FirstOrDefault(p => p.Name != null && p.Name.ToStringFull == strValue);
                if (pawn != null) return pawn;
            }

            if (float.TryParse(strValue, out float fResult)) return fResult;

            return value;
        }

    }
}
