using System.Collections;
using UnityEngine;

public enum InternalWorkflowType
{
    FixtureChange, // Смена оснастки (то, что мы рефакторили)
    AutoApproach,  // Автоподвод
    // Сюда можно добавлять новые процессы
}

[CreateAssetMenu(menuName = "Scenario/Steps/Run Internal Workflow")]
public class DataStep_RunWorkflow : DataStep
{
    public InternalWorkflowType WorkflowType;

    public override IScenarioLogic CreateLogic()
    {
        return new Logic_RunWorkflow(this);
    }
}

public class Logic_RunWorkflow : IScenarioLogic
{
    private readonly DataStep_RunWorkflow _data;

    public Logic_RunWorkflow(DataStep_RunWorkflow data)
    {
        _data = data;
    }

    public IEnumerator Execute(ScenarioExecutor executor)
    {
        Debug.Log($"[Step Workflow] Запуск процесса: {_data.WorkflowType}");
        var csm = CentralizedStateManager.Instance;

        switch (_data.WorkflowType)
        {
            case InternalWorkflowType.FixtureChange:
                // Запускаем Workflow в CSM
                csm.StartStandardFixtureChangeWorkflow();
                
                // Ждем, пока Runner в CSM освободится
                // (Мы предполагаем, что CSM.Runner - это свойство, которое мы добавили в Фазе 2.2)
                yield return new WaitUntil(() => !csm.Runner.IsRunning);
                break;

            case InternalWorkflowType.AutoApproach:
                // Пример на будущее
                // csm.StartAutoApproachSequence();
                // yield return new WaitUntil(() => ...);
                break;
        }

        Debug.Log($"[Step Workflow] Процесс {_data.WorkflowType} завершен.");
    }
}