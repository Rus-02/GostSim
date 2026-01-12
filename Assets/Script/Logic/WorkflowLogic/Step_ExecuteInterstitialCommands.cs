using System.Collections;
using UnityEngine;

public class Step_ExecuteInterstitialCommands : IWorkflowStep
{
    public IEnumerator Execute(WorkflowContext context)
    {
        var plan = context.GetData<FixtureChangePlan>(Step_CalculateFixturePlan.CTX_KEY_PLAN);
        
        if (plan != null && plan.InterstitialCommands != null && plan.InterstitialCommands.Count > 0)
        {
            Debug.Log("[Step_Interstitial] Выполнение промежуточных команд...");
            foreach (var cmd in plan.InterstitialCommands)
            {
                ToDoManager.Instance.HandleAction(cmd.Action, cmd.Args);
                // Небольшая задержка для надежности (опционально)
                yield return new WaitForSeconds(0.1f);
            }
        }
    }
}