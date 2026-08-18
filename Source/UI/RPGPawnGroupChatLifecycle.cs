using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Verse;

namespace Ustas.RimAI.Communication.Relations.UI
{
        internal sealed class RPGPawnGroupChatLifecycle : Dialog_RPGPawnGroupChatCollaborator
    {
        internal RPGPawnGroupChatLifecycle(Dialog_RPGPawnGroupChat owner) : base(owner)
        {
        }


        internal void PrepareEnvelopeForDisplay(DialogueResponseEnvelope envelope)
        {
            if (envelope == null) return;

            if (RelationsMod.Settings.EnableRPGAPI && envelope.Actions != null && envelope.Actions.Count > 0)
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

        internal void HandleDroppedResponse(string reason)
        {
            if (isWindowClosing) return;
            string message = "RimChat_DialogueResponseDropped".Translate(reason ?? "unknown");
            aiResponseText = message;
            Owner.CloseActiveRequestLease();
        }
        }

}
