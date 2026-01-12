using UnityEngine;

public class TestResult_SampleSafeState : StateBase
{
    public TestResult_SampleSafeState(CentralizedStateManager context) : base(context) { }

    public override TestState StateEnum => TestState.TestResult_SampleSafe;

    public override void OnEnter()
    {
        base.OnEnter();
        // UI сам обновится, увидев состояние Safe. 
        // Кнопка "Снять образец" будет активна, пока IsSampleInPlace == true.
    }

    // ====================================================================
    // ПОВЕДЕНИЕ 1: ОБРАЗЕЦ ЕСТЬ (Нужно снять)
    // ====================================================================

    public override void OnSampleAction()
    {
        // Если образца нет, кнопку жать бессмысленно (или это установка, что здесь запрещено)
        if (!monitor.IsSampleInPlace) return;

        var handler = context.CurrentTestLogicHandler;
        if (handler is IScenarioProvider scenarioProvider)
        {
            var ctx = context.CreateLogicHandlerContext();
            
            // Запрашиваем сценарий СНЯТИЯ
            // (Внутри сценария обязательно должен быть шаг, который вызовет
            // monitor.ReportSamplePresence(false) или уничтожит объект)
            var scenario = scenarioProvider.GetOnSampleButtonPress_Scenario(ctx);
            
            context.RunScenario(scenario);
            
            // После завершения сценария мы остаемся в ЭТОМ ЖЕ состоянии (Safe),
            // но флаг IsSampleInPlace в мониторе станет false.
        }
    }

    // ====================================================================
    // ПОВЕДЕНИЕ 2: ОБРАЗЦА НЕТ (Можно начать новый тест)
    // ====================================================================

    // Нажатие "Настройки" / "Применить" / "Новый тест"
    public override void OnTestParametersConfirmed()
    {
        // БЛОКИРОВКА: Нельзя начать новый тест, пока старый образец внутри
        if (monitor.IsSampleInPlace)
        {
            ToDoManager.Instance.HandleAction(ActionType.ShowHintText, new ShowHintArgs("Сначала удалите разрушенный образец!"));
            return;
        }

        // Если образца нет — сбрасываем данные и идем на круг
        RestartSystem();
    }

    // --- Вспомогательные методы ---

    private void RestartSystem()
    {
        Debug.Log("[ResultSafe] Перезапуск системы для нового теста...");
        
        // 1. Сброс графиков и контроллера
        context.PerformFullSimulationReset();

        // 2. Повторная инициализация параметров (на случай изменений в меню)
        if (context.InitializeTestParameters())
        {
            // Если всё ок -> Configuring -> ReadyForSetup
            context.TransitionToState(new ConfiguringState(context));
        }
    }

    private void PerformExitAndReset()
    {
        // Полный сброс всего
        context.PerformFullSimulationReset();
        
        // Переход в режим просмотра
        ToDoManager.Instance.HandleAction(ActionType.SetDisplayMode, new SetDisplayModeArgs("РЕЖИМ ПРОСМОТРА"));
        
        // В Idle
        context.TransitionToState(new IdleState(context));
    }

    public override void OnFinishTestCommand()
    {
        // Проверка: Если образца нет — выпускаем
        if (monitor.IsSampleInPlace)
        {
            ToDoManager.Instance.HandleAction(ActionType.ShowHintText, new ShowHintArgs("Снимите образец перед завершением!"));
            return;
        }

        PerformExitToIdle(); // Используем метод базы
    }

    // ДВИЖЕНИЕ ТРАВЕРСЫ
    public override void OnTraverseMove(float direction, SpeedType speed)
    {
        // Разрешаем движение.
        // В качестве точки возврата передаем TestState.TestResult_SampleSafe.
        context.TransitionToState(new TraverseManualMovingState(context, direction, speed, TestState.TestResult_SampleSafe));
    }
}