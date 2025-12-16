using UnityEngine;

[CreateAssetMenu(fileName = "ProportionalInsert_New", menuName = "Data Models/Fixtures/Proportional Insert", order = 11)]
public class ProportionalInsertData : FixtureData, IClampRangeProvider
{
    [Header("Параметры вкладыша")]
    
    [Tooltip("Минимальный диаметр/толщина образца, который может зажать этот вкладыш.")]
    public float minGripDimension;
    
    [Tooltip("Максимальный диаметр/толщина образца, который может зажать этот вкладыш.")]
    public float maxGripDimension;

    // Реализация интерфейса для системы выбора оснастки
    public float MinGripDimension => minGripDimension;
    public float MaxGripDimension => maxGripDimension;

    public override string GetFixtureType()
    {
        return "ProportionalInsert";
    }
}