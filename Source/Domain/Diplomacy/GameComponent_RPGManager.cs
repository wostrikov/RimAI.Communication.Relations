using RimWorld;
using Verse;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.Diagnostics;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.WorldState;
using System.Reflection;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.Config;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
    public class GameComponent_RPGManager : GameComponent
    {
        internal GameComponent_RPGManagerParts Parts;

        public static GameComponent_RPGManager Instance;

        internal Dictionary<string, int> pawnDialogueCooldownUntilTickById = new Dictionary<string, int>();
        internal List<string> cooldownKeysByIdWorkingList;
        internal List<int> cooldownValuesByIdWorkingList;

        internal Dictionary<string, string> pawnPersonaPromptsById = new Dictionary<string, string>();
        internal List<string> pawnPersonaPromptKeysByIdWorkingList;
        internal List<string> pawnPersonaPromptValuesByIdWorkingList;

        // Legacy fields are loaded once for migration only (read-only on load).
        // These use LookMode.Reference to consume legacy Pawn-keyed XML nodes from old saves.
        // Pawn keys that resolve to null (destroyed/recycled) are safely skipped in MigrateLegacyPawnDictionaries.
        internal Dictionary<Pawn, int> legacyPawnDialogueCooldownUntilTick;
        internal List<Pawn> legacyCooldownKeysWorkingList;
        internal List<int> legacyCooldownValuesWorkingList;
        internal Dictionary<Pawn, string> legacyPawnPersonaPrompts;
        internal List<Pawn> legacyPawnPersonaPromptKeysWorkingList;
        internal List<string> legacyPawnPersonaPromptValuesWorkingList;
        internal readonly HashSet<int> pawnPersonaSyncGuards = new HashSet<int>();
        internal string persistentRpgSaveSlotId = string.Empty;

        internal const float DefaultExitCooldownHours = 2f;
        internal const string PersistentRpgSaveSlotPrefix = "slot";

        public GameComponent_RPGManager(Game game)
        {
            Parts = new GameComponent_RPGManagerParts(this);
            Instance = this;
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            Instance = this;
            SaveContextTracker.Reset();
            RelationsTrackedEntityRegistry.Reset();
            ResetPersistentRpgSaveSlotIdForNewGame();
            RpgNpcDialogueArchiveManager.Instance.OnNewGame();
            MarkNpcPersonaBootstrapAsNewGame();
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            Instance = this;
            RpgNpcDialogueArchiveManager.Instance.OnLoadedGame();
            ScheduleNpcPersonaBootstrapOnLoad();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            Instance = this;

            // Check if AI Quest Def is loaded
            var questDef = DefDatabase<QuestScriptDef>.GetNamedSilentFail("RimChat_AIQuest");
            if (questDef == null)
            {
                Log.Warning("[RimAI.Relations] Failed to find QuestScriptDef 'RimChat_AIQuest'. AI Quests will not be available.");
            }
            else
            {
                Log.Message("[RimAI.Relations] QuestScriptDef 'RimChat_AIQuest' loaded successfully.");
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                EnsurePersistentRpgSaveSlotId();
                RpgNpcDialogueArchiveManager.Instance.OnBeforeGameSave();
            }

            Scribe_Values.Look(ref persistentRpgSaveSlotId, "persistentRpgSaveSlotId", string.Empty);

            Scribe_Collections.Look(
                ref pawnDialogueCooldownUntilTickById,
                "pawnDialogueCooldownUntilTickById",
                LookMode.Value,
                LookMode.Value,
                ref cooldownKeysByIdWorkingList,
                ref cooldownValuesByIdWorkingList);

            Scribe_Collections.Look(
                ref pawnPersonaPromptsById,
                "pawnPersonaPromptsById",
                LookMode.Value,
                LookMode.Value,
                ref pawnPersonaPromptKeysByIdWorkingList,
                ref pawnPersonaPromptValuesByIdWorkingList);

            if (Scribe.mode != LoadSaveMode.Saving)
            {
                // Consume legacy Pawn-keyed dictionaries from old saves.
                // LookMode.Reference is required to match the original save format.
                // Pawns that no longer exist resolve to null and are skipped in migration.
                try
                {
                    Scribe_Collections.Look(
                        ref legacyPawnDialogueCooldownUntilTick,
                        "pawnDialogueCooldownUntilTick",
                        LookMode.Reference,
                        LookMode.Value,
                        ref legacyCooldownKeysWorkingList,
                        ref legacyCooldownValuesWorkingList);

                    Scribe_Collections.Look(
                        ref legacyPawnPersonaPrompts,
                        "pawnPersonaPrompts",
                        LookMode.Reference,
                        LookMode.Value,
                        ref legacyPawnPersonaPromptKeysWorkingList,
                        ref legacyPawnPersonaPromptValuesWorkingList);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimAI.Relations] Failed to load legacy pawn data, clearing for compatibility: {ex.Message}");
                    legacyPawnDialogueCooldownUntilTick = null;
                    legacyPawnPersonaPrompts = null;
                }
            }

            ExposeData_NpcPersonaBootstrap();

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                EnsurePersistentRpgSaveSlotId();
                if (pawnDialogueCooldownUntilTickById == null)
                {
                    pawnDialogueCooldownUntilTickById = new Dictionary<string, int>();
                }

                if (pawnPersonaPromptsById == null)
                {
                    pawnPersonaPromptsById = new Dictionary<string, string>();
                }

                MigrateLegacyPawnDictionaries();
                int currentTick = Find.TickManager?.TicksGame ?? 0;
                CleanupInvalidRpgDictionaries(currentTick);

                cooldownKeysByIdWorkingList = null;
                cooldownValuesByIdWorkingList = null;
                pawnPersonaPromptKeysByIdWorkingList = null;
                pawnPersonaPromptValuesByIdWorkingList = null;
                legacyCooldownKeysWorkingList = null;
                legacyCooldownValuesWorkingList = null;
                legacyPawnPersonaPromptKeysWorkingList = null;
                legacyPawnPersonaPromptValuesWorkingList = null;

                RpgNpcDialogueArchiveManager.Instance.OnAfterGameLoad();
                OnPostLoadInit_NpcPersonaBootstrap();
            }
        }

        

        

        

        public int GetRpgDialogueExitCooldownTicks()
        {
            return Mathf.RoundToInt(DefaultExitCooldownHours * 2500f);
        }

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        #region Facade forwards
        public override void GameComponentTick() => Parts.PersonaBootstrap.GameComponentTick();
        internal void ExposeData_NpcPersonaBootstrap() => Parts.PersonaBootstrap.ExposeData_NpcPersonaBootstrap();
        internal void MarkNpcPersonaBootstrapAsNewGame() => Parts.PersonaBootstrap.MarkNpcPersonaBootstrapAsNewGame();
        internal void ScheduleNpcPersonaBootstrapOnLoad() => Parts.PersonaBootstrap.ScheduleNpcPersonaBootstrapOnLoad();
        internal void OnPostLoadInit_NpcPersonaBootstrap() => Parts.PersonaBootstrap.OnPostLoadInit_NpcPersonaBootstrap();
        internal void ProcessNpcPersonaBootstrapTick() => Parts.PersonaBootstrap.ProcessNpcPersonaBootstrapTick();
        internal void ProcessNpcPersonaRuntimeTick() => Parts.PersonaBootstrap.ProcessNpcPersonaRuntimeTick();
        internal void ProcessPersonaScanOneFrame(int currentTick) => Parts.PersonaBootstrap.ProcessPersonaScanOneFrame(currentTick);
        internal void FinishPersonaScan(int currentTick) => Parts.PersonaBootstrap.FinishPersonaScan(currentTick);
        internal void InitializeNpcPersonaBootstrapQueue() => Parts.PersonaBootstrap.InitializeNpcPersonaBootstrapQueue();
        internal List<Pawn> CollectNpcPersonaBootstrapTargets() => Parts.PersonaBootstrap.CollectNpcPersonaBootstrapTargets();
        internal static void AppendUniqueNpcTarget(List<Pawn> target, HashSet<int> ids, Pawn pawn) => RPGManagerPersonaBootstrap.AppendUniqueNpcTarget(target, ids, pawn);
        internal static bool IsEligibleNpcPersonaTarget(Pawn pawn) => RPGManagerPersonaBootstrap.IsEligibleNpcPersonaTarget(pawn);
        internal bool HasPersonaPrompt(Pawn pawn) => Parts.PersonaBootstrap.HasPersonaPrompt(pawn);
        internal bool TryGetNextBootstrapPawn(out Pawn pawn) => Parts.PersonaBootstrap.TryGetNextBootstrapPawn(out pawn);
        internal bool TryApplyRimTalkPersonaFromRuntimeScan() => Parts.PersonaBootstrap.TryApplyRimTalkPersonaFromRuntimeScan();
        internal bool TryFindMissingPersonaPawn(out Pawn pawn) => Parts.PersonaBootstrap.TryFindMissingPersonaPawn(out pawn);
        internal bool IsPawnPersonaGenerationPending(Pawn pawn) => Parts.PersonaBootstrap.IsPawnPersonaGenerationPending(pawn);
        internal static bool CanStartPersonaGeneration() => RPGManagerPersonaBootstrap.CanStartPersonaGeneration();
        internal static bool ShouldBlockAiPersonaGeneration() => RPGManagerPersonaBootstrap.ShouldBlockAiPersonaGeneration();
        internal static bool IsRimTalkLoadedForPersonaBlock() => RPGManagerPersonaBootstrap.IsRimTalkLoadedForPersonaBlock();
        internal void StartNpcPersonaGeneration(Pawn pawn, int attempt) => Parts.PersonaBootstrap.StartNpcPersonaGeneration(pawn, attempt);
        internal bool TryApplyRimTalkPersonaFromBootstrapQueue() => Parts.PersonaBootstrap.TryApplyRimTalkPersonaFromBootstrapQueue();
        internal bool TryCopyPawnPersonaFromRimTalk(Pawn pawn) => Parts.PersonaBootstrap.TryCopyPawnPersonaFromRimTalk(pawn);
        internal bool TryCopyPawnPersonaFromRimTalk(Pawn pawn, string template) => Parts.PersonaBootstrap.TryCopyPawnPersonaFromRimTalk(pawn, template);
        internal bool TrySyncPawnPersonaFromRimTalk(Pawn pawn) => Parts.PersonaBootstrap.TrySyncPawnPersonaFromRimTalk(pawn);
        internal bool TrySyncPawnPersonaFromRimTalk(Pawn pawn, string template) => Parts.PersonaBootstrap.TrySyncPawnPersonaFromRimTalk(pawn, template);
        public bool TrySyncAllColonyPawnPersonasFromRimTalk(out int updated, out int cleared, out int unchanged, out int skipped) => Parts.PersonaBootstrap.TrySyncAllColonyPawnPersonasFromRimTalk(out updated, out cleared, out unchanged, out skipped);
        internal static string ResolveRimTalkPersonaCopyTemplateOrDefaultCached() => RPGManagerPersonaBootstrap.ResolveRimTalkPersonaCopyTemplateOrDefaultCached();
        internal static void TryEnsureRpgPersonaTokenCoverageSafe() => RPGManagerPersonaBootstrap.TryEnsureRpgPersonaTokenCoverageSafe();
        internal static bool IsEligibleRimTalkPersonaCopyTarget(Pawn pawn) => RPGManagerPersonaBootstrap.IsEligibleRimTalkPersonaCopyTarget(pawn);
        internal static bool CanCopyPawnPersonaFromRimTalk(Pawn pawn) => RPGManagerPersonaBootstrap.CanCopyPawnPersonaFromRimTalk(pawn);
        internal static bool TryGetRimTalkSourcePersona(Pawn pawn, out string sourcePersona) => RPGManagerPersonaBootstrap.TryGetRimTalkSourcePersona(pawn, out sourcePersona);
        internal static MethodInfo ResolveRimTalkGetPersonalityMethod() => RPGManagerPersonaBootstrap.ResolveRimTalkGetPersonalityMethod();
        internal static string NormalizeCopiedPersonaPrompt(string raw) => RPGManagerPersonaBootstrap.NormalizeCopiedPersonaPrompt(raw);
        internal string RenderPersonaCopyTemplateOrThrow(Pawn pawn, string template, string sourcePersona) => Parts.PersonaBootstrap.RenderPersonaCopyTemplateOrThrow(pawn, template, sourcePersona);
        internal static PromptRenderException BuildPersonaCopyRenderException(string templateId, string channel, string message) => RPGManagerPersonaBootstrap.BuildPersonaCopyRenderException(templateId, channel, message);
        internal List<ChatMessageData> BuildNpcPersonaGenerationMessages(Pawn pawn) => Parts.PersonaBootstrap.BuildNpcPersonaGenerationMessages(pawn);
        internal static string BuildPersonaTemplateLine(RPGManagerPersonaBootstrap.PersonaPronouns pronouns) => RPGManagerPersonaBootstrap.BuildPersonaTemplateLine(pronouns);
        internal void OnNpcPersonaGenerationSuccess(string requestId, string response) => Parts.PersonaBootstrap.OnNpcPersonaGenerationSuccess(requestId, response);
        internal void OnNpcPersonaGenerationError(string requestId, string error) => Parts.PersonaBootstrap.OnNpcPersonaGenerationError(requestId, error);
        internal void RetryOrFallbackPersonaPrompt(RPGManagerPersonaBootstrap.PendingPersonaGenerationContext pending) => Parts.PersonaBootstrap.RetryOrFallbackPersonaPrompt(pending);
        internal static bool TryNormalizePersonaPrompt(string raw, out string normalized) => RPGManagerPersonaBootstrap.TryNormalizePersonaPrompt(raw, out normalized);
        internal static bool IsPersonaTemplateFormat(string text) => RPGManagerPersonaBootstrap.IsPersonaTemplateFormat(text);
        internal static bool HasOrderedAnchors(string text, params string[] anchors) => RPGManagerPersonaBootstrap.HasOrderedAnchors(text, anchors);
        internal static string CollapseWhitespace(string text) => RPGManagerPersonaBootstrap.CollapseWhitespace(text);
        internal string BuildFallbackPersonaPrompt(Pawn pawn) => Parts.PersonaBootstrap.BuildFallbackPersonaPrompt(pawn);
        internal static RPGManagerPersonaBootstrap.PersonaPronouns ResolvePersonaPronouns(Pawn pawn) => RPGManagerPersonaBootstrap.ResolvePersonaPronouns(pawn);
        internal static string BuildPersonaBootstrapPrompt(RpgPromptDefaultsConfig defaults, RPGManagerPersonaBootstrap.PersonaPronouns pronouns, string profile) => RPGManagerPersonaBootstrap.BuildPersonaBootstrapPrompt(defaults, pronouns, profile);
        internal static string RenderPersonaBootstrapTemplate(string template, RPGManagerPersonaBootstrap.PersonaPronouns pronouns) => RPGManagerPersonaBootstrap.RenderPersonaBootstrapTemplate(template, pronouns);
        internal static PromptRenderContext BuildPersonaBootstrapRenderContext(string templateId, RPGManagerPersonaBootstrap.PersonaPronouns pronouns) => RPGManagerPersonaBootstrap.BuildPersonaBootstrapRenderContext(templateId, pronouns);
        internal static string BuildCoreTemperament(Pawn pawn) => RPGManagerPersonaBootstrap.BuildCoreTemperament(pawn);
        internal static string BuildEmotionalPattern(Pawn pawn) => RPGManagerPersonaBootstrap.BuildEmotionalPattern(pawn);
        internal static string BuildBehavioralStrategy(Pawn pawn) => RPGManagerPersonaBootstrap.BuildBehavioralStrategy(pawn);
        internal static string BuildCoreMotivation(Pawn pawn) => RPGManagerPersonaBootstrap.BuildCoreMotivation(pawn);
        internal static string BuildDefenseWeakness(Pawn pawn) => RPGManagerPersonaBootstrap.BuildDefenseWeakness(pawn);
        internal static string BuildPersonalityCost(Pawn pawn) => RPGManagerPersonaBootstrap.BuildPersonalityCost(pawn);
        internal static bool StartsWithVowelSound(string text) => RPGManagerPersonaBootstrap.StartsWithVowelSound(text);
        internal void CompleteNpcPersonaBootstrap() => Parts.PersonaBootstrap.CompleteNpcPersonaBootstrap();
        internal bool ShouldRunNpcPersonaBootstrap() => Parts.PersonaBootstrap.ShouldRunNpcPersonaBootstrap();
        internal void ResetNpcPersonaBootstrapRuntimeState() => Parts.PersonaBootstrap.ResetNpcPersonaBootstrapRuntimeState();
        #endregion
    
        #region Cluster forwards
        public string GetPersistentRpgSaveSlotId() => Parts.Slice1.GetPersistentRpgSaveSlotId();
        internal void ResetPersistentRpgSaveSlotIdForNewGame() => Parts.Slice1.ResetPersistentRpgSaveSlotIdForNewGame();
        internal void EnsurePersistentRpgSaveSlotId() => Parts.Slice1.EnsurePersistentRpgSaveSlotId();
        public void StartRpgDialogueCooldown(Pawn pawn, int cooldownTicks) => Parts.Slice1.StartRpgDialogueCooldown(pawn, cooldownTicks);
        public bool IsRpgDialogueOnCooldown(Pawn pawn, out int remainingTicks) => Parts.Slice1.IsRpgDialogueOnCooldown(pawn, out remainingTicks);
        public int GetDialogueCooldownUntilTick(Pawn pawn) => Parts.Slice1.GetDialogueCooldownUntilTick(pawn);
        public void SetDialogueCooldownUntilTick(Pawn pawn, int untilTick) => Parts.Slice1.SetDialogueCooldownUntilTick(pawn, untilTick);
        public string GetPawnPersonaPrompt(Pawn pawn) => Parts.Slice1.GetPawnPersonaPrompt(pawn);
        public string ResolveEffectivePawnPersonalityPrompt(Pawn pawn, bool allowGenerateFallback = true) => Parts.Slice1.ResolveEffectivePawnPersonalityPrompt(pawn, allowGenerateFallback);
        internal void TrySyncPawnPersonaFromRimTalkSafely(Pawn pawn) => Parts.Slice1.TrySyncPawnPersonaFromRimTalkSafely(pawn);
        internal bool IsPawnPersonaSyncInProgress(Pawn pawn) => Parts.Slice1.IsPawnPersonaSyncInProgress(pawn);
        internal bool TryBeginPawnPersonaSync(Pawn pawn) => Parts.Slice1.TryBeginPawnPersonaSync(pawn);
        internal void EndPawnPersonaSync(Pawn pawn) => Parts.Slice1.EndPawnPersonaSync(pawn);
        internal string BuildAndPersistFallbackPawnPersonaPrompt(Pawn pawn) => Parts.Slice1.BuildAndPersistFallbackPawnPersonaPrompt(pawn);
        public void SetPawnPersonaPrompt(Pawn pawn, string prompt) => Parts.Slice1.SetPawnPersonaPrompt(pawn, prompt);
        internal void MigrateLegacyPawnDictionaries() => Parts.Slice1.MigrateLegacyPawnDictionaries();
        internal void CleanupInvalidRpgDictionaries(int currentTick) => Parts.Slice1.CleanupInvalidRpgDictionaries(currentTick);
        internal static bool TryResolvePawnByStableId(string pawnId, out Pawn pawn) => RPGManagerSlice1.TryResolvePawnByStableId(pawnId, out pawn);
        internal static string GetPawnStableId(Pawn pawn) => RPGManagerSlice1.GetPawnStableId(pawn);
        #endregion
}
    internal sealed class RPGManagerSlice1 : GameComponent_RPGManagerCollaborator
    {
        internal RPGManagerSlice1(GameComponent_RPGManager owner) : base(owner)
        {
        }

public string GetPersistentRpgSaveSlotId()
        {
            Owner.EnsurePersistentRpgSaveSlotId();
            return persistentRpgSaveSlotId;
        }

internal void ResetPersistentRpgSaveSlotIdForNewGame()
        {
            persistentRpgSaveSlotId = string.Empty;
            Owner.EnsurePersistentRpgSaveSlotId();
        }

internal void EnsurePersistentRpgSaveSlotId()
        {
            if (!string.IsNullOrWhiteSpace(persistentRpgSaveSlotId))
            {
                return;
            }

            persistentRpgSaveSlotId = $"{PersistentRpgSaveSlotPrefix}_{Guid.NewGuid():N}";
        }

public void StartRpgDialogueCooldown(Pawn pawn, int cooldownTicks)
        {
            if (pawn == null || cooldownTicks <= 0)
            {
                return;
            }

            string pawnId = GameComponent_RPGManager.GetPawnStableId(pawn);
            if (string.IsNullOrWhiteSpace(pawnId))
            {
                return;
            }

            int currentTick = Find.TickManager?.TicksGame ?? 0;
            int untilTick = currentTick + cooldownTicks;
            if (pawnDialogueCooldownUntilTickById.TryGetValue(pawnId, out int existing))
            {
                pawnDialogueCooldownUntilTickById[pawnId] = Mathf.Max(existing, untilTick);
                return;
            }

            pawnDialogueCooldownUntilTickById[pawnId] = untilTick;
        }

public bool IsRpgDialogueOnCooldown(Pawn pawn, out int remainingTicks)
        {
            remainingTicks = 0;
            if (pawn == null || pawnDialogueCooldownUntilTickById == null)
            {
                return false;
            }

            string pawnId = GameComponent_RPGManager.GetPawnStableId(pawn);
            if (string.IsNullOrWhiteSpace(pawnId) ||
                !pawnDialogueCooldownUntilTickById.TryGetValue(pawnId, out int untilTick))
            {
                return false;
            }

            int currentTick = Find.TickManager?.TicksGame ?? 0;
            remainingTicks = untilTick - currentTick;
            if (remainingTicks > 0)
            {
                return true;
            }

            pawnDialogueCooldownUntilTickById.Remove(pawnId);
            remainingTicks = 0;
            return false;
        }

public int GetDialogueCooldownUntilTick(Pawn pawn)
        {
            if (pawn == null || pawnDialogueCooldownUntilTickById == null)
            {
                return 0;
            }

            string pawnId = GameComponent_RPGManager.GetPawnStableId(pawn);
            if (string.IsNullOrWhiteSpace(pawnId))
            {
                return 0;
            }

            return pawnDialogueCooldownUntilTickById.TryGetValue(pawnId, out int untilTick) ? untilTick : 0;
        }

public void SetDialogueCooldownUntilTick(Pawn pawn, int untilTick)
        {
            if (pawn == null)
            {
                return;
            }

            string pawnId = GameComponent_RPGManager.GetPawnStableId(pawn);
            if (string.IsNullOrWhiteSpace(pawnId))
            {
                return;
            }

            if (pawnDialogueCooldownUntilTickById == null)
            {
                pawnDialogueCooldownUntilTickById = new Dictionary<string, int>();
            }

            int currentTick = Find.TickManager?.TicksGame ?? 0;
            if (untilTick <= currentTick)
            {
                pawnDialogueCooldownUntilTickById.Remove(pawnId);
                return;
            }

            pawnDialogueCooldownUntilTickById[pawnId] = untilTick;
        }

public string GetPawnPersonaPrompt(Pawn pawn)
        {
            if (pawn == null || pawnPersonaPromptsById == null)
            {
                return string.Empty;
            }

            string pawnId = GameComponent_RPGManager.GetPawnStableId(pawn);
            if (string.IsNullOrWhiteSpace(pawnId))
            {
                return string.Empty;
            }

            bool found = pawnPersonaPromptsById.TryGetValue(pawnId, out string prompt);
            return found ? prompt ?? string.Empty : string.Empty;
        }

public string ResolveEffectivePawnPersonalityPrompt(Pawn pawn, bool allowGenerateFallback = true)
        {
            if (pawn == null)
            {
                return string.Empty;
            }

            if (Owner.IsPawnPersonaSyncInProgress(pawn))
            {
                return Owner.GetPawnPersonaPrompt(pawn)?.Trim() ?? string.Empty;
            }

            Owner.TrySyncPawnPersonaFromRimTalkSafely(pawn);

            string existing = Owner.GetPawnPersonaPrompt(pawn)?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }

            return allowGenerateFallback ? Owner.BuildAndPersistFallbackPawnPersonaPrompt(pawn) : string.Empty;
        }

internal void TrySyncPawnPersonaFromRimTalkSafely(Pawn pawn)
        {
            if (pawn == null ||
                pawn.Faction != Faction.OfPlayer ||
                pawn.Dead ||
                pawn.Destroyed)
            {
                return;
            }

            if (!PawnDialogueRoutingPolicy.IsPersonaSyncEligible(pawn))
            {
                DebugLogger.Debug(
                    $"Skip RimTalk persona sync: pawn '{pawn.LabelShortCap}' lacks persona sync capability.");
                return;
            }

            if (!Owner.TryBeginPawnPersonaSync(pawn))
            {
                return;
            }

            try
            {
                if (GameComponent_RPGManager.CanCopyPawnPersonaFromRimTalk(pawn))
                {
                    Owner.TrySyncPawnPersonaFromRimTalk(pawn);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Relations] Failed to resolve RimTalk personality for '{pawn.LabelShortCap}': {ex.Message}");
            }
            finally
            {
                Owner.EndPawnPersonaSync(pawn);
            }
        }

internal bool IsPawnPersonaSyncInProgress(Pawn pawn)
        {
            return pawn != null &&
                pawn.thingIDNumber > 0 &&
                pawnPersonaSyncGuards.Contains(pawn.thingIDNumber);
        }

internal bool TryBeginPawnPersonaSync(Pawn pawn)
        {
            if (pawn == null || pawn.thingIDNumber <= 0)
            {
                return false;
            }

            return pawnPersonaSyncGuards.Add(pawn.thingIDNumber);
        }

internal void EndPawnPersonaSync(Pawn pawn)
        {
            if (pawn == null || pawn.thingIDNumber <= 0)
            {
                return;
            }

            pawnPersonaSyncGuards.Remove(pawn.thingIDNumber);
        }

internal string BuildAndPersistFallbackPawnPersonaPrompt(Pawn pawn)
        {
            if (!GameComponent_RPGManager.IsEligibleNpcPersonaTarget(pawn))
            {
                return string.Empty;
            }

            string generated = Owner.BuildFallbackPersonaPrompt(pawn)?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(generated))
            {
                return string.Empty;
            }

            Owner.SetPawnPersonaPrompt(pawn, generated);
            return generated;
        }

public void SetPawnPersonaPrompt(Pawn pawn, string prompt)
        {
            if (pawn == null)
            {
                return;
            }

            string pawnId = GameComponent_RPGManager.GetPawnStableId(pawn);
            if (string.IsNullOrWhiteSpace(pawnId))
            {
                return;
            }

            if (pawnPersonaPromptsById == null)
            {
                pawnPersonaPromptsById = new Dictionary<string, string>();
            }

            string normalized = prompt?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(normalized))
            {
                pawnPersonaPromptsById.Remove(pawnId);
                return;
            }

            pawnPersonaPromptsById[pawnId] = normalized;
        }

internal void MigrateLegacyPawnDictionaries()
        {
            if (legacyPawnDialogueCooldownUntilTick != null)
            {
                foreach (KeyValuePair<Pawn, int> entry in legacyPawnDialogueCooldownUntilTick)
                {
                    string pawnId = GameComponent_RPGManager.GetPawnStableId(entry.Key);
                    if (string.IsNullOrWhiteSpace(pawnId))
                    {
                        continue;
                    }

                    if (pawnDialogueCooldownUntilTickById.TryGetValue(pawnId, out int existing))
                    {
                        pawnDialogueCooldownUntilTickById[pawnId] = Mathf.Max(existing, entry.Value);
                    }
                    else
                    {
                        pawnDialogueCooldownUntilTickById[pawnId] = entry.Value;
                    }
                }
            }

            if (legacyPawnPersonaPrompts != null)
            {
                foreach (KeyValuePair<Pawn, string> entry in legacyPawnPersonaPrompts)
                {
                    string pawnId = GameComponent_RPGManager.GetPawnStableId(entry.Key);
                    if (string.IsNullOrWhiteSpace(pawnId))
                    {
                        continue;
                    }

                    string normalized = entry.Value?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(normalized))
                    {
                        continue;
                    }

                    pawnPersonaPromptsById[pawnId] = normalized;
                }
            }

            legacyPawnDialogueCooldownUntilTick = null;
            legacyPawnPersonaPrompts = null;
        }

internal void CleanupInvalidRpgDictionaries(int currentTick)
        {
            if (pawnDialogueCooldownUntilTickById == null)
            {
                pawnDialogueCooldownUntilTickById = new Dictionary<string, int>();
            }
            else
            {
                List<string> invalidCooldownIds = pawnDialogueCooldownUntilTickById
                    .Where(entry => entry.Value <= currentTick || !GameComponent_RPGManager.TryResolvePawnByStableId(entry.Key, out _))
                    .Select(entry => entry.Key)
                    .ToList();
                foreach (string id in invalidCooldownIds)
                {
                    pawnDialogueCooldownUntilTickById.Remove(id);
                }
            }

            if (pawnPersonaPromptsById == null)
            {
                pawnPersonaPromptsById = new Dictionary<string, string>();
            }
            else
            {
                List<string> invalidPersonaIds = pawnPersonaPromptsById
                    .Where(entry =>
                    {
                        if (string.IsNullOrWhiteSpace(entry.Value))
                        {
                            return true;
                        }

                        if (!GameComponent_RPGManager.TryResolvePawnByStableId(entry.Key, out Pawn pawn))
                        {
                            return true;
                        }

                        return !PawnDialogueRoutingPolicy.IsRpgDialogueEligibleRace(pawn);
                    })
                    .Select(entry => entry.Key)
                    .ToList();
                foreach (string id in invalidPersonaIds)
                {
                    pawnPersonaPromptsById.Remove(id);
                }
            }
        }

internal static bool TryResolvePawnByStableId(string pawnId, out Pawn pawn)
        {
            if (string.IsNullOrWhiteSpace(pawnId))
            {
                pawn = null;
                return false;
            }

            return DialogueContextResolver.TryResolvePawn(pawnId, out pawn);
        }

internal static string GetPawnStableId(Pawn pawn)
        {
            if (pawn == null || pawn.Destroyed || pawn.Dead)
            {
                return string.Empty;
            }

            return pawn.GetUniqueLoadID() ?? string.Empty;
        }
    }

    internal sealed class GameComponent_RPGManagerParts
    {
        internal readonly GameComponent_RPGManager Owner;
        internal readonly RPGManagerPersonaBootstrap PersonaBootstrap;
        internal readonly RPGManagerSlice1 Slice1;
        internal GameComponent_RPGManagerParts(GameComponent_RPGManager owner)
        {
            Owner = owner;
            PersonaBootstrap = new RPGManagerPersonaBootstrap(owner);
            Slice1 = new RPGManagerSlice1(owner);
        }
    }


}
