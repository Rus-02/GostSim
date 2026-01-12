using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Step_CalculateFixturePlan : IWorkflowStep
{
    private readonly TestConfigurationData _config;
    
    public const string CTX_KEY_PLAN = "ActiveFixturePlan";
    public const string CTX_KEY_HANDLER_OVERRIDE = "HandlerOverride"; // Ключ для передачи хендлера

    public Step_CalculateFixturePlan(TestConfigurationData config)
    {
        _config = config;
    }

    public IEnumerator Execute(WorkflowContext context)
    {
        // 1. Пытаемся найти хендлер в контексте (для VSM)
        ITestLogicHandler logicHandler = context.GetData<ITestLogicHandler>(CTX_KEY_HANDLER_OVERRIDE);

        // 2. Если в контексте нет, берем глобальный из CSM (для стандартного режима)
        if (logicHandler == null)
        {
            logicHandler = context.CSM.CurrentTestLogicHandler;
        }
        
        if (logicHandler == null)
        {
            Debug.LogError("[Step_CalculateFixturePlan] LogicHandler is null! (Ни в контексте, ни в CSM)");
            yield break;
        }

        // --- Остальной код без изменений ---
        List<string> liveInstalledFixtures = FixtureController.Instance.GetAllInstalledFixtureIDs();
        var shape = SystemStateMonitor.Instance.SelectedShape;

        var plan = logicHandler.CreateFixtureChangePlan(_config, shape, liveInstalledFixtures);

        if (plan == null)
        {
            Debug.LogError("[Step_CalculateFixturePlan] Хендлер вернул NULL план.");
            yield break;
        }

        context.SetData(CTX_KEY_PLAN, plan);
        
        Debug.Log($"[Step_CalculateFixturePlan] План рассчитан. Handler: {logicHandler.GetType().Name}");

        yield return null;
    }
}