using System.Collections;
using UnityEngine;

public class Step_FinalizeFixtureChange : IWorkflowStep
{
    public IEnumerator Execute(WorkflowContext context)
    {
        Debug.Log("[Step_FinalizeFixtureChange] Начало финализации...");

        // 1. Ищем хендлер: Сначала в контексте (для VSM), потом в CSM
        ITestLogicHandler handler = context.GetData<ITestLogicHandler>(Step_CalculateFixturePlan.CTX_KEY_HANDLER_OVERRIDE);
        
        if (handler == null)
        {
            handler = context.CSM.CurrentTestLogicHandler;
        }

        // 2. Выполнение пост-команд
        if (handler != null)
        {
            var finalizationCommands = handler.GetPostChangeFinalizationCommands();
            if (finalizationCommands != null && finalizationCommands.Count > 0)
            {
                foreach (var command in finalizationCommands)
                {
                    ToDoManager.Instance.HandleAction(command.Action, command.Args);
                }
            }
        }
        else
        {
            Debug.LogWarning("[Step_FinalizeFixtureChange] Handler не найден. Финализация пропущена частично.");
        }

        // 3. Фиксация LogicHandler в системе (Только для CSM, в VSM отправляем null или игнорируем)
        // Если мы в VSM (handler в контексте), то глобально менять хендлер машины не обязательно,
        // но ToDoManager ожидает аргумент.
        
        // ВАЖНО: В режиме VSM мы не хотим сбивать глобальный хендлер машины на фиктивный.
        // Поэтому SetCurrentLogicHandler вызываем только если мы НЕ в VSM (нет оверрайда).
        bool isVsmMode = context.HasData(Step_CalculateFixturePlan.CTX_KEY_HANDLER_OVERRIDE);
        
        if (!isVsmMode)
        {
            ToDoManager.Instance.HandleAction(ActionType.SetCurrentLogicHandler, null);
        }

        // 4. Снятие флага "Занято"
        SystemStateMonitor.Instance?.ReportFixtureChangeStatus(false);

        Debug.Log("[Step_FinalizeFixtureChange] Завершено.");
        yield return null;
    }
}