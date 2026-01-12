using UnityEngine;

public class ReadyToTestState : StateBase
{
    public ReadyToTestState(CentralizedStateManager context) : base(context) { }

    public override TestState StateEnum => TestState.ReadyToTest;

    public override void OnEnter()
    {
        base.OnEnter();
    }

    // =========================================================================
    // ГЛАВНОЕ ДЕЙСТВИЕ: СТАРТ
    // =========================================================================
    public override void OnStartTest()
    {
        // Инициализация контроллера теста
        ToDoManager.Instance.HandleAction(ActionType.InitializeTestController, null);
        
        // Запуск физики и графика
        ToDoManager.Instance.HandleAction(ActionType.StartGraphAndSimulation, null);

        // Переход в режим "Тест идет"
        context.TransitionToState(new TestRunningState(context));
    }

    // =========================================================================
    // СБРОС ГОТОВНОСТИ (Любое ручное вмешательство)
    // =========================================================================

    // 1. Движение Траверсы -> Сброс в ReadyForSetup
    public override void OnTraverseMove(float direction, SpeedType speed)
    {
        // Переходим в движение. Точкой возврата указываем ReadyForSetup, 
        // так как позиция собьется.
        context.TransitionToState(new TraverseManualMovingState(context, direction, speed, TestState.ReadyForSetup));
    }

    // 2. Движение Гидравлики -> Сброс в ReadyForSetup
    public override void OnHydraulicMove(float direction, SpeedType speed)
    {
        // Проверка насоса
        if (!monitor.IsPowerUnitActive)
        {
            ToDoManager.Instance.HandleAction(ActionType.ShowHintText, new ShowHintArgs("Включите насос!"));
            return;
        }
        context.TransitionToState(new HydraulicManualMovingState(context, direction, speed));
    }

    // 3. Захваты -> Сброс в ReadyForSetup
    // 3.1. Обработка нажатия кнопки
    public override void OnClampAction(GripType type, bool clamp)
    {
        // Если команда "ЗАЖАТЬ" (вдруг пришла?) — просто шлем, ничего не меняется.
        if (clamp) 
        {
            base.OnClampAction(type, true); // (или ToDoManager...Clamp)
            return;
        }

        // --- ЛОГИКА РАЗЖАТИЯ ---

        // СЛУЧАЙ 1: Разжимаем ВЕРХНИЙ (Опасность висения)
        // Твое решение: Удаляем образец полностью.
        if (type == GripType.Upper)
        {
            Debug.Log("[ReadyToTest] Разжатие верха -> Полное снятие образца.");
            
            // Мы просто вызываем метод снятия образца, который у нас уже есть!
            // Он запросит у хендлера сценарий (обычно: UnclampUp -> UnclampLow -> Remove -> Safe)
            OnSampleAction(); 
            return;
        }

        // СЛУЧАЙ 2: Разжимаем НИЖНИЙ
        // Образец повиснет на верхнем захвате. Это физически корректно.
        // Траверса может уехать вниз, образец останется висеть на месте.
        if (type == GripType.Lower)
        {
            Debug.Log("[ReadyToTest] Разжатие низа -> Возврат в настройку.");
            ToDoManager.Instance.HandleAction(ActionType.UnclampLowerGrip, null);
            
            // Анимация начнется -> GripAnimating -> ReadyForSetup.
            // В ReadyForSetup флаг IsSampleInPlace = true.
            // Траверса сможет ехать, образец (прикрепленный к верху) останется на месте.
        }
    }

    // 3.2. РЕАКЦИЯ НА ДВИЖЕНИЕ (СБРОС ГОТОВНОСТИ)
    public override void OnClampAnimationStarted()
    {
        // ВАЖНО: Мы переходим в GripAnimating.
        // А GripAnimating настроен так, что по завершении он возвращает в ReadyForSetup.
        // Таким образом мы автоматически "понижаем" статус готовности.
        
        context.TransitionToState(new GripAnimatingState(context, TestState.ReadyForSetup));
    }

    // 4. Образец (Снятие) -> Сброс в ReadyForSetup
    public override void OnSampleAction()
    {
        // Если юзер решил снять образец в последний момент
        var handler = context.CurrentTestLogicHandler;
        if (handler is IScenarioProvider scenarioProvider)
        {
            // Запускаем сценарий (обычно это снятие)
            var logicContext = context.CreateLogicHandlerContext();
            var scenario = scenarioProvider.GetOnSampleButtonPress_Scenario(logicContext); // Или Unload, зависит от логики
            
            context.RunScenario(scenario);            
        }
    }
}