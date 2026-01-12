using System.Collections;
using UnityEngine;

public class Step_TransitionToState : IWorkflowStep
{
    // В будущем здесь можно передавать Enum и использовать Фабрику,
    // но пока сделаем жесткий переход в ReadyForSetup, так как это финал настройки.
    public IEnumerator Execute(WorkflowContext context)
    {
        Debug.Log("[Step_Transition] Переход в ReadyForSetupState.");
        context.CSM.TransitionToState(new ReadyForSetupState(context.CSM));
        yield break;
    }
}