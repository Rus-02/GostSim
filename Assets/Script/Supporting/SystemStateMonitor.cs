using System;
using UnityEngine;
using System.Collections.Generic;

public enum ApplicationMode
{
    ViewMode,  // Режим Просмотра
    TestMode   // Режим Испытания
}

public class SystemStateMonitor : MonoBehaviour
{
    public static SystemStateMonitor Instance { get; private set; }

    // --- Глобальный режим работы ---
    public ApplicationMode CurrentApplicationMode { get; private set; }

    // --- Состояние из CSM ---
    public TestState CurrentTestState { get; private set; }
    public TestConfigurationData CurrentTestConfig { get; private set; }
    public TestType CurrentGeneralTestType { get; private set; }
    public ITestLogicHandler CurrentTestLogicHandler { get; private set; }
    public bool IsSampleInPlace { get; private set; }
    public bool IsUpperGripClamped { get; private set; }
    public bool IsLowerGripClamped { get; private set; }
    public GameObject CurrentSampleInstance { get; private set; }
    public Vector3 ReqDrivePos { get; private set; }
    public Vector3 ReqUndrivePos { get; private set; }
    public ApproachActionType ReqActionType { get; private set; }

    // --- Состояние из VSM ---
    public bool IsDropdownMenuActive { get; private set; }
    public bool AreDoorsClosed { get; private set; }

    // --- Состояние из MachineController ---
    public float OriginMinLimitY { get; private set; }
    public float OriginMaxLimitY { get; private set; }
    public float CurrentMinTraverseLimitY { get; private set; }
    public float CurrentMaxTraverseLimitY { get; private set; }
    public bool IsDynamicLimitsActive { get; private set; }
    public float LastApproachTargetZ { get; private set; } // Локкльные координаты траверсы
    public float LastApproachTargetWorldY { get; private set; } // Мировые координаты траверсы
    public bool IsTraverseMovingManually { get; private set; } // Флаг ручного перемещения траверсы
    public float CurrentHydroDisplacement { get; private set; } // Текущее смещение силовой рамы от стартовой точки


    public float CurrentTraverseY => Application.isPlaying && MachineController.Instance?.MovingTraverseRoot != null
    ? MachineController.Instance.MovingTraverseRoot.position.y
    : 0f;

    // --- Состояние из SetupPanel ---
    public string SelectedTemplateName { get; private set; }
    public string SelectedMaterialName { get; private set; }
    public SampleForm SelectedShape { get; private set; }
    public Dictionary<string, float> CurrentSampleParameters { get; private set; } = new Dictionary<string, float>();
    public bool IsSetupPanelValid { get; private set; }
    public float CalculatedArea { get; private set; }
    public TestSpeedMode SelectedSpeedMode { get; private set; }
    public MaterialPropertiesAsset SelectedMaterial { get; private set; }
    public float CurrentClampingLength { get; private set; }
    public bool IsExtensometerEnabledByUser { get; private set; }
    public string ReportGroupName { get; private set; }
    public string ReportBatchNumber { get; private set; }
    public string ReportMarking { get; private set; }
    public string ReportNotes { get; private set; }

    // --- Состояние из GraphController ---
    
    public GraphController.GraphState CurrentGraphState { get; private set; }    

    // Текущие значения
    public float CurrentRelativeStrain_Percent { get; private set; }
    public float CurrentForce_kN { get; private set; }
    public float MaxForceInTest_kN { get; private set; }   

    // Ключевые точки теста
    public float UTS_RelativeStrain_Percent { get; private set; } // Деформация (%) при макс. нагрузке
    public float Rupture_RelativeStrain_Percent { get; private set; } // Деформация (%) при разрыве
    public float ProportionalityLimit_kN { get; private set; } // Предел пропорциональности (кН)

    // Флаги событий
    public bool WasExtensometerAttachRequested { get; private set; }
    public bool WasExtensometerRemoveRequested { get; private set; }

    // Ивенты экстензометра
    public event Action OnExtensometerAttachRequested;
    public event Action OnExtensometerRemoveRequested;


    // --- Состояние из FixtureController & CSM ---
    public List<string> AllInstalledFixtureIDs { get; private set; } = new List<string>();
    public List<string> FixturesRequiredForTest { get; private set; } = new List<string>();

    // --- Состояние процессов и блокировок ---
    public bool IsFixtureChangeInProgress { get; private set; }
    public bool IsApproachInProgress { get; private set; }
    public bool IsScenarioExecuting { get; private set; }
    public bool IsClampAnimating { get; private set; }
    public bool IsHydraulicBufferReady { get; private set; }
    public bool IsSampleUnloaded { get; private set; }
    public bool IsMachineReadyForSetup { get; private set; } = true; // Глобальный флаг готовности машины к настройке
    public string MachineNotReadyReason { get; private set; } = string.Empty; // Причина неготовности
    public bool IsPowerUnitActive { get; private set; } // Состояние масляного насоса



    // --- Различные мелочи ---
    public bool IsPromptPanelCollapsed { get; private set; }
    public string CurrentPromptKey { get; private set; }

    // --- Секция Событий (Events) ---
    public event System.Action<bool> OnDropdownMenuActivityChanged;
    public event Action<bool> OnTraverseManualMoveStateChanged;
    public event Action OnApproachCalcRequested;
    
    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        LastApproachTargetZ = float.NaN;
    }

    // --- Методы-репортеры для глобального режима ---
    public void ReportApplicationMode(ApplicationMode mode) => CurrentApplicationMode = mode;

    // --- Методы-репортеры для CSM ---
    public void ReportTestState(TestState state) => CurrentTestState = state;
    public void ReportTestConfiguration(TestConfigurationData config) => CurrentTestConfig = config;
    public void ReportGeneralTestType(TestType type) => CurrentGeneralTestType = type;
    public void ReportTestLogicHandler(ITestLogicHandler handler) => CurrentTestLogicHandler = handler;
    public void ReportSamplePresence(bool isPresent) => IsSampleInPlace = isPresent;
    public void ReportGripState(GripType grip, bool isClamped)
    {
        if (grip == GripType.Upper) IsUpperGripClamped = isClamped;
        else IsLowerGripClamped = isClamped;
    }
    public void ReportCurrentSampleInstance(GameObject sampleInstance) => CurrentSampleInstance = sampleInstance;

    public void RequestApproachCalculation(Vector3 drive, Vector3 undrive, ApproachActionType type)
    {
        ReqDrivePos = drive;
        ReqUndrivePos = undrive;
        ReqActionType = type;
        OnApproachCalcRequested?.Invoke(); // МК проснется тут
    }

    // --- Методы-репортеры для VSM ---
    public void ReportDropdownMenuActivity(bool isActive)
    {
        if (IsDropdownMenuActive == isActive) return;        
        IsDropdownMenuActive = isActive;
        OnDropdownMenuActivityChanged?.Invoke(isActive);
    }
    public void ReportDoorState(bool isClosed) => AreDoorsClosed = isClosed;

    // --- Методы-репортеры для MachineController ---
    public void ReportOriginLimits(float min, float max)
    {
        OriginMinLimitY = min;
        OriginMaxLimitY = max;
    }

    public void ReportTraverseLimits(float min, float max, bool isDynamic)
    {
        CurrentMinTraverseLimitY = min;
        CurrentMaxTraverseLimitY = max;
        IsDynamicLimitsActive = isDynamic;
    }

    public void ReportApproachTarget(float targetZ_Local, float targetY_World)
    {
        LastApproachTargetZ = targetZ_Local;
        LastApproachTargetWorldY = targetY_World;
    }
    
    public void ReportExtensometerUsage(bool isEnabled) => IsExtensometerEnabledByUser = isEnabled;
    public void ReportTraverseManualMoveStatus(bool isMoving)
    {
        if (IsTraverseMovingManually == isMoving) return; // Не вызываем событие, если состояние не изменилось

        IsTraverseMovingManually = isMoving;
        OnTraverseManualMoveStateChanged?.Invoke(isMoving); // Вызываем событие
    }

    public void ReportHydroDisplacement(float displacement) => CurrentHydroDisplacement = displacement;


    // --- Методы-репортеры для SetupPanel ---
    public void ReportSetupSelection(string template, string material, SampleForm shape)
    {
        SelectedTemplateName = template;
        SelectedMaterialName = material;
        SelectedShape = shape;
    }

    public void ReportSampleParameters(Dictionary<string, float> parameters, float area)
    {
        CurrentSampleParameters = parameters ?? new Dictionary<string, float>();
        CalculatedArea = area;
    }

    public void ReportSetupValidity(bool isValid) => IsSetupPanelValid = isValid;
    public void ReportSpeedMode(TestSpeedMode mode) => SelectedSpeedMode = mode;
    public void ReportSelectedMaterial(MaterialPropertiesAsset materialAsset) => SelectedMaterial = materialAsset;
    public void ReportClampingLength(float length) => CurrentClampingLength = length;

    public void ReportHeaderInfo(string group, string batch, string marking, string notes)
    {
        ReportGroupName = group;
        ReportBatchNumber = batch;
        ReportMarking = marking;
        ReportNotes = notes;
    }

    // --- НОВОЕ: Методы-репортеры для GraphController ---
    public void ReportGraphState(GraphController.GraphState state) => CurrentGraphState = state;   

    public void ReportGraphPlotPoint(float relativeStrain, float force)
    {
        CurrentRelativeStrain_Percent = relativeStrain;
        CurrentForce_kN = force;
    }

    public void ReportGraphKeyPoints(float utsStrain, float ruptureStrain, float propLimit_kN)
    {
        UTS_RelativeStrain_Percent = utsStrain;
        Rupture_RelativeStrain_Percent = ruptureStrain;
        ProportionalityLimit_kN = propLimit_kN;
    }

    public void ReportMaxForce(float force) { if (force > MaxForceInTest_kN) MaxForceInTest_kN = force; }
    
    public void ReportExtensometerEvent(bool attachRequested, bool removeRequested)
    {
        if (attachRequested && !WasExtensometerAttachRequested)
        {
            WasExtensometerAttachRequested = true;
            OnExtensometerAttachRequested?.Invoke();
        }
        if (removeRequested && !WasExtensometerRemoveRequested)
        {
            WasExtensometerRemoveRequested = true;
            OnExtensometerRemoveRequested?.Invoke();
        }
    }

    // --- Методы-репортеры для FixtureController & CSM ---
    public void ReportAllInstalledFixtures(List<string> installedIDs)
    {
        AllInstalledFixtureIDs = installedIDs ?? new List<string>();
    }

    public void ReportRequiredFixtures(List<string> requiredIDs)
    {
        FixturesRequiredForTest = requiredIDs ?? new List<string>();
    }

    public void ReportFixtureChangeStatus(bool inProgress) => IsFixtureChangeInProgress = inProgress;
    public void ReportApproachStatus(bool inProgress) => IsApproachInProgress = inProgress;
    public void ReportScenarioStatus(bool isExecuting) => IsScenarioExecuting = isExecuting;
    public void ReportClampAnimation(bool isAnimating) => IsClampAnimating = isAnimating;
    public void ReportHydraulicBufferReady(bool isReady) => IsHydraulicBufferReady = isReady;
    public void ReportSampleUnloaded(bool isUnloaded) => IsSampleUnloaded = isUnloaded;

    // --- Методы-репортеры для различных мелочей ---
    public void ReportPromptPanelState(bool isCollapsed, string currentKey)
    {
        IsPromptPanelCollapsed = isCollapsed;
        CurrentPromptKey = currentKey;
    }

    /// <summary>
    /// Сообщает о готовности машины к операциям настройки.
    /// </summary>
    public void ReportMachineReadyStatus(bool isReady, string reason)
    {
        IsMachineReadyForSetup = isReady;
        MachineNotReadyReason = reason;
    }

    public void ReportPowerUnitState(bool isActive)
    {
        if (IsPowerUnitActive != isActive) { IsPowerUnitActive = isActive; }
    }

    public bool IsTraverseAtTarget
    {
        get
        {
            if (float.IsNaN(LastApproachTargetZ)) return false;
            
            // Сравниваем Текущий Y (World) и Цель (Local Z/Scalar)
            // Раз они совпадают визуально, то сработает.
            // Допуск 0.002f (2 мм) для надежности (иногда float плавает)
            return Mathf.Abs(CurrentTraverseY - LastApproachTargetZ) <= 0.002f;
        }
    }



    #region Методы сброса состояния

    /// <summary>
    /// УРОВЕНЬ 1: "Мягкий" сброс.
    /// Сбрасывает все, что связано с НАСТРОЙКОЙ теста (выбранный шаблон, материал, параметры образца),
    /// но НЕ трогает состояние симуляции (график, контроллер теста).
    /// </summary>
    public void ResetTestSetupState()
    {
        ReportSetupSelection(null, null, SampleForm.Неопределено);
        ReportSampleParameters(new Dictionary<string, float>(), 0f);
        ReportSetupValidity(false);
        ReportSpeedMode(TestSpeedMode.DeformationRate);
        ReportRequiredFixtures(new List<string>());
        ReportSelectedMaterial(null);
        MaxForceInTest_kN = 0;
        ReportClampingLength(0f);
        
        Debug.LogWarning("[SystemStateMonitor] УРОВЕНЬ 1: Состояние настройки теста сброшено.");
    }

    /// <summary>
    /// УРОВЕНЬ 2: "Жесткий" сброс.
    /// Полный сброс СИМУЛЯЦИИ в текущей сцене. Вызывает сброс всех модулей (график, тест контроллер).
    /// НЕ трогает постоянное состояние (установленная оснастка, положение траверсы).
    /// </summary>
    public void ResetSimulationState()
    {
        // 1. Сначала сбрасываем все настройки
        ResetTestSetupState();
        ReportGeneralTestType(TestType.None);

        // 2. Затем отправляем команды на сброс всем остальным модулям
        if (ToDoManager.Instance != null)
        {
            ToDoManager.Instance.HandleAction(ActionType.ResetGraphAndSimulation, null);
            ToDoManager.Instance.HandleAction(ActionType.ResetTestController, null);
            ToDoManager.Instance.HandleAction(ActionType.ResetHydraulicBuffer, null);
            ToDoManager.Instance.HandleAction(ActionType.ClearLastReport, null);
        }
        
        // 3. Сбрасываем состояние самого теста
        ReportTestState(TestState.ReadyForSetup); // или TestState.Idle
        ReportApplicationMode(ApplicationMode.ViewMode);
        ReportCurrentSampleInstance(null);
        ReportTestLogicHandler(null);

        Debug.LogWarning("[SystemStateMonitor] УРОВЕНЬ 2: Состояние симуляции полностью сброшено.");
    }

    /// <summary>
    /// УРОВЕНЬ 3: Глобальный сброс.
    /// Сбрасывает состояние, которое не должно "переживать" смену машины или выход из сцены.
    /// В данный момент ничего не делает, но является заделом на будущее.
    /// </summary>
    public void ResetPersistentState()
    {
        // Пока здесь нечего сбрасывать, но метод уже есть.
        // Здесь можно было бы сбросить, например, AllInstalledFixtureIDs, если бы мы хотели
        // симулировать "чистую" сцену при каждом входе.
        Debug.LogWarning("[SystemStateMonitor] УРОВЕНЬ 3: Глобальный сброс вызван (пока пустой).");
    }

    #endregion
}