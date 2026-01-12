using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Scenario/Steps/Set State")]
public class DataStep_SetState : DataStep
{
    [Tooltip("В какое состояние перевести CSM?")]
    public TestState TargetState;

    public override IScenarioLogic CreateLogic()
    {
        return new Logic_SetState(this);
    }
}

public class Logic_SetState : IScenarioLogic
{
    private readonly DataStep_SetState _data;

    public Logic_SetState(DataStep_SetState data)
    {
        _data = data;
    }

    public IEnumerator Execute(ScenarioExecutor executor)
    {
        Debug.Log($"[Step State] Переход в состояние: {_data.TargetState}");
        
        // Обращаемся к CSM и просим сменить состояние
        CentralizedStateManager.Instance.TransitionToStateByEnum(_data.TargetState);
        
        yield return null;
    }
}