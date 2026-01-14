using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System;

#region Data Structures

// Данные для отдельной кнопки в UI подсказки
[System.Serializable]
public class ButtonData
{
    public string buttonText = "Кнопка"; // Текст на кнопке
    public string link = "";             // Ссылка, на которую переходим при нажатии
}

// Данные для СИСТЕМНЫХ сообщений
[System.Serializable]
public class PromptData
{
    public string key; // Ключ для поиска данных
    [TextArea(3, 10)]
    public string text; // Текст для РАЗВЕРНУТОГО состояния (Primary)
    public string secondaryText; // Текст для СВЕРНУТОГО состояния (Collapsed)
    public List<ButtonData> buttons; // Список данных для кнопок (в развернутом виде)

    // Конструктор для создания "пустого" PromptData, если ключ не найден
    public PromptData(string missingKey)
    {
        key = missingKey;
        text = $"Системное сообщение с ключом '{missingKey}' не найдено.";
        secondaryText = $"Ключ '{missingKey}' не найден";
        buttons = new List<ButtonData>();
    }
}

#endregion

/// <summary>
/// Управляет UI-панелью для отображения динамических подсказок. 
/// Получает данные из разных источников, обрабатывает состояния (свернуто/развернуто) и анимации.
/// </summary>
public class PromptController : MonoBehaviour
{
    public static PromptController Instance { get; private set; }

    #region Serialized Fields & UI References

    [Header("UI Элементы")]
    [Tooltip("Основной текстовый компонент для отображения информации.")]
    [SerializeField] private Text textComponent;
    [Tooltip("Текстовый компонент для отображения в свернутом состоянии (опционально). Если не задан, будет использоваться textComponent.")]
    [SerializeField] private Text collapsedTextComponent;
    [Tooltip("Контейнер, в котором находятся кнопки.")]
    [SerializeField] private GameObject buttonContainer;
    [Tooltip("Массив кнопок в контейнере.")]
    [SerializeField] private Button[] buttons;

    [Header("Управление Сворачиванием")]
    [Tooltip("Главная кнопка для сворачивания/разворачивания панели.")]
    [SerializeField] private Button hideButton;
    [Tooltip("Спрайты для кнопки hideButton (0: развернуто, 1: свернуто).")]
    [SerializeField] private Sprite[] hideButtonImages;
    [Tooltip("Прозрачная кнопка для разворачивания, активна во время анимации фона.")]
    public Button expandButton;

    [Header("Рамка и Размеры")]
    [Tooltip("Объект Image, представляющий рамку панели.")]
    [SerializeField] private Image frameObject;
    [Tooltip("Размеры панели в свернутом состоянии.")]
    [SerializeField] private Vector2 collapsedSize = new Vector2(720, 50);
    [Tooltip("Размеры панели в развернутом состоянии.")]
    [SerializeField] private Vector2 expandedSize = new Vector2(720, 450);
    [Tooltip("Отступ рамки от краев панели.")]
    [SerializeField] private float framePadding = 4f;

    [Header("Анимации")]
    [Tooltip("Скорость анимации изменения размера панели.")]
    [SerializeField] private float resizeSpeed = 10f;
    [Tooltip("Множитель масштаба для анимации 'подпрыгивания' кнопки сворачивания.")]
    [SerializeField] private Vector2 bounceScaleFactor = new Vector2(1.2f, 1.2f);
    [SerializeField] private float bounceDurationUp = 0.12f;
    [SerializeField] private float bounceDurationDown = 0.15f;
    [SerializeField] private int bounceCount = 2;

    [Header("Анимация Уведомления")]
    [Tooltip("Объект Image, который будет анимироваться как фон-уведомление.")]
    [SerializeField] private Image imageObject;
    [Tooltip("На сколько пикселей увеличить высоту imageObject при анимации.")]
    [SerializeField] private float notificationHeightIncrease = 50f;
    [Tooltip("Длительность фазы увеличения/уменьшения фона.")]
    [SerializeField] private float notificationAnimDuration = 0.5f;
    [Tooltip("Длительность паузы в увеличенном состоянии фона.")]
    [SerializeField] private float notificationHoldDuration = 3.0f;


    [Header("Источники Данных")]
    [Tooltip("Список стандартных системных сообщений (PromptData), доступных по ключу.")]
    public List<PromptData> systemPromptDataList;
    
    #endregion

    #region Private State
    
    private Dictionary<string, PromptData> systemPrompts;
    private RectTransform rectTransform;
    private RectTransform frameRectTransform;

    private bool isCollapsed = true;

    private Vector3 originalHideButtonScale = Vector3.one;
    
    // Храним текущий активный тип источника и его приоритет (на данный момент не используется в логике, но может пригодиться)
    // private PromptSourceType activeSourceType = PromptSourceType.None;
    // private int activeSourcePriority = -1; 

    // Флаги состояния
    private string currentKeyOrIdentifier = null; // Ключ или ID текущего отображаемого промпта
    private string currentMainText = "";
    private string currentCollapsedText = "";
    private List<ButtonData> currentButtons = new List<ButtonData>();

    private Coroutine resizeCoroutine = null;
    private Coroutine notificationCoroutine = null;
    private string _activeKeyOrIdInNotificationCoroutine = null; // Ключ/ID, для которого идет анимация фона
    private bool isAnimatingNotificationBackground = false;

    private Vector2 currentSize;
    private Vector2 targetSize;

    // 1. Словарь для кэширования объектов с InteractableInfo
    private Dictionary<string, InteractableInfo> _interactableCache = new Dictionary<string, InteractableInfo>();

    #endregion

    #region Unity Lifecycle Methods

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null) Debug.LogError("[PromptController] RectTransform не найден на этом объекте!", this);

        if (frameObject != null)
        {
            frameRectTransform = frameObject.GetComponent<RectTransform>();
            if (frameRectTransform == null) Debug.LogError("[PromptController] Frame object не имеет RectTransform!", frameObject);
        }
        else Debug.LogWarning("[PromptController] Frame object не назначен. Рамка не будет изменяться.");

        // Валидация обязательных полей
        if (textComponent == null) Debug.LogError("[PromptController] Text Component не назначен!", this);
        if (collapsedTextComponent == null)
        {
             Debug.LogWarning("[PromptController] Collapsed Text Component не назначен. Будет использоваться основной textComponent.");
             collapsedTextComponent = textComponent;
        }
        if (buttonContainer == null) Debug.LogError("[PromptController] Button Container не назначен!", this);
        if (buttons == null || buttons.Length == 0) Debug.LogWarning("[PromptController] Массив Buttons не назначен или пуст.", this);
        if (hideButton == null) Debug.LogError("[PromptController] Hide Button не назначен!", this);
        if (hideButtonImages == null || hideButtonImages.Length < 2) Debug.LogWarning("[PromptController] Массив Hide Button Images не назначен или содержит менее 2 спрайтов.", this);
        if (imageObject == null) Debug.LogWarning("[PromptController] Image Object для анимации фона не назначен.", this);
        if (expandButton == null) Debug.LogWarning("[PromptController] Expand Button не назначен.", this);

        InitializeSystemPrompts();

        if (hideButton != null) originalHideButtonScale = hideButton.transform.localScale;
        currentSize = isCollapsed ? collapsedSize : expandedSize;
        targetSize = currentSize;
        if (rectTransform != null) rectTransform.sizeDelta = currentSize;
        UpdateFrameSize(currentSize);
        ResetNotificationBackgroundSize();
        _activeKeyOrIdInNotificationCoroutine = null;
    }

    private void Start()
    {
        SubscribeToActions();
        SubscribeToExternalEvents();

        ApplyInitialCollapsedState(isCollapsed);
        if (expandButton != null) expandButton.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        UnsubscribeFromActions();
        UnsubscribeFromExternalEvents();

        // Отписка от событий кнопок, чтобы избежать утечек
        if (hideButton != null) hideButton.onClick.RemoveAllListeners();
        if (expandButton != null) expandButton.onClick.RemoveAllListeners();
        if(buttons != null)
        {
            foreach (Button button in buttons)
            {
                if (button != null) button.onClick.RemoveAllListeners();
            }
        }

        // Остановка всех корутин
        if (resizeCoroutine != null) StopCoroutine(resizeCoroutine);
        if (notificationCoroutine != null)
        {
            StopCoroutine(notificationCoroutine);
            notificationCoroutine = null;
            isAnimatingNotificationBackground = false;
        }
        _activeKeyOrIdInNotificationCoroutine = null;
        StopCoroutine("BounceHideButton");
    }
    
    #endregion

    #region Initialization and Subscriptions
    
    private void InitializeSystemPrompts()
    {
        systemPrompts = new Dictionary<string, PromptData>();
        if (systemPromptDataList == null)
        {
            Debug.LogWarning("[PromptController] System Prompt Data List не назначен.");
            systemPromptDataList = new List<PromptData>();
        }

        foreach (var data in systemPromptDataList)
        {
            if (data != null && !string.IsNullOrEmpty(data.key))
            {
                if (!systemPrompts.ContainsKey(data.key))
                {
                    systemPrompts[data.key] = data;
                }
                else
                {
                    Debug.LogError($"[PromptController] Обнаружен дубликат системного ключа: '{data.key}'. Будет использован первый найденный.", this);
                }
            }
            else
            {
                Debug.LogWarning("[PromptController] Найдены невалидные данные (null или пустой key) в systemPromptDataList.", this);
            }
        }
    }

    private void SubscribeToActions()
    {
        if (ToDoManager.Instance == null)
        {
            Debug.LogError("[PromptController] ToDoManager не найден! Не удалось подписаться на действия.", this);
            return;
        }
        ToDoManager.Instance.SubscribeToAction(ActionType.UpdatePromptDisplay, HandleUpdatePromptActionCommand);
    }

    private void UnsubscribeFromActions()
    {
        if (ToDoManager.Instance != null)
        {
            ToDoManager.Instance.UnsubscribeFromAction(ActionType.UpdatePromptDisplay, HandleUpdatePromptActionCommand);
        }
    }

    private void SubscribeToExternalEvents()
    {
        if (EventManager.Instance == null) return;
        EventManager.Instance.Subscribe(EventType.PromptInteractionBlockStarted, this, HandlePromptInteractionBlockStarted);
        EventManager.Instance.Subscribe(EventType.PromptInteractionBlockFinished, this, HandlePromptInteractionBlockFinished);
        EventManager.Instance.Subscribe(EventType.TogglePromptPanel, this, HandleToggleRequest);
    }

    private void UnsubscribeFromExternalEvents()
    {
        if (EventManager.Instance != null)
        {
            EventManager.Instance.Unsubscribe(EventType.PromptInteractionBlockStarted, this, HandlePromptInteractionBlockStarted);
            EventManager.Instance.Unsubscribe(EventType.PromptInteractionBlockFinished, this, HandlePromptInteractionBlockFinished);
            EventManager.Instance.Unsubscribe(EventType.TogglePromptPanel, this, HandleToggleRequest);
        }
    }
    
    #endregion

    #region Event Handlers
    
    // Обработчик события начала блокировки взаимодействий (т.е. начала анимации фона)
    private void HandlePromptInteractionBlockStarted(EventArgs args)
    {
        // Активируем прозрачную кнопку разворачивания
        if (expandButton != null)
        {
            expandButton.gameObject.SetActive(true);
            // Назначаем ей слушателя, который развернет панель при клике
            expandButton.onClick.RemoveAllListeners();
            expandButton.onClick.AddListener(ToggleCollapseState);
        }
    }

    // Обработчик события конца блокировки взаимодействий (т.е. конца анимации фона)
    private void HandlePromptInteractionBlockFinished(EventArgs args)
    {
        // Деактивируем прозрачную кнопку разворачивания
        if (expandButton != null)
        {
            expandButton.onClick.RemoveAllListeners(); // Удаляем слушателя
            expandButton.gameObject.SetActive(false);
        }
    }
    
    // Внешний обработчик команды от ToDoManager
    private void HandleUpdatePromptActionCommand(BaseActionArgs baseArgs)
    {
        if (!(baseArgs is UpdatePromptArgs args))
        {
            Debug.LogWarning("[PromptController] Получены некорректные аргументы для UpdatePromptDisplay от ToDoManager.", this);
            return;
        }
        // Вызываем основной метод обработки с дополнительными параметрами из args
        ProcessPromptUpdate(args.TargetKeyOrIdentifier, args.SourceType, args.IsNewTargetForPrompt, args.SourceSenderInfo);
    }
    
    private void HandleToggleRequest(EventArgs args)
    {
        ToggleCollapseState();
    }
    
    #endregion

    #region Core Logic

    // Вспомогательный метод для получения приоритета (в данный момент не используется в логике блокировок)
    // private int GetSourcePriority(PromptSourceType sourceType) { ... }

    // Основной метод обработки и обновления промпта
    private void ProcessPromptUpdate(string requestedKeyOrId, PromptSourceType requestedSourceType, bool isNewTargetForPromptEvent, string sourceInfo)
    {
        if (string.IsNullOrEmpty(requestedKeyOrId))
        {
            Debug.LogError($"[PromptController] ProcessPromptUpdate вызван с пустым requestedKeyOrId от {requestedSourceType} ({sourceInfo}). Игнорируется.");
            return;
        }

        // --- Логика блокировок и приоритетов ---

        // 1. Блокировка из-за активного Dropdown UI
        if (SystemStateMonitor.Instance.IsDropdownMenuActive)
        {
            if (requestedSourceType == PromptSourceType.HoverInteraction || requestedSourceType == PromptSourceType.ClickInteraction)
            {
                Debug.Log($"[PromptController] Dropdown UI активен. Запрос от {requestedSourceType} для '{requestedKeyOrId}' ({sourceInfo}) проигнорирован.");
                return;
            }
        }

        // 2. Блокировка из-за анимации фона
        if (isAnimatingNotificationBackground)
        {
            if (requestedSourceType == PromptSourceType.HoverInteraction)
            {
                //Debug.Log($"[PromptController] Анимация фона ({_activeKeyOrIdInNotificationCoroutine}) активна. Запрос от {requestedSourceType} для '{requestedKeyOrId}' ({sourceInfo}) проигнорирован.");
                return;
            }
        }

        // --- Загрузка данных и обновление ---
        bool dataLoaded = LoadDisplayData(requestedKeyOrId, requestedSourceType);

        if (dataLoaded)
        {
            string previousKeyOrId = currentKeyOrIdentifier;
            currentKeyOrIdentifier = requestedKeyOrId;
            SystemStateMonitor.Instance?.ReportPromptPanelState(isCollapsed, currentKeyOrIdentifier);
            // activeSourceType = requestedSourceType; // Сохранение источника, если потребуется
            // activeSourcePriority = GetSourcePriority(requestedSourceType); // Сохранение приоритета

            UpdateDisplay(); // Обновляем тексты и кнопки

            bool shouldAnimateBackground = (isNewTargetForPromptEvent || requestedSourceType == PromptSourceType.ClickInteraction);

            // Запускаем анимацию фона ТОЛЬКО если это "новое" событие И панель СВЕРНУТА.
            if (shouldAnimateBackground && isCollapsed)
            {
                if (notificationCoroutine != null) StopCoroutine(notificationCoroutine);

                ResetNotificationBackgroundSize();
                _activeKeyOrIdInNotificationCoroutine = currentKeyOrIdentifier; // Запоминаем, для чего запускаем анимацию
                notificationCoroutine = StartCoroutine(AnimateNotificationBackground());
            }

            // Анимация кнопки сворачивания, если панель свернута и информация обновилась
            if (isCollapsed && (previousKeyOrId != currentKeyOrIdentifier || isNewTargetForPromptEvent))
            {
                StartCoroutine(BounceHideButton());
            }
        }
        else
        {
            Debug.LogWarning($"[PromptController] Данные для ключа/ID '{requestedKeyOrId}' от {requestedSourceType} ({sourceInfo}) не загружены. Промпт не изменен.");
        }
    }

    private bool LoadDisplayData(string keyOrID, PromptSourceType sourceType)
    {
        string mainTxt = "";
        string collapsedTxt = "";
        List<ButtonData> btnData = new List<ButtonData>();
        bool success = false;
        PromptData loadedPromptData = null;

        if (sourceType == PromptSourceType.SystemAction)
        {
            if (!systemPrompts.TryGetValue(keyOrID, out loadedPromptData))
            {
                Debug.LogWarning($"[PromptController] Системный промпт с ключом '{keyOrID}' не найден. Будет отображено сообщение об ошибке.");
                loadedPromptData = new PromptData(keyOrID); // Создаем временный PromptData с сообщением об ошибке
            }
        }
        else
        {
            InteractableInfo info = FindInteractableInfoById(keyOrID);
            if (info != null)
            {
                if (!string.IsNullOrEmpty(info.SystemPromptKey)) // Если у объекта есть ссылка на системный ключ
                {
                    if (!systemPrompts.TryGetValue(info.SystemPromptKey, out loadedPromptData))
                    {
                        Debug.LogWarning($"[PromptController] Объект '{keyOrID}' ссылается на системный ключ '{info.SystemPromptKey}', который не найден. Будет отображено сообщение об ошибке.");
                        loadedPromptData = new PromptData(info.SystemPromptKey);
                    }
                }
                else // Используем собственные данные объекта
                {
                    mainTxt = info.detailedDescription ?? "";
                    collapsedTxt = info.shortDescription ?? "";
                    btnData = info.buttonDataList ?? new List<ButtonData>();
                    success = true; 
                }
            }
            else
            {
                Debug.LogWarning($"[PromptController] InteractableInfo с ID '{keyOrID}' не найден. Промпт не может быть загружен для этого ID.");
                return false; // Данные не загружены
            }
        }

        // Если загружали через PromptData, извлекаем из него данные
        if (loadedPromptData != null)
        {
            mainTxt = loadedPromptData.text ?? "";
            collapsedTxt = loadedPromptData.secondaryText ?? "";
            btnData = loadedPromptData.buttons ?? new List<ButtonData>();
            success = true;
        }

        if (success)
        {
            currentMainText = mainTxt;
            currentCollapsedText = collapsedTxt;
            currentButtons = new List<ButtonData>(btnData);
        }
        return success;
    }
    
    // 2. Метод для регистрации всех интерактивов новой машины
    public void RegisterMachineInteractables(GameObject machineRoot)
    {
        _interactableCache.Clear();
        
        if (machineRoot == null) return;

        // Ищем вообще все InteractableInfo, даже на выключенных объектах
        var allInteractables = machineRoot.GetComponentsInChildren<InteractableInfo>(true);

        foreach (var info in allInteractables)
        {
            if (string.IsNullOrEmpty(info.Identifier)) continue;

            if (!_interactableCache.ContainsKey(info.Identifier))
            {
                _interactableCache.Add(info.Identifier, info);
            }
            else
            {
                // Полезный варнинг: если при создании префаба ты скопировал объект, ID могли сдублироваться
                Debug.LogWarning($"[PromptController] Дубликат ID '{info.Identifier}' на объекте '{info.name}'. Игнорирую.");
            }
        }
        Debug.Log($"[PromptController] Зарегистрировано {allInteractables.Length} интерактивных объектов.");
    }

    /// <summary>
    /// Находит объект с компонентом InteractableInfo по его строковому идентификатору.
    /// </summary>
    private InteractableInfo FindInteractableInfoById(string identifier)
    {
        if (string.IsNullOrEmpty(identifier)) return null;

        // Сначала ищем в кэше (быстро)
        if (_interactableCache.TryGetValue(identifier, out InteractableInfo info))
        {
            return info;
        }

        // Фолбэк (на случай, если объект не из машины, а статический в сцене)
        // Можно оставить старый поиск как запасной вариант, но лучше полагаться на кэш.
        Debug.LogWarning($"[PromptController] ID '{identifier}' не найден в кэше машины.");
        return null;
    }


    #endregion

    #region UI Update Logic

    /// <summary>
    /// Обновляет отображение текста и кнопок в зависимости от состояния (свернуто/развернуто).
    /// </summary>
    private void UpdateDisplay()
    {
        if (isCollapsed)
        {
            Text targetCollapsedTextComp = (collapsedTextComponent != null && collapsedTextComponent != textComponent) ? collapsedTextComponent : textComponent;
            if(targetCollapsedTextComp != null)
            {
                targetCollapsedTextComp.text = currentCollapsedText;
                targetCollapsedTextComp.gameObject.SetActive(true);
            }
            if (textComponent != null && textComponent != targetCollapsedTextComp)
            {
                 textComponent.gameObject.SetActive(false);
            }
            UpdateButtonsInternal(null);
        }
        else
        {
            if (textComponent != null)
            {
                 textComponent.text = currentMainText;
                 textComponent.gameObject.SetActive(true);
            }
            if (collapsedTextComponent != null && collapsedTextComponent != textComponent)
            {
                collapsedTextComponent.gameObject.SetActive(false);
            }
            UpdateButtonsInternal(currentButtons);
        }
    }

    /// <summary>
    /// Внутренний метод для настройки и отображения кнопок по списку данных.
    /// </summary>
    private void UpdateButtonsInternal(List<ButtonData> buttonDataList)
    {
        if (buttons == null) return;

        bool showContainer = buttonDataList != null && buttonDataList.Count > 0 && !isCollapsed;
        if (buttonContainer != null) buttonContainer.SetActive(showContainer);

        if (!showContainer)
        {
             foreach (var btn in buttons) if (btn != null) btn.gameObject.SetActive(false);
             return;
        }

        int dataCount = buttonDataList?.Count ?? 0;
        int buttonsToDisplay = Mathf.Min(dataCount, buttons.Length);

        for (int i = 0; i < buttonsToDisplay; i++)
        {
            Button currentButton = buttons[i];
            ButtonData currentButtonData = buttonDataList[i];

            if (currentButton == null) continue;
            if (currentButtonData == null) { currentButton.gameObject.SetActive(false); continue; }

            currentButton.gameObject.SetActive(true);
            currentButton.interactable = true;

            Text buttonTextComp = currentButton.GetComponentInChildren<Text>(true);
            if (buttonTextComp != null)
            {
                buttonTextComp.text = currentButtonData.buttonText ?? $"Кнопка {i+1}";
            }

            string link = currentButtonData.link;
            currentButton.onClick.RemoveAllListeners();
            if (!string.IsNullOrEmpty(link))
            {
                currentButton.onClick.AddListener(() => OpenLinkInternal(link));
            }
        }

        // Скрываем неиспользуемые кнопки
        for (int i = buttonsToDisplay; i < buttons.Length; i++)
        {
             if (buttons[i] != null) buttons[i].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Безопасно открывает URL-ссылку.
    /// </summary>
    private void OpenLinkInternal(string url)
    {
        if (!string.IsNullOrEmpty(url))
        {
            // Application.OpenURL сам корректно обрабатывает протоколы
            try
            {
                 Application.OpenURL(url);
            }
            catch (Exception ex)
            {
                 Debug.LogError($"[PromptController] Не удалось открыть URL '{url}': {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning("[PromptController] Попытка открыть пустой URL.");
        }
    }

    #endregion

    #region Main Panel Logic & Animations
    
    /// <summary>
    /// Публичный метод для переключения состояния панели (свернуто/развернуто).
    /// </summary>
    public void ToggleCollapseState()
    {
        Debug.Log("[PromptController] ToggleCollapseState called!");
        if (rectTransform == null)
        {
             Debug.LogError("[PromptController] Cannot toggle collapse: RectTransform is missing.");
             return;
        }

        isCollapsed = !isCollapsed;
        SystemStateMonitor.Instance?.ReportPromptPanelState(isCollapsed, currentKeyOrIdentifier);
        targetSize = isCollapsed ? collapsedSize : expandedSize;

        // Если сворачиваем/разворачиваем панель, любая анимация уведомления должна быть прервана.
        if (isAnimatingNotificationBackground)
        {
            if (notificationCoroutine != null) StopCoroutine(notificationCoroutine);
            ResetNotificationBackgroundSize();
            notificationCoroutine = null;
            _activeKeyOrIdInNotificationCoroutine = null;
            isAnimatingNotificationBackground = false;
            EventManager.Instance?.RaiseEvent(EventType.PromptInteractionBlockFinished, null);
        }
        
        // Когда панель разворачивается, прозрачная кнопка-перехватчик должна быть неактивна
        if (!isCollapsed && expandButton != null)
        {
            expandButton.onClick.RemoveAllListeners();
            expandButton.gameObject.SetActive(false);
        }

        if (resizeCoroutine != null) StopCoroutine(resizeCoroutine);
        resizeCoroutine = StartCoroutine(AnimateSizeChange(targetSize));
        UpdateHideButtonSprite();
    }
    
    /// <summary>
    /// Корутина для плавной анимации изменения размера панели.
    /// </summary>
    private IEnumerator AnimateSizeChange(Vector2 finalSize)
    {
        Vector2 startSize = rectTransform.sizeDelta;
        float journey = 0f;
        // Расчет длительности для консистентной скорости анимации
        float duration = Vector2.Distance(startSize, finalSize) / (resizeSpeed * 50f); 
        duration = Mathf.Max(duration, 0.1f); // Минимальная длительность

        while (journey < duration)
        {
             journey += Time.unscaledDeltaTime;
             float percent = Mathf.Clamp01(journey / duration);
             percent = percent * percent * (3f - 2f * percent); // EaseInOut

             currentSize = Vector2.Lerp(startSize, finalSize, percent);
             if (rectTransform != null) rectTransform.sizeDelta = currentSize;
             UpdateFrameSize(currentSize);
             yield return null;
        }

        // Финальная установка значений
        if (rectTransform != null) rectTransform.sizeDelta = finalSize;
        currentSize = finalSize;
        UpdateFrameSize(finalSize);
        UpdateDisplay(); // Обновляем содержимое (текст/кнопки) ПОСЛЕ завершения анимации
        resizeCoroutine = null;
    }
    
    /// <summary>
    /// Корутина для анимации "подпрыгивания" кнопки сворачивания.
    /// </summary>
    private IEnumerator BounceHideButton()
    {
        if (hideButton == null) yield break;
        
        if (originalHideButtonScale == Vector3.zero)
                originalHideButtonScale = hideButton.transform.localScale;
        if (originalHideButtonScale == Vector3.zero) yield break;

        Vector3 baseScale = originalHideButtonScale;
        Vector3 targetScale = new Vector3(baseScale.x * bounceScaleFactor.x, baseScale.y * bounceScaleFactor.y, baseScale.z);
        float elapsed;

        for (int i = 0; i < bounceCount; i++)
        {
                elapsed = 0f;
                while (elapsed < bounceDurationUp)
                {
                    if (hideButton == null) yield break; // Защита, если объект уничтожен
                    hideButton.transform.localScale = Vector3.Lerp(baseScale, targetScale, elapsed / bounceDurationUp);
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (hideButton == null) yield break;
                hideButton.transform.localScale = targetScale;

                elapsed = 0f;
                while (elapsed < bounceDurationDown)
                {
                    if (hideButton == null) yield break;
                    hideButton.transform.localScale = Vector3.Lerp(targetScale, baseScale, elapsed / bounceDurationDown);
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (hideButton == null) yield break;
                hideButton.transform.localScale = baseScale;
        }
    }

    /// <summary>
    /// Корутина для анимации фонового элемента-уведомления.
    /// </summary>
    private IEnumerator AnimateNotificationBackground()
    {
        RectTransform imgRectTransform = null;
        if (imageObject != null)
        {
            imgRectTransform = imageObject.GetComponent<RectTransform>();
        }
        if (imgRectTransform == null) goto EndCoroutineSafely; // Выход, если нет цели для анимации

        // --- Начало анимации ---
        isAnimatingNotificationBackground = true;
        EventManager.Instance?.RaiseEvent(EventType.PromptInteractionBlockStarted, null);

        Vector2 startSize = collapsedSize;
        imgRectTransform.sizeDelta = startSize;
        Vector2 targetSizeAnim = new Vector2(collapsedSize.x, collapsedSize.y + notificationHeightIncrease);

        // Фаза 1: Увеличение
        float elapsed = 0f;
        while (elapsed < notificationAnimDuration)
        {
            if (imgRectTransform == null) goto EndCoroutineSafely;
            imgRectTransform.sizeDelta = Vector2.Lerp(startSize, targetSizeAnim, elapsed / notificationAnimDuration);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        if (imgRectTransform == null) goto EndCoroutineSafely;
        imgRectTransform.sizeDelta = targetSizeAnim;

        // Фаза 2: Пауза
        float holdTimer = 0f;
        while(holdTimer < notificationHoldDuration)
        {
            if (imgRectTransform == null || !isCollapsed) goto EndCoroutineSafely;
            holdTimer += Time.unscaledDeltaTime;
            yield return null;
        }

        // Фаза 3: Уменьшение
        elapsed = 0f;
        while (elapsed < notificationAnimDuration)
        {
            if (imgRectTransform == null || !isCollapsed) goto EndCoroutineSafely;
            imgRectTransform.sizeDelta = Vector2.Lerp(targetSizeAnim, startSize, elapsed / notificationAnimDuration);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        EndCoroutineSafely: // Метка для безопасного завершения корутины
        if (isAnimatingNotificationBackground)
        {
            EventManager.Instance?.RaiseEvent(EventType.PromptInteractionBlockFinished, null);
        }
        
        notificationCoroutine = null;
        _activeKeyOrIdInNotificationCoroutine = null;
        isAnimatingNotificationBackground = false;
        ResetNotificationBackgroundSize();
    }

    #endregion

    #region Helper Methods
    
    /// <summary>
    /// Устанавливает начальное состояние панели при старте.
    /// </summary>
    private void ApplyInitialCollapsedState(bool startCollapsed)
    {
        isCollapsed = startCollapsed;
        targetSize = isCollapsed ? collapsedSize : expandedSize;
        currentSize = targetSize;

        if (rectTransform != null) rectTransform.sizeDelta = currentSize;
        UpdateFrameSize(currentSize);
        UpdateHideButtonSprite();
        UpdateDisplay();
    }

    /// <summary>
    /// Обновляет спрайт на кнопке сворачивания.
    /// </summary>
    private void UpdateHideButtonSprite()
    {
        if (hideButton != null && hideButton.image != null && hideButtonImages != null && hideButtonImages.Length >= 2)
        {
            int spriteIndex = isCollapsed ? 1 : 0;
            if (hideButtonImages[spriteIndex] != null)
            {
                hideButton.image.sprite = hideButtonImages[spriteIndex];
            }
        }
    }

    /// <summary>
    /// Подгоняет размер рамки под размер основной панели с учетом отступа.
    /// </summary>
    private void UpdateFrameSize(Vector2 parentSize)
    {
        if (frameRectTransform != null)
        {
            float frameWidth = Mathf.Max(0, parentSize.x - (framePadding * 2));
            float frameHeight = Mathf.Max(0, parentSize.y - (framePadding * 2));
            frameRectTransform.sizeDelta = new Vector2(frameWidth, frameHeight);
        }
    }

    /// <summary>
    /// Сбрасывает размер фона-уведомления к стандартному свернутому размеру.
    /// </summary>
    private void ResetNotificationBackgroundSize()
    {
         if (imageObject != null)
         {
             RectTransform imgRectTransform = imageObject.GetComponent<RectTransform>();
             if (imgRectTransform != null)
             {
                 imgRectTransform.sizeDelta = collapsedSize;
             }
         }
    }

    /// <summary>
    /// Проверяет, существует ли системный промпт с указанным ключом.
    /// </summary>
    public bool HasSystemPrompt(string key)
    {
        if (string.IsNullOrEmpty(key) || systemPrompts == null) return false;
        return systemPrompts.ContainsKey(key);
    }

    #endregion
}