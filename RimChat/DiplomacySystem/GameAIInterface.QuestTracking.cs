using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Util;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
    public class RimChatQuestPublicationRecord : IExposable
    {
        public int QuestId;
        public string QuestDefName;
        public string FactionUniqueId;
        public string FactionDefName;
        public string FactionName;
        public int PublishedTick;

        public RimChatQuestPublicationRecord()
        {
            QuestDefName = string.Empty;
            FactionUniqueId = string.Empty;
            FactionDefName = string.Empty;
            FactionName = string.Empty;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref QuestId, "questId", -1);
            Scribe_Values.Look(ref QuestDefName, "questDefName", string.Empty);
            Scribe_Values.Look(ref FactionUniqueId, "factionUniqueId", string.Empty);
            Scribe_Values.Look(ref FactionDefName, "factionDefName", string.Empty);
            Scribe_Values.Look(ref FactionName, "factionName", string.Empty);
            Scribe_Values.Look(ref PublishedTick, "publishedTick", 0);
        }
    }

    public class RimChatFactionQuestRuntimeRecord : IExposable
    {
        public int QuestId;
        public string QuestDefName;
        public string QuestName;
        public string QuestDescription;
        public List<string> InvolvedFactionIds = new List<string>();
        public List<string> InvolvedFactionDefNames = new List<string>();
        public int PublishedTick;
        public int LastKnownState;
        public bool CompletionRecorded;

        public void ExposeData()
        {
            Scribe_Values.Look(ref QuestId, "questId", -1);
            Scribe_Values.Look(ref QuestDefName, "questDefName", string.Empty);
            Scribe_Values.Look(ref QuestName, "questName", string.Empty);
            Scribe_Values.Look(ref QuestDescription, "questDescription", string.Empty);
            Scribe_Collections.Look(ref InvolvedFactionIds, "involvedFactionIds", LookMode.Value);
            Scribe_Collections.Look(ref InvolvedFactionDefNames, "involvedFactionDefNames", LookMode.Value);
            Scribe_Values.Look(ref PublishedTick, "publishedTick", 0);
            Scribe_Values.Look(ref LastKnownState, "lastKnownState", 0);
            Scribe_Values.Look(ref CompletionRecorded, "completionRecorded", false);
            InvolvedFactionIds ??= new List<string>();
            InvolvedFactionDefNames ??= new List<string>();
            QuestDefName ??= string.Empty;
            QuestName ??= string.Empty;
            QuestDescription ??= string.Empty;
        }
    }

    public class RimChatFactionQuestCompletionRecord : IExposable
    {
        public int QuestId;
        public string QuestDefName;
        public string QuestName;
        public string QuestDescription;
        public string FactionUniqueId;
        public string FactionDefName;
        public string FactionName;
        public int EndedTick;
        public bool Succeeded;

        public void ExposeData()
        {
            Scribe_Values.Look(ref QuestId, "questId", -1);
            Scribe_Values.Look(ref QuestDefName, "questDefName", string.Empty);
            Scribe_Values.Look(ref QuestName, "questName", string.Empty);
            Scribe_Values.Look(ref QuestDescription, "questDescription", string.Empty);
            Scribe_Values.Look(ref FactionUniqueId, "factionUniqueId", string.Empty);
            Scribe_Values.Look(ref FactionDefName, "factionDefName", string.Empty);
            Scribe_Values.Look(ref FactionName, "factionName", string.Empty);
            Scribe_Values.Look(ref EndedTick, "endedTick", 0);
            Scribe_Values.Look(ref Succeeded, "succeeded", false);
            QuestDefName ??= string.Empty;
            QuestName ??= string.Empty;
            QuestDescription ??= string.Empty;
            FactionUniqueId ??= string.Empty;
            FactionDefName ??= string.Empty;
            FactionName ??= string.Empty;
        }
    }

    public partial class GameAIInterface
    {
        private const int MaxQuestCompletionHistory = 40;
        private List<RimChatQuestPublicationRecord> _rimChatQuestPublicationRecords = new List<RimChatQuestPublicationRecord>();
        private List<RimChatFactionQuestRuntimeRecord> _rimChatFactionQuestRuntimeRecords = new List<RimChatFactionQuestRuntimeRecord>();
        private List<RimChatFactionQuestCompletionRecord> _rimChatFactionQuestCompletionRecords = new List<RimChatFactionQuestCompletionRecord>();
        private int _questTrackingRevision;

        public int QuestTrackingRevision => _questTrackingRevision;

        internal void ExposeQuestPublicationData()
        {
            Scribe_Collections.Look(ref _rimChatQuestPublicationRecords, "rimChatQuestPublicationRecords", LookMode.Deep);
            Scribe_Collections.Look(ref _rimChatFactionQuestRuntimeRecords, "rimChatFactionQuestRuntimeRecords", LookMode.Deep);
            Scribe_Collections.Look(ref _rimChatFactionQuestCompletionRecords, "rimChatFactionQuestCompletionRecords", LookMode.Deep);
            Scribe_Values.Look(ref _questTrackingRevision, "questTrackingRevision", 0);
            if (Scribe.mode != LoadSaveMode.PostLoadInit)
            {
                return;
            }

            _rimChatQuestPublicationRecords ??= new List<RimChatQuestPublicationRecord>();
            _rimChatFactionQuestRuntimeRecords ??= new List<RimChatFactionQuestRuntimeRecord>();
            _rimChatFactionQuestCompletionRecords ??= new List<RimChatFactionQuestCompletionRecord>();
            CleanupQuestPublicationRecords();
            CleanupQuestTrackingRecords();
        }

        internal static HashSet<int> CaptureCurrentQuestIdsForTracking()
        {
            List<Quest> quests = Find.QuestManager?.QuestsListForReading;
            if (quests == null || quests.Count == 0)
            {
                return new HashSet<int>();
            }

            return new HashSet<int>(quests.Where(quest => quest != null).Select(quest => quest.id));
        }

        internal void TryTrackCreateQuestResult(
            string requestedQuestDefName,
            Dictionary<string, object> parameters,
            APIResult result,
            HashSet<int> questIdsBefore)
        {
            if (result == null || !result.Success)
            {
                return;
            }

            Faction faction = ResolveQuestPublicationFaction(parameters);
            if (faction == null)
            {
                return;
            }

            Quest createdQuest = ResolveNewQuestFromSnapshot(questIdsBefore);
            if (createdQuest == null)
            {
                return;
            }

            string normalizedDefName = ResolveQuestDefNameFromResult(result, requestedQuestDefName);
            var record = new RimChatQuestPublicationRecord
            {
                QuestId = createdQuest.id,
                QuestDefName = normalizedDefName,
                FactionUniqueId = GetFactionUniqueId(faction),
                FactionDefName = faction.def?.defName ?? string.Empty,
                FactionName = faction.Name ?? string.Empty,
                PublishedTick = Find.TickManager?.TicksGame ?? 0
            };

            UpsertQuestPublicationRecord(record);
            UpsertQuestRuntimeRecord(createdQuest, normalizedDefName);
            BumpQuestTrackingRevision();
        }

        internal void RefreshQuestTrackingState()
        {
            _rimChatFactionQuestRuntimeRecords ??= new List<RimChatFactionQuestRuntimeRecord>();
            List<Quest> quests = Find.QuestManager?.QuestsListForReading ?? new List<Quest>();
            bool changed = false;

            foreach (RimChatFactionQuestRuntimeRecord record in _rimChatFactionQuestRuntimeRecords.Where(item => item != null).ToList())
            {
                Quest liveQuest = quests.FirstOrDefault(item => item != null && item.id == record.QuestId);
                if (liveQuest != null)
                {
                    int stateValue = (int)liveQuest.State;
                    if (record.LastKnownState != stateValue)
                    {
                        record.LastKnownState = stateValue;
                        changed = true;
                    }

                    if (!record.CompletionRecorded && liveQuest.State != QuestState.Ongoing)
                    {
                        TryFinalizeEndedQuestRecord(record, liveQuest, IsQuestSucceeded(liveQuest, record.LastKnownState));
                        changed = true;
                    }

                    continue;
                }

                if (!record.CompletionRecorded)
                {
                    bool succeeded = IsQuestSucceeded(null, record.LastKnownState);
                    TryFinalizeEndedQuestRecord(record, null, succeeded);
                    changed = true;
                }
            }

            if (changed)
            {
                CleanupQuestTrackingRecords();
                BumpQuestTrackingRevision();
            }
        }

        internal RimChatFactionQuestCompletionRecord GetLatestCompletedQuestForFaction(Faction faction)
        {
            if (faction == null)
            {
                return null;
            }

            RefreshQuestTrackingState();
            string factionId = GetFactionUniqueId(faction);
            string factionDefName = faction.def?.defName ?? string.Empty;
            return (_rimChatFactionQuestCompletionRecords ?? new List<RimChatFactionQuestCompletionRecord>())
                .Where(record => record != null && IsCompletionRecordFactionMatch(record, factionId, factionDefName))
                .OrderByDescending(record => record.EndedTick)
                .FirstOrDefault();
        }

        private void UpsertQuestPublicationRecord(RimChatQuestPublicationRecord record)
        {
            _rimChatQuestPublicationRecords ??= new List<RimChatQuestPublicationRecord>();
            int existingIndex = _rimChatQuestPublicationRecords.FindIndex(item => item != null && item.QuestId == record.QuestId);
            if (existingIndex >= 0)
            {
                _rimChatQuestPublicationRecords[existingIndex] = record;
            }
            else
            {
                _rimChatQuestPublicationRecords.Add(record);
            }
        }

        private void UpsertQuestRuntimeRecord(Quest quest, string questDefName)
        {
            if (quest == null)
            {
                return;
            }

            _rimChatFactionQuestRuntimeRecords ??= new List<RimChatFactionQuestRuntimeRecord>();
            var record = new RimChatFactionQuestRuntimeRecord
            {
                QuestId = quest.id,
                QuestDefName = questDefName ?? string.Empty,
                QuestName = ResolveQuestName(quest, questDefName),
                QuestDescription = ResolveQuestDescription(quest),
                PublishedTick = Find.TickManager?.TicksGame ?? 0,
                LastKnownState = (int)quest.State,
                CompletionRecorded = false,
                InvolvedFactionIds = ResolveQuestFactionIds(quest),
                InvolvedFactionDefNames = ResolveQuestFactionDefNames(quest)
            };

            int existingIndex = _rimChatFactionQuestRuntimeRecords.FindIndex(item => item != null && item.QuestId == record.QuestId);
            if (existingIndex >= 0)
            {
                _rimChatFactionQuestRuntimeRecords[existingIndex] = record;
            }
            else
            {
                _rimChatFactionQuestRuntimeRecords.Add(record);
            }
        }

        private void TryFinalizeEndedQuestRecord(RimChatFactionQuestRuntimeRecord runtimeRecord, Quest liveQuest, bool succeeded)
        {
            if (runtimeRecord == null || runtimeRecord.CompletionRecorded)
            {
                return;
            }

            _rimChatFactionQuestCompletionRecords ??= new List<RimChatFactionQuestCompletionRecord>();
            int endedTick = Find.TickManager?.TicksGame ?? runtimeRecord.PublishedTick;
            List<string> factionIds = runtimeRecord.InvolvedFactionIds ?? new List<string>();
            List<string> factionDefNames = runtimeRecord.InvolvedFactionDefNames ?? new List<string>();
            for (int i = 0; i < Math.Max(factionIds.Count, factionDefNames.Count); i++)
            {
                string factionId = i < factionIds.Count ? factionIds[i] ?? string.Empty : string.Empty;
                string factionDefName = i < factionDefNames.Count ? factionDefNames[i] ?? string.Empty : string.Empty;
                Faction faction = ResolveFactionByIdentity(factionId, factionDefName);
                _rimChatFactionQuestCompletionRecords.Add(new RimChatFactionQuestCompletionRecord
                {
                    QuestId = runtimeRecord.QuestId,
                    QuestDefName = runtimeRecord.QuestDefName ?? string.Empty,
                    QuestName = !string.IsNullOrWhiteSpace(runtimeRecord.QuestName) ? runtimeRecord.QuestName : ResolveQuestName(liveQuest, runtimeRecord.QuestDefName),
                    QuestDescription = !string.IsNullOrWhiteSpace(runtimeRecord.QuestDescription) ? runtimeRecord.QuestDescription : ResolveQuestDescription(liveQuest),
                    FactionUniqueId = factionId,
                    FactionDefName = factionDefName,
                    FactionName = faction?.Name ?? string.Empty,
                    EndedTick = endedTick,
                    Succeeded = succeeded
                });
            }

            runtimeRecord.CompletionRecorded = true;
            TrimQuestCompletionHistory();
        }

        private void TrimQuestCompletionHistory()
        {
            _rimChatFactionQuestCompletionRecords = (_rimChatFactionQuestCompletionRecords ?? new List<RimChatFactionQuestCompletionRecord>())
                .Where(item => item != null)
                .OrderByDescending(item => item.EndedTick)
                .Take(MaxQuestCompletionHistory)
                .ToList();
        }

        private void CleanupQuestPublicationRecords()
        {
            _rimChatQuestPublicationRecords ??= new List<RimChatQuestPublicationRecord>();
            List<Quest> quests = Find.QuestManager?.QuestsListForReading ?? new List<Quest>();
            _rimChatQuestPublicationRecords = _rimChatQuestPublicationRecords
                .Where(record => record != null && record.QuestId >= 0)
                .Where(record => quests.Any(quest => quest != null && quest.id == record.QuestId))
                .ToList();
        }

        private void CleanupQuestTrackingRecords()
        {
            _rimChatFactionQuestRuntimeRecords = (_rimChatFactionQuestRuntimeRecords ?? new List<RimChatFactionQuestRuntimeRecord>())
                .Where(record => record != null && record.QuestId >= 0)
                .Where(record => !record.CompletionRecorded)
                .ToList();
            TrimQuestCompletionHistory();
        }

        private static bool IsCompletionRecordFactionMatch(RimChatFactionQuestCompletionRecord record, string factionId, string factionDefName)
        {
            if (record == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(record.FactionUniqueId) && !string.IsNullOrWhiteSpace(factionId))
            {
                if (string.Equals(record.FactionUniqueId, factionId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return !string.IsNullOrWhiteSpace(record.FactionDefName) &&
                   !string.IsNullOrWhiteSpace(factionDefName) &&
                   string.Equals(record.FactionDefName, factionDefName, StringComparison.OrdinalIgnoreCase);
        }

        internal bool HasActiveRimChatQuestForFaction(Faction faction)
        {
            if (faction == null)
            {
                return false;
            }

            string factionId = GetFactionUniqueId(faction);
            string factionDefName = faction.def?.defName ?? string.Empty;
            return (_rimChatFactionQuestRuntimeRecords ?? new List<RimChatFactionQuestRuntimeRecord>())
                .Where(record => record != null && !record.CompletionRecorded)
                .Any(record =>
                    (record.InvolvedFactionIds ?? new List<string>()).Any(id =>
                        !string.IsNullOrWhiteSpace(id) &&
                        !string.IsNullOrWhiteSpace(factionId) &&
                        string.Equals(id, factionId, StringComparison.Ordinal)) ||
                    (record.InvolvedFactionDefNames ?? new List<string>()).Any(defName =>
                        !string.IsNullOrWhiteSpace(defName) &&
                        !string.IsNullOrWhiteSpace(factionDefName) &&
                        string.Equals(defName, factionDefName, StringComparison.OrdinalIgnoreCase)));
        }

        private static Faction ResolveQuestPublicationFaction(Dictionary<string, object> parameters)
        {
            if (parameters == null)
            {
                return null;
            }

            if (parameters.TryGetValue("faction", out object factionObject) && factionObject is Faction faction)
            {
                return faction;
            }

            if (parameters.TryGetValue("targetFaction", out object targetFactionObject) && targetFactionObject is Faction targetFaction)
            {
                return targetFaction;
            }

            return null;
        }

        private static Quest ResolveNewQuestFromSnapshot(HashSet<int> questIdsBefore)
        {
            List<Quest> quests = Find.QuestManager?.QuestsListForReading;
            if (quests == null || quests.Count == 0)
            {
                return null;
            }

            return quests.FirstOrDefault(quest => quest != null && (questIdsBefore == null || !questIdsBefore.Contains(quest.id)));
        }

        private static string ResolveQuestDefNameFromResult(APIResult result, string requestedQuestDefName)
        {
            if (!string.IsNullOrWhiteSpace(requestedQuestDefName))
            {
                return requestedQuestDefName.Trim();
            }

            string message = result?.Message?.Trim();
            return string.IsNullOrWhiteSpace(message) ? string.Empty : message;
        }

        private static string GetFactionUniqueId(Faction faction)
        {
            return faction?.GetUniqueLoadID() ?? string.Empty;
        }

        private static bool IsQuestSucceeded(Quest quest, int lastKnownState)
        {
            if (quest != null)
            {
                return quest.State == QuestState.EndedSuccess;
            }

            return lastKnownState == (int)QuestState.EndedSuccess;
        }

        private static List<string> ResolveQuestFactionIds(Quest quest)
        {
            return QuestInvolvedFactionsGuard.GetInvolvedFactionsSafe(quest)
                .Where(faction => faction != null)
                .Select(GetFactionUniqueId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static List<string> ResolveQuestFactionDefNames(Quest quest)
        {
            return QuestInvolvedFactionsGuard.GetInvolvedFactionsSafe(quest)
                .Where(faction => faction != null && !string.IsNullOrWhiteSpace(faction.def?.defName))
                .Select(faction => faction.def.defName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string ResolveQuestName(Quest quest, string fallbackDefName)
        {
            if (!string.IsNullOrWhiteSpace(quest?.name))
            {
                return quest.name.Trim();
            }

            string rootTypeName = quest?.root?.GetType().Name;
            if (!string.IsNullOrWhiteSpace(rootTypeName))
            {
                return rootTypeName.Trim();
            }

            return fallbackDefName?.Trim() ?? string.Empty;
        }

        private static string ResolveQuestDescription(Quest quest)
        {
            if (quest == null)
            {
                return string.Empty;
            }

            string description = quest.description.ToString();
            return (description ?? string.Empty).Trim();
        }

        private static Faction ResolveFactionByIdentity(string factionId, string factionDefName)
        {
            List<Faction> factions = Find.FactionManager?.AllFactionsListForReading;
            if (factions == null)
            {
                return null;
            }

            return factions.FirstOrDefault(faction =>
                faction != null &&
                ((!string.IsNullOrWhiteSpace(factionId) && string.Equals(GetFactionUniqueId(faction), factionId, StringComparison.Ordinal)) ||
                 (!string.IsNullOrWhiteSpace(factionDefName) && string.Equals(faction.def?.defName, factionDefName, StringComparison.OrdinalIgnoreCase))));
        }

        private void BumpQuestTrackingRevision()
        {
            unchecked
            {
                _questTrackingRevision++;
            }
        }
    }
}
