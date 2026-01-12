using UnityEngine;

public class AwaitingExtensometerAttachState : StateBase
{
    public AwaitingExtensometerAttachState(CentralizedStateManager context) : base(context) { }
    
    // Соответствует Enum
    public override TestState StateEnum => TestState.AwaitingExtensometerAttach;

    public override void OnEnter()
    {
        base.OnEnter();
        Debug.Log("Ожидание установки экстензометра...");
        // Тут потом будет логика подсказок
    }
    
    // Сюда потом добавим подтверждение установки
}