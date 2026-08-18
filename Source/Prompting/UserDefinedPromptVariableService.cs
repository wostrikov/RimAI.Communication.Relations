using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Persistence;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Prompting
{
    /// <summary>
    /// Dependencies: Relations settings, prompt validation service, and RimWorld defs.
    /// Responsibility: own normalized CRUD, validation, migration, runtime rendering, and editor metadata for user-defined prompt variables.
    /// </summary>
    internal static class UserDefinedPromptVariableService
    {
        public const string NamespaceRoot = "system.custom";
        internal const string SourceId = "rimai.relations.user";
        internal const string SourceLabel = "User Variable";
        public const string QuickPawnThingIdPrefix = "thingid:";

        internal static readonly string[] SuggestedKeys =
        {
            "pawn_personality_override",
            "pawn_personality_append",
            "faction_tone",
            "faction_attitude_text",
            "pawn_speaking_style",
            "relationship_flavor"
        };

        

        

        

        

        

        public static string GetSourceId()
        {
            return SourceId;
        }

        public static string GetSourceLabel()
        {
            return SourceLabel;
        }

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        public static IEnumerable<string> GetSuggestedKeys()
        {
            return SuggestedKeys;
        }

        

        

        

        

        



        

        

        

        

        

        

        internal sealed class PromptTemplateReferenceCandidate
        {
            public PromptTemplateReferenceCandidate(string locationId, string displayText, string templateText)
            {
                LocationId = locationId ?? string.Empty;
                DisplayText = displayText ?? string.Empty;
                TemplateText = templateText ?? string.Empty;
            }

            public string LocationId { get; }
            public string DisplayText { get; }
            public string TemplateText { get; }
        }

        #region Facade forwards
        public static string BuildQuickPath(QuickPromptTargetKind kind) => UserDefinedPromptVariableServiceQuickActions.BuildQuickPath(kind);
        public static string BuildQuickToken(QuickPromptTargetKind kind) => UserDefinedPromptVariableServiceQuickActions.BuildQuickToken(kind);
        public static bool RequiresQuickConflictResolution(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings, QuickPromptTargetKind kind) => UserDefinedPromptVariableServiceQuickActions.RequiresQuickConflictResolution(settings, kind);
        public static string GetQuickFactionTemplate(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings, Faction faction) => UserDefinedPromptVariableServiceQuickActions.GetQuickFactionTemplate(settings, faction);
        public static string GetQuickPawnTemplate(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings, Pawn pawn) => UserDefinedPromptVariableServiceQuickActions.GetQuickPawnTemplate(settings, pawn);
        public static bool TrySaveQuickFactionPrompt(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings, Faction faction, string templateText, QuickPromptConflictDecision decision, out UserDefinedPromptVariableValidationResult validationResult) => UserDefinedPromptVariableServiceQuickActions.TrySaveQuickFactionPrompt(settings, faction, templateText, decision, out validationResult);
        public static bool TrySaveQuickPawnPrompt(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings, Pawn pawn, string templateText, QuickPromptConflictDecision decision, out UserDefinedPromptVariableValidationResult validationResult) => UserDefinedPromptVariableServiceQuickActions.TrySaveQuickPawnPrompt(settings, pawn, templateText, decision, out validationResult);
        public static string BuildQuickPawnMatchToken(Pawn pawn) => UserDefinedPromptVariableServiceQuickActions.BuildQuickPawnMatchToken(pawn);
        internal static UserDefinedPromptVariableEditModel BuildQuickEditModel(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings, QuickPromptTargetKind kind, QuickPromptConflictDecision decision) => UserDefinedPromptVariableServiceQuickActions.BuildQuickEditModel(settings, kind, decision);
        internal static UserDefinedPromptVariableConfig BuildOfficialQuickVariable(QuickPromptTargetKind kind) => UserDefinedPromptVariableServiceQuickActions.BuildOfficialQuickVariable(kind);
        internal static void ApplyOfficialQuickVariableMetadata(UserDefinedPromptVariableConfig variable, QuickPromptTargetKind kind) => UserDefinedPromptVariableServiceQuickActions.ApplyOfficialQuickVariableMetadata(variable, kind);
        internal static bool HasQuickManagedRules(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings, QuickPromptTargetKind kind) => UserDefinedPromptVariableServiceQuickActions.HasQuickManagedRules(settings, kind);
        internal static bool IsQuickManagedVariable(UserDefinedPromptVariableConfig variable, QuickPromptTargetKind kind) => UserDefinedPromptVariableServiceQuickActions.IsQuickManagedVariable(variable, kind);
        internal static PawnPromptVariableRuleConfig FindQuickPawnRule(IEnumerable<PawnPromptVariableRuleConfig> rules, Pawn pawn) => UserDefinedPromptVariableServiceQuickActions.FindQuickPawnRule(rules, pawn);
        internal static string GetQuickKey(QuickPromptTargetKind kind) => UserDefinedPromptVariableServiceQuickActions.GetQuickKey(kind);
        internal static string GetQuickDescription(QuickPromptTargetKind kind) => UserDefinedPromptVariableServiceQuickActions.GetQuickDescription(kind);
        internal static string GetQuickVariableIdPrefix(QuickPromptTargetKind kind) => UserDefinedPromptVariableServiceQuickActions.GetQuickVariableIdPrefix(kind);
        internal static string GetQuickRuleIdPrefix(QuickPromptTargetKind kind) => UserDefinedPromptVariableServiceQuickActions.GetQuickRuleIdPrefix(kind);
        internal static string CreateQuickVariableId(QuickPromptTargetKind kind) => UserDefinedPromptVariableServiceQuickActions.CreateQuickVariableId(kind);
        internal static string CreateQuickRuleId(QuickPromptTargetKind kind) => UserDefinedPromptVariableServiceQuickActions.CreateQuickRuleId(kind);
        internal static string EnsureQuickRuleId(string existingId, QuickPromptTargetKind kind) => UserDefinedPromptVariableServiceQuickActions.EnsureQuickRuleId(existingId, kind);
        internal static bool HasQuickIdPrefix(string value, string prefix) => UserDefinedPromptVariableServiceQuickActions.HasQuickIdPrefix(value, prefix);
        public static void PopulateRuntimeValues(IDictionary<string, object> values, PromptRuntimeVariableContext context) => UserDefinedPromptVariableServiceRuntime.PopulateRuntimeValues(values, context);
        internal static string ResolveVariableValue(string path, IDictionary<string, object> values, PromptRuntimeVariableContext context, IDictionary<string, string> cache, Stack<string> resolving) => UserDefinedPromptVariableServiceRuntime.ResolveVariableValue(path, values, context, cache, resolving);
        internal static string RenderTemplate(string templateText, IDictionary<string, object> values, PromptRuntimeVariableContext context, IDictionary<string, string> cache, Stack<string> resolving) => UserDefinedPromptVariableServiceRuntime.RenderTemplate(templateText, values, context, cache, resolving);
        internal static void ApplyEffectivePawnPersonality(IDictionary<string, object> values, PromptRuntimeVariableContext context, IDictionary<string, string> cache, Stack<string> resolving) => UserDefinedPromptVariableServiceRuntime.ApplyEffectivePawnPersonality(values, context, cache, resolving);
        internal static string ResolveOptionalVariableValue(string path, IDictionary<string, object> values, PromptRuntimeVariableContext context, IDictionary<string, string> cache, Stack<string> resolving) => UserDefinedPromptVariableServiceRuntime.ResolveOptionalVariableValue(path, values, context, cache, resolving);
        public static UserDefinedPromptVariableValidationResult ValidateEdit(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings, UserDefinedPromptVariableEditModel editModel, UserDefinedPromptVariableConfig originalVariable = null) => UserDefinedPromptVariableServiceValidation.ValidateEdit(settings, editModel, originalVariable);
        public static bool TrySaveEdit(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings, UserDefinedPromptVariableEditModel editModel, UserDefinedPromptVariableConfig originalVariable, out UserDefinedPromptVariableValidationResult validationResult) => UserDefinedPromptVariableServiceValidation.TrySaveEdit(settings, editModel, originalVariable, out validationResult);
        internal static void ValidateFactionRules(UserDefinedPromptVariableValidationResult result, Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings, UserDefinedPromptVariableEditModel editModel, string normalizedKey, string currentPath, UserDefinedPromptVariableConfig originalVariable) => UserDefinedPromptVariableServiceValidation.ValidateFactionRules(result, settings, editModel, normalizedKey, currentPath, originalVariable);
        internal static void ValidatePawnRules(UserDefinedPromptVariableValidationResult result, Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings, UserDefinedPromptVariableEditModel editModel, string normalizedKey, string currentPath, UserDefinedPromptVariableConfig originalVariable) => UserDefinedPromptVariableServiceValidation.ValidatePawnRules(result, settings, editModel, normalizedKey, currentPath, originalVariable);
        internal static void ValidatePawnRuleConditions(UserDefinedPromptVariableValidationResult result, PawnPromptVariableRuleConfig rule) => UserDefinedPromptVariableServiceValidation.ValidatePawnRuleConditions(result, rule);
        internal static void ApplyVariable(UserDefinedPromptVariableConfig target, UserDefinedPromptVariableConfig source) => UserDefinedPromptVariableServiceValidation.ApplyVariable(target, source);
        internal static void ValidateTemplate(UserDefinedPromptVariableValidationResult result, Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings, string templateId, string templateText, string currentPath, UserDefinedPromptVariableConfig originalVariable) => UserDefinedPromptVariableServiceValidation.ValidateTemplate(result, settings, templateId, templateText, currentPath, originalVariable);
        internal static IEnumerable<string> BuildAdditionalKnownPaths(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings, string currentPath, UserDefinedPromptVariableConfig originalVariable) => UserDefinedPromptVariableServiceValidation.BuildAdditionalKnownPaths(settings, currentPath, originalVariable);
        internal static void DetectCycleErrors(UserDefinedPromptVariableValidationResult result, Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings, UserDefinedPromptVariableEditModel editModel, UserDefinedPromptVariableConfig originalVariable) => UserDefinedPromptVariableServiceValidation.DetectCycleErrors(result, settings, editModel, originalVariable);
        internal static Dictionary<string, HashSet<string>> BuildDependencyGraph(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings, UserDefinedPromptVariableEditModel editModel, UserDefinedPromptVariableConfig originalVariable) => UserDefinedPromptVariableServiceValidation.BuildDependencyGraph(settings, editModel, originalVariable);
        #endregion
    
        #region Cluster forwards
        public static bool IsUserDefinedPath(string path) => UserDefinedPromptVariableSlice1.IsUserDefinedPath(path);
        public static string NormalizeKey(string key) => UserDefinedPromptVariableSlice1.NormalizeKey(key);
        public static bool IsValidKey(string key) => UserDefinedPromptVariableSlice1.IsValidKey(key);
        public static string BuildPath(string key) => UserDefinedPromptVariableSlice1.BuildPath(key);
        public static string ExtractKeyFromPath(string path) => UserDefinedPromptVariableSlice1.ExtractKeyFromPath(path);
        public static IReadOnlyList<UserDefinedPromptVariableConfig> GetVariables(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings = null) => UserDefinedPromptVariableSlice1.GetVariables(settings);
        public static IReadOnlyList<FactionPromptVariableRuleConfig> GetFactionRules(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings = null) => UserDefinedPromptVariableSlice1.GetFactionRules(settings);
        public static IReadOnlyList<PawnPromptVariableRuleConfig> GetPawnRules(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings = null) => UserDefinedPromptVariableSlice1.GetPawnRules(settings);
        public static IReadOnlyList<FactionScopedPromptVariableOverrideConfig> GetLegacyOverrides(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings = null) => UserDefinedPromptVariableSlice1.GetLegacyOverrides(settings);
        public static UserDefinedPromptVariableConfig FindVariableByPath(string path, Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings = null) => UserDefinedPromptVariableSlice1.FindVariableByPath(path, settings);
        public static UserDefinedPromptVariableConfig FindVariableByKey(string key, Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings = null) => UserDefinedPromptVariableSlice1.FindVariableByKey(key, settings);
        public static List<FactionPromptVariableRuleConfig> GetFactionRulesForKey(string key, Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings = null) => UserDefinedPromptVariableSlice1.GetFactionRulesForKey(key, settings);
        public static List<PawnPromptVariableRuleConfig> GetPawnRulesForKey(string key, Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings = null) => UserDefinedPromptVariableSlice1.GetPawnRulesForKey(key, settings);
        public static PromptRuntimeVariableDefinition BuildDefinition(UserDefinedPromptVariableConfig config) => UserDefinedPromptVariableSlice1.BuildDefinition(config);
        public static string BuildDefinitionDescription(UserDefinedPromptVariableConfig config, IReadOnlyCollection<FactionPromptVariableRuleConfig> factionRules, IReadOnlyCollection<PawnPromptVariableRuleConfig> pawnRules) => UserDefinedPromptVariableSlice1.BuildDefinitionDescription(config, factionRules, pawnRules);
        public static PromptVariableTooltipInfo BuildTooltipInfo(string path) => UserDefinedPromptVariableSlice1.BuildTooltipInfo(path);
        public static void NormalizeSettingsCollections(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings) => UserDefinedPromptVariableSlice1.NormalizeSettingsCollections(settings);
        public static List<UserDefinedPromptVariableReferenceLocation> FindReferences(string path, Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings = null) => UserDefinedPromptVariableSlice1.FindReferences(path, settings);
        public static bool TryDeleteVariable(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings, string path, out List<UserDefinedPromptVariableReferenceLocation> references) => UserDefinedPromptVariableSlice1.TryDeleteVariable(settings, path, out references);
        public static UserDefinedPromptVariableEditModel CreateSuggestedModel(string key) => UserDefinedPromptVariableSlice1.CreateSuggestedModel(key);
        internal static void NormalizeVariables(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings) => UserDefinedPromptVariableSlice1.NormalizeVariables(settings);
        internal static void MigrateLegacyOverrides(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings) => UserDefinedPromptVariableSlice1.MigrateLegacyOverrides(settings);
        internal static void NormalizeFactionRules(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings) => UserDefinedPromptVariableSlice1.NormalizeFactionRules(settings);
        internal static void NormalizePawnRules(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings) => UserDefinedPromptVariableSlice2.NormalizePawnRules(settings);
        internal static void AddDependencies(Dictionary<string, HashSet<string>> graph, string key, string templateText) => UserDefinedPromptVariableSlice2.AddDependencies(graph, key, templateText);
        internal static bool TryFindCycle(string current, Dictionary<string, HashSet<string>> graph, HashSet<string> visiting, HashSet<string> visited, List<string> path, out List<string> cycle) => UserDefinedPromptVariableSlice2.TryFindCycle(current, graph, visiting, visited, path, out cycle);
        internal static IEnumerable<PromptTemplateReferenceCandidate> EnumerateReferenceCandidates(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings) => UserDefinedPromptVariableSlice2.EnumerateReferenceCandidates(settings);
        internal static string NormalizeBoolToken(string value) => UserDefinedPromptVariableSlice2.NormalizeBoolToken(value);
        internal static string BuildSuggestedDescription(string key) => UserDefinedPromptVariableSlice2.BuildSuggestedDescription(key);
        internal static string BuildSuggestedTemplate(string key) => UserDefinedPromptVariableSlice2.BuildSuggestedTemplate(key);
        #endregion
}


}
