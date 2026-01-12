using UnityEngine;

public class ConfiguringState : StateBase
{
    public ConfiguringState(CentralizedStateManager context) : base(context) { }

    public override TestState StateEnum => TestState.Configuring;

    public override void OnEnter()
    {
        base.OnEnter();
        
        Debug.Log("[ConfiguringState] Запуск Workflow смены оснастки...");
        
        // Вместо вызова корутины напрямую, мы вызываем метод запуска Workflow Runner-а
        context.StartStandardFixtureChangeWorkflow();
    }
    
    // В случае принудительного выхода (Stop/Reset) нам нужно остановить Runner
    public override void OnExit()
    {
        base.OnExit();
        // Если мы уходим из состояния раньше времени - рубим процесс
        context.Runner?.Stop();
    }
}
