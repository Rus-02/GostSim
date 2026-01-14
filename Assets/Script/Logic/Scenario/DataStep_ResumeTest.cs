using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Scenario/Steps/Action - Resume Test")]
public class DataStep_ResumeTest : DataStep
{
    public override IScenarioLogic CreateLogic()
    {
        return new Logic_ResumeTest();
    }
}

public class Logic_ResumeTest : IScenarioLogic
{
    public IEnumerator Execute(ScenarioExecutor executor)
    {
        Debug.Log("[Step Resume] Отправка команды Resume...");
        
        // Отправляем команду возобновления графику и симуляции
        ToDoManager.Instance.HandleAction(ActionType.ResumeGraphAndSimulation, null);
        
        yield return null;
    }
}