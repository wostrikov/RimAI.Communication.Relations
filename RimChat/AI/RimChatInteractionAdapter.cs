using RimWorld;
using Ustas.RimAI.Core.Relations;
using Verse;

namespace Ustas.RimAI.Communication.Relations.AI
{
    /// <summary>
    /// Thin Relations frontend hook. RimAI Host owns diplomacy policy and mutation.
    /// HostExecute is attached through IRelationsApplication.
    /// </summary>
    public static class RimChatInteractionAdapter
    {
        public static System.Func<object, object, bool, object> HostExecute { get; set; }

        public static ActionResult Execute(AIAction action, Faction faction, bool applyDialogueApiGoodwillCost = false)
        {
            if (action == null)
            {
                return ActionResult.Failure("Action is null");
            }

            if (HostExecute == null)
            {
                return ActionResult.Failure("RimAI diplomacy host is unavailable.");
            }

            object raw = HostExecute(action, faction, applyDialogueApiGoodwillCost);
            if (raw is ActionResult result)
            {
                return result;
            }

            if (raw is RelationsHostResult host)
            {
                return host.Success
                    ? ActionResult.Success(host.Message, host.Data)
                    : ActionResult.Failure(host.Message);
            }

            return ActionResult.Failure("RimAI diplomacy host returned an unexpected result.");
        }
    }
}
