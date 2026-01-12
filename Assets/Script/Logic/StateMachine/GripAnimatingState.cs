using UnityEngine;

public class GripAnimatingState : StateBase
{
    private readonly TestState _returnStateEnum; 

    public GripAnimatingState(CentralizedStateManager context, TestState returnStateEnum) : base(context)
    {
        _returnStateEnum = returnStateEnum;
    }

    public override TestState StateEnum => TestState.GripAnimating;

    public override void OnClampAnimationFinished()
    {
        // Возвращаемся в запрошенное состояние
        switch (_returnStateEnum)
        {
            case TestState.ReadyToTest:
                context.TransitionToState(new ReadyToTestState(context));
                break;
                
            // По дефолту или для ReadyForSetup
            case TestState.ReadyForSetup:
            default:
                context.TransitionToState(new ReadyForSetupState(context));
                break;
        }
    }
}