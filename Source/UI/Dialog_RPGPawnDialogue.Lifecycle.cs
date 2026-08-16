using System;
using System.Collections.Generic;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Dialogue;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Relations.UI
{
    /// <summary>
    /// Responsibilities: request lease lifecycle, stale-response fail-fast handling, and stage-B envelope apply.
    /// Dependencies: RpgDialogueConversationController, DialogueContextValidator/Resolver, action execution pipeline.
    /// </summary>
    public partial class Dialog_RPGPawnDialogue
    {
        public bool MatchesWindowLifecycleKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            return string.Equals(windowLifecycleKey, key.Trim(), StringComparison.Ordinal);
        }

        private void CloseActiveRequestLease()
        {
            if (activeRequestLease == null)
            {
                return;
            }

            conversationController.CloseLease(activeRequestLease);
            activeRequestLease = null;
            activeRequestRuntimeContext = null;
        }

        private void ReleaseActiveRequestLease()
        {
            if (activeRequestLease == null)
            {
                return;
            }

            activeRequestLease.Dispose();
            activeRequestLease = null;
            activeRequestRuntimeContext = null;
        }

        private void PrepareEnvelopeForDisplay(DialogueResponseEnvelope envelope)
        {
            if (envelope == null)
            {
                return;
            }

            if (RelationsMod.Settings.EnableRPGAPI)
            {
                var apiResponse = new LLMRpgApiResponse
                {
                    DialogueContent = envelope.DialogueText ?? string.Empty,
                    Actions = envelope.Actions ?? new List<LLMRpgApiResponse.ApiAction>()
                };
                EnsureRpgActionFallbacks(apiResponse);
                envelope.DialogueText = NormalizeEnvelopeVisibleDialogueForDisplay(envelope, "prepare_envelope");
                envelope.Actions = apiResponse.Actions ?? new List<LLMRpgApiResponse.ApiAction>();
                return;
            }

            envelope.DialogueText = NormalizeEnvelopeVisibleDialogueForDisplay(envelope, "prepare_envelope_no_api");
            envelope.Actions = new List<LLMRpgApiResponse.ApiAction>();
        }

        private void TryApplyPendingEnvelope()
        {
            if (pendingResponseEnvelope == null)
            {
                return;
            }

            if (!RelationsMod.Settings.EnableRPGAPI || pendingResponseEnvelope.Actions == null || pendingResponseEnvelope.Actions.Count == 0)
            {
                pendingResponseEnvelope = null;
                ReleaseActiveRequestLease();
                return;
            }

            if (!conversationController.TryApplyResponseEnvelope(
                    activeRequestLease,
                    activeRequestRuntimeContext ?? runtimeContext,
                    pendingResponseEnvelope,
                    out string reason))
            {
                HandleDroppedResponse(reason);
                pendingResponseEnvelope = null;
                ReleaseActiveRequestLease();
                return;
            }

            var apiResponse = new LLMRpgApiResponse
            {
                DialogueContent = currentDialogueText,
                Actions = pendingResponseEnvelope.Actions
            };
            ApplyRPGAPIAndShowPopup(apiResponse);
            pendingResponseEnvelope = null;
            ReleaseActiveRequestLease();
        }

        private void HandleDroppedResponse(string reason)
        {
            if (isWindowClosing)
            {
                return;
            }

            if (DialogueDropPolicy.ShouldSuppressUserFacingDrop(reason))
            {
                Log.Message($"[RimAI.Relations] Suppressed user-facing dropped RPG callback: reason={reason ?? "unknown"}");
                ReleaseActiveRequestLease();
                return;
            }

            string message = "RimChat_DialogueResponseDropped".Translate(reason ?? "unknown").ToString();
            aiResponseText = message;
            AddSystemFeedback(message, 4.5f);
            chatHistory.Add(new ChatMessageData { role = "system", content = message });
            dialogPages.Add(new DialoguePage { speakerName = "System", text = message });
            RecordSessionDialogueTurn("System", message, false);
            ReleaseActiveRequestLease();
        }
    }
}
