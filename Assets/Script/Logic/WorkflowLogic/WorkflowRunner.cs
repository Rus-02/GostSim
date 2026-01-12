using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorkflowRunner
{
    private MonoBehaviour _coroutineHost; // Тот, кто физически крутит корутину (обычно CSM или State)
    private Coroutine _activeRoutine;

    // Свойство для проверки, занят ли раннер прямо сейчас
    public bool IsRunning => _activeRoutine != null;

    public WorkflowRunner(MonoBehaviour host)
    {
        _coroutineHost = host;
    }

    /// <summary>
    /// Запускает последовательность шагов. Если что-то уже бежало — прерывает.
    /// </summary>
    public void Start(List<IWorkflowStep> steps, WorkflowContext context)
    {
        Stop(); // Сброс предыдущего, если был
        if (steps == null || steps.Count == 0) return;

        _activeRoutine = _coroutineHost.StartCoroutine(RunSequence(steps, context));
    }

    /// <summary>
    /// Принудительная остановка процесса.
    /// </summary>
    public void Stop()
    {
        if (_activeRoutine != null)
        {
            _coroutineHost.StopCoroutine(_activeRoutine);
            _activeRoutine = null;
        }
    }

    private IEnumerator RunSequence(List<IWorkflowStep> steps, WorkflowContext context)
    {
        // Проходим по списку шагов по очереди
        foreach (var step in steps)
        {
            if (step == null) continue;

            // Выполняем шаг и ждем его завершения
            yield return step.Execute(context);
        }

        _activeRoutine = null;
        // Здесь можно добавить событие OnWorkflowCompleted, если понадобится
    }
}