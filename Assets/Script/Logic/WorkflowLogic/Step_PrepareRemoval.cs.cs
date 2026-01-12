using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Step_PrepareRemoval : IWorkflowStep
{
    public IEnumerator Execute(WorkflowContext context)
    {
        // 1. Берем план
        var plan = context.GetData<FixtureChangePlan>(Step_CalculateFixturePlan.CTX_KEY_PLAN);
        if (plan == null || plan.MainFixturesToRemove == null || plan.MainFixturesToRemove.Count == 0) 
            yield break;

        // 2. Спрашиваем у текущего хендлера команды подготовки
        var handler = FixtureController.Instance.GetActiveLogicHandler();
        if (handler == null) yield break;

        var prepCommands = handler.GetPreChangePreparationCommands(plan.MainFixturesToRemove);
        
        if (prepCommands != null && prepCommands.Count > 0)
        {
            Debug.Log("[Step_PrepareRemoval] Выполнение подготовки к снятию (разжатие и т.д.)");
            bool containsUnclamp = false;

            foreach (var cmd in prepCommands)
            {
                if (cmd.Action == ActionType.UnclampUpperGrip || cmd.Action == ActionType.UnclampLowerGrip)
                    containsUnclamp = true;

                ToDoManager.Instance.HandleAction(cmd.Action, cmd.Args);
            }

            // 3. Если было разжатие, нужно подождать, пока захваты реально разожмутся
            if (containsUnclamp)
            {
                // Ждем 1 секунду для надежности (так как анимация разжатия занимает время)
                yield return new WaitForSeconds(1.0f); 
            }
        }
    }
}