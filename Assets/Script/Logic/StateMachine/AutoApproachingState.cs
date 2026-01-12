using UnityEngine;

public class AutoApproachingState : StateBase
{
    public AutoApproachingState(CentralizedStateManager context) : base(context) { }

    public override TestState StateEnum => TestState.AutoApproaching;

    public override void OnEnter()
    {
        base.OnEnter();
        context.StartAutoApproachSequence();
    }

    // --- 1. ПЕРЕХВАТ РУЧНЫМ ДВИЖЕНИЕМ ---
    public override void OnTraverseMove(float direction, SpeedType speed)
    {
        // Если юзер нажал кнопку движения, мы прерываем авто-подвод и переходим в состояние ручного движения.
        // OnExit() этого состояния вызовется автоматически и остановит корутину.
        context.TransitionToState(new TraverseManualMovingState(context, direction, speed, TestState.ReadyForSetup));
    }

    // --- 2. ОСТАНОВКА / СТОП ---
    public override void OnTraverseStop()
    {
        // Просто прерываем и выходим в готовность
        context.TransitionToState(new ReadyForSetupState(context));
    }

    public override void OnExit()
    {
        // ВАЖНО: При любом выходе (перехват, стоп, смена состояния)
        // мы обязаны убить корутину авто-подвода.
        context.StopAutoApproachSequence(); 
        
        // И остановить мотор (на всякий случай)
        ToDoManager.Instance.HandleAction(ActionType.StopTraverseAction, null);
        
        base.OnExit();
    }

    public override void OnApproachCompleted()
    {
        // 1. САМОЕ ГЛАВНОЕ: Если образца нет — мы просто приехали в точку.
        // Тест начинать нельзя. Возвращаемся в настройку.
        if (!monitor.IsSampleInPlace)
        {
            Debug.Log("[AutoApproach] Траверса на месте (без образца). Возврат в ReadyForSetup.");
            context.TransitionToState(new ReadyForSetupState(context));
            return;
        }

        // 2. Если образец ЕСТЬ — проверяем, нужно ли дожать нижний захват
        if (ShouldAutoClampLower())
        {
            Debug.Log("[AutoApproach] Завершено. Авто-зажатие нижнего захвата.");
            ToDoManager.Instance.HandleAction(ActionType.ClampLowerGrip, null);

            // Идем анимировать, а потом в ReadyToTest
            context.TransitionToState(new GripAnimatingState(context, TestState.ReadyToTest));
            return;
        }

        // 3. Образец есть, зажимать не надо — значит ГОТОВО.
        context.TransitionToState(new ReadyToTestState(context));
    }

    private bool ShouldAutoClampLower()
    {
        // Образец должен быть
        if (!monitor.IsSampleInPlace) return false;

        // Нижний захват должен быть открыт (иначе зачем зажимать)
        if (monitor.IsLowerGripClamped) return false;

        // Конфиг должен требовать зажатия (для сжатия это false)
        var config = monitor.CurrentTestConfig;
        if (config == null || !config.requiresLowerClamp) return false;

        // Проверяем тип хендлера (Только для Standard Tensile)
        var handler = monitor.CurrentTestLogicHandler;
        if (handler is TensileLogicHandler) return true;
        return false;
    }
}