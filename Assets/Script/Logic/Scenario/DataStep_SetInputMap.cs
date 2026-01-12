using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Scenario/Steps/Set Input Map")]
public class DataStep_SetInputMap : DataStep
{
    [Tooltip("Новая карта правил. Если пусто — сброс на дефолт (всё разрешено).")]
    public MapInput TargetMap;

    public override IScenarioLogic CreateLogic()
    {
        return new Logic_SetInputMap(this);
    }
}

public class Logic_SetInputMap : IScenarioLogic
{
    private readonly DataStep_SetInputMap _data;

    public Logic_SetInputMap(DataStep_SetInputMap data)
    {
        _data = data;
    }

    public IEnumerator Execute(ScenarioExecutor executor)
    {
        // Переключаем карту в Исполнителе
        executor.SetInputMap(_data.TargetMap);
        
        Debug.Log($"[Step Input] Карта переключена на: {(_data.TargetMap != null ? _data.TargetMap.name : "Default")}");
        
        yield return null;
    }
}