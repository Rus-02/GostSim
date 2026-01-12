using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Scenario/Steps/Jump (GOTO)")]
public class DataStep_Jump : DataStep
{
    public string TargetLabel;

    public override IScenarioLogic CreateLogic()
    {
        return new Logic_Jump(this);
    }
}

public class Logic_Jump : IScenarioLogic
{
    private readonly DataStep_Jump _data;

    public Logic_Jump(DataStep_Jump data)
    {
        _data = data;
    }

    public IEnumerator Execute(ScenarioExecutor executor)
    {
        // Вызываем прыжок и сразу выходим
        executor.TriggerJump(_data.TargetLabel);
        yield return null;
    }
}