using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewScenario", menuName = "Scenario/Scenario Data")]
public class ScenarioData : ScriptableObject
{
    [Header("Scenario Config")]
    public string ScenarioName;
    [TextArea] public string Description;

    [Header("Behavior Settings")]
    [Tooltip("Если true, сценарий может игнорировать ActionPolicy (режим Экзамена/Поломки).")]
    public bool AllowUnsafeActions = false;

    [Header("Sequence")]
    public List<DataStep> Steps = new List<DataStep>();

    [Header("Global Interrupts")]
    [Tooltip("Если произойдет это событие -> Прыгнуть на указанную метку.")]
    public List<ScenarioInterrupt> Interrupts = new List<ScenarioInterrupt>();
}

[System.Serializable]
public struct ScenarioInterrupt
{
    public EventType TriggerEvent;
    public string TargetLabel; // Имя метки, куда прыгнуть
}