using UnityEngine;

public class HydraulicReturningState : StateBase
{
    public HydraulicReturningState(CentralizedStateManager context) : base(context) { }

    public override TestState StateEnum => TestState.HydraulicReturning;

    public override void OnEnter()
    {
        base.OnEnter();
        Debug.Log("Возврат гидравлики и сброс подушки...");

        // 1. Сбрасываем масляную подушку (твое требование)
        // false = деактивировать/опустить
        ToDoManager.Instance.HandleAction(ActionType.SetSupportSystemState, new SetSupportSystemStateArgs(false));
        
        // 2. Шлем команду ВОЗВРАТ на силовую раму
        ToDoManager.Instance.HandleAction(ActionType.ControlLoader, new ControlLoaderArgs(true));
        
        // Ждем события OnOperationFinished...
    }

    public override void OnOperationFinished()
    {
        context.TransitionToState(new ReadyForSetupState(context));
    }
}