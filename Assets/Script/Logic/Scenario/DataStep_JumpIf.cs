using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Scenario/Steps/Jump IF")]
public class DataStep_JumpIf : DataStep
{
    [Tooltip("Условие для проверки")]
    public InputCondition Condition;

    [Tooltip("Куда прыгать, если условие ИСТИННО")]
    public string TargetLabel;

    [Tooltip("Если true - прыгаем, когда условие выполнено. Если false - когда НЕ выполнено.")]
    public bool JumpOnTrue = true;

    public override IScenarioLogic CreateLogic()
    {
        return new Logic_JumpIf(this);
    }
}

public class Logic_JumpIf : IScenarioLogic
{
    private readonly DataStep_JumpIf _data;

    public Logic_JumpIf(DataStep_JumpIf data)
    {
        _data = data;
    }

    public IEnumerator Execute(ScenarioExecutor executor)
    {
        bool met = _data.Condition.IsMet();
        
        // Если условие совпадает с требованием прыжка
        if (met == _data.JumpOnTrue)
        {
            Debug.Log($"[Step JumpIf] Условие '{_data.Condition.name}' = {met}. Прыгаем на '{_data.TargetLabel}'");
            executor.TriggerJump(_data.TargetLabel);
        }
        else
        {
            Debug.Log($"[Step JumpIf] Условие '{_data.Condition.name}' = {met}. Идем дальше.");
        }

        yield return null;
    }
}