using Ustas.RimAI.Communication.Relations.NpcDialogue;
using Ustas.RimAI.Communication.Relations.PawnRpgPush;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.UI;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsNpcPushSettingsSection
{
    readonly RelationsSettingsPages Pages;

    internal RelationsNpcPushSettingsSection(RelationsSettingsPages pages)
    {
        Pages = pages;
    }

    RelationsSettings Settings => Pages.Settings;


        internal void DrawNpcInitiatedDialogueSettings(Listing_Standard listing)
        {
            listing.CheckboxLabeled("RimChat_EnableDiplomacyInitiatedDialogue".Translate(), ref Settings.EnableNpcInitiatedDialogue);
            listing.CheckboxLabeled("RimChat_EnablePawnRpgInitiatedDialogue".Translate(), ref Settings.EnablePawnRpgInitiatedDialogue);
            if (!Settings.EnableNpcInitiatedDialogue && !Settings.EnablePawnRpgInitiatedDialogue)
            {
                return;
            }

            DrawNpcPushFrequencySelector(listing);
            listing.Label("RimChat_NpcQueueMaxPerFaction".Translate(Settings.NpcQueueMaxPerFaction));
            Settings.NpcQueueMaxPerFaction = Mathf.RoundToInt(listing.Slider(Settings.NpcQueueMaxPerFaction, 1f, 10f));
            listing.Label("RimChat_NpcQueueExpireHours".Translate(Settings.NpcQueueExpireHours.ToString("F1")));
            Settings.NpcQueueExpireHours = listing.Slider(Settings.NpcQueueExpireHours, 1f, 48f);
            listing.Label("RimChat_NpcGlobalDeliveryCooldownHours".Translate(Settings.NpcGlobalDeliveryCooldownHours.ToString("F1")));
            Settings.NpcGlobalDeliveryCooldownHours = listing.Slider(Settings.NpcGlobalDeliveryCooldownHours, 1f, 24f);
            listing.Label("RimChat_NpcGlobalMaxMessagesPerWindow".Translate(Settings.NpcGlobalMaxMessagesPerWindow, Settings.NpcGlobalWindowHours.ToString("F1")));
            Settings.NpcGlobalMaxMessagesPerWindow = Mathf.RoundToInt(listing.Slider(Settings.NpcGlobalMaxMessagesPerWindow, 1f, 10f));
            listing.Label("RimChat_NpcGlobalWindowHours".Translate(Settings.NpcGlobalWindowHours.ToString("F1")));
            Settings.NpcGlobalWindowHours = listing.Slider(Settings.NpcGlobalWindowHours, 6f, 72f);
            listing.Label("RimChat_NpcFactionCooldownMinDays".Translate(Settings.NpcFactionCooldownMinDays));
            Settings.NpcFactionCooldownMinDays = Mathf.RoundToInt(listing.Slider(Settings.NpcFactionCooldownMinDays, 1f, 15f));
            listing.Label("RimChat_NpcFactionCooldownMaxDays".Translate(Settings.NpcFactionCooldownMaxDays));
            Settings.NpcFactionCooldownMaxDays = Mathf.RoundToInt(listing.Slider(Settings.NpcFactionCooldownMaxDays, 1f, 15f));
            if (Settings.NpcFactionCooldownMaxDays < Settings.NpcFactionCooldownMinDays)
            {
                Settings.NpcFactionCooldownMaxDays = Settings.NpcFactionCooldownMinDays;
            }
            listing.CheckboxLabeled("RimChat_EnableBusyByDrafted".Translate(), ref Settings.EnableBusyByDrafted);
            listing.CheckboxLabeled("RimChat_EnableBusyByHostiles".Translate(), ref Settings.EnableBusyByHostiles);
            listing.CheckboxLabeled("RimChat_EnableBusyByClickRate".Translate(), ref Settings.EnableBusyByClickRate);
            listing.CheckboxLabeled("RimChat_EnableNpcPushThrottleDebugLog".Translate(), ref Settings.EnableNpcPushThrottleDebugLog);
            DrawPawnRpgProtagonistSettings(listing);
            if (Settings.EnablePawnRpgInitiatedDialogue)
            {
                DrawColonistPairSettings(listing);
            }
            DrawNegotiatorModeSettings(listing);
            DrawDebugForceTriggerButton(listing);
        }

        internal void DrawPawnRpgProtagonistSettings(Listing_Standard listing)
        {
            var manager = Current.Game?.GetComponent<GameComponent_PawnRpgDialoguePushManager>();
            listing.Gap(6f);
            listing.Label("RimChat_PawnRpgProtagonistSettings".Translate());
            listing.Label("RimChat_PawnRpgProtagonistCap".Translate(Settings.PawnRpgProtagonistCap));
            int capValue = Mathf.RoundToInt(listing.Slider(Settings.PawnRpgProtagonistCap, 1f, 100f));
            Settings.PawnRpgProtagonistCap = capValue;
            manager?.SetRpgProactiveProtagonistCap(capValue);

            if (manager == null)
            {
                listing.Label("RimChat_PawnRpgProtagonistNeedGame".Translate());
                return;
            }

            listing.Label("RimChat_PawnRpgProtagonistCurrentCount".Translate(manager.GetConfiguredProtagonistCount(), manager.GetRpgProactiveProtagonistCap()));
            DrawPawnRpgProtagonistActionButtons(listing, manager);
            DrawPawnRpgProtagonistSummary(listing, manager);
        }

        internal void DrawPawnRpgProtagonistActionButtons(Listing_Standard listing, GameComponent_PawnRpgDialoguePushManager manager)
        {
            Rect row = listing.GetRect(30f);
            float width = (row.width - 12f) / 3f;
            Rect addRect = new Rect(row.x, row.y, width, row.height);
            Rect removeRect = new Rect(row.x + width + 6f, row.y, width, row.height);
            Rect clearRect = new Rect(row.x + (width + 6f) * 2f, row.y, width, row.height);

            if (Widgets.ButtonText(addRect, "RimChat_PawnRpgProtagonistAdd".Translate()))
            {
                OpenAddPawnRpgProtagonistMenu(manager);
            }

            if (Widgets.ButtonText(removeRect, "RimChat_PawnRpgProtagonistRemove".Translate()))
            {
                OpenRemovePawnRpgProtagonistMenu(manager);
            }

            if (Widgets.ButtonText(clearRect, "RimChat_PawnRpgProtagonistClear".Translate()))
            {
                manager.ClearRpgProactiveProtagonists();
                Messages.Message("RimChat_PawnRpgProtagonistCleared".Translate(), MessageTypeDefOf.NeutralEvent, false);
            }
        }

        internal void DrawPawnRpgProtagonistSummary(Listing_Standard listing, GameComponent_PawnRpgDialoguePushManager manager)
        {
            List<Pawn> protagonists = manager.GetRpgProactiveProtagonists();
            if (protagonists.Count == 0)
            {
                listing.Label("RimChat_PawnRpgProtagonistEmpty".Translate());
                return;
            }

            string names = string.Join(", ", protagonists.Select(GetNpcPushPawnDisplayName).Where(name => !string.IsNullOrWhiteSpace(name)));
            listing.Label("RimChat_PawnRpgProtagonistMembers".Translate(names));
        }

        internal static string GetNpcPushPawnDisplayName(Pawn pawn)
        {
            return pawn?.Name?.ToStringShort ?? pawn?.LabelShort ?? "Unknown";
        }

        internal void OpenAddPawnRpgProtagonistMenu(GameComponent_PawnRpgDialoguePushManager manager)
        {
            List<Pawn> candidates = PawnsFinder.AllMapsWorldAndTemporary_Alive
                .Where(pawn => pawn != null && pawn.Faction == Faction.OfPlayer && !pawn.Dead && !pawn.Destroyed)
                .OrderBy(GetNpcPushPawnDisplayName)
                .ToList();
            if (candidates.Count == 0)
            {
                Messages.Message("RimChat_PawnRpgProtagonistNoCandidates".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            var options = new List<FloatMenuOption>();
            foreach (Pawn pawn in candidates)
            {
                string label = GetNpcPushPawnDisplayName(pawn);
                options.Add(new FloatMenuOption(label, () => TryAddPawnRpgProtagonist(manager, pawn)));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        internal void TryAddPawnRpgProtagonist(GameComponent_PawnRpgDialoguePushManager manager, Pawn pawn)
        {
            if (manager.TryAddRpgProactiveProtagonist(pawn))
            {
                Messages.Message("RimChat_PawnRpgProtagonistAddSuccess".Translate(GetNpcPushPawnDisplayName(pawn)), MessageTypeDefOf.TaskCompletion, false);
                return;
            }

            Messages.Message("RimChat_PawnRpgProtagonistAddFailedCap".Translate(manager.GetRpgProactiveProtagonistCap()), MessageTypeDefOf.RejectInput, false);
        }

        internal void OpenRemovePawnRpgProtagonistMenu(GameComponent_PawnRpgDialoguePushManager manager)
        {
            List<Pawn> configured = manager.GetRpgProactiveProtagonists();
            if (configured.Count == 0)
            {
                Messages.Message("RimChat_PawnRpgProtagonistEmpty".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            var options = configured
                .OrderBy(GetNpcPushPawnDisplayName)
                .Select(pawn => new FloatMenuOption(GetNpcPushPawnDisplayName(pawn), () =>
                {
                    if (manager.RemoveRpgProactiveProtagonist(pawn))
                    {
                        Messages.Message("RimChat_PawnRpgProtagonistRemoved".Translate(GetNpcPushPawnDisplayName(pawn)), MessageTypeDefOf.NeutralEvent, false);
                    }
                }))
                .ToList();
            Find.WindowStack.Add(new FloatMenu(options));
        }

        internal void DrawColonistPairSettings(Listing_Standard listing)
        {
            listing.Gap(6f);
            listing.Label("RimChat_ColonistPairFrequency".Translate());
            listing.CheckboxLabeled("RimChat_EnableColonistToColonistDialogue".Translate(), ref Settings.EnableColonistToColonistDialogue);
            if (!Settings.EnableColonistToColonistDialogue)
            {
                return;
            }

            DrawColonistPairFrequencySelector(listing);
            listing.Label("RimChat_ColonistPairMinOpinion".Translate(Settings.ColonistPairMinOpinion));
            Settings.ColonistPairMinOpinion = Mathf.RoundToInt(listing.Slider(Settings.ColonistPairMinOpinion, 0f, 60f));
        }

        internal void DrawNegotiatorModeSettings(Listing_Standard listing)
        {
            listing.Gap(6f);
            listing.Label("RimChat_NegotiatorMode".Translate());
            Rect rowRect = listing.GetRect(30f);
            float buttonWidth = (rowRect.width - 30f) / 4f;
            bool openMenu = false;
            if (DrawNegotiatorModeButton(new Rect(rowRect.x, rowRect.y, buttonWidth, 30f), NegotiatorSelectionMode.HighestSocial, "RimChat_NegotiatorMode_HighestSocial".Translate()))
                Messages.Message("RimChat_NegotiatorMode_HighestSocial".Translate(), MessageTypeDefOf.TaskCompletion, false);
            if (DrawNegotiatorModeButton(new Rect(rowRect.x + (buttonWidth + 10f), rowRect.y, buttonWidth, 30f), NegotiatorSelectionMode.ProtagonistList, "RimChat_NegotiatorMode_ProtagonistList".Translate()))
                Messages.Message("RimChat_NegotiatorMode_ProtagonistList".Translate(), MessageTypeDefOf.TaskCompletion, false);
            if (DrawNegotiatorModeButton(new Rect(rowRect.x + (buttonWidth + 10f) * 2f, rowRect.y, buttonWidth, 30f), NegotiatorSelectionMode.LastUsed, "RimChat_NegotiatorMode_LastUsed".Translate()))
                Messages.Message("RimChat_NegotiatorMode_LastUsed".Translate(), MessageTypeDefOf.TaskCompletion, false);
            if (DrawNegotiatorModeButton(new Rect(rowRect.x + (buttonWidth + 10f) * 3f, rowRect.y, buttonWidth, 30f), NegotiatorSelectionMode.Designated, "RimChat_NegotiatorMode_Designated".Translate()))
            {
                Messages.Message("RimChat_NegotiatorMode_Designated".Translate(), MessageTypeDefOf.TaskCompletion, false);
                openMenu = true;
            }

            if (Settings.DiplomacyNegotiatorMode == NegotiatorSelectionMode.Designated)
            {
                listing.Gap(4f);
                DrawDesignatedNegotiatorSelector(listing);
            }

            if (openMenu) OpenDesignatedNegotiatorMenu();
        }

        internal bool DrawNegotiatorModeButton(Rect rect, NegotiatorSelectionMode mode, string label)
        {
            bool isActive = Settings.DiplomacyNegotiatorMode == mode;
            if (Widgets.ButtonText(rect, label, drawBackground: true, doMouseoverSound: true, isActive ? new Color(0.2f, 0.6f, 0.2f) : new Color(0.3f, 0.3f, 0.3f)))
            {
                Settings.DiplomacyNegotiatorMode = mode;
                return true;
            }
            return false;
        }

        internal void DrawDesignatedNegotiatorSelector(Listing_Standard listing)
        {
            string currentName = "RimChat_NegotiatorMode_None".Translate();
            if (Settings.DesignatedNegotiatorThingId > 0 && Current.ProgramState == ProgramState.Playing)
            {
                var pawns = PawnsFinder.AllMapsWorldAndTemporary_Alive;
                if (pawns != null)
                {
                    Pawn current = pawns.FirstOrDefault(p => p != null && p.thingIDNumber == Settings.DesignatedNegotiatorThingId);
                    if (current != null)
                    {
                        currentName = GetNpcPushPawnDisplayName(current);
                    }
                }
            }

            listing.Label("RimChat_NegotiatorMode_DesignatedPawn".Translate(currentName));
            if (listing.ButtonText("RimChat_NegotiatorMode_SelectPawn".Translate()))
            {
                OpenDesignatedNegotiatorMenu();
            }
        }

        internal void OpenDesignatedNegotiatorMenu()
        {
            if (Current.ProgramState != ProgramState.Playing) return;
            var allPawns = PawnsFinder.AllMapsWorldAndTemporary_Alive;
            if (allPawns == null) return;

            List<Pawn> candidates = allPawns
                .Where(pawn => pawn != null && pawn.Faction == Faction.OfPlayer && !pawn.Dead && !pawn.Destroyed && pawn.RaceProps?.Humanlike == true)
                .OrderBy(GetNpcPushPawnDisplayName)
                .ToList();

            var options = new List<FloatMenuOption>();
            options.Add(new FloatMenuOption("RimChat_NegotiatorMode_None".Translate(), () => Settings.DesignatedNegotiatorThingId = -1));
            foreach (Pawn pawn in candidates)
            {
                string label = GetNpcPushPawnDisplayName(pawn);
                options.Add(new FloatMenuOption(label, () => Settings.DesignatedNegotiatorThingId = pawn.thingIDNumber));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        internal void DrawColonistPairFrequencySelector(Listing_Standard listing)
        {
            Rect rowRect = listing.GetRect(30f);
            float buttonWidth = (rowRect.width - 20f) / 3f;
            DrawColonistPairFrequencyButton(
                new Rect(rowRect.x, rowRect.y, buttonWidth, 30f),
                NpcPushFrequencyMode.Low,
                "RimChat_ColonistPairFrequencyLow".Translate());
            DrawColonistPairFrequencyButton(
                new Rect(rowRect.x + buttonWidth + 10f, rowRect.y, buttonWidth, 30f),
                NpcPushFrequencyMode.Medium,
                "RimChat_ColonistPairFrequencyMedium".Translate());
            DrawColonistPairFrequencyButton(
                new Rect(rowRect.x + (buttonWidth + 10f) * 2f, rowRect.y, buttonWidth, 30f),
                NpcPushFrequencyMode.High,
                "RimChat_ColonistPairFrequencyHigh".Translate());
        }

        internal void DrawColonistPairFrequencyButton(Rect rect, NpcPushFrequencyMode mode, string label)
        {
            Color oldColor = GUI.color;
            if (Settings.ColonistPairFrequencyMode == mode)
            {
                GUI.color = new Color(0.35f, 0.55f, 0.85f, 0.9f);
            }

            if (Widgets.ButtonText(rect, label))
            {
                Settings.ColonistPairFrequencyMode = mode;
            }

            GUI.color = oldColor;
        }

        internal void DrawDebugForceTriggerButton(Listing_Standard listing)
        {
            listing.Gap(4f);
            Rect buttonRect = listing.GetRect(30f);
            float leftWidth = (buttonRect.width - 8f) * 0.5f;
            Rect oldButtonRect = new Rect(buttonRect.x, buttonRect.y, leftWidth, buttonRect.height);
            Rect newButtonRect = new Rect(buttonRect.x + leftWidth + 8f, buttonRect.y, leftWidth, buttonRect.height);

            if (Widgets.ButtonText(oldButtonRect, "RimChat_NpcPush_DebugForceTrigger".Translate()))
            {
                bool ok = GameComponent_NpcDialoguePushManager.Instance?.DebugForceRandomProactiveDialogue() == true;
                MessageTypeDef messageType = ok ? MessageTypeDefOf.TaskCompletion : MessageTypeDefOf.RejectInput;
                string key = ok
                    ? "RimChat_NpcPush_DebugTriggerSuccess"
                    : "RimChat_NpcPush_DebugTriggerFailed";
                Messages.Message(key.Translate(), messageType, false);
            }

            if (Widgets.ButtonText(newButtonRect, "RimChat_PawnRpgPush_DebugForceTrigger".Translate()))
            {
                bool ok = GameComponent_PawnRpgDialoguePushManager.Instance?.DebugForcePawnRpgProactiveDialogue() == true;
                MessageTypeDef messageType = ok ? MessageTypeDefOf.TaskCompletion : MessageTypeDefOf.RejectInput;
                string key = ok
                    ? "RimChat_PawnRpgPush_DebugTriggerSuccess"
                    : "RimChat_PawnRpgPush_DebugTriggerFailed";
                Messages.Message(key.Translate(), messageType, false);
            }
        }

        internal void DrawNpcPushFrequencySelector(Listing_Standard listing)
        {
            listing.Label("RimChat_NpcPushFrequency".Translate());
            Rect rowRect = listing.GetRect(30f);
            float buttonWidth = (rowRect.width - 20f) / 3f;
            DrawFrequencyButton(
                new Rect(rowRect.x, rowRect.y, buttonWidth, 30f),
                global::Ustas.RimAI.Communication.Relations.Config.NpcPushFrequencyMode.Low,
                "RimChat_NpcPushFrequencyLow".Translate());
            DrawFrequencyButton(
                new Rect(rowRect.x + buttonWidth + 10f, rowRect.y, buttonWidth, 30f),
                global::Ustas.RimAI.Communication.Relations.Config.NpcPushFrequencyMode.Medium,
                "RimChat_NpcPushFrequencyMedium".Translate());
            DrawFrequencyButton(
                new Rect(rowRect.x + (buttonWidth + 10f) * 2f, rowRect.y, buttonWidth, 30f),
                global::Ustas.RimAI.Communication.Relations.Config.NpcPushFrequencyMode.High,
                "RimChat_NpcPushFrequencyHigh".Translate());
        }

        internal void DrawFrequencyButton(Rect rect, NpcPushFrequencyMode mode, string label)
        {
            Color oldColor = GUI.color;
            if (Settings.NpcPushFrequencyMode == mode)
            {
                GUI.color = new Color(0.35f, 0.55f, 0.85f, 0.9f);
            }

            if (Widgets.ButtonText(rect, label))
            {
                Settings.NpcPushFrequencyMode = mode;
            }

            GUI.color = oldColor;
        }

        internal void ResetNpcInitiatedDialogueSettings()
        {
            Settings.EnableNpcInitiatedDialogue = true;
            Settings.EnablePawnRpgInitiatedDialogue = true;
            Settings.NpcPushFrequencyMode = global::Ustas.RimAI.Communication.Relations.Config.NpcPushFrequencyMode.Low;
            Settings.ProactiveMessageHardLimit = 0;
            Settings.NpcQueueMaxPerFaction = 3;
            Settings.NpcQueueExpireHours = 12f;
            Settings.NpcGlobalDeliveryCooldownHours = 3f;
            Settings.NpcGlobalMaxMessagesPerWindow = 1;
            Settings.NpcGlobalWindowHours = 12f;
            Settings.NpcFactionCooldownMinDays = 3;
            Settings.NpcFactionCooldownMaxDays = 7;
            Settings.EnableBusyByDrafted = true;
            Settings.EnableBusyByHostiles = true;
            Settings.EnableBusyByClickRate = true;
            Settings.EnableNpcPushThrottleDebugLog = false;
            Settings.NpcPushThrottleProfileVersion = 1;
            Settings.PawnRpgProtagonistCap = 20;
            Settings.EnableColonistToColonistDialogue = true;
            Settings.ColonistPairMinOpinion = 10;
            Settings.ColonistPairFrequencyMode = NpcPushFrequencyMode.Low;
        }
    
}
