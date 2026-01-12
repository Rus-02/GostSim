using System.Collections;
using UnityEngine;

public class Step_VSM_Finalize : IWorkflowStep
{
    private readonly FictiveTestParameters _fictiveParams;

    public Step_VSM_Finalize(FictiveTestParameters fictiveParams)
    {
        _fictiveParams = fictiveParams;
    }

    public IEnumerator Execute(WorkflowContext context)
    {
        Debug.Log("[Step_VSM_Finalize] Очистка фиктивных параметров...");
        _fictiveParams?.ResetMonitor();
        yield return null;
    }
}