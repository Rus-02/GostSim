using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Scenario/Steps/Wait For Condition")]
public class DataStep_WaitCondition : DataStep
{
    [Tooltip("Ждать, пока это условие не станет истинным")]
    public InputCondition Condition;

    public override IScenarioLogic CreateLogic()
    {
        return new Logic_WaitCondition(this);
    }
}

public class Logic_WaitCondition : IScenarioLogic
{
    private readonly DataStep_WaitCondition _data;

    public Logic_WaitCondition(DataStep_WaitCondition data)
    {
        _data = data;
    }

    public IEnumerator Execute(ScenarioExecutor executor)
    {
        // Ждем, пока условие не выполнится
        while (!_data.Condition.IsMet())
        {
            // Если нажали Стоп/Аварию — выходим, чтобы сработал Interrupt
            if (executor.IsInterruptPending) yield break;
            yield return null;
        }
    }
}