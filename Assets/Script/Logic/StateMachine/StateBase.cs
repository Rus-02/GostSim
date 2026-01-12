using UnityEngine;
using System;

/// <summary>
/// Базовый класс состояния.
/// По умолчанию все действия ЗАПРЕЩЕНЫ (пустые методы).
/// Конкретные состояния переопределяют только то, что им можно.
/// </summary>
public abstract class StateBase
{
    protected CentralizedStateManager context;
    
    // Ссылка на монитор для удобства (чтобы не писать каждый раз Instance)
    protected SystemStateMonitor monitor => SystemStateMonitor.Instance;

    protected StateBase(CentralizedStateManager context)
    {
        this.context = context;
    }

    // Обязательное поле: какое это состояние в Enum
    public abstract TestState StateEnum { get; }

    // --- Жизненный цикл ---
    public virtual void OnEnter() 
    {
        // Логирование переходов полезно для отладки
        Debug.Log($"[CSM State Machine] Enter: {StateEnum}");
    }
    
    public virtual void OnExit() { }
    
    // --- Update (если нужно что-то чекать каждый кадр) ---
    public virtual void OnUpdate() { }

    // =========================================================================
    // ОБРАБОТЧИКИ СОБЫТИЙ (ACTIONS)
    // =========================================================================

    // 1. Настройка
    public virtual void OnTestParametersConfirmed() { } // Старт настройки из меню

    // 2. Движение Траверсы (Элмех)
    public virtual void OnTraverseMove(float direction, SpeedType speed) { } // Нажатие
    public virtual void OnTraverseStop() { }                                 // Отпускание
    public virtual void OnAutoApproach() { }                                 // Кнопка автоподвода
    public virtual void OnTraverseSpeedAdjust(bool increase) { }             // Изменение скорости движения
    public virtual void OnApproachCompleted() { }                            // Завершение автоподвода

    // 3. Движение Рамы (Гидравлика)
    public virtual void OnHydraulicMove(float direction, SpeedType speed) { }
    public virtual void OnHydraulicStop() { }                                // Кнопка Стоп или Отмена
    public virtual void OnOperationFinished() { }

    // 4. Захваты
    public virtual void OnClampAction(GripType type, bool clamp) { }         // Входящая команда от кнопки UI
    public virtual void OnClampAnimationStarted() { }                        // Реакция на событие начала анимации захватов
    public virtual void OnClampAnimationFinished() { }                       // Реакция на событие окончания анимации захватов

    // 5. Тест
    public virtual void OnStartTest() { }
    public virtual void OnPauseTest() { }
    public virtual void OnStopTest() { }                                     // Кнопка Стоп
    public virtual void OnTestFinished() { }                                 // Сигнал от контроллера, что тест кончился
    public virtual void OnMachineForceLimitReached() { }                     // Логика остановки теста по достижению лимита силы

    // 6. Образец и Прочее
    public virtual void OnSampleAction() { }                                 // Установить/Снять образец
    public virtual void OnExtensometerRequest(bool isAttach) { }             // Экшны экстензометра СНЯТИЕ/УСТАНОВКА
    public virtual void OnExtensometerConfirm() { }                          // Нажатие кнопки действия ("Установить/Снять экстензометр")
    public virtual void OnUnloadSample() { }                                 // Разгрузить образец

    // 7. Глобальный  выход в режим просмотра
    
    // --- 1. КНОПКА "ДОМОЙ" (Базовая реализация для всех) ---
    public virtual void OnViewHomeConfirm()
    {
        // Принудительный выход работает везде одинаково
        PerformExitToIdle();
    }

    // --- 2. КНОПКА "ЗАВЕРШИТЬ" (По умолчанию отключена) ---
    public virtual void OnFinishTestCommand() { }


    // --- ОБЩАЯ ЛОГИКА СБРОСА ---
    protected void PerformExitToIdle()
    {
        // 1. Разжимаем захваты (если надо и это не проп. образцы)
        // (Логика проверки типа теста должна быть доступна через монитор или контекст)
        bool isProportional = monitor.CurrentTestLogicHandler is TensileProportionalLogicHandler;
        
        if (!isProportional && monitor.IsSampleInPlace)
        {
            if (monitor.IsUpperGripClamped) ToDoManager.Instance.HandleAction(ActionType.UnclampUpperGrip, null);
            if (monitor.IsLowerGripClamped) ToDoManager.Instance.HandleAction(ActionType.UnclampLowerGrip, null);
        }

        // 2. Сброс симуляции
        context.PerformFullSimulationReset();
        
        // 3. СБРОС UI
        context.SwitchUIToViewMode();

        // 4. Переход в Idle
        context.TransitionToState(new IdleState(context));
    }
}