using System;

[Serializable] // !!! Важно: атрибут [Serializable], чтобы Unity мог сериализовать этот класс в ScriptableObject-ах (в TestConfigurationData)
public class FixturePlacementDescriptor
{
    public FixtureData fixtureData;             // Ссылка на ассет оснастки (ScriptableObject)
    public FixtureZone placementZone;           // В какой зоне машины размещаем (из enum FixtureZone)
    public string placementPointName;          // Имя точки крепления *внутри зоны*, если нужно (например, "Вкладыш_Точка" в зоне GripperUpper) - Опционально

    public FixturePlacementDescriptor() // Пустой конструктор по умолчанию (необязательно, но полезно иметь)
    {
    }

    public FixturePlacementDescriptor(FixtureData data, FixtureZone zone, string pointName = null) // Конструктор с параметрами, чтобы удобнее создавать Descriptors в коде (например, в TestConfigurationData)
    {
        fixtureData = data;
        placementZone = zone;
        placementPointName = pointName;
    }
}