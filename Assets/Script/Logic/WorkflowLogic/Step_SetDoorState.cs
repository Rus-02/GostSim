using System.Collections;
using System.Linq;
using UnityEngine;

public class Step_SetDoorState : IWorkflowStep
{
    private readonly bool _shouldBeOpen;

    public Step_SetDoorState(bool shouldBeOpen)
    {
        _shouldBeOpen = shouldBeOpen;
    }

    public IEnumerator Execute(WorkflowContext context)
    {
        // 1. ПРОВЕРКА ПЛАНА (Умная защита)
        // Пытаемся достать план смены оснастки
        if (context.HasData(Step_CalculateFixturePlan.CTX_KEY_PLAN))
        {
            var plan = context.GetData<FixtureChangePlan>(Step_CalculateFixturePlan.CTX_KEY_PLAN);
            
            // Если план есть, но он ПУСТОЙ (ничего не меняем) -> выходим, дверь не трогаем
            if (plan != null && !HasWork(plan))
            {
                // Debug.Log("[Step_SetDoorState] Работы по плану нет, дверь не трогаем.");
                yield break;
            }
        }

        // 2. СТАНДАРТНАЯ ЛОГИКА
        bool areClosed = SystemStateMonitor.Instance.AreDoorsClosed;
        bool currentlyOpen = !areClosed;

        if (currentlyOpen == _shouldBeOpen)
        {
            yield break;
        }

        Debug.Log($"[Step_SetDoorState] Переключение дверей в: {(_shouldBeOpen ? "ОТКРЫТО" : "ЗАКРЫТО")}");
        
        ToDoManager.Instance.HandleAction(ActionType.SetDoorStateAction, null);
        yield return new WaitForSeconds(0.6f); 
    }

    // Вспомогательный метод проверки: "Надо ли что-то делать?"
    private bool HasWork(FixtureChangePlan plan)
    {
        if (plan.MainFixturesToRemove != null && plan.MainFixturesToRemove.Count > 0) return true;
        if (plan.MainFixturesToInstall != null && plan.MainFixturesToInstall.Count > 0) return true;
        if (plan.InternalFixturesToInstall != null && plan.InternalFixturesToInstall.Count > 0) return true;
        if (plan.FixturesToPreInitialize != null && plan.FixturesToPreInitialize.Count > 0) return true;
        
        return false;
    }
}