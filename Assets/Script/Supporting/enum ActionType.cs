public enum ActionType
{
    // UI Tab Buttons - Кнопки управления вкладками интерфейса
    ShowControlTabAction,       // Кнопка "Управление" (отобразить вкладку "Управление")
    ShowTestTabAction,          // Кнопка "Испытание" (отобразить вкладку "Испытание")
    ShowResultsTabAction,       // Кнопка "Результаты" (отобразить вкладку "Результаты")
    ActivateUITab,           // Активация вкладки UI 

    // Traverse Control Buttons - Кнопки управления траверсой (вкладка "Управление")
    FastlyUpAction,             // Кнопка "Вверх быстро"
    FastlyDownAction,           // Кнопка "Вниз быстро"
    SlowlyUpAction,             // Кнопка "Вверх медленно"
    SlowlyDownAction,           // Кнопка "Вниз медленно"
    IncreaseTraverseSpeedAction, // Кнопка "Увеличить скорость"
    DecreaseTraverseSpeedAction, // Кнопка "Уменьшить скорость"
    StopTraverseAction,         // Кнопка "Стоп траверсы"
    ApproachTraverseAction,      // Кнопка "Подвести траверсу"

    // Test Start/Stop Buttons - Кнопки запуска и остановки теста (вкладка "Испытание")
    SampleButtonAction,    // Кнопка "Образец" (разместить образец)
    StartTestAction,            // Кнопка "Старт" (запуск испытания)
    PauseTestAction,            // Кнопка "Пауза" (пауза испытания)
    StopTestAction,             // Кнопка "Стоп" (остановка испытания)

    // Test Result Buttons - Кнопки отображения результатов (вкладка "Результат")
    ProtocolAction,             // Кнопка "Протокол" (остановка испытания)
    FinishTestAction,           // Кнопка "Закончить" (завершение испытания после остановки)

    // --- Новые ActionType для управления UI, добавлены здесь, чтобы не нарушать порядок до этого места ---
    EnableSampleButtonUI,       // Включить кнопку "Образец" UI
    DisableSampleButtonUI,      // Выключить кнопку "Образец" UI
    SetTestTypeButtonsDisabledUI, // Установить кнопки выбора типа теста в Disabled, кроме выбранной
    SetUIContainerActive, // Установить контейнер UI активным

    SampleButtonUI,             // Идентификатор ActionType для кнопки "Образец" в UI
    TestTypeButtonsUI,          // Идентификатор ActionType для группы кнопок "Test Type" в UI


    // Общие/вспомогательные действия UI (если нужны еще кнопки, не привязанные к вкладкам)
    UpdateUIState,              // Обновление состояния UI (например, вкл/выкл кнопок, если нужно кнопкой)
    UpdateUIButtonVisuals,


    // Действия, которые могут быть вызваны не кнопками, или общие "логические" действия (если понадобятся)
    MoveTraverse,                // Общее действие "Переместить траверсу" (может вызываться не кнопкой)
    MoveTraverseToPosition,     // Общее действие "Переместить траверсу в позицию"
    AdjustSpeed,                // Общее действие "Изменить скорость траверсы"
    SetupTest,                  // Общее действие "Подготовка к тесту"
    ControlTest,                // Общее действие "Управление тестом"


    // Workflow Events - События workflow, сигнализирующие о завершении этапов системы (НЕ КНОПКИ, в конце списка)
    TraverseApproachCompleted,   // Подвод траверсы к начальной позиции завершен (событие от MachineController)
    RequestApproachCalculation, // Запрос на расчет подводки траверсы 
    FixturePlacementConfirmed,  // Размещение оснастки подтверждено (событие от TestController)
    //SamplePlacementConfirmed,    // Размещение образца подтверждено (событие от TestController)

    // Test Manager Actions - Действия, которые ранее вызывались напрямую у TestManager
    SetCurrentTestType,         // Установить текущий тип теста

    // Test Controller Actions - Действия, которые ранее вызывались напрямую у TestController
    BeginTestByType,            // Начать тест, передавая тип теста
    PlaceSampleByType,          // Разместить образец, передавая точку размещения
    PlaceFixtureByIdentifier,     // Разместить оснастку, передавая идентификатор оснастки
    RemoveFixtureByIdentifier,     //Снять оснастку, передавая идентификатор
    ResetSampleVisuals, // Сбросить разделение образца и перепривязку родителя

    // Fixture Animation Actions - Анимации снятия/устанвки вкладышей
    PlayFixtureAnimationAction,
    InitializeFixturesAtStartup,

    // Setup Panel Actions
    SampleSetupAction, // Кнопка "Образец"
    ShowTestSettingsPanelAction, // Кнопка "Шаблоны"
    ApplySampleSetupSettingsAction, // Применить настройки образца
    CloseSettingsPanelAction, // Сбросить настройки образца
    SetSetupActionButtonsVisibilityAction, // Установить видимость кнопок кнопки Play

    // Обработка графика
    UpdateMachineVisuals, // Задача для ToDoManager: Обновить визуализацию машины/образца на основе данных от GraphController
    StartGraphAndSimulation,    // Запустить отрисовку графика и связанную симуляцию
    PauseGraphAndSimulation,    // Поставить график/симуляцию на паузу
    ResumeGraphAndSimulation,   // Возобновить график/симуляцию с паузы
    StopGraphAndSimulation,      // Остановить и сбросить график/симуляцию
    InitializeTestController, // Инициализация TestController
    UpdateSampleVisuals, // Обновить визуализацию образца на основе данных от GraphController
    NotifyTestControllerRupture, // Уведомить TestController о разрыве образца
    NotifyTestControllerAnimationEnd, // Уведомить TestController о завершении анимации
    FinalizeTestData, // Завершить тестовые данные
    UpdateUIIdleState, // Обновить состояние UI
    UpdateUITestSelectedState, // Обновить состояние UI О режиме теста
    UpdateUIReadyState, // Обновить состояние UI О режиме готовности
    UpdateUITraverseMovingState, // Обновить состояние UI О режиме движения траверсы
    UpdateUISamplePlacedState, // Обновить состояние UI О режиме размещения образца
    UpdateUITestRunningState, // Обновить состояние UI О режиме теста
    UpdateUITestPausedState, // Обновить состояние UI О режиме паузы
    UpdateUITestCompletedState, // Обновить состояние UI О режиме завершения теста
    UpdateUIConfiguringState, // Обновить состояние UI О режиме конфигурации
    UpdateUIErrorState, // Обновить состояние UI О режиме ошибки
    ResetTestController, // Сбросить TestController
    ResetGraphAndSimulation, // Сбросить график и симуляцию
    ForceStopAndResetTest, // Принудительно остановить и сбросить тест
    PrepareGraph, // Подготовить графики данные для графика

    //Обработка зажимов траверс
    ClampUpperGrip, // Зажать верхний захват
    ClampLowerGrip, // Зажать нижний захват
    UnclampUpperGrip, // Отжать верхний захват
    UnclampLowerGrip, // Отжать нижний захват

    // Обработка движения траверсы
    SetDynamicTraverseLimits,
    SetOriginMachineLimits,
    UpdateMinLimitPostTension,
    UpdateUIReadyForSetupState,
    UpdateUISamplePlacedAwaitingApproachState,
    UpdateUIReadyToTestState,

    // Обработка UIHelper
    ShowHintText, // Показать текст подсказки
    ClearHints, // Очистить подсказки

    ActivateHydraulicBuffer, // Поднять масляную подушку
    ResetHydraulicBuffer, // Опустить масляную подушку

    UpdatePromptDisplay, // Обновить отображение подсказки
    UpdateHighlight, // Обновить подсветку объектов
    FastlyHydroUp,
    FastlyHydroDown,
    SlowlyHydroUp,
    SlowlyHydroDown,
    HydroStop,
    SetDoorStateAction,
    SetDisplayMode,
    PlaceFixtureWithoutAnimation,
    ReinitializeFixtureZones,
    StoreFinalReport,
    ClearLastReport,
    ShowSmallReport,
    ShowBigReport,
    HideAllReports,
    EnsureFixtureInstallationClearance,
    AnimatePumpOn, // Включение насоса
    AnimatePumpOff, // Выключение насоса
    SetButtonEventType, // Установить для кнопки надпись/имадж/ивент
    ControlExtensometer, // Управление экстензометром
    NotifyReportExtensometerUsage, // Уведомить о использовании экстензометра в отчете
    SetCurrentLogicHandler, // Установить текущий хендлер

    ControlLoader,         // Универсальная команда управления нагружателем (рамой/траверсой)
    SetSupportSystemState, // Универсальная команда управления вспомогательной системой (подушкой)

}