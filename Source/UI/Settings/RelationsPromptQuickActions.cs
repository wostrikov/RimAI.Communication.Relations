using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.UI;
using Ustas.RimAI.Communication.Relations.Module;
using RimWorld;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsPromptQuickActions
{
    readonly RelationsSettingsPages Pages;

    internal RelationsPromptQuickActions(RelationsSettingsPages pages)
    {
        Pages = pages;
    }

    RelationsSettings Settings => Pages.Settings;

        // Quick actions cache to eliminate per-frame LINQ operations on world entities
        internal static List<Faction> _cachedQuickFactions;
        internal static int _lastQuickFactionCacheTick = -1;
        internal const int QuickFactionCacheTicks = 60; // Refresh every 60 ticks (~1 second)

        internal static List<Pawn> _cachedQuickPawns;
        internal static int _lastQuickPawnCacheTick = -1;
        internal const int QuickPawnCacheTicks = 60; // Refresh every 60 ticks (~1 second)
        internal void DrawPromptWorkspaceQuickActions(Rect rect)
        {
            float labelWidth = Mathf.Min(120f, Mathf.Max(78f, rect.width * 0.32f));
            Rect labelRect = new Rect(rect.x, rect.y, labelWidth, rect.height);
            // Keep spacing for layout stability while hiding the quick header text.

            float buttonGap = 8f;
            float buttonWidth = Mathf.Max(84f, (rect.width - labelWidth - buttonGap * 2f) * 0.5f);
            Rect factionRect = new Rect(labelRect.xMax + buttonGap, rect.y, buttonWidth, rect.height);
            Rect pawnRect = new Rect(factionRect.xMax + buttonGap, rect.y, Mathf.Max(84f, rect.xMax - (factionRect.xMax + buttonGap)), rect.height);
            bool factionEnabled = CanUsePromptWorkspaceFactionTemplateQuickAction();
            bool pawnEnabled = CanUsePromptWorkspaceQuickPawnAction();

            DrawPromptWorkspaceQuickButton(
                factionRect,
                "RimChat_PromptWorkbench_QuickFaction",
                factionEnabled ? "RimChat_PromptWorkbench_QuickFactionTooltip" : "RimChat_PromptWorkbench_QuickFactionEmpty",
                factionEnabled,
                OpenPromptWorkspaceFactionTemplateMenu);
            DrawPromptWorkspaceQuickButton(
                pawnRect,
                "RimChat_PromptWorkbench_QuickPawn",
                pawnEnabled ? "RimChat_PromptWorkbench_QuickPawnTooltip" : "RimChat_PromptWorkbench_QuickNeedGame",
                pawnEnabled,
                OpenPromptWorkspaceQuickPawnMenu);
        }

        internal void DrawPromptWorkspaceQuickButton(Rect rect, string labelKey, string tooltipKey, bool enabled, Action onClick)
        {
            bool oldEnabled = GUI.enabled;
            Color oldColor = GUI.color;
            GUI.enabled = enabled;
            GUI.color = enabled ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            if (Widgets.ButtonText(rect, labelKey.Translate()))
            {
                onClick?.Invoke();
            }

            GUI.color = oldColor;
            GUI.enabled = oldEnabled;
            Pages.Tooltips.Register(rect, tooltipKey);
        }

        internal static bool CanUsePromptWorkspaceFactionTemplateQuickAction()
        {
            return Current.ProgramState == ProgramState.Playing &&
                   Current.Game != null &&
                   Find.FactionManager != null &&
                   GetPromptWorkspaceQuickFactions().Count > 0;
        }

        internal static bool CanUsePromptWorkspaceQuickPawnAction()
        {
            return Current.ProgramState == ProgramState.Playing &&
                   Current.Game != null &&
                   Find.FactionManager != null;
        }

        internal void OpenPromptWorkspaceFactionTemplateMenu()
        {
            // Force refresh cache when opening menu for up-to-date data
            _cachedQuickFactions = null;
            List<Faction> factions = GetPromptWorkspaceQuickFactions();
            if (factions.Count == 0)
            {
                Messages.Message("RimChat_PromptWorkbench_QuickFactionEmpty".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            List<FloatMenuOption> options = factions
                .Select(faction => new FloatMenuOption(
                    GetPromptWorkspaceQuickFactionLabel(faction),
                    () => HandlePromptWorkspaceQuickFactionSelected(faction)))
                .ToList();
            Find.WindowStack.Add(new FloatMenu(options));
        }

        internal static void HandlePromptWorkspaceQuickFactionSelected(Faction faction)
        {
            if (!TryResolvePromptWorkspaceQuickFactionDefName(faction, out string factionDefName))
            {
                Messages.Message("RimChat_ActionsHint_Reason_InvalidFaction".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            FactionPromptManager manager = FactionPromptManager.Instance;
            string displayName = faction.Name ?? faction.def.label ?? factionDefName;
            bool added = manager.TryAddTemplateForFaction(factionDefName, displayName, out string status);
            if (!added && !string.Equals(status, "existing", StringComparison.OrdinalIgnoreCase))
            {
                Messages.Message("RimChat_FactionTemplateAddFailed".Translate(displayName), MessageTypeDefOf.RejectInput, false);
                return;
            }

            FactionPromptConfig config = manager.GetConfig(factionDefName);
            if (config == null)
            {
                Messages.Message("RimChat_FactionTemplateAddFailed".Translate(displayName), MessageTypeDefOf.RejectInput, false);
                return;
            }

            Find.WindowStack.Add(new Dialog_FactionPromptEditor(config.Clone()));
        }

        internal static bool TryResolvePromptWorkspaceQuickFactionDefName(Faction faction, out string defName)
        {
            defName = faction?.def?.defName?.Trim() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(defName);
        }

        internal void OpenPromptWorkspaceQuickPawnMenu()
        {
            // Force refresh cache when opening menu for up-to-date data
            _cachedQuickPawns = null;
            List<Pawn> pawns = GetPromptWorkspaceQuickPawns();
            if (pawns.Count == 0)
            {
                Messages.Message("RimChat_PromptWorkbench_QuickPawnEmpty".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            List<FloatMenuOption> options = pawns
                .Select(pawn => new FloatMenuOption(
                    GetPromptWorkspaceQuickPawnLabel(pawn),
                    () => HandlePromptWorkspaceQuickPawnSelected(pawn)))
                .ToList();
            Find.WindowStack.Add(new FloatMenu(options));
        }

        internal void HandlePromptWorkspaceQuickPawnSelected(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }
            if (!UserDefinedPromptVariableService.RequiresQuickConflictResolution(Settings, QuickPromptTargetKind.Pawn))
            {
                Find.WindowStack.Add(new Dialog_QuickPromptVariableRuleEditor(Settings, pawn, QuickPromptConflictDecision.ReuseExisting));
                return;
            }

            ShowPromptWorkspaceQuickConflictMenu(
                QuickPromptTargetKind.Pawn,
                GetPromptWorkspaceQuickPawnLabel(pawn),
                () => Find.WindowStack.Add(new Dialog_QuickPromptVariableRuleEditor(Settings, pawn, QuickPromptConflictDecision.ReuseExisting)),
                () => Find.WindowStack.Add(new Dialog_QuickPromptVariableRuleEditor(Settings, pawn, QuickPromptConflictDecision.TakeOver)));
        }

        internal void ShowPromptWorkspaceQuickConflictMenu(
            QuickPromptTargetKind kind,
            string targetLabel,
            Action reuseAction,
            Action takeOverAction)
        {
            string path = UserDefinedPromptVariableService.BuildQuickPath(kind);
            List<FloatMenuOption> options = new List<FloatMenuOption>
            {
                new FloatMenuOption(
                    "RimChat_PromptWorkbench_QuickConflictReuse".Translate(path, targetLabel),
                    () => reuseAction?.Invoke()),
                new FloatMenuOption(
                    "RimChat_PromptWorkbench_QuickConflictTakeOver".Translate(path, targetLabel),
                    () => takeOverAction?.Invoke())
            };
            Find.WindowStack.Add(new FloatMenu(options));
        }

        /// <summary>
        /// Gets cached quick factions list. Refreshes every QuickFactionCacheTicks to eliminate per-frame LINQ.
        /// </summary>
        internal static List<Faction> GetPromptWorkspaceQuickFactions()
        {
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            if (_cachedQuickFactions == null ||
                currentTick - _lastQuickFactionCacheTick > QuickFactionCacheTicks)
            {
                _cachedQuickFactions = GetPromptWorkspaceQuickFactionsUncached();
                _lastQuickFactionCacheTick = currentTick;
            }
            return _cachedQuickFactions;
        }

        internal static List<Faction> GetPromptWorkspaceQuickFactionsUncached()
        {
            return Find.FactionManager?.AllFactionsListForReading?
                .Where(IsPromptWorkspaceQuickFactionCandidate)
                .GroupBy(faction => faction.def.defName, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(GetPromptWorkspaceQuickFactionLabel)
                .ToList() ?? new List<Faction>();
        }

        internal static bool IsPromptWorkspaceQuickFactionCandidate(Faction faction)
        {
            return faction != null &&
                   !faction.IsPlayer &&
                   !faction.defeated &&
                   faction.def != null &&
                   !string.IsNullOrWhiteSpace(faction.def.defName);
        }

        /// <summary>
        /// Gets cached quick pawns list. Refreshes every QuickPawnCacheTicks to eliminate per-frame LINQ.
        /// </summary>
        internal static List<Pawn> GetPromptWorkspaceQuickPawns()
        {
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            if (_cachedQuickPawns == null ||
                currentTick - _lastQuickPawnCacheTick > QuickPawnCacheTicks)
            {
                _cachedQuickPawns = GetPromptWorkspaceQuickPawnsUncached();
                _lastQuickPawnCacheTick = currentTick;
            }
            return _cachedQuickPawns;
        }

        internal static List<Pawn> GetPromptWorkspaceQuickPawnsUncached()
        {
            return PawnsFinder.AllMapsWorldAndTemporary_Alive
                .Where(IsPromptWorkspaceQuickPawnCandidate)
                .GroupBy(pawn => pawn.ThingID ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(GetPromptWorkspaceQuickPawnSortBucket)
                .ThenBy(GetPromptWorkspaceQuickPawnLabel)
                .ToList();
        }

        internal static bool IsPromptWorkspaceQuickPawnCandidate(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed || pawn.Faction != Faction.OfPlayer || pawn.RaceProps == null)
            {
                return false;
            }

            if (pawn.IsColonistPlayerControlled)
            {
                return true;
            }

            return pawn.RaceProps.Animal || pawn.RaceProps.IsMechanoid;
        }

        internal static int GetPromptWorkspaceQuickPawnSortBucket(Pawn pawn)
        {
            if (pawn?.IsColonistPlayerControlled == true)
            {
                return 0;
            }

            if (pawn?.RaceProps?.Animal == true)
            {
                return 1;
            }

            if (pawn?.RaceProps?.IsMechanoid == true)
            {
                return 2;
            }

            return 3;
        }

        internal void HandlePromptWorkspaceQuickPromptSaved(QuickPromptTargetKind kind, string targetLabel)
        {
            Pages.CustomVariables.InvalidatePromptVariableBrowserCache();
            Pages.VariableBrowser._rimTalkSelectedVariableName = UserDefinedPromptVariableService.BuildQuickPath(kind);
            if (kind == QuickPromptTargetKind.Pawn)
            {
                RelationsMod.Settings?.EnsurePawnPersonalityTokenForRpgChannelsSafe();
                TryEnsurePawnPersonalityTokenInCurrentChannel();
            }

            Pages.PromptWorkspaceBuffers.SelectPromptWorkspaceSection("character_persona");
            Pages.PromptWorkspaceBuffers.EnsurePromptWorkspaceSelection();
            Find.WindowStack.Add(new Dialog_MessageBox(
                "RimChat_PromptWorkbench_QuickSavedBody".Translate(
                    targetLabel,
                    UserDefinedPromptVariableService.BuildQuickToken(kind),
                    PromptSectionSchemaCatalog.GetMainChainSections()
                        .First(section => string.Equals(section.Id, "character_persona", StringComparison.OrdinalIgnoreCase))
                        .GetDisplayLabel()),
                "OK".Translate()));
            Messages.Message("RimChat_PromptWorkbench_QuickSavedToast".Translate(targetLabel), MessageTypeDefOf.TaskCompletion, false);
        }

        internal bool TryEnsurePawnPersonalityTokenInCurrentChannel()
        {
            string channel = Pages.PromptWorkspaceBuffers.EnsurePromptWorkspaceSelection();
            if (string.IsNullOrWhiteSpace(channel))
            {
                return false;
            }

            const string sectionId = "character_persona";
            if (!TryAppendPawnPersonalityTokenToSection(channel, sectionId, out string updated))
            {
                return false;
            }

            UpdatePromptWorkspacePersonaSectionBuffer(channel, sectionId, updated);
            return true;
        }

        internal bool TryAppendPawnPersonalityTokenToSection(string channel, string sectionId, out string updated)
        {
            if (!Pages.PromptPresetsUi.EnsurePromptWorkspaceEditablePresetForMutation("workspace.quick_action"))
            {
                updated = string.Empty;
                return false;
            }

            const string variableName = "pawn.personality";
            const string token = "{{ pawn.personality }}";
            string current = Settings.ResolvePromptSectionText(channel, sectionId) ?? string.Empty;
            if (RelationsRimTalkTemplateEditors.ContainsVariableToken(current, variableName))
            {
                updated = string.Empty;
                return false;
            }

            updated = string.IsNullOrWhiteSpace(current)
                ? token
                : current.TrimEnd() + "\n" + token;
            Settings.SetPromptSectionText(channel, sectionId, updated, persistToFiles: false);
            return true;
        }

        internal void UpdatePromptWorkspacePersonaSectionBuffer(string channel, string sectionId, string updated)
        {
            Pages.PromptWorkspace._promptWorkspaceBufferedChannel = channel;
            Pages.PromptWorkspace._promptWorkspaceBufferedNodeMode = false;
            Pages.PromptWorkspace._promptWorkspaceBufferedSectionId = sectionId;
            Pages.PromptWorkspace._promptWorkspaceBufferedNodeId = Pages.PromptWorkspace._promptWorkspaceSelectedNodeId ?? string.Empty;
            Pages.PromptWorkspace._promptWorkspaceEditorBuffer = updated;
        }

        internal static string GetPromptWorkspaceQuickFactionLabel(Faction faction)
        {
            if (faction == null)
            {
                return string.Empty;
            }

            string name = faction.Name ?? faction.def?.label ?? faction.def?.defName ?? string.Empty;
            string defName = faction.def?.defName ?? string.Empty;
            return string.IsNullOrWhiteSpace(defName)
                ? name
                : $"{name} ({defName})";
        }

        internal static string GetPromptWorkspaceQuickPawnLabel(Pawn pawn)
        {
            if (pawn == null)
            {
                return string.Empty;
            }

            string category = pawn.IsColonistPlayerControlled
                ? "RimChat_PromptWorkbench_QuickPawnTypeColonist".Translate().ToString()
                : pawn.RaceProps?.Animal == true
                    ? "RimChat_PromptWorkbench_QuickPawnTypeAnimal".Translate().ToString()
                    : "RimChat_PromptWorkbench_QuickPawnTypeMech".Translate().ToString();
            string name = UserDefinedPromptVariableRuleMatcher.ResolvePawnName(pawn);
            return $"{category} · {name}";
        }
    
}
