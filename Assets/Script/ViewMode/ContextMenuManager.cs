using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System;
using TMPro;

// --- Вспомогательные классы для настройки в Инспекторе ---

// Определяет тип действия, которое может выполнить кнопка
public enum ContextActionType
{
    RaiseEvent, // Вызвать системное событие
    OpenLink    // Открыть внешнюю ссылку
}

// Структура, описывающая одно возможное контекстное действие.
// Это "база данных" всех действий, которые можно вызвать через правый клик.
[System.Serializable]
public class ContextAction
{
    [Tooltip("Уникальный ключ действия. Должен совпадать с ключом в InteractableInfo на объекте.")]
    public string key;

    [Tooltip("Текст, который будет отображаться на кнопке.")]
    public string buttonText;

    [Tooltip("Тип действия: вызвать событие или открыть ссылку.")]
    public ContextActionType actionType;

    [Tooltip("Какое СУЩЕСТВУЮЩЕЕ событие вызвать, если actionType = RaiseEvent.")]
    public EventType eventToRaise; // Убедитесь, что у вас есть enum EventType в проекте

    [Tooltip("URL-адрес для перехода, если actionType = OpenLink.")]
    public string externalLink;
}

// Класс-помощник для удобной связи кнопки и ее иконки ссылки в инспекторе.
[System.Serializable]
public class MenuButtonUI
{
    public Button button;
    public Image linkIcon; 
}


// --- Основной класс Менеджера Контекстного Меню ---

public class ContextMenuManager : MonoBehaviour
{
    [Header("UI Элементы")]
    [Tooltip("Главная панель контекстного меню, которая будет показываться/скрываться.")]
    [SerializeField] private GameObject contextMenuPanel;

    [Tooltip("Список ВСЕХ кнопок в пуле. Скрипт будет переиспользовать их.")]
    [SerializeField] private List<MenuButtonUI> actionButtonsPool;

    [Header("Спрайты для Фона Кнопок")]
    [Tooltip("Спрайт для ВЕРХНЕЙ кнопки в списке (закругление сверху).")]
    [SerializeField] private Sprite topButtonSprite;

    [Tooltip("Спрайт для НИЖНЕЙ кнопки в списке (закругление снизу).")]
    [SerializeField] private Sprite bottomButtonSprite;

    [Tooltip("Спрайт для ЕДИНСТВЕННОЙ кнопки в списке (закругление со всех сторон).")]
    [SerializeField] private Sprite singleButtonSprite;


    [Header("База Данных и Настройки")]
    [Tooltip("Список всех возможных контекстных действий, доступных в приложении.")]
    [SerializeField] private List<ContextAction> availableActions;

    [Tooltip("Ключ для обязательного действия 'Сброс камеры'. Это действие будет добавлено в конец любого контекстного меню.")]
    [SerializeField] private string resetCameraActionKey = "global_reset_camera";

    // Словарь для быстрого поиска действий по ключу. Заполняется на старте.
    private Dictionary<string, ContextAction> actionsDatabase = new Dictionary<string, ContextAction>();
    private bool isMenuOpen = false;
    public bool IsOpen => isMenuOpen;
    private PlayerControls playerControls;

    [Header("UI Raycasting")]
    [Tooltip("Canvas, на котором находится меню. Нужен для GraphicRaycaster.")]
    [SerializeField] private Canvas canvas;

    private GraphicRaycaster graphicRaycaster;
    private PointerEventData pointerEventData;
    private EventSystem eventSystem;

    private void Awake()
    {
        // Создаем словарь из списка для быстрого доступа.
        // Это эффективнее, чем каждый раз перебирать список в поиске.
        foreach (var action in availableActions)
        {
            if (!string.IsNullOrEmpty(action.key) && !actionsDatabase.ContainsKey(action.key))
            {
                actionsDatabase.Add(action.key, action);
            }
        }
        if (canvas != null)
        {
            graphicRaycaster = canvas.GetComponent<GraphicRaycaster>();
        }
        else
        {
            Debug.LogError("[ContextMenuManager] Canvas не назначен! 'Умное' закрытие не будет работать.", this);
        }
        eventSystem = EventSystem.current;

        playerControls = new PlayerControls();
    }

    private void OnEnable()
    {
        // Подписываемся на события
        EventManager.Instance?.Subscribe(EventType.ContextMenuRequested, this, HandleContextMenuRequested);

        // Включаем прослушивание кликов
        playerControls.UI.Enable();
        playerControls.UI.Click.performed += OnLeftClickPerformed;
    }

    private void OnDisable()
    {
        // Отписываемся от событий, чтобы избежать утечек памяти
        if (EventManager.Instance != null)
        {
            EventManager.Instance.Unsubscribe(EventType.ContextMenuRequested, this, HandleContextMenuRequested);
        }

        // Отключаем прослушивание кликов
        playerControls.UI.Click.performed -= OnLeftClickPerformed;
        playerControls.UI.Disable();
    }

    private void Start()
    {
        // При запуске меню должно быть скрыто
        if (contextMenuPanel != null)
        {
            contextMenuPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Главный обработчик. Вызывается, когда InteractionDetector обнаружил правый клик.
    /// </summary>
    private void HandleContextMenuRequested(EventArgs args)
    {
        // Проверяем и объявляем eventArgs внутри if
        if (args is ContextMenuRequestedEventArgs eventArgs)
        {
            // 1. "Решаем", какие действия и с каким текстом показать
            List<ContextAction> actionsToShow = ResolveActions(eventArgs.Keys);

            // Добавляем обязательное действие сброса камеры в конец списка.
            if (actionsDatabase.TryGetValue(resetCameraActionKey, out ContextAction resetAction))
            {
                actionsToShow.Add(resetAction);
            }
            else
            {
                Debug.LogWarning($"[ContextMenuManager] Обязательное действие с ключом '{resetCameraActionKey}' не найдено в базе данных!");
            }

            ShowMenu(actionsToShow, eventArgs.ScreenPosition);
        }
    }


    /// <summary>
    /// Готовит и отображает панель контекстного меню.
    /// </summary>
    private void ShowMenu(List<ContextAction> actions, Vector2 screenPosition)
    {
        if (contextMenuPanel == null) return;
        contextMenuPanel.transform.position = screenPosition;

        // 1. Прячем все кнопки из пула перед настройкой
        foreach (var buttonUI in actionButtonsPool)
        {
            buttonUI.button.gameObject.SetActive(false);
        }

        // 2. Настраиваем и активируем нужное количество кнопок
        int buttonsToSetup = Mathf.Min(actions.Count, actionButtonsPool.Count);
        for (int i = 0; i < buttonsToSetup; i++)
        {
            ContextAction currentAction = actions[i];
            MenuButtonUI currentButtonUI = actionButtonsPool[i];

            // --- Настройка текста ---
            TextMeshProUGUI buttonText = currentButtonUI.button.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = currentAction.buttonText;
            }

            // --- Настройка иконки ссылки (надежная версия) ---
            if (currentButtonUI.linkIcon != null)
            {
                if (currentAction.actionType == ContextActionType.OpenLink)
                {
                    currentButtonUI.linkIcon.gameObject.SetActive(true);
                }
                else
                {
                    currentButtonUI.linkIcon.gameObject.SetActive(false);
                }
            }

            // --- Логика настройки фона кнопки (надежная версия) ---
            Image buttonImage = currentButtonUI.button.GetComponent<Image>();
            if (buttonImage != null)
            {
                // Сначала ВСЕГДА включаем компонент Image, чтобы им можно было управлять.
                buttonImage.enabled = true;

                if (buttonsToSetup == 1)
                {
                    buttonImage.sprite = singleButtonSprite;
                }
                else if (i == 0)
                {
                    buttonImage.sprite = topButtonSprite;
                }
                else if (i == buttonsToSetup - 1)
                {
                    buttonImage.sprite = bottomButtonSprite;
                }
                else
                {
                    // Для средних кнопок просто убираем спрайт.
                    // Компонент Image остается включенным и будет использовать свой цвет как фон.
                    buttonImage.sprite = null;
                }
            }

            // --- Назначение действия на клик ---
            currentButtonUI.button.onClick.RemoveAllListeners();
            ContextAction actionCopy = currentAction;

            currentButtonUI.button.onClick.AddListener(() =>
            {
                // Используем в лямбде именно копию
                ExecuteAction(actionCopy);
            });

            // --- Финальная активация ---
            currentButtonUI.button.gameObject.SetActive(true);
        }

        // 3. Показываем главную панель
        contextMenuPanel.SetActive(true);
        isMenuOpen = true;
    }

    /// <summary>
    /// Выполняет действие, привязанное к кнопке.
    /// </summary>
    private void ExecuteAction(ContextAction action)
    {
        // В зависимости от типа действия, выполняем нужную логику
        if (action.actionType == ContextActionType.OpenLink)
        {
            if (!string.IsNullOrEmpty(action.externalLink))
            {
                Application.OpenURL(action.externalLink);
            }
        }
        else if (action.actionType == ContextActionType.RaiseEvent)
        {
            EventManager.Instance?.RaiseEvent(action.eventToRaise, new EventArgs(this));
        }

        // После выполнения любого действия меню нужно скрыть
        HideMenu();
    }

    /// <summary>
    /// Скрывает панель контекстного меню.
    /// </summary>
    public void HideMenu()
    {
        if (!isMenuOpen) return;

        if (contextMenuPanel != null)
        {
            contextMenuPanel.SetActive(false);
        }
        isMenuOpen = false;
    }
    
    /// Обрабатывает левый клик, чтобы закрыть меню, если клик был ВНЕ его.
    /// </summary>
    private void OnLeftClickPerformed(InputAction.CallbackContext context)
    {
        // 1. Если меню и так закрыто, ничего не делаем.
        if (!isMenuOpen) return;

        // 2. Готовим данные для рейкаста в UI
        pointerEventData = new PointerEventData(eventSystem);
        pointerEventData.position = playerControls.UI.Point.ReadValue<Vector2>();

        // 3. Создаем список для хранения результатов
        List<RaycastResult> results = new List<RaycastResult>();

        // 4. Делаем рейкаст
        graphicRaycaster.Raycast(pointerEventData, results);

        // 5. Проверяем, попал ли клик в наше меню
        bool clickWasInsideMenu = false;
        foreach (RaycastResult result in results)
        {
            // IsChildOf проверяет, является ли объект, по которому кликнули,
            // дочерним для нашей панели меню.
            if (result.gameObject.transform.IsChildOf(contextMenuPanel.transform))
            {
                clickWasInsideMenu = true;
                break; // Нашли совпадение, дальше можно не проверять
            }
        }

        // 6. Если клик был НЕ внутри меню, закрываем его
        if (!clickWasInsideMenu)
        {
            HideMenu();
        }
    }

    private List<ContextAction> ResolveActions(List<string> rawKeys)
    {
        var resolvedActions = new List<ContextAction>();

        foreach (var key in rawKeys)
        {
            // 1. Сначала находим базовое действие в нашей базе данных
            if (actionsDatabase.TryGetValue(key, out ContextAction originalAction))
            {
                // 2. Создаем КОПИЮ, чтобы не изменять оригинальный ассет
                var actionToShow = new ContextAction
                {
                    key = originalAction.key,
                    buttonText = originalAction.buttonText, // Берем текст по умолчанию
                    actionType = originalAction.actionType,
                    eventToRaise = originalAction.eventToRaise,
                    externalLink = originalAction.externalLink
                };

                // --- ДИНАМИЧЕСКИЙ ТЕКСТ ---
                
                if (key == "door_toggle")
                {
                    // Спрашиваем у Монитора
                    bool areDoorsClosed = SystemStateMonitor.Instance.AreDoorsClosed;
                    // Меняем текст у НАШЕЙ КОПИИ
                    actionToShow.buttonText = areDoorsClosed ? "Открыть дверь" : "Закрыть дверь";
                }
                else if (key == "tooltip_toggle")
                {
                    // Точно такая же логика
                    bool isPromptCollapsed = SystemStateMonitor.Instance.IsPromptPanelCollapsed;
                    actionToShow.buttonText = isPromptCollapsed ? "Развернуть подсказку" : "Свернуть подсказку";
                }                

                // 3. Добавляем в список уже настроенную, кастомную версию действия
                resolvedActions.Add(actionToShow);
            }
            else
            {
                Debug.LogWarning($"[ContextMenuManager] Действие с ключом '{key}' не найдено в базе!");
            }
        }

        return resolvedActions;
    }
}