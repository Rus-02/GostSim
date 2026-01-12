using UnityEngine;

public class InitializingState : StateBase
{
    public InitializingState(CentralizedStateManager context) : base(context) { }

    public override TestState StateEnum => TestState.Initializing;

    public override void OnEnter()
    {
        base.OnEnter();

        // --- МОЖНО ДОБАВИТЬ ПРОВЕРКИ ---
        
        // Пока просто считаем, что всё ок.
        // Мгновенный переход к простою.
        context.TransitionToState(new IdleState(context));
    }
}