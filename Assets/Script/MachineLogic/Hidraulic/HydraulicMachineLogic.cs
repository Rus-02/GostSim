using UnityEngine;
using DG.Tweening;
using System;
using System.Collections.Generic;

/// <summary>
/// Реализация логики для Гидравлической машины.
/// Собирает воедино компоненты: Траверсу, Гидрораму, Двери и Гидрозахваты.
/// </summary>
public class HydraulicMachineLogic : IMachineLogic
{
    // --- Компоненты системы ---
    private Vector3 _localMotionAxis; 
    private TraverseComponent _traverse;
    private HydroFrameComponent _hydro;
    private SimpleHingeDoorController _doors;
    private HydraulicGripController _grips;

    // --- Визуализация (специфично для гидравлики) ---
    private List<Transform> _manometerNeedles;
    private float _needleActiveAngle;
    private float _needleAnimationDuration;
    private List<Tween> _needleTweens = new List<Tween>();

    // --- Реализация свойств интерфейса ---
    public IMoveableComponent Positioner => _traverse;
    public IMoveableComponent Loader => _hydro;
    public IDoorController Doors => _doors;
    public IGripController Grips => _grips;

    public Transform TraverseTransform => _traverse?.MovingTraverseRoot;
    public bool IsUpperClamped => _grips != null && _grips.IsUpperClamped;
    public bool IsLowerClamped => _grips != null && _grips.IsLowerClamped;

    // Свойства готовности машины к настройке.
    public bool IsReadyForSetup => _hydro != null && _hydro.IsBufferActivated;
    public string NotReadyReason => "Для установки образца или подвода траверсы поднимите масляную подушку на вкладке \"Управление\".";
    
    public event Action OnReadyStateChanged;
    public event Action<bool> OnBusyStateChanged;
    public event Action<string> OnActionRejected;
    public event Action<bool> OnPowerUnitStateChanged; // Событие смены питания

    private bool _isPumpActive = false;

    // --- Инициализация ---
public void Initialize(HydraulicMachineConfig config)
    {
        if (config == null) { Debug.LogError("HydraulicMachineLogic: Config is null!"); return; }

        _doors = new SimpleHingeDoorController(config.DoorObjects, config.DoorOpenAngle, config.DoorAnimationDuration);

        // 1. ОБЪЯВЛЯЕМ ПЕРЕМЕННУЮ axis
        // Берем её из конфига и нормализуем (делаем длину равной 1)
        Vector3 axis = config.LocalMotionAxis.normalized;
        _localMotionAxis = axis; 

        // 2. ПЕРЕДАЕМ axis В КОНСТРУКТОР ТРАВЕРСЫ (12-й аргумент)
        _traverse = new TraverseComponent(
            config.MovingTraverseRoot, config.RotatingParts, config.BeltGameObject, config.BeltTextureMovement,
            config.MovingTraverseBellowBones, config.TraverseFastSpeed, config.TraverseSlowSpeed,
            config.SlowSpeedStep, config.MinSlowSpeed, config.MaxSlowSpeed, config.GearRotationSpeed,
            axis
        );

        // 3. ПЕРЕДАЕМ axis В КОНСТРУКТОР ГИДРОРАМЫ
        _hydro = new HydroFrameComponent(
            config.HydroAssemblyRoot, config.MovingTraverseRoot, config.HydroAssemblyBellowBones,
            config.HydroReturnDuration, config.HydraulicBufferMoveDuration,
            config.HydroFastSpeedChange, config.HydroSlowSpeedChange, config.HydroUpperLimitOffset,
            axis
        );

        _grips = new HydraulicGripController(
            config.UpperHydroPiston, config.UpperLeftClampZone, config.UpperRightClampZone,
            config.LowerHydroPiston, config.LowerLeftClampZone, config.LowerRightClampZone,
            config.ClampDuration, config.PistonVerticalDisplacement, config.ZoneHorizontalDisplacement
        );

        _manometerNeedles = config.ManometerNeedles;
        _needleActiveAngle = config.NeedleActiveAngle;
        _needleAnimationDuration = config.NeedleAnimationDuration;

        // Подписки
        _traverse.OnBusyStateChanged += CheckBusyState;
        _hydro.OnBusyStateChanged += CheckBusyState;
        _grips.OnBusyStateChanged += CheckBusyState;
        _hydro.OnBusyStateChanged += (isBusy) => NotifyReadyStateChanged();
    }

    // --- Жизненный цикл ---
    public void OnUpdate()
    {
        if (_hydro != null && _hydro.IsManuallyMoving) _hydro.ManualMoveTick();
    }
    
    public void OnDestroy()
    {
        _traverse?.KillAllTweens();
        _hydro?.Stop();
        _grips?.Stop();
        _doors?.KillAllTweens();
        foreach (var t in _needleTweens) t.Kill();
        
        if (_traverse != null) _traverse.OnBusyStateChanged -= CheckBusyState;
        if (_grips != null) _grips.OnBusyStateChanged -= CheckBusyState;
        if (_hydro != null) _hydro.OnBusyStateChanged -= CheckBusyState;
    }

    // --- Команды ---

    // 1. Траверса (Электромеханика -> БЕЗ проверки насоса)
    public void StartManualPositioning(Direction direction, SpeedType speed)
    {
        if (_hydro.IsManuallyMoving || _hydro.IsBusy) _hydro.ManualStopAndReturn();
        
        float speedVal = (speed == SpeedType.Fast) ? _traverse.FastSpeed : _traverse.CurrentSlowSpeed;
        _traverse.MoveContinuously(direction, speedVal);
    }

    public void AdjustManualPositioningSpeed(bool increase)
    {
        if (_hydro.IsManuallyMoving || _hydro.IsBusy) _hydro.ManualStopAndReturn();
        _traverse.AdjustContinuousSpeed(increase);
    }

    public void StopManualPositioning() => _traverse.Stop();

    // 2. Авто-подвод (Электромеханика -> БЕЗ проверки насоса)
    public void StartAutomaticApproach(float targetPosition)
    {
        _hydro.StopManualSpeed();
        float approachSpeed = _traverse.FastSpeed; 
        _traverse.MoveTo(targetPosition, approachSpeed, () => 
        {
            EventManager.Instance?.RaiseEvent(EventType.TraverseApproachCompleted, EventArgs.Empty);
        });
    }

    // 3. Силовая рама (Гидравлика -> С проверкой насоса)
    public void StartManualLoading(Direction direction, SpeedType speed)
    {
        ///НОВОЕ///
        if (!_isPumpActive)
        {
            OnActionRejected?.Invoke("Невозможно переместить силовую раму: Масляный насос выключен.");
            return;
        }
        ///КОНЕЦ НОВОЕ///

        _traverse.Stop();
        bool isUp = (direction == Direction.Up);
        bool isFast = (speed == SpeedType.Fast);
        _hydro.ChangeManualSpeed(isUp, isFast);
        NotifyReadyStateChanged();
    }

    public void StopManualLoading()
    {
        _hydro.ManualStopAndReturn();
        NotifyReadyStateChanged();
    }

    // 4. Программа (Тест)
    public void ApplyProgrammaticDisplacement(float displacement)
    {
        _traverse.Stop(); 
        _hydro.SetPositionByDisplacement(displacement);
    }

    // 5. Подушка (Гидравлика -> С проверкой насоса)
    public void SetSupportSystemState(bool isActive)
    {
        ///НОВОЕ///
        if (isActive && !_isPumpActive)
        {
            OnActionRejected?.Invoke("Невозможно поднять масляную подушку: Масляный насос выключен.");
            return;
        }
        ///КОНЕЦ НОВОЕ///

        if (isActive)
        {
            _hydro.ActivateBuffer(
                onSuccess: () => { EventManager.Instance?.RaiseEvent(EventType.HydraulicBufferActivationSuccessful, new EventArgs(null)); NotifyReadyStateChanged(); },
                onFail: (reason) => { EventManager.Instance?.RaiseEvent(EventType.HydraulicBufferActivationFailed, new HydraulicBufferActivationFailedEventArgs(null, reason)); NotifyReadyStateChanged(); }
            );
        }
        else
        {
            _hydro.ResetBuffer();
            NotifyReadyStateChanged(); 
        }
    }

    // 6. Питание
    public void SetPowerUnitState(bool isOn)
    {
        if (isOn)
        {
            _isPumpActive = true;
            AnimateNeedles(_needleActiveAngle);
            OnPowerUnitStateChanged?.Invoke(true);
        }
        else
        {
            // Не даем выключить, если работает гидравлика или захваты
            bool isHydroBusy = (_hydro != null && _hydro.IsBusy) || (_grips != null && _grips.IsAnimating);
            if (isHydroBusy)
            {
                OnActionRejected?.Invoke("Невозможно выключить насос: работают механизмы.");
                return;
            }

            _isPumpActive = false;
            AnimateNeedles(0f);
            OnPowerUnitStateChanged?.Invoke(false);
        }
    }

    // 7. Захваты (Гидравлика -> С проверкой насоса)
    // Используем вспомогательный метод для проверки, чтобы не дублировать код
    public void ClampUpper() { if (CheckPump("зажатие")) _grips?.ClampUpper(); }
    public void UnclampUpper() { if (CheckPump("разжатие")) _grips?.UnclampUpper(); }
    public void ClampLower() { if (CheckPump("зажатие")) _grips?.ClampLower(); }
    public void UnclampLower() { if (CheckPump("разжатие")) _grips?.UnclampLower(); }

    private bool CheckPump(string actionName)
    {
        var monitor = SystemStateMonitor.Instance;

        // 1. АБСОЛЮТНЫЙ ПРИОРИТЕТ: Идет процесс смены оснастки.
        // Этот флаг ставится в начале корутины ExecutePreciseFixtureChangeWorkflow.
        // Если он true — мы игнорируем насос, так как это действие системы.
        if (monitor.IsFixtureChangeInProgress)
        {
            return true;
        }

        // 2. ПРОВЕРКА СОСТОЯНИЯ (Резерв)
        // Если мы еще в Idle или только заходим в Configuring
        if (monitor.CurrentTestState == TestState.Configuring || 
            monitor.CurrentTestState == TestState.Idle)
        {
            return true;
        }

        // 3. ПРОВЕРКА НАСОСА (Обычный режим)
        if (!_isPumpActive)
        {
            OnActionRejected?.Invoke($"Невозможно выполнить {actionName}: Масляный насос выключен.");
            return false;
        }
        
        return true;
    }

    // 8. Расчет зазора (из MC)
    public void EnsureClearance(TestType testType, float? targetLocalZ, ActionRequester requester)
    {
        _hydro.StopManualSpeed();
        Transform traverse = TraverseTransform;
        if (traverse == null) return;

        var limits = GetCurrentLimits();
        Transform traverseParent = traverse.parent;
        float targetPointInLocalZ;

        if (requester == ActionRequester.CSM)
        {
            if (testType == TestType.Tensile)
            {
                Vector3 worldPosMaxY = new Vector3(traverse.position.x, limits.maxY, traverse.position.z);
                targetPointInLocalZ = traverseParent.InverseTransformPoint(worldPosMaxY).z;
            }
            else if (testType == TestType.Compression)
            {
                Vector3 worldPosMinY = new Vector3(traverse.position.x, limits.minY, traverse.position.z);
                targetPointInLocalZ = traverseParent.InverseTransformPoint(worldPosMinY).z;
            }
            else return;
        }
        else 
        {
            if (!targetLocalZ.HasValue) return;
            targetPointInLocalZ = targetLocalZ.Value;
        }

        float currentLocalZ = traverseParent.InverseTransformPoint(traverse.position).z;
        bool needsToMove = false;

        if (testType == TestType.Tensile && currentLocalZ > targetPointInLocalZ) needsToMove = true;
        else if (testType == TestType.Compression && currentLocalZ < targetPointInLocalZ) needsToMove = true;

        if (needsToMove)
        {
            Vector3 targetLocalPos = new Vector3(traverse.localPosition.x, traverse.localPosition.y, targetPointInLocalZ);
            float targetWorldY = traverseParent.TransformPoint(targetLocalPos).y;

            _traverse.MoveTo(targetWorldY, _traverse.FastSpeed, () =>
            {
                EventManager.Instance?.RaiseEvent(EventType.FixtureInstallationClearanceReady, new FixtureInstallationClearanceReadyEventArgs(null, requester));
            });
        }
        else
        {
            EventManager.Instance.RaiseEvent(EventType.FixtureInstallationClearanceReady, new FixtureInstallationClearanceReadyEventArgs(null, requester));
        }
    }

    // 9. Расчет и отчет о точке подвода (Реализация Fire and Forget для CSM)
public void CalculateAndReportApproach(Vector3 driveWorld, Vector3 undriveWorld, float effectiveLen, ApproachActionType type)
    {
        if (TraverseTransform == null) return;

        Transform railRoot = TraverseTransform.parent;
        
        // 1. Мир -> Локал
        Vector3 driveLocal = railRoot.InverseTransformPoint(driveWorld);
        Vector3 undriveLocal = railRoot.InverseTransformPoint(undriveWorld);
        Vector3 traverseLocal = TraverseTransform.localPosition;

        // 2. Получаем ось (обязательно нормализованную!)
        Vector3 axis = _localMotionAxis.normalized;

        // 3. Аргументы
        var args = new ApproachCalculationArgs
        {
            DrivePosLocal = driveLocal,
            UndrivePosLocal = undriveLocal,
            TraverseCenterLocal = traverseLocal,
            EffectiveDimension_mm = effectiveLen,
            ActionType = type,
            LocalMotionAxis = axis
        };

        // 4. Считаем скаляр (число)
        // Создаем калькулятор на лету. Axis для конструктора берем условно, 
        // так как новый метод CalculateApproachTargetLocalScalar полагается на Vector3 ось в args.
        var calculator = new HydraulicMachineCalculator(); 
        float targetScalar = calculator.CalculateApproachTargetLocalScalar(args);

        // --- ИСПРАВЛЕНИЕ МАТЕМАТИКИ ЗДЕСЬ ---
        
        // А. Находим, какая часть текущего вектора лежит на оси движения (проекция)
        // Формула: (Vector . Axis) * Axis
        Vector3 currentProjectionVector = axis * Vector3.Dot(traverseLocal, axis);
        
        // Б. Получаем "базовый" вектор без учета оси движения (перпендикулярная плоскость)
        Vector3 baseVectorNoAxis = traverseLocal - currentProjectionVector;
        
        // В. Добавляем новую целевую высоту вдоль оси
        Vector3 targetLocalVec = baseVectorNoAxis + (axis * targetScalar);

        // ------------------------------------

        // 5. Локал -> Мир
        float targetWorldY = railRoot.TransformPoint(targetLocalVec).y;

        // 6. Репорт
        SystemStateMonitor.Instance.ReportApproachTarget(targetScalar, targetWorldY);
        
        Debug.Log($"[HydraulicLogic] Calculated Approach: LocalScalar={targetScalar:F4}, WorldY={targetWorldY:F4}");
    }

    // Хелпер для конвертации (можно вынести в утилиты)
    private HydraulicMachineCalculator.Axis GetCalculatorAxisFromVector(Vector3 v)
    {
        // Берем абсолютные значения, так как ось может быть (0, -1, 0)
        if (Mathf.Abs(v.x) > 0.9f) return HydraulicMachineCalculator.Axis.X;
        if (Mathf.Abs(v.y) > 0.9f) return HydraulicMachineCalculator.Axis.Y;
        return HydraulicMachineCalculator.Axis.Z;
    }

    // --- Внутренние методы ---

    private void CheckBusyState(bool componentBusy)
    {
        bool isBusy = (_hydro != null && _hydro.IsBusy) || (_grips != null && _grips.IsAnimating); 
        OnBusyStateChanged?.Invoke(isBusy);
    }
    
    private void NotifyReadyStateChanged() => OnReadyStateChanged?.Invoke();

    private void AnimateNeedles(float targetAngle)
    {
        foreach (var tween in _needleTweens) { tween.Kill(); }
        _needleTweens.Clear();
        if (_manometerNeedles == null) return;
        foreach (var needle in _manometerNeedles)
        {
            if (needle != null)
            {
                Tween t = needle.DOLocalRotate(new Vector3(targetAngle, 0, 0), _needleAnimationDuration).SetEase(Ease.OutSine);
                _needleTweens.Add(t);
            }
        }
    }
    
    private (float minY, float maxY) GetCurrentLimits()
    {
        var monitor = SystemStateMonitor.Instance;
        if (monitor == null) return (-10f, 10f); 
        float finalMinY = monitor.IsDynamicLimitsActive ? monitor.CurrentMinTraverseLimitY : monitor.OriginMinLimitY;
        float finalMaxY = monitor.IsDynamicLimitsActive ? monitor.CurrentMaxTraverseLimitY : monitor.OriginMaxLimitY;
        return (finalMinY, finalMaxY);
    }
}