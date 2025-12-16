using UnityEngine;
using System.Collections.Generic;

public class InputInterceptor : MonoBehaviour
{
    public static InputInterceptor Instance { get; private set; }

    [Header("Default Safety Policy")]
    [SerializeField] private ActionPolicy _defaultPolicy; 

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    public void ProcessClick(ButtonScript btn)
    {
        string id = btn.buttonId;
        EventType evt = btn.eventTypeToRaise;

        // --- ЭТАП 1: СЦЕНАРИЙ ---
        if (ScenarioExecutor.Instance != null)
        {
            string scenarioBlockReason = ScenarioExecutor.Instance.CheckInputBlock(id);
            
            if (!string.IsNullOrEmpty(scenarioBlockReason))
            {
                Debug.Log($"[Interceptor] Блок сценария для {id}: {scenarioBlockReason}");
                // ИСПРАВЛЕНИЕ: Оборачиваем строку в ShowHintArgs
                ToDoManager.Instance.HandleAction(ActionType.ShowHintText, new ShowHintArgs(scenarioBlockReason));
                return;
            }
        }

        // --- ЭТАП 2: ACTION POLICY ---
        bool bypassPolicy = ScenarioExecutor.Instance != null && ScenarioExecutor.Instance.BypassSafety;
        
        if (!bypassPolicy && _defaultPolicy != null)
        {
            var state = CentralizedStateManager.Instance.CurrentTestState;
            var config = CentralizedStateManager.Instance.CurrentTestConfiguration;
            
            // ИСПРАВЛЕНИЕ: Используем правильный Enum.
            // Предполагаем, что в config есть поле typeOfTest (TypeOfTest).
            // Если конфига нет, берем дефолтный WedgeGrip_Cylinder.
            TypeOfTest type = config != null ? config.typeOfTest : TypeOfTest.WedgeGrip_Cylinder;

            string safetyHint = _defaultPolicy.GetHintForAction(evt, state, type);
            
            if (!string.IsNullOrEmpty(safetyHint))
            {
                Debug.Log($"[Interceptor] Блок политики для {evt}: {safetyHint}");
                // ИСПРАВЛЕНИЕ: Оборачиваем строку в ShowHintArgs
                ToDoManager.Instance.HandleAction(ActionType.ShowHintText, new ShowHintArgs(safetyHint));
                return; 
            }
        }

        // --- ЭТАП 3: ИСПОЛНЕНИЕ ---
        Debug.Log($"[Interceptor] Пропуск события: {evt} от {id}");
        
        if (evt == EventType.PauseTestAction)
        {
            // Возвращаем управление кнопке, чтобы она сама решила Pause или Resume
            btn.ExecuteEventInternal(); 
        }
        else
        {
            EventManager.Instance.RaiseEvent(evt, new EventArgs(btn));
        }
    }
}