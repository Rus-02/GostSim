using UnityEngine;
using DG.Tweening;
using System;
using System.Collections.Generic;

public class HydroFrameComponent : IMoveableComponent
{
    // --- Ссылки ---
    private readonly GameObject _hydroAssemblyRoot;
    private readonly GameObject _movingTraverseRoot; 
    private readonly List<Transform> _hydroAssemblyBellowBones;
    
    // --- Параметры ---
    private readonly float _hydroReturnDuration;
    private readonly float _hydraulicBufferMoveDuration;
    private readonly float _hydroFastSpeedChange;
    private readonly float _hydroSlowSpeedChange;
    private readonly float _hydroUpperLimitOffset;

    private readonly Vector3 _motionAxis;

    // --- Состояние ---
    // Храним стартовую позицию как Vector3 для расчетов в мире
    private Vector3 _hydroAssemblyStartPosition;
    
    private Vector3 _initialHydroAssemblyPosition; 
    private List<Vector3> _initialHydroAssemblyBoneLocalPositions = new List<Vector3>();

    private float _currentHydroSpeed = 0f;
    private bool _isHydroManuallyMoving = false;
    
    public bool IsManuallyMoving => _isHydroManuallyMoving;

    private bool _hydraulicBufferActivated = false;
    
    // Текущее смещение от старта (аналог Y, но вдоль оси)
    private float _currentDisplacement = 0f;
    public float CurrentDisplacement => _currentDisplacement;
    

    // --- Твины ---
    private Tween _hydroReturnTween;
    private Tween _hydraulicBufferTween;

    public bool IsBusy => _isHydroManuallyMoving || 
                          (_hydroReturnTween != null && _hydroReturnTween.IsActive()) || 
                          (_hydraulicBufferTween != null && _hydraulicBufferTween.IsActive());

    public bool IsBufferActivated => _hydraulicBufferActivated;
    
    public event Action<bool> OnBusyStateChanged;

    public HydroFrameComponent(
        GameObject hydroAssemblyRoot,
        GameObject movingTraverseRoot,
        List<Transform> bellowBones,
        float returnDuration,
        float bufferDuration,
        float fastSpeedChange,
        float slowSpeedChange,
        float upperLimitOffset,
        Vector3 motionAxis)
    {
        _hydroAssemblyRoot = hydroAssemblyRoot;
        _movingTraverseRoot = movingTraverseRoot;
        _hydroAssemblyBellowBones = bellowBones;
        _hydroReturnDuration = returnDuration;
        _hydraulicBufferMoveDuration = bufferDuration;
        _hydroFastSpeedChange = fastSpeedChange;
        _hydroSlowSpeedChange = slowSpeedChange;
        _hydroUpperLimitOffset = upperLimitOffset;        
        _motionAxis = motionAxis.normalized;

        if (_hydroAssemblyRoot != null)
        {
            _hydroAssemblyStartPosition = _hydroAssemblyRoot.transform.position;
            _initialHydroAssemblyPosition = _hydroAssemblyStartPosition;
        }

        StoreInitialBellowBoneData();
    }

    // --- Реализация IMoveableComponent ---

    public void SetPositionByDisplacement(float absoluteDisplacement)
    {
        if (_isHydroManuallyMoving) 
        {
            _isHydroManuallyMoving = false;
            _currentHydroSpeed = 0f;
        }
        
        float bufferOffset = _hydraulicBufferActivated ? 0.01f : 0f;
        
        // Работаем со смещением (Displacement), а не с абсолютной Y
        float targetDisplacement = absoluteDisplacement + bufferOffset;

        float final_lower = 0f; // 0 от старта
        float final_upper = _hydroUpperLimitOffset; // Макс смещение

        if (!SystemStateMonitor.Instance.IsDynamicLimitsActive && _movingTraverseRoot != null)
        {
            // 1. Считаем текущее расстояние между Траверсой и Стартом Рамы (вдоль оси)
            // Это аналог (_movingTraverseRoot.y - _initialHydroAssemblyPosition.y)
            float currentDistToTraverse = Vector3.Dot(_movingTraverseRoot.transform.position - _hydroAssemblyStartPosition, _motionAxis);

            // 2. Считаем safeGap.
            // В оригинале: safeGap = OriginMinLimitY - _initialHydroAssemblyPosition.y
            // Трактуем это как "минимально допустимое расстояние" (константу).
            // OriginMinLimitY берем из монитора (предполагаем, что оно настроено корректно как число).
            float safeGap = SystemStateMonitor.Instance.OriginMinLimitY - _initialHydroAssemblyPosition.y;
            // Если мы в горизонтали и Y=0, safeGap может сломаться.
            // Чтобы "просто работало" как раньше, берем абсолютную разницу, если это константа дистанции.
            // Но лучше следовать формуле:
            if (Mathf.Abs(safeGap) < 0.001f) safeGap = 0.05f; // Фолбэк, если Y одинаковые (горизонталь)

            // 3. Считаем лимит
            // collision_limit = traversePos - safeGap
            // Но в системе смещений: collision_limit (displacement) = currentDistToTraverse - safeGap
            float collision_limit = currentDistToTraverse - safeGap;
            
            final_upper = Mathf.Min(final_upper, collision_limit);
        }

        float clampedDisplacement = Mathf.Clamp(targetDisplacement, final_lower, final_upper);
        
        // Применяем
        ApplyDisplacement(clampedDisplacement);
        
        UpdateBellowBones();
    }

    public void MoveContinuously(Direction direction, float speed)
    {
        ChangeManualSpeed(direction == Direction.Up, speed >= 0.01f); 
    }

    public void AdjustContinuousSpeed(bool increase) { }

    public void Stop()
    {
        _currentHydroSpeed = 0f;
        _isHydroManuallyMoving = false;
        KillTween(ref _hydroReturnTween);
        KillTween(ref _hydraulicBufferTween);
        ReportBusyState();
    }

    public void MoveTo(float targetPosition, float speed, Action onCompleteCallback) { }

    // --- Специфичные методы ---

    public void ChangeManualSpeed(bool isUp, bool isFast)
    {
        _isHydroManuallyMoving = true;
        float change = isFast ? _hydroFastSpeedChange : _hydroSlowSpeedChange;
        if (isUp) _currentHydroSpeed += change;
        else _currentHydroSpeed -= change;
        ReportBusyState();
    }

    public void ManualStopAndReturn()
    {
        _currentHydroSpeed = 0f;
        _isHydroManuallyMoving = false;
        _hydraulicBufferActivated = false; 

        KillTween(ref _hydroReturnTween);
        
        // Твиним смещение в 0
        _hydroReturnTween = DOTween.To(() => _currentDisplacement, x => ApplyDisplacement(x), 0f, _hydroReturnDuration)
            .SetEase(Ease.InOutSine)
            .OnUpdate(UpdateBellowBones)
            .OnComplete(() => { 
                _hydroReturnTween = null; 
                UpdateBellowBones(); 
                ReportBusyState(); 
            })
            .OnKill(() => ReportBusyState());
            
        ReportBusyState();
    }

    public void StopManualSpeed()
    {
        if (_isHydroManuallyMoving)
        {
            _currentHydroSpeed = 0f;
            _isHydroManuallyMoving = false;
            ReportBusyState();
        }
    }

    public void ActivateBuffer(Action onSuccess, Action<string> onFail)
    {
        if (_hydraulicBufferActivated) { onSuccess?.Invoke(); return; }

        // Проверка по смещению (вместо Y)
        bool isHydroBaseAtHome = Mathf.Abs(_currentDisplacement) < 0.001f;
        
        if (!isHydroBaseAtHome)
        { 
            onFail?.Invoke("Невозможно поднять масляную подушку. Сначала полностью опустите силовую раму."); 
            return; 
        }

        Stop();
        
        _hydraulicBufferActivated = true; 
        
        // Твиним смещение к 0.01
        _hydraulicBufferTween = DOTween.To(() => _currentDisplacement, x => ApplyDisplacement(x), 0.01f, _hydraulicBufferMoveDuration)
            .SetEase(Ease.OutSine)
            .OnUpdate(UpdateBellowBones)
            .OnComplete(() => { 
                _hydraulicBufferTween = null; 
                onSuccess?.Invoke(); 
                ReportBusyState();
            })
            .OnKill(() => ReportBusyState());
            
        ReportBusyState();
    }

    public void ResetBuffer()
    {
        Stop();
        _hydraulicBufferActivated = false;

        // Возврат в 0
        _hydraulicBufferTween = DOTween.To(() => _currentDisplacement, x => ApplyDisplacement(x), 0f, _hydraulicBufferMoveDuration)
            .SetEase(Ease.InOutSine)
            .OnUpdate(UpdateBellowBones)
            .OnComplete(() => {
                _hydraulicBufferTween = null;
                UpdateBellowBones();
                ReportBusyState();
            })
            .OnKill(() => ReportBusyState());
            
        ReportBusyState();
    }

    // --- Метод тика ---
    public void ManualMoveTick()
    {
        if (_isHydroManuallyMoving && !Mathf.Approximately(_currentHydroSpeed, 0f))
        {
            ApplyHydroSpeedMovement();
            UpdateBellowBones();
            ReportBusyState();
        }
    }
    
    // --- Приватная логика ---

    private void ApplyHydroSpeedMovement()
    {
        if (_hydroAssemblyRoot == null) return;

        // 1. Считаем будущее смещение
        float targetDisplacement = _currentDisplacement + (_currentHydroSpeed * Time.deltaTime);

        // 2. Лимиты (в пространстве смещений)
        float final_lower = 0f;
        float final_upper = _hydroUpperLimitOffset;

        if (_movingTraverseRoot != null)
        {
            // АНАЛОГ safeGap
            // Дистанция = (TraversePos - StartPos) DOT Axis
            float currentDistToTraverse = Vector3.Dot(_movingTraverseRoot.transform.position - _hydroAssemblyStartPosition, _motionAxis);
            
            // safeGap берем как константу разницы Y из конфига (OriginMin - StartY).
            // Если машина горизонтальная, это число может быть странным, но мы сохраняем логику "как было".
            float safeGap = SystemStateMonitor.Instance.OriginMinLimitY - _initialHydroAssemblyPosition.y;
            
            // Если вдруг safeGap околонулевой (из-за того что Y равны), ставим дефолт 5см
            if (Mathf.Abs(safeGap) < 0.001f) safeGap = 0.05f;

            // collision_limit = (TraversePos - StartPos) - safeGap
            // Это точная копия формулы: Traverse.y - safeGap - StartPos.y (в пересчете на смещение)
            float collision_limit = currentDistToTraverse - safeGap;
            
            final_upper = Mathf.Min(final_upper, collision_limit);
        }

        // 3. Кламп
        float clampedDisplacement = Mathf.Clamp(targetDisplacement, final_lower, final_upper);
        float limitTolerance = 0.0001f;
        
        // 4. Проверка на остановку
        if ((_currentHydroSpeed > 0 && clampedDisplacement >= final_upper - limitTolerance) || 
            (_currentHydroSpeed < 0 && clampedDisplacement <= final_lower + limitTolerance))
        {
            _currentHydroSpeed = 0f;
            _isHydroManuallyMoving = false;
        }

        // 5. Применяем
        ApplyDisplacement(clampedDisplacement);
    }

    private void ApplyDisplacement(float displacement)
    {
        _currentDisplacement = displacement;
        SystemStateMonitor.Instance?.ReportHydroDisplacement(_currentDisplacement); // РЕПОРТИМ В МОНИТОР
        if (_hydroAssemblyRoot != null)
        {
            // Start + Axis * Displacement
            _hydroAssemblyRoot.transform.position = _hydroAssemblyStartPosition + (_motionAxis * _currentDisplacement);
        }
    }

    private void UpdateBellowBones()
    {
        if (_hydroAssemblyBellowBones == null || _hydroAssemblyRoot == null) return;
        for (int i = 0; i < _hydroAssemblyBellowBones.Count; i++)
        {
            Transform bone = _hydroAssemblyBellowBones[i];
            if (bone != null && i < _initialHydroAssemblyBoneLocalPositions.Count)
            {
                bone.position = _hydroAssemblyRoot.transform.TransformPoint(_initialHydroAssemblyBoneLocalPositions[i]);
            }
        }
    }

    private void StoreInitialBellowBoneData()
    {
        _initialHydroAssemblyBoneLocalPositions.Clear();
        if (_hydroAssemblyBellowBones == null || _hydroAssemblyRoot == null) return;
        foreach (var bone in _hydroAssemblyBellowBones)
        {
            if (bone != null)
            {
                _initialHydroAssemblyBoneLocalPositions.Add(_hydroAssemblyRoot.transform.InverseTransformPoint(bone.position));
            }
        }
    }

    private void KillTween(ref Tween tween)
    {
        if (tween != null && tween.IsActive()) tween.Kill();
        tween = null;
    }
    
    private void ReportBusyState()
    {
        OnBusyStateChanged?.Invoke(IsBusy);
    }
}