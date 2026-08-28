using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Ustas.RimAI.Communication.Relations.Diagnostics;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Patches
{
    public static class QuestGenPatch
    {
        // Hard constraint — changing this breaks an invariant. (summary asker/faction summary)
        public static bool LockSlateVariables = false;

        /// <summary>/// initialize Patch
 ///</summary>
        public static void Initialize(Harmony harmony)
        {
            // 0. Patch Slate.Set to implement locking (Disabled: Generic open methods cannot be patched simply)
            /* Try {
 var slateSet = AccessTools.Method(typeof(Slate), "Set");
 if (slateSet != null)
 harmony.Patch(slateSet, prefix: new HarmonyMethod(typeof(QuestGenPatch), nameof(Prefix_SlateSet)));
 } catch (Exception ex) { DebugLogger.WarningGated($"Failed patch Slate.Set: {ex.Message}"); } */

            // 1. Patch QuestNode_GetNearbySettlement.RunInt
            try {
                var target1 = AccessTools.Method(typeof(QuestNode_GetNearbySettlement), "RunInt");
                if (target1 != null)
                    harmony.Patch(target1, prefix: new HarmonyMethod(typeof(QuestGenPatch), nameof(Prefix_GetNearbySettlement)));
            } catch (Exception ex) { DebugLogger.WarningGated($"Failed patch GetNearbySettlement: {ex.Message}"); }

            // 2. Patch QuestNode_GetFactionOf.RunInt
            try {
                var target2 = AccessTools.Method(typeof(QuestNode_GetFactionOf), "RunInt");
                if (target2 != null)
                    harmony.Patch(target2, prefix: new HarmonyMethod(typeof(QuestGenPatch), nameof(Prefix_GetFactionOf)));
            } catch (Exception ex) { DebugLogger.WarningGated($"Failed patch GetFactionOf: {ex.Message}"); }

            // 3. Mission_BanditCamp is disabled by safety policy; skip patching its quest node to avoid startup warnings.

            // 4. Patch producer nodes to prevent overwriting 'asker' and 'faction'
            string[] producerNodes = { 
                "RimWorld.QuestGen.QuestNode_GetPawn", 
                "RimWorld.QuestGen.QuestNode_GetFaction",
                "RimWorld.QuestGen.QuestNode_GetSiteFaction"
            };
            foreach (var nodeName in producerNodes)
            {
                try {
                    Type nodeType = AccessTools.TypeByName(nodeName);
                    if (nodeType != null)
                    {
                        var method = AccessTools.Method(nodeType, "RunInt");
                        if (method != null)
                            harmony.Patch(method, prefix: new HarmonyMethod(typeof(QuestGenPatch), nameof(Prefix_PreventOverwrite)));
                    }
                } catch (Exception ex) { DebugLogger.WarningGated($"Failed patch {nodeName}: {ex.Message}"); }
            }

            // 5. Patch QuestNode_GiveRewards to force giverFaction
            try {
                var giveRewardsType = AccessTools.TypeByName("RimWorld.QuestGen.QuestNode_GiveRewards");
                if (giveRewardsType != null)
                {
                    var method = AccessTools.Method(giveRewardsType, "RunInt");
                    if (method != null)
                        harmony.Patch(method, prefix: new HarmonyMethod(typeof(QuestGenPatch), nameof(Prefix_ForceGiverFaction)));
                }
            } catch (Exception ex) { DebugLogger.WarningGated($"Failed patch GiveRewards: {ex.Message}"); }

            // 6. Patch QuestNode_HasRoyalTitleInCurrentFaction
            try {
                Type hasRoyalTitleType = AccessTools.TypeByName("RimWorld.QuestGen.QuestNode_HasRoyalTitleInCurrentFaction");
                if (hasRoyalTitleType != null)
                {
                    var method = AccessTools.Method(hasRoyalTitleType, "RunInt");
                    if (method != null)
                        harmony.Patch(method, prefix: new HarmonyMethod(typeof(QuestGenPatch), nameof(Prefix_HasRoyalTitleInCurrentFaction)));
                }
            } catch (Exception ex) { DebugLogger.WarningGated($"Failed patch HasRoyalTitleInCurrentFaction: {ex.Message}"); }
        }

        public static bool Prefix_SlateSet(Slate __instance, string name, object var)
        {
            if (LockSlateVariables)
            {
                if (name == "asker" || name == "faction" || name == "askerFaction" || name == "giverFaction" || name == "enemyFaction" || name == "siteFaction")
                {
                    if (__instance.Exists(name))
                    {
                        try
                        {
                            object current = __instance.Get<object>(name);
                            if (current != null)
                            {
                                return false;
                            }
                        }
                        // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY - quest slot probe treated the slot as unset
                        catch (System.Exception ex)
                        {
                            ModuleLog.Message("[RimAI.Relations] quest slot probe treated the slot as unset: " + ex.Message);
                        }
                    }

                    if (var == null) return false;
                }
                
                if (name == "colonistCount" || name == "requiredPawnCount")
                {
                    if (__instance.Exists(name))
                    {
                        try
                        {
                            int current = __instance.Get<int>(name);
                            if (current > 0)
                            {
                                if (var != null)
                                {
                                    int newInt = -1;
                                    if (var is int i)
                                    {
                                        newInt = i;
                                    }
                                    else if (var.GetType().IsValueType)
                                    {
                                        try { newInt = Convert.ToInt32(var); }
                                        // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY - quest slot probe treated the slot as unset
                                        catch (System.Exception ex)
                                        {
                                            ModuleLog.Message("[RimAI.Relations] quest slot probe treated the slot as unset: " + ex.Message);
                                        }
                                    }
                                    
                                    if (newInt <= 0)
                                    {
                                        return false;
                                    }
                                }
                            }
                        }
                        // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY - quest slot probe treated the slot as unset
                        catch (System.Exception ex)
                        {
                            ModuleLog.Message("[RimAI.Relations] quest slot probe treated the slot as unset: " + ex.Message);
                        }
                    }
                }
            }
            return true;
        }

        public static bool Prefix_ForceGiverFaction(QuestNode __instance)
        {
            if (!LockSlateVariables) return true;
            try
            {
                var slate = QuestGen.slate;
                if (slate.Exists("faction"))
                {
                    Faction f = slate.Get<Faction>("faction");
                    if (f != null)
                    {
                        string[] fieldNames = { "giverFaction", "faction", "askerFaction" };

                        foreach (var fieldName in fieldNames)
                        {
                            try
                            {
                                var field = AccessTools.Field(__instance.GetType(), fieldName);
                                if (field == null) continue;

                                object slateRef = field.GetValue(__instance);
                                if (slateRef == null) continue;

                                var sliField = AccessTools.Field(slateRef.GetType(), "sli");
                                if (sliField == null) continue;

                                string currentSli = sliField.GetValue(slateRef) as string;
                                if (string.IsNullOrEmpty(currentSli) || (!currentSli.Contains("faction") && !currentSli.Contains("giverFaction")))
                                {
                                    sliField.SetValue(slateRef, "$faction");
                                }
                            }
                            catch (Exception ex)
                            {
                                DebugLogger.WarningGated($"QuestGenPatch: failed to patch field '{fieldName}' on {__instance.GetType().Name}: {ex.Message}");
                            }
                        }

                        if (!slate.Exists("giverFaction"))
                        {
                            slate.Set("giverFaction", f);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.WarningGated($"QuestGenPatch.Prefix_ForceGiverFaction failed: {ex.Message}");
            }
            return true;
        }

        public static bool Prefix_PreventOverwrite(QuestNode __instance)
        {
            if (!LockSlateVariables) return true;
            try
            {
                var slate = QuestGen.slate;
                string[] protectedVars = { "asker", "faction", "askerFaction", "settlement", "giverFaction", "enemyFaction", "siteFaction" };
                string[] storageFields = { "storeAs", "storeFactionAs", "storeFactionLeaderAs", "storeSettlementAs" };

                foreach (var fieldName in storageFields)
                {
                    try
                    {
                        var field = AccessTools.Field(__instance.GetType(), fieldName);
                        if (field == null) continue;

                        object fieldValue = field.GetValue(__instance);
                        if (fieldValue == null) continue;

                        string varName = null;
                        if (fieldValue is string s)
                        {
                            varName = s;
                        }
                        else
                        {
                            var getter = AccessTools.Method(fieldValue.GetType(), "GetValue", new[] { typeof(Slate) });
                            if (getter != null)
                            {
                                varName = getter.Invoke(fieldValue, new object[] { slate }) as string;
                            }
                        }

                        if (!string.IsNullOrEmpty(varName) && protectedVars.Contains(varName))
                        {
                            if (slate.Exists(varName))
                            {
                                return false;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.WarningGated($"QuestGenPatch.Prefix_PreventOverwrite: failed on field '{fieldName}' of {__instance.GetType().Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.WarningGated($"QuestGenPatch.Prefix_PreventOverwrite failed: {ex.Message}");
            }
            return true;
        }

        public static bool Prefix_GetNearbySettlement(QuestNode_GetNearbySettlement __instance)
        {
            if (!LockSlateVariables) return true;
            var slate = QuestGen.slate;
            string storeAs = __instance.storeAs.GetValue(slate);

            if (!string.IsNullOrEmpty(storeAs) && slate.Exists(storeAs))
            {
                object existing = slate.Get<object>(storeAs);
                if (existing is Settlement s && s.Spawned)
                {
                    string storeFactionLeaderAs = __instance.storeFactionLeaderAs.GetValue(slate);
                    if (!string.IsNullOrEmpty(storeFactionLeaderAs) && !slate.Exists(storeFactionLeaderAs))
                    {
                        if (s.Faction?.leader != null)
                            slate.Set(storeFactionLeaderAs, s.Faction.leader);
                    }

                    return false;
                }
            }
            return true;
        }

        public static bool Prefix_GetFactionOf(QuestNode_GetFactionOf __instance)
        {
            if (!LockSlateVariables) return true;
            var slate = QuestGen.slate;
            string storeAs = __instance.storeAs.GetValue(slate);

            if (!string.IsNullOrEmpty(storeAs) && slate.Exists(storeAs))
            {
                object existing = slate.Get<object>(storeAs);
                if (existing is Faction)
                {
                    return false;
                }
            }
            return true;
        }

        public static bool Prefix_HasRoyalTitleInCurrentFaction(QuestNode __instance)
        {
            if (!LockSlateVariables) return true;
            try
            {
                var slate = QuestGen.slate;
                if (!slate.Exists("faction"))
                {
                    return true;
                }

                Faction faction = slate.Get<Faction>("faction");
                if (faction == null)
                {
                    return true;
                }

                bool isEmpire = faction.def == FactionDefOf.Empire;
                if (isEmpire)
                {
                    return true;
                }

                var nodeField = AccessTools.Field(__instance.GetType(), "node");
                if (nodeField == null)
                {
                    return true;
                }

                QuestNode node = nodeField.GetValue(__instance) as QuestNode;
                if (node == null)
                {
                    return true;
                }

                PatchGiveRewardsNodeForNonEmpireFaction(node, faction);

                node.Run();

                return false;
            }
            catch (Exception ex)
            {
                DebugLogger.WarningGated($"QuestGenPatch.Prefix_HasRoyalTitleInCurrentFaction failed: {ex.Message}");
                return true;
            }
        }

        private static void PatchGiveRewardsNodeForNonEmpireFaction(QuestNode node, Faction faction)
        {
            if (node == null) return;

            try
            {
                var nodeType = node.GetType();
                if (nodeType.Name != "QuestNode_GiveRewards") return;

                var parmsField = AccessTools.Field(nodeType, "parms");
                if (parmsField == null) return;

                object parms = parmsField.GetValue(node);
                if (parms == null) return;

                var parmsType = parms.GetType();

                var allowRoyalFavorField = AccessTools.Field(parmsType, "allowRoyalFavor");
                if (allowRoyalFavorField != null)
                {
                    allowRoyalFavorField.SetValue(parms, false);
                }

                var allowGoodwillField = AccessTools.Field(parmsType, "allowGoodwill");
                if (allowGoodwillField != null)
                {
                    allowGoodwillField.SetValue(parms, true);
                }

                var thingRewardItemsOnlyField = AccessTools.Field(parmsType, "thingRewardItemsOnly");
                if (thingRewardItemsOnlyField != null)
                {
                    thingRewardItemsOnlyField.SetValue(parms, false);
                }

                if (!QuestGen.slate.Exists("giverFaction"))
                {
                    QuestGen.slate.Set("giverFaction", faction);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.WarningGated($"QuestGenPatch.PatchGiveRewardsNodeForNonEmpireFaction failed on {node.GetType().Name}: {ex.Message}");
            }
        }

        public static bool Prefix_Mission_BanditCamp(QuestNode __instance)
        {
            try
            {
                var slate = QuestGen.slate;
                if (slate.Exists("asker") && slate.Exists("faction"))
                {
                    var field = AccessTools.Field(__instance.GetType(), "factionsToDrawLeaderFrom");
                    if (field != null)
                    {
                        var list = field.GetValue(__instance) as List<FactionDef>;
                        if (list != null)
                        {
                            Faction f = slate.Get<Faction>("faction");
                            if (f != null && !list.Contains(f.def))
                            {
                                list.Add(f.def);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.WarningGated($"QuestGenPatch.Prefix_Mission_BanditCamp failed: {ex.Message}");
            }
            return true;
        }

        public static void Postfix_GetRequiredPawnCount(ref int __result)
        {
            if (!LockSlateVariables) return;
            var slate = QuestGen.slate;
            if (slate != null && slate.Exists("requiredPawnCount"))
            {
                int slateCount = slate.Get<int>("requiredPawnCount");
                if (slateCount > 0)
                {
                    __result = slateCount;
                }
            }
        }
    }
}
