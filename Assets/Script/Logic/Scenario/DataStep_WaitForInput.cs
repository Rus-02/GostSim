using System;
using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Scenario/Steps/Wait For Input")]
public class DataStep_WaitForInput : DataStep
{
    [Tooltip("Какое событие ждем? (Например, StartTestAction)")]
    public EventType EventToWait;

    public override IScenarioLogic CreateLogic()
    {
        return new Logic_WaitForInput(this);
    }
}

public class Logic_WaitForInput : IScenarioLogic
{
    private readonly DataStep_WaitForInput _data;
    private bool _eventReceived = false;

    public Logic_WaitForInput(DataStep_WaitForInput data)
    {
        _data = data;
    }

    public IEnumerator Execute(ScenarioExecutor executor)
    {
        _eventReceived = false;

        // 1. Подписываемся на событие
        Action<EventArgs> handler = OnEventReceived;
        EventManager.Instance.Subscribe(_data.EventToWait, executor, handler);

        Debug.Log($"[Step Wait] Жду события: {_data.EventToWait}...");

        // 2. Ждем, пока флаг не станет true
        while (!_eventReceived)
        {
            // --- НОВОЕ: Проверка на прерывание ---
            if (executor.IsInterruptPending)
            {
                Debug.Log($"[Step Wait] Прерывание ожидания {_data.EventToWait} (Глобальный триггер).");
                break; // Выходим из цикла, чтобы Executor мог совершить прыжок
            }
            // -------------------------------------

            yield return null;
        }

        // 3. Отписываемся (всегда, даже если прервали)
        EventManager.Instance.Unsubscribe(_data.EventToWait, executor, handler);
        
        if (_eventReceived)
        {
            Debug.Log($"[Step Wait] Событие {_data.EventToWait} получено. Идем дальше.");
        }
    }

    private void OnEventReceived(EventArgs args)
    {
        _eventReceived = true;
    }
}