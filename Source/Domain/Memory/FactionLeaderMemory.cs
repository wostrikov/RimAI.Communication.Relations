using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace Ustas.RimAI.Communication.Relations.Memory
{
    public class FactionLeaderMemory : IExposable
    {
        public string OwnerFactionId { get; set; }
        
        public string OwnerFactionName { get; set; }
        
        public string LeaderName { get; set; }
        
        public List<FactionMemoryEntry> FactionMemories = new List<FactionMemoryEntry>();
        
        public List<SignificantEventMemory> SignificantEvents = new List<SignificantEventMemory>();
        
        public List<DialogueRecord> DialogueHistory = new List<DialogueRecord>();

        public List<CrossChannelSummaryRecord> RpgDepartSummaries = new List<CrossChannelSummaryRecord>();

        public List<CrossChannelSummaryRecord> DiplomacySessionSummaries = new List<CrossChannelSummaryRecord>();
        
        public int LastUpdatedTick { get; set; }
        
        public long CreatedTimestamp { get; set; }
        
        public long LastSavedTimestamp { get; set; }
        
        public FactionLeaderMemory()
        {
            CreatedTimestamp = DateTime.Now.Ticks;
        }

        public FactionLeaderMemory(Faction ownerFaction) : this()
        {
            OwnerFactionId = GetUniqueFactionId(ownerFaction);
            OwnerFactionName = ownerFaction.Name;
            LeaderName = ownerFaction.leader?.Name?.ToStringFull ?? "Unknown";
            LastUpdatedTick = Find.TickManager.TicksGame;
            
        }

        private void InitializeFactionMemories(Faction ownerFaction)
        {
            var allFactions = Find.FactionManager.AllFactions;
            foreach (var faction in allFactions)
            {
                if (faction != ownerFaction && !faction.IsPlayer && !faction.defeated)
                {
                    FactionMemories.Add(new FactionMemoryEntry
                    {
                        FactionId = GetUniqueFactionId(faction),
                        FactionName = faction.Name,
                        FirstContactTick = Find.TickManager.TicksGame,
                        RelationHistory = new List<RelationSnapshot>()
                    });
                }
            }
        }

        public FactionMemoryEntry GetOrCreateMemory(Faction targetFaction)
        {
            var factionId = GetUniqueFactionId(targetFaction);
            var memory = FactionMemories.Find(m => m.FactionId == factionId);
            
            if (memory == null)
            {
                memory = new FactionMemoryEntry
                {
                    FactionId = factionId,
                    FactionName = targetFaction.Name,
                    FirstContactTick = Find.TickManager.TicksGame,
                    RelationHistory = new List<RelationSnapshot>()
                };
                FactionMemories.Add(memory);
            }
            
            return memory;
        }

        public void AddSignificantEvent(SignificantEventType eventType, Faction involvedFaction, string description)
        {
            SignificantEvents.Add(new SignificantEventMemory
            {
                EventType = eventType,
                InvolvedFactionId = GetUniqueFactionId(involvedFaction),
                InvolvedFactionName = involvedFaction.Name,
                Description = description,
                OccurredTick = Find.TickManager.TicksGame,
                Timestamp = DateTime.Now.Ticks
            });
            
            LastUpdatedTick = Find.TickManager.TicksGame;
        }

        public void UpdateFromDialogue(List<DialogueMessageData> messages)
        {
            foreach (var message in messages)
            {
                AnalyzeDialogueMessage(message);
            }
            
            LastUpdatedTick = Find.TickManager.TicksGame;
        }

        private void AnalyzeDialogueMessage(DialogueMessageData message)
        {
            var allFactions = Find.FactionManager.AllFactions;
            foreach (var faction in allFactions)
            {
                if (message.message.Contains(faction.Name))
                {
                    var memory = GetOrCreateMemory(faction);
                    memory.LastMentionedTick = Find.TickManager.TicksGame;
                    memory.MentionCount++;
                    
                    if (IsNegativeContext(message.message, faction.Name))
                    {
                        memory.NegativeInteractions++;
                    }
                    else if (IsPositiveContext(message.message, faction.Name))
                    {
                        memory.PositiveInteractions++;
                    }
                }
            }
        }

        private bool IsNegativeContext(string message, string factionName)
        {
            var negativeWords = new[] { "enemy", "attack", "war", "hostile", "threat", "destroy", "hate", "ворог", "війна", "атакувати", "погроза" };
            foreach (var word in negativeWords)
            {
                if (message.ToLower().Contains(word.ToLower()))
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsPositiveContext(string message, string factionName)
        {
            var positiveWords = new[] { "ally", "friend", "peace", "trade", "help", "support", "дружній", "мир", "торг", "союзник", "допомогти" };
            foreach (var word in positiveWords)
            {
                if (message.ToLower().Contains(word.ToLower()))
                {
                    return true;
                }
            }
            return false;
        }

        public void UpdateRelationSnapshot(Faction targetFaction)
        {
            var memory = GetOrCreateMemory(targetFaction);
            var currentRelation = targetFaction.RelationKindWith(Faction.OfPlayer);
            
            memory.RelationHistory.Add(new RelationSnapshot
            {
                Tick = Find.TickManager.TicksGame,
                Relation = currentRelation.ToString(),
                Goodwill = targetFaction.PlayerGoodwill
            });
            
            if (memory.RelationHistory.Count > 50)
            {
                memory.RelationHistory.RemoveAt(0);
            }
            
            LastUpdatedTick = Find.TickManager.TicksGame;
        }

        private static string GetUniqueFactionId(Faction faction)
        {
            if (faction.def != null && !string.IsNullOrEmpty(faction.def.defName))
            {
                return $"{faction.def.defName}_{faction.loadID}";
            }
            return $"custom_{faction.loadID}";
        }

        public void RefreshLeaderInfo()
        {
            var faction = Find.FactionManager.AllFactions.Where(f => GetUniqueFactionId(f) == OwnerFactionId).FirstOrDefault();
            if (faction != null)
            {
                LeaderName = faction.leader?.Name?.ToStringFull ?? "Unknown";
                OwnerFactionName = faction.Name;
            }
        }

        public void UpsertRpgDepartSummary(CrossChannelSummaryRecord record, int maxEntries)
        {
            UpsertSummary(RpgDepartSummaries, record, maxEntries);
        }

        public void UpsertDiplomacySessionSummary(CrossChannelSummaryRecord record, int maxEntries)
        {
            UpsertSummary(DiplomacySessionSummaries, record, maxEntries);
        }

        private static void UpsertSummary(List<CrossChannelSummaryRecord> pool, CrossChannelSummaryRecord record, int maxEntries)
        {
            if (pool == null || record == null || string.IsNullOrWhiteSpace(record.SummaryText))
            {
                return;
            }

            int existingIndex = pool.FindIndex(x =>
                x != null &&
                !string.IsNullOrWhiteSpace(x.ContentHash) &&
                string.Equals(x.ContentHash, record.ContentHash, StringComparison.Ordinal));

            if (existingIndex >= 0)
            {
                pool[existingIndex] = record;
            }
            else
            {
                pool.Add(record);
            }

            pool.Sort((a, b) =>
            {
                if (a == null && b == null) return 0;
                if (a == null) return 1;
                if (b == null) return -1;
                return b.GameTick.CompareTo(a.GameTick);
            });

            int cap = Math.Max(1, maxEntries);
            if (pool.Count > cap)
            {
                pool.RemoveRange(cap, pool.Count - cap);
            }
        }

        // Serialization / save-load constraint — keep field identity stable. (summary summary)
        public void ExposeData()
        {
            string ownerFactionId = OwnerFactionId;
            string ownerFactionName = OwnerFactionName;
            string leaderName = LeaderName;
            int lastUpdatedTick = LastUpdatedTick;
            long createdTimestamp = CreatedTimestamp;
            long lastSavedTimestamp = LastSavedTimestamp;
            
            Scribe_Values.Look(ref ownerFactionId, "ownerFactionId", "");
            Scribe_Values.Look(ref ownerFactionName, "ownerFactionName", "");
            Scribe_Values.Look(ref leaderName, "leaderName", "");
            Scribe_Values.Look(ref lastUpdatedTick, "lastUpdatedTick", 0);
            Scribe_Values.Look(ref createdTimestamp, "createdTimestamp", 0);
            Scribe_Values.Look(ref lastSavedTimestamp, "lastSavedTimestamp", 0);
            
            OwnerFactionId = ownerFactionId;
            OwnerFactionName = ownerFactionName;
            LeaderName = leaderName;
            LastUpdatedTick = lastUpdatedTick;
            CreatedTimestamp = createdTimestamp;
            LastSavedTimestamp = lastSavedTimestamp;
            
            Scribe_Collections.Look(ref FactionMemories, "factionMemories", LookMode.Deep);
            Scribe_Collections.Look(ref SignificantEvents, "significantEvents", LookMode.Deep);
            Scribe_Collections.Look(ref DialogueHistory, "dialogueHistory", LookMode.Deep);
            Scribe_Collections.Look(ref RpgDepartSummaries, "rpgDepartSummaries", LookMode.Deep);
            Scribe_Collections.Look(ref DiplomacySessionSummaries, "diplomacySessionSummaries", LookMode.Deep);

            if (RpgDepartSummaries == null)
            {
                RpgDepartSummaries = new List<CrossChannelSummaryRecord>();
            }
            if (DiplomacySessionSummaries == null)
            {
                DiplomacySessionSummaries = new List<CrossChannelSummaryRecord>();
            }
        }
    }

    public class FactionMemoryEntry : IExposable
    {
        public string FactionId = "";
        public string FactionName = "";
        public int FirstContactTick = 0;
        public int LastMentionedTick = 0;
        public int MentionCount = 0;
        public int PositiveInteractions = 0;
        public int NegativeInteractions = 0;
        public List<RelationSnapshot> RelationHistory = new List<RelationSnapshot>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref FactionId, "factionId", "");
            Scribe_Values.Look(ref FactionName, "factionName", "");
            Scribe_Values.Look(ref FirstContactTick, "firstContactTick", 0);
            Scribe_Values.Look(ref LastMentionedTick, "lastMentionedTick", 0);
            Scribe_Values.Look(ref MentionCount, "mentionCount", 0);
            Scribe_Values.Look(ref PositiveInteractions, "positiveInteractions", 0);
            Scribe_Values.Look(ref NegativeInteractions, "negativeInteractions", 0);
            Scribe_Collections.Look(ref RelationHistory, "relationHistory", LookMode.Deep);
            if (RelationHistory == null)
            {
                RelationHistory = new List<RelationSnapshot>();
            }
        }
    }

    public class RelationSnapshot : IExposable
    {
        public int Tick = 0;
        public string Relation = "";
        public int Goodwill = 0;

        public void ExposeData()
        {
            Scribe_Values.Look(ref Tick, "tick", 0);
            Scribe_Values.Look(ref Relation, "relation", "");
            Scribe_Values.Look(ref Goodwill, "goodwill", 0);
        }
    }

    public class SignificantEventMemory : IExposable
    {
        public SignificantEventType EventType = SignificantEventType.GoodwillChanged;
        public string InvolvedFactionId = "";
        public string InvolvedFactionName = "";
        public string Description = "";
        public int OccurredTick = 0;
        public long Timestamp = 0L;

        public void ExposeData()
        {
            Scribe_Values.Look(ref EventType, "eventType", SignificantEventType.GoodwillChanged);
            Scribe_Values.Look(ref InvolvedFactionId, "involvedFactionId", "");
            Scribe_Values.Look(ref InvolvedFactionName, "involvedFactionName", "");
            Scribe_Values.Look(ref Description, "description", "");
            Scribe_Values.Look(ref OccurredTick, "occurredTick", 0);
            Scribe_Values.Look(ref Timestamp, "timestamp", 0L);
        }
    }

    public enum SignificantEventType
    {
        WarDeclared,     
        PeaceMade,       
        TradeCaravan,    
        GiftSent,        
        AidRequested,    
        QuestIssued,     
        GoodwillChanged, 
        AllianceFormed,  
        Betrayal         
    }

    /// <summary>/// dialoguerecord
 ///</summary>
    public class DialogueRecord : IExposable
    {
        public bool IsPlayer = false;
        public string Message = "";
        public int GameTick = 0;

        public void ExposeData()
        {
            Scribe_Values.Look(ref IsPlayer, "isPlayer", false);
            Scribe_Values.Look(ref Message, "message", "");
            Scribe_Values.Look(ref GameTick, "gameTick", 0);
        }
    }
}
