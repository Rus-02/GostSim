using UnityEngine;

[CreateAssetMenu(fileName = "CompressionPlateData", menuName = "Data Models/Fixtures/Compression Plates", order = 1)]
public class CompressionPlateData : FixtureData, IClampRangeProvider
{
    [Header("Plate Specific Properties")]
    [Tooltip("Размеры рабочей поверхности плиты (X, Z) в мм.")]
    public Vector2 plateDimensions; // Размеры рабочей поверхности плиты

    public float minSampleHeight = 1f;
    public float maxSampleHeight = 1000f;

    public float MinGripDimension => minSampleHeight;
    public float MaxGripDimension => maxSampleHeight;

    public override string GetFixtureType()
    {
        return "CompressionPlate"; // Возвращаем тип как строку (оставляем)
    }
}