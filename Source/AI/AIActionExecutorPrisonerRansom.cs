using Ustas.RimAI.Communication.Relations.DiplomacySystem;

namespace Ustas.RimAI.Communication.Relations.AI
{
    /// <summary>
    /// Dependencies: GameAIInterface prisoner-ransom API.
    /// Responsibility: execute pay_prisoner_ransom action in diplomacy dialogue pipeline.
    /// </summary>
        internal sealed class AIActionExecutorPrisonerRansom : AIActionExecutorCollaborator
    {
        internal AIActionExecutorPrisonerRansom(AIActionExecutor owner) : base(owner)
        {
        }


        internal ActionResult ExecutePayPrisonerRansom(AIAction action)
        {
            if (action?.Parameters == null)
            {
                return ActionResult.Failure("pay_prisoner_ransom requires parameters.");
            }

            GameAIInterface.APIResult result = gameInterface.PayPrisonerRansom(faction, action.Parameters);
            return result.Success
                ? ActionResult.Success(result.Message, result.Data)
                : ActionResult.Failure(result.Message);
        }
        }

}
