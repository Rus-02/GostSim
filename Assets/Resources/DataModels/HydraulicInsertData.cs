using UnityEngine;

[CreateAssetMenu(fileName = "HydraulicInsertData", menuName = "Data Models/Fixtures/Hydraulic Inserts", order = 2)]
public class HydraulicInsertData : FixtureData, IClampRangeProvider
{
    public float minGripDimension; // Минимальный размер образца для захвата (диаметр или толщина)
    public float maxGripDimension; // Максимальный размер образца для захвата (диаметр или толщина)
    public float MinGripDimension => minGripDimension;
    public float MaxGripDimension => maxGripDimension;


    public override string GetFixtureType()
    {
        return "HydraulicInsert"; // Возвращаем тип как строку
    }
}