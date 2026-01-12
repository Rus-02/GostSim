using UnityEngine;

public class ReadyForSetupState : StateBase
{
    public ReadyForSetupState(CentralizedStateManager context) : base(context) { }

    public override TestState StateEnum => TestState.ReadyForSetup;

    public override void OnEnter()
    {
        base.OnEnter();
        // Сразу при входе проверяем, не готовы ли мы к тесту
        CheckAndTransitionToReady();
    }

    // --- 1. ПЕРЕКОНФИГУРАЦИЯ ---
    public override void OnTestParametersConfirmed()
    {
        if (context.InitializeTestParameters())
            context.TransitionToState(new ConfiguringState(context));
    }

    // 1.1. ПРОВЕРКА ГОТОВНОСТИ К ТЕСТУ
    private void CheckAndTransitionToReady()
    {
        // 1.1.1. Базовые условия для ВСЕХ тестов
        if (!monitor.IsSampleInPlace) return;    // Образца нет - не готовы
        if (!monitor.IsTraverseAtTarget) return; // Траверса не в точке подвода - не готовы

        bool specificConditionsMet = false;

        // 1.1.2. Специфичные условия
        if (monitor.CurrentGeneralTestType == TestType.Tensile)
        {
            // Для Растяжения: Обязательно оба захвата закрыты
            if (monitor.IsUpperGripClamped && monitor.IsLowerGripClamped)
            {
                specificConditionsMet = true;
            }
        }
        else if (monitor.CurrentGeneralTestType == TestType.Compression)
        {
            // Для Сжатия: Достаточно того, что образец стоит и мы подъехали (касание)
            // (Если у тебя есть логика зажатия для сжатия - добавь сюда проверку)
            specificConditionsMet = true;
        }

        // 1.1.3. Переход
        if (specificConditionsMet)
        {
            Debug.Log("[ReadyForSetup] Все условия выполнены -> Авто-переход в ReadyToTest");
            context.TransitionToState(new ReadyToTestState(context));
        }
    }


    // --- 2. ДВИЖЕНИЕ ТРАВЕРСЫ ---
    public override void OnTraverseMove(float direction, SpeedType speed)
    {
        if (ShouldAutoUnclampLowerGrip())
        {
            Debug.Log("[ReadyForSetup] Авто-разжатие нижнего захвата (Standard Tensile).");
            ToDoManager.Instance.HandleAction(ActionType.UnclampLowerGrip, null);
            // Не едем, ждем пока разожмется
            //return;
        }

        context.TransitionToState(new TraverseManualMovingState(context, direction, speed, TestState.ReadyForSetup));
    }

    public override void OnTraverseSpeedAdjust(bool increase)
    {
        // Отправляем команду изменения скорости (она работает и в покое)
        ToDoManager.Instance.HandleAction(ActionType.AdjustSpeed, new AdjustSpeedArgs(increase ? 1f : -1f));
    }

    public override void OnApproachCompleted()
    {
        // Логика завершения подвода
        // Пока просто лог, или переход в состояние Setup_AtTarget (если будешь его делать)
        Debug.Log("Траверса в позиции подвода!");
    }

    public override void OnAutoApproach()
    {
        if (ShouldAutoUnclampLowerGrip())
        {
            Debug.Log("[ReadyForSetup] Авто-разжатие перед авто-подводом.");
            ToDoManager.Instance.HandleAction(ActionType.UnclampLowerGrip, null);
            //return; 
        }

        // 1. Проверяем, готова ли машина (двери, насос и т.д.)
        if (!monitor.IsMachineReadyForSetup)
        {
            ToDoManager.Instance.HandleAction(ActionType.ShowHintText, new ShowHintArgs(monitor.MachineNotReadyReason));
            return;
        }

        // 2. Считаем цель
        context.CalculateAndStoreApproachTarget();
        
        // Проверяем, посчиталось ли (если NaN — значит ошибка)
        if (float.IsNaN(monitor.LastApproachTargetZ)) // Или используй WorldY, если добавил проверку валидности
        {
            ToDoManager.Instance.HandleAction(ActionType.ShowHintText, new ShowHintArgs("Ошибка расчета точки подвода."));
            return;
        }

        // 3. Переходим в состояние подвода
        context.TransitionToState(new AutoApproachingState(context));
    }

    
    // --- 3. ДВИЖЕНИЕ ГИДРАВЛИКИ ---
    public override void OnHydraulicMove(float direction, SpeedType speed)
    {
        // Проверяем, включен ли насос (можно тут, или внутри состояния, или МК сам проверит)
        if (!monitor.IsPowerUnitActive)
        {
            ToDoManager.Instance.HandleAction(ActionType.ShowHintText, new ShowHintArgs("Включите насос!"));
            return;
        }

        context.TransitionToState(new HydraulicManualMovingState(context, direction, speed));
    }

    public override void OnHydraulicStop()
    {
        // Даже если мы стоим, нажатие СТОП означает "Верни всё в ноль и опусти подушку".
        // Переходим в состояние возврата, оно само всё сделает.
        context.TransitionToState(new HydraulicReturningState(context));
    }
    
    // --- 4.   РАБОТА ЗАХВАТОВ ---
    // 1. Обработка кнопок UI
    public override void OnClampAction(GripType type, bool clamp)
    {        
        var actionType = clamp 
            ? (type == GripType.Upper ? ActionType.ClampUpperGrip : ActionType.ClampLowerGrip)
            : (type == GripType.Upper ? ActionType.UnclampUpperGrip : ActionType.UnclampLowerGrip);

        ToDoManager.Instance.HandleAction(actionType, null);
    }

    // 2. Реакция на старт анимации
    // (Этот метод вызовется, когда машина реально начнет двигаться)
    public override void OnClampAnimationStarted()
    {
        // Блокируем интерфейс переходом в GripAnimating
        context.TransitionToState(new GripAnimatingState(context, TestState.ReadyForSetup));
    }
    
    // --- 5. ЛОГИКА ОБРАЗЦА ---
    public override void OnSampleAction()
    {
        // 1. Проверки
        if (context.IsScenarioRunning) return;
        if (monitor.IsFixtureChangeInProgress) return;

        // ИСПОЛЬЗУЕМ МОНИТОР
        bool isSamplePresent = monitor.IsSampleInPlace; 
        
        // Логика блокировки установки, если машина не готова
        // (Обычно снимать можно всегда, а ставить - только если готова)
        if (!isSamplePresent && !monitor.IsMachineReadyForSetup)
        {
            ToDoManager.Instance.HandleAction(ActionType.ShowHintText, new ShowHintArgs(monitor.MachineNotReadyReason));
            return;
        }

        // 2. Получаем логику от хендлера
        var handler = context.CurrentTestLogicHandler;
        if (handler is IScenarioProvider scenarioProvider)
        {
            var logicContext = context.CreateLogicHandlerContext();
            var scenario = scenarioProvider.GetOnSampleButtonPress_Scenario(logicContext);
            
            // 3. Запускаем
            context.RunScenario(scenario);
        }
    }

    public override void OnFinishTestCommand()
    {
        // Проверка: Если образца нет — выпускаем
        if (monitor.IsSampleInPlace)
        {
            ToDoManager.Instance.HandleAction(ActionType.ShowHintText, new ShowHintArgs("Снимите образец перед выходом!"));
            return;
        }

        PerformExitToIdle(); // Используем метод базы
    }

    // --- ВСПОМОГАТЕЛЬНЫЙ МЕТОД ПРОВЕРКИ НЕОБХОДИМОСТИ РАЗЖАТИЯ НИЖНЕГО ЗАХВАТА ---
    private bool ShouldAutoUnclampLowerGrip()
    {
        // 1. Базовые условия: Образец есть + Низ зажат
        if (!monitor.IsSampleInPlace || !monitor.IsLowerGripClamped) return false;

        // 2. Проверка Типа Теста
        var handler = monitor.CurrentTestLogicHandler;

        // Если это Пропорциональные (Proportional) — НЕЛЬЗЯ разжимать (false)
        if (handler is TensileProportionalLogicHandler) return false;

        // Если это Обычный Разрыв (Tensile) — НУЖНО разжимать (true)
        if (handler is TensileLogicHandler) return true;

        // Для Сжатия (Compression) и прочих — false
        return false;
    }
}