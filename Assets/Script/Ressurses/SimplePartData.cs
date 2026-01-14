using UnityEngine;

[CreateAssetMenu(fileName = "SimplePart_New", menuName = "Data Models/Fixtures/Simple Part", order = 100)]
public class SimplePartData : FixtureData
{
    // Этот класс пуст, потому что у простой детали нет никаких дополнительных свойств, кроме базовых из FixtureData.

    public override string GetFixtureType()
    {
        return "SimplePart";
    }
}