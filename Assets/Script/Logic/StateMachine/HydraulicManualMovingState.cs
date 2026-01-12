using UnityEngine;

public class HydraulicManualMovingState : StateBase
{
    private readonly float _startDirection; 
    private readonly SpeedType _startSpeed;

    public HydraulicManualMovingState(CentralizedStateManager context, float dir, SpeedType speed) : base(context)
    {
        _startDirection = dir;
        _startSpeed = speed;
    }

    public override TestState StateEnum => TestState.HydraulicManualMoving;

    public override void OnEnter()
    {
        base.OnEnter();
        // Первый толчок при входе в состояние
        SendMoveCommand(_startDirection, _startSpeed);
    }

    // --- ИСПРАВЛЕНИЕ ЗДЕСЬ ---
    public override void OnHydraulicMove(float direction, SpeedType speed)
    {
        // Просто передаем команду дальше.
        // Твоя HydraulicMachineLogic сама разберется:
        // Если direction совпадает с текущим -> Ускорит.
        // Если direction противоположный -> Замедлит.
        SendMoveCommand(direction, speed);
    }
    // -------------------------

    public override void OnHydraulicStop()
    {
        context.TransitionToState(new HydraulicReturningState(context));
    }

    public override void OnOperationFinished()
    {
        // Если машина сама остановилась (уперлась в лимит) -> выходим в готовность
        context.TransitionToState(new ReadyForSetupState(context));
    }

    private void SendMoveCommand(float dir, SpeedType speed)
    {
        var dirEnum = dir > 0 ? Direction.Up : Direction.Down;
        var args = new ControlLoaderArgs(dirEnum, speed);
        ToDoManager.Instance.HandleAction(ActionType.ControlLoader, args);
    }
}