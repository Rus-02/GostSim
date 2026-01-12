using System.Collections;

public interface IWorkflowStep
{
    /// <summary>
    /// Выполнить действие.
    /// Возвращает IEnumerator, так как большинство действий асинхронны (анимации, таймеры).
    /// </summary>
    IEnumerator Execute(WorkflowContext context);
}