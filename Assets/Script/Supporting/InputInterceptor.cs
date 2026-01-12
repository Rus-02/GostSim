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

        // 1. Спрашиваем Сценарий (Scenario Decision)
        string scenarioBlockReason = null;
        if (ScenarioExecutor.Instance != null)
        {
            scenarioBlockReason = ScenarioExecutor.Instance.CheckInputBlock(id);
        }

        // 2. Спрашиваем Политику (Policy Decision)
        string policyBlockReason = null;
        bool bypassPolicy = ScenarioExecutor.Instance != null && ScenarioExecutor.Instance.BypassSafety;

        if (!bypassPolicy && _defaultPolicy != null)
        {
            var state = CentralizedStateManager.Instance.CurrentTestState;
            var config = CentralizedStateManager.Instance.CurrentTestConfiguration;
            TypeOfTest type = config != null ? config.typeOfTest : TypeOfTest.WedgeGrip_Cylinder;

            policyBlockReason = _defaultPolicy.GetHintForAction(evt, state, type);
        }

        // --- 3. ПРИНЯТИЕ РЕШЕНИЯ (Умный Арбитраж) ---

        // А. Если Политика против (Физический/Технический запрет)
        // Мы показываем ЕЁ текст, потому что он важнее и конкретнее ("Двери открыты", "Лимит силы").
        if (!string.IsNullOrEmpty(policyBlockReason))
        {
            Debug.Log($"[Interceptor] Блок Политики: {policyBlockReason}");
            ToDoManager.Instance.HandleAction(ActionType.ShowHintText, new ShowHintArgs(policyBlockReason));
            return;
        }

        // Б. Если Политика не против, но Сценарий против (Организационный запрет)
        // Показываем текст сценария ("В режиме теста это меню недоступно").
        if (!string.IsNullOrEmpty(scenarioBlockReason))
        {
            Debug.Log($"[Interceptor] Блок Сценария: {scenarioBlockReason}");
            ToDoManager.Instance.HandleAction(ActionType.ShowHintText, new ShowHintArgs(scenarioBlockReason));
            return;
        }

        // В. Все согласны -> Выполняем
        Debug.Log($"[Interceptor] Пропуск события: {evt} от {id}");
        
        if (evt == EventType.PauseTestAction)
        {
            btn.ExecuteEventInternal();
        }
        else
        {
            EventManager.Instance.RaiseEvent(evt, new EventArgs(btn));
        }
    }
}