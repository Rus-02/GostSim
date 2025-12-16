using UnityEngine;
using System; 

public class ApplicationStateManager : MonoBehaviour
{
    public static ApplicationStateManager Instance { get; private set; }

    public TestState currentState;
    public TypeOfTest currentTestType;
    public EventType lastAction;

    // --- НОВОЕ: Событие, которое будет оповещать подписчиков об изменениях ---
    public static event Action OnStateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    public void SetLastAction(EventType action)
    {
        // Небольшая оптимизация: вызываем событие, только если значение реально изменилось
        if (lastAction == action) return;

        lastAction = action;
        // Оповещаем всех подписчиков, что что-то изменилось
        OnStateChanged?.Invoke();
    }

    public void SetCurrentState(TestState state)
    {
        // Небольшая оптимизация: вызываем событие, только если значение реально изменилось
        if (currentState == state) return;

        currentState = state;
        // Оповещаем всех подписчиков, что что-то изменилось
        OnStateChanged?.Invoke();
    }
}