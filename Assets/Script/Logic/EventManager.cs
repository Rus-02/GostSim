using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

// Базовый класс для аргументов событий
public class EventArgs // Оставляем как есть
{
    public object Sender { get; protected set; }
    public DateTime TimeStamp { get; protected set; }

    public EventArgs(object sender)
    {
        Sender = sender;
        TimeStamp = DateTime.Now;
    }

    public static readonly EventArgs Empty = new EventArgs(null);
}

// Пример аргументов для события ButtonClicked - Deprecated
public class ButtonClickedEventArgs : EventArgs
{
    public string ButtonId { get; private set; }

    public ButtonClickedEventArgs(object sender, string buttonId) : base(sender)
    {
        ButtonId = buttonId;
    }
}

public class ExtensometerToggleEventArgs : EventArgs { public bool IsEnabled { get; }
    public ExtensometerToggleEventArgs(object sender, bool isEnabled) : base(sender) { IsEnabled = isEnabled; } }

public class FixtureInstallationClearanceReadyEventArgs : EventArgs { public ActionRequester Requester { get; }
    public FixtureInstallationClearanceReadyEventArgs(object sender, ActionRequester requester) : base(sender) { Requester = requester; } }

public enum TestProgressState
{
    Idle,
    Running,
    Paused,
    Finished,
    Error
}

public enum InteractionType { None, Hover, Click, Selection, Tap }

// Класс ShowInteractableInfoEventArgs из твоего исходного кода, но с добавленным isNewTargetForPrompt
// Если у тебя этот класс определен в другом месте, убедись, что он обновлен, и удали это определение отсюда.
public class ShowInteractableInfoEventArgs : EventArgs
{
    public string TargetIdentifier { get; private set; }
    public GameObject TargetObject { get; private set; }
    public string SystemPromptKeyFromInteractable { get; private set; }
    public InteractionType InteractionType { get; private set; }
    public bool IsNewTargetForPrompt { get; private set; } // Добавлено для новой логики PromptController

    // Обновленный конструктор
    public ShowInteractableInfoEventArgs(object sender, string targetIdentifier, GameObject targetObject, string systemPromptKey, bool isNewTargetForPrompt = true) // Добавлен параметр
        : base(sender)
    {
        if (targetObject == null) // Эта проверка остается, InteractionDetector НЕ должен сюда передавать null
        {
            Debug.LogError("ShowInteractableInfoEventArgs: TargetObject не может быть null.");
        }
        TargetIdentifier = targetIdentifier ?? string.Empty; // Защита от null
        TargetObject = targetObject;
        SystemPromptKeyFromInteractable = systemPromptKey ?? string.Empty; // Защита от null
        InteractionType = InteractionType.Hover; // Для этого события тип всегда Hover
        IsNewTargetForPrompt = isNewTargetForPrompt; // Присвоение нового поля
    }
}

// Класс ClickedInteractableInfoEventArgs из твоего исходного кода, но с добавленным isNewTargetForPrompt
// Если у тебя этот класс определен в другом месте, убедись, что он обновлен, и удали это определение отсюда.
public class ClickedInteractableInfoEventArgs : EventArgs
{
    public string TargetIdentifier { get; private set; }
    public GameObject TargetObject { get; private set; }
    public string SystemPromptKeyFromInteractable { get; private set; }
    public InteractionType InteractionType { get; private set; }
    public bool IsNewTargetForPrompt { get; private set; } // Добавлено для новой логики PromptController

    // Обновленный конструктор
    public ClickedInteractableInfoEventArgs(object sender, string targetIdentifier, GameObject targetObject, string systemPromptKey, InteractionType interactionType, bool isNewTargetForPrompt = true) // Добавлен параметр
        : base(sender)
    {
        if (targetObject == null)
        {
            Debug.LogError("ClickedInteractableInfoEventArgs: TargetObject не может быть null.");
        }
        if (interactionType != InteractionType.Click && interactionType != InteractionType.Selection)
        {
            Debug.LogWarning($"ClickedInteractableInfoEventArgs создан с нерелевантным типом взаимодействия: {interactionType}. Ожидался Click или Selection.");
        }
        TargetIdentifier = targetIdentifier ?? string.Empty; // Защита от null
        TargetObject = targetObject;
        SystemPromptKeyFromInteractable = systemPromptKey ?? string.Empty; // Защита от null
        InteractionType = interactionType;
        IsNewTargetForPrompt = isNewTargetForPrompt; // Присвоение нового поля
    }
}

public class ScreenTapEventArgs : EventArgs
{
    public Vector2 ScreenPosition { get; private set; }
    public int TouchId { get; private set; } // Для информации, если пригодится

    public ScreenTapEventArgs(object sender, Vector2 screenPosition, int touchId) : base(sender)
    {
        ScreenPosition = screenPosition;
        TouchId = touchId;
    }
}

public class GlobalModeButtonsVisibilityEventArgs : EventArgs
{
    public bool ShowMenuButton { get; private set; } public bool ShowHomeButton { get; private set; }
    public GlobalModeButtonsVisibilityEventArgs(object sender, bool showMenu, bool showHome) : base(sender) { ShowMenuButton = showMenu; ShowHomeButton = showHome; }
}

public class HydraulicBufferActivationFailedEventArgs : EventArgs // Наследуемся от вашего EventArgs
{
    public string Reason { get; }

    public HydraulicBufferActivationFailedEventArgs(object sender, string reason) : base(sender) // Вызываем конструктор базового класса
    {
        Reason = reason;
    }
}

public class DoorStateChangedEventArgs : EventArgs { public bool IsOpen { get; private set; } public DoorStateChangedEventArgs(object sender, bool isOpen) : base(sender) { IsOpen = isOpen; } }

public class ContextMenuRequestedEventArgs : EventArgs { public List<string> Keys { get; } public Vector2 ScreenPosition { get; }
    public ContextMenuRequestedEventArgs(object sender, List<string> keys, Vector2 screenPosition) : base(sender) { Keys = keys ?? new List<string>(); ScreenPosition = screenPosition; } }


// Для события MachineForceLimitReached (Авария)
public class ForceLimitReachedEventArgs : EventArgs { public float LimitValue { get; } public float CurrentValue { get; }        
    public ForceLimitReachedEventArgs(object sender, float limit, float current) : base(sender) { LimitValue = limit; CurrentValue = current; } }

// Для события StopTestAction (Ручной стоп)
public class StopTestEventArgs : EventArgs { public string Reason { get; }
    public StopTestEventArgs(object sender, string reason = "User Request") : base(sender) { Reason = reason; } }

public class EventManager : MonoBehaviour
{
    public event Action OnEventRaised;

    private static EventManager _instance;
    private static bool isShuttingDown = false;
    public static EventManager Instance
    {
        get
        {
            if (isShuttingDown) return null; 
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<EventManager>();
                if (_instance == null)
                {
                    GameObject singletonObject = new GameObject("EventManager");
                    _instance = singletonObject.AddComponent<EventManager>();
                    Debug.Log("[EventManager] Instance created automatically.");
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        isShuttingDown = false;
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }
    private void OnDestroy() { isShuttingDown = true; }
    private void OnApplicationQuit() { isShuttingDown = true; }

    private Dictionary<EventType, List<SubscriberInfo>> _eventListeners = new Dictionary<EventType, List<SubscriberInfo>>(); // Убедись, что SubscriberInfo определен
    private List<KeyValuePair<EventType, EventArgs>> _eventHistory = new List<KeyValuePair<EventType, EventArgs>>();
    public IEnumerable<KeyValuePair<EventType, EventArgs>> EventHistory => _eventHistory;

    public void Subscribe(EventType eventType, object subscriber, Action<EventArgs> listener)
    {
        if (!_eventListeners.ContainsKey(eventType))
        {
            _eventListeners.Add(eventType, new List<SubscriberInfo>());
        }
        _eventListeners[eventType].Add(new SubscriberInfo(subscriber, listener));
    }

    public void Unsubscribe(EventType eventType, object subscriber, Action<EventArgs> listener)
    {
        if (_eventListeners.ContainsKey(eventType))
        {
            _eventListeners[eventType].RemoveAll(info => info.Subscriber == subscriber && info.Listener == listener);
        }
    }

    public void RaiseOnButtonClicked(object sender, string buttonId)
    {
        RaiseEvent(EventType.ButtonClicked, new ButtonClickedEventArgs(sender, buttonId));
    }

    public void RaiseEvent(EventType eventType, EventArgs eventArgs)
    {
        if (Application.isPlaying && ApplicationStateManager.Instance != null)
        {
            ApplicationStateManager.Instance.SetLastAction(eventType);
        }
        _eventHistory.Add(new KeyValuePair<EventType, EventArgs>(eventType, eventArgs));
        OnEventRaised?.Invoke();

        if (_eventListeners.ContainsKey(eventType))
        {
            List<SubscriberInfo> listenersCopy = new List<SubscriberInfo>(_eventListeners[eventType]);
            foreach (var listenerInfo in listenersCopy)
            {
                try
                {
                    listenerInfo.Listener?.Invoke(eventArgs);
                }
                catch (Exception e)
                {
                    string subscriberName = "Unknown Subscriber";
                    if (listenerInfo.Subscriber != null)
                    {
                        subscriberName = listenerInfo.Subscriber.GetType().Name;
                        if (listenerInfo.Subscriber is MonoBehaviour monoBehaviour)
                        {
                            subscriberName = $"{monoBehaviour.gameObject.name} ({subscriberName})";
                        }
                    }
                    Debug.LogError($"Error while invoking event {eventType} on {subscriberName}: {e.Message}\nStackTrace: {e.StackTrace}");
                }
            }
        }
    }

    public List<SubscriberInfo> GetEventListeners(EventType eventType)
    {
        return _eventListeners.ContainsKey(eventType) ? new List<SubscriberInfo>(_eventListeners[eventType]) : null;
    }
}