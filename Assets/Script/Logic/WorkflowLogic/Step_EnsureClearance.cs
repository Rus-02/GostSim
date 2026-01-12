using System;
using System.Collections;
using UnityEngine;

public class Step_EnsureClearance : IWorkflowStep
{
    private readonly TestType _testType;
    private bool _isCompleted = false;

    // Ключ для передачи конкретной позиции через контекст (для VSM)
    public const string CTX_KEY_SAFE_POS_OVERRIDE = "SafeTraversePositionOverride";

    public Step_EnsureClearance(TestType testType)
    {
        _testType = testType;
    }

    public IEnumerator Execute(WorkflowContext context)
    {
        SystemStateMonitor.Instance?.ReportFixtureChangeStatus(true);
        
        Debug.Log($"[Step_EnsureClearance] Запрос пространства для: {_testType}");

        _isCompleted = false;
        ActionRequester myRequester = context.Requester; // Используем реквестера из контекста!

        Action<EventArgs> onClearanceReady = (e) =>
        {
            if (e is FixtureInstallationClearanceReadyEventArgs args && args.Requester == myRequester)
            {
                _isCompleted = true;
            }
        };

        EventManager.Instance.Subscribe(EventType.FixtureInstallationClearanceReady, this, onClearanceReady);

        // --- ИЗМЕНЕНИЕ ЗДЕСЬ ---
        // 1. Проверяем, есть ли переопределение позиции в контексте (Nullable float)
        float? specificPosition = context.GetData<float?>(CTX_KEY_SAFE_POS_OVERRIDE);

        // 2. Формируем аргументы. Если specificPosition есть, передаем его, иначе null.
        var cmdArgs = new EnsureFixtureInstallationClearanceArgs(specificPosition, _testType, myRequester);
        
        ToDoManager.Instance.HandleAction(ActionType.EnsureFixtureInstallationClearance, cmdArgs);

        // 3. Ждем выполнения
        float timeout = 15f;
        float timer = 0f;
        while (!_isCompleted && timer < timeout)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        EventManager.Instance.Unsubscribe(EventType.FixtureInstallationClearanceReady, this, onClearanceReady);

        if (!_isCompleted)
        {
            Debug.LogError($"[Step_EnsureClearance] Таймаут! Машина не ответила на запрос (Pos: {specificPosition?.ToString() ?? "Auto"}, Req: {myRequester}).");
            // Здесь можно добавить yield break, если критично, но пока оставим как есть, чтобы видеть логику дальше
        }
        else
        {
            Debug.Log("[Step_EnsureClearance] Пространство обеспечено.");
        }
    }
}