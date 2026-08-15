using RimWorld;
using Verse;

namespace RimChat.AI
{
    /// <summary>
    /// Thin RimChat frontend hook. RimAI owns diplomacy policy and mutation.
    /// HostExecute is registered by RimAI.RimWorld at boot.
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

            return ActionResult.Failure("RimAI diplomacy host returned an unexpected result.");
        }
    }
}
