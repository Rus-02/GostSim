using UnityEngine;

public class TestFinished_SampleUnderLoadState : StateBase
{
    public TestFinished_SampleUnderLoadState(CentralizedStateManager context) : base(context) { }

    public override TestState StateEnum => TestState.TestFinished_SampleUnderLoad;

    public override void OnEnter()
    {
        base.OnEnter();

        // --- ГЛАВНАЯ ПРОВЕРКА ---
        // Если флаг уже стоит (разгрузили траверсой или кнопкой), 
        // нам тут делать нечего. Сразу пробрасываем в Safe.
        if (monitor.IsSampleUnloaded)
        {
            Debug.Log("[UnderLoad] Образец уже разгружен. Авто-переход в Safe.");
            context.TransitionToState(new TestResult_SampleSafeState(context));
            return; // ВАЖНО: Прерываем выполнение, чтобы не показывать UI опасности
        }

        // Если мы дошли сюда — значит, нагрузка реально есть
        Debug.LogWarning("[Result] Образец под нагрузкой. Требуется действие.");
        // Тут код включения UI предупреждений...
    }

    // --- 1. КНОПКА "РАЗГРУЗИТЬ" (Явная) ---
    public override void OnUnloadSample()
    {
        // Запускаем сценарий разгрузки
        var handler = context.CurrentTestLogicHandler;
        if (handler is IScenarioProvider scenarioProvider)
        {
            var ctx = context.CreateLogicHandlerContext();
            var scenario = scenarioProvider.GetOnUnloadSamplePress_Scenario(ctx);
            context.RunScenario(scenario);
            
            // Важно: Обычно сценарий разгрузки заканчивается установкой флага IsSampleUnloaded = true
            // и, возможно, переходом в SafeState. 
            // Если сценарий не меняет состояние сам, юзер нажмет "Снять образец" и проверка пройдет.
        }
    }

    // --- 2. РУЧНАЯ РАЗГРУЗКА (Траверсой) ---
    public override void OnTraverseMove(float direction, SpeedType speed)
    {
        // Логика определения разгрузки
        if (monitor.CurrentGeneralTestType == TestType.Compression)
        {
            // Едем ВВЕРХ -> Ставим флаг
            if (direction > 0)
            {
                SystemStateMonitor.Instance.ReportSampleUnloaded(true);
                ToDoManager.Instance.HandleAction(ActionType.ShowHintText, new ShowHintArgs("Образец разгружен (траверсой)."));
            }
        }

        // Едем и возвращаемся СЮДА ЖЕ.
        // OnEnter при возврате увидит флаг и перекинет в Safe.
        context.TransitionToState(new TraverseManualMovingState(context, direction, speed, TestState.TestFinished_SampleUnderLoad));
    }

    // --- 3. СНЯТИЕ ОБРАЗЦА (Проверка безопасности) ---
    public override void OnSampleAction()
    {
        // Если флаг разгрузки не стоит - запрещаем снимать
        if (!monitor.IsSampleUnloaded)
        {
            ToDoManager.Instance.HandleAction(ActionType.ShowHintText, new ShowHintArgs("Опасно! Сначала разгрузите образец."));
            return;
        }

        // Если разгружен -> Переходим в Safe и сразу запускаем процедуру снятия
        context.TransitionToState(new TestResult_SampleSafeState(context));
    }
}