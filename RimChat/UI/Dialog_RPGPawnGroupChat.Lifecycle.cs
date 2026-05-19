using RimChat.AI;
using RimChat.Core;
using RimChat.Dialogue;
using Verse;

namespace RimChat.UI
{
    public partial class Dialog_RPGPawnGroupChat
    {
        private void PrepareEnvelopeForDisplay(DialogueResponseEnvelope envelope)
        {
            if (envelope == null) return;

            if (RimChatMod.Settings.EnableRPGAPI && envelope.Actions != null && envelope.Actions.Count > 0)
            {
                var apiResponse = new LLMRpgApiResponse
                {
                    DialogueContent = envelope.DialogueText ?? string.Empty,
                    Actions = envelope.Actions
                };
                // Fallback actions handled by action policies
                envelope.Actions = apiResponse.Actions;
                return;
            }

            envelope.Actions = new System.Collections.Generic.List<LLMRpgApiResponse.ApiAction>();
        }

        private void HandleDroppedResponse(string reason)
        {
            if (isWindowClosing) return;
            string message = "RimChat_DialogueResponseDropped".Translate(reason ?? "unknown");
            aiResponseText = message;
            CloseActiveRequestLease();
        }
    }
}
