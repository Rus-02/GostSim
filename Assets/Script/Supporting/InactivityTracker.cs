using UnityEngine;
using System;

public class InactivityTracker : MonoBehaviour
{
    [SerializeField] private float inactivityThreshold = 300f;
    [SerializeField] private EventType eventTypeToRaiseOnIdle = EventType.ViewInfoAction;

    private float _idleTime = 0f;

    private void OnEnable()
    {
        if (EventManager.Instance == null)
        {
            Debug.LogError("[InactivityTracker] EventManager не найден! Скрипт не будет работать.");
            enabled = false;
            return;
        }

        // Подписываемся на события активности
        EventManager.Instance.Subscribe(EventType.ButtonClicked, this, HandleUserActivity);
        EventManager.Instance.Subscribe(EventType.ClickedInteractableInfo, this, HandleUserActivity);
        EventManager.Instance.Subscribe(EventType.ShowInteractableInfo, this, HandleUserActivity);
        EventManager.Instance.Subscribe(EventType.CameraRotationStarted, this, HandleUserActivity);
    }

    private void OnDisable()
    {
        if (EventManager.Instance != null)
        {
            EventManager.Instance.Unsubscribe(EventType.ButtonClicked, this, HandleUserActivity);
            EventManager.Instance.Unsubscribe(EventType.ClickedInteractableInfo, this, HandleUserActivity);
            EventManager.Instance.Unsubscribe(EventType.ShowInteractableInfo, this, HandleUserActivity);
            EventManager.Instance.Unsubscribe(EventType.CameraRotationStarted, this, HandleUserActivity);
        }
    }

    // При любой активности пользователя просто сбрасываем таймер.
    private void HandleUserActivity(EventArgs args)
    {
        _idleTime = 0f;
    }

    void Update()
    {
        // Тупо считаем время.
        _idleTime += Time.deltaTime;

        // Если время вышло...
        if (_idleTime >= inactivityThreshold)
        {
            // ...тупо вызываем ивент.
            EventManager.Instance?.RaiseEvent(eventTypeToRaiseOnIdle, new EventArgs(this));
            
            // И тупо сбрасываем таймер, чтобы не спамить.
            _idleTime = 0f;
        }
    }
}
