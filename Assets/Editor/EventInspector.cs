using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class EventInspector : EditorWindow
{
    private static EventInspector _windowInstance;
    private Vector2 _scrollPosition;

    [MenuItem("Window/Event Inspector")]
    public static void ShowWindow()
    {
        _windowInstance = GetWindow<EventInspector>("Event Inspector");
    }

    private void OnEnable()
    {
        EditorApplication.update += EditorUpdate; // Используем EditorApplication.update
        SubscribeToEventManager();
    }

    private void OnDisable()
    {
        EditorApplication.update -= EditorUpdate;
        UnsubscribeFromEventManager();
    }

    private void EditorUpdate()
    {
        if (EventManager.Instance != null)
        {
            Repaint(); // Перерисовываем окно каждый кадр в редакторе
        }
    }

    private void SubscribeToEventManager()
    {
        if (EventManager.Instance != null)
        {
            EventManager.Instance.OnEventRaised -= OnEventRaisedHandler; // Безопасная отписка
            EventManager.Instance.OnEventRaised += OnEventRaisedHandler;
        }
    }

    private void UnsubscribeFromEventManager()
    {
        if (EventManager.Instance != null)
        {
            EventManager.Instance.OnEventRaised -= OnEventRaisedHandler;
        }
    }

    private void OnEventRaisedHandler()
    {
        Repaint(); // Перерисовываем окно при получении события
    }

    private void OnGUI()
    {
        GUILayout.Label("Event Inspector", EditorStyles.boldLabel);

        if (EventManager.Instance == null)
        {
            GUILayout.Label("Waiting for EventManager...", EditorStyles.miniLabel);
            return;
        }

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        // Отображение истории событий
        GUILayout.Label("Event History", EditorStyles.boldLabel);
        foreach (var eventPair in EventManager.Instance.EventHistory.OrderByDescending(e => e.Value.TimeStamp))
        {
            DisplayEventInfo(eventPair.Key, eventPair.Value);
        }

        GUILayout.Space(10);

        // Отображение информации о подписчиках для каждого типа событий
        GUILayout.Label("Subscribers", EditorStyles.boldLabel);
        foreach (EventType eventType in System.Enum.GetValues(typeof(EventType)))
        {
            DisplaySubscribersForEvent(eventType);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DisplayEventInfo(EventType eventType, EventArgs eventArgs)
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label($"{eventArgs.TimeStamp:HH:mm:ss} {eventType} ({eventArgs.Sender?.GetType().Name})");
        GUILayout.Label($"    Sender: {eventArgs.Sender}");

        // Отображение параметров события (нужно будет расширить для разных типов EventArgs)
        if (eventArgs is ButtonClickedEventArgs buttonArgs)
        {
            GUILayout.Label($"    Button ID: {buttonArgs.ButtonId}");
        }
        GUILayout.EndVertical();
        EditorGUILayout.Space();
    }

    private void DisplaySubscribersForEvent(EventType eventType)
    {
        GUILayout.Label($"{eventType}:", EditorStyles.boldLabel);

        if (EventManager.Instance != null)
        {
            var subscribers = EventManager.Instance.GetEventListeners(eventType);
            GUILayout.Label($"Count: {subscribers?.Count ?? 0}");

            if (subscribers != null && subscribers.Any())
            {
                foreach (var subInfo in subscribers)
                {
                    DisplaySubscriberInfo(subInfo);
                }
            }
            else
            {
                GUILayout.Label($"No subscribers for {eventType}.");
            }
        }
        else
        {
            GUILayout.Label("EventManager not found.");
        }
        EditorGUILayout.Space();
    }

    private void DisplaySubscriberInfo(SubscriberInfo subInfo)
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label($"Subscriber: {subInfo.Subscriber}");
        GUILayout.Label($"Listener: {subInfo.Listener.Method.Name}");
        GUILayout.EndVertical();
    }
}