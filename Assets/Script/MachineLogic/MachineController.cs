using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using System;
using System.Linq;

public class MachineController : MonoBehaviour
{
    #region Singleton Implementation
    private static MachineController _instance;
    public static MachineController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<MachineController>();
                if (_instance == null)
                {
                    Debug.LogError("MachineController Instance is null and couldn't be found. Make sure MachineController is present in the scene.");
                }
            }
            return _instance;
        }
    }
    #endregion

    #region Поля и свойства

    // Единственная точка входа в логику. МК больше не знает о деталях реализации.
    private IMachineLogic _currentLogic;

    // Мы ищем конфиг на этом объекте, чтобы создать логику.
    private MachineConfigBase _config;

    // --- Публичные свойства (Прокси) ---
    // МК предоставляет доступ к важным частям, спрашивая их у текущей логики.
    
    public Transform MovingTraverseRoot => _currentLogic?.TraverseTransform;
    
    public bool IsUpperClamped => _currentLogic != null && _currentLogic.IsUpperClamped;
    public bool IsLowerClamped => _currentLogic != null && _currentLogic.IsLowerClamped;

    // Свойство занятости для локальных проверок (если нужно), синхронизируется с логикой
    private bool _isHydraulicSystemBusy = false; 

    #endregion

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        _instance = this;

        // 1. Ищем любой компонент конфигурации на этом объекте
        _config = GetComponent<MachineConfigBase>();

        if (_config == null)
        {
            Debug.LogError("MachineController: CRITICAL! No Machine Configuration component found on this GameObject!");
            return;
        }

        // 2. Просим конфиг создать соответствующую логику
        _currentLogic = _config.CreateLogic();
        
        // 3. Подписываемся на события логики
        _currentLogic.OnBusyStateChanged += HandleLogicBusyStateChanged;
        
        // Подписываемся на изменение статуса готовности
        _currentLogic.OnReadyStateChanged += HandleReadyStateChanged;
        _currentLogic.OnActionRejected += HandleLogicActionRejected;
        _currentLogic.OnPowerUnitStateChanged += HandlePowerUnitStateChanged;
        Debug.Log($"[MachineController] Initialized with logic: {_currentLogic.GetType().Name}");

        SubscribeToToDoManagerCommands();
    }

    private void Start()
    {
        // Сообщаем начальный статус готовности в Монитор при старте
        HandleReadyStateChanged();
    }

    private void Update()
    {
        // Делегируем обновление логике
        _currentLogic?.OnUpdate();
    }

    private void OnDestroy()
    {
        UnsubscribeFromToDoManagerCommands();
        
        if (_currentLogic != null)
        {
            _currentLogic.OnBusyStateChanged -= HandleLogicBusyStateChanged;
            _currentLogic.OnReadyStateChanged -= HandleReadyStateChanged;
            _currentLogic.OnActionRejected -= HandleLogicActionRejected;
            _currentLogic.OnPowerUnitStateChanged -= HandlePowerUnitStateChanged;
            _currentLogic.OnDestroy();
        }
    }

    #region ToDoManager Command Subscription
    private void SubscribeToToDoManagerCommands()
    {
        if (ToDoManager.Instance == null)
        {
            Debug.LogError("[MachineController] ToDoManager.Instance is null during subscription!");
            return;
        }        
        var tm = ToDoManager.Instance;

        tm.SubscribeToAction(ActionType.MoveTraverse, HandleMoveTraverseCommand);
        tm.SubscribeToAction(ActionType.MoveTraverseToPosition, HandleApproachTraverseCommand);
        tm.SubscribeToAction(ActionType.StopTraverseAction, HandleStopTraverseCommand);
        tm.SubscribeToAction(ActionType.AdjustSpeed, HandleAdjustSpeedCommand);
        tm.SubscribeToAction(ActionType.ClampUpperGrip, HandleClampUpperGripCommand);
        tm.SubscribeToAction(ActionType.UnclampUpperGrip, HandleUnclampUpperGripCommand);
        tm.SubscribeToAction(ActionType.ClampLowerGrip, HandleClampLowerGripCommand);
        tm.SubscribeToAction(ActionType.UnclampLowerGrip, HandleUnclampLowerGripCommand);
        tm.SubscribeToAction(ActionType.UpdateMachineVisuals, HandleUpdateMachineVisualsCommand);
        
        // Новые универсальные команды
        tm.SubscribeToAction(ActionType.ControlLoader, HandleControlLoaderCommand);
        tm.SubscribeToAction(ActionType.SetSupportSystemState, HandleSetSupportSystemStateCommand);
        
        tm.SubscribeToAction(ActionType.SetDoorStateAction, HandleSetDoorStateCommand);
        tm.SubscribeToAction(ActionType.EnsureFixtureInstallationClearance, HandleEnsureFixtureInstallationClearanceCommand);
        tm.SubscribeToAction(ActionType.AnimatePumpOn, HandleAnimatePumpOn);
        tm.SubscribeToAction(ActionType.AnimatePumpOff, HandleAnimatePumpOff);

        if (SystemStateMonitor.Instance != null)
        {
            SystemStateMonitor.Instance.OnApproachCalcRequested += HandleApproachCalcRequest;
        }
    }

    private void UnsubscribeFromToDoManagerCommands()
    {
        var tm = ToDoManager.Instance;
        if (tm != null)
        {
            tm.UnsubscribeFromAction(ActionType.MoveTraverse, HandleMoveTraverseCommand);
            tm.UnsubscribeFromAction(ActionType.MoveTraverseToPosition, HandleApproachTraverseCommand);
            tm.UnsubscribeFromAction(ActionType.StopTraverseAction, HandleStopTraverseCommand);
            tm.UnsubscribeFromAction(ActionType.AdjustSpeed, HandleAdjustSpeedCommand);
            tm.UnsubscribeFromAction(ActionType.ClampUpperGrip, HandleClampUpperGripCommand);
            tm.UnsubscribeFromAction(ActionType.UnclampUpperGrip, HandleUnclampUpperGripCommand);
            tm.UnsubscribeFromAction(ActionType.ClampLowerGrip, HandleClampLowerGripCommand);
            tm.UnsubscribeFromAction(ActionType.UnclampLowerGrip, HandleUnclampLowerGripCommand);
            tm.UnsubscribeFromAction(ActionType.UpdateMachineVisuals, HandleUpdateMachineVisualsCommand);
            
            tm.UnsubscribeFromAction(ActionType.ControlLoader, HandleControlLoaderCommand);
            tm.UnsubscribeFromAction(ActionType.SetSupportSystemState, HandleSetSupportSystemStateCommand);
            
            tm.UnsubscribeFromAction(ActionType.SetDoorStateAction, HandleSetDoorStateCommand);
            tm.UnsubscribeFromAction(ActionType.EnsureFixtureInstallationClearance, HandleEnsureFixtureInstallationClearanceCommand);
            tm.UnsubscribeFromAction(ActionType.AnimatePumpOn, HandleAnimatePumpOn);
            tm.UnsubscribeFromAction(ActionType.AnimatePumpOff, HandleAnimatePumpOff);
        }

        if (SystemStateMonitor.Instance != null)
        {
            SystemStateMonitor.Instance.OnApproachCalcRequested -= HandleApproachCalcRequest;
        }
    }
    #endregion

    #region Public API Methods (Сохранено для совместимости)
    
    // Перенаправление всех вызовов в _currentLogic
    
    public void StopMovingTraverse() => _currentLogic?.StopManualPositioning();

    public void MoveMovingTraverseFastUp() => _currentLogic?.StartManualPositioning(Direction.Up, SpeedType.Fast);

    public void MoveMovingTraverseFastDown() => _currentLogic?.StartManualPositioning(Direction.Down, SpeedType.Fast);

    public void MoveMovingTraverseSlowUp() => _currentLogic?.StartManualPositioning(Direction.Up, SpeedType.Slow);

    public void MoveMovingTraverseSlowDown() => _currentLogic?.StartManualPositioning(Direction.Down, SpeedType.Slow);

    public void IncreaseTraverseSlowSpeed() => _currentLogic?.AdjustManualPositioningSpeed(true);

    public void DecreaseTraverseSlowSpeed() => _currentLogic?.AdjustManualPositioningSpeed(false);

    public void ApproachTraverse(float targetZLocal)
    {
        if (_currentLogic == null || MovingTraverseRoot == null) return;

        Transform traverseParent = MovingTraverseRoot.parent;
        Vector3 currentLocalPos = traverseParent.InverseTransformPoint(MovingTraverseRoot.position);
        Vector3 targetLocalPos = new Vector3(currentLocalPos.x, currentLocalPos.y, targetZLocal);
        float targetWorldY = traverseParent.TransformPoint(targetLocalPos).y;

        _currentLogic.StartAutomaticApproach(targetWorldY);
    }
    
    public void ClampUpper() => _currentLogic?.ClampUpper();
    public void UnclampUpper() => _currentLogic?.UnclampUpper();
    public void ClampLower() => _currentLogic?.ClampLower();
    public void UnclampLower() => _currentLogic?.UnclampLower();

    #endregion

    #region ToDoManager Command Handlers

    // --- Traverse Handlers ---
    private void HandleMoveTraverseCommand(BaseActionArgs baseArgs)
    {
        if (baseArgs is MoveTraverseArgs args)
        {
            var direction = args.Direction > 0 ? Direction.Up : Direction.Down;
            var speed = (args.Speed == SpeedType.Fast) ? SpeedType.Fast : SpeedType.Slow;
            
            _currentLogic?.StartManualPositioning(direction, speed);
        }
    }

    private void HandleApproachTraverseCommand(BaseActionArgs baseArgs)
    {
        float targetZLocal = SystemStateMonitor.Instance.LastApproachTargetZ;
        if (float.IsNaN(targetZLocal)) return;
        ApproachTraverse(targetZLocal);
    }

    private void HandleStopTraverseCommand(BaseActionArgs baseArgs) 
    { 
        _currentLogic?.StopManualPositioning();
    }
    
    private void HandleAdjustSpeedCommand(BaseActionArgs baseArgs)
    {
        if (baseArgs is AdjustSpeedArgs args)
        {
            _currentLogic?.AdjustManualPositioningSpeed(args.Change > 0);
        }
    }

    // --- Универсальные обработчики нагрузки (Бывшие Hydro) ---

    private void HandleControlLoaderCommand(BaseActionArgs baseArgs)
    {
        if (baseArgs is ControlLoaderArgs args)
        {
            if (args.IsStopCommand)
            {
                _currentLogic?.StopManualLoading();
            }
            else
            {
                // Просто передаем команду, устранив зависимость от _isTraverseMoving
                _currentLogic?.StartManualLoading(args.MoveDirection, args.MoveSpeed);
            }
        }
    }

    private void HandleSetSupportSystemStateCommand(BaseActionArgs baseArgs)
    {
        if (baseArgs is SetSupportSystemStateArgs args)
        {
            _currentLogic?.SetSupportSystemState(args.Activate);
        }
    }

    // --- Visual Update Handler ---
    private void HandleUpdateMachineVisualsCommand(BaseActionArgs baseArgs)
    {
        var monitor = SystemStateMonitor.Instance;
        if (monitor == null) return;

        float currentGraphRelativeDeformationPercent = monitor.CurrentRelativeStrain_Percent;
        var graphState = monitor.CurrentGraphState;
        monitor.CurrentSampleParameters.TryGetValue("Length", out float actualSampleLengthFromArgs);

        ITestLogicHandler currentHandler = monitor.CurrentTestLogicHandler;
        TestConfigurationData currentConfig = monitor.CurrentTestConfig;

        float pistonDisplacementMeters = 0f;
        if (currentHandler != null && currentConfig != null)
        {
            pistonDisplacementMeters = currentHandler.GetPistonDisplacementFromGraphValue(currentGraphRelativeDeformationPercent, actualSampleLengthFromArgs, currentConfig);
        }
        else
        {
            if (actualSampleLengthFromArgs > 0)
            {
                float absoluteDisplacement_mm = (currentGraphRelativeDeformationPercent / 100f) * actualSampleLengthFromArgs;
                pistonDisplacementMeters = absoluteDisplacement_mm / 1000.0f;
            }
        }

        if (graphState == GraphController.GraphState.Plotting)
        {
            _currentLogic?.ApplyProgrammaticDisplacement(pistonDisplacementMeters);
        }
        else if (graphState == GraphController.GraphState.Paused || graphState == GraphController.GraphState.Finished || graphState == GraphController.GraphState.Error)
        {
            _currentLogic?.Loader.Stop();
        }
        else if (graphState == GraphController.GraphState.Idle)
        {
             _currentLogic?.StopManualLoading();
        }
    }

    // --- Other Handlers ---

    private void HandleClampUpperGripCommand(BaseActionArgs baseArgs) => ClampUpper();
    private void HandleUnclampUpperGripCommand(BaseActionArgs baseArgs) => UnclampUpper();
    private void HandleClampLowerGripCommand(BaseActionArgs baseArgs) => ClampLower();
    private void HandleUnclampLowerGripCommand(BaseActionArgs baseArgs) => UnclampLower();

    private void HandleSetDoorStateCommand(BaseActionArgs baseArgs)
    {
        bool AreDoorsClosed = SystemStateMonitor.Instance.AreDoorsClosed;
        if (!AreDoorsClosed) _currentLogic?.Doors.CloseDoors();
        else _currentLogic?.Doors.OpenDoors();
    }
        
    private void HandleEnsureFixtureInstallationClearanceCommand(BaseActionArgs baseArgs)
    {
        if (!(baseArgs is EnsureFixtureInstallationClearanceArgs args)) return;
        if (_currentLogic == null) return;

        // Делегируем логику обеспечения зазора
        _currentLogic.EnsureClearance(args.GeneralTestType, args.TargetLocalZ, args.Requester);
    }

    private void HandleAnimatePumpOn(BaseActionArgs args) => _currentLogic?.SetPowerUnitState(true);
    private void HandleAnimatePumpOff(BaseActionArgs args) => _currentLogic?.SetPowerUnitState(false);
    
    // Обработчик общего события занятости от Логики
    private void HandleLogicBusyStateChanged(bool isBusy)
    {
        if (_isHydraulicSystemBusy != isBusy)
        {
            _isHydraulicSystemBusy = isBusy;
            if (isBusy) EventManager.Instance?.RaiseEvent(EventType.HydraulicOperationStarted, EventArgs.Empty);
            else EventManager.Instance?.RaiseEvent(EventType.HydraulicOperationFinished, EventArgs.Empty);
        }
    }

    // Обработчик изменения статуса готовности машины (например, Подушка)
    private void HandleReadyStateChanged()
    {
        if (_currentLogic == null || SystemStateMonitor.Instance == null) return;

        // Транслируем статус из Логики в Монитор
        SystemStateMonitor.Instance.ReportMachineReadyStatus(
            _currentLogic.IsReadyForSetup, 
            _currentLogic.NotReadyReason
        );
    }

    private void HandleLogicActionRejected(string reason)
    { ToDoManager.Instance?.HandleAction(ActionType.ShowHintText, new ShowHintArgs(reason)); }

    private void HandlePowerUnitStateChanged(bool isActive)
    {
        // 1. Сообщаем в Монитор (это уже было)
        SystemStateMonitor.Instance?.ReportPowerUnitState(isActive);

        // 2. ДОБАВЛЕНО: Показываем хинт
        string msg = isActive ? "Масляный насос включен" : "Масляный насос отключен";
        ToDoManager.Instance?.HandleAction(ActionType.ShowHintText, new ShowHintArgs(msg));
    }

    private void HandleApproachCalcRequest()
    {
        var monitor = SystemStateMonitor.Instance;
        
        // Достаем длину образца, которая УЖЕ есть в мониторе
        monitor.CurrentSampleParameters.TryGetValue("Length", out float sampleLen);
        float effectiveLen = sampleLen + (monitor.CurrentClampingLength * 2);

        // Дергаем Логику (она сама напишет результат обратно в Монитор)
        _currentLogic?.CalculateAndReportApproach(
            monitor.ReqDrivePos, 
            monitor.ReqUndrivePos, 
            effectiveLen, 
            monitor.ReqActionType
        );
    }    
    #endregion
}