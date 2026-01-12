using UnityEngine;

/// <summary>
/// Базовый ассет для шага сценария.
/// Хранит настройки и умеет создавать своего "исполнителя" (Logic).
/// </summary>
public abstract class DataStep : ScriptableObject
{
    [TextArea(2, 3)]
    public string Description; // Для удобства в инспекторе (комментарий)

    /// <summary>
    /// Фабричный метод. Превращает данные в живой код.
    /// </summary>
    public abstract IScenarioLogic CreateLogic();
}