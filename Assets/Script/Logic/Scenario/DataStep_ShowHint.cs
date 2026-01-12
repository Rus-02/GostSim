using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Scenario/Steps/Show Hint")]
public class DataStep_ShowHint : DataStep
{
    [TextArea] public string Message;
    public float Duration = 3.0f;
    public bool WaitForCompletion = true; // Ждать ли таймер или идти дальше сразу

    public override IScenarioLogic CreateLogic()
    {
        return new Logic_ShowHint(this);
    }
}

public class Logic_ShowHint : IScenarioLogic
{
    private readonly DataStep_ShowHint _data;

    public Logic_ShowHint(DataStep_ShowHint data)
    {
        _data = data;
    }

    public IEnumerator Execute(ScenarioExecutor executor)
    {
        // Отправляем команду в UI
        if (ToDoManager.Instance != null)
        {
            var args = new ShowHintArgs(_data.Message, _data.Duration);
            ToDoManager.Instance.HandleAction(ActionType.ShowHintText, args);
        }

        Debug.Log($"[Step Hint] {_data.Message}");

        // Если нужно ждать, пока текст висит (например, в обучении)
        if (_data.WaitForCompletion && _data.Duration > 0)
        {
            yield return new WaitForSeconds(_data.Duration);
        }
        else
        {
            yield return null; // Один кадр
        }
    }
}