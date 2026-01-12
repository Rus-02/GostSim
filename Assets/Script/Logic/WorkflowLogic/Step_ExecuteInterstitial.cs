using System.Collections;
using UnityEngine;

public class Step_ExecuteInterstitial : IWorkflowStep
{
    public IEnumerator Execute(WorkflowContext context)
    {
        var plan = context.GetData<FixtureChangePlan>(Step_CalculateFixturePlan.CTX_KEY_PLAN);
        
        if (plan != null && plan.InterstitialCommands != null && plan.InterstitialCommands.Count > 0)
        {
            Debug.Log("[Step_ExecuteInterstitial] Выполнение промежуточных команд");
            foreach (var cmd in plan.InterstitialCommands)
            {
                ToDoManager.Instance.HandleAction(cmd.Action, cmd.Args);
            }
            // Даем системе мгновение на обработку
            yield return null; 
        }
    }
}