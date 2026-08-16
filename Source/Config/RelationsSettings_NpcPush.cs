using Ustas.RimAI.Communication.Relations.NpcDialogue;
using Ustas.RimAI.Communication.Relations.PawnRpgPush;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Config
{
    /// <summary>/// Dependencies: RimWorld.Listing_Standard, RelationsSettings_AI UI pipeline.
 /// Responsibility: NPC proactive dialogue settings fields and UI rendering.
 ///</summary>
    public partial class RelationsSettings : ModSettings
    {
        public bool EnableNpcInitiatedDialogue = true;
        public bool EnablePawnRpgInitiatedDialogue = true;
        public NpcPushFrequencyMode NpcPushFrequencyMode = global::Ustas.RimAI.Communication.Relations.Config.NpcPushFrequencyMode.Low;
        public int NpcQueueMaxPerFaction = 3;
        public float NpcQueueExpireHours = 12f;
        public float NpcGlobalDeliveryCooldownHours = 3f;
        public int NpcGlobalMaxMessagesPerWindow = 1;
        public float NpcGlobalWindowHours = 12f;
        public int NpcFactionCooldownMinDays = 3;
        public int NpcFactionCooldownMaxDays = 7;
        public bool EnableBusyByDrafted = true;
        public bool EnableBusyByHostiles = true;
        public bool EnableBusyByClickRate = true;
        public bool EnableNpcPushThrottleDebugLog = false;
        public int NpcPushThrottleProfileVersion = 1;
        public int PawnRpgProtagonistCap = 20;
        public bool EnableColonistToColonistDialogue = true;
        public int ColonistPairMinOpinion = 10;
        public NpcPushFrequencyMode ColonistPairFrequencyMode = NpcPushFrequencyMode.Low;

        private void DrawNpcInitiatedDialogueSettings(Listing_Standard listing)
        {
            listing.CheckboxLabeled("RimChat_EnableDiplomacyInitiatedDialogue".Translate(), ref EnableNpcInitiatedDialogue);
            listing.CheckboxLabeled("RimChat_EnablePawnRpgInitiatedDialogue".Translate(), ref EnablePawnRpgInitiatedDialogue);
            if (!EnableNpcInitiatedDialogue && !EnablePawnRpgInitiatedDialogue)
            {
                return;
            }

            DrawNpcPushFrequencySelector(listing);
            listing.Label("RimChat_NpcQueueMaxPerFaction".Translate(NpcQueueMaxPerFaction));
            NpcQueueMaxPerFaction = Mathf.RoundToInt(listing.Slider(NpcQueueMaxPerFaction, 1f, 10f));
            listing.Label("RimChat_NpcQueueExpireHours".Translate(NpcQueueExpireHours.ToString("F1")));
            NpcQueueExpireHours = listing.Slider(NpcQueueExpireHours, 1f, 48f);
            listing.Label("RimChat_NpcGlobalDeliveryCooldownHours".Translate(NpcGlobalDeliveryCooldownHours.ToString("F1")));
            NpcGlobalDeliveryCooldownHours = listing.Slider(NpcGlobalDeliveryCooldownHours, 1f, 24f);
            listing.Label("RimChat_NpcGlobalMaxMessagesPerWindow".Translate(NpcGlobalMaxMessagesPerWindow, NpcGlobalWindowHours.ToString("F1")));
            NpcGlobalMaxMessagesPerWindow = Mathf.RoundToInt(listing.Slider(NpcGlobalMaxMessagesPerWindow, 1f, 10f));
            listing.Label("RimChat_NpcGlobalWindowHours".Translate(NpcGlobalWindowHours.ToString("F1")));
            NpcGlobalWindowHours = listing.Slider(NpcGlobalWindowHours, 6f, 72f);
            listing.Label("RimChat_NpcFactionCooldownMinDays".Translate(NpcFactionCooldownMinDays));
            NpcFactionCooldownMinDays = Mathf.RoundToInt(listing.Slider(NpcFactionCooldownMinDays, 1f, 15f));
            listing.Label("RimChat_NpcFactionCooldownMaxDays".Translate(NpcFactionCooldownMaxDays));
            NpcFactionCooldownMaxDays = Mathf.RoundToInt(listing.Slider(NpcFactionCooldownMaxDays, 1f, 15f));
            if (NpcFactionCooldownMaxDays < NpcFactionCooldownMinDays)
            {
                NpcFactionCooldownMaxDays = NpcFactionCooldownMinDays;
            }
            listing.CheckboxLabeled("RimChat_EnableBusyByDrafted".Translate(), ref EnableBusyByDrafted);
            listing.CheckboxLabeled("RimChat_EnableBusyByHostiles".Translate(), ref EnableBusyByHostiles);
            listing.CheckboxLabeled("RimChat_EnableBusyByClickRate".Translate(), ref EnableBusyByClickRate);
            listing.CheckboxLabeled("RimChat_EnableNpcPushThrottleDebugLog".Translate(), ref EnableNpcPushThrottleDebugLog);
            DrawPawnRpgProtagonistSettings(listing);
            if (EnablePawnRpgInitiatedDialogue)
            {
                DrawColonistPairSettings(listing);
            }
            DrawNegotiatorModeSettings(listing);
            DrawDebugForceTriggerButton(listing);
        }

        private void DrawPawnRpgProtagonistSettings(Listing_Standard listing)
        {
            var manager = Current.Game?.GetComponent<GameComponent_PawnRpgDialoguePushManager>();
            listing.Gap(6f);
            listing.Label("RimChat_PawnRpgProtagonistSettings".Translate());
            listing.Label("RimChat_PawnRpgProtagonistCap".Translate(PawnRpgProtagonistCap));
            int capValue = Mathf.RoundToInt(listing.Slider(PawnRpgProtagonistCap, 1f, 100f));
            PawnRpgProtagonistCap = capValue;
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

        private void DrawPawnRpgProtagonistActionButtons(Listing_Standard listing, GameComponent_PawnRpgDialoguePushManager manager)
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

        private void DrawPawnRpgProtagonistSummary(Listing_Standard listing, GameComponent_PawnRpgDialoguePushManager manager)
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

        private static string GetNpcPushPawnDisplayName(Pawn pawn)
        {
            return pawn?.Name?.ToStringShort ?? pawn?.LabelShort ?? "Unknown";
        }

        private void OpenAddPawnRpgProtagonistMenu(GameComponent_PawnRpgDialoguePushManager manager)
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

        private void TryAddPawnRpgProtagonist(GameComponent_PawnRpgDialoguePushManager manager, Pawn pawn)
        {
            if (manager.TryAddRpgProactiveProtagonist(pawn))
            {
                Messages.Message("RimChat_PawnRpgProtagonistAddSuccess".Translate(GetNpcPushPawnDisplayName(pawn)), MessageTypeDefOf.TaskCompletion, false);
                return;
            }

            Messages.Message("RimChat_PawnRpgProtagonistAddFailedCap".Translate(manager.GetRpgProactiveProtagonistCap()), MessageTypeDefOf.RejectInput, false);
        }

        private void OpenRemovePawnRpgProtagonistMenu(GameComponent_PawnRpgDialoguePushManager manager)
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

        private void DrawColonistPairSettings(Listing_Standard listing)
        {
            listing.Gap(6f);
            listing.Label("RimChat_ColonistPairFrequency".Translate());
            listing.CheckboxLabeled("RimChat_EnableColonistToColonistDialogue".Translate(), ref EnableColonistToColonistDialogue);
            if (!EnableColonistToColonistDialogue)
            {
                return;
            }

            DrawColonistPairFrequencySelector(listing);
            listing.Label("RimChat_ColonistPairMinOpinion".Translate(ColonistPairMinOpinion));
            ColonistPairMinOpinion = Mathf.RoundToInt(listing.Slider(ColonistPairMinOpinion, 0f, 60f));
        }

        private void DrawNegotiatorModeSettings(Listing_Standard listing)
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

            if (DiplomacyNegotiatorMode == NegotiatorSelectionMode.Designated)
            {
                listing.Gap(4f);
                DrawDesignatedNegotiatorSelector(listing);
            }

            if (openMenu) OpenDesignatedNegotiatorMenu();
        }

        private bool DrawNegotiatorModeButton(Rect rect, NegotiatorSelectionMode mode, string label)
        {
            bool isActive = DiplomacyNegotiatorMode == mode;
            if (Widgets.ButtonText(rect, label, drawBackground: true, doMouseoverSound: true, isActive ? new Color(0.2f, 0.6f, 0.2f) : new Color(0.3f, 0.3f, 0.3f)))
            {
                DiplomacyNegotiatorMode = mode;
                return true;
            }
            return false;
        }

        private void DrawDesignatedNegotiatorSelector(Listing_Standard listing)
        {
            string currentName = "RimChat_NegotiatorMode_None".Translate();
            if (DesignatedNegotiatorThingId > 0 && Current.ProgramState == ProgramState.Playing)
            {
                var pawns = PawnsFinder.AllMapsWorldAndTemporary_Alive;
                if (pawns != null)
                {
                    Pawn current = pawns.FirstOrDefault(p => p != null && p.thingIDNumber == DesignatedNegotiatorThingId);
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

        private void OpenDesignatedNegotiatorMenu()
        {
            if (Current.ProgramState != ProgramState.Playing) return;
            var allPawns = PawnsFinder.AllMapsWorldAndTemporary_Alive;
            if (allPawns == null) return;

            List<Pawn> candidates = allPawns
                .Where(pawn => pawn != null && pawn.Faction == Faction.OfPlayer && !pawn.Dead && !pawn.Destroyed && pawn.RaceProps?.Humanlike == true)
                .OrderBy(GetNpcPushPawnDisplayName)
                .ToList();

            var options = new List<FloatMenuOption>();
            options.Add(new FloatMenuOption("RimChat_NegotiatorMode_None".Translate(), () => DesignatedNegotiatorThingId = -1));
            foreach (Pawn pawn in candidates)
            {
                string label = GetNpcPushPawnDisplayName(pawn);
                options.Add(new FloatMenuOption(label, () => DesignatedNegotiatorThingId = pawn.thingIDNumber));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void DrawColonistPairFrequencySelector(Listing_Standard listing)
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

        private void DrawColonistPairFrequencyButton(Rect rect, NpcPushFrequencyMode mode, string label)
        {
            Color oldColor = GUI.color;
            if (ColonistPairFrequencyMode == mode)
            {
                GUI.color = new Color(0.35f, 0.55f, 0.85f, 0.9f);
            }

            if (Widgets.ButtonText(rect, label))
            {
                ColonistPairFrequencyMode = mode;
            }

            GUI.color = oldColor;
        }

        private void DrawDebugForceTriggerButton(Listing_Standard listing)
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

        private void DrawNpcPushFrequencySelector(Listing_Standard listing)
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

        private void DrawFrequencyButton(Rect rect, NpcPushFrequencyMode mode, string label)
        {
            Color oldColor = GUI.color;
            if (NpcPushFrequencyMode == mode)
            {
                GUI.color = new Color(0.35f, 0.55f, 0.85f, 0.9f);
            }

            if (Widgets.ButtonText(rect, label))
            {
                NpcPushFrequencyMode = mode;
            }

            GUI.color = oldColor;
        }

        private void ResetNpcInitiatedDialogueSettings()
        {
            EnableNpcInitiatedDialogue = true;
            EnablePawnRpgInitiatedDialogue = true;
            NpcPushFrequencyMode = global::Ustas.RimAI.Communication.Relations.Config.NpcPushFrequencyMode.Low;
            ProactiveMessageHardLimit = 0;
            NpcQueueMaxPerFaction = 3;
            NpcQueueExpireHours = 12f;
            NpcGlobalDeliveryCooldownHours = 3f;
            NpcGlobalMaxMessagesPerWindow = 1;
            NpcGlobalWindowHours = 12f;
            NpcFactionCooldownMinDays = 3;
            NpcFactionCooldownMaxDays = 7;
            EnableBusyByDrafted = true;
            EnableBusyByHostiles = true;
            EnableBusyByClickRate = true;
            EnableNpcPushThrottleDebugLog = false;
            NpcPushThrottleProfileVersion = 1;
            PawnRpgProtagonistCap = 20;
            EnableColonistToColonistDialogue = true;
            ColonistPairMinOpinion = 10;
            ColonistPairFrequencyMode = NpcPushFrequencyMode.Low;
        }
    }
}
