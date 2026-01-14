using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;

public class CentralizedStateManager : MonoBehaviour
{
    #region State & Core Data
    //================================================================================================================//
    // РЕГИОН 1: СОСТОЯНИЕ И КЛЮЧЕВЫЕ ДАННЫЕ
    // Здесь только то, что описывает ТЕКУЩИЙ момент: состояние, флаги, ссылки на активные объекты и конфигурации.
    //================================================================================================================//

    #region Синглтон
    private static CentralizedStateManager _instance;
    public static CentralizedStateManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<CentralizedStateManager>();
                if (_instance == null)
                {
                    GameObject singletonObject = new GameObject("CentralizedStateManager");
                    _instance = singletonObject.AddComponent<CentralizedStateManager>();
                    Debug.Log("[CentralizedStateManager] Экземпляр создан автоматически.");
                }
            }
            return _instance;
        }
    }
    #endregion

    [Header("Current State")]
    [SerializeField] private TestState _currentTestState = TestState.Idle;
    public TestState CurrentTestState => _currentTestState;

    [Header("Policy Configuration")]
    [Tooltip("Ассет с правилами разрешений/запретов действий и текстами подсказок.")]
    [SerializeField] private ActionPolicy actionPolicyAsset;

    // --- Active Test Data ---
    private ITestLogicHandler _currentTestLogicHandler; 
    public ITestLogicHandler CurrentTestLogicHandler => _currentTestLogicHandler;
    public TestConfigurationData CurrentTestConfiguration => _currentTestConfiguration;
    private TestConfigurationData _currentTestConfiguration;
    private IMachineCalculator _currentCalculator; 

    [Header("Active Test References")]
    [SerializeField] private GameObject _currentSampleInstance = null;
    
    // --- State Flags ---
    private bool _isClampAnimating = false;
    private bool _isTestCurrentlyOrPreviouslyConfigured = false;
    private float _cached_X_UltimateStrength_Percent = -1f;
    private float _cached_X_Rupture_Percent = -1f;
    private StateBase _currentState;

    // --- Coroutine & Process State ---
    private Coroutine _fixtureChangeCoroutine;
    private Coroutine _approachProcessCoroutine = null;
    private Coroutine _scenarioCoroutine = null;
    private List<string> _removingFixtureIds = new List<string>();
    private List<string> _installingFixtureIds = new List<string>();
    private bool isExtensometerRequestedByUser = false;
    private bool _useExtensometerInThisTest = false;
    
    // --- Managers & Constants ---
    private EventManager _eventManager;
    private const float _distanceTolerance = 0.001f;

    // --- WorkflowRunner ---
    private WorkflowRunner _workflowRunner;
    public WorkflowRunner Runner => _workflowRunner;

    // --- Scenario ---
    [Header("Scenarios")]
    [SerializeField] private ScenarioData _defaultScenario;

    #endregion

    #region Initialization & Subscriptions
    //================================================================================================================//
    // РЕГИОН 2: ИНИЦИАЛИЗАЦИЯ И ПОДПИСКИ
    // Все, что касается запуска и остановки скрипта: Awake, Start, OnDestroy и управление подписками на события.
    //================================================================================================================//

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    void Start()
    {
        _eventManager = EventManager.Instance;
        SubscribeToEvents();

        //ToDoManager.Instance?.HandleAction(ActionType.InitializeFixturesAtStartup, null);

        if (_eventManager == null) Debug.LogError("[CSM] EventManager не найден!");
        if (FixtureManager.Instance == null) Debug.LogError("[CSM] FixtureManager не найден!");
        if (SampleManager.Instance == null) Debug.LogError("[CSM] SampleManager не найден!");
        if (TestManager.Instance == null) Debug.LogError("[CSM] TestManager не найден!");
        if (actionPolicyAsset == null) Debug.LogError("[CSM] ActionPolicy ассет не назначен в инспекторе!", this);
        if (SystemStateMonitor.Instance != null) 
        {
            SystemStateMonitor.Instance.OnExtensometerAttachRequested += HandleExtensometerAttachRequest;
            SystemStateMonitor.Instance.OnExtensometerRemoveRequested += HandleExtensometerRemoveRequest;
        }

        if (_defaultScenario != null) { ScenarioExecutor.Instance.StartScenario(_defaultScenario); } else { Debug.LogWarning("[CSM] Дефолтный сценарий не назначен!"); }

        TransitionToState(new InitializingState(this));
        if (_instance != null && _instance != this) { Destroy(gameObject); return; } _instance = this;
        _workflowRunner = new WorkflowRunner(this);
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
        _workflowRunner?.Stop();
        if (_approachProcessCoroutine != null)
        {
            StopCoroutine(_approachProcessCoroutine);
            _approachProcessCoroutine = null;
        }
        if (_scenarioCoroutine != null)
        {
            StopCoroutine(_scenarioCoroutine);
            _scenarioCoroutine = null;
        }
        if (SystemStateMonitor.Instance != null) 
        {
            SystemStateMonitor.Instance.OnExtensometerAttachRequested -= HandleExtensometerAttachRequest;
            SystemStateMonitor.Instance.OnExtensometerRemoveRequested -= HandleExtensometerRemoveRequest;
        }

    }

    private void SubscribeToEvents()
    {
        if (_eventManager == null) return;
        _eventManager.Subscribe(EventType.TestParametersConfirmed, this, HandleTestParametersConfirmed);
        _eventManager.Subscribe(EventType.FastlyUpAction, this, HandleFastlyUpAction);
        _eventManager.Subscribe(EventType.FastlyUpReleased, this, HandleFastlyUpReleased);
        _eventManager.Subscribe(EventType.FastlyDownAction, this, HandleFastlyDownAction);
        _eventManager.Subscribe(EventType.FastlyDownReleased, this, HandleFastlyDownReleased);
        _eventManager.Subscribe(EventType.SlowlyUpAction, this, HandleSlowlyUpAction);
        _eventManager.Subscribe(EventType.SlowlyDownAction, this, HandleSlowlyDownAction);
        _eventManager.Subscribe(EventType.IncreaseTraverseSpeedAction, this, HandleIncreaseTraverseSpeedAction);
        _eventManager.Subscribe(EventType.DecreaseTraverseSpeedAction, this, HandleDecreaseTraverseSpeedAction);
        _eventManager.Subscribe(EventType.StopTraverseAction, this, HandleStopTraverseAction);
        _eventManager.Subscribe(EventType.StartTestAction, this, HandleStartTestAction);
        _eventManager.Subscribe(EventType.PauseTestAction, this, HandlePauseTestAction);
        _eventManager.Subscribe(EventType.StopTestAction, this, HandleStopTestAction);
        _eventManager.Subscribe(EventType.FinishTestAction, this, HandleFinishTestAction);
        _eventManager.Subscribe(EventType.ApproachTraverseAction, this, HandleApproachTraverseAction);
        _eventManager.Subscribe(EventType.SampleButtonAction, this, HandleSampleButtonAction);
        _eventManager.Subscribe(EventType.FixtureAnimationStarted, this, HandleFixtureAnimationStarted);
        _eventManager.Subscribe(EventType.FixtureAnimationFinished, this, HandleFixtureAnimationFinished);
        _eventManager.Subscribe(EventType.TraverseApproachCompleted, this, HandleTraverseApproachCompleted);
        _eventManager.Subscribe(EventType.GraphStepUpdated, this, HandleGraphStepUpdated);
        _eventManager.Subscribe(EventType.TestSequenceCompleted, this, HandleTestSequenceCompleted);
        _eventManager.Subscribe(EventType.ShowControlTabAction, this, HandleShowControlTabAction);
        _eventManager.Subscribe(EventType.ShowTestTabAction, this, HandleShowTestTabAction);
        _eventManager.Subscribe(EventType.ShowResultsTabAction, this, HandleShowResultsTabAction);
        _eventManager.Subscribe(EventType.TraverseLimitReached, this, HandleTraverseLimitReached);
        _eventManager.Subscribe(EventType.ClampAnimationStarted, this, HandleClampAnimationStarted);
        _eventManager.Subscribe(EventType.ClampAnimationFinished, this, HandleClampAnimationFinished);
        _eventManager.Subscribe(EventType.ShowTestSettingsPanel, this, HandleShowTestSettingsPanel);
        _eventManager.Subscribe(EventType.SampleSetup, this, HandleSampleSetup);
        _eventManager.Subscribe(EventType.ApplySampleSetupSettings, this, HandleApplySampleSetupSettings);
        _eventManager.Subscribe(EventType.CloseSettingsPanel, this, HandleCloseSettingsPanel);
        _eventManager.Subscribe(EventType.HydraulicBufferAction, this, HandleHydraulicBufferAction);
        _eventManager.Subscribe(EventType.ViewHomeConfirmAction, this, HandleViewHomeConfirmClick);
        _eventManager.Subscribe(EventType.FastlyHydroUp, this, HandleFastlyHydroUp);
        _eventManager.Subscribe(EventType.FastlyHydroDown, this, HandleFastlyHydroDown);
        _eventManager.Subscribe(EventType.SlowlyHydroUp, this, HandleSlowlyHydroUp);
        _eventManager.Subscribe(EventType.SlowlyHydroDown, this, HandleSlowlyHydroDown);
        _eventManager.Subscribe(EventType.HydroStop, this, HandleHydroStop);
        _eventManager.Subscribe(EventType.RequestClampUpperGrip, this, HandleRequestClampUpperGrip);
        _eventManager.Subscribe(EventType.RequestUnclampUpperGrip, this, HandleRequestUnclampUpperGrip);
        _eventManager.Subscribe(EventType.RequestClampLowerGrip, this, HandleRequestClampLowerGrip);
        _eventManager.Subscribe(EventType.RequestUnclampLowerGrip, this, HandleRequestUnclampLowerGrip);
        _eventManager.Subscribe(EventType.UpperGripClamped, this, HandleUpperGripClampedInternal);
        _eventManager.Subscribe(EventType.UpperGripUnclamped, this, HandleUpperGripUnclampedInternal);
        _eventManager.Subscribe(EventType.LowerGripClamped, this, HandleLowerGripClampedInternal);
        _eventManager.Subscribe(EventType.LowerGripUnclamped, this, HandleLowerGripUnclampedInternal);
        _eventManager.Subscribe(EventType.HydraulicBufferActivationSuccessful, this, HandleHydraulicBufferActivationSuccessful);
        _eventManager.Subscribe(EventType.HydraulicBufferActivationFailed, this, HandleHydraulicBufferActivationFailed);
        _eventManager.Subscribe(EventType.UnloadSampleAction, this, HandleUnloadSampleAction);
        _eventManager.Subscribe(EventType.GraphKeyPointsCalculated, this, HandleGraphKeyPointsCalculatedInternal);
        _eventManager.Subscribe(EventType.RequestShowBigReport, this, HandleRequestShowBigReport);
        _eventManager.Subscribe(EventType.RequestShowSmallReport, this, HandleRequestShowSmallReport);
        _eventManager.Subscribe(EventType.HydroPumpButtonAction, this, HandleToggleHydroPumpAction);
        _eventManager.Subscribe(EventType.HydraulicOperationFinished, this, HandleHydraulicOperationFinished);
        _eventManager.Subscribe(EventType.ExtensometerActionConfirmed, this, HandleExtensometerConfirmation);
        _eventManager.Subscribe(EventType.MachineForceLimitReached, this, HandleMachineForceLimitReached);
        _eventManager.Subscribe(EventType.GoToCatalogAction, this, HandleGoToCatalogAction);
    }

    private void UnsubscribeFromEvents()
    {
        if (_eventManager == null) return;
        _eventManager.Unsubscribe(EventType.TestParametersConfirmed, this, HandleTestParametersConfirmed);
        _eventManager.Unsubscribe(EventType.FastlyUpAction, this, HandleFastlyUpAction);
        _eventManager.Unsubscribe(EventType.FastlyUpReleased, this, HandleFastlyUpReleased);
        _eventManager.Unsubscribe(EventType.FastlyDownAction, this, HandleFastlyDownAction);
        _eventManager.Unsubscribe(EventType.FastlyDownReleased, this, HandleFastlyDownReleased);
        _eventManager.Unsubscribe(EventType.SlowlyUpAction, this, HandleSlowlyUpAction);
        _eventManager.Unsubscribe(EventType.SlowlyDownAction, this, HandleSlowlyDownAction);
        _eventManager.Unsubscribe(EventType.IncreaseTraverseSpeedAction, this, HandleIncreaseTraverseSpeedAction);
        _eventManager.Unsubscribe(EventType.DecreaseTraverseSpeedAction, this, HandleDecreaseTraverseSpeedAction);
        _eventManager.Unsubscribe(EventType.StopTraverseAction, this, HandleStopTraverseAction);
        _eventManager.Unsubscribe(EventType.StartTestAction, this, HandleStartTestAction);
        _eventManager.Unsubscribe(EventType.PauseTestAction, this, HandlePauseTestAction);
        _eventManager.Unsubscribe(EventType.StopTestAction, this, HandleStopTestAction);
        _eventManager.Unsubscribe(EventType.FinishTestAction, this, HandleFinishTestAction);
        _eventManager.Unsubscribe(EventType.ApproachTraverseAction, this, HandleApproachTraverseAction);
        _eventManager.Unsubscribe(EventType.SampleButtonAction, this, HandleSampleButtonAction);
        _eventManager.Unsubscribe(EventType.FixtureAnimationStarted, this, HandleFixtureAnimationStarted);
        _eventManager.Unsubscribe(EventType.FixtureAnimationFinished, this, HandleFixtureAnimationFinished);
        _eventManager.Unsubscribe(EventType.TraverseApproachCompleted, this, HandleTraverseApproachCompleted);
        _eventManager.Unsubscribe(EventType.GraphStepUpdated, this, HandleGraphStepUpdated);
        _eventManager.Unsubscribe(EventType.TestSequenceCompleted, this, HandleTestSequenceCompleted);
        _eventManager.Unsubscribe(EventType.ShowControlTabAction, this, HandleShowControlTabAction);
        _eventManager.Unsubscribe(EventType.ShowTestTabAction, this, HandleShowTestTabAction);
        _eventManager.Unsubscribe(EventType.ShowResultsTabAction, this, HandleShowResultsTabAction);
        _eventManager.Unsubscribe(EventType.TraverseLimitReached, this, HandleTraverseLimitReached);
        _eventManager.Unsubscribe(EventType.ClampAnimationStarted, this, HandleClampAnimationStarted);
        _eventManager.Unsubscribe(EventType.ClampAnimationFinished, this, HandleClampAnimationFinished);
        _eventManager.Unsubscribe(EventType.ShowTestSettingsPanel, this, HandleShowTestSettingsPanel);
        _eventManager.Unsubscribe(EventType.SampleSetup, this, HandleSampleSetup);
        _eventManager.Unsubscribe(EventType.ApplySampleSetupSettings, this, HandleApplySampleSetupSettings);
        _eventManager.Unsubscribe(EventType.CloseSettingsPanel, this, HandleCloseSettingsPanel);
        _eventManager.Unsubscribe(EventType.HydraulicBufferAction, this, HandleHydraulicBufferAction);
        _eventManager.Unsubscribe(EventType.ViewHomeConfirmAction, this, HandleViewHomeConfirmClick);
        _eventManager.Unsubscribe(EventType.FastlyHydroUp, this, HandleFastlyHydroUp);
        _eventManager.Unsubscribe(EventType.FastlyHydroDown, this, HandleFastlyHydroDown);
        _eventManager.Unsubscribe(EventType.SlowlyHydroUp, this, HandleSlowlyHydroUp);
        _eventManager.Unsubscribe(EventType.SlowlyHydroDown, this, HandleSlowlyHydroDown);
        _eventManager.Unsubscribe(EventType.HydroStop, this, HandleHydroStop);
        _eventManager.Unsubscribe(EventType.RequestClampUpperGrip, this, HandleRequestClampUpperGrip);
        _eventManager.Unsubscribe(EventType.RequestUnclampUpperGrip, this, HandleRequestUnclampUpperGrip);
        _eventManager.Unsubscribe(EventType.RequestClampLowerGrip, this, HandleRequestClampLowerGrip);
        _eventManager.Unsubscribe(EventType.RequestUnclampLowerGrip, this, HandleRequestUnclampLowerGrip);
        _eventManager.Unsubscribe(EventType.UpperGripClamped, this, HandleUpperGripClampedInternal);
        _eventManager.Unsubscribe(EventType.UpperGripUnclamped, this, HandleUpperGripUnclampedInternal);
        _eventManager.Unsubscribe(EventType.LowerGripClamped, this, HandleLowerGripClampedInternal);
        _eventManager.Unsubscribe(EventType.LowerGripUnclamped, this, HandleLowerGripUnclampedInternal);
        _eventManager.Unsubscribe(EventType.HydraulicBufferActivationSuccessful, this, HandleHydraulicBufferActivationSuccessful);
        _eventManager.Unsubscribe(EventType.HydraulicBufferActivationFailed, this, HandleHydraulicBufferActivationFailed);
        _eventManager.Unsubscribe(EventType.UnloadSampleAction, this, HandleUnloadSampleAction);
        _eventManager.Unsubscribe(EventType.GraphKeyPointsCalculated, this, HandleGraphKeyPointsCalculatedInternal);
        _eventManager.Unsubscribe(EventType.HydroPumpButtonAction, this, HandleToggleHydroPumpAction);
        _eventManager.Unsubscribe(EventType.HydraulicOperationFinished, this, HandleHydraulicOperationFinished);
        _eventManager.Unsubscribe(EventType.ExtensometerActionConfirmed, this, HandleExtensometerConfirmation);
        _eventManager.Unsubscribe(EventType.MachineForceLimitReached, this, HandleMachineForceLimitReached);
        _eventManager.Unsubscribe(EventType.GoToCatalogAction, this, HandleGoToCatalogAction);
    }

    #endregion

    #region User Input Handlers (Event Triggers)
    //================================================================================================================//
    // РЕГИОН 3: ОБРАБОТЧИКИ ВВОДА ПОЛЬЗОВАТЕЛЯ
    // Все методы Handle..., которые срабатывают от ПРЯМЫХ действий пользователя (нажатия кнопок).
    // Их задача - проверить разрешение и ЗАПУСТИТЬ соответствующую корутину или простую команду.
    //================================================================================================================//
    
    //---------------------------
    private void HandleTestParametersConfirmed(EventArgs args)
    {
        // 1. Проверка безопасности Политики (как было)
        if (CheckActionAllowedAndShowHint(EventType.TestParametersConfirmed)) { return; }

        // --- НОВАЯ ПРОВЕРКА ---
        // Если образец установлен, мы запрещаем менять настройки
        if (SystemStateMonitor.Instance.IsSampleInPlace)
        {
            // Показываем ошибку пользователю
            ToDoManager.Instance.HandleAction(ActionType.ShowHintText, new ShowHintArgs("Сначала снимите установленный образец!"));
            return; // ПРЕРЫВАЕМ ОПЕРАЦИЮ. Событие Agreed не уйдет.
        }
        // ----------------------

        // 2. Инициализация (если дошли сюда, значит образца нет)
        bool success = InitializeTestParameters(); 

        if (success)
        {
            // Отправка сигнала Сценарию (Interrupt сработает только сейчас)
            _eventManager.RaiseEvent(EventType.TestParametersAgreed, EventArgs.Empty);
        }
    }

    
    //Новые методы принятия настроек образца по методу StateMachine
    /*private void HandleTestParametersConfirmed(EventArgs args)
    {
        if (CheckActionAllowedAndShowHint(EventType.TestParametersConfirmed)) { return; }
        _currentState?.OnTestParametersConfirmed();
    }*/

    /*public void StartFixtureChangeSequence()
    {
        if (_fixtureChangeCoroutine != null) StopCoroutine(_fixtureChangeCoroutine);
        _fixtureChangeCoroutine = StartCoroutine(ExecutePreciseFixtureChangeWorkflow());
    }*/
    //---------------------------

    public bool InitializeTestParameters()
    {
        // --- 1. ПОЛУЧАЕМ КОНТЕКСТ ИЗ МОНИТОРА ---
        var monitor = SystemStateMonitor.Instance;
        if (monitor == null || !monitor.IsSetupPanelValid)
        {
            Debug.LogError("<color=red>CSM: Старт невозможен. SystemStateMonitor не готов или панель настроек невалидна.</color>");
            SetCurrentState(TestState.Error);
            return false;
        }

        // --- 2. ОПРЕДЕЛЯЕМ КОНФИГУРАЦИЮ И ЛОГИКУ ТЕСТА ---
        _currentTestConfiguration = DataManager.Instance.AllTestConfigs.FirstOrDefault(
            t => t.templateName == monitor.SelectedTemplateName && t.compatibleSampleIDs.Any(
                sId => DataManager.Instance.GetSampleDataByID(sId)?.sampleForm == monitor.SelectedShape
            )
        );

        if (_currentTestConfiguration == null)
        {
            Debug.LogError($"<color=red>CSM: Не удалось найти TestConfiguration для шаблона '{monitor.SelectedTemplateName}' и формы '{monitor.SelectedShape}'.</color>");
            SetCurrentState(TestState.Error);
            return false;
        }

        _currentTestLogicHandler = TestLogicHandlerFactory.Create(_currentTestConfiguration);
        monitor.ReportTestConfiguration(_currentTestConfiguration); // Сообщаем в монитор, какой конфиг мы выбрали
        SystemStateMonitor.Instance?.ReportGeneralTestType(_currentTestConfiguration.testType); // Сообщаем в монитор, какой тип теста
        SystemStateMonitor.Instance?.ReportTestLogicHandler(_currentTestLogicHandler); // Сообщаем в монитор, какой хэндлер мы выбрали
        _currentCalculator = CalculatorFactory.Create(SystemStateMonitor.Instance); // Создаем калькулятор лимитов на основе данных монитора

        // --- 3. ВЫПОЛНЯЕМ ОСТАЛЬНУЮ ЛОГИКУ ПОДГОТОВКИ ---
        ToDoManager.Instance?.HandleAction(ActionType.ClearLastReport, null);

        if (_currentSampleInstance != null) { PerformSampleRemoval(); }

        TestManager.Instance?.SetCurrentTestType(_currentTestConfiguration.typeOfTest);

        SetUIContainerActiveArgs uiArgs1 = new SetUIContainerActiveArgs("MainButtonContainer", true);
        ToDoManager.Instance?.HandleAction(ActionType.SetUIContainerActive, uiArgs1);
        ToDoManager.Instance?.HandleAction(ActionType.ActivateUITab, new ActivateUITabArgs("ControlTab"));
        SetUIContainerActiveArgs uiArgs2 = new SetUIContainerActiveArgs("ScreenContainer", true);
        ToDoManager.Instance?.HandleAction(ActionType.SetUIContainerActive, uiArgs2);
        SetUIContainerActiveArgs uiArgs3 = new SetUIContainerActiveArgs("Pult", true);
        ToDoManager.Instance?.HandleAction(ActionType.SetUIContainerActive, uiArgs3);

        _eventManager.RaiseEvent(EventType.FocusCameraRequested, new FocusCameraEventArgs(this, CameraFocusTarget.Fixture));

        if (_currentTestConfiguration != null)
        {
            // 1. Сообщаем новые базовые ("заводские") лимиты в Монитор
            monitor.ReportOriginLimits(
                _currentTestConfiguration.minLowerTraversePosition,
                _currentTestConfiguration.maxUpperTraversePosition
            );
            
            // 2. Сбрасываем ТЕКУЩИЕ лимиты на базовые и отключаем динамический флаг
            monitor.ReportTraverseLimits(
                _currentTestConfiguration.minLowerTraversePosition,
                _currentTestConfiguration.maxUpperTraversePosition,
                false // isDynamic: false
            );
        }

        // Определяем нужную оснастку, используя данные из монитора
        float dimensionForFixtures = monitor.CurrentSampleParameters["DiameterThickness"];
        List<string> resolvedFixtureIDs = FixtureManager.Instance?.ResolveTargetFixtureIDs(_currentTestConfiguration, dimensionForFixtures);
        monitor.ReportRequiredFixtures(resolvedFixtureIDs);

        if (resolvedFixtureIDs == null) { Debug.LogError("<color=red>CSM: FixtureManager вернул null список.</color>"); SetCurrentState(TestState.Error); return false; }

        _workflowRunner?.Stop();

        _eventManager?.RaiseEvent(EventType.GlobalModeButtonsVisibilityChanged, new GlobalModeButtonsVisibilityEventArgs(this, showMenu: false, showHome: true));

        //TransitionToState(new ReadyForSetupState(this));

        _cached_X_UltimateStrength_Percent = -1f;
        _cached_X_Rupture_Percent = -1f;

        ToDoManager.Instance?.HandleAction(ActionType.ResetGraphAndSimulation, null);   // Сначала сбрасываем, чтобы график вышел из состояния Ready в Idle/Empty
        ToDoManager.Instance?.HandleAction(ActionType.PrepareGraph, null);              // А потом готовим заново с новыми лимитами

        SendPromptUpdateCommand("CSM_TestParametersConfirmed", PromptSourceType.SystemAction, "UserAction", true);
        var modeArgs = new SetDisplayModeArgs("РЕЖИМ ИСПЫТАНИЯ");
        ToDoManager.Instance?.HandleAction(ActionType.SetDisplayMode, modeArgs);
        monitor.ReportApplicationMode(ApplicationMode.TestMode);

        if (_approachProcessCoroutine != null) StopCoroutine(_approachProcessCoroutine);
        _isTestCurrentlyOrPreviouslyConfigured = true;
        return true;
    }

    private void HandleFastlyUpAction(EventArgs args) { if (CheckActionAllowedAndShowHint(EventType.FastlyUpAction)) { return; } _currentState.OnTraverseMove(1f, SpeedType.Fast); return; }
    private void HandleFastlyUpReleased(EventArgs args) { _currentState.OnTraverseStop(); return; }
    private void HandleFastlyDownAction(EventArgs args) { if (CheckActionAllowedAndShowHint(EventType.FastlyDownAction)) { return; } _currentState.OnTraverseMove(-1f, SpeedType.Fast);return; }
    private void HandleFastlyDownReleased(EventArgs args) { _currentState.OnTraverseStop(); return; }
    private void HandleSlowlyUpAction(EventArgs args) { if (CheckActionAllowedAndShowHint(EventType.SlowlyUpAction)) { return; } _currentState.OnTraverseMove(1f, SpeedType.Slow);return; }
    private void HandleSlowlyDownAction(EventArgs args) { if (CheckActionAllowedAndShowHint(EventType.SlowlyDownAction)) { return; } _currentState.OnTraverseMove(-1f, SpeedType.Slow);return; }
    private void HandleIncreaseTraverseSpeedAction(EventArgs args) { if (CheckActionAllowedAndShowHint(EventType.IncreaseTraverseSpeedAction)) return; _currentState.OnTraverseSpeedAdjust(true); return; }
    private void HandleDecreaseTraverseSpeedAction(EventArgs args) { if (CheckActionAllowedAndShowHint(EventType.DecreaseTraverseSpeedAction)) return;         _currentState.OnTraverseSpeedAdjust(false); return; }
    private void HandleStopTraverseAction(EventArgs args) { if (CheckActionAllowedAndShowHint(EventType.StopTraverseAction)) return; _currentState.OnTraverseStop(); return; }
    private void HandleStartTestAction(EventArgs args) { _currentState.OnStartTest(); return; }
    private void HandlePauseTestAction(EventArgs args) { if (CheckActionAllowedAndShowHint(EventType.PauseTestAction)) { return; } _currentState.OnPauseTest(); return; }
    private void HandleStopTestAction(EventArgs args) { if (CheckActionAllowedAndShowHint(EventType.StopTestAction)) { return; } _currentState.OnStopTest(); return; }
    private void HandleFinishTestAction(EventArgs args) { _currentState?.OnFinishTestCommand(); }

    private void HandleApproachTraverseAction(EventArgs args)
    {
        if (_isClampAnimating) { ShowHint("Подождите, идёт установка образца"); return; }
        if (CheckActionAllowedAndShowHint(EventType.ApproachTraverseAction)) { return; }
        _currentState.OnAutoApproach(); return;
    }

    private void HandleSampleButtonAction(EventArgs args) { if (CheckActionAllowedAndShowHint(EventType.SampleButtonAction)) { return; } _currentState.OnSampleAction(); return;}
    
    private void HandleUnloadSampleAction(EventArgs args)
    { if (CheckActionAllowedAndShowHint(EventType.UnloadSampleAction)) { return; }
        if (_scenarioCoroutine != null) { return; }
        _currentState.OnUnloadSample(); return;
    }
    
    private void HandleShowControlTabAction(EventArgs args)
    {
        if (CheckActionAllowedAndShowHint(EventType.ShowControlTabAction)) { return; }
        ToDoManager.Instance?.HandleAction(ActionType.ActivateUITab, new ActivateUITabArgs("ControlTab"));
        ToDoManager.Instance?.HandleAction(ActionType.HideAllReports, null);
    }
    
    private void HandleShowTestTabAction(EventArgs args)
    {
        if (CheckActionAllowedAndShowHint(EventType.ShowTestTabAction)) { return; }
        ToDoManager.Instance?.HandleAction(ActionType.ActivateUITab, new ActivateUITabArgs("TestTab"));
        ToDoManager.Instance?.HandleAction(ActionType.HideAllReports, null);
    }
    
    private void HandleShowResultsTabAction(EventArgs args)
    {
        if (CheckActionAllowedAndShowHint(EventType.ShowResultsTabAction)) { return; }
        ToDoManager.Instance?.HandleAction(ActionType.ActivateUITab, new ActivateUITabArgs("ResultsTab"));
        ToDoManager.Instance?.HandleAction(ActionType.ShowSmallReport, null);
    }

    private void HandleShowTestSettingsPanel(EventArgs args)
    { if (CheckActionAllowedAndShowHint(EventType.ShowTestSettingsPanel)) { return; } ToDoManager.Instance?.HandleAction(ActionType.ShowTestSettingsPanelAction, null); }

    private void HandleSampleSetup(EventArgs args)
    {
        if (CheckActionAllowedAndShowHint(EventType.SampleSetup)) { return; }
        ToDoManager.Instance?.HandleAction(ActionType.SampleSetupAction, null);
        SendPromptUpdateCommand("CSM_SampleSetup", PromptSourceType.SystemAction, "UserAction");
    }

    private void HandleApplySampleSetupSettings(EventArgs args)
    { if (CheckActionAllowedAndShowHint(EventType.ApplySampleSetupSettings)) { return; } ToDoManager.Instance?.HandleAction(ActionType.ApplySampleSetupSettingsAction, null); }

    private void HandleCloseSettingsPanel(EventArgs args)
    { if (CheckActionAllowedAndShowHint(EventType.CloseSettingsPanel)) { return; } ToDoManager.Instance?.HandleAction(ActionType.CloseSettingsPanelAction, null); }

    private void HandleFastlyHydroUp(EventArgs args)  {  if (CheckActionAllowedAndShowHint(EventType.FastlyHydroUp)) return;  _currentState.OnHydraulicMove(1f, SpeedType.Fast); }
    private void HandleFastlyHydroDown(EventArgs args)  {  if (CheckActionAllowedAndShowHint(EventType.FastlyHydroDown)) return;  _currentState.OnHydraulicMove(-1f, SpeedType.Fast); }
    private void HandleSlowlyHydroUp(EventArgs args) { if (CheckActionAllowedAndShowHint(EventType.SlowlyHydroUp)) return; _currentState.OnHydraulicMove(1f, SpeedType.Slow); }
    private void HandleSlowlyHydroDown(EventArgs args) { if (CheckActionAllowedAndShowHint(EventType.SlowlyHydroDown)) return; _currentState.OnHydraulicMove(-1f, SpeedType.Slow);}
    private void HandleHydroStop(EventArgs args) { if (CheckActionAllowedAndShowHint(EventType.HydroStop)) return; _currentState.OnHydraulicStop();}
    private void HandleHydraulicOperationFinished(EventArgs args) { _currentState?.OnOperationFinished(); }

    private void HandleHydraulicBufferAction(EventArgs args) 
    { 
        if (CheckActionAllowedAndShowHint(EventType.HydraulicBufferAction)) return; 
        
        var cmdArgs = new SetSupportSystemStateArgs(true); // activate = true
        ToDoManager.Instance?.HandleAction(ActionType.SetSupportSystemState, cmdArgs);
    }

    private void HandleRequestClampUpperGrip(EventArgs args) { if (CheckActionAllowedAndShowHint(EventType.RequestClampUpperGrip)) { return; } _currentState.OnClampAction(GripType.Upper, true); }    
    private void HandleRequestUnclampUpperGrip(EventArgs args) { if (CheckActionAllowedAndShowHint(EventType.RequestUnclampUpperGrip)) { return; } _currentState.OnClampAction(GripType.Upper, false); }    
    private void HandleRequestClampLowerGrip(EventArgs args) { if (CheckActionAllowedAndShowHint(EventType.RequestClampLowerGrip)) { return; } _currentState.OnClampAction(GripType.Lower, true);}    
    private void HandleRequestUnclampLowerGrip(EventArgs args) { if (CheckActionAllowedAndShowHint(EventType.RequestUnclampLowerGrip)) { return; } _currentState.OnClampAction(GripType.Lower, false); }
    private void HandleViewHomeConfirmClick(EventArgs args) { _currentState?.OnViewHomeConfirm(); }
    private void HandleRequestShowBigReport(EventArgs args) { ToDoManager.Instance?.HandleAction(ActionType.ShowBigReport, null); }
    private void HandleRequestShowSmallReport(EventArgs args) { ToDoManager.Instance?.HandleAction(ActionType.ShowSmallReport, null); }
    
    private void HandleToggleHydroPumpAction(EventArgs args)
    {
        // Проверяем актуальное состояние через Монитор
        bool isPumpOn = SystemStateMonitor.Instance.IsPowerUnitActive;

        if (isPumpOn)
        {
            // Если включен -> отправляем команду выключить
            ToDoManager.Instance.HandleAction(ActionType.AnimatePumpOff, null);
        }
        else
        {
            // Если выключен -> отправляем команду включить
            ToDoManager.Instance.HandleAction(ActionType.AnimatePumpOn, null);
        }
        
        // Обновление визуала кнопки. 
        // В идеале UI сам должен слушать Монитор, но пока можно вызвать принудительно:
        UpdateHydroPumpButtonVisuals(); 
    }

    private void HandleExtensometerToggle(EventArgs args)
    {
        if (args is ExtensometerToggleEventArgs eventArgs)
        { isExtensometerRequestedByUser = eventArgs.IsEnabled; }
    }

    private void HandleExtensometerAttachRequest() { _currentState?.OnExtensometerRequest(true); }
    private void HandleExtensometerRemoveRequest() { _currentState?.OnExtensometerRequest(false); }
    private void HandleExtensometerConfirmation(EventArgs args) { _currentState.OnExtensometerConfirm(); }

    private void HandleMachineForceLimitReached(EventArgs args) { Debug.LogWarning("[CSM] Превышение лимита силы."); _currentState.OnMachineForceLimitReached(); }
    private void HandleGoToCatalogAction(EventArgs args) { Debug.Log("[CSM] Получен запрос на возврат в каталог."); StartCoroutine(ResetAndLoadCatalogSceneCoroutine()); }

    #endregion

    #region Workflow Coroutines: Test Setup & Approach
    //================================================================================================================//
    // РЕГИОН 4: КОРУТИНЫ: НАСТРОЙКА ТЕСТА И ПОДВОД
    // Здесь лежат самые сложные, многошаговые процессы: смена оснастки и автоматический подвод траверсы.
    // Если что-то не так с подготовкой машины к тесту, смотреть сюда.
    //================================================================================================================//

    /*private IEnumerator ExecutePreciseFixtureChangeWorkflow()
    {
        // --- НАЧАЛЬНАЯ ПРОВЕРКА ---
        if (_currentTestLogicHandler == null || FixtureController.Instance == null)
        {
            Debug.LogError("<color=red>CSM: Невозможно выполнить Workflow. Handler или FixtureController null.</color>");
            SetCurrentState(TestState.Error);
            yield break;
        }

        // =================================================================================
        // --- ОБЯЗАТЕЛЬНЫЙ БЛОК: Обеспечение пространства для установки ---
        // =================================================================================

        SystemStateMonitor.Instance?.ReportFixtureChangeStatus(true);
        TestType testType = _currentTestConfiguration.testType;
        ActionRequester myRequester = ActionRequester.CSM;

        Debug.Log($"<color=cyan>[CSM]</color> Запуск обязательной проверки пространства для типа теста: {testType}");

        var commandArgs = new EnsureFixtureInstallationClearanceArgs(null, testType, myRequester);

        bool commandCompleted = false;
        Action<EventArgs> onClearanceReadyHandler = (e) =>
        {
            if (e is FixtureInstallationClearanceReadyEventArgs eventArgs && eventArgs.Requester == myRequester)
            {
                commandCompleted = true;
            }
        };

        EventManager.Instance.Subscribe(EventType.FixtureInstallationClearanceReady, this, onClearanceReadyHandler);
        ToDoManager.Instance.HandleAction(ActionType.EnsureFixtureInstallationClearance, commandArgs);

        float timeout = 15.0f;
        float elapsedTime = 0f;
        while (!commandCompleted && elapsedTime < timeout)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        EventManager.Instance.Unsubscribe(EventType.FixtureInstallationClearanceReady, this, onClearanceReadyHandler);

        if (!commandCompleted)
        {
            Debug.LogError($"[CSM] Таймаут ожидания ответа от MachineController. Процесс смены оснастки прерван.");
            SetCurrentState(TestState.Error);
            _fixtureChangeCoroutine = null;
            yield break;
        }

        Debug.Log("<color=green>[CSM]</color> Пространство обеспечено. Продолжаю...");


        // =================================================================================
        // --- ПОЛУЧЕНИЕ ПЛАНА СМЕНЫ ОСНАСТКИ ---
        // =================================================================================
        List<string> liveInstalledFixtures = FixtureController.Instance.GetAllInstalledFixtureIDs();
        
        var plan = _currentTestLogicHandler.CreateFixtureChangePlan(
            _currentTestConfiguration,
            SystemStateMonitor.Instance.SelectedShape,
            liveInstalledFixtures
        );

        if (plan == null)
        {
            Debug.LogError("<color=red>CSM: Хендлер вернул null план смены оснастки.</color>");
            SetCurrentState(TestState.Error);
            yield break;
        }

        _removingFixtureIds.Clear();
        _installingFixtureIds.Clear();

        // --- БЛОК PREINITIALIZE ---
        bool preInitializeExecuted = false;
        if (plan.FixturesToPreInitialize != null && plan.FixturesToPreInitialize.Count > 0)
        {
            string firstFixtureId = plan.FixturesToPreInitialize[0];
            FixtureData firstFixtureData = FixtureManager.Instance.GetFixtureData(firstFixtureId);

            if (firstFixtureData != null)
            {
                FixtureData installedInZone = FixtureController.Instance.GetInstalledFixtureInZone(firstFixtureData.fixtureZone);

                if (installedInZone == null)
                {
                    Debug.Log($"<color=yellow>CSM: PreInitialize для зоны '{firstFixtureData.fixtureZone}'.</color>");
                    
                    // Если НЕ закрыты -> закрываем двери
                    if (!SystemStateMonitor.Instance.AreDoorsClosed)
                    {
                         ToDoManager.Instance?.HandleAction(ActionType.SetDoorStateAction, null);
                         yield return new WaitForSeconds(_currentTestConfiguration != null ? 0.5f : 0.5f); // Ждем закрытия
                    }

                    // 1. Родители
                    var parentsToInstall = new List<string>();
                    var childrenToInstall = new List<string>();
                    foreach (var fixtureId in plan.FixturesToPreInitialize)
                    {
                        var data = FixtureManager.Instance.GetFixtureData(fixtureId);
                        if (data != null && string.IsNullOrEmpty(data.parentFixtureId)) parentsToInstall.Add(fixtureId);
                        else childrenToInstall.Add(fixtureId);
                    }

                    foreach (var parentId in parentsToInstall)
                    {
                        ToDoManager.Instance.HandleAction(ActionType.PlaceFixtureWithoutAnimation, new PlaceFixtureArgs(parentId, null, null));
                        yield return new WaitForSeconds(0.05f);
                    }

                    // 2. Пересчет зон
                    ToDoManager.Instance.HandleAction(ActionType.ReinitializeFixtureZones, null);
                    yield return null;

                    // 3. Дети
                    foreach (var childId in childrenToInstall)
                    {
                        ToDoManager.Instance.HandleAction(ActionType.PlaceFixtureWithoutAnimation, new PlaceFixtureArgs(childId, null, null));
                        yield return new WaitForSeconds(0.05f);
                    }

                    yield return new WaitForSeconds(0.2f);
                    
                    // Закрываем двери обратно (если они открыты)
                    if (!SystemStateMonitor.Instance.AreDoorsClosed)
                    {
                         ToDoManager.Instance?.HandleAction(ActionType.SetDoorStateAction, null);
                    }

                    preInitializeExecuted = true;
                }
            }
        }

        if (preInitializeExecuted)
        {
            plan.MainFixturesToInstall.RemoveAll(info => plan.FixturesToPreInitialize.Contains(info.FixtureId));
            plan.InternalFixturesToInstall.RemoveAll(item => plan.FixturesToPreInitialize.Contains(item.FixtureId));
        }

        // --- СТАНДАРТНЫЙ WORKFLOW ---

        if (plan.MainFixturesToRemove.Count > 0)
        {
            // Подготовка (Unclamp и т.д.)
            ITestLogicHandler handlerForTeardown = FixtureController.Instance.GetActiveLogicHandler();
            if (handlerForTeardown != null)
            {
                var correctlyOrderedFixturesToRemove = handlerForTeardown.CreateTeardownPlan(plan.MainFixturesToRemove);
                plan.MainFixturesToRemove = correctlyOrderedFixturesToRemove;

                var prepCommands = handlerForTeardown.GetPreChangePreparationCommands(plan.MainFixturesToRemove);
                if (prepCommands != null && prepCommands.Count > 0)
                {
                    bool containsUnclamp = prepCommands.Any(c => c.Action == ActionType.UnclampUpperGrip || c.Action == ActionType.UnclampLowerGrip);
                    foreach (var command in prepCommands) ToDoManager.Instance.HandleAction(command.Action, command.Args);
                    if (containsUnclamp) yield return new WaitUntil(() => !_isClampAnimating);
                }
            }

            // Снятие
            Debug.Log($"<color=lightblue>CSM: Снятие: [{string.Join(", ", plan.MainFixturesToRemove)}]</color>");
            
            // Открываем двери перед анимацией
            if (SystemStateMonitor.Instance.AreDoorsClosed)
            {
                 ToDoManager.Instance?.HandleAction(ActionType.SetDoorStateAction, null);
                 yield return new WaitForSeconds(0.3f);
            }
            
            _removingFixtureIds.AddRange(plan.MainFixturesToRemove);
            foreach (var fixtureId in plan.MainFixturesToRemove)
            {
                ToDoManager.Instance.HandleAction(ActionType.PlayFixtureAnimationAction, new PlayFixtureAnimationArgs(fixtureId, AnimationDirection.Out, ActionRequester.CSM));
                yield return new WaitForSeconds(0.05f);
            }
            yield return new WaitUntil(() => _removingFixtureIds.Count == 0);
        }

        // УСТАНОВКА ОСНОВНОЙ
        if (plan.MainFixturesToInstall.Count > 0)
        {
            Debug.Log($"<color=lightblue>CSM: Установка ОСНОВНОЙ: [{string.Join(", ", plan.MainFixturesToInstall.Select(info => info.FixtureId))}]</color>");
            
            // Открываем двери, если закрыты
            if (SystemStateMonitor.Instance.AreDoorsClosed)
            {
                 ToDoManager.Instance?.HandleAction(ActionType.SetDoorStateAction, null);
            }

            _installingFixtureIds.AddRange(plan.MainFixturesToInstall.Select(info => info.FixtureId));
            foreach (var installInfo in plan.MainFixturesToInstall)
            {
                var actionType = installInfo.UseAnimation ? ActionType.PlayFixtureAnimationAction : ActionType.PlaceFixtureWithoutAnimation;
                var args = installInfo.UseAnimation
                       ? (BaseActionArgs)new PlayFixtureAnimationArgs(installInfo.FixtureId, AnimationDirection.In, ActionRequester.CSM)
                       : new PlaceFixtureArgs(installInfo.FixtureId, null, null);
                ToDoManager.Instance.HandleAction(actionType, args);
                yield return new WaitForSeconds(0.05f);
            }
        }

        yield return new WaitUntil(() => _installingFixtureIds.Count == 0);

        // ПРОМЕЖУТОЧНЫЕ КОМАНДЫ
        if (plan.InterstitialCommands != null && plan.InterstitialCommands.Count > 0)
        {
            foreach (var command in plan.InterstitialCommands) ToDoManager.Instance.HandleAction(command.Action, command.Args);
            yield return null;
        }

        // УСТАНОВКА ВЛОЖЕННОЙ
        if (plan.InternalFixturesToInstall.Count > 0)
        {
            // Здесь двери уже должны быть закрыты после предыдущих этапов, но можно добавить проверку для надежности
            _installingFixtureIds.AddRange(plan.InternalFixturesToInstall.Select(info => info.FixtureId));
            foreach (var internalInstallInfo in plan.InternalFixturesToInstall)
            {
                ToDoManager.Instance.HandleAction(ActionType.PlayFixtureAnimationAction, new PlayFixtureAnimationArgs(internalInstallInfo.FixtureId, AnimationDirection.In, ActionRequester.CSM));
                yield return new WaitForSeconds(0.05f);
            }
            yield return new WaitUntil(() => _installingFixtureIds.Count == 0);
        }

        // ФИНАЛИЗАЦИЯ
        bool anyStandardActionTaken = plan.MainFixturesToRemove.Count > 0 ||
                                      plan.MainFixturesToInstall.Count > 0 ||
                                      plan.InternalFixturesToInstall.Count > 0;
        
        if (anyStandardActionTaken)
        {
            // Закрываем двери в конце (если они открыты)
            if (!SystemStateMonitor.Instance.AreDoorsClosed)
            {
                 ToDoManager.Instance?.HandleAction(ActionType.SetDoorStateAction, null);
            }
        }

        var finalizationCommands = _currentTestLogicHandler.GetPostChangeFinalizationCommands();
        if (finalizationCommands != null && finalizationCommands.Count > 0)
        {
            foreach (var command in finalizationCommands) ToDoManager.Instance.HandleAction(command.Action, command.Args);
        }

        ToDoManager.Instance.HandleAction(ActionType.SetCurrentLogicHandler, null);
        
        TransitionToState(new ReadyForSetupState(this));

        _fixtureChangeCoroutine = null;
        SystemStateMonitor.Instance?.ReportFixtureChangeStatus(false);
        Debug.Log("<color=green>CSM: ExecutePreciseFixtureChangeWorkflow полностью завершен.</color>");
    }*/

    public void StartStandardFixtureChangeWorkflow()
    {
        // 1. Создаем контекст
        var ctx = new WorkflowContext(this, ActionRequester.CSM);

        // 2. Собираем сценарий (список шагов)
        var steps = BuildFixtureChangeScenario();

        // 3. Запускаем
        _workflowRunner.Start(steps, ctx);
    }

    // --- КОНСТРУКТОР СЦЕНАРИЯ (СБОРКА КОНВЕЙЕРА) ---
    private List<IWorkflowStep> BuildFixtureChangeScenario()
    {
        var steps = new List<IWorkflowStep>();

        // 1. Безопасность (Машина отъезжает)
        steps.Add(new Step_EnsureClearance(_currentTestConfiguration.testType));

        // 2. Математика
        steps.Add(new Step_CalculateFixturePlan(_currentTestConfiguration));

        // 3. ОТКРЫВАЕМ ДВЕРИ 
        steps.Add(new Step_SetDoorState(true));

        // 4. Тихая пре-инициализация
        steps.Add(new Step_PreInitializeFixtures());

        // 5. Подготовка к снятию
        steps.Add(new Step_PrepareForTeardown());

        // 6. Снятие
        steps.Add(new Step_BatchFixtureAction(BatchActionMode.RemoveAllOld));

        // 7. Установка ОСНОВНОЙ
        steps.Add(new Step_BatchFixtureAction(BatchActionMode.InstallMain));

        // 8. Промежуточные
        steps.Add(new Step_ExecuteInterstitialCommands());

        // 9. Установка ВЛОЖЕННОЙ
        steps.Add(new Step_BatchFixtureAction(BatchActionMode.InstallInternal));

        // 10. Закрываем двери
        steps.Add(new Step_SetDoorState(false));

        // 11. Финализация
        steps.Add(new Step_FinalizeFixtureChange());

        // 12. Переход
        steps.Add(new Step_TransitionToState());

        return steps;
    }

    
    private IEnumerator ProcessApproachSequenceCoroutine()
    {
        if (_currentTestLogicHandler == null || _currentTestConfiguration == null)
        {
            Debug.LogError("[CSM] Невозможно выполнить ProcessApproachSequenceCoroutine: ключевые данные не установлены.");
            SetCurrentState(TestState.Error);
            _approachProcessCoroutine = null;
            yield break;
        }

        SystemStateMonitor.Instance?.ReportApproachStatus(true);
        if (_workflowRunner != null && _workflowRunner.IsRunning)
        {
            Debug.Log("[CSM] Ожидание завершения Workflow смены оснастки...");
            yield return new WaitUntil(() => !_workflowRunner.IsRunning);
            Debug.Log("[CSM] Workflow завершен.");
        }

        Debug.Log($"[CSM] Запуск обработки инструкций от {_currentTestLogicHandler.GetType().Name}");

        IEnumerator setupInstructions = _currentTestLogicHandler.SetupTestSpecificFixtures(
            _currentTestConfiguration, 
            FixtureManager.Instance, 
            ToDoManager.Instance
        );


        while (setupInstructions.MoveNext())
        {
            object currentStep = setupInstructions.Current;

            if (currentStep is string fixtureIdToInstall && !string.IsNullOrEmpty(fixtureIdToInstall))
            {
                if (IsFixtureInstalled(fixtureIdToInstall))
                {
                    Debug.Log($"[CSM] Оснастка '{fixtureIdToInstall}' уже установлена. Пропускаем.");
                    continue;
                }

                Debug.Log($"[CSM] Отправка команды на установку: {fixtureIdToInstall}");
                var placeArgs = new PlaceFixtureArgs(fixtureIdToInstall, null, null);
                ToDoManager.Instance.HandleAction(ActionType.PlaceFixtureByIdentifier, placeArgs);
                
                yield return new WaitUntil(() => IsFixtureInstalled(fixtureIdToInstall));
                Debug.Log($"[CSM] Подтверждена установка: {fixtureIdToInstall}");
            }
            else if (currentStep is ToDoManagerCommand command)
            {
                Debug.Log($"[CSM] Выполнение прямой команды от хендлера: {command.Action}");
                ToDoManager.Instance.HandleAction(command.Action, command.Args);
                
                yield return new WaitForSeconds(0.1f); 
            }
        }

        Debug.Log($"[CSM] Обработка инструкций от {_currentTestLogicHandler.GetType().Name} завершена.");

        ToDoManager.Instance?.HandleAction(ActionType.MoveTraverseToPosition, null);
        SetCurrentState(TestState.TraverseManualMoving);

        _approachProcessCoroutine = null;
    }
    
    private IEnumerator ResetAndLoadCatalogSceneCoroutine()
    {
        // 1. Вызываем универсальный метод сброса
    if (_isTestCurrentlyOrPreviouslyConfigured)
        {
            // 1. Вызываем наш универсальный метод сброса
            PerformFullSimulationReset();
        }
        else
        {
            // Если мы просто в режиме просмотра, сбрасывать нечего.
            Debug.Log("[CSM] Тест не был сконфигурирован. Пропуск полного сброса.");
        }

        // 2. Устанавливаем базовое состояние, чтобы CSM был "чистым"
        SetCurrentState(TestState.Idle);

        // 3. Асинхронно загружаем сцену
        string sceneToLoad = "MachineSelection"; 
        Debug.Log($"[CSM] Состояние сброшено. Загрузка сцены: {sceneToLoad}");

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneToLoad);

        while (!asyncLoad.isDone)
        {
            yield return null;
        }
    }
        
    #endregion

    #region Workflow Coroutines: Scenario Execution
    //================================================================================================================//
    // РЕГИОН 5: КОРУТИНЫ: ВЫПОЛНЕНИЕ СЦЕНАРИЕВ
    // Здесь живет НОВАЯ система. Эти методы исполняют пошаговые сценарии, полученные от хендлеров.
    // Если проблема с логикой установки/снятия образца, зажатия/разжатия захватов - смотреть сюда.
    //================================================================================================================//

    private IEnumerator ExecuteAdvisedScenario(List<ScenarioStep> scenario)
    {
        if (scenario == null || scenario.Count == 0)
        {
            _scenarioCoroutine = null;
            yield break;
        }

        SystemStateMonitor.Instance?.ReportScenarioStatus(true); 
        foreach (var step in scenario)
        {
            switch (step.Action)
            {
                case HandlerAdvisedAction.CreateSample:
                    yield return StartCoroutine(CreateSampleStep());
                    break;
                case HandlerAdvisedAction.RemoveSample:
                    PerformSampleRemoval();
                    break;
                case HandlerAdvisedAction.ClampUpperGrip:
                    ToDoManager.Instance.HandleAction(ActionType.ClampUpperGrip, null);
                    yield return new WaitUntil(() => !_isClampAnimating);
                    break;
                case HandlerAdvisedAction.UnclampUpperGrip:
                    ToDoManager.Instance.HandleAction(ActionType.UnclampUpperGrip, null);
                    yield return new WaitUntil(() => !_isClampAnimating);
                    break;
                case HandlerAdvisedAction.ClampLowerGrip:
                    ToDoManager.Instance.HandleAction(ActionType.ClampLowerGrip, null);
                    yield return new WaitUntil(() => !_isClampAnimating);
                    break;
                case HandlerAdvisedAction.UnclampLowerGrip:
                    ToDoManager.Instance.HandleAction(ActionType.UnclampLowerGrip, null);
                    yield return new WaitUntil(() => !_isClampAnimating);
                    break;

                    case HandlerAdvisedAction.SetState:
                    TestState targetEnum = (TestState)step.Argument;
                    
                    // КОНВЕРТИРУЕМ И ПЕРЕКЛЮЧАЕМ
                    StateBase newState = ConvertEnumToState(targetEnum);
                    if (newState != null)
                    {
                        TransitionToState(newState);
                    }
                    else
                    {
                        // Фоллбэк для старых состояний, которые мы еще не перенесли в классы
                        // (чтобы не сломать логику, если хендлер попросит SamplePlaced_AwaitingApproach)
                         _currentTestState = targetEnum; 
                         SystemStateMonitor.Instance.ReportTestState(targetEnum);
                    }
                    break;
                    
                case HandlerAdvisedAction.SetUnloadedFlag:
                    SystemStateMonitor.Instance?.ReportSampleUnloaded(true);
                    break;
                case HandlerAdvisedAction.ShowHint:
                    ShowHint((string)step.Argument);
                    break;
                case HandlerAdvisedAction.UpdateSampleButtonText:
                    UpdateButtonState(buttonId: "SampleButton", text: (string)step.Argument);
                    break;
                case HandlerAdvisedAction.Play_In_Animation:
                    yield return StartCoroutine(ExecuteAnimationStepAndWait((string)step.Argument, AnimationDirection.In));
                    break;
                case HandlerAdvisedAction.Play_Out_Animation:
                    yield return StartCoroutine(ExecuteAnimationStepAndWait((string)step.Argument, AnimationDirection.Out));
                    break;
                case HandlerAdvisedAction.Play_SampleInstall_Animation:
                    yield return StartCoroutine(ExecuteAnimationStepAndWait((string)step.Argument, AnimationDirection.SampleInstall));
                    break;
                case HandlerAdvisedAction.Play_SampleRemove_Animation:
                    yield return StartCoroutine(ExecuteAnimationStepAndWait((string)step.Argument, AnimationDirection.SampleRemove));
                    break;
                case HandlerAdvisedAction.SetDoorState:
                    bool open = (bool)step.Argument;
                    bool areCurrentlyClosed = SystemStateMonitor.Instance.AreDoorsClosed;
                    if (open == areCurrentlyClosed) { ToDoManager.Instance.HandleAction(ActionType.SetDoorStateAction, null); }
                    break;
            }
            yield return new WaitForSeconds(0.05f);
        }

        UpdateSampleButtonVisuals();
        _scenarioCoroutine = null;
        SystemStateMonitor.Instance?.ReportScenarioStatus(false);
        SystemStateMonitor.Instance.ReportFixtureChangeStatus(false);
    }

    private IEnumerator CreateSampleStep()
    {
        var monitor = SystemStateMonitor.Instance;
        if (monitor == null)
        {
            SetCurrentState(TestState.Error);
            yield break;
        }

        var (drivePoint, undrivePoint, currentDistance) = FindDriveUndrivePointsAndDistance(_currentTestConfiguration.samplePlacementZoneTag);
        if (drivePoint == null || undrivePoint == null)
        {
            SetCurrentState(TestState.Error);
            yield break;
        }
        
        monitor.CurrentSampleParameters.TryGetValue("Length", out float sampleLength);
        float sampleLengthMeters = sampleLength / 1000.0f; 
        if (currentDistance < sampleLengthMeters - _distanceTolerance)
        {
            ShowHint("Для установки образца выбранного размера недостаточно расстояния. Подвиньте траверсу во вкладке \"Управление\"");
            yield break;
        }

        string selectedSampleId = SelectCompatibleSampleID();
        if (string.IsNullOrEmpty(selectedSampleId))
        {
            SetCurrentState(TestState.Error);
            yield break;
        }

        monitor.CurrentSampleParameters.TryGetValue("DiameterThickness", out float actualDiameterThickness);
        monitor.CurrentSampleParameters.TryGetValue("Width", out float actualWidth);
        
        GameObject createdSample = SampleManager.Instance.CreateAndSetupSample(selectedSampleId, undrivePoint, actualDiameterThickness, actualWidth, sampleLength);
        if (createdSample == null)
        {
            SetCurrentState(TestState.Error);
            yield break;
        }
        _currentSampleInstance = createdSample;
        SystemStateMonitor.Instance?.ReportCurrentSampleInstance(_currentSampleInstance);
        SystemStateMonitor.Instance.ReportSamplePresence(true);

        // Используем 'drivePoint', 'undrivePoint' и 'sampleLengthMeters', которые у нас уже есть
        EnableDynamicLimits(drivePoint, undrivePoint, sampleLengthMeters);
    }

    private IEnumerator ExecuteAnimationStepAndWait(string fixtureId, AnimationDirection direction)
    {
        // 1. Определяем, в какой список отслеживания добавить ID
        List<string> targetList;
        if (direction == AnimationDirection.In || direction == AnimationDirection.SampleInstall)
        {
            targetList = _installingFixtureIds;
        }
        else // Out или SampleRemove
        {
            targetList = _removingFixtureIds;
        }

        // 2. Добавляем ID в список и отправляем команду
        targetList.Add(fixtureId);
        ToDoManager.Instance.HandleAction(ActionType.PlayFixtureAnimationAction, new PlayFixtureAnimationArgs(fixtureId, direction, ActionRequester.CSM));

        // 3. Ждем, пока наш глобальный обработчик HandleFixtureAnimationFinished не удалит ID из списка
        // Добавляем таймаут для безопасности, чтобы игра не зависла навсегда.
        float timeout = 15f; // 15 секунд на анимацию
        float startTime = Time.time;
        yield return new WaitUntil(() => !targetList.Contains(fixtureId) || (Time.time - startTime) > timeout);

        // 4. Проверяем, не вышли ли мы по таймауту
        if (targetList.Contains(fixtureId))
        {
            Debug.LogError($"<color=red>CSM: Таймаут ожидания анимации '{direction}' для '{fixtureId}'!</color>");
            // Принудительно удаляем ID, чтобы не блокировать дальнейшие операции
            targetList.Remove(fixtureId);
        }
    }

    #endregion

    #region Internal Event Handlers & Callbacks
    //================================================================================================================//
    // РЕГИОН 6: ВНУТРЕННИЕ ОБРАБОТЧИКИ И КОЛЛБЭКИ
    // Методы Handle..., которые вызываются НЕ пользователем, а другими системами в ответ на какое-то событие.
    // Например, "анимация завершилась", "траверса доехала", "получены новые данные графика".
    //================================================================================================================//

    private void HandleFixtureAnimationStarted(EventArgs args) { }
    private void HandleFixtureAnimationFinished(EventArgs args)
    {
        // 1. Проверяем, что заказчик - именно CSM. Если нет, игнорируем.
        if (!(args is FixtureEventArguments fa) || fa.Requester != ActionRequester.CSM) 
            return;

        // 2. Обрабатываем и УДАЛЕНИЕ, и УСТАНОВКУ.
        
        // Если этот ID был в списке на удаление, удаляем его.
        if (_removingFixtureIds.Contains(fa.FixtureId))
        {
            _removingFixtureIds.Remove(fa.FixtureId);
        }
        
        // Если этот ID был в списке на установку, тоже удаляем.
        if (_installingFixtureIds.Contains(fa.FixtureId))
        {
            _installingFixtureIds.Remove(fa.FixtureId);
        }
    }

    private void HandleTraverseApproachCompleted(EventArgs args)
    {
        SystemStateMonitor.Instance?.ReportApproachStatus(false);
        _currentState.OnApproachCompleted();
    }
    

    private void HandleGraphStepUpdated(EventArgs args)
    {
        var monitor = SystemStateMonitor.Instance;
        if (monitor == null) return;

        // CSM отправляет команду на обновление в любом случае. Исполнители сами решат, что делать, на основе состояния в Мониторе.
        ToDoManager.Instance?.HandleAction(ActionType.UpdateMachineVisuals, null);
        ToDoManager.Instance?.HandleAction(ActionType.UpdateSampleVisuals, null);

        // Логика экстензометра остается. Она зависит от решения CSM (_useExtensometerInThisTest) и должна работать только во время движения графика.
        if (_useExtensometerInThisTest && monitor.CurrentGraphState == GraphController.GraphState.Plotting)
        {
            monitor.CurrentSampleParameters.TryGetValue("Length", out float actualLength);
            float elongation_mm = (monitor.CurrentRelativeStrain_Percent / 100.0f) * actualLength;
            
            var commandArgs = new ExtensometerControlArgs(ExtensometerAction.UpdatePosition, elongation_mm: elongation_mm);
            ToDoManager.Instance.HandleAction(ActionType.ControlExtensometer, commandArgs);
        }
    }

    private void HandleTestSequenceCompleted(EventArgs args)
    {
        _currentState.OnTestFinished();
    }

    private void HandleTraverseLimitReached(EventArgs args)
    {
        if (_currentState != null) { _currentState.OnApproachCompleted(); }

        if (_currentTestState != TestState.ReadyForSetup) return;
        bool distanceMatches = CheckIfClampDistanceMatchesSampleLength();
        if (!distanceMatches) return;
        bool requiresClamping = (_currentTestConfiguration != null && 
                                (_currentTestConfiguration.requiresUpperClamp || _currentTestConfiguration.requiresLowerClamp));
        if (!requiresClamping)
        {
            TransitionToState(new ReadyToTestState(this));
        }
        else
        {
            UpdateSampleButtonVisuals();
        }
    }
    
    private void HandleClampAnimationStarted(EventArgs args)
    {
        _isClampAnimating = true;
        SystemStateMonitor.Instance?.ReportClampAnimation(true);
        _currentState.OnClampAnimationStarted();
    }

    private void HandleClampAnimationFinished(EventArgs args)
    {        
        _isClampAnimating = false;
        SystemStateMonitor.Instance?.ReportClampAnimation(false);
        _currentState?.OnClampAnimationFinished();
    }

    private void HandleHydraulicBufferActivationSuccessful(EventArgs args)
    {
    }
    
    private void HandleHydraulicBufferActivationFailed(EventArgs args)
    {
        string reason = "Не удалось активировать масляную подушку.";
        if (args is HydraulicBufferActivationFailedEventArgs failedArgs) { reason = failedArgs.Reason; }
        ShowHint(reason);
    }
    
    private void HandleUpperGripClampedInternal(EventArgs args) { SystemStateMonitor.Instance?.ReportGripState(GripType.Upper, true); }
    private void HandleUpperGripUnclampedInternal(EventArgs args) { SystemStateMonitor.Instance?.ReportGripState(GripType.Upper, false); }
    private void HandleLowerGripClampedInternal(EventArgs args) { SystemStateMonitor.Instance?.ReportGripState(GripType.Lower, true); }
    private void HandleLowerGripUnclampedInternal(EventArgs args) { SystemStateMonitor.Instance?.ReportGripState(GripType.Lower, false); }
    
    private void HandleGraphKeyPointsCalculatedInternal(EventArgs args)
    {
        var monitor = SystemStateMonitor.Instance;
        if (monitor != null && monitor.UTS_RelativeStrain_Percent >= 0 && monitor.Rupture_RelativeStrain_Percent >= 0)
        {
            _cached_X_UltimateStrength_Percent = monitor.UTS_RelativeStrain_Percent;
            _cached_X_Rupture_Percent = monitor.Rupture_RelativeStrain_Percent;

            Debug.Log($"[CSM] Cached GraphKeyPoints from Monitor: UTS_X={_cached_X_UltimateStrength_Percent:F3}%, Rupture_X={_cached_X_Rupture_Percent:F3}%");
        }
        else
        {
            Debug.LogError("[CSM] HandleGraphKeyPointsCalculatedInternal: Получен сигнал, но ключевые точки в Мониторе не валидны!");
            _cached_X_UltimateStrength_Percent = -1f;
            _cached_X_Rupture_Percent = -1f;
        }
    }

    #endregion
    
    #region Helper Methods & Utilities
    //================================================================================================================//
    // РЕГИОН 7: ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ И УТИЛИТЫ
    // "Ящик с инструментами". Все остальные методы, которые используются в разных частях класса:
    // поиск объектов, проверки, отправка команд в UI, управление состоянием и т.д.
    //================================================================================================================//
    
    private void SetCurrentState(TestState newState)
    {
        if (_currentTestState == newState) return;
        
        _currentTestState = newState;
        
        // 1. Отчет в Монитор
        SystemStateMonitor.Instance?.ReportTestState(_currentTestState); 

        // 2. Внешние системы (подсветка кнопок и т.д.)
        if (Application.isPlaying && ApplicationStateManager.Instance != null) 
        { 
            ApplicationStateManager.Instance.SetCurrentState(newState); 
        }

        // 3. Обновление UI кнопок (Sample Button текст меняется от состояния)
        UpdateSampleButtonVisuals();
        UpdateUIForCurrentState();
        UpdateHydroPumpButtonVisuals();
    }

    public void TransitionToState(StateBase newState)
    {
        // 1. Выход из старого
        if (_currentState != null)
        {
            _currentState.OnExit();
        }

        // 2. Смена ссылки
        _currentState = newState;

        if (_currentState != null)
        {
            // 3. СИНХРОНИЗАЦИЯ: Обновляем старый Enum для обратной совместимости
            _currentTestState = _currentState.StateEnum;

            if (Application.isPlaying && ApplicationStateManager.Instance != null)  {  ApplicationStateManager.Instance.SetCurrentState(_currentState.StateEnum); } // Подсветка кастомного ActionPolicy

            // 4. Вход в новое
            _currentState.OnEnter();

            // 5. Отчет в монитор
            if (SystemStateMonitor.Instance != null)
            {
                SystemStateMonitor.Instance.ReportTestState(_currentState.StateEnum);
            }
        }
    }

    private void ClearCurrentTestData()
    {
        SystemStateMonitor.Instance?.ResetTestSetupState();
        
        _currentTestConfiguration = null;
        _currentTestLogicHandler = null;
        if (_currentSampleInstance != null && (_currentTestState == TestState.Idle)) { PerformSampleRemoval(); }
        _workflowRunner?.Stop();
        if (_approachProcessCoroutine != null)
        {
            StopCoroutine(_approachProcessCoroutine);
            _approachProcessCoroutine = null;
        }
        if (_scenarioCoroutine != null)
        {
            StopCoroutine(_scenarioCoroutine);
            _scenarioCoroutine = null;
        }

        _cached_X_UltimateStrength_Percent = -1f; _cached_X_Rupture_Percent = -1f;
        
        UpdateHydroPumpButtonVisuals();
    }

    internal void PerformFullSimulationReset()
    {
        Debug.Log("[CSM] Выполнение полного сброса симуляции...");

        // Сбрасываем все внешние системы через ToDoManager
        if (ToDoManager.Instance != null)
        {
            ToDoManager.Instance.HandleAction(ActionType.PauseGraphAndSimulation, null); // На всякий случай
            ToDoManager.Instance.HandleAction(ActionType.ResetGraphAndSimulation, null);
            ToDoManager.Instance.HandleAction(ActionType.ResetTestController, null);
            ToDoManager.Instance.HandleAction(ActionType.FinalizeTestData, null); // Очищаем данные
            ToDoManager.Instance.HandleAction(ActionType.ClearLastReport, null);
            ToDoManager.Instance.HandleAction(ActionType.SetSupportSystemState, new SetSupportSystemStateArgs(false));
        }

        // Удаляем образец, если он есть
        if (_currentSampleInstance != null)
        {
            PerformSampleRemoval();
        }
        
        // Сбрасываем внутреннее состояние CSM
        ClearCurrentTestData();
    }
    
    private void UpdateUIForCurrentState() { }

    private void PerformSampleRemoval()
    {
// 1. Проверяем локальное поле CSM
    if (_currentSampleInstance == null) { return; }
    
    // 2. Уничтожаем объект, на который оно ссылается
    Destroy(_currentSampleInstance);

    // 3. Сразу же обнуляем локальное поле, чтобы избежать ошибок
    _currentSampleInstance = null;
        
    // 4. Сообщаем всем остальным через Монитор, что образца больше нет
    SystemStateMonitor.Instance?.ReportCurrentSampleInstance(null);
    SystemStateMonitor.Instance?.ReportSamplePresence(false);

    // 5. Сбрасываем динамические лимиты
    DisableDynamicLimits();
    }
    
    internal (Transform drivePoint, Transform undrivePoint, float distanceY) FindDriveUndrivePointsAndDistance(string fixtureTag)
    {
        if (string.IsNullOrEmpty(fixtureTag)) { Debug.LogError($"<color=red>CSM: FindDrive - fixtureTag пуст.</color>"); return (null, null, float.NaN); }
        GameObject[] taggedObjects;
        try { taggedObjects = GameObject.FindGameObjectsWithTag(fixtureTag); }
        catch (UnityException unityEx)  {  Debug.LogError($"<color=red>CSM: FindDrive - Ошибка тега '{fixtureTag}': {unityEx.Message}.</color>");  return (null, null, float.NaN); }
        if (taggedObjects == null || taggedObjects.Length == 0) { Debug.LogError($"<color=red>CSM: FindDrive - Нет GameObject с тегом: '{fixtureTag}'.</color>"); return (null, null, float.NaN); }
        Transform drivePoint = null; Transform undrivePoint = null;
        foreach (GameObject obj in taggedObjects) { Transform tempDrive = FindDescendantTransformByName(obj.transform, "(Drive)"); if (tempDrive != null) drivePoint = tempDrive; Transform tempUndrive = FindDescendantTransformByName(obj.transform, "(Undrive)"); if (tempUndrive != null) undrivePoint = tempUndrive; if (drivePoint != null && undrivePoint != null) break; }
        if (drivePoint == null || undrivePoint == null) { Debug.LogError($"<color=red>CSM: FindDrive - Не найдены (Drive) [{(drivePoint != null)}] и (Undrive) [{(undrivePoint != null)}] у тега '{fixtureTag}'.</color>"); return (null, null, float.NaN); }
        float currentDistance = Vector3.Distance(drivePoint.position, undrivePoint.position); return (drivePoint, undrivePoint, currentDistance);
    }
    
    private Transform FindDescendantTransformByName(Transform parent, string nameSubstring) { if (parent.name.Contains(nameSubstring)) return parent; foreach (Transform child in parent) { Transform result = FindDescendantTransformByName(child, nameSubstring); if (result != null) return result; } return null; }
    
    private bool IsManualTraverseAllowed() { if (_currentTestState == TestState.TestRunning || _currentTestState == TestState.TestPaused) { return false; } return true; }

    private string SelectCompatibleSampleID()
    {
        if (SystemStateMonitor.Instance == null) return null;
        SampleForm targetShape = SystemStateMonitor.Instance.SelectedShape;
        List<string> compatibleSampleIDs = _currentTestConfiguration.compatibleSampleIDs;
        if (compatibleSampleIDs == null || compatibleSampleIDs.Count == 0) { return null; }
        string selectedSampleId = compatibleSampleIDs.FirstOrDefault(id =>
        {
            if (string.IsNullOrEmpty(id)) return false;
            SampleData sd = SampleManager.Instance.GetSampleData(id); return sd != null && sd.sampleForm == targetShape;
        });
        if (string.IsNullOrEmpty(selectedSampleId)) { return null; }
        return selectedSampleId;
    }

    private void UnclampGripsIfNeeded()
    {
        if (ToDoManager.Instance == null) { return; }
        if (_currentTestConfiguration != null && _currentTestConfiguration.requiresUpperClamp) { ToDoManager.Instance.HandleAction(ActionType.UnclampUpperGrip, null); }
        if (_currentTestConfiguration != null && _currentTestConfiguration.requiresLowerClamp) { ToDoManager.Instance.HandleAction(ActionType.UnclampLowerGrip, null); }
    }
    
    private void EnableDynamicLimits(Transform drivePoint, Transform undrivePoint, float sampleLengthMeters)
    {
        var monitor = SystemStateMonitor.Instance;
        if (monitor == null || _currentCalculator == null) return;

        // Используем интерфейс, а не конкретный класс
        var limits = _currentCalculator.CalculateDynamicLimits(monitor, drivePoint.position, undrivePoint.position);

        monitor.ReportTraverseLimits(limits.NewMin, limits.NewMax, true);
    }
    
    private void DisableDynamicLimits()
    {
        var monitor = SystemStateMonitor.Instance;
        if (monitor == null) return;

        // Просто берем базовые лимиты из Монитора и сообщаем, что динамический режим выключен.
        monitor.ReportTraverseLimits(monitor.OriginMinLimitY, monitor.OriginMaxLimitY, false);
    }
    
    private void ShowHint(string message, float duration = 5.0f) { if (ToDoManager.Instance == null) { return; }
        ShowHintArgs hintArgs = new ShowHintArgs(message, duration); ToDoManager.Instance.HandleAction(ActionType.ShowHintText, hintArgs); }
    
    private bool CheckActionAllowedAndShowHint(EventType eventTypeFromButton)
    {
        if (actionPolicyAsset == null) { return false; }
        TypeOfTest currentTestType = (_currentTestConfiguration != null) ? _currentTestConfiguration.typeOfTest : default;
        string hintText = actionPolicyAsset.GetHintForAction(eventTypeFromButton, CurrentTestState, currentTestType);
        if (!string.IsNullOrEmpty(hintText)) { ToDoManager.Instance?.HandleAction(ActionType.ShowHintText, new ShowHintArgs(hintText)); return true; }
        return false;
    }
    
    private bool CheckIfClampDistanceMatchesSampleLength()
    {
        var monitor = SystemStateMonitor.Instance;
        if (monitor == null || monitor.CurrentSampleParameters == null || !monitor.CurrentSampleParameters.ContainsKey("Length")) return false;
        if (string.IsNullOrEmpty(_currentTestConfiguration?.samplePlacementZoneTag)) return false;

        var (drivePoint, undrivePoint, currentDistanceY) = FindDriveUndrivePointsAndDistance(_currentTestConfiguration.samplePlacementZoneTag);
        if (drivePoint == null || undrivePoint == null) return false;

        float sampleLengthMeters = monitor.CurrentSampleParameters["Length"] / 1000.0f;
        return Mathf.Abs(currentDistanceY - sampleLengthMeters) <= _distanceTolerance;
    }

    private void UpdateButtonState(string buttonId, string text = null, EventType? newEventType = null, ButtonVisualStateType? visualState = null)
    {
        if (ToDoManager.Instance == null || string.IsNullOrEmpty(buttonId)) return;    
        var args = new UpdateUIButtonVisualsArgs(buttonId, visualState, text, newEventType);
        ToDoManager.Instance.HandleAction(ActionType.UpdateUIButtonVisuals, args);
    }

    private void SendPromptUpdateCommand(string keyOrId, PromptSourceType sourceType, string sourceInfo, bool isNewTarget = true)
    {
        if (ToDoManager.Instance != null)
        {
            var promptArgs = new UpdatePromptArgs(keyOrId, sourceType, sourceInfo, isNewTarget);
            ToDoManager.Instance.HandleAction(ActionType.UpdatePromptDisplay, promptArgs);
        }
        else
        {
            Debug.LogError($"[CSM] ToDoManager.Instance не найден! Не могу отправить Prompt Update Command для ключа '{keyOrId}'.");
        }
    }
    
    private bool IsFixtureInstalled(string fixtureId)
    {
        var data = FixtureManager.Instance.GetFixtureData(fixtureId);
        if (data == null) return false;

        var installedData = FixtureController.Instance.GetInstalledFixtureInZone(data.fixtureZone);
        return installedData != null && installedData.fixtureId == fixtureId;
    }

    private void UpdateSampleButtonVisuals()
    {
        if (_currentTestState == TestState.Configuring) { return; }
        if (!SystemStateMonitor.Instance.IsSampleInPlace) { UpdateButtonState("SampleButton", text: "УСТАНОВИТЬ\nОБРАЗЕЦ"); return; }

        bool requiresClamping = _currentTestConfiguration != null && _currentTestConfiguration.requiresLowerClamp;
        bool isReadyForFinalClamping = (requiresClamping &&
                                        !SystemStateMonitor.Instance.IsLowerGripClamped &&
                                        CheckIfClampDistanceMatchesSampleLength());

        if (isReadyForFinalClamping) { UpdateButtonState("SampleButton", text: "ЗАЖАТЬ\nОБРАЗЕЦ"); }
        else { UpdateButtonState("SampleButton", text: "УБРАТЬ\nОБРАЗЕЦ"); }
    }
    
    private void UpdateHydroPumpButtonVisuals()
    {
        bool isPumpOn = SystemStateMonitor.Instance.IsPowerUnitActive;
        if (isPumpOn) { UpdateButtonState("HydroPumpButton", text: "ОТКЛЮЧИТЬ\nМАСЛЯНЫЙ НАСОС"); }
        else { UpdateButtonState("HydroPumpButton", text: "ВКЛЮЧИТЬ\nМАСЛЯНЫЙ НАСОС"); }
    }

    internal LogicHandlerContext CreateLogicHandlerContext()
    {
        var (drivePoint, undrivePoint, currentDistance) = FindDriveUndrivePointsAndDistance(_currentTestConfiguration?.samplePlacementZoneTag);
        var monitor = SystemStateMonitor.Instance; // Получаем доступ к монитору

        float requiredLength = 0f;

        // Проверяем наличие монитора и необходимых данных в нем
        if (monitor != null && _currentTestConfiguration != null && monitor.CurrentSampleParameters != null)
        {
            // 1. Берем базовую рабочую длину из монитора
            monitor.CurrentSampleParameters.TryGetValue("Length", out float workingLength);

            // 2. Находим SampleData, используя форму из монитора
            SampleForm shape = monitor.SelectedShape;
            SampleData sampleData = SampleManager.Instance.GetFirstCompatibleSampleData(_currentTestConfiguration, shape);

            float clampingLength = 0f;
            if (sampleData != null)
            {
                clampingLength = sampleData.ClampingLength;
            }
            else
            {
                Debug.LogWarning($"[CSM CreateContext] Не удалось найти SampleData для '{_currentTestConfiguration.name}'. ClampingLength будет равен 0.");
            }

            // 3. Считаем полную требуемую длину и конвертируем в метры
            requiredLength = (workingLength + (clampingLength * 2)) / 1000.0f;
        }

        var installedFixturesDict = new Dictionary<FixtureZone, string>();
        if (FixtureController.Instance != null)
        {
            foreach (FixtureZone zone in Enum.GetValues(typeof(FixtureZone)))
            {
                if (zone == FixtureZone.None) continue;
                string id = FixtureController.Instance.GetInstalledFixtureIdInZone(zone);
                if (!string.IsNullOrEmpty(id))
                {
                    installedFixturesDict[zone] = id;
                }
            }
        }

        return new LogicHandlerContext
        {
            CurrentState = _currentTestState,
            IsSamplePresent = monitor.IsSampleInPlace,
            IsSampleUnloaded = monitor.IsSampleUnloaded,
            IsUpperGripClamped = monitor.IsUpperGripClamped,
            IsLowerGripClamped = monitor.IsLowerGripClamped,
            CurrentDistance = currentDistance,
            RequiredSampleLength = requiredLength,
            InstalledFixtures = installedFixturesDict
        };
    }  

    /// <summary>
    /// Рассчитывает целевую точку для подвода траверсы и сохраняет ее в SystemStateMonitor.
    /// Этот метод следует вызывать ПОСЛЕ того, как вся необходимая оснастка установлена.
    /// </summary>
    internal void CalculateAndStoreApproachTarget()
    {
        var monitor = SystemStateMonitor.Instance;
        // Проверки на null...
        if (_currentTestLogicHandler == null || _currentTestConfiguration == null || monitor == null)
        {
            Debug.LogError("[CSM] Невозможно рассчитать точку подвода: компоненты не готовы.");
            SetCurrentState(TestState.Error);
            return;
        }

        // --- Шаг 1: Получаем точки крепления (CSM находит их в мире по тегам) ---
        var (drivePoint, undrivePoint, _) = FindDriveUndrivePointsAndDistance(_currentTestConfiguration.samplePlacementZoneTag);
        if (drivePoint == null || undrivePoint == null)
        {
            Debug.LogError("[CSM] Не удалось найти точки (Drive)/(Undrive).");
            SetCurrentState(TestState.Error);
            return;
        }

        // --- Шаг 2: Получаем параметры образца ---
        ApproachGuidanceOutput guidance = _currentTestLogicHandler.GetApproachGuidance(_currentTestConfiguration);
        
        monitor.CurrentSampleParameters.TryGetValue("Length", out float sampleLength_mm);
        SampleData sampleData = SampleManager.Instance.GetFirstCompatibleSampleData(_currentTestConfiguration, monitor.SelectedShape);
        float clampingLength_mm = sampleData?.ClampingLength ?? 0f;
        float effectiveDimension_mm = (sampleLength_mm + (clampingLength_mm * 2));


        // --- ШАГ 3 : Мы просто отдаем мировые координаты монитору ---
        
        SystemStateMonitor.Instance.RequestApproachCalculation(
            drivePoint.position, 
            undrivePoint.position, 
            guidance.ActionType
        );

        Debug.Log($"<color=cyan>[CSM] Запрос на расчет точки подвода отправлен в MachineController.</color>");
    }

    internal void StartAutoApproachSequence()
    {
        if (_approachProcessCoroutine != null) StopCoroutine(_approachProcessCoroutine);
        _approachProcessCoroutine = StartCoroutine(ProcessApproachSequenceCoroutine());
    }

    internal void RunScenario(List<ScenarioStep> scenario)
    {
        if (_scenarioCoroutine != null) return; // Защита
        _scenarioCoroutine = StartCoroutine(ExecuteAdvisedScenario(scenario));
    }

    // Свойство для проверки занятости (чтобы State знал, можно ли жать кнопку)
    public bool IsScenarioRunning => _scenarioCoroutine != null;

    private StateBase ConvertEnumToState(TestState stateEnum)
    {
switch (stateEnum)
        {
            // --- ОСНОВНЫЕ (Используются в сценариях) ---
            case TestState.ReadyForSetup: 
                return new ReadyForSetupState(this);
            
            case TestState.ReadyToTest: 
                return new ReadyToTestState(this);
            
            case TestState.TestResult_SampleSafe: 
                return new TestResult_SampleSafeState(this);
            
            case TestState.TestFinished_SampleUnderLoad: 
                return new TestFinished_SampleUnderLoadState(this);

            case TestState.Configuring: 
                return new ConfiguringState(this);
                
            case TestState.Idle: 
                return new IdleState(this);

            // --- ТЕХНИЧЕСКИЕ (Можно добавить, но вряд ли сценарий их вызовет) ---
            case TestState.TestRunning: 
                return new TestRunningState(this);
            
            case TestState.TestPaused: 
                return new TestPausedState(this);

            // --- СЛОЖНЫЕ (Не добавлять, требуют аргументов в конструкторе) ---

            default: 
                Debug.LogWarning($"[CSM Factory] Нет конвертации для {stateEnum}. Переход проигнорирован.");
                return null;
        }
    }

    internal void StopAutoApproachSequence()
    {
        if (_approachProcessCoroutine != null)
        {
            StopCoroutine(_approachProcessCoroutine);
            _approachProcessCoroutine = null;
        }
        
        // Сброс флага в мониторе
        SystemStateMonitor.Instance?.ReportApproachStatus(false);
    }

    internal void SwitchUIToViewMode()
    {
        // 1. Сброс локальных флагов CSM
        _isClampAnimating = false; // Важно, чтобы не блокировать UI

        // 2. Отключение контейнеров (ВСЕХ, что были в старом коде)
        var uiArgsMain = new SetUIContainerActiveArgs("MainButtonContainer", false);
        ToDoManager.Instance.HandleAction(ActionType.SetUIContainerActive, uiArgsMain);
        
        var uiArgsScreen = new SetUIContainerActiveArgs("ScreenContainer", false);
        ToDoManager.Instance.HandleAction(ActionType.SetUIContainerActive, uiArgsScreen);        

        // 3. Переключение глобальных кнопок (Меню / Домой)
        _eventManager?.RaiseEvent(EventType.GlobalModeButtonsVisibilityChanged, 
            new GlobalModeButtonsVisibilityEventArgs(this, showMenu: true, showHome: false));

        // 4. Переключение режима отображения (Надпись)
        var modeArgs = new SetDisplayModeArgs("РЕЖИМ ПРОСМОТРА"); 
        ToDoManager.Instance?.HandleAction(ActionType.SetDisplayMode, modeArgs);
        
        // 5. Сообщение в монитор
        SystemStateMonitor.Instance?.ReportApplicationMode(ApplicationMode.ViewMode);

        // 6. Промпты
        SendPromptUpdateCommand("CSM_TestFinished", PromptSourceType.SystemAction, "UserAction");

        // 7. Камера
        _eventManager.RaiseEvent(EventType.FocusCameraRequested, new FocusCameraEventArgs(this, CameraFocusTarget.Overview));
    }

    // Публичный метод для вызова смены состояния из Сценария
    public void TransitionToStateByEnum(TestState stateEnum)
    {
        StateBase newState = ConvertEnumToState(stateEnum);
        if (newState != null)
        {
            TransitionToState(newState);
        }
        else
        {
            Debug.LogWarning($"[CSM] Не удалось конвертировать Enum {stateEnum} в класс состояния.");
        }
    }

    #endregion
}