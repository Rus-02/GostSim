using UnityEngine;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Linq;

public class SimpleHingeDoorController : IDoorController
{
    // --- Прямой перенос полей из MachineController ---
    private readonly List<GameObject> _doors;
    private readonly float _doorOpenAngle;
    private readonly float _doorAnimationDuration;

    private bool _areDoorsOpen = false;
    private readonly List<Quaternion> _initialDoorRotations = new List<Quaternion>();
    private readonly List<Tween> _doorTweens = new List<Tween>();
    
    // --- Конструктор для инициализации ---
    public SimpleHingeDoorController(List<GameObject> doors, float openAngle, float duration)
    {
        _doors = doors ?? new List<GameObject>();
        _doorOpenAngle = openAngle;
        _doorAnimationDuration = duration;

        // --- Перенос логики из вашего StoreInitialDoorRotations() ---
        foreach (var door in _doors)
        {
            _initialDoorRotations.Add(door != null ? door.transform.localRotation : Quaternion.identity);
        }
    }

    // --- Реализация интерфейса ---
    public void OpenDoors(Action onComplete = null)
    {
        // "Открыть" соответствует вашему openTargetState = true
        SetDoorState(true, onComplete);
    }

    public void CloseDoors(Action onComplete = null)
    {
        // "Закрыть" соответствует вашему openTargetState = false
        SetDoorState(false, onComplete);
    }

    /// <summary>
    /// ПРЯМОЙ ПЕРЕНОС ВАШЕГО МЕТОДА SetDoorState
    /// </summary>
    private void SetDoorState(bool openTargetState, Action onComplete)
    {
        if (_doors == null || _doors.Count == 0) 
        {
            onComplete?.Invoke();
            return;
        }

        if (_areDoorsOpen == openTargetState && _doorTweens.All(t => t == null || !t.IsActive())) 
        {
            onComplete?.Invoke();
            return;
        }

        KillAllTweens();

        for (int i = 0; i < _doors.Count; i++)
        {
            GameObject doorObject = _doors[i];
            if (doorObject == null || i >= _initialDoorRotations.Count) continue;

            Quaternion targetRotation = openTargetState 
                ? (_initialDoorRotations[i] * Quaternion.Euler(0, 0, _doorOpenAngle)) 
                : _initialDoorRotations[i];
            
            Tween doorTween = doorObject.transform
                .DOLocalRotateQuaternion(targetRotation, _doorAnimationDuration)
                .SetEase(Ease.InOutSine);
            
            _doorTweens.Add(doorTween);
        }

        _areDoorsOpen = openTargetState;
        
        // ВАЖНО: Сообщаем в монитор. areDoorsClosed = !areDoorsOpen
        SystemStateMonitor.Instance?.ReportDoorState(!_areDoorsOpen);

        // Добавляем вызов onComplete к последней анимации
        if (_doorTweens.Count > 0)
        {
            _doorTweens.LastOrDefault()?.OnComplete(() => onComplete?.Invoke());
        }
        else
        {
            onComplete?.Invoke();
        }
    }

    // --- Прямой перенос вспомогательных методов ---
    public void KillAllTweens()
    {
        foreach (var tween in _doorTweens)
        {
            tween?.Kill();
        }
        _doorTweens.Clear();
    }
}