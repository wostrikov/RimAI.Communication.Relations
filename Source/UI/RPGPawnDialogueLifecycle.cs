using System;
using System.Collections.Generic;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Dialogue;
using RimWorld;
using Verse;

using DialoguePage = Ustas.RimAI.Communication.Relations.UI.Dialog_RPGPawnDialogue.DialoguePage;

namespace Ustas.RimAI.Communication.Relations.UI
{
    /// <summary>
    /// Responsibilities: request lease lifecycle, stale-response fail-fast handling, and stage-B envelope apply.
    /// Dependencies: RpgDialogueConversationController, DialogueContextValidator/Resolver, action execution pipeline.
    /// </summary>
        internal sealed class RPGPawnDialogueLifecycle : Dialog_RPGPawnDialogueCollaborator
    {
        internal RPGPawnDialogueLifecycle(Dialog_RPGPawnDialogue owner) : base(owner)
        {
        }


        public bool MatchesWindowLifecycleKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            return string.Equals(windowLifecycleKey, key.Trim(), StringComparison.Ordinal);
        }

        internal void CloseActiveRequestLease()
        {
            if (activeRequestLease == null)
            {
                return;
            }

            conversationController.CloseLease(activeRequestLease);
            activeRequestLease = null;
            activeRequestRuntimeContext = null;
        }

        internal void ReleaseActiveRequestLease()
        {
            if (activeRequestLease == null)
            {
                return;
            }

            activeRequestLease.Dispose();
            activeRequestLease = null;
            activeRequestRuntimeContext = null;
        }

        internal void PrepareEnvelopeForDisplay(DialogueResponseEnvelope envelope)
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
                Owner.EnsureRpgActionFallbacks(apiResponse);
                envelope.DialogueText = Owner.NormalizeEnvelopeVisibleDialogueForDisplay(envelope, "prepare_envelope");
                envelope.Actions = apiResponse.Actions ?? new List<LLMRpgApiResponse.ApiAction>();
                return;
            }

            envelope.DialogueText = Owner.NormalizeEnvelopeVisibleDialogueForDisplay(envelope, "prepare_envelope_no_api");
            envelope.Actions = new List<LLMRpgApiResponse.ApiAction>();
        }

        internal void TryApplyPendingEnvelope()
        {
            if (pendingResponseEnvelope == null)
            {
                return;
            }

            if (!RelationsMod.Settings.EnableRPGAPI || pendingResponseEnvelope.Actions == null || pendingResponseEnvelope.Actions.Count == 0)
            {
                pendingResponseEnvelope = null;
                Owner.ReleaseActiveRequestLease();
                return;
            }

            if (!conversationController.TryApplyResponseEnvelope(
                    activeRequestLease,
                    activeRequestRuntimeContext ?? runtimeContext,
                    pendingResponseEnvelope,
                    out string reason))
            {
                Owner.HandleDroppedResponse(reason);
                pendingResponseEnvelope = null;
                Owner.ReleaseActiveRequestLease();
                return;
            }

            var apiResponse = new LLMRpgApiResponse
            {
                DialogueContent = currentDialogueText,
                Actions = pendingResponseEnvelope.Actions
            };
            Owner.ApplyRPGAPIAndShowPopup(apiResponse);
            pendingResponseEnvelope = null;
            Owner.ReleaseActiveRequestLease();
        }

        internal void HandleDroppedResponse(string reason)
        {
            if (isWindowClosing)
            {
                return;
            }

            if (DialogueDropPolicy.ShouldSuppressUserFacingDrop(reason))
            {
                Log.Message($"[RimAI.Relations] Suppressed user-facing dropped RPG callback: reason={reason ?? "unknown"}");
                Owner.ReleaseActiveRequestLease();
                return;
            }

            string message = "RimChat_DialogueResponseDropped".Translate(reason ?? "unknown").ToString();
            aiResponseText = message;
            Owner.AddSystemFeedback(message, 4.5f);
            chatHistory.Add(new ChatMessageData { role = "system", content = message });
            dialogPages.Add(new DialoguePage { speakerName = "System", text = message });
            Owner.RecordSessionDialogueTurn("System", message, false);
            Owner.ReleaseActiveRequestLease();
        }
        }

}
