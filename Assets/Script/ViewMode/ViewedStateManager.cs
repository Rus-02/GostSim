using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Collections;
using TMPro;

public class ViewedStateManager : MonoBehaviour
{
    #region Singleton

    private static ViewedStateManager _instance;
    public static ViewedStateManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<ViewedStateManager>();
                if (_instance == null)
                {
                    GameObject singletonObject = new GameObject("ViewedStateManager");
                    _instance = singletonObject.AddComponent<ViewedStateManager>();
                    Debug.Log("[ViewedStateManager] Экземпляр создан автоматически.");
                }
            }
            return _instance;
        }
    }
    #endregion

    // ===================================================================================
    // Настройки для отладки
    // ===================================================================================
    [Header("Debugging")]
    [Tooltip("Включает подробное логирование всех внутренних шагов VSM в консоль.")]
    [SerializeField] private bool enableVerboseLogging = false;
    // ===================================================================================

    [Header("UI References (View Mode)")]
    [SerializeField] private TMP_Dropdown frameDropdown;
    [SerializeField] private TMP_Dropdown fixtureDropdown;
    [SerializeField] private TMP_Dropdown hydraulicsDropdown;
    [SerializeField] private TMP_Dropdown measurementDropdown;

    [Header("UI Containers")]
    [Tooltip("Контейнер с кнопками выбора категорий (Рама, Оснастка и т.д.). Активируется/деактивируется.")]
    [SerializeField] private GameObject categoryButtonContainer;
    [SerializeField] private GameObject homeConfirmationContainer;
    [SerializeField] private GameObject exitConfirmationContainer;
    [Tooltip("Контейнер с переключателем состояния дверей.")]
    [SerializeField] private GameObject doorControlsContainer;


    [Header("External References")]
    [SerializeField] private MenuDropdownData menuData;
    [SerializeField] private GameObject MenuButton;
    [SerializeField] private GameObject HomeButton;
    [Tooltip("Переключатель (Toggle) для открытия/закрытия дверей.")]
    [SerializeField] private UnityEngine.UI.Toggle doorToggle;

    [Header("Prompt Settings")]
    [Tooltip("Ключ системного сообщения для PromptController, которое будет отображаться при закрытии всех дропдаунов категорий.")]
    [SerializeField] private string basePromptKeyForDropdownsClosed = "VSM_Default"; // Ключ для "базового/пустого" состояния промпта

    [Header("Controlled Objects")]    // Ссылки на объекты, видимостью которых управляет VSM
    private List<GameObject> protectiveCasings; // Кожухи
    [SerializeField] private GameObject extensometerModel;

    [Header("Controls Info Panel")] // Панель с информацией о схемах управления
    [Tooltip("Панель, которая показывает схему управления.")]
    [SerializeField] private GameObject controlsInfoPanel;
    [Tooltip("Компонент Image, на котором будет показана схема (мышь или тач).")]
    [SerializeField] private UnityEngine.UI.Image controlsDisplayImage;
    [Tooltip("Спрайт со схемой управления для тач-устройств.")]
    [SerializeField] private Sprite touchControlsSprite;
    [Tooltip("Спрайт со схемой управления для мыши.")]
    [SerializeField] private Sprite mouseControlsSprite;

    // --- Внутреннее состояние VSM ---

    // Список инициализации воркфлоуРаннера
    private WorkflowRunner _workflowRunner;

    // Словарь для связи ID кнопок категорий с соответствующими дропдаунами
    private Dictionary<string, TMP_Dropdown> buttonIdToDropdownMap = new Dictionary<string, TMP_Dropdown>();
    private bool areCasingsVisible = true;
    private bool isPlayMenuContainerIntendedVisible = false;
    private bool isDoorControlsIntendedVisible = false; // Флаг для отслеживания желаемого состояния видимости контейнера управления дверями

    // Словарь для сопоставления строковых ID кнопок с целями фокусировки камеры
    private Dictionary<string, CameraFocusTarget> buttonIdToFocusTargetMap;

    // Отслеживаем, активен ли какой-либо дропдаун категории в данный момент
    private bool isAnyCategoryDropdownActive = false;
    private bool casingsHiddenAutomatically = false; 

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        InitializeDropdownMap();
        InitializeFocusTargetMap(); // Инициализируем карту целей фокусировки
        _workflowRunner = new WorkflowRunner(this);
    }

    /// Инициализация из Паспорта Машины.
    public void Initialize(MachineVisualData visualData)
    {
        if (visualData == null) return;

        // Берём список объектов для скрытия (кожухи, двери) из паспорта
        protectiveCasings = visualData.ObjectsToHide;

        // Сразу применяем текущее состояние видимости (скрыть/показать)
        UpdateCasingsVisibility(areCasingsVisible);
        
        Debug.Log($"[VSM] Initialized. Protective objects count: {protectiveCasings?.Count ?? 0}");
    }

    // Инициализация словаря для связи ID кнопок категорий с их дропдаунами
    private void InitializeDropdownMap()
    {
        if (frameDropdown != null) buttonIdToDropdownMap["FocusButton_Frame"] = frameDropdown;
        if (fixtureDropdown != null) buttonIdToDropdownMap["FocusButton_Fixture"] = fixtureDropdown;
        if (hydraulicsDropdown != null) buttonIdToDropdownMap["FocusButton_Hydraulics"] = hydraulicsDropdown;
        if (measurementDropdown != null) buttonIdToDropdownMap["FocusButton_Measurement"] = measurementDropdown;

        Debug.Log($"[VSM] Dropdown map initialized. Found {buttonIdToDropdownMap.Count} mappings.");
    }

    // Инициализация словаря для связи ID кнопок категорий с целями фокусировки камеры
    private void InitializeFocusTargetMap()
    {
        buttonIdToFocusTargetMap = new Dictionary<string, CameraFocusTarget>
        {
            { "FocusButton_Frame",       CameraFocusTarget.Frame },
            { "FocusButton_Fixture",     CameraFocusTarget.Fixture },
            { "FocusButton_Hydraulics",  CameraFocusTarget.Hydraulics },
            { "FocusButton_Measurement", CameraFocusTarget.Measurement },
        };
        Debug.Log($"[VSM] Focus Target map initialized with {buttonIdToFocusTargetMap.Count} entries.");
    }

    void Start()
    {
        // Проверяем зависимости от других менеджеров
        if (EventManager.Instance == null) Debug.LogError("[VSM] EventManager.Instance не доступен!", this);
        if (ToDoManager.Instance == null) Debug.LogError("[VSM] ToDoManager.Instance не доступен!", this);
        if (FixtureController.Instance == null) Debug.LogError("[VSM] FixtureController.Instance не доступен!", this);
        if (FixtureManager.Instance == null) Debug.LogError("[VSM] FixtureManager.Instance не доступен!", this);
        if (CameraController.Instance == null) Debug.LogWarning("[VSM] CameraController.Instance не доступен! Фокусировка камеры может не работать.", this);
        if (menuData == null) Debug.LogError("[VSM] MenuDropdownData не назначен!", this);


        SubscribeToEvents();
        SetupUIListeners();

        UpdateCasingsVisibility(areCasingsVisible);
        // Инициализация UI для дверей и его состояния
        if (doorControlsContainer != null) doorControlsContainer.SetActive(false);
        isDoorControlsIntendedVisible = false; // Убедимся, что флаг сброшен
        SystemStateMonitor.Instance?.ReportDoorState(true); // Сообщаем, что двери закрыты при старте

        // Скрываем контейнеры при старте
        if (homeConfirmationContainer != null) homeConfirmationContainer.SetActive(false);
        if (exitConfirmationContainer != null) exitConfirmationContainer.SetActive(false);
        if (categoryButtonContainer != null) categoryButtonContainer.SetActive(false);
        DeactivateAllCategoryDropdowns(); // Этот метод устанавливает базовый промпт

        //RequestCurrentFixtureState();
        // Установка начального "приветственного" промпта при старте
        SendPromptUpdateCommand("VSM_Start", PromptSourceType.SystemAction, "StartApp", true);
        SystemStateMonitor.Instance?.ReportApplicationMode(ApplicationMode.ViewMode);
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
        StopAllCoroutines();
    }

    #region Подписка / Отписка от событий EventManager

    private void SubscribeToEvents()
    {
        if (EventManager.Instance == null) return;
        //EventManager.Instance.Subscribe(EventType.FixtureAnimationFinished, this, HandleFixtureAnimationFinished);
        EventManager.Instance.Subscribe(EventType.ViewMenuButtonAction, this, HandleViewMenuButtonClick);
        EventManager.Instance.Subscribe(EventType.ViewHomeButtonAction, this, HandleViewHomeButtonClick);
        EventManager.Instance.Subscribe(EventType.ViewPlayButtonAction, this, HandleViewPlayButtonAction);
        EventManager.Instance.Subscribe(EventType.ViewVisibleButtonAction, this, HandleViewVisibleButtonClick);
        EventManager.Instance.Subscribe(EventType.ViewToggleAction, this, HandleViewDoorToggleClick);
        EventManager.Instance.Subscribe(EventType.DoorStateChanged, this, HandleDoorStateChangedEvent);
        EventManager.Instance.Subscribe(EventType.ViewExitButtonAction, this, HandleViewExitButtonClick);
        EventManager.Instance.Subscribe(EventType.ViewHomeConfirmAction, this, HandleViewHomeConfirmClick);
        EventManager.Instance.Subscribe(EventType.ViewExitConfirmAction, this, HandleViewExitConfirmClick);
        EventManager.Instance.Subscribe(EventType.ButtonClicked, this, HandleCategoryButtonClick);
        EventManager.Instance.Subscribe(EventType.RequestViewPlayContainerDisable, this, HandleRequestViewPlayContainerDisable);
        EventManager.Instance.Subscribe(EventType.RequestDoorControlsDisable, this, HandleRequestDoorControlsDisable);
        EventManager.Instance.Subscribe(EventType.ShowInteractableInfo, this, HandleShowInteractableInfo);
        EventManager.Instance.Subscribe(EventType.ClickedInteractableInfo, this, HandleClickedInteractableInfo);
        EventManager.Instance.Subscribe(EventType.GlobalModeButtonsVisibilityChanged, this, HandleGlobalModeButtonsVisibilityChanged);
        EventManager.Instance.Subscribe(EventType.ViewInfoAction, this, HandleViewInfoButtonAction);
        EventManager.Instance.Subscribe(EventType.DoorToggle, this, HandleDoorToggle);
    }

    private void UnsubscribeFromEvents()
    {
        if (EventManager.Instance != null)
        {
            //EventManager.Instance.Unsubscribe(EventType.FixtureAnimationFinished, this, HandleFixtureAnimationFinished);
            EventManager.Instance.Unsubscribe(EventType.ViewMenuButtonAction, this, HandleViewMenuButtonClick);
            EventManager.Instance.Unsubscribe(EventType.ViewHomeButtonAction, this, HandleViewHomeButtonClick);
            EventManager.Instance.Unsubscribe(EventType.ViewPlayButtonAction, this, HandleViewPlayButtonAction);
            EventManager.Instance.Unsubscribe(EventType.ViewVisibleButtonAction, this, HandleViewVisibleButtonClick);
            EventManager.Instance.Unsubscribe(EventType.ViewToggleAction, this, HandleViewDoorToggleClick);
            EventManager.Instance.Unsubscribe(EventType.DoorStateChanged, this, HandleDoorStateChangedEvent);
            EventManager.Instance.Unsubscribe(EventType.ViewExitButtonAction, this, HandleViewExitButtonClick);
            EventManager.Instance.Unsubscribe(EventType.ViewHomeConfirmAction, this, HandleViewHomeConfirmClick);
            EventManager.Instance.Unsubscribe(EventType.ViewExitConfirmAction, this, HandleViewExitConfirmClick);
            EventManager.Instance.Unsubscribe(EventType.ButtonClicked, this, HandleCategoryButtonClick);
            EventManager.Instance.Unsubscribe(EventType.RequestViewPlayContainerDisable, this, HandleRequestViewPlayContainerDisable);
            EventManager.Instance.Unsubscribe(EventType.RequestDoorControlsDisable, this, HandleRequestDoorControlsDisable);
            EventManager.Instance.Unsubscribe(EventType.ShowInteractableInfo, this, HandleShowInteractableInfo);
            EventManager.Instance.Unsubscribe(EventType.ClickedInteractableInfo, this, HandleClickedInteractableInfo);
            EventManager.Instance.Unsubscribe(EventType.GlobalModeButtonsVisibilityChanged, this, HandleGlobalModeButtonsVisibilityChanged);
            EventManager.Instance.Unsubscribe(EventType.ViewInfoAction, this, HandleViewInfoButtonAction);
            EventManager.Instance.Unsubscribe(EventType.DoorToggle, this, HandleDoorToggle);
        }
    }

/*
    private void HandleFixtureAnimationFinished(EventArgs args)
    {
        // Этому методу больше НЕ НУЖНО знать направление анимации.
        // Его единственная задача - "отпустить" корутину.
        if (args is FixtureEventArguments fa)
        {
            // Мы не знаем, была это установка или удаление, поэтому просто
            // пытаемся удалить ID из ОБОИХ списков. Если его там нет, ничего страшного.
            _vsmRemovingFixtureIds.Remove(fa.FixtureId);
            _vsmInstallingFixtureIds.Remove(fa.FixtureId);
            Debug.Log($"[VSM Workflow] ID {fa.FixtureId} обработан по событию AnimFinished.");
        }
    }*/

    // --- Обработчики событий от InteractionDetector ---

    /// Обрабатывает событие наведения курсора на интерактивный объект.
    private void HandleShowInteractableInfo(EventArgs args)
    {
        if (!this.gameObject.activeInHierarchy || !this.enabled) return;
        if (!(args is ShowInteractableInfoEventArgs hoverArgs)) return;
        if (hoverArgs.TargetObject != null) // Если есть объект, на который навели курсор
        {
            // 1. Команда для HighlightController
            var highlightArgs = new UpdateHighlightArgs(hoverArgs.TargetObject);
            ToDoManager.Instance?.HandleAction(ActionType.UpdateHighlight, highlightArgs);

            // 2. Команда для PromptController
            // Убедимся, что keyOrId не пустой, перед отправкой команды в PromptController
            string keyOrId = !string.IsNullOrEmpty(hoverArgs.SystemPromptKeyFromInteractable)
                        ? hoverArgs.SystemPromptKeyFromInteractable
                        : hoverArgs.TargetIdentifier;

            if (!string.IsNullOrEmpty(keyOrId))
            {
                PromptSourceType sourceType = !string.IsNullOrEmpty(hoverArgs.SystemPromptKeyFromInteractable)
                                            ? PromptSourceType.SystemAction
                                            : PromptSourceType.HoverInteraction;

                // Добавляем флаг isNewTarget для PromptController, чтобы он решил, нужна ли анимация
                var promptArgs = new UpdatePromptArgs(keyOrId, sourceType, $"Hover: {hoverArgs.TargetObject?.name ?? "None"}", hoverArgs.IsNewTargetForPrompt);
                ToDoManager.Instance?.HandleAction(ActionType.UpdatePromptDisplay, promptArgs);
            }
            else
            {
                Debug.LogWarning($"[VSM] HandleShowInteractableInfo: TargetObject '{hoverArgs.TargetObject.name}' provided, but keyOrId is empty. Prompt not updated for hover.");
            }
        }
    }

    /// Обрабатывает событие клика на интерактивный объект.
    private void HandleClickedInteractableInfo(EventArgs args)
    {
        if (!this.gameObject.activeInHierarchy || !this.enabled) return;
        if (!(args is ClickedInteractableInfoEventArgs clickArgs)) return;

        // Отправляем команды на подсветку и обновление промпта

        if (clickArgs.TargetObject == null)
        {
            Debug.LogWarning("[VSM] HandleClickedInteractableInfo: clickArgs.TargetObject is null. Cannot process click.");
            return; // Нечего обрабатывать, если нет объекта
        }

        // 2. Команда для PromptController
        string keyOrId = !string.IsNullOrEmpty(clickArgs.SystemPromptKeyFromInteractable)
                       ? clickArgs.SystemPromptKeyFromInteractable
                       : clickArgs.TargetIdentifier;

        if (string.IsNullOrEmpty(keyOrId))
        {
            Debug.LogWarning($"[VSM] HandleClickedInteractableInfo: keyOrId is empty for clicked object '{clickArgs.TargetObject.name}'. Prompt not updated.");
            return;
        }

        PromptSourceType sourceType;
        if (!string.IsNullOrEmpty(clickArgs.SystemPromptKeyFromInteractable))
        {
            sourceType = PromptSourceType.SystemAction;
        }
        else if (clickArgs.InteractionType == InteractionType.Click)
        {
            sourceType = PromptSourceType.ClickInteraction;
        }
        else
        {
            Debug.LogWarning($"[VSM] Получен неожиданный InteractionType ({clickArgs.InteractionType}) в ClickedInteractableInfo. Treating as ClickInteraction.");
            sourceType = PromptSourceType.ClickInteraction;
        }

        // Для клика всегда передаем isNewTargetForPrompt = true, чтобы анимация промпта запускалась
        var promptArgs = new UpdatePromptArgs(keyOrId, sourceType, $"Clicked: {clickArgs.TargetObject?.name}", true);
        ToDoManager.Instance?.HandleAction(ActionType.UpdatePromptDisplay, promptArgs);
    }

    private void HandleGlobalModeButtonsVisibilityChanged(EventArgs args)
    {
        if (!(args is GlobalModeButtonsVisibilityEventArgs visibilityArgs)) return;

        // Меняем активность кнопок согласно полученным флагам
        if (MenuButton != null) MenuButton.SetActive(visibilityArgs.ShowMenuButton);
        if (HomeButton != null) HomeButton.SetActive(visibilityArgs.ShowHomeButton);
        if (enableVerboseLogging) Debug.Log($"[VSM] Handled GlobalModeButtonsVisibilityChanged: MenuButton Active = {visibilityArgs.ShowMenuButton}, HomeButton Active = {visibilityArgs.ShowHomeButton}");
    }

    /// Обрабатывает нажатие кнопки "View Info".
    private void HandleViewInfoButtonAction(EventArgs args)
    {
        Debug.Log("[VSM] Received View Info Button Action.");

        // Активируем Info Overlay через его контроллер.
        if (InfoOverlayController.Instance != null)
        {
            InfoOverlayController.Instance.ActivateOverlay();
        }
        else
        {
            Debug.LogError("[VSM] InfoOverlayController.Instance не найден! Невозможно активировать Info Overlay.", this);
        }

        // Определяем тип управления и назначаем нужный спрайт.
        if (Input.touchSupported)
        {
            controlsDisplayImage.sprite = touchControlsSprite;
        }
        else
        {
            controlsDisplayImage.sprite = mouseControlsSprite;
        }
    }

    // Обработчик события изменения состояния дверей от MachineController
    private void HandleDoorStateChangedEvent(EventArgs args)
    {
        if (args is DoorStateChangedEventArgs doorArgs)
        {
            if (doorToggle != null)
            {
                // Обновляем состояние Toggle без вызова его события onValueChanged, чтобы избежать зацикливания
                doorToggle.SetIsOnWithoutNotify(doorArgs.IsOpen);
            }
            if (enableVerboseLogging) Debug.Log($"[VSM] Door state updated from event: {(doorArgs.IsOpen ? "Open" : "Closed")}");
        }
    }

    // Обработчик события запроса на скрытие контейнера управления дверями
    private void HandleRequestDoorControlsDisable(EventArgs args)
    {
        if (isDoorControlsIntendedVisible) // Только если он должен был быть видимым
        {
            if (enableVerboseLogging) Debug.Log("[VSM] Handling RequestDoorControlsDisable event.");
            isDoorControlsIntendedVisible = false;
            if (doorControlsContainer != null)
            {
                doorControlsContainer.SetActive(false);
            }
        }
    }

    public void OnExtensometerToggleChanged(bool isOn)
    {
        extensometerModel.SetActive(isOn);
        SystemStateMonitor.Instance?.ReportExtensometerUsage(isOn);
    }

    #endregion
    #region Настройка слушателей UI (Только для Dropdown)

    private void SetupUIListeners()
    {
        if (frameDropdown != null) frameDropdown.onValueChanged.AddListener(OnFrameDropdownChanged);
        if (fixtureDropdown != null) fixtureDropdown.onValueChanged.AddListener(OnFixtureDropdownChanged);
        if (hydraulicsDropdown != null) hydraulicsDropdown.onValueChanged.AddListener(OnHydraulicsDropdownChanged);
        if (measurementDropdown != null) measurementDropdown.onValueChanged.AddListener(OnMeasurementDropdownChanged);
        if (doorToggle != null) doorToggle.onValueChanged.AddListener(OnDoorToggleChanged);
    }

    #endregion
    #region Обработчики UI Событий и Dropdown

    // --- Обработчики событий от стандартных кнопок ---
    private void HandleViewMenuButtonClick(EventArgs args)
    {
        EventManager.Instance?.RaiseEvent(EventType.RequestDoorControlsDisable, null);
        EventManager.Instance?.RaiseEvent(EventType.RequestViewPlayContainerDisable, null);
        ToggleCategoryMenu();
    }

    // Обработчик кнопки "Домой" (Home)
    private void HandleViewHomeButtonClick(EventArgs args)
    {
        EventManager.Instance?.RaiseEvent(EventType.RequestDoorControlsDisable, null);
        EventManager.Instance?.RaiseEvent(EventType.RequestViewPlayContainerDisable, null);
        if (homeConfirmationContainer != null && homeConfirmationContainer.activeSelf)
        {
            homeConfirmationContainer.SetActive(false);
            if (enableVerboseLogging) Debug.Log("[VSM] Home confirmation panel hidden by toggle.");
            return;
        }
        ShowHomeConfirmation();
    }


    private void HandleViewVisibleButtonClick(EventArgs args)
    {
        ToggleVisibility();
    }

    // Обработчик клика по кнопке управления дверями
    private void HandleViewDoorToggleClick(EventArgs args)
    {
        if (enableVerboseLogging) Debug.Log("[VSM] View Door Toggle Button Action received.");

        if (isDoorControlsIntendedVisible)
        {
            if (enableVerboseLogging) Debug.Log("[VSM] Door controls container is visible, raising RequestDoorControlsDisable event.");
            EventManager.Instance?.RaiseEvent(EventType.RequestDoorControlsDisable, null); // Запрашиваем скрытие
        }
        else
        {
            // 1. Устанавливаем желаемое состояние видимости для контейнера дверей
            isDoorControlsIntendedVisible = true;

            // 2. Генерируем события для скрытия других эксклюзивных панелей VSM
            EventManager.Instance?.RaiseEvent(EventType.RequestViewPlayContainerDisable, null); // Скрываем Play меню, если было активно
            if (categoryButtonContainer != null && categoryButtonContainer.activeSelf) // Если меню категорий было активно
            {
                categoryButtonContainer.SetActive(false); // Скрываем его
                DeactivateAllCategoryDropdowns(); // И все его дропдауны (это также сбросит промпт на базовый)
            }
            HideConfirmationContainers(); // Скрываем окна подтверждений

            // 3. Показываем контейнер управления дверями и настраиваем его
            if (doorControlsContainer != null)
            {
                doorControlsContainer.SetActive(true);
                if (doorToggle != null)
                {
                    // Берем состояние из Монитора.
                    bool isClosed = SystemStateMonitor.Instance.AreDoorsClosed;
                    doorToggle.SetIsOnWithoutNotify(!isClosed); 
                }
            }
            if (enableVerboseLogging) Debug.Log("[VSM] Door controls container requested to be visible. Other VSM panels (Play, Categories, Confirmations) requested to hide.");
        }
    }

    // Вызывается при изменении значения doorToggle
    private void OnDoorToggleChanged(bool isOn)
    {
        if (ToDoManager.Instance == null)
        {
            Debug.LogError("[VSM] ToDoManager.Instance not found. Cannot send SetDoorStateAction.");
            return;
        }        
        
        if (enableVerboseLogging) Debug.Log($"[VSM] Door toggle changed. Requesting action.");

        // Отправляем команду. MC сам посмотрит текущее состояние в Мониторе и переключит его.
        ToDoManager.Instance.HandleAction(ActionType.SetDoorStateAction, null);
    }

    private void HandleDoorToggle(EventArgs args)
    {
        if (doorToggle != null)
        {
            doorToggle.isOn = !doorToggle.isOn;            
        }
    }

    // Обработчик кнопки "Выход" (Exit)
    private void HandleViewExitButtonClick(EventArgs args)
    {
        EventManager.Instance?.RaiseEvent(EventType.RequestDoorControlsDisable, null);
        EventManager.Instance?.RaiseEvent(EventType.RequestViewPlayContainerDisable, null);

        if (exitConfirmationContainer != null && exitConfirmationContainer.activeSelf)
        {
            exitConfirmationContainer.SetActive(false);
            if (enableVerboseLogging) Debug.Log("[VSM] Exit confirmation panel hidden by toggle.");
            return;
        }
        ShowExitConfirmation();
    }

    private void HandleViewHomeConfirmClick(EventArgs args)
    {
        HideConfirmationContainers(); // Этот метод должен остаться для скрытия самих окон подтверждений
        if (categoryButtonContainer != null) categoryButtonContainer.SetActive(false); // Скрываем меню категорий
        DeactivateAllCategoryDropdowns(); // Скрываем дропдауны
        RequestCameraFocusAndSetContext(CameraFocusTarget.Overview);
    }

    private void HandleViewPlayButtonAction(EventArgs args)
    {
        if (enableVerboseLogging) Debug.Log("[VSM] View Play Button Action received.");
        EventManager.Instance?.RaiseEvent(EventType.RequestDoorControlsDisable, null);

        if (isPlayMenuContainerIntendedVisible)
        {
            if (enableVerboseLogging) Debug.Log("[VSM] Play menu is visible, raising RequestViewPlayContainerDisable event.");
            EventManager.Instance?.RaiseEvent(EventType.RequestViewPlayContainerDisable, null);
        }
        else
        {
            // 1. Устанавливаем желаемое состояние видимости и посылаем промпт
            isPlayMenuContainerIntendedVisible = true;
            SendPromptUpdateCommand("VSM_Play", PromptSourceType.SystemAction, "Category menu opened", true);

            // 2. Отправляем команду SPC для обновления его UI
            SendSetupActionButtonsVisibilityUpdate(true);

            // 3. VSM должен скрыть свои панели
            if (enableVerboseLogging) Debug.Log("[VSM] Play menu requested to be visible. Hiding VSM panels.");
            if (categoryButtonContainer != null) categoryButtonContainer.SetActive(false); // Скрываем меню категорий
            DeactivateAllCategoryDropdowns(); // Скрываем дропдауны
            HideConfirmationContainers(); // Скрываем окна подтверждений
        }
    }

    private void HandleViewExitConfirmClick(EventArgs args)
    {
        ConfirmExitApp();
    }

    // Обработчик события запроса на скрытие контейнера SPC
    private void HandleRequestViewPlayContainerDisable(EventArgs args)
    {
        // Проверяем, действительно ли нужно скрывать (был ли флаг true)
        if (isPlayMenuContainerIntendedVisible)
        {
            if (enableVerboseLogging) Debug.Log("[VSM] Handling RequestViewPlayContainerDisable event.");
            // Устанавливаем флаг в false
            isPlayMenuContainerIntendedVisible = false;
            // Отправляем команду на скрытие SPC
            SendSetupActionButtonsVisibilityUpdate(false);
        }
    }


    // --- Обработчик общего клика для кнопок категорий (управление UI и ФОКУСИРОВКОЙ) ---
    private void HandleCategoryButtonClick(EventArgs args)
    {
        // Получаем ID кнопки
        if (!(args is ButtonClickedEventArgs buttonArgs)) return;
        string buttonId = buttonArgs.ButtonId;
        if (buttonId == "ViewHome" || buttonId == "ViewExit" || buttonId == "ViewMenu") { return; }
        HideConfirmationContainers();

        // 1. Управление UI: Активация соответствующего Dropdown'а (если он есть)
        if (buttonIdToDropdownMap.TryGetValue(buttonId, out TMP_Dropdown targetDropdown))
        {
            if (enableVerboseLogging) Debug.Log($"[VSM] Button ID '{buttonId}' matched dropdown: {targetDropdown.name}. Activating dropdown.");
            ActivateCategoryDropdown(targetDropdown);
        }

        // 2. Управление камерой: Запрос на фокусировку (если ID кнопки есть в карте фокуса)
        if (buttonIdToFocusTargetMap.TryGetValue(buttonId, out CameraFocusTarget targetFocus))
        {
            if (enableVerboseLogging) Debug.Log($"[VSM] Button ID '{buttonId}' matched focus target: {targetFocus}. Requesting camera focus.");
            RequestCameraFocusAndSetContext(targetFocus);
            ToDoManager.Instance?.HandleAction(ActionType.UpdateHighlight, new UpdateHighlightArgs(true));
        }
    }


    // --- Общий обработчик изменения значения в Dropdown'ах (для НЕ оснастки) ---
    private void HandleGenericDropdownChanged(TMP_Dropdown dropdown, int index)
    {
        HideConfirmationContainers();
        if (index > 0) // Если выбран реальный элемент
        {
            GameObject selectedObject = menuData?.GetGameObjectByIndex(dropdown, index);
            if (selectedObject != null)
            {
                var highlightArgs = new UpdateHighlightArgs(selectedObject);
                ToDoManager.Instance?.HandleAction(ActionType.UpdateHighlight, highlightArgs);

                InteractableInfo info = selectedObject.GetComponent<InteractableInfo>();
                if (info != null)
                {
                    string keyOrId = !string.IsNullOrEmpty(info.SystemPromptKey) ? info.SystemPromptKey : info.Identifier;
                    if (!string.IsNullOrEmpty(keyOrId))
                    {
                        PromptSourceType sourceType = !string.IsNullOrEmpty(info.SystemPromptKey) ? PromptSourceType.SystemAction : PromptSourceType.DropdownSelection;
                        var promptArgs = new UpdatePromptArgs(keyOrId, sourceType, $"Dropdown selection: {selectedObject.name}", true);
                        ToDoManager.Instance?.HandleAction(ActionType.UpdatePromptDisplay, promptArgs);
                    }
                    else
                    {
                        Debug.LogWarning($"[VSM] На объекте '{selectedObject.name}' (из dropdown '{dropdown.name}') InteractableInfo не имеет валидного ID или SystemPromptKey. Промпт не обновлен.");
                    }
                }
                else
                {
                    Debug.LogWarning($"[VSM] На объекте '{selectedObject.name}' из dropdown '{dropdown.name}' не найден InteractableInfo.", selectedObject);
                }
            }
            else
            {
                ToDoManager.Instance?.HandleAction(ActionType.UpdateHighlight, new UpdateHighlightArgs(true)); // Очистить подсветку
                Debug.LogWarning($"Объект для dropdown {dropdown?.name} и индекса {index} не найден в MenuDropdownData.");
            }
        }
        else // Если выбран "--- Выберите ---" (index == 0)
        {
            ToDoManager.Instance?.HandleAction(ActionType.UpdateHighlight, new UpdateHighlightArgs(true)); // Очистить подсветку
            // Если в дропдауне выбрали "--- Выберите ---", и это был единственный активный UI, то можно установить базовый промпт.
            if (!string.IsNullOrEmpty(basePromptKeyForDropdownsClosed) && isAnyCategoryDropdownActive)
            {
                bool otherUiActive = (homeConfirmationContainer != null && homeConfirmationContainer.activeSelf) ||
                                    (exitConfirmationContainer != null && exitConfirmationContainer.activeSelf) ||
                                    (categoryButtonContainer != null && categoryButtonContainer.activeSelf); // Дропдаун активен внутри этого контейнера

                if (!otherUiActive || (categoryButtonContainer != null && categoryButtonContainer.activeSelf && !IsAnyOtherDropdownActive(dropdown))) // Если активен только контейнер категорий и ЭТОТ дропдаун
                {
                    SendPromptUpdateCommand(basePromptKeyForDropdownsClosed, PromptSourceType.SystemAction, "Dropdown reset to default", true);
                }
            }
        }
    }
    // Вспомогательный метод для проверки, активны ли другие дропдауны, кроме указанного
    private bool IsAnyOtherDropdownActive(TMP_Dropdown AusnahmeDropdown)
    {
        foreach (var entry in buttonIdToDropdownMap)
        {
            if (entry.Value != null && entry.Value != AusnahmeDropdown && entry.Value.gameObject.activeSelf)
            {
                return true; // Найден другой активный дропдаун
            }
        }
        return false; // Других активных дропдаунов нет
    }

    // Обработчики вызывают общий метод для дропдаунов НЕ оснастки
    private void OnFrameDropdownChanged(int index) { HandleGenericDropdownChanged(frameDropdown, index); }
    private void OnHydraulicsDropdownChanged(int index) { HandleGenericDropdownChanged(hydraulicsDropdown, index); }
    private void OnMeasurementDropdownChanged(int index) { HandleGenericDropdownChanged(measurementDropdown, index); }


    // Отдельный обработчик для Fixture Dropdown
    private void OnFixtureDropdownChanged(int index)
    {
        HideConfirmationContainers();

        if (index > 0) // Если выбран реальный элемент оснастки
        {
            GameObject representativeObject = menuData?.GetGameObjectByIndex(fixtureDropdown, index);
            if (representativeObject != null)
            {
                InteractableInfo info = representativeObject.GetComponent<InteractableInfo>();

                // --- ПРОВЕРКА: Является ли оснастка "обычной" или "сложной" ---
                FictiveTestParameters fictiveParamsComponent = representativeObject.GetComponent<FictiveTestParameters>();
                bool isComplexFixture = fictiveParamsComponent != null && info != null && info.isFixture && !string.IsNullOrEmpty(info.FixtureTypeDisplayName);

                if (info != null && info.isFixture && !string.IsNullOrEmpty(info.FixtureTypeDisplayName))
                {
                    if (!_workflowRunner.IsRunning)
                    {
                        // --- ОБРАБОТКА СЛОЖНОЙ ОСНАСТКИ ---
                        if (isComplexFixture)
                        {
                            if (enableVerboseLogging) Debug.Log($"[VSM] Обнаружен сложный тип оснастки '{info.FixtureTypeDisplayName}'. Подготовка Монитора и запуск Workflow...");

                            // 1. Вызываем новый метод, который и Монитор заполнит, и хендлер создаст.
                            var (specificHandler, tempConfig) = fictiveParamsComponent.PrimeMonitorAndCreateHandler();

                            // 2. Проверяем, что все прошло успешно.
                            if (specificHandler != null && tempConfig != null)
                            {
                                // 1. Создаем контекст
                                var ctx = new WorkflowContext(CentralizedStateManager.Instance, ActionRequester.VSM);
                                
                                // Передаем хендлер в контекст, чтобы шаги могли его найти
                                ctx.SetData(Step_CalculateFixturePlan.CTX_KEY_HANDLER_OVERRIDE, specificHandler);

                                // Передаем безопасную позицию (чтобы не было таймаута)
                                float? safePos = fictiveParamsComponent.SafeTraversePositionLocalZ; 
                                ctx.SetData(Step_EnsureClearance.CTX_KEY_SAFE_POS_OVERRIDE, safePos);

                                // 2. Собираем сценарий
                                var steps = BuildViewModeFixtureChangeScenario(tempConfig, fictiveParamsComponent);

                                // 3. Запускаем
                                _workflowRunner.Start(steps, ctx);
                            }
                            else
                            {
                                Debug.LogError("[VSM] Не удалось подготовить Монитор и создать хендлер. Смена оснастки прервана.");
                            }
                            return; // Прерываем стандартную обработку
                        }
                        // --- КОНЕЦ ОБРАБОТКИ СЛОЖНОЙ ОСНАСТКИ ---

                        // --- СТАНДАРТНАЯ ОБРАБОТКА ---
                        List<FixtureData> relatedAssets = info.associatedFixtureDataAssets;
                        List<string> targetFixtureIDs = relatedAssets?
                            .Where(dataAsset => dataAsset != null && !string.IsNullOrEmpty(dataAsset.fixtureId))
                            .Select(dataAsset => dataAsset.fixtureId)
                            .ToList() ?? new List<string>();

                        if (targetFixtureIDs.Any())
                        {
                            if (enableVerboseLogging) Debug.Log($"[VSM] Выбран тип оснастки '{info.FixtureTypeDisplayName}'. Запуск стандартной смены для {targetFixtureIDs.Count} ID: [{string.Join(", ", targetFixtureIDs)}]...");
                            //StartCoroutine(VSM_ExecuteFixtureChangeWorkflow(targetFixtureIDs, info.FixtureTypeDisplayName));
                        }
                        else
                        {
                            Debug.LogWarning($"[VSM] Не найдены ID оснастки для типа '{info.FixtureTypeDisplayName}'. Смена невозможна.");
                        }
                        // --- КОНЕЦ СТАНДАРТНОЙ ОБРАБОТКИ ---
                    }
                    else
                    {
                        Debug.LogWarning($"[VSM] Попытка смены оснастки типа '{info.FixtureTypeDisplayName}', но процесс уже идет.");
                    }
                }
                else
                {
                    string reason = info == null ? "InteractableInfo not found" :
                                    !info.isFixture ? "isFixture is false" :
                                    "FixtureTypeDisplayName is empty";
                    Debug.LogWarning($"[VSM] Объект-представитель '{representativeObject.name}' из fixtureDropdown не является корректной оснасткой ({reason}). Смена невозможна.");
                }
            }
            else
            {
                ToDoManager.Instance?.HandleAction(ActionType.UpdateHighlight, new UpdateHighlightArgs(true)); // Очистить подсветку
                Debug.LogWarning($"Объект для fixtureDropdown и индекса {index} не найден в MenuDropdownData.");
            }
        }
        else // Если выбран "- Выберите -" (index == 0)
        {
            ToDoManager.Instance?.HandleAction(ActionType.UpdateHighlight, new UpdateHighlightArgs(true)); // Очистить подсветку
            // Аналогично HandleGenericDropdownChanged, сбрасываем промпт если нужно
            if (!string.IsNullOrEmpty(basePromptKeyForDropdownsClosed) && isAnyCategoryDropdownActive)
            {
                bool otherUiActive = (homeConfirmationContainer != null && homeConfirmationContainer.activeSelf) ||
                                    (exitConfirmationContainer != null && exitConfirmationContainer.activeSelf) ||
                                    (categoryButtonContainer != null && categoryButtonContainer.activeSelf);
                if (!otherUiActive || (categoryButtonContainer != null && categoryButtonContainer.activeSelf && !IsAnyOtherDropdownActive(fixtureDropdown)))
                {
                    SendPromptUpdateCommand(basePromptKeyForDropdownsClosed, PromptSourceType.SystemAction, "Fixture dropdown reset to default", true);
                }
            }
        }
    }

    // --- Методы, реализующие действия кнопок ---

    private void ToggleCategoryMenu()
    {
        if (enableVerboseLogging) Debug.Log("[VSM] Toggle Category Menu requested");
        HideConfirmationContainers();
        if (categoryButtonContainer != null)
        {
            bool станетВидимым = !categoryButtonContainer.activeSelf;
            categoryButtonContainer.SetActive(станетВидимым);

            if (станетВидимым)
            {
                // При открытии меню категорий, убедимся, что другие эксклюзивные панели VSM скрыты
                if (isPlayMenuContainerIntendedVisible) EventManager.Instance?.RaiseEvent(EventType.RequestViewPlayContainerDisable, null);
                if (isDoorControlsIntendedVisible) EventManager.Instance?.RaiseEvent(EventType.RequestDoorControlsDisable, null);
                HideConfirmationContainers();

                SendPromptUpdateCommand("VSM_Menu", PromptSourceType.SystemAction, "Category menu opened", true);
            }
            else // Если скрыли меню категорий
            {
                DeactivateAllCategoryDropdowns(); // Это также установит базовый промпт и сбросит фокус
                if (enableVerboseLogging) Debug.Log("[VSM] Menu closed, requested Overview focus.");
            }
        }
    }

    private void ShowHomeConfirmation()
    {
        if (enableVerboseLogging) Debug.Log("[VSM] Home button action requested");
        // При показе подтверждения "Домой", скрываем другие эксклюзивные панели VSM
        if (isPlayMenuContainerIntendedVisible) EventManager.Instance?.RaiseEvent(EventType.RequestViewPlayContainerDisable, null);
        if (isDoorControlsIntendedVisible) EventManager.Instance?.RaiseEvent(EventType.RequestDoorControlsDisable, null);
        if (categoryButtonContainer != null) categoryButtonContainer.SetActive(false);
        DeactivateAllCategoryDropdowns();
        if (exitConfirmationContainer != null) exitConfirmationContainer.SetActive(false); // Скрываем другое подтверждение
        if (homeConfirmationContainer != null) homeConfirmationContainer.SetActive(true);
    }

    private void ToggleVisibility()
    {
        areCasingsVisible = !areCasingsVisible;
        UpdateCasingsVisibility(areCasingsVisible);
        SendPromptUpdateCommand("VSM_Visible", PromptSourceType.SystemAction, "Visible button clicked", true);
        casingsHiddenAutomatically = false;
    }

    private void ShowExitConfirmation()
    {
        if (enableVerboseLogging) Debug.Log("[VSM] Exit button action requested");
        if (isPlayMenuContainerIntendedVisible) EventManager.Instance?.RaiseEvent(EventType.RequestViewPlayContainerDisable, null);
        if (isDoorControlsIntendedVisible) EventManager.Instance?.RaiseEvent(EventType.RequestDoorControlsDisable, null);
        if (categoryButtonContainer != null) categoryButtonContainer.SetActive(false);
        DeactivateAllCategoryDropdowns();
        if (homeConfirmationContainer != null) homeConfirmationContainer.SetActive(false); // Скрываем другое подтверждение
        if (exitConfirmationContainer != null) exitConfirmationContainer.SetActive(true);
    }

    private void ConfirmExitApp()
    {
        if (enableVerboseLogging) Debug.Log("[VSM] Confirmed Exit Application action requested");
        // При подтверждении выхода, скрываем все активные панели VSM
        if (isPlayMenuContainerIntendedVisible) EventManager.Instance?.RaiseEvent(EventType.RequestViewPlayContainerDisable, null);
        if (isDoorControlsIntendedVisible) EventManager.Instance?.RaiseEvent(EventType.RequestDoorControlsDisable, null);
        if (categoryButtonContainer != null) categoryButtonContainer.SetActive(false);
        DeactivateAllCategoryDropdowns();
        HideConfirmationContainers();
        CrashAndHangDetector.NotifyCleanExit();
        Application.Quit();
#if UNITY_EDITOR
         UnityEditor.EditorApplication.isPlaying = false;
#endif
    }


    // --- Вспомогательные методы для UI ---
    private void HideConfirmationContainers()
    {
        if (homeConfirmationContainer != null) homeConfirmationContainer.SetActive(false);
        if (exitConfirmationContainer != null) exitConfirmationContainer.SetActive(false);
    }

    // Активирует один дропдаун категории и деактивирует остальные
    private void ActivateCategoryDropdown(TMP_Dropdown dropdownToActivate)
    {
        if (dropdownToActivate == null) return;

        // Перед активацией дропдауна, убедимся, что другие панели VSM скрыты
        HideConfirmationContainers();

        // Сначала деактивируем все *другие* дропдауны, если они были активны.
        foreach (var entry in buttonIdToDropdownMap)
        {
            if (entry.Value != null && entry.Value != dropdownToActivate && entry.Value.gameObject.activeSelf)
            {
                entry.Value.gameObject.SetActive(false);
            }
        }
        // Снимаем общую подсветку перед активацией нового дропдауна
        ToDoManager.Instance?.HandleAction(ActionType.UpdateHighlight, new UpdateHighlightArgs(true));

        // Активируем целевой
        dropdownToActivate.gameObject.SetActive(true);
        dropdownToActivate.value = 0; // Сбрасываем его значение на "--- Выберите ---"
        dropdownToActivate.RefreshShownValue();

        // Устанавливаем флаг, что какой-то дропдаун активен, и сообщаем об этом другим системам (например, InteractionDetector)
        if (!isAnyCategoryDropdownActive)
        {
            isAnyCategoryDropdownActive = true;
            SystemStateMonitor.Instance?.ReportDropdownMenuActivity(true);
            if (enableVerboseLogging) Debug.Log("[VSM] A category dropdown has been activated. Event DropdownMenuStateChanged(true) raised.");
        }
    }

    // Деактивирует все дропдауны категорий, УБИРАЕТ ПОДСВЕТКУ и УСТАНАВЛИВАЕТ БАЗОВЫЙ ПРОМПТ
    private void DeactivateAllCategoryDropdowns()
    {
        bool anyWasActive = false;
        // Проверяем активные дропдауны *перед* их деактивацией и запоминаем
        if (frameDropdown != null && frameDropdown.gameObject.activeSelf) { frameDropdown.gameObject.SetActive(false); anyWasActive = true; }
        if (fixtureDropdown != null && fixtureDropdown.gameObject.activeSelf) { fixtureDropdown.gameObject.SetActive(false); anyWasActive = true; }
        if (hydraulicsDropdown != null && hydraulicsDropdown.gameObject.activeSelf) { hydraulicsDropdown.gameObject.SetActive(false); anyWasActive = true; }
        if (measurementDropdown != null && measurementDropdown.gameObject.activeSelf) { measurementDropdown.gameObject.SetActive(false); anyWasActive = true; }

        // Убираем подсветку через команду (это всегда нужно при сбросе UI)
        ToDoManager.Instance?.HandleAction(ActionType.UpdateHighlight, new UpdateHighlightArgs(true));

        // Устанавливаем базовый промпт ТОЛЬКО если были активные дропдауны
        bool otherVsmUiActive = // (categoryButtonContainer != null && categoryButtonContainer.activeSelf) || // Не учитываем, т.к. дропдауны внутри него
                               (homeConfirmationContainer != null && homeConfirmationContainer.activeSelf) ||
                               (exitConfirmationContainer != null && exitConfirmationContainer.activeSelf) ||
                               (doorControlsContainer != null && doorControlsContainer.activeSelf && isDoorControlsIntendedVisible) || // Учитываем только если он должен быть видимым
                               (isPlayMenuContainerIntendedVisible);

        if (anyWasActive && !otherVsmUiActive && (categoryButtonContainer == null || !categoryButtonContainer.activeSelf)) // Только если дропдауны были активны И НИЧЕГО ДРУГОГО из VSM не активно (кроме самого меню категорий, если оно их содержит)
        {
            if (!string.IsNullOrEmpty(basePromptKeyForDropdownsClosed))
            {
                SendPromptUpdateCommand(
                    basePromptKeyForDropdownsClosed,
                    PromptSourceType.SystemAction,
                    "All category dropdowns deactivated, returning to base VSM state.",
                    true
                );
                if (enableVerboseLogging) Debug.Log($"[VSM] All category dropdowns deactivated. Set base prompt: {basePromptKeyForDropdownsClosed}");
            }
            else
            {
                Debug.LogWarning("[VSM] basePromptKeyForDropdownsClosed не задан! Не могу установить базовый промпт после закрытия всех дропдаунов.");
            }
            
        }
        else
        {
            if (enableVerboseLogging) Debug.Log($"[VSM] Base prompt not sent. anyWasActive: {anyWasActive}, otherVsmUiActive: {otherVsmUiActive}, categoryButtonContainerActive: {categoryButtonContainer?.activeSelf}");
        }

        if (isAnyCategoryDropdownActive || anyWasActive)
        {
            isAnyCategoryDropdownActive = false;
            SystemStateMonitor.Instance?.ReportDropdownMenuActivity(false);
            if (enableVerboseLogging) Debug.Log("[VSM] All category dropdowns are now inactive. Event DropdownMenuStateChanged(false) raised.");
            RequestCameraFocusAndSetContext(CameraFocusTarget.Overview);
        }
    }

    private void SendSetupActionButtonsVisibilityUpdate(bool isVisible)
    {
        if (ToDoManager.Instance == null)
        {
            Debug.LogError("[VSM] ToDoManager не найден! Не могу отправить SetSetupActionButtonsVisibilityAction.");
            return;
        }
        SetVisibilityArgs args = new SetVisibilityArgs(isVisible);
        ToDoManager.Instance.HandleAction(ActionType.SetSetupActionButtonsVisibilityAction, args);
    }

    private List<IWorkflowStep> BuildViewModeFixtureChangeScenario(TestConfigurationData config, FictiveTestParameters fictiveParams)
    {
        var steps = new List<IWorkflowStep>();

        // 1. Безопасность
        // (Для VSM передаем фиктивный TestType из конфига)
        steps.Add(new Step_EnsureClearance(config.testType));

        // 2. Математика (Расчет плана)
        steps.Add(new Step_CalculateFixturePlan(config));

        // 3. Открываем двери (В начале, как и в CSM)
        steps.Add(new Step_SetDoorState(true));

        // 4. Тихая пре-инициализация
        steps.Add(new Step_PreInitializeFixtures());

        // 5. Подготовка к снятию
        steps.Add(new Step_PrepareForTeardown());

        // 6. Снятие
        steps.Add(new Step_BatchFixtureAction(BatchActionMode.RemoveAllOld));

        // 7. Установка ОСНОВНОЙ
        steps.Add(new Step_BatchFixtureAction(BatchActionMode.InstallMain));

        // 8. Промежуточные
        steps.Add(new Step_ExecuteInterstitialCommands());

        // 9. Установка ВЛОЖЕННОЙ
        steps.Add(new Step_BatchFixtureAction(BatchActionMode.InstallInternal));

        // 10. Закрываем двери
        steps.Add(new Step_SetDoorState(false));

        // 11. Финализация (Уборка, сброс флагов системы)
        steps.Add(new Step_FinalizeFixtureChange());

        // 12. Специфичная уборка VSM (Сброс фиктивных параметров)
        // Теперь переменная fictiveParams доступна, так как мы передали её в аргументы метода
        steps.Add(new Step_VSM_Finalize(fictiveParams));

        return steps;
    }

/*
    /// <summary>
    /// Специальный корутин для установки/снятия сложной оснастки.
    /// Логика полностью повторяет ExecutePreciseFixtureChangeWorkflow из CSM, адаптирована для VSM.
    /// </summary>
    private IEnumerator VSM_ExecuteSpecialFixtureChangeWorkflow(
        ITestLogicHandler specificHandler,
        TestConfigurationData tempConfig,
        FictiveTestParameters fictiveParamsSource, // Принимаем сам компонент для вызова Reset
        string fixtureTypeNameForHighlight)
    {
        // --- НАЧАЛЬНАЯ ПРОВЕРКА ---
        if (isChangingFixture)
        {
            Debug.LogWarning("[VSM Special Workflow] Попытка запустить смену, когда она уже идет. Игнорируется.");
            yield break;
        }
        isChangingFixture = true;
        HideConfirmationContainers();
        Debug.Log($"<color=lightblue>[VSM Special Workflow] Запуск для хендлера '{specificHandler.GetType().Name}'.</color>");

        // =================================================================================
        // --- ОБЯЗАТЕЛЬНЫЙ БЛОК: Обеспечение пространства для установки ---
        // =================================================================================
        SystemStateMonitor.Instance?.ReportFixtureChangeStatus(true);
        ActionRequester myRequester = ActionRequester.VSM;

        var commandArgs = new EnsureFixtureInstallationClearanceArgs(
            fictiveParamsSource.SafeTraversePositionLocalZ,
            fictiveParamsSource.GeneralTestType,
            ActionRequester.VSM
        );

        bool commandCompleted = false;
        Action<EventArgs> onClearanceReadyHandler = (e) =>
        {
            if (e is FixtureInstallationClearanceReadyEventArgs eventArgs && eventArgs.Requester == myRequester)
            {
                commandCompleted = true;
            }
        };

        EventManager.Instance.Subscribe(EventType.FixtureInstallationClearanceReady, this, onClearanceReadyHandler);
        ToDoManager.Instance.HandleAction(ActionType.EnsureFixtureInstallationClearance, commandArgs);

        float timeout = 15.0f;
        float elapsedTime = 0f;
        while (!commandCompleted && elapsedTime < timeout)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        EventManager.Instance.Unsubscribe(EventType.FixtureInstallationClearanceReady, this, onClearanceReadyHandler);

        if (!commandCompleted)
        {
            Debug.LogError($"[VSM] Таймаут ожидания ответа от MachineController. Процесс смены оснастки прерван.");
            isChangingFixture = false;
            fictiveParamsSource.ResetMonitor(); // Очищаем даже при ошибке
            yield break;
        }

        if (enableVerboseLogging) Debug.Log("<color=green>[VSM]</color> Пространство для установки оснастки обеспечено. Продолжаю...");

        // =================================================================================
        // --- ПОЛУЧЕНИЕ ПЛАНА И ВЫПОЛНЕНИЕ ---
        // =================================================================================

        // --- ЭТАП 1: Получаем "Консультанта по сносу" ---
        ITestLogicHandler handlerForTeardown = FixtureController.Instance.GetActiveLogicHandler();

        // --- ЭТАП 2: "Прораб" составляет первоначальный план ---
        List<string> liveInstalledFixtures = SystemStateMonitor.Instance.AllInstalledFixtureIDs;
        FixtureChangePlan plan = specificHandler.CreateFixtureChangePlan(tempConfig, fictiveParamsSource.SampleShape, liveInstalledFixtures);
        
        if (plan != null)
        {
            var requiredIds = new HashSet<string>();
            plan.MainFixturesToInstall.ForEach(info => requiredIds.Add(info.FixtureId));
            plan.InternalFixturesToInstall.ForEach(info => requiredIds.Add(info.FixtureId));

            foreach (var installedId in liveInstalledFixtures)
            {
                if (!plan.MainFixturesToRemove.Contains(installedId))
                {
                    requiredIds.Add(installedId);
                }
            }
            
            SystemStateMonitor.Instance?.ReportRequiredFixtures(requiredIds.ToList());
        }

        if (plan == null)
        {
            Debug.LogError("<color=red>[VSM] Новый хендлер вернул null план смены.</color>");
            isChangingFixture = false;
            fictiveParamsSource.ResetMonitor();
            yield break;
        }

        // --- ЭТАП 3: Скорректировать план демонтажа ---
        if (handlerForTeardown != null)
        {
            if (enableVerboseLogging) Debug.Log($"[VSM Workflow] Для демонтажа будет использован консультант из памяти: {handlerForTeardown.GetType().Name}");
            
            plan.MainFixturesToRemove = handlerForTeardown.CreateTeardownPlan(plan.MainFixturesToRemove);
            
            var prepCommands = handlerForTeardown.GetPreChangePreparationCommands(plan.MainFixturesToRemove);
            if (prepCommands != null && prepCommands.Count > 0)
            {
                foreach (var command in prepCommands) { ToDoManager.Instance.HandleAction(command.Action, command.Args); }
                yield return new WaitForSeconds(0.5f);
            }
        }

        _vsmRemovingFixtureIds.Clear();
        _vsmInstallingFixtureIds.Clear();

        // --- БЛОК PREINITIALIZE ---
        bool preInitializeExecuted = false;
        if (plan.FixturesToPreInitialize != null && plan.FixturesToPreInitialize.Count > 0)
        {
            string firstFixtureId = plan.FixturesToPreInitialize[0];
            FixtureData firstFixtureData = FixtureManager.Instance.GetFixtureData(firstFixtureId);

            if (firstFixtureData != null)
            {
                FixtureData installedInZone = FixtureController.Instance.GetInstalledFixtureInZone(firstFixtureData.fixtureZone);

                if (installedInZone == null)
                {
                    if (enableVerboseLogging) Debug.Log($"<color=yellow>[VSM] Зона '{firstFixtureData.fixtureZone}' пуста. Запуск PreInitialize.</color>");
                    
                    // Открываем двери перед работой (если закрыты)
                    if (SystemStateMonitor.Instance.AreDoorsClosed)
                    {
                         ToDoManager.Instance?.HandleAction(ActionType.SetDoorStateAction, null);
                         yield return new WaitForSeconds(0.5f);
                    }

                    // 1. Родители
                    var parentsToInstall = new List<string>();
                    var childrenToInstall = new List<string>();
                    foreach (var fixtureId in plan.FixturesToPreInitialize)
                    {
                        var data = FixtureManager.Instance.GetFixtureData(fixtureId);
                        if (data != null && string.IsNullOrEmpty(data.parentFixtureId)) parentsToInstall.Add(fixtureId);
                        else childrenToInstall.Add(fixtureId);
                    }

                    foreach (var parentId in parentsToInstall)
                    {
                        ToDoManager.Instance.HandleAction(ActionType.PlaceFixtureWithoutAnimation, new PlaceFixtureArgs(parentId, null, null));
                        yield return new WaitForSeconds(0.05f);
                    }

                    // 2. Пересчет зон
                    if (enableVerboseLogging) Debug.Log($"<color=yellow>[VSM PreInitialize] Родители установлены. Принудительный пересчет зон...</color>");
                    ToDoManager.Instance.HandleAction(ActionType.ReinitializeFixtureZones, null);
                    yield return null;

                    // 3. Дети
                    if (enableVerboseLogging) Debug.Log($"<color=yellow>[VSM PreInitialize] Установка дочерних элементов...</color>");
                    foreach (var childId in childrenToInstall)
                    {
                        ToDoManager.Instance.HandleAction(ActionType.PlaceFixtureWithoutAnimation, new PlaceFixtureArgs(childId, null, null));
                        yield return new WaitForSeconds(0.05f);
                    }
                    yield return new WaitForSeconds(0.2f);

                    // Закрываем двери обратно для порядка (хотя тут спорно, но так было в логике)
                    // В CSM мы открывали снова, здесь по логике PreInit обычно закрывает.
                    // Оставим пока как было: закрыть.
                    // UPD: В CSM мы закрывали в конце PreInit. Ок.
                    if (!SystemStateMonitor.Instance.AreDoorsClosed)
                    {
                         ToDoManager.Instance?.HandleAction(ActionType.SetDoorStateAction, null);
                    }
                    
                    if (enableVerboseLogging) Debug.Log("<color=yellow>[VSM Special Workflow] PreInitialize завершен.</color>");
                    preInitializeExecuted = true;
                }
                else
                {
                    if (enableVerboseLogging) Debug.Log($"<color=orange>[VSM] Зона '{firstFixtureData.fixtureZone}' уже занята. Пропуск PreInitialize.</color>");
                }
            }
        }

        if (preInitializeExecuted)
        {
            plan.MainFixturesToInstall.RemoveAll(info => plan.FixturesToPreInitialize.Contains(info.FixtureId));
            plan.InternalFixturesToInstall.RemoveAll(item => plan.FixturesToPreInitialize.Contains(item.FixtureId));
        }

        // --- [ИЗМЕНЕНИЕ] ЛОГИКА ПРАВИЛЬНОГО УДАЛЕНИЯ ---
        if (plan.MainFixturesToRemove.Count > 0)
        {
            bool isRemovingProportionalRig = plan.MainFixturesToRemove.Any(id => id.StartsWith("Prop"));
            if (isRemovingProportionalRig)
            {
                if (enableVerboseLogging) Debug.Log("<color=orange>[VSM Special Workflow] Обнаружено удаление пропорциональной оснастки.</color>");
                var proportionalHandler = new TensileProportionalLogicHandler(null);
                var correctlyOrderedFixturesToRemove = proportionalHandler.CreateTeardownPlan(plan.MainFixturesToRemove);
                plan.MainFixturesToRemove = correctlyOrderedFixturesToRemove;

                var prepCommands = proportionalHandler.GetPreChangePreparationCommands(plan.MainFixturesToRemove);
                if (prepCommands != null && prepCommands.Count > 0)
                {
                    foreach (var command in prepCommands) { ToDoManager.Instance.HandleAction(command.Action, command.Args); }
                    yield return new WaitForSeconds(0.5f);
                }
            }
        }
        
        // --- СТАНДАРТНЫЙ WORKFLOW ---

        // 2. СНЯТИЕ ОСНОВНОЙ ОСНАСТКИ
        if (plan.MainFixturesToRemove.Count > 0)
        {
            if (enableVerboseLogging) Debug.Log($"<color=lightblue>[VSM] План на снятие: [{string.Join(", ", plan.MainFixturesToRemove)}]</color>");
            
            // Открываем двери (если закрыты)
            if (SystemStateMonitor.Instance.AreDoorsClosed)
            {
                 ToDoManager.Instance?.HandleAction(ActionType.SetDoorStateAction, null);
                 yield return new WaitForSeconds(0.5f);
            }

            _vsmRemovingFixtureIds.AddRange(plan.MainFixturesToRemove);
            foreach (var fixtureId in plan.MainFixturesToRemove)
            {
                ToDoManager.Instance.HandleAction(ActionType.PlayFixtureAnimationAction, new PlayFixtureAnimationArgs(fixtureId, AnimationDirection.Out, ActionRequester.VSM));
                yield return new WaitForSeconds(0.05f);
            }
            yield return new WaitUntil(() => _vsmRemovingFixtureIds.Count == 0);
            if (enableVerboseLogging) Debug.Log("<color=lightblue>[VSM] Снятие основной оснастки завершено.</color>");
        }

        // 3. УСТАНОВКА ОСНОВНОЙ ОСНАСТКИ
        if (plan.MainFixturesToInstall.Count > 0)
        {
            if (enableVerboseLogging) Debug.Log($"<color=lightblue>[VSM] План на установку ОСНОВНОЙ: [{string.Join(", ", plan.MainFixturesToInstall.Select(info => info.FixtureId))}]</color>");
            
            // Открываем двери (если закрыты) - на случай, если пропустили шаг снятия
            if (SystemStateMonitor.Instance.AreDoorsClosed)
            {
                 ToDoManager.Instance?.HandleAction(ActionType.SetDoorStateAction, null);
                 yield return new WaitForSeconds(0.5f);
            }

            _vsmInstallingFixtureIds.AddRange(plan.MainFixturesToInstall.Select(info => info.FixtureId));
            foreach (var installInfo in plan.MainFixturesToInstall)
            {
                var actionType = installInfo.UseAnimation ? ActionType.PlayFixtureAnimationAction : ActionType.PlaceFixtureWithoutAnimation;
                var args = installInfo.UseAnimation
                    ? (BaseActionArgs)new PlayFixtureAnimationArgs(installInfo.FixtureId, AnimationDirection.In, ActionRequester.VSM)
                    : new PlaceFixtureArgs(installInfo.FixtureId, null, null);
                ToDoManager.Instance.HandleAction(actionType, args);
                yield return new WaitForSeconds(0.05f);
            }
        }

        yield return new WaitUntil(() => _vsmInstallingFixtureIds.Count == 0);
        if (plan.MainFixturesToInstall.Count > 0) { if (enableVerboseLogging) Debug.Log("<color=lightblue>[VSM] Установка ОСНОВНОЙ оснастки завершена.</color>"); }

        // --- ВЫПОЛНЕНИЕ ПРОМЕЖУТОЧНЫХ КОМАНД ---
        if (plan.InterstitialCommands != null && plan.InterstitialCommands.Count > 0)
        {
            if (enableVerboseLogging) Debug.Log($"<color=yellow>[VSM] Выполнение {plan.InterstitialCommands.Count} промежуточных команд...</color>");
            foreach (var command in plan.InterstitialCommands) { ToDoManager.Instance.HandleAction(command.Action, command.Args); }
            yield return null;
        }

        // 4. УСТАНОВКА ВЛОЖЕННОЙ ОСНАСТКИ
        if (plan.InternalFixturesToInstall.Count > 0)
        {
            if (enableVerboseLogging) Debug.Log($"<color=lime>[VSM] План на установку ВЛОЖЕННОЙ: [{string.Join(", ", plan.InternalFixturesToInstall.Select(info => info.FixtureId))}]</color>");
            _vsmInstallingFixtureIds.AddRange(plan.InternalFixturesToInstall.Select(info => info.FixtureId));
            foreach (var internalInstallInfo in plan.InternalFixturesToInstall)
            {
                ToDoManager.Instance.HandleAction(ActionType.PlayFixtureAnimationAction, new PlayFixtureAnimationArgs(internalInstallInfo.FixtureId, AnimationDirection.In, ActionRequester.VSM));
                yield return new WaitForSeconds(0.05f);
            }
            yield return new WaitUntil(() => _vsmInstallingFixtureIds.Count == 0);
            if (enableVerboseLogging) Debug.Log("<color=lime>[VSM] Установка ВЛОЖЕННОЙ оснастки завершена.</color>");
        }

        // 5. ЗАКРЫТИЕ ДВЕРЕЙ И ФИНАЛИЗАЦИЯ
        bool anyStandardActionTaken = plan.MainFixturesToRemove.Count > 0 ||
                                      plan.MainFixturesToInstall.Count > 0 ||
                                      plan.InternalFixturesToInstall.Count > 0;
        
        if (anyStandardActionTaken)
        {
            // Закрываем двери в самом конце (безопасность)
            if (!SystemStateMonitor.Instance.AreDoorsClosed)
            {
                 ToDoManager.Instance?.HandleAction(ActionType.SetDoorStateAction, null);
            }
        }

        var finalizationCommands = specificHandler.GetPostChangeFinalizationCommands();
        if (finalizationCommands != null && finalizationCommands.Count > 0)
        {
            foreach (var command in finalizationCommands) { ToDoManager.Instance.HandleAction(command.Action, command.Args); }
            yield return new WaitForSeconds(0.5f);
        }

        // --- ОБНОВЛЕНИЕ UI (без изменений) ---
        // ... (весь блок подсветки остается как был) ...
        
        // --- ЗАВЕРШЕНИЕ ---
        ToDoManager.Instance.HandleAction(ActionType.SetCurrentLogicHandler, null);
        Debug.Log("<color=green>[VSM Special Workflow] Workflow полностью завершен.</color>");
        isChangingFixture = false;
        SystemStateMonitor.Instance?.ReportFixtureChangeStatus(false);
        fictiveParamsSource.ResetMonitor();
    }*/

    #endregion
    #region Управление состоянием VSM и визуализацией

    // Обновление видимости кожухов
    private void UpdateCasingsVisibility(bool visible)
    {
        if (protectiveCasings == null) return;
        foreach (var casing in protectiveCasings)
            if (casing != null) casing.SetActive(visible);
    }

    // Метод для централизованной отправки команды на обновление PromptController
    private void SendPromptUpdateCommand(string keyOrId, PromptSourceType sourceType, string sourceInfo, bool isNewTarget)
    {
        if (ToDoManager.Instance != null)
        {
            var promptArgs = new UpdatePromptArgs(
                keyOrId,
                sourceType,
                sourceInfo,
                isNewTarget
            );
            ToDoManager.Instance.HandleAction(ActionType.UpdatePromptDisplay, promptArgs);
        }
        else
        {
            Debug.LogError($"[VSM] ToDoManager.Instance не найден! Не могу отправить Prompt Update Command для ключа '{keyOrId}'.");
        }
    }
    
    /// Централизованно запрашивает смену фокуса камеры и устанавливает связанный контекст (например, видимость кожухов).
    private void RequestCameraFocusAndSetContext(CameraFocusTarget target)
    {
        bool isInternalView = (target == CameraFocusTarget.Frame || target == CameraFocusTarget.Hydraulics);

        if (isInternalView)
        {
            // Если вид внутренний и кожухи видимы, скрываем их и запоминаем это.
            if (areCasingsVisible)
            {
                areCasingsVisible = false;
                UpdateCasingsVisibility(areCasingsVisible);
                casingsHiddenAutomatically = true; // Запоминаем, что мы это сделали
                if (enableVerboseLogging) Debug.Log("[VSM] Кожухи автоматически скрыты.");
            }
        }
        else // Если вид стал внешним
        {
            // Если кожухи были скрыты НАШЕЙ автоматикой, возвращаем их.
            if (casingsHiddenAutomatically)
            {
                areCasingsVisible = true;
                UpdateCasingsVisibility(areCasingsVisible);
                casingsHiddenAutomatically = false; // Сбрасываем флаг, работа сделана
                if (enableVerboseLogging) Debug.Log("[VSM] Видимость кожухов восстановлена.");
            }
        }
        
        // Отправляем событие для смены камеры в любом случае.
        EventManager.Instance?.RaiseEvent(EventType.FocusCameraRequested, new FocusCameraEventArgs(this, target));
    }

#endregion
}