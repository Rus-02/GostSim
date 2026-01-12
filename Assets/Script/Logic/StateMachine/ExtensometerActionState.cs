using UnityEngine;
using System;

public class ExtensometerActionState : StateBase
{
    private readonly bool _isAttach;

    // Флаг _isActionCompleted удален, так как блокировка теперь не нужна.
    // Сценарий сам решит, когда идти дальше (по событию нажатия).

    public ExtensometerActionState(CentralizedStateManager context, bool isAttach) : base(context)
    {
        _isAttach = isAttach;
    }

    public override TestState StateEnum => _isAttach ? TestState.AwaitingExtensometerAttach : TestState.AwaitingExtensometerRemove;

    public override void OnEnter()
    {
        base.OnEnter();
        
        // Паузу здесь вызывать не обязательно (машина сама встала на паузу, чтобы попасть сюда),
        // но можно оставить для надежности.
        ToDoManager.Instance.HandleAction(ActionType.PauseGraphAndSimulation, null);

        string btnText = _isAttach ? "УСТАНОВИТЬ\nЭКСТЕНЗОМЕТР" : "СНЯТЬ\nЭКСТЕНЗОМЕТР";
        
        // Настраиваем кнопку: Текст + Ивент подтверждения (его ждет Сценарий)
        ToDoManager.Instance.HandleAction(ActionType.UpdateUIButtonVisuals, 
            new UpdateUIButtonVisualsArgs("ApproachTraverse", null, btnText, EventType.ExtensometerActionConfirmed));
    }

    public override void OnExtensometerConfirm()
    {
        // --- ЛОГИКА ФИЗИКИ (Оставляем) ---
        // Мы просто отправляем команду машине сделать действие. 
        // Никаких переходов состояний!

        if (_isAttach)
        {
            var config = monitor.CurrentTestConfig;
            if (config != null && !string.IsNullOrEmpty(config.samplePlacementZoneTag))
            {
                var (drive, undrive, _) = context.FindDriveUndrivePointsAndDistance(config.samplePlacementZoneTag);

                if (drive != null && undrive != null)
                {
                    var args = new ExtensometerControlArgs(ExtensometerAction.Attach, drivePoint: drive, undrivePoint: undrive);
                    ToDoManager.Instance.HandleAction(ActionType.ControlExtensometer, args);
                }
            }
        }
        else
        {
            var args = new ExtensometerControlArgs(ExtensometerAction.ReturnToTable);
            ToDoManager.Instance.HandleAction(ActionType.ControlExtensometer, args);
        }

        // --- ЛОГИКА ВИЗУАЛА (Оставляем) ---
        // Мгновенно меняем текст, чтобы юзер видел реакцию
        string doneText = _isAttach ? "УСТАНОВЛЕНО" : "СНЯТО";
        ToDoManager.Instance.HandleAction(ActionType.UpdateUIButtonVisuals, 
            new UpdateUIButtonVisualsArgs("ApproachTraverse", null, doneText, null));
            
        // ВАЖНО: Мы НЕ вызываем здесь TransitionToState и НЕ вызываем Resume.
        // Мы просто сделали дело. 
        // Сценарий увидит событие ExtensometerActionConfirmed и сам нажмет Resume на следующем шаге.
    }

    public override void OnStartTest()
    {
        // ПУСТО.
        // Кнопка "Старт" теперь обрабатывается Сценарием или заблокирована.
        // Старая логика (Resume + Transition) здесь удалена, чтобы не было двойного срабатывания.
    }
    
    public override void OnStopTest()
    {
        // ПУСТО.
        // Глобальный Interrupt в Сценарии сам поймает StopTestAction и переведет систему в финал.
        // Если оставить здесь код, будет конфликт переходов.
    }

    public override void OnExit()
    {
        // Возвращаем кнопку в исходное состояние (для ручного режима)
        ToDoManager.Instance.HandleAction(ActionType.UpdateUIButtonVisuals, 
            new UpdateUIButtonVisualsArgs("ApproachTraverse", null, "ПОДВЕСТИ\nТРАВЕРСУ", EventType.ApproachTraverseAction));
            
        base.OnExit();
    }
}