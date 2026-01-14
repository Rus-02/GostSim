using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BatchActionMode
{
    RemoveAllOld,      // Снять все старые
    InstallMain,       // Поставить новые (Основной уровень)
    InstallInternal    // Поставить новые (Вложенный уровень)
}

public class Step_BatchFixtureAction : IWorkflowStep
{
    private readonly BatchActionMode _mode;
    private readonly List<string> _pendingAnimations = new List<string>();

    public Step_BatchFixtureAction(BatchActionMode mode)
    {
        _mode = mode;
    }

    public IEnumerator Execute(WorkflowContext context)
    {
        // 1. Достаем ПЛАН из контекста
        var plan = context.GetData<FixtureChangePlan>(Step_CalculateFixturePlan.CTX_KEY_PLAN);

        if (plan == null)
        {
            Debug.LogError("[Step_BatchFixtureAction] План не найден в контексте! Сначала выполните Step_CalculateFixturePlan.");
            yield break;
        }

        // Подписываемся на события анимации
        EventManager.Instance.Subscribe(EventType.FixtureAnimationFinished, this, OnAnimationFinished);
        _pendingAnimations.Clear();

        Debug.Log($"[Step_BatchFixtureAction] Mode: {_mode}");

        // 2. ВЫПОЛНЕНИЕ ДЕЙСТВИЙ (Раздельная логика для каждого списка)
        switch (_mode)
        {
            case BatchActionMode.RemoveAllOld:
                // --- СНЯТИЕ СТАРОЙ ОСНАСТКИ ---
                if (plan.MainFixturesToRemove != null && plan.MainFixturesToRemove.Count > 0)
                {
                    foreach (var id in plan.MainFixturesToRemove)
                    {
                        _pendingAnimations.Add(id);
                        var args = new PlayFixtureAnimationArgs(id, AnimationDirection.Out, context.Requester);
                        ToDoManager.Instance.HandleAction(ActionType.PlayFixtureAnimationAction, args);
                        yield return new WaitForSeconds(0.05f);
                    }
                }
                break;

            case BatchActionMode.InstallMain:
                // --- УСТАНОВКА ОСНОВНОЙ (FixtureInstallationInfo) ---
                if (plan.MainFixturesToInstall != null && plan.MainFixturesToInstall.Count > 0)
                {
                    foreach (var info in plan.MainFixturesToInstall)
                    {
                        if (info.UseAnimation)
                        {
                            _pendingAnimations.Add(info.FixtureId);
                            var args = new PlayFixtureAnimationArgs(info.FixtureId, AnimationDirection.In, context.Requester);
                            ToDoManager.Instance.HandleAction(ActionType.PlayFixtureAnimationAction, args);
                        }
                        else
                        {
                            // Без анимации ставим мгновенно, ждать не надо
                            var args = new PlaceFixtureArgs(info.FixtureId, null, null);
                            ToDoManager.Instance.HandleAction(ActionType.PlaceFixtureWithoutAnimation, args);
                        }
                        yield return new WaitForSeconds(0.05f);
                    }
                }
                break;

            case BatchActionMode.InstallInternal:
                // --- УСТАНОВКА ВЛОЖЕННОЙ ---
                if (plan.InternalFixturesToInstall != null && plan.InternalFixturesToInstall.Count > 0)
                {
                    foreach (var internalItem in plan.InternalFixturesToInstall)
                    {
                        // 1. Ищем родителя (он должен быть уже установлен на этапе InstallMain)
                        GameObject parentObj = FixtureController.Instance.GetInstalledFixtureObjectById(internalItem.ParentFixtureId);

                        if (parentObj == null)
                        {
                            Debug.LogError($"[Step_BatchFixtureAction] Не могу установить '{internalItem.FixtureId}': Родитель '{internalItem.ParentFixtureId}' не найден!");
                            continue;
                        }

                        // 2. Формируем команду с передачей найденного родителя
                        _pendingAnimations.Add(internalItem.FixtureId);
                        
                        var args = new PlayFixtureAnimationArgs(
                            internalItem.FixtureId, 
                            AnimationDirection.In, 
                            context.Requester,
                            parentObj,                       // <--- Передаем родителя
                            internalItem.AttachmentPointName // <--- Передаем имя точки
                        );
                        
                        ToDoManager.Instance.HandleAction(ActionType.PlayFixtureAnimationAction, args);
                        yield return new WaitForSeconds(0.05f);
                    }
                }
                break;
        }

        // 3. ЖДЕМ ЗАВЕРШЕНИЯ (Общая логика для всех)
        float timeout = 20f;
        float timer = 0f;

        while (_pendingAnimations.Count > 0 && timer < timeout)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (_pendingAnimations.Count > 0)
        {
            Debug.LogError($"[Step_BatchFixtureAction] Таймаут ожидания анимации! Зависли: {string.Join(", ", _pendingAnimations)}");
        }

        // 4. Отписка
        EventManager.Instance.Unsubscribe(EventType.FixtureAnimationFinished, this, OnAnimationFinished);
    }

    private void OnAnimationFinished(EventArgs args)
    {
        if (args is FixtureEventArguments fArgs)
        {
            if (_pendingAnimations.Contains(fArgs.FixtureId))
            {
                _pendingAnimations.Remove(fArgs.FixtureId);
            }
        }
    }
}