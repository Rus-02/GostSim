using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;

public class ButtonScript : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    // --- Поля и Awake() остаются без изменений ---

    [Header("Button Identification & Action")]
    public string buttonId;
    public EventType eventTypeToRaise;

    private Button button;
    private Text buttonTextComponent;
    private TextMeshProUGUI buttonTMPComponent;
    private Image buttonImageComponent;
    private bool isCurrentlyPauseButton = true;

    [System.Serializable]
    public struct VisualStateMapping
    {
        public ButtonVisualStateType state;
        public Sprite sprite;
    }

    [Header("Visual States Configuration")]
    [SerializeField] private List<VisualStateMapping> visualMappings = new List<VisualStateMapping>();

    private void Awake()
    {
        button = GetComponent<Button>();
        if (button == null) { Debug.LogError($"Button component not found on {gameObject.name}", this); return; }

        if (string.IsNullOrEmpty(buttonId))
        {
            Debug.LogWarning($"ButtonId не назначен для кнопки: {gameObject.name}. Управление этой кнопкой извне будет невозможно.", this);
        }
        buttonTextComponent = GetComponentInChildren<Text>(true);
        buttonTMPComponent = GetComponentInChildren<TextMeshProUGUI>(true);
        buttonImageComponent = GetComponent<Image>();

        if (buttonImageComponent == null && visualMappings.Count > 0)
        {
            Debug.LogWarning($"Button '{buttonId}' has visual mappings defined, but no Image component found to apply sprites!", this);
        }

        button.onClick.AddListener(OnButtonClick);

        // Подписываемся на команду обновления визуальных элементов (и теперь функционала)
        if (ToDoManager.Instance != null && !string.IsNullOrEmpty(buttonId))
        {
            ToDoManager.Instance.SubscribeToAction(ActionType.UpdateUIButtonVisuals, HandleUpdateVisualsCommand);
        }
    }

    private void OnDestroy()
    {
        button.onClick.RemoveListener(OnButtonClick);

        // Отписываемся от команды при уничтожении объекта, чтобы избежать ошибок
        if (ToDoManager.Instance != null && !string.IsNullOrEmpty(buttonId))
        {
            ToDoManager.Instance.UnsubscribeFromAction(ActionType.UpdateUIButtonVisuals, HandleUpdateVisualsCommand);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventTypeToRaise == EventType.FastlyUpAction)
        {
            EventManager.Instance?.RaiseEvent(EventType.FastlyUpAction, new EventArgs(this));
        }
        else if (eventTypeToRaise == EventType.FastlyDownAction)
        {
            EventManager.Instance?.RaiseEvent(EventType.FastlyDownAction, new EventArgs(this));
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventTypeToRaise == EventType.FastlyUpAction)
        {
            EventManager.Instance?.RaiseEvent(EventType.FastlyUpReleased, new EventArgs(this));
        }
        else if (eventTypeToRaise == EventType.FastlyDownAction)
        {
            EventManager.Instance?.RaiseEvent(EventType.FastlyDownReleased, new EventArgs(this));
        }
    }

    public void OnButtonClick()
    {
        // "Быстрые" действия (FastlyUp/Down) обычно идут мимо перехватчика, 
        // так как они работают по PointerDown/Up, а не Click.
        // Но если они есть в OnButtonClick (как защита) - оставим проверку.
        if (eventTypeToRaise == EventType.FastlyUpAction || eventTypeToRaise == EventType.FastlyDownAction)
        {
            return;
        }

        // Если Интерцептора нет (например, старая сцена), работаем по-старому
        if (InputInterceptor.Instance == null)
        {
            ExecuteEventInternal();
            return;
        }

        // Идем к Шефу
        InputInterceptor.Instance.ProcessClick(this);
    }

    // Выполняет фактическую отправку события. Вызывается либо Интерцептором (если разрешено), либо напрямую (если Интерцептора нет).
    public void ExecuteEventInternal()
    {
        if (EventManager.Instance == null) { Debug.LogError($"EventManager null! Button: {gameObject.name}"); return; }

        if (!string.IsNullOrEmpty(buttonId))
        {
            EventManager.Instance.RaiseEvent(EventType.ButtonClicked, new ButtonClickedEventArgs(this, buttonId));
        }

        if (eventTypeToRaise == EventType.PauseTestAction)
        {
            if (isCurrentlyPauseButton)
            {
                EventManager.Instance.RaiseEvent(EventType.PauseTestAction, new EventArgs(this));
            }
            else
            {
                EventManager.Instance.RaiseEvent(EventType.ResumeTestRequested, new EventArgs(this));
            }
        }
        else if (eventTypeToRaise != default(EventType))
        {
            EventManager.Instance.RaiseEvent(eventTypeToRaise, new EventArgs(this));
        }
    }

    /// Обрабатывает команду от ToDoManager на обновление визуального и функционального состояния кнопки.
    private void HandleUpdateVisualsCommand(BaseActionArgs args)
    {
        var commandArgs = args as UpdateUIButtonVisualsArgs;
        if (commandArgs == null || this.buttonId != commandArgs.ButtonId) return;

        // Меняем текст, ТОЛЬКО если он был передан
        if (!string.IsNullOrEmpty(commandArgs.ButtonText))
        {
            SetButtonText(commandArgs.ButtonText);
        }

        // Меняем визуал, ТОЛЬКО если он был передан
        if (commandArgs.VisualState.HasValue)
        {
            SetVisualState(commandArgs.VisualState.Value);
        }

        // Меняем событие, ТОЛЬКО если оно было передано
        if (commandArgs.NewEventType.HasValue)
        {
            this.eventTypeToRaise = commandArgs.NewEventType.Value;
        }
    }

    public void SetButtonText(string newText)
    {
        if (buttonTMPComponent != null)
        {
            buttonTMPComponent.text = newText;
        }
        else if (buttonTextComponent != null)
        {
            buttonTextComponent.text = newText;
        }
        else
        {
            Debug.LogWarning($"Не найден компонент Text или TextMeshProUGUI на кнопке '{buttonId}' ({gameObject.name}) для установки текста.", this);
        }
    }

    public void SetButtonImage(Sprite newImage)
    {
        if (buttonImageComponent != null)
        {
            buttonImageComponent.sprite = newImage;
        }
    }

    public void SetButtonContent(string text = null, Sprite image = null)
    {
        if (!string.IsNullOrEmpty(text))
        {
            SetButtonText(text);
        }
        if (image != null)
        {
            SetButtonImage(image);
        }
    }

    public void SetPauseResumeMode(bool isPauseMode)
    {
        isCurrentlyPauseButton = isPauseMode;
    }

    public string GetButtonId()
    {
        return buttonId;
    }

    public void SetVisualState(ButtonVisualStateType newState)
    {
        if (buttonImageComponent == null || visualMappings.Count == 0)
        {
            return;
        }

        Sprite targetSprite = null;
        bool stateFound = false;
        foreach (var mapping in visualMappings)
        {
            if (mapping.state == newState)
            {
                targetSprite = mapping.sprite;
                stateFound = true;
                break;
            }
        }

        if (stateFound && targetSprite != null)
        {
            SetButtonImage(targetSprite);
        }
        else if (stateFound && targetSprite == null)
        {
            Debug.LogWarning($"Button '{buttonId}': Visual mapping for state '{newState}' exists, but sprite is not assigned in Inspector.", this);
        }
        else
        {
            Debug.LogWarning($"Button '{buttonId}': No visual mapping found for state '{newState}'. Sprite not changed.", this);
        }
    }  

    public void SetEventType(EventType newType) { this.eventTypeToRaise = newType; }  
}