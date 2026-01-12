using UnityEngine;
using System.Collections;

[CreateAssetMenu(menuName = "Scenario/Steps/Label")]
public class DataStep_Label : DataStep
{
    public string LabelName;

    public override IScenarioLogic CreateLogic()
    {
        // Логика пустая, шаг выполняется мгновенно
        return new Logic_Label(); 
    }
}

public class Logic_Label : IScenarioLogic
{
    public IEnumerator Execute(ScenarioExecutor executor)
    {
        yield return null; 
    }
}
