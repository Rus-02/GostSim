using System.Collections;

/// <summary>
/// Интерфейс для логики выполнения одного шага сценария.
/// Создается фабрикой (DataStep) во время выполнения.
/// </summary>
public interface IScenarioLogic
{
    /// <summary>
    /// Запускает выполнение шага.
    /// </summary>
    /// <param name="executor">Ссылка на исполнителя для доступа к системам (UI, CSM).</param>
    IEnumerator Execute(ScenarioExecutor executor);
}