using UnityEngine;
using DG.Tweening;
using System;
using System.Collections.Generic;

public class HydraulicGripController : IGripController
{
    // --- Ссылки на объекты (передаются через конструктор) ---
    private readonly Transform _upperHydroPiston;
    private readonly Transform _upperLeftClampZone;
    private readonly Transform _upperRightClampZone;
    private readonly Transform _lowerHydroPiston;
    private readonly Transform _lowerLeftClampZone;
    private readonly Transform _lowerRightClampZone;

    // --- Параметры ---
    private readonly float _clampDuration;
    private readonly float _pistonVerticalDisplacement;
    private readonly float _zoneHorizontalDisplacement;

    // --- Состояние ---
    private bool _isUpperClamped = false;
    private bool _isLowerClamped = false;
    private int _activeClampAnimations = 0;

    // --- Начальные позиции (кэшируем при старте) ---
    private readonly Vector3 _initialUpperPistonLocalPos;
    private readonly Vector3 _initialUpperLeftZoneLocalPos;
    private readonly Vector3 _initialUpperRightZoneLocalPos;
    private readonly Vector3 _initialLowerPistonLocalPos;
    private readonly Vector3 _initialLowerLeftZoneLocalPos;
    private readonly Vector3 _initialLowerRightZoneLocalPos;

    // --- Твины ---
    private Tween _upperClampTween;
    private Tween _lowerClampTween;

    // --- Свойства интерфейса ---
    public bool IsUpperClamped => _isUpperClamped;
    public bool IsLowerClamped => _isLowerClamped;
    public bool IsAnimating => _activeClampAnimations > 0;
    
    // Событие для уведомления MC о смене состояния занятости (опционально)
    public event Action<bool> OnBusyStateChanged;


    public HydraulicGripController(
        Transform upperPiston, Transform upperLeft, Transform upperRight,
        Transform lowerPiston, Transform lowerLeft, Transform lowerRight,
        float duration, float pistonDisplacement, float zoneDisplacement)
    {
        _upperHydroPiston = upperPiston;
        _upperLeftClampZone = upperLeft;
        _upperRightClampZone = upperRight;
        _lowerHydroPiston = lowerPiston;
        _lowerLeftClampZone = lowerLeft;
        _lowerRightClampZone = lowerRight;

        _clampDuration = duration;
        _pistonVerticalDisplacement = pistonDisplacement;
        _zoneHorizontalDisplacement = zoneDisplacement;

        // Сохраняем начальные позиции (аналог StoreInitialClampPositions)
        if (_upperHydroPiston) _initialUpperPistonLocalPos = _upperHydroPiston.localPosition;
        if (_upperLeftClampZone) _initialUpperLeftZoneLocalPos = _upperLeftClampZone.localPosition;
        if (_upperRightClampZone) _initialUpperRightZoneLocalPos = _upperRightClampZone.localPosition;
        if (_lowerHydroPiston) _initialLowerPistonLocalPos = _lowerHydroPiston.localPosition;
        if (_lowerLeftClampZone) _initialLowerLeftZoneLocalPos = _lowerLeftClampZone.localPosition;
        if (_lowerRightClampZone) _initialLowerRightZoneLocalPos = _lowerRightClampZone.localPosition;
    }

    public void ClampUpper(Action onComplete = null) => DoClampAnimation(true, true, onComplete);
    public void UnclampUpper(Action onComplete = null) => DoClampAnimation(true, false, onComplete);
    public void ClampLower(Action onComplete = null) => DoClampAnimation(false, true, onComplete);
    public void UnclampLower(Action onComplete = null) => DoClampAnimation(false, false, onComplete);

    public void Stop()
    {
        KillClampTween(ref _upperClampTween);
        KillClampTween(ref _lowerClampTween);
        _activeClampAnimations = 0;
        ReportBusyState();
    }

    // --- Внутренняя логика (1-в-1 из MachineController) ---

    private void DoClampAnimation(bool isUpper, bool clampTargetState, Action externalCallback)
    {
        Transform piston = isUpper ? _upperHydroPiston : _lowerHydroPiston;
        Transform leftZone = isUpper ? _upperLeftClampZone : _lowerLeftClampZone;
        Transform rightZone = isUpper ? _upperRightClampZone : _lowerRightClampZone;

        Vector3 initialPistonPos = isUpper ? _initialUpperPistonLocalPos : _initialLowerPistonLocalPos;
        Vector3 initialLeftZonePos = isUpper ? _initialUpperLeftZoneLocalPos : _initialLowerLeftZoneLocalPos;
        Vector3 initialRightZonePos = isUpper ? _initialUpperRightZoneLocalPos : _initialLowerRightZoneLocalPos;

        if (!piston || !leftZone || !rightZone) return;

        bool currentClampedState = isUpper ? _isUpperClamped : _isLowerClamped;
        
        // Если состояние уже верное и нет активной анимации
        if (currentClampedState == clampTargetState && (isUpper ? _upperClampTween == null : _lowerClampTween == null))
        {
            externalCallback?.Invoke();
            return; 
        }

        if (isUpper) KillClampTween(ref _upperClampTween); else KillClampTween(ref _lowerClampTween);

        if (_activeClampAnimations == 0)
        {
            EventManager.Instance?.RaiseEvent(EventType.ClampAnimationStarted, EventArgs.Empty);
        }
        _activeClampAnimations++;
        ReportBusyState();

        Sequence sequence = DOTween.Sequence();
        if (isUpper) _upperClampTween = sequence; else _lowerClampTween = sequence;

        float targetPistonLocalZVal = initialPistonPos.z + (clampTargetState ? (isUpper ? -_pistonVerticalDisplacement : _pistonVerticalDisplacement) : 0f);
        float targetLeftZoneLocalY = initialLeftZonePos.y + (clampTargetState ? -_zoneHorizontalDisplacement : 0f);
        float targetRightZoneLocalY = initialRightZonePos.y + (clampTargetState ? _zoneHorizontalDisplacement : 0f);

        sequence.Join(piston.DOLocalMoveZ(targetPistonLocalZVal, _clampDuration).SetEase(Ease.InOutSine));
        sequence.Join(leftZone.DOLocalMoveY(targetLeftZoneLocalY, _clampDuration).SetEase(Ease.InOutSine));
        sequence.Join(rightZone.DOLocalMoveY(targetRightZoneLocalY, _clampDuration).SetEase(Ease.InOutSine));

        sequence.OnComplete(() =>
        {
            if (isUpper) _isUpperClamped = clampTargetState; else _isLowerClamped = clampTargetState;
            
            RaiseClampEvent(isUpper, clampTargetState);
            
            if (isUpper && _upperClampTween == sequence) _upperClampTween = null;
            else if (!isUpper && _lowerClampTween == sequence) _lowerClampTween = null;

            _activeClampAnimations--;
            if (_activeClampAnimations < 0) _activeClampAnimations = 0;

            if (_activeClampAnimations == 0)
            {
                EventManager.Instance?.RaiseEvent(EventType.ClampAnimationFinished, EventArgs.Empty);
            }
            
            ReportBusyState();
            externalCallback?.Invoke();
        });

        sequence.OnKill(() =>
        {
            if (isUpper && _upperClampTween == sequence) _upperClampTween = null;
            else if (!isUpper && _lowerClampTween == sequence) _lowerClampTween = null;
            
            _activeClampAnimations--;
            if (_activeClampAnimations < 0) _activeClampAnimations = 0;
            
            ReportBusyState();
        });

        sequence.Play();
    }

    private void RaiseClampEvent(bool isUpper, bool wasClamped)
    {
        EventType eventType = isUpper 
            ? (wasClamped ? EventType.UpperGripClamped : EventType.UpperGripUnclamped) 
            : (wasClamped ? EventType.LowerGripClamped : EventType.LowerGripUnclamped);
        EventManager.Instance?.RaiseEvent(eventType, EventArgs.Empty);
    }

    private void KillClampTween(ref Tween tween)
    {
        if (tween != null && tween.IsActive()) tween.Kill(false);
        tween = null;
    }
    
    private void ReportBusyState()
    {
        OnBusyStateChanged?.Invoke(IsAnimating);
    }
}