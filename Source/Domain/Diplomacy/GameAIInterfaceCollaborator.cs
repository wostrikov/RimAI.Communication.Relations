using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Ustas.RimAI.Communication.Relations.Relation;
using APIResult = Ustas.RimAI.Communication.Relations.DiplomacySystem.GameAIInterface.APIResult;
using APICallRecord = Ustas.RimAI.Communication.Relations.DiplomacySystem.GameAIInterface.APICallRecord;
using DialogueApiGoodwillCostResult = Ustas.RimAI.Communication.Relations.DiplomacySystem.GameAIInterface.DialogueApiGoodwillCostResult;
using FactionCooldownEntry = Ustas.RimAI.Communication.Relations.DiplomacySystem.GameAIInterface.FactionCooldownEntry;
using RaidWaveState = Ustas.RimAI.Communication.Relations.DiplomacySystem.GameAIInterface.RaidWaveState;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
    internal abstract class GameAIInterfaceCollaborator
    {
        internal readonly GameAIInterface Owner;

        protected GameAIInterfaceCollaborator(GameAIInterface owner)
        {
            Owner = owner;
        }
        protected GameAIInterfaceParts Parts => Owner.Parts;


        protected Dictionary<int, float> _airdropFactionTradeTotals
        {
            get => Owner._airdropFactionTradeTotals;
            set => Owner._airdropFactionTradeTotals = value;
        }
        protected int _lastSuccessfulAirdropFactionId
        {
            get => Owner._lastSuccessfulAirdropFactionId;
            set => Owner._lastSuccessfulAirdropFactionId = value;
        }
        protected int _lastSuccessfulCaravanFactionId
        {
            get => Owner._lastSuccessfulCaravanFactionId;
            set => Owner._lastSuccessfulCaravanFactionId = value;
        }
        protected List<APICallRecord> _apiCallHistory
        {
            get => Owner._apiCallHistory;
            set => Owner._apiCallHistory = value;
        }
        protected Dictionary<Faction, int> _goodwillAdjustmentsToday
        {
            get => Owner._goodwillAdjustmentsToday;
            set => Owner._goodwillAdjustmentsToday = value;
        }
        protected Dictionary<Faction, Dictionary<string, int>> _factionCooldowns
        {
            get => Owner._factionCooldowns;
            set => Owner._factionCooldowns = value;
        }
        protected Dictionary<DialogueGoodwillCost.DialogueActionType, Dictionary<Faction, int>> _dialogueActionCooldowns
        {
            get => Owner._dialogueActionCooldowns;
            set => Owner._dialogueActionCooldowns = value;
        }
        protected List<DialogueActionRecord> _dialogueActionRecords
        {
            get => Owner._dialogueActionRecords;
            set => Owner._dialogueActionRecords = value;
        }
        protected int _lastResetTick
        {
            get => Owner._lastResetTick;
            set => Owner._lastResetTick = value;
        }
        protected int _raidCallEveryoneNextAvailableTick
        {
            get => Owner._raidCallEveryoneNextAvailableTick;
            set => Owner._raidCallEveryoneNextAvailableTick = value;
        }
        protected List<RaidWaveState> _raidWavesState
        {
            get => Owner._raidWavesState;
            set => Owner._raidWavesState = value;
        }
        protected void EnsureInitialized() => Owner.EnsureInitialized();
    }
}
