using UnityEngine;

public class TestPausedState : StateBase
{
    public TestPausedState(CentralizedStateManager context) : base(context) { }

    public override TestState StateEnum => TestState.TestPaused;

    // --- ВОЗОБНОВЛЕНИЕ (Кнопка Старт) ---
    public override void OnStartTest()
    {
        // Команда возобновления
        ToDoManager.Instance.HandleAction(ActionType.ResumeGraphAndSimulation, null);
        
        // Обратно в бег
        context.TransitionToState(new TestRunningState(context));
    }

    // --- СТОП (Кнопка Стоп) ---
    public override void OnStopTest()
    {
        // Логика идентична стопу из Running
        ToDoManager.Instance.HandleAction(ActionType.FinalizeTestData, null);
        ToDoManager.Instance.HandleAction(ActionType.ShowSmallReport, null);
        
        context.TransitionToState(new TestFinished_SampleUnderLoadState(context));
    }
    
    // В паузе также работает только Пауза (как переключатель)
    public override void OnPauseTest()
    {
        // Если нажали Паузу еще раз -> это Resume
        OnStartTest();
    }
}