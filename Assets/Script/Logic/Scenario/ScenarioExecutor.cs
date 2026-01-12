using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScenarioExecutor : MonoBehaviour
{
    // --- Робустный Синглтон ---
    private static ScenarioExecutor _instance;
    public static ScenarioExecutor Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<ScenarioExecutor>();
                if (_instance == null)
                {
                    GameObject singletonObject = new GameObject("ScenarioExecutor");
                    _instance = singletonObject.AddComponent<ScenarioExecutor>();
                    Debug.Log("[ScenarioExecutor] Экземпляр создан автоматически.");
                }
            }
            return _instance;
        }
    }
    // ----------------------------------------

    [Header("Runtime State")]
    [SerializeField] private ScenarioData _activeScenario; 
    [SerializeField] private MapInput _currentMap;         

    public ScenarioData CurrentScenario => _activeScenario; 

    public bool BypassSafety => _activeScenario != null && _activeScenario.AllowUnsafeActions;
    
    // Свойство для проверки прерывания внутри шагов (например, WaitForInput)
    public bool IsInterruptPending => _interruptTriggered;

    private Coroutine _executionRoutine;

    private bool _interruptTriggered = false;
    private string _interruptTargetLabel = "";

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    // =========================================================================
    // API ЗАПУСКА
    // =========================================================================

    public void StartScenario(ScenarioData scenario)
    {
        StopScenario(); 

        if (scenario == null)
        {
            Debug.LogError("[ScenarioExecutor] Попытка запустить null сценарий.");
            return;
        }

        _activeScenario = scenario;
        _currentMap = null; // Сброс на дефолт
        
        Debug.Log($"[ScenarioExecutor] Запуск сценария: {scenario.ScenarioName}");

        _executionRoutine = StartCoroutine(ExecuteSequence());
    }

    public void StopScenario()
    {
        if (_executionRoutine != null)
        {
            StopCoroutine(_executionRoutine);
            _executionRoutine = null;
        }

        _activeScenario = null;
        _currentMap = null;
        _interruptTriggered = false; // Сбрасываем флаг прерывания
        
        Debug.Log("[ScenarioExecutor] Сценарий остановлен.");
    }

    // =========================================================================
    // INPUT SYSTEM INTERFACE
    // =========================================================================

    public void SetInputMap(MapInput newMap)
    {
        _currentMap = newMap;
        if (_currentMap != null) _currentMap.Initialize();
        Debug.Log($"[ScenarioExecutor] Применена карта ввода: {(newMap != null ? newMap.name : "Default")}");
    }

    public string CheckInputBlock(string buttonId)
    {
        if (_currentMap == null) return null;

        RuleInput rule = _currentMap.GetRule(buttonId);

        if (rule != null)
        {
            string error = rule.CheckBlock();
            if (error != null) return error; 

            if (rule.Type == RuleType.ForceAllow) return null;
        }
        else
        {
            if (_currentMap.DefaultBehavior == RuleType.ForceBlock)
            {
                return _currentMap.DefaultBlockHint;
            }
        }

        return null; 
    }

    private void OnGlobalEvent(EventType type)
    {
        if (_activeScenario == null) return;

        foreach (var rule in _activeScenario.Interrupts)
        {
            if (rule.TriggerEvent == type)
            {
                Debug.LogWarning($"[ScenarioExecutor] ПРЕРЫВАНИЕ! Событие {type} -> Прыжок на '{rule.TargetLabel}'");
                
                _interruptTriggered = true;
                _interruptTargetLabel = rule.TargetLabel;
                break;
            }
        }
    }

    // =========================================================================
    // ГЛАВНЫЙ ЦИКЛ
    // =========================================================================

    private IEnumerator ExecuteSequence()
    {
        var activeListeners = new Dictionary<EventType, Action<EventArgs>>();

        if (_activeScenario.Interrupts != null)
        {
            foreach (var rule in _activeScenario.Interrupts)
            {
                if (!activeListeners.ContainsKey(rule.TriggerEvent))
                {
                    Action<EventArgs> listener = (args) => OnGlobalEvent(rule.TriggerEvent);
                    activeListeners.Add(rule.TriggerEvent, listener);
                    EventManager.Instance.Subscribe(rule.TriggerEvent, this, listener);
                }
            }
        }

        for (int i = 0; i < _activeScenario.Steps.Count; i++)
        {
            var stepData = _activeScenario.Steps[i];
            if (stepData == null) continue;

            if (CheckAndPerformJump(ref i)) continue;

            IScenarioLogic logic = stepData.CreateLogic();
            if (logic != null)
            {
                var stepRoutine = StartCoroutine(logic.Execute(this));
                
                // Ждем завершения шага ИЛИ прерывания
                yield return stepRoutine;
            }

            if (CheckAndPerformJump(ref i)) continue;
        }

        // Отписка от прерываний
        foreach (var kvp in activeListeners)
        {
            EventManager.Instance.Unsubscribe(kvp.Key, this, kvp.Value);
        }

        Debug.Log($"[ScenarioExecutor] Сценарий '{_activeScenario.ScenarioName}' завершен успешно.");
        StopScenario();
    }

    private bool CheckAndPerformJump(ref int currentIndex)
    {
        if (_interruptTriggered)
        {
            _interruptTriggered = false;
            int targetIndex = FindLabelIndex(_interruptTargetLabel);
            
            if (targetIndex != -1)
            {
                Debug.Log($"[ScenarioExecutor] Выполнен прыжок к метке: {_interruptTargetLabel} (шаг {targetIndex})");
                currentIndex = targetIndex - 1; // -1, т.к. цикл сделает i++
                return true; 
            }
            else
            {
                Debug.LogError($"[ScenarioExecutor] Метка '{_interruptTargetLabel}' не найдена в сценарии!");
            }
        }
        return false;
    }

    private int FindLabelIndex(string labelName)
    {
        for (int i = 0; i < _activeScenario.Steps.Count; i++)
        {
            // Проверяем, является ли шаг меткой и совпадает ли имя
            if (_activeScenario.Steps[i] is DataStep_Label labelStep && labelStep.LabelName == labelName)
            {
                return i;
            }
        }
        return -1;
    }

    public void TriggerJump(string targetLabel)
    {
        Debug.Log($"[ScenarioExecutor] Запрошен прыжок на '{targetLabel}'");
        _interruptTriggered = true;
        _interruptTargetLabel = targetLabel;
    }
}