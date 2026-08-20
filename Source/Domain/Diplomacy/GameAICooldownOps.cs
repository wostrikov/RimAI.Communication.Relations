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
    /// <summary>Cooldown, API history, and daily reset helpers.</summary>
    internal sealed class GameAICooldownOps : GameAIInterfaceCollaborator
    {
        internal GameAICooldownOps(GameAIInterface owner) : base(owner)
        {
        }

internal float GetAirdropFactionTradeTotal(Faction faction)
        {
            if (faction == null || faction.loadID < 0)
            {
                return 0f;
            }

            return _airdropFactionTradeTotals.TryGetValue(faction.loadID, out float total)
                ? Math.Max(0f, total)
                : 0f;
        }

internal void RecordAirdropFactionTradeTotal(Faction faction, float tradeTotalSilver)
        {
            if (faction == null || faction.loadID < 0)
            {
                return;
            }

            float nextTotal = GetAirdropFactionTradeTotal(faction) + Math.Max(0f, tradeTotalSilver);
            _airdropFactionTradeTotals[faction.loadID] = nextTotal;
        }

internal void RecordSuccessfulAirdropFaction(Faction faction)
        {
            _lastSuccessfulAirdropFactionId = faction?.loadID ?? -1;
        }

internal void RecordSuccessfulCaravanFaction(Faction faction)
        {
            _lastSuccessfulCaravanFactionId = faction?.loadID ?? -1;
        }

internal bool WasLastSuccessfulAirdropFromFaction(Faction faction)
        {
            return faction != null && faction.loadID >= 0 && faction.loadID == _lastSuccessfulAirdropFactionId;
        }

internal bool WasLastSuccessfulCaravanFromFaction(Faction faction)
        {
            return faction != null && faction.loadID >= 0 && faction.loadID == _lastSuccessfulCaravanFactionId;
        }

internal float GetAirdropFactionTradeTotalForPolicy(Faction faction)
        {
            return GetAirdropFactionTradeTotal(faction);
        }

internal void InitializeCooldowns()
        {
            EnsureInitialized();
            
            _factionCooldowns.Clear();
        }

internal Dictionary<string, int> GetOrCreateFactionCooldowns(Faction faction)
        {
            EnsureInitialized();
            
            if (faction == null) return null;
            
            if (!_factionCooldowns.TryGetValue(faction, out var cooldowns))
            {
                cooldowns = new Dictionary<string, int>
                {
                    ["AdjustGoodwill"] = 0,
                    ["SendGift"] = 0,
                    ["RequestAid"] = 0,
                    ["DeclareWar"] = 0,
                    ["MakePeace"] = 0,
                    ["RequestTradeCaravan"] = 0,
                    ["RequestVisitor"] = 0,
                    ["RequestRaid"] = 0,
                    ["RequestItemAirdrop"] = 0
                };
                _factionCooldowns[faction] = cooldowns;
            }
            
            return cooldowns;
        }

public void DailyReset()
        {
            EnsureInitialized();
            _goodwillAdjustmentsToday.Clear();
            _dialogueActionRecords.Clear();
            CleanupOldRecords();
        }

internal void CleanupOldRecords()
        {
            EnsureInitialized();
            
            if (Find.TickManager == null) return;
            
            int currentTick = Find.TickManager.TicksGame;
            int maxAgeTicks = 60000 * 7;

            _apiCallHistory.RemoveAll(r => currentTick - r.TickCalled > maxAgeTicks);
        }

internal void InitializeCooldownsIfNeeded()
        {
            EnsureInitialized();
            
            if (_factionCooldowns == null)
            {
                InitializeCooldowns();
            }
        }

internal bool CheckCooldown(Faction faction, string methodName, int cooldownTicks)
        {
            InitializeCooldownsIfNeeded();

            var factionCooldowns = GetOrCreateFactionCooldowns(faction);
            if (factionCooldowns == null) return true;

            if (!factionCooldowns.TryGetValue(methodName, out int nextAvailableTick))
                return true;

            int currentTick = Find.TickManager.TicksGame;
            return currentTick >= nextAvailableTick;
        }

public int GetItemAirdropCooldownTicks(Faction faction)
        {
            if (faction == null)
            {
                return 8 * GenDate.TicksPerDay;
            }

            return GetItemAirdropCooldownTicks(faction, 1f);
        }

internal int GetItemAirdropCooldownTicks(Faction faction, float offerPercentMultiplier)
        {
            if (faction == null)
            {
                return 8 * GenDate.TicksPerDay;
            }

            float goodwillMultiplier = ResolveGoodwillCooldownMultiplier(faction);
            float merchantMultiplier = ResolveMerchantCooldownMultiplier(faction);
            float normalizedOfferPercentMultiplier = Mathf.Max(0.01f, offerPercentMultiplier);
            float crossFactionDays = WasLastSuccessfulAirdropFromFaction(faction) ? 0f : 3f;
            float cooldownDays = 8f * goodwillMultiplier * merchantMultiplier * normalizedOfferPercentMultiplier + crossFactionDays;
            float cooldownMultiplier = RelationsMod.Instance?.InstanceSettings?.ItemAirdropCooldownMultiplier ?? 1.0f;
            return Mathf.Max(1, Mathf.RoundToInt(cooldownDays * cooldownMultiplier * GenDate.TicksPerDay));
        }

internal int GetTradeCaravanCooldownTicks(Faction faction)
        {
            if (faction == null)
            {
                return 7 * GenDate.TicksPerDay;
            }

            float goodwillMultiplier = ResolveGoodwillCooldownMultiplier(faction);
            float merchantMultiplier = ResolveMerchantCooldownMultiplier(faction);
            float crossFactionDays = WasLastSuccessfulCaravanFromFaction(faction) ? 0f : 2f;
            float cooldownDays = 7f * goodwillMultiplier * merchantMultiplier + crossFactionDays;
            return Mathf.Max(1, Mathf.RoundToInt(cooldownDays * GenDate.TicksPerDay));
        }

internal float ResolveGoodwillCooldownMultiplier(Faction faction)
        {
            int goodwill = Mathf.Clamp(faction?.GoodwillWith(Faction.OfPlayer) ?? 0, 0, 100);
            return Mathf.Lerp(1f, GameAIInterface.MinimumGoodwillCooldownMultiplier, goodwill / 100f);
        }

internal float ResolveMerchantCooldownMultiplier(Faction faction)
        {
            return string.Equals(faction?.def?.defName ?? string.Empty, GameAIInterface.TradersGuildDefName, StringComparison.Ordinal)
                ? 0.8f
                : 1f;
        }

internal void SetCooldown(Faction faction, string methodName)
        {
            SetCooldown(faction, methodName, 1f);
        }

internal void SetCooldown(Faction faction, string methodName, float offerPercentMultiplier)
        {
            InitializeCooldownsIfNeeded();

            if (RelationsMod.Instance == null) return;
            var settings = RelationsMod.Instance.InstanceSettings;
            int cooldownTicks;
            if (methodName == "CreateQuest")
            {
                int minDays = settings?.MinQuestCooldownDays ?? 7;
                int maxDays = settings?.MaxQuestCooldownDays ?? 12;
                float randomDays = Rand.Range(minDays, maxDays);
                cooldownTicks = (int)(randomDays * 60000);
            }
            else
            {
                cooldownTicks = methodName switch
                {
                    "AdjustGoodwill" => settings?.GoodwillCooldownTicks ?? 2500,
                    "SendGift" => settings?.GiftCooldownTicks ?? 60000,
                    "RequestAid" => GameAIInterface.AidFactionCooldownTicks,
                    "DeclareWar" => settings?.WarCooldownTicks ?? 60000,
                    "MakePeace" => settings?.PeaceCooldownTicks ?? 60000,
                    "RequestTradeCaravan" => GetTradeCaravanCooldownTicks(faction),
                    "RequestVisitor" => GetTradeCaravanCooldownTicks(faction),
                    "RequestRaid" => settings?.RaidCooldownTicks ?? 180000,
                    "RequestRaidWaves" => 5 * 60000,
                    "RequestItemAirdrop" => GetItemAirdropCooldownTicks(faction, offerPercentMultiplier),
                    _ => 2500
                };
            }

            var factionCooldowns = GetOrCreateFactionCooldowns(faction);
            if (factionCooldowns != null && Find.TickManager != null)
                factionCooldowns[methodName] = Find.TickManager.TicksGame + cooldownTicks;
        }

public int GetRemainingCooldownSeconds(Faction faction, string methodName)
        {
            InitializeCooldownsIfNeeded();
            EnsureInitialized();

            if (faction == null) return 0;

            var factionCooldowns = GetOrCreateFactionCooldowns(faction);
            if (factionCooldowns == null) return 0;

            if (!factionCooldowns.TryGetValue(methodName, out int nextAvailableTick))
                return 0;

            if (Find.TickManager == null) return 0;

            int remainingTicks = nextAvailableTick - Find.TickManager.TicksGame;
            return Math.Max(0, remainingTicks / 60);
        }

public Dictionary<string, int> GetFactionCooldownOverview(Faction faction)
        {
            InitializeCooldownsIfNeeded();
            EnsureInitialized();

            if (faction == null) return new Dictionary<string, int>();

            var factionCooldowns = GetOrCreateFactionCooldowns(faction);
            if (factionCooldowns == null) return new Dictionary<string, int>();

            var result = new Dictionary<string, int>();
            int currentTick = Find.TickManager?.TicksGame ?? 0;

            foreach (var kvp in factionCooldowns)
            {
                int remainingTicks = kvp.Value - currentTick;
                result[kvp.Key] = Math.Max(0, remainingTicks / 60);
            }

            return result;
        }

public int GetRaidCallEveryoneRemainingCooldownSeconds()
        {
            if (Find.TickManager == null) return 0;
            int remaining = _raidCallEveryoneNextAvailableTick - Find.TickManager.TicksGame;
            return Math.Max(0, remaining / 60);
        }

public void SetRaidCallEveryoneCooldown()
        {
            if (Find.TickManager == null) return;
            _raidCallEveryoneNextAvailableTick = Find.TickManager.TicksGame + (15 * GenDate.TicksPerDay);
        }

public bool IsRaidCallEveryoneAvailable()
        {
            return GetRaidCallEveryoneRemainingCooldownSeconds() <= 0;
        }

public void SetFactionCooldown(Faction faction, string methodName)
        {
            SetCooldown(faction, methodName);
        }

internal void RecordAPICall(string methodName, bool success, string parameters, string errorMessage = "")
        {
            try
            {
                EnsureInitialized();
                
                if (Find.TickManager == null)
                {
                    return;
                }

                var record = new APICallRecord
                {
                    MethodName = methodName,
                    TickCalled = Find.TickManager.TicksGame,
                    Parameters = parameters,
                    Success = success,
                    ErrorMessage = errorMessage
                };

                _apiCallHistory.Add(record);

                if (RelationsMod.Instance != null && (RelationsMod.Instance.InstanceSettings?.EnableDebugLogging ?? false))
                {
                    string status = success ? "SUCCESS" : "FAILED";
                    DebugLogger.Debug($"API Call [{status}]: {methodName} - {parameters}");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Failed to record API call: {ex.Message}");
            }
        }

public List<APICallRecord> GetAPICallHistory(string methodName = null, int maxRecords = 50)
        {
            EnsureInitialized();
            
            var query = _apiCallHistory.AsEnumerable();

            if (!string.IsNullOrEmpty(methodName))
                query = query.Where(r => r.MethodName == methodName);

            return query
                .OrderByDescending(r => r.TickCalled)
                .Take(maxRecords)
                .ToList();
        }

    }
}
