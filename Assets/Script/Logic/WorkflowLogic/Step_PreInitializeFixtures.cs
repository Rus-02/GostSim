using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Step_PreInitializeFixtures : IWorkflowStep
{
    public IEnumerator Execute(WorkflowContext context)
    {
        var plan = context.GetData<FixtureChangePlan>(Step_CalculateFixturePlan.CTX_KEY_PLAN);
        if (plan == null || plan.FixturesToPreInitialize == null || plan.FixturesToPreInitialize.Count == 0)
        {
            yield break;
        }

        // Проверяем первый элемент, чтобы понять зону
        string firstFixtureId = plan.FixturesToPreInitialize[0];
        var firstData = FixtureManager.Instance.GetFixtureData(firstFixtureId);
        
        if (firstData != null)
        {
            var installedInZone = FixtureController.Instance.GetInstalledFixtureInZone(firstData.fixtureZone);
            
            if (installedInZone == null)
            {
                Debug.Log($"[Step_PreInit] Выполняем пре-инициализацию для {firstData.fixtureZone}");

                // --- УБРАНА ЛОГИКА ДВЕРЕЙ --- 
                // Мы полагаем, что Step_SetDoorState(true) был вызван ДО этого шага.

                // 1. Разделяем на родителей и детей
                var parents = new List<string>();
                var children = new List<string>();

                foreach (var id in plan.FixturesToPreInitialize)
                {
                    var d = FixtureManager.Instance.GetFixtureData(id);
                    if (d != null && string.IsNullOrEmpty(d.parentFixtureId)) parents.Add(id);
                    else children.Add(id);
                }

                // 2. Ставим родителей (без анимации)
                foreach (var pId in parents)
                {
                    ToDoManager.Instance.HandleAction(ActionType.PlaceFixtureWithoutAnimation, new PlaceFixtureArgs(pId, null, null));
                    yield return new WaitForSeconds(0.05f);
                }

                // 3. ПЕРЕСЧЕТ ЗОН
                ToDoManager.Instance.HandleAction(ActionType.ReinitializeFixtureZones, null);
                yield return null; 

                // 4. Ставим детей (без анимации)
                foreach (var cId in children)
                {
                    ToDoManager.Instance.HandleAction(ActionType.PlaceFixtureWithoutAnimation, new PlaceFixtureArgs(cId, null, null));
                    yield return new WaitForSeconds(0.05f);
                }

                // 5. ЧИСТКА ПЛАНА
                plan.MainFixturesToInstall.RemoveAll(info => plan.FixturesToPreInitialize.Contains(info.FixtureId));
                plan.InternalFixturesToInstall.RemoveAll(item => plan.FixturesToPreInitialize.Contains(item.FixtureId));
                
                // Небольшая пауза, чтобы визуально зафиксировать изменение перед следующими шагами
                yield return new WaitForSeconds(0.2f);
            }
        }
    }
}