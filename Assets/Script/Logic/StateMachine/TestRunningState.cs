using UnityEngine;

public class TestRunningState : StateBase
{
    public TestRunningState(CentralizedStateManager context) : base(context) { }

    public override TestState StateEnum => TestState.TestRunning;

    public override void OnEnter()
    {
        base.OnEnter();
        // Можно включить блокировку UI, если она еще не включена
        Debug.Log("[TestRunning] График пишется...");
    }

    // --- 1. ПАУЗА ---
    public override void OnPauseTest()
    {
        // Шлем команду паузы графику и физике
        ToDoManager.Instance.HandleAction(ActionType.PauseGraphAndSimulation, null);
        
        // Переходим в состояние паузы
        context.TransitionToState(new TestPausedState(context));
    }

    // --- 2. ПРИНУДИТЕЛЬНЫЙ СТОП (Юзер нажал Стоп) ---
    public override void OnStopTest()
    {
        // Останавливаем всё
        ToDoManager.Instance.HandleAction(ActionType.PauseGraphAndSimulation, null);
        
        // Финализируем данные (подсчет результатов на текущий момент)
        ToDoManager.Instance.HandleAction(ActionType.FinalizeTestData, null);
        ToDoManager.Instance.HandleAction(ActionType.ShowSmallReport, null); // Показать отчет

        // Переходим в состояние "Остановлен" (обычно под нагрузкой, т.к. прервали)
        // Соответствует TestAborted или TestResult_UnderLoad
        context.TransitionToState(new TestFinished_SampleUnderLoadState(context));
        
        //ToDoManager.Instance.HandleAction(ActionType.ShowHintText, new ShowHintArgs("Испытание принудительно остановлено."));
    }

    // --- 3. АВТОМАТИЧЕСКОЕ ЗАВЕРШЕНИЕ (Машина порвала образец) ---
    public override void OnTestFinished()
    {
        // Действия те же: финализация данных
        ToDoManager.Instance.HandleAction(ActionType.FinalizeTestData, null);
        ToDoManager.Instance.HandleAction(ActionType.ShowSmallReport, null);

        // 2. ВЫБИРАЕМ ПУТЬ
        // Если это сжатие — образец цел и под нагрузкой.
        if (monitor.CurrentGeneralTestType == TestType.Compression)
        {
            // Идем в "Опасное" состояние, где работает логика разгрузки
            context.TransitionToState(new TestFinished_SampleUnderLoadState(context));
            ToDoManager.Instance.HandleAction(ActionType.ShowHintText, new ShowHintArgs("Испытание завершено. Выполните разгрузку."));
        }
        else
        {
            // Если растяжение — считаем, что образец порвался (нагрузка 0)
            context.TransitionToState(new TestResult_SampleSafeState(context));
            ToDoManager.Instance.HandleAction(ActionType.ShowHintText, new ShowHintArgs("Испытание завершено успешно."));
        }
    }

    public override void OnMachineForceLimitReached()
    {
        // 1. Останавливаем всё
        ToDoManager.Instance.HandleAction(ActionType.PauseGraphAndSimulation, null);
        
        // 2. Считаем результаты
        ToDoManager.Instance.HandleAction(ActionType.FinalizeTestData, null);
        ToDoManager.Instance.HandleAction(ActionType.ShowSmallReport, null);

        // 3. Показываем причину
        ToDoManager.Instance.HandleAction(ActionType.ShowHintText, new ShowHintArgs("Достигнут предел прочности машины! Стоп."));

        // 4. Переходим в состояние "Под нагрузкой" (так как прервали силой)
        context.TransitionToState(new TestFinished_SampleUnderLoadState(context));
    }

    public override void OnExtensometerRequest(bool isAttach)
    {
        // Проверяем, включил ли юзер эту опцию
        if (!monitor.IsExtensometerEnabledByUser) return;

        Debug.Log($"[TestRunning] Запрос на экстензометр: {(isAttach ? "Install" : "Remove")}");
        
        // Переходим в состояние-питстоп
        context.TransitionToState(new ExtensometerActionState(context, isAttach));
    }
}