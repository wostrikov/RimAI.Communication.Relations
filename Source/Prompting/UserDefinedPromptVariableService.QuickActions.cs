using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Config;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Prompting
{
    internal enum QuickPromptTargetKind
    {
        Faction = 0,
        Pawn = 1
    }

    internal enum QuickPromptConflictDecision
    {
        ReuseExisting = 0,
        TakeOver = 1
    }

    /// <summary>
    /// Dependencies: unified custom-variable CRUD/validation pipeline plus in-game faction/pawn runtime objects.
    /// Responsibility: provide fixed-slot quick prompt helpers for faction and pawn persona rule editing.
    /// </summary>
        internal static class UserDefinedPromptVariableServiceQuickActions
    {

        internal const string QuickFactionPersonaKey = "quick_faction_persona";
        internal const string QuickPawnPersonaKey = "quick_pawn_persona";
        internal const string QuickFactionVariableIdPrefix = "rimchat_quick_faction_var_";
        internal const string QuickPawnVariableIdPrefix = "rimchat_quick_pawn_var_";
        internal const string QuickFactionRuleIdPrefix = "rimchat_quick_faction_rule_";
        internal const string QuickPawnRuleIdPrefix = "rimchat_quick_pawn_rule_";
        internal const string QuickPawnThingIdPrefix = "thingid:";

        public static string BuildQuickPath(QuickPromptTargetKind kind)
        {
            return UserDefinedPromptVariableService.BuildPath(UserDefinedPromptVariableService.GetQuickKey(kind));
        }

        public static string BuildQuickToken(QuickPromptTargetKind kind)
        {
            return "{{ " + UserDefinedPromptVariableService.BuildQuickPath(kind) + " }}";
        }

        public static bool RequiresQuickConflictResolution(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings, QuickPromptTargetKind kind)
        {
            UserDefinedPromptVariableConfig variable = UserDefinedPromptVariableService.FindVariableByKey(UserDefinedPromptVariableService.GetQuickKey(kind), settings);
            return variable != null &&
                   !UserDefinedPromptVariableService.IsQuickManagedVariable(variable, kind) &&
                   !UserDefinedPromptVariableService.HasQuickManagedRules(settings, kind);
        }

        public static string GetQuickFactionTemplate(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings, Faction faction)
        {
            if (faction?.def == null)
            {
                return string.Empty;
            }

            string key = UserDefinedPromptVariableService.GetQuickKey(QuickPromptTargetKind.Faction);
            FactionPromptVariableRuleConfig rule = UserDefinedPromptVariableService.GetFactionRulesForKey(key, settings)
                .FirstOrDefault(item =>
                    item != null &&
                    string.Equals(item.FactionDefName, faction.def.defName, StringComparison.OrdinalIgnoreCase));
            return rule?.TemplateText ?? string.Empty;
        }

        public static string GetQuickPawnTemplate(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings, Pawn pawn)
        {
            PawnPromptVariableRuleConfig rule = UserDefinedPromptVariableService.FindQuickPawnRule(UserDefinedPromptVariableService.GetPawnRulesForKey(UserDefinedPromptVariableService.GetQuickKey(QuickPromptTargetKind.Pawn), settings), pawn);
            return rule?.TemplateText ?? string.Empty;
        }

        public static bool TrySaveQuickFactionPrompt(
            Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings,
            Faction faction,
            string templateText,
            QuickPromptConflictDecision decision,
            out UserDefinedPromptVariableValidationResult validationResult)
        {
            validationResult = new UserDefinedPromptVariableValidationResult();
            if (settings == null || faction?.def == null)
            {
                validationResult.Errors.Add("Quick faction prompt target is unavailable.");
                return false;
            }

            string key = UserDefinedPromptVariableService.GetQuickKey(QuickPromptTargetKind.Faction);
            UserDefinedPromptVariableConfig originalVariable = UserDefinedPromptVariableService.FindVariableByKey(key, settings)?.Clone();
            UserDefinedPromptVariableEditModel model = UserDefinedPromptVariableService.BuildQuickEditModel(settings, QuickPromptTargetKind.Faction, decision);
            FactionPromptVariableRuleConfig rule = model.FactionRules.FirstOrDefault(item =>
                item != null &&
                string.Equals(item.FactionDefName, faction.def.defName, StringComparison.OrdinalIgnoreCase));
            if (rule == null)
            {
                rule = new FactionPromptVariableRuleConfig
                {
                    Id = UserDefinedPromptVariableService.CreateQuickRuleId(QuickPromptTargetKind.Faction),
                    VariableKey = key,
                    FactionDefName = faction.def.defName,
                    Priority = 0,
                    Enabled = true,
                    Order = model.FactionRules.Count
                };
                model.FactionRules.Add(rule);
            }

            rule.Id = UserDefinedPromptVariableService.EnsureQuickRuleId(rule.Id, QuickPromptTargetKind.Faction);
            rule.VariableKey = key;
            rule.FactionDefName = faction.def.defName;
            rule.TemplateText = templateText ?? string.Empty;
            rule.Enabled = true;
            return UserDefinedPromptVariableService.TrySaveEdit(settings, model, originalVariable, out validationResult);
        }

        public static bool TrySaveQuickPawnPrompt(
            Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings,
            Pawn pawn,
            string templateText,
            QuickPromptConflictDecision decision,
            out UserDefinedPromptVariableValidationResult validationResult)
        {
            validationResult = new UserDefinedPromptVariableValidationResult();
            if (settings == null || pawn == null)
            {
                validationResult.Errors.Add("Quick pawn prompt target is unavailable.");
                return false;
            }

            string key = UserDefinedPromptVariableService.GetQuickKey(QuickPromptTargetKind.Pawn);
            UserDefinedPromptVariableConfig originalVariable = UserDefinedPromptVariableService.FindVariableByKey(key, settings)?.Clone();
            UserDefinedPromptVariableEditModel model = UserDefinedPromptVariableService.BuildQuickEditModel(settings, QuickPromptTargetKind.Pawn, decision);
            PawnPromptVariableRuleConfig rule = UserDefinedPromptVariableService.FindQuickPawnRule(model.PawnRules, pawn);
            if (rule == null)
            {
                rule = new PawnPromptVariableRuleConfig
                {
                    Id = UserDefinedPromptVariableService.CreateQuickRuleId(QuickPromptTargetKind.Pawn),
                    VariableKey = key,
                    Priority = 0,
                    Enabled = true,
                    Order = model.PawnRules.Count
                };
                model.PawnRules.Add(rule);
            }

            rule.Id = UserDefinedPromptVariableService.EnsureQuickRuleId(rule.Id, QuickPromptTargetKind.Pawn);
            rule.VariableKey = key;
            rule.NameExact = UserDefinedPromptVariableService.BuildQuickPawnMatchToken(pawn);
            rule.FactionDefName = string.Empty;
            rule.RaceDefName = string.Empty;
            rule.Gender = string.Empty;
            rule.AgeStage = string.Empty;
            rule.XenotypeDefName = string.Empty;
            rule.PlayerControlled = string.Empty;
            rule.TraitsAny = new List<string>();
            rule.TraitsAll = new List<string>();
            rule.TemplateText = templateText ?? string.Empty;
            rule.Enabled = true;
            return UserDefinedPromptVariableService.TrySaveEdit(settings, model, originalVariable, out validationResult);
        }

        public static string BuildQuickPawnMatchToken(Pawn pawn)
        {
            return pawn == null || string.IsNullOrWhiteSpace(pawn.ThingID)
                ? string.Empty
                : QuickPawnThingIdPrefix + pawn.ThingID.Trim();
        }

        internal static UserDefinedPromptVariableEditModel BuildQuickEditModel(
            Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings,
            QuickPromptTargetKind kind,
            QuickPromptConflictDecision decision)
        {
            string key = UserDefinedPromptVariableService.GetQuickKey(kind);
            UserDefinedPromptVariableConfig existing = UserDefinedPromptVariableService.FindVariableByKey(key, settings)?.Clone();
            UserDefinedPromptVariableConfig variable = existing ?? UserDefinedPromptVariableService.BuildOfficialQuickVariable(kind);
            if (existing != null && decision == QuickPromptConflictDecision.TakeOver)
            {
                UserDefinedPromptVariableService.ApplyOfficialQuickVariableMetadata(variable, kind);
            }

            return new UserDefinedPromptVariableEditModel
            {
                Variable = variable,
                FactionRules = UserDefinedPromptVariableService.GetFactionRulesForKey(key, settings),
                PawnRules = UserDefinedPromptVariableService.GetPawnRulesForKey(key, settings)
            };
        }

        internal static UserDefinedPromptVariableConfig BuildOfficialQuickVariable(QuickPromptTargetKind kind)
        {
            var variable = new UserDefinedPromptVariableConfig();
            UserDefinedPromptVariableService.ApplyOfficialQuickVariableMetadata(variable, kind);
            return variable;
        }

        internal static void ApplyOfficialQuickVariableMetadata(UserDefinedPromptVariableConfig variable, QuickPromptTargetKind kind)
        {
            if (variable == null)
            {
                return;
            }

            variable.Id = UserDefinedPromptVariableService.CreateQuickVariableId(kind);
            variable.Key = UserDefinedPromptVariableService.GetQuickKey(kind);
            variable.DisplayName = UserDefinedPromptVariableService.BuildQuickPath(kind);
            variable.Description = UserDefinedPromptVariableService.GetQuickDescription(kind);
            variable.DefaultTemplateText = string.Empty;
            variable.Enabled = true;
        }

        internal static bool HasQuickManagedRules(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings, QuickPromptTargetKind kind)
        {
            string prefix = UserDefinedPromptVariableService.GetQuickRuleIdPrefix(kind);
            if (kind == QuickPromptTargetKind.Faction)
            {
                return UserDefinedPromptVariableService.GetFactionRulesForKey(UserDefinedPromptVariableService.GetQuickKey(kind), settings)
                    .Any(item => item != null && UserDefinedPromptVariableService.HasQuickIdPrefix(item.Id, prefix));
            }

            return UserDefinedPromptVariableService.GetPawnRulesForKey(UserDefinedPromptVariableService.GetQuickKey(kind), settings)
                .Any(item => item != null && UserDefinedPromptVariableService.HasQuickIdPrefix(item.Id, prefix));
        }

        internal static bool IsQuickManagedVariable(UserDefinedPromptVariableConfig variable, QuickPromptTargetKind kind)
        {
            return variable != null &&
                   string.Equals(UserDefinedPromptVariableService.NormalizeKey(variable.Key), UserDefinedPromptVariableService.GetQuickKey(kind), StringComparison.OrdinalIgnoreCase) &&
                   UserDefinedPromptVariableService.HasQuickIdPrefix(variable.Id, UserDefinedPromptVariableService.GetQuickVariableIdPrefix(kind));
        }

        internal static PawnPromptVariableRuleConfig FindQuickPawnRule(IEnumerable<PawnPromptVariableRuleConfig> rules, Pawn pawn)
        {
            if (pawn == null)
            {
                return null;
            }

            string quickToken = UserDefinedPromptVariableService.BuildQuickPawnMatchToken(pawn);
            string resolvedName = UserDefinedPromptVariableRuleMatcher.ResolvePawnName(pawn);
            return (rules ?? Enumerable.Empty<PawnPromptVariableRuleConfig>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.NameExact))
                .FirstOrDefault(item =>
                    string.Equals(item.NameExact, quickToken, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item.NameExact, resolvedName, StringComparison.OrdinalIgnoreCase));
        }

        internal static string GetQuickKey(QuickPromptTargetKind kind)
        {
            return kind == QuickPromptTargetKind.Faction
                ? QuickFactionPersonaKey
                : QuickPawnPersonaKey;
        }

        internal static string GetQuickDescription(QuickPromptTargetKind kind)
        {
            return kind == QuickPromptTargetKind.Faction
                ? "RimChat_PromptWorkbench_QuickFactionDescription".Translate().ToString()
                : "RimChat_PromptWorkbench_QuickPawnDescription".Translate().ToString();
        }

        internal static string GetQuickVariableIdPrefix(QuickPromptTargetKind kind)
        {
            return kind == QuickPromptTargetKind.Faction
                ? QuickFactionVariableIdPrefix
                : QuickPawnVariableIdPrefix;
        }

        internal static string GetQuickRuleIdPrefix(QuickPromptTargetKind kind)
        {
            return kind == QuickPromptTargetKind.Faction
                ? QuickFactionRuleIdPrefix
                : QuickPawnRuleIdPrefix;
        }

        internal static string CreateQuickVariableId(QuickPromptTargetKind kind)
        {
            return UserDefinedPromptVariableService.GetQuickVariableIdPrefix(kind) + Guid.NewGuid().ToString("N");
        }

        internal static string CreateQuickRuleId(QuickPromptTargetKind kind)
        {
            return UserDefinedPromptVariableService.GetQuickRuleIdPrefix(kind) + Guid.NewGuid().ToString("N");
        }

        internal static string EnsureQuickRuleId(string existingId, QuickPromptTargetKind kind)
        {
            string prefix = UserDefinedPromptVariableService.GetQuickRuleIdPrefix(kind);
            return UserDefinedPromptVariableService.HasQuickIdPrefix(existingId, prefix)
                ? existingId
                : prefix + Guid.NewGuid().ToString("N");
        }

        internal static bool HasQuickIdPrefix(string value, string prefix)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        }

}
