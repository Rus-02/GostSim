using UnityEngine;

public class IdleState : StateBase
{
    public IdleState(CentralizedStateManager context) : base(context) { }

    public override TestState StateEnum => TestState.Idle;

    public override void OnEnter()
    {
        base.OnEnter();
    }

    // В Idle разрешено ТОЛЬКО подтверждение параметров теста.
    // Остальные методы (движение, старт) тут не переопределены = запрещены.
    public override void OnTestParametersConfirmed()
    {
        // 1. Пытаемся инициализировать данные
        bool success = context.InitializeTestParameters();

        if (success)
        {
            // 2. Если успех — идем конфигурировать железо
            context.TransitionToState(new ConfiguringState(context));
        }
        else
        {
            // Если провал — остаемся в Idle, показываем ошибку
            Debug.LogWarning("Не удалось инициализировать тест.");
        }
    }
}