using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Relation;
using Ustas.RimAI.Communication.Relations.Guards;
using Ustas.RimAI.Communication.Relations.WorldState;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.Prompting.Builders;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Prompting.Transfer;
using Ustas.RimAI.Communication.Relations.Serialization;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting.Diplomacy;

namespace Ustas.RimAI.Communication.Relations.Prompting.Diplomacy
{
    internal sealed class PromptSnapshotService
    {
        private readonly PromptPersistenceService host;

        [ThreadStatic] private static Stack<DiplomacyPromptRuntimeSnapshot> _runtimeSnapshotScope;

        internal PromptSnapshotService(PromptPersistenceService host)
        {
            this.host = host ?? throw new System.ArgumentNullException(nameof(host));
        }
        private sealed class RuntimeSnapshotScope : IDisposable
        {
            private bool disposed;

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                if (_runtimeSnapshotScope == null || _runtimeSnapshotScope.Count == 0)
                {
                    return;
                }

                _runtimeSnapshotScope.Pop();
            }
        }

        internal IDisposable PushRuntimeSnapshotScope(DiplomacyPromptRuntimeSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            _runtimeSnapshotScope ??= new Stack<DiplomacyPromptRuntimeSnapshot>();
            _runtimeSnapshotScope.Push(snapshot);
            return new RuntimeSnapshotScope();
        }

        internal bool TryGetScopedRuntimeSnapshotForFaction(Faction faction, out DiplomacyPromptRuntimeSnapshot snapshot)
        {
            snapshot = null;
            if (faction == null || _runtimeSnapshotScope == null || _runtimeSnapshotScope.Count == 0)
            {
                return false;
            }

            DiplomacyPromptRuntimeSnapshot scoped = _runtimeSnapshotScope.Peek();
            if (scoped == null)
            {
                return false;
            }

            string factionId = faction.GetUniqueLoadID() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(factionId))
            {
                return false;
            }

            if (!string.Equals(scoped.FactionLoadId, factionId, StringComparison.Ordinal))
            {
                return false;
            }

            snapshot = scoped;
            return true;
        }

        internal DiplomacyPromptRuntimeSnapshot BuildRuntimeSnapshotForFaction(
            Faction faction,
            Pawn preferredNegotiator,
            int builtTick,
            int memoryRevision,
            int worldEventRevision,
            long promptFilesStampUtcTicks,
            int settingsSignature)
        {
            if (faction == null)
            {
                return null;
            }

            SystemPromptConfig config = host.DomainStore.LoadConfigReadOnly() ?? host.DomainStore.CreateDefaultConfig();
            GameAIInterface.Instance.RefreshQuestTrackingState();
            DialogueScenarioContext context = DialogueScenarioContext.CreateDiplomacy(
                faction,
                false,
                new[] { "scene:social" });
            string environmentBlock = host.BuildEnvironmentPromptBlocks(config, context);
            string memoryDataBlock = host.NodeSupport.BuildTextBlock(sb => host.ContextAssembler.AppendMemoryData(sb, faction));
            string factionInfoBlock = host.NodeSupport.BuildTextBlock(sb => host.ContextAssembler.AppendFactionInfo(sb, faction));
            string playerPawnProfileBlock = host.ContextAssembler.BuildPlayerPawnContextForPrompt(faction, preferredNegotiator);
            string playerRoyaltySummaryBlock = host.ContextAssembler.BuildPlayerRoyaltySummaryForPrompt(faction, preferredNegotiator);
            string factionSettlementSummaryBlock = host.ContextAssembler.BuildFactionSettlementSummaryForPrompt(faction);
            string factionQuestStatusBlock = host.ContextAssembler.BuildFactionQuestStatusBlockForPrompt(faction);

            return new DiplomacyPromptRuntimeSnapshot(
                faction.GetUniqueLoadID(),
                environmentBlock,
                memoryDataBlock,
                factionInfoBlock,
                playerPawnProfileBlock,
                playerRoyaltySummaryBlock,
                factionSettlementSummaryBlock,
                factionQuestStatusBlock,
                builtTick,
                memoryRevision,
                worldEventRevision,
                faction.PlayerGoodwill,
                faction.RelationKindWith(Faction.OfPlayer),
                promptFilesStampUtcTicks,
                settingsSignature,
                GameAIInterface.Instance.QuestTrackingRevision);
        }
    }
}
