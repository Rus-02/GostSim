using UnityEngine;

/// <summary>
/// Базовый абстрактный класс конфигурации.
/// MachineController знает только о нем и через него создает логику.
/// </summary>
public abstract class MachineConfigBase : MonoBehaviour
{
    /// <summary>
    /// Фабричный метод: создает и настраивает логику, специфичную для этой конфигурации.
    /// </summary>
    public abstract IMachineLogic CreateLogic();
}
