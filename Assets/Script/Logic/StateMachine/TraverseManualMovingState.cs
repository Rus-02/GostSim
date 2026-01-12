using UnityEngine;

public class TraverseManualMovingState : StateBase
{
    private readonly float _direction;
    private readonly SpeedType _speed;
    private readonly TestState _returnToState; // Куда возвращаться

    public TraverseManualMovingState(CentralizedStateManager context, float dir, SpeedType speed, TestState returnTo) 
        : base(context)
    {
        _direction = dir;
        _speed = speed;
        _returnToState = returnTo;
    }

    public override TestState StateEnum => TestState.TraverseManualMoving;

    public override void OnEnter()
    {
        base.OnEnter();
        // 1. Сразу шлем команду на движение (копируем логику из твоего CSM)
        // Проверка IsManualTraverseAllowed здесь не нужна - мы уже внутри состояния, где это разрешено.
        ToDoManager.Instance.HandleAction(ActionType.MoveTraverse, new MoveTraverseArgs(_direction, _speed));
    }

    public override void OnTraverseStop()
    {
        // 1. Шлем стоп машине
        ToDoManager.Instance.HandleAction(ActionType.StopTraverseAction, null);

        // 2. ВЫБИРАЕМ, КУДА ВЕРНУТЬСЯ
        // Используем переменную _returnToState, которую мы получили в конструкторе
        StateBase nextState;

        switch (_returnToState)
        {
            // Если разгрузили образец -> Идем в Safe
            case TestState.TestResult_SampleSafe:
                nextState = new TestResult_SampleSafeState(context);
                break;

            // Если просто подвигали, но не разгрузили -> Идем обратно в UnderLoad
            case TestState.TestFinished_SampleUnderLoad:
                nextState = new TestFinished_SampleUnderLoadState(context);
                break;

            // Если были готовы к тесту -> Идем обратно в ReadyToTest
            case TestState.ReadyToTest:
                nextState = new ReadyToTestState(context);
                break;

            // По умолчанию (и для ReadyForSetup)
            case TestState.ReadyForSetup:
            default:
                nextState = new ReadyForSetupState(context);
                break;
        }

        // 3. Переход
        context.TransitionToState(nextState);
    }

    public override void OnTraverseSpeedAdjust(bool increase)
    {
        // Можно менять скорость во время движения
        ToDoManager.Instance.HandleAction(ActionType.AdjustSpeed, new AdjustSpeedArgs(increase ? 1f : -1f));
    }

    public override void OnExit()
    {
        // Страховка
        ToDoManager.Instance.HandleAction(ActionType.StopTraverseAction, null);
        base.OnExit();
    }
}