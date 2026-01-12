using System.Collections;
using System.Linq;
using UnityEngine;

public class Step_PrepareForTeardown : IWorkflowStep
{
    public IEnumerator Execute(WorkflowContext context)
    {
        var plan = context.GetData<FixtureChangePlan>(Step_CalculateFixturePlan.CTX_KEY_PLAN);
        if (plan == null || plan.MainFixturesToRemove == null || plan.MainFixturesToRemove.Count == 0)
        {
            yield break;
        }

        // 1. Получаем хендлер, который СЕЙЧАС управляет машиной (старый)
        // Обращаемся через FixtureController, так как CSM.LogicHandler уже может быть нацелен на новый тест
        var activeHandler = FixtureController.Instance.GetActiveLogicHandler();

        if (activeHandler != null)
        {
            // 2. Сортировка списка удаления (сначала дети, потом родители)
            // Прямая модификация списка в объекте Plan
            var orderedList = activeHandler.CreateTeardownPlan(plan.MainFixturesToRemove);
            plan.MainFixturesToRemove = orderedList;

            // 3. Выполнение подготовительных команд (Unclamp и т.д.)
            var prepCommands = activeHandler.GetPreChangePreparationCommands(plan.MainFixturesToRemove);
            
            if (prepCommands != null && prepCommands.Count > 0)
            {
                Debug.Log("[Step_PrepareForTeardown] Выполнение команд подготовки к снятию...");
                
                bool needsWait = false;
                foreach (var cmd in prepCommands)
                {
                    ToDoManager.Instance.HandleAction(cmd.Action, cmd.Args);
                    
                    // Если была команда на разжатие, нужно будет подождать
                    if (cmd.Action == ActionType.UnclampUpperGrip || cmd.Action == ActionType.UnclampLowerGrip)
                    {
                        needsWait = true;
                    }
                }

                // Ждем окончания анимации захватов, если она была запущена
                if (needsWait)
                {
                    // Используем монитор для проверки статуса анимации
                    yield return new WaitUntil(() => !SystemStateMonitor.Instance.IsClampAnimating);
                }
            }
        }
    }
}