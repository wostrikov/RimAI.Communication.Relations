using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.Diagnostics;
using RimWorld;
using Verse;
using Ustas.RimAI.Communication.Relations.Context;
using PersonaPronouns = Ustas.RimAI.Communication.Relations.DiplomacySystem.RPGManagerPersonaBootstrap.PersonaPronouns;
using PendingPersonaGenerationContext = Ustas.RimAI.Communication.Relations.DiplomacySystem.RPGManagerPersonaBootstrap.PendingPersonaGenerationContext;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
    /// <summary>
    /// Dependencies: PromptPersistenceService, PromptTemplateRenderer, ModDependencyProbe, RimTalk reflection bridge, and pawn persona storage in this component.
    /// Responsibility: bootstrap/runtime RimTalk persona copy-sync flow for dialogue-eligible colony pawns with capability gating, without external persona-bootstrap requests.
    /// </summary>
        internal sealed class RPGManagerPersonaBootstrap : GameComponent_RPGManagerCollaborator
    {
        internal RPGManagerPersonaBootstrapParts Parts;

        internal RPGManagerPersonaBootstrap(GameComponent_RPGManager owner) : base(owner)
        {
            Parts = new RPGManagerPersonaBootstrapParts(this);
        }


        internal sealed class PendingPersonaGenerationContext
        {
            public Pawn Pawn = null;
            public int Attempt = 0;
            public List<ChatMessageData> Messages = null;
        }

        internal const int PersonaBootstrapTickInterval = 150;
        internal const int PersonaRuntimeScanIntervalTicks = 9000; // was 900; reduced peak frequency 10x
        internal const int PersonaPromptMaxLength = 1200;
        internal const int CurrentNpcPersonaBootstrapVersion = 3;
        internal const string RimTalkPersonaServiceTypeName = "Ustas.RimAI.Communication.Data.PersonaService";
        internal const string RimTalkDependencyToken = "rimtalk";

        internal static readonly Regex WhitespaceRegex = new Regex(@"\s+", RegexOptions.Compiled);
        internal static readonly Regex PersonaSentenceStartRegex =
            new Regex(@"\b(?:He|She|They)\s+(?:is|are)\s+(?:a|an)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        internal static readonly Regex PersonaTemplateRegex =
            new Regex(
                @"^(?:He|She|They)\s+(?:is|are)\s+(?:a|an)\s+.+?\s+person\s+who\s+.+?,\s+because\s+deep\s+down\s+(?:he|she|they)\s+seek[s]?\s+.+?,\s+but\s+this\s+also\s+makes\s+(?:him|her|them)\s+.+?(?:,\s+often\s+leading\s+to\s+.+?)?[.!]",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        internal bool npcPersonaBootstrapCompleted;
        internal int npcPersonaBootstrapVersion;
        internal bool npcPersonaBootstrapQueued;
        internal readonly Queue<Pawn> npcPersonaBootstrapTargets = new Queue<Pawn>();
        internal readonly Dictionary<string, PendingPersonaGenerationContext> npcPersonaPendingRequests =
            new Dictionary<string, PendingPersonaGenerationContext>();
        internal readonly HashSet<int> npcPersonaPendingThingIds = new HashSet<int>();
        internal List<Pawn> cachedNpcPersonaTargets;
        internal int npcPersonaTargetsCacheTick;
        // Multi-frame scan state to avoid blocking the game tick on full pawn sweep.
        internal bool personaScanInProgress;
        internal int personaScanMapIndex;
        internal List<Pawn> personaScanAccumulatedTargets;
        internal HashSet<int> personaScanSeenIds;
        internal int nextPersonaBootstrapTick;
        internal int nextPersonaRuntimeScanTick;
        internal bool npcPersonaRuntimeScanDisabledNoRimTalk;
        internal static readonly object RimTalkPersonaResolverLock = new object();
        internal static bool rimTalkPersonaResolverInitialized;
        internal static MethodInfo rimTalkGetPersonalityMethod;
        internal static bool rimTalkPersonaResolverLoggedUnavailable;
        internal static bool rimTalkPersonaAiBlockLogged;

        internal readonly struct PersonaPronouns
        {
            public PersonaPronouns(string subject, string beVerb, string possessive, string objective, string seekVerb)
            {
                Subject = subject;
                BeVerb = beVerb;
                Possessive = possessive;
                Objective = objective;
                SeekVerb = seekVerb;
            }

            public string Subject { get; }
            public string BeVerb { get; }
            public string Possessive { get; }
            public string Objective { get; }
            public string SeekVerb { get; }
            public string SubjectLower => Subject.ToLowerInvariant();
        }

        internal void GameComponentTick()
        {
            if (Current.ProgramState != ProgramState.Playing)
                return;

            if (npcPersonaBootstrapCompleted && npcPersonaRuntimeScanDisabledNoRimTalk)
                return;

            ProcessNpcPersonaBootstrapTick();
            ProcessNpcPersonaRuntimeTick();
        }

        

        

        

        

        

        

        

        

        

        

        

        

        internal bool HasPersonaPrompt(Pawn pawn)
        {
            return !string.IsNullOrWhiteSpace(Owner.GetPawnPersonaPrompt(pawn));
        }

        

        

        

        

        

        

        internal static bool IsRimTalkLoadedForPersonaBlock()
        {
            return ModDependencyProbe.IsLoaded(RimTalkDependencyToken);
        }

        

        

        internal bool TryCopyPawnPersonaFromRimTalk(Pawn pawn)
        {
            return TryCopyPawnPersonaFromRimTalk(pawn, ResolveRimTalkPersonaCopyTemplateOrDefaultCached());
        }

        

        internal bool TrySyncPawnPersonaFromRimTalk(Pawn pawn)
        {
            return TrySyncPawnPersonaFromRimTalk(pawn, ResolveRimTalkPersonaCopyTemplateOrDefaultCached());
        }

        

        

        internal static string ResolveRimTalkPersonaCopyTemplateOrDefaultCached()
        {
            return RelationsMod.Settings?.GetRimTalkPersonaCopyTemplateOrDefault() ?? string.Empty;
        }

        

        

        internal static bool CanCopyPawnPersonaFromRimTalk(Pawn pawn)
        {
            return IsEligibleRimTalkPersonaCopyTarget(pawn) && TryGetRimTalkSourcePersona(pawn, out _);
        }

        

        

        

        

        

        

        

        

        

        

        

        internal static bool IsPersonaTemplateFormat(string text)
        {
            return PersonaTemplateRegex.IsMatch(text);
        }

        

        internal static string CollapseWhitespace(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? string.Empty : WhitespaceRegex.Replace(text, " ").Trim();
        }

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        
        
        #region Cluster forwards
        internal void ExposeData_NpcPersonaBootstrap() => Parts.Slice1.ExposeData_NpcPersonaBootstrap();
        internal void MarkNpcPersonaBootstrapAsNewGame() => Parts.Slice1.MarkNpcPersonaBootstrapAsNewGame();
        internal void ScheduleNpcPersonaBootstrapOnLoad() => Parts.Slice1.ScheduleNpcPersonaBootstrapOnLoad();
        internal void OnPostLoadInit_NpcPersonaBootstrap() => Parts.Slice1.OnPostLoadInit_NpcPersonaBootstrap();
        internal void ProcessNpcPersonaBootstrapTick() => Parts.Slice1.ProcessNpcPersonaBootstrapTick();
        internal void ProcessNpcPersonaRuntimeTick() => Parts.Slice1.ProcessNpcPersonaRuntimeTick();
        internal void ProcessPersonaScanOneFrame(int currentTick) => Parts.Slice1.ProcessPersonaScanOneFrame(currentTick);
        internal void FinishPersonaScan(int currentTick) => Parts.Slice1.FinishPersonaScan(currentTick);
        internal void InitializeNpcPersonaBootstrapQueue() => Parts.Slice1.InitializeNpcPersonaBootstrapQueue();
        internal List<Pawn> CollectNpcPersonaBootstrapTargets() => Parts.Slice1.CollectNpcPersonaBootstrapTargets();
        internal static void AppendUniqueNpcTarget(List<Pawn> target, HashSet<int> ids, Pawn pawn) => RPGPersonaSlice1.AppendUniqueNpcTarget(target, ids, pawn);
        internal static bool IsEligibleNpcPersonaTarget(Pawn pawn) => RPGPersonaSlice1.IsEligibleNpcPersonaTarget(pawn);
        internal bool TryGetNextBootstrapPawn(out Pawn pawn) => Parts.Slice1.TryGetNextBootstrapPawn(out pawn);
        internal bool TryApplyRimTalkPersonaFromRuntimeScan() => Parts.Slice1.TryApplyRimTalkPersonaFromRuntimeScan();
        internal bool TryFindMissingPersonaPawn(out Pawn pawn) => Parts.Slice1.TryFindMissingPersonaPawn(out pawn);
        internal bool IsPawnPersonaGenerationPending(Pawn pawn) => Parts.Slice1.IsPawnPersonaGenerationPending(pawn);
        internal static bool CanStartPersonaGeneration() => RPGPersonaSlice1.CanStartPersonaGeneration();
        internal static bool ShouldBlockAiPersonaGeneration() => RPGPersonaSlice1.ShouldBlockAiPersonaGeneration();
        internal void StartNpcPersonaGeneration(Pawn pawn, int attempt) => Parts.Slice1.StartNpcPersonaGeneration(pawn, attempt);
        internal bool TryApplyRimTalkPersonaFromBootstrapQueue() => Parts.Slice1.TryApplyRimTalkPersonaFromBootstrapQueue();
        internal bool TryCopyPawnPersonaFromRimTalk(Pawn pawn, string template) => Parts.Slice1.TryCopyPawnPersonaFromRimTalk(pawn, template);
        internal bool TrySyncPawnPersonaFromRimTalk(Pawn pawn, string template) => Parts.Slice2.TrySyncPawnPersonaFromRimTalk(pawn, template);
        public bool TrySyncAllColonyPawnPersonasFromRimTalk(out int updated, out int cleared, out int unchanged, out int skipped) => Parts.Slice2.TrySyncAllColonyPawnPersonasFromRimTalk(out updated, out cleared, out unchanged, out skipped);
        internal static void TryEnsureRpgPersonaTokenCoverageSafe() => RPGPersonaSlice2.TryEnsureRpgPersonaTokenCoverageSafe();
        internal static bool IsEligibleRimTalkPersonaCopyTarget(Pawn pawn) => RPGPersonaSlice2.IsEligibleRimTalkPersonaCopyTarget(pawn);
        internal static bool TryGetRimTalkSourcePersona(Pawn pawn, out string sourcePersona) => RPGPersonaSlice2.TryGetRimTalkSourcePersona(pawn, out sourcePersona);
        internal static MethodInfo ResolveRimTalkGetPersonalityMethod() => RPGPersonaSlice2.ResolveRimTalkGetPersonalityMethod();
        internal static string NormalizeCopiedPersonaPrompt(string raw) => RPGPersonaSlice2.NormalizeCopiedPersonaPrompt(raw);
        internal string RenderPersonaCopyTemplateOrThrow(Pawn pawn, string template, string sourcePersona) => Parts.Slice2.RenderPersonaCopyTemplateOrThrow(pawn, template, sourcePersona);
        internal static PromptRenderException BuildPersonaCopyRenderException(string templateId, string channel, string message) => RPGPersonaSlice2.BuildPersonaCopyRenderException(templateId, channel, message);
        internal List<ChatMessageData> BuildNpcPersonaGenerationMessages(Pawn pawn) => Parts.Slice2.BuildNpcPersonaGenerationMessages(pawn);
        internal static string BuildPersonaTemplateLine(PersonaPronouns pronouns) => RPGPersonaSlice2.BuildPersonaTemplateLine(pronouns);
        internal void OnNpcPersonaGenerationSuccess(string requestId, string response) => Parts.Slice2.OnNpcPersonaGenerationSuccess(requestId, response);
        internal void OnNpcPersonaGenerationError(string requestId, string error) => Parts.Slice2.OnNpcPersonaGenerationError(requestId, error);
        internal void RetryOrFallbackPersonaPrompt(PendingPersonaGenerationContext pending) => Parts.Slice2.RetryOrFallbackPersonaPrompt(pending);
        internal static bool TryNormalizePersonaPrompt(string raw, out string normalized) => RPGPersonaSlice2.TryNormalizePersonaPrompt(raw, out normalized);
        internal static bool HasOrderedAnchors(string text, params string[] anchors) => RPGPersonaSlice2.HasOrderedAnchors(text, anchors);
        internal string BuildFallbackPersonaPrompt(Pawn pawn) => Parts.Slice2.BuildFallbackPersonaPrompt(pawn);
        internal static PersonaPronouns ResolvePersonaPronouns(Pawn pawn) => RPGPersonaSlice2.ResolvePersonaPronouns(pawn);
        internal static string BuildPersonaBootstrapPrompt(RpgPromptDefaultsConfig defaults, PersonaPronouns pronouns, string profile) => RPGPersonaSlice3.BuildPersonaBootstrapPrompt(defaults, pronouns, profile);
        internal static string RenderPersonaBootstrapTemplate(string template, PersonaPronouns pronouns) => RPGPersonaSlice3.RenderPersonaBootstrapTemplate(template, pronouns);
        internal static PromptRenderContext BuildPersonaBootstrapRenderContext(string templateId, PersonaPronouns pronouns) => RPGPersonaSlice3.BuildPersonaBootstrapRenderContext(templateId, pronouns);
        internal static string BuildCoreTemperament(Pawn pawn) => RPGPersonaSlice3.BuildCoreTemperament(pawn);
        internal static string BuildEmotionalPattern(Pawn pawn) => RPGPersonaSlice3.BuildEmotionalPattern(pawn);
        internal static string BuildBehavioralStrategy(Pawn pawn) => RPGPersonaSlice3.BuildBehavioralStrategy(pawn);
        internal static string BuildCoreMotivation(Pawn pawn) => RPGPersonaSlice3.BuildCoreMotivation(pawn);
        internal static string BuildDefenseWeakness(Pawn pawn) => RPGPersonaSlice3.BuildDefenseWeakness(pawn);
        internal static string BuildPersonalityCost(Pawn pawn) => RPGPersonaSlice3.BuildPersonalityCost(pawn);
        internal static bool StartsWithVowelSound(string text) => RPGPersonaSlice3.StartsWithVowelSound(text);
        internal void CompleteNpcPersonaBootstrap() => Parts.Slice3.CompleteNpcPersonaBootstrap();
        internal bool ShouldRunNpcPersonaBootstrap() => Parts.Slice3.ShouldRunNpcPersonaBootstrap();
        internal void ResetNpcPersonaBootstrapRuntimeState() => Parts.Slice3.ResetNpcPersonaBootstrapRuntimeState();
        #endregion
}
    internal sealed class RPGPersonaSlice2 : RPGManagerPersonaBootstrapCollaborator
    {
        internal RPGPersonaSlice2(RPGManagerPersonaBootstrap owner) : base(owner)
        {
        }

internal bool TrySyncPawnPersonaFromRimTalk(Pawn pawn, string template)
        {
            if (!RPGManagerPersonaBootstrap.IsEligibleRimTalkPersonaCopyTarget(pawn))
            {
                return false;
            }

            if (!RPGManagerPersonaBootstrap.TryGetRimTalkSourcePersona(pawn, out string sourcePersona))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(template))
            {
                DebugLogger.Debug("RimTalk persona sync skipped: template is empty.");
                return false;
            }

            string rendered = Owner.RenderPersonaCopyTemplateOrThrow(pawn, template, sourcePersona);
            string normalized = RPGManagerPersonaBootstrap.NormalizeCopiedPersonaPrompt(rendered);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw RPGManagerPersonaBootstrap.BuildPersonaCopyRenderException(
                    "prompt_templates.rpg_persona_copy",
                    "rpg",
                    $"Persona sync template returned empty normalized text for pawn '{pawn?.LabelShortCap ?? "unknown"}'.");
            }

            string current = GetPawnPersonaPrompt(pawn)?.Trim() ?? string.Empty;
            if (string.Equals(current, normalized, StringComparison.Ordinal))
            {
                return false;
            }

            SetPawnPersonaPrompt(pawn, normalized);
            RPGManagerPersonaBootstrap.TryEnsureRpgPersonaTokenCoverageSafe();
            DebugLogger.Debug($"RimTalk persona synced(update) for pawn '{pawn?.LabelShortCap}'.");
            return true;
        }

public bool TrySyncAllColonyPawnPersonasFromRimTalk(
            out int updated,
            out int cleared,
            out int unchanged,
            out int skipped)
        {
            updated = 0;
            cleared = 0;
            unchanged = 0;
            skipped = 0;
            string template = RPGManagerPersonaBootstrap.ResolveRimTalkPersonaCopyTemplateOrDefaultCached();
            var targets = cachedNpcPersonaTargets ?? Owner.CollectNpcPersonaBootstrapTargets();

            foreach (Pawn pawn in targets)
            {
                if (!RPGManagerPersonaBootstrap.CanCopyPawnPersonaFromRimTalk(pawn) || Owner.IsPawnPersonaGenerationPending(pawn))
                {
                    skipped++;
                    continue;
                }

                string before = GetPawnPersonaPrompt(pawn)?.Trim() ?? string.Empty;
                if (!Owner.TrySyncPawnPersonaFromRimTalk(pawn, template))
                {
                    unchanged++;
                    continue;
                }

                string after = GetPawnPersonaPrompt(pawn)?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(before) && string.IsNullOrWhiteSpace(after))
                {
                    cleared++;
                }
                else
                {
                    updated++;
                }
            }

            return updated > 0 || cleared > 0;
        }

internal static void TryEnsureRpgPersonaTokenCoverageSafe()
        {
            try
            {
                RelationsMod.Settings?.EnsurePawnPersonalityTokenForRpgChannelsSafe();
            }
            catch (Exception ex)
            {
                DebugLogger.Debug($"Failed to ensure RPG persona token coverage: {ex.Message}");
            }
        }

internal static bool IsEligibleRimTalkPersonaCopyTarget(Pawn pawn)
        {
            return pawn != null &&
                   pawn.Faction == Faction.OfPlayer &&
                   !pawn.Dead &&
                   !pawn.Destroyed &&
                   PawnDialogueRoutingPolicy.IsPersonaSyncEligible(pawn);
        }

internal static bool TryGetRimTalkSourcePersona(Pawn pawn, out string sourcePersona)
        {
            sourcePersona = string.Empty;
            if (!RPGManagerPersonaBootstrap.IsEligibleRimTalkPersonaCopyTarget(pawn))
            {
                DebugLogger.Debug(
                    $"RimTalk persona sync skipped: pawn '{pawn?.LabelShortCap ?? "unknown"}' is not persona-sync eligible.");
                return false;
            }

            MethodInfo getPersonality = RPGManagerPersonaBootstrap.ResolveRimTalkGetPersonalityMethod();
            if (getPersonality == null)
            {
                return false;
            }

            try
            {
                sourcePersona = RPGManagerPersonaBootstrap.CollapseWhitespace(getPersonality.Invoke(null, new object[] { pawn }) as string);
                return !string.IsNullOrWhiteSpace(sourcePersona);
            }
            catch (Exception ex)
            {
                DebugLogger.Debug($"RimTalk persona source read failed for pawn '{pawn?.LabelShortCap}': {ex.Message}");
                return false;
            }
        }

internal static MethodInfo ResolveRimTalkGetPersonalityMethod()
        {
            if (rimTalkPersonaResolverInitialized)
            {
                return rimTalkGetPersonalityMethod;
            }

            lock (RimTalkPersonaResolverLock)
            {
                if (rimTalkPersonaResolverInitialized)
                {
                    return rimTalkGetPersonalityMethod;
                }

                Type personaServiceType = GenTypes.GetTypeInAnyAssembly(RimTalkPersonaServiceTypeName);
                rimTalkGetPersonalityMethod = personaServiceType?.GetMethod(
                    "GetPersonality",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(Pawn) },
                    null);
                rimTalkPersonaResolverInitialized = true;
                if (rimTalkGetPersonalityMethod == null && !rimTalkPersonaResolverLoggedUnavailable)
                {
                    rimTalkPersonaResolverLoggedUnavailable = true;
                    DebugLogger.Debug("RimTalk persona source unavailable: Ustas.RimAI.Communication.Data.PersonaService.GetPersonality not found.");
                }

                return rimTalkGetPersonalityMethod;
            }
        }

internal static string NormalizeCopiedPersonaPrompt(string raw)
        {
            string normalized = RPGManagerPersonaBootstrap.CollapseWhitespace(raw);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            return normalized.Length > PersonaPromptMaxLength
                ? normalized.Substring(0, PersonaPromptMaxLength).TrimEnd()
                : normalized;
        }

internal string RenderPersonaCopyTemplateOrThrow(Pawn pawn, string template, string sourcePersona)
        {
            if (pawn == null || string.IsNullOrWhiteSpace(template))
            {
                throw RPGManagerPersonaBootstrap.BuildPersonaCopyRenderException(
                    "prompt_templates.rpg_persona_copy",
                    "rpg",
                    "Persona copy template or pawn is missing.");
            }

            if (string.IsNullOrWhiteSpace(sourcePersona))
            {
                throw RPGManagerPersonaBootstrap.BuildPersonaCopyRenderException(
                    "prompt_templates.rpg_persona_copy",
                    "rpg",
                    "Persona copy source is empty.");
            }

            const string templateId = "prompt_templates.rpg_persona_copy";
            const string channel = "rpg";
            DialogueScenarioContext scenarioContext = DialogueScenarioContext.CreateRpg(null, pawn, false);
            var runtimeValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["pawn.target"] = pawn,
                ["pawn.target.name"] = pawn.LabelShort ?? string.Empty,
                ["pawn.personality"] = sourcePersona
            };
            UserDefinedPromptVariableService.PopulateRuntimeValues(
                runtimeValues,
                new PromptRuntimeVariableContext(templateId, channel, scenarioContext, null));
            PromptRenderContext context = PromptRenderContext.Create(templateId, channel);
            context.SetValues(runtimeValues);
            string rendered = PromptTemplateRenderer.RenderOrThrow(templateId, channel, template, context);
            if (string.IsNullOrWhiteSpace(rendered))
            {
                throw RPGManagerPersonaBootstrap.BuildPersonaCopyRenderException(
                    templateId,
                    channel,
                    $"Persona copy template rendered empty text for pawn '{pawn?.LabelShortCap ?? "unknown"}'.");
            }

            return rendered;
        }

internal static PromptRenderException BuildPersonaCopyRenderException(
            string templateId,
            string channel,
            string message)
        {
            return new PromptRenderException(
                templateId,
                channel,
                new PromptRenderDiagnostic
                {
                    ErrorCode = PromptRenderErrorCode.TemplateBlocked,
                    Message = message ?? "Persona copy template blocked."
                });
        }

internal List<ChatMessageData> BuildNpcPersonaGenerationMessages(Pawn pawn)
        {
            PersonaPronouns pronouns = RPGManagerPersonaBootstrap.ResolvePersonaPronouns(pawn);
            string profile = PromptPersistenceService.Instance.BuildPawnPersonaBootstrapProfile(pawn);
            var variables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["pawn.profile"] = profile ?? string.Empty,
                ["pawn.target"] = pawn,
                ["pawn.target.name"] = pawn?.LabelShort ?? "Unknown",
                ["pawn.pronouns.subject"] = pronouns.Subject,
                ["pawn.pronouns.subject_lower"] = pronouns.SubjectLower,
                ["pawn.pronouns.be_verb"] = pronouns.BeVerb,
                ["pawn.pronouns.object"] = pronouns.Objective,
                ["pawn.pronouns.possessive"] = pronouns.Possessive,
                ["pawn.pronouns.seek_verb"] = pronouns.SeekVerb,
                ["dialogue.template_line"] = RPGManagerPersonaBootstrap.BuildPersonaTemplateLine(pronouns),
                ["dialogue.example_line"] = RpgPromptDefaultsProvider.GetDefaults().PersonaBootstrapExample ?? string.Empty,
                ["dialogue.primary_objective"] = "Generate exactly one persona bootstrap line.",
                ["dialogue.optional_followup"] = "Keep language concise and stable for long-term roleplay continuity.",
                ["dialogue.latest_unresolved_intent"] = string.Empty
            };
            DialogueScenarioContext context = DialogueScenarioContext.CreateRpg(
                null,
                pawn,
                false,
                new[] { "channel:persona_bootstrap", "phase:bootstrap" });
            string systemPrompt = PromptPersistenceService.Instance.BuildUnifiedChannelSystemPrompt(
                RimTalkPromptChannel.Rpg,
                RimTalkPromptEntryChannelCatalog.PersonaBootstrap,
                context,
                null,
                variables,
                "persona_profile",
                profile ?? string.Empty);

            return new List<ChatMessageData>
            {
                new ChatMessageData
                {
                    role = "system",
                    content = systemPrompt
                }
            };
        }

internal static string BuildPersonaTemplateLine(PersonaPronouns pronouns)
        {
            return $"{pronouns.Subject} {pronouns.BeVerb} a [core temperament] person who tends to [emotional pattern], "
                + $"usually handles situations by [behavioral strategy], because deep down {pronouns.SubjectLower} {pronouns.SeekVerb} [core motivation], "
                + $"but this also makes {pronouns.Objective} [defense/weakness], often leading to [personality cost].";
        }

internal void OnNpcPersonaGenerationSuccess(string requestId, string response)
        {
            if (string.IsNullOrWhiteSpace(requestId) ||
                !npcPersonaPendingRequests.TryGetValue(requestId, out PendingPersonaGenerationContext pending))
            {
                return;
            }

            npcPersonaPendingRequests.Remove(requestId);
            if (!RPGManagerPersonaBootstrap.IsEligibleNpcPersonaTarget(pending.Pawn) || Owner.HasPersonaPrompt(pending.Pawn))
            {
                return;
            }

            if (RPGManagerPersonaBootstrap.TryNormalizePersonaPrompt(response, out string normalized))
            {
                SetPawnPersonaPrompt(pending.Pawn, normalized);
                return;
            }

            Owner.RetryOrFallbackPersonaPrompt(pending);
        }

internal void OnNpcPersonaGenerationError(string requestId, string error)
        {
            if (string.IsNullOrWhiteSpace(requestId) ||
                !npcPersonaPendingRequests.TryGetValue(requestId, out PendingPersonaGenerationContext pending))
            {
                return;
            }

            npcPersonaPendingRequests.Remove(requestId);
            Owner.RetryOrFallbackPersonaPrompt(pending);
        }

internal void RetryOrFallbackPersonaPrompt(PendingPersonaGenerationContext pending)
        {
            if (pending == null || !RPGManagerPersonaBootstrap.IsEligibleNpcPersonaTarget(pending.Pawn) || Owner.HasPersonaPrompt(pending.Pawn))
            {
                return;
            }

            Owner.TrySyncPawnPersonaFromRimTalk(pending.Pawn);
            Owner.TryCopyPawnPersonaFromRimTalk(pending.Pawn);
        }

internal static bool TryNormalizePersonaPrompt(string raw, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            string text = RPGManagerPersonaBootstrap.CollapseWhitespace(raw.Replace("```", " ").Trim(' ', '"', '\'', '`'));
            Match start = PersonaSentenceStartRegex.Match(text);
            if (start.Success)
            {
                text = text.Substring(start.Index).Trim();
            }

            Match match = PersonaTemplateRegex.Match(text);
            if (!match.Success)
            {
                return false;
            }

            string personaLine = RPGManagerPersonaBootstrap.CollapseWhitespace(match.Value);
            if (!RPGManagerPersonaBootstrap.IsPersonaTemplateFormat(personaLine))
            {
                return false;
            }

            normalized = personaLine.Length > PersonaPromptMaxLength
                ? personaLine.Substring(0, PersonaPromptMaxLength).TrimEnd()
                : personaLine;
            return true;
        }

internal static bool HasOrderedAnchors(string text, params string[] anchors)
        {
            if (string.IsNullOrWhiteSpace(text) || anchors == null || anchors.Length == 0)
            {
                return false;
            }

            int index = 0;
            for (int i = 0; i < anchors.Length; i++)
            {
                index = text.IndexOf(anchors[i], index, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    return false;
                }

                index += anchors[i].Length;
            }

            return true;
        }

internal string BuildFallbackPersonaPrompt(Pawn pawn)
        {
            PersonaPronouns pronouns = RPGManagerPersonaBootstrap.ResolvePersonaPronouns(pawn);
            string temperament = RPGManagerPersonaBootstrap.BuildCoreTemperament(pawn);
            string emotion = RPGManagerPersonaBootstrap.BuildEmotionalPattern(pawn);
            string strategy = RPGManagerPersonaBootstrap.BuildBehavioralStrategy(pawn);
            string motivation = RPGManagerPersonaBootstrap.BuildCoreMotivation(pawn);
            string defense = RPGManagerPersonaBootstrap.BuildDefenseWeakness(pawn);
            string cost = RPGManagerPersonaBootstrap.BuildPersonalityCost(pawn);
            string article = RPGManagerPersonaBootstrap.StartsWithVowelSound(temperament) ? "an" : "a";
            string prompt =
                $"{pronouns.Subject} {pronouns.BeVerb} {article} {temperament} person who tends to {emotion}, " +
                $"usually handles situations by {strategy}, because deep down {pronouns.SubjectLower} {pronouns.SeekVerb} {motivation}, " +
                $"but this also makes {pronouns.Objective} {defense}, often leading to {cost}.";
            return prompt.Length > PersonaPromptMaxLength ? prompt.Substring(0, PersonaPromptMaxLength).TrimEnd() : prompt;
        }

internal static PersonaPronouns ResolvePersonaPronouns(Pawn pawn)
        {
            switch (pawn?.gender ?? Gender.None)
            {
                case Gender.Female:
                    return new PersonaPronouns("She", "is", "her", "her", "seeks");
                case Gender.Male:
                    return new PersonaPronouns("He", "is", "his", "him", "seeks");
                default:
                    return new PersonaPronouns("They", "are", "their", "them", "seek");
            }
        }
    }

    internal sealed class RPGManagerPersonaBootstrapParts
    {
        internal readonly RPGManagerPersonaBootstrap Owner;
        internal readonly RPGPersonaSlice1 Slice1;
        internal readonly RPGPersonaSlice2 Slice2;
        internal readonly RPGPersonaSlice3 Slice3;
        internal RPGManagerPersonaBootstrapParts(RPGManagerPersonaBootstrap owner)
        {
            Owner = owner;
            Slice1 = new RPGPersonaSlice1(owner);
            Slice2 = new RPGPersonaSlice2(owner);
            Slice3 = new RPGPersonaSlice3(owner);
        }
    }


}
