using UnityEngine;
using DG.Tweening;
using System;
using System.Collections.Generic;

/// <summary>
/// Реализует IMoveableComponent для управления траверсой машины.
/// Инкапсулирует всю логику движения, анимации вращения и DOTween.
/// Логика и имена полей/методов максимально сохранены из оригинального MachineController.
/// </summary>
public class TraverseComponent : IMoveableComponent
{
    // --- Ссылки на объекты и параметры (передаются извне) ---
    private readonly GameObject _movingTraverseRoot;
    private readonly List<GameObject> _rotatingParts;
    private readonly GameObject _beltGameObject;
    private readonly GRMTextureMovement _beltTextureMovement;
    private readonly List<Transform> _movingTraverseBellowBones;
    private readonly float _traverseFastSpeed;
    private float _traverseSlowSpeed; // Не readonly, т.к. изменяется
    private readonly float _slowSpeedStep;
    private readonly float _minSlowSpeed;
    private readonly float _maxSlowSpeed;
    private readonly float _gearRotationSpeed;    
    private readonly Vector3 _initialTraversePosition; // Запоминаем начальную МИРОВУЮ позицию
    private readonly Vector3 _motionAxis;  // Ось движения считаем её направлением в МИРЕ

    // Public properties for logic access
    public float FastSpeed => _traverseFastSpeed;
    public float CurrentSlowSpeed => _traverseSlowSpeed;
    public Transform MovingTraverseRoot => _movingTraverseRoot.transform;

    // Событие изменения статуса занятости (движения)
    public event Action<bool> OnBusyStateChanged;

    // --- Внутреннее состояние компонента ---
    private Tween _currentTraverseTween;
    private List<Tween> _currentRotationTweens = new List<Tween>();
    private float _currentSlowMoveDirection = 0f;
    private float _currentRotationDirection = 1f;
    private bool _isMovingAtSlowSpeed = false; // Flag to track if we are currently in slow mode
    private Action _onApproachCompleteCallback;

    private readonly List<Vector3> _initialMovingTraverseBoneLocalPositions = new List<Vector3>();

    /// <summary>
    /// Конструктор для инициализации компонента.
    /// </summary>
    public TraverseComponent(
        GameObject movingTraverseRoot,
        List<GameObject> rotatingParts,
        GameObject beltGameObject,
        GRMTextureMovement beltTextureMovement,
        List<Transform> bellowBones,
        float fastSpeed, float slowSpeed, float slowStep, float minSlow, float maxSlow,
        float gearSpeed, Vector3 motionAxis) // Аргумент оси
    {
        // Сохраняем все зависимости
        _movingTraverseRoot = movingTraverseRoot;
        _rotatingParts = rotatingParts;
        _beltGameObject = beltGameObject;
        _beltTextureMovement = beltTextureMovement;
        _movingTraverseBellowBones = bellowBones;
        _traverseFastSpeed = fastSpeed;
        _traverseSlowSpeed = slowSpeed;
        _slowSpeedStep = slowStep;
        _minSlowSpeed = minSlow;
        _maxSlowSpeed = maxSlow;
        _gearRotationSpeed = gearSpeed;
        
        // Нормализуем ось
        _motionAxis = motionAxis.normalized;

        if (_movingTraverseRoot != null)
        {
            // Сохраняем МИРОВУЮ позицию. Это база.
            _initialTraversePosition = _movingTraverseRoot.transform.position;
        }

        // Выполняем инициализацию, которая была в Awake()
        StoreInitialBellowBoneData();
    }

    // --- Реализация интерфейса IMoveableComponent ---

    public void MoveContinuously(Direction direction, float speed)
    {
        SystemStateMonitor.Instance?.ReportTraverseManualMoveStatus(true); //Сообщаем о начале движения
        
        OnBusyStateChanged?.Invoke(true); // Сообщаем подписчикам, что компонент занят

        // Конвертируем enum в float для старого метода
        float directionFloat = (direction == Direction.Up) ? 1f : -1f;
        _currentSlowMoveDirection = directionFloat; 
        
        // Check if the requested speed matches our slow speed setting
        _isMovingAtSlowSpeed = Mathf.Approximately(speed, _traverseSlowSpeed);
        
        MoveMovingTraverse(directionFloat, speed);
    }
    
    public void AdjustContinuousSpeed(bool increase)
    {
        // Allow adjusting speed even if stopped, but only apply movement change if moving at slow speed
        
        float savedDirection = _currentSlowMoveDirection;
        float savedSpeed = _traverseSlowSpeed;
        
        // We don't need to Stop() if we are just updating the value, 
        // but if we are moving, we might want to restart smoothly.
        // The original logic called Stop(), which kills tweens.
        // If we are NOT moving at slow speed (e.g. Fast or Stopped), we shouldn't interrupt.
        
        if (_isMovingAtSlowSpeed && savedDirection != 0)
        {
             Stop(); 
             _currentSlowMoveDirection = savedDirection; // Restore direction after Stop() cleared it
        }

        // Update the speed value
        _traverseSlowSpeed = savedSpeed; // Restore in case Stop() messed with it (it shouldn't, but safe)
        
        if (increase)
            _traverseSlowSpeed += _slowSpeedStep;
        else
            _traverseSlowSpeed -= _slowSpeedStep;
            
        _traverseSlowSpeed = Mathf.Clamp(_traverseSlowSpeed, _minSlowSpeed, _maxSlowSpeed);
        
        // Apply new speed ONLY if we were already moving at slow speed
        if (_isMovingAtSlowSpeed && _currentSlowMoveDirection != 0)
        {
            MoveMovingTraverse(_currentSlowMoveDirection, _traverseSlowSpeed);
        }
    }

    public void Stop()
    {
        // Метод StopMovingTraverse был переименован в Stop для соответствия интерфейсу,
        // но его внутренняя логика осталась прежней.
        if (_currentTraverseTween != null && _currentTraverseTween.IsActive())
        {
            _currentTraverseTween.Kill();
            _currentTraverseTween = null;
        }
        StopRotation();
        _currentSlowMoveDirection = 0f;
        _isMovingAtSlowSpeed = false; // Reset flag on stop
        _beltTextureMovement?.StopMovement();
        SystemStateMonitor.Instance?.ReportTraverseManualMoveStatus(false); //Сообщаем о завершении движения

        OnBusyStateChanged?.Invoke(false); // Сообщаем подписчикам, что компонент свободен
    }
    
    public void MoveTo(float targetPosition, float speed, Action onCompleteCallback)
    {
        SystemStateMonitor.Instance?.ReportTraverseManualMoveStatus(true);
        OnBusyStateChanged?.Invoke(true);

        if (_movingTraverseRoot == null) 
        {
            // Если объекта нет, сразу завершаем, чтобы не зависнуть
            onCompleteCallback?.Invoke();
            return;
        }
        
        _onApproachCompleteCallback = onCompleteCallback;
        
        KillCurrentMovement();
        StartRotation(1f, speed); 

        Transform traverseTransform = _movingTraverseRoot.transform;
        
        // Используем МИРОВУЮ позицию
        Vector3 currentWorldPos = traverseTransform.position;
        
        // Получаем лимиты
        var limits = GetCurrentLimits();
        float clampedTargetVal = Mathf.Clamp(targetPosition, limits.minY, limits.maxY);

        // Убираем из текущей позиции компоненту оси, чтобы заменить её на новую
        Vector3 currentPosNoAxis = currentWorldPos - (_motionAxis * Vector3.Dot(currentWorldPos, _motionAxis));
        // Добавляем новую компоненту вдоль оси
        Vector3 targetWorldPos = currentPosNoAxis + (_motionAxis * clampedTargetVal);
        
        float distance = Vector3.Distance(currentWorldPos, targetWorldPos);

        float duration = (speed > 0) ? distance / speed : 0.1f;
        duration = Mathf.Max(duration, 0.01f);

        // --- ИСПРАВЛЕНИЕ ---
        // Если двигаться не нужно (мы уже на месте), 
        // мы ОБЯЗАНЫ вызвать завершение процесса вручную.
        if (distance < 0.0001f) 
        { 
            StopRotationOnLimitReached();
            
            // Принудительно вызываем завершение
            OnTraverseMoveComplete(); 
            return; 
        }
        // -------------------

        // Используем DOMove (мировые координаты)
        _currentTraverseTween = traverseTransform.DOMove(targetWorldPos, duration)
            .SetEase(Ease.Linear)
            .OnUpdate(UpdateBellowBones)
            .OnComplete(OnTraverseMoveComplete);
    }

    public void SetPositionByDisplacement(float absoluteDisplacement)
    {
        // Для электромеханики. Двигает траверсу по данным графика.
        // Используем мировую ось и мировую начальную позицию
        Vector3 targetPosition = _initialTraversePosition + (_motionAxis * absoluteDisplacement);
        _movingTraverseRoot.transform.position = targetPosition;
        UpdateBellowBones();
    }

    /// <summary>
    /// Метод для принудительной остановки всех анимаций при уничтожении объекта.
    /// </summary>
    public void KillAllTweens()
    {
        if (_currentTraverseTween != null && _currentTraverseTween.IsActive())
        {
            _currentTraverseTween.Kill();
        }
        StopRotation();
    }


    // --- ПРИВАТНЫЕ МЕТОДЫ (СКОПИРОВАНЫ 1-В-1 ИЗ MachineController) ---

    private (float minY, float maxY) GetCurrentLimits()
    {
        // Логика получения лимитов осталась прежней
        var monitor = SystemStateMonitor.Instance;
        float baseMin = monitor.IsDynamicLimitsActive ? monitor.CurrentMinTraverseLimitY : monitor.OriginMinLimitY;
        float baseMax = monitor.IsDynamicLimitsActive ? monitor.CurrentMaxTraverseLimitY : monitor.OriginMaxLimitY;

        // ЧИТАЕМ СМЕЩЕНИЕ ИЗ МОНИТОРА
        float hydroOffset = monitor.CurrentHydroDisplacement;

        // Поднимаем пол на высоту подъема рамы
        return (baseMin + hydroOffset, baseMax);
    }
    
    private void MoveMovingTraverse(float direction, float speed)
    {
        var limits = GetCurrentLimits();
        if (_movingTraverseRoot == null || Mathf.Approximately(speed, 0)) return;
        
        KillCurrentMovement();
        StartRotation(direction, speed);
        
        // Работаем в МИРОВЫХ координатах
        Transform t = _movingTraverseRoot.transform;
        Vector3 currentWorldPos = t.position;
        
        // Проецируем текущую мировую позицию на ось
        float currentVal = Vector3.Dot(currentWorldPos, _motionAxis);

        // Определяем цель (мин или макс) в зависимости от направления
        float targetVal = (direction > 0) ? limits.maxY : limits.minY;
        
        // Считаем дистанцию
        float distance = Mathf.Abs(targetVal - currentVal);
        
        // Формируем целевой вектор: убираем старую проекцию, добавляем новую
        Vector3 currentPosNoAxis = currentWorldPos - (_motionAxis * currentVal);
        Vector3 targetWorldPos = currentPosNoAxis + (_motionAxis * targetVal);

        float duration = (Mathf.Abs(speed) > 0.0001f) ? distance / Mathf.Abs(speed) : float.MaxValue;
        
        if (distance < 0.0001f || duration <= 0) { StopRotationOnLimitReached(); return; }
        
        // Используем DOMove
        _currentTraverseTween = t.DOMove(targetWorldPos, duration)
            .SetEase(Ease.Linear)
            .OnUpdate(UpdateBellowBones)
            .OnComplete(StopRotationOnLimitReached);
    }

    private void StartRotation(float direction, float traverseSpeed)
    {
        if (Mathf.Approximately(traverseSpeed, 0)) return;
        _currentRotationDirection = direction;
        StopRotation();
        _currentRotationTweens.Clear();

        if (_rotatingParts != null)
        {
            foreach (var part in _rotatingParts)
            {
                if (part == null) continue;
                float rotationAngle = 360f * _currentRotationDirection;
                float duration = 1f / (Mathf.Abs(traverseSpeed) * _gearRotationSpeed);
                duration = Mathf.Max(duration, 0.01f);
                Tween rotationTween = part.transform.DORotate(new Vector3(0f, rotationAngle, 0f), duration, RotateMode.WorldAxisAdd)
                    .SetLoops(-1).SetEase(Ease.Linear).SetTarget(part.transform);
                _currentRotationTweens.Add(rotationTween);
            }
        }
        _beltTextureMovement?.StartMovement(_gearRotationSpeed * _currentRotationDirection);
    }

    private void StopRotation()
    {
        foreach (var tween in _currentRotationTweens) { tween?.Kill(); }
        _currentRotationTweens.Clear();
        _beltTextureMovement?.StopMovement();
    }

    private void OnTraverseMoveComplete()
    {
        StopRotation();
        _currentTraverseTween = null;
        SystemStateMonitor.Instance?.ReportTraverseManualMoveStatus(false); //Сообщаем о завершении движения
        
        OnBusyStateChanged?.Invoke(false); // Сообщаем подписчикам, что компонент свободен
        
        _onApproachCompleteCallback?.Invoke(); // Вызываем новый коллбэк
        _onApproachCompleteCallback = null;
    }

    private void StopRotationOnLimitReached()
    {
        // Логика осталась идентичной, просто вызывает Stop(), который теперь содержит логику StopMovingTraverse
        Stop(); 
    }

    private void KillCurrentMovement()
    {
        if (_currentTraverseTween != null && _currentTraverseTween.IsActive())
        {
            _currentTraverseTween.Kill();
            _currentTraverseTween = null;
        }
        StopRotation();
        _currentSlowMoveDirection = 0f;
    }

    private void UpdateBellowBones()
    {
        if (_movingTraverseBellowBones == null || _movingTraverseRoot == null) return;
        for (int i = 0; i < _movingTraverseBellowBones.Count; i++)
        {
            Transform bone = _movingTraverseBellowBones[i];
            if (bone != null && i < _initialMovingTraverseBoneLocalPositions.Count)
            {
                bone.position = _movingTraverseRoot.transform.TransformPoint(_initialMovingTraverseBoneLocalPositions[i]);
            }
        }
    }

    private void StoreInitialBellowBoneData()
    {
        _initialMovingTraverseBoneLocalPositions.Clear();
        if (_movingTraverseBellowBones == null || _movingTraverseRoot == null) return;
        foreach (var bone in _movingTraverseBellowBones)
        {
            if (bone != null)
            {
                _initialMovingTraverseBoneLocalPositions.Add(_movingTraverseRoot.transform.InverseTransformPoint(bone.position));
            }
        }
    }
}