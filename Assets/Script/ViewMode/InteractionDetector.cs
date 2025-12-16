using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.InputSystem;

/// Отвечает за обнаружение наведения курсора и кликов мыши на объектах
public class InteractionDetector : MonoBehaviour
{
    [Header("Зависимости")]
    [Tooltip("Основная камера для Raycast. Если не задана, попытается найти Camera.main.")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private ContextMenuManager contextMenuManager;

    [Header("Настройки")]
    [Tooltip("Максимальная дистанция для Raycast.")]
    [SerializeField] private float maxRaycastDistance = 100f;
    [Tooltip("Небольшая задержка (в секундах) перед проверкой объекта под курсором после завершения анимации промпта.")]
    [SerializeField] private float hoverCheckDelayAfterPromptAnim = 0.05f;

    // Внутреннее состояние
    private GameObject lastHoveredObject = null;
    private bool isCameraRotating = false;
    private Coroutine deferredHoverCheckCoroutine = null;

    private bool isPromptBlockingInteractions = false;
    private bool isDropdownMenuActive = false;
    private bool contextMenuActionRequested = false;

    private PlayerControls playerControls;
    private Vector2 lastContextMenuPosition;
    private bool primaryActionTriggered = false;
    private InputDevice lastUsedDevice = null;
    private Vector2 lastScreenPosition;
    private bool mouseMovedThisFrame = false;
    private Coroutine holdCheckCoroutine;
    private int holdPointerId;

    private void Awake()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("[InteractionDetector] Не найдена основная камера (Camera.main)! Детектор не будет работать.", this);
                enabled = false;
                return;
            }
        }
        playerControls = new PlayerControls();
    }

    private void OnEnable()
    {
        playerControls?.UI.Enable();
        playerControls?.Camera.Enable();
        SubscribeToEvents();
        playerControls.Camera.ContextMenu.performed += OnContextMenuPerformed;
    }

    private void OnDisable()
    {
        playerControls.Camera.ContextMenu.performed -= OnContextMenuPerformed;
        playerControls?.UI.Disable();
        playerControls?.Camera.Disable();
        UnsubscribeFromEvents();
        if (deferredHoverCheckCoroutine != null)
        {
            StopCoroutine(deferredHoverCheckCoroutine);
            deferredHoverCheckCoroutine = null;
        }
    }

    void Update()
    {
        // Обрабатываем наведение только если мышь двигалась в этом кадре
        if (mouseMovedThisFrame)
        {
            HandleHoverDetection();
            mouseMovedThisFrame = false; // Сбрасываем флаг, чтобы не вызывать постоянно
        }

        // Сначала обрабатываем наш отложенный клик/тап, если он был
        if (primaryActionTriggered)
        {
            if (holdCheckCoroutine != null)
            {
                StopCoroutine(holdCheckCoroutine);
                holdCheckCoroutine = null;
            }
            // СРАЗУ СБРАСЫВАЕМ, чтобы не сработал дважды
            primaryActionTriggered = false;

            if (!isCameraRotating && !isDropdownMenuActive)
            {
                if (!EventSystem.current.IsPointerOverGameObject())
                {
                    // Если дошли сюда, действие было по 3D-сцене.
                    if (lastUsedDevice is Mouse)
                    {
                        // Для мыши PerformRaycast сам возьмет актуальную позицию,
                        // но для единообразия можно использовать и сохраненную.
                        GameObject clickedObject = PerformRaycast();
                        ProcessClickedObject(clickedObject, InteractionType.Click);
                    }
                    else if (lastUsedDevice is Touchscreen)
                    {
                        // Используем сохраненную позицию
                        GameObject tappedObject = PerformRaycastAtScreenPoint(lastScreenPosition);
                        if (tappedObject == null) ClearHoverStateIfNeeded();
                        ProcessClickedObject(tappedObject, InteractionType.Click);
                    }
                }
            }
        }

        if (contextMenuActionRequested)
        {
            // Немедленно сбрасываем флаг, чтобы он сработал только один раз
            contextMenuActionRequested = false;

            // Выполняем проверку на UI. Если клик по UI, ничего не делаем.
            if (EventSystem.current.IsPointerOverGameObject())
            {
                return; // Выходим, чтобы не мешать UI
            }

            // Этот клик точно предназначен для сцены. Успокаиваем камеру и себя.
            CameraController.Instance?.CancelRotationState();
            isCameraRotating = false;

            // 1. Создаем список ключей. По умолчанию он ПУСТОЙ.
            List<string> keysToShow = new List<string>();

            // 2. Пытаемся найти объект и заполнить список
            GameObject targetObject = PerformRaycastAtScreenPoint(lastContextMenuPosition);
            if (targetObject != null)
            {
                SendHoverInfo(targetObject, true);
                InteractableInfo info = targetObject.GetComponent<InteractableInfo>();
                if (info != null && info.contextMenuKeys.Count > 0)
                {
                    // Если нашли ключи, используем их
                    keysToShow = info.contextMenuKeys;
                }
            }

            // 3. Отправляем событие ВСЕГДА.
            // Список будет либо содержать ключи, либо будет пустым.
            EventManager.Instance?.RaiseEvent(EventType.ContextMenuRequested, new ContextMenuRequestedEventArgs(this, keysToShow, lastContextMenuPosition));
        }

        if (mainCamera == null || playerControls == null) return;

        if (EventSystem.current.IsPointerOverGameObject())
        {
            if (contextMenuManager == null || !contextMenuManager.IsOpen) ClearHoverStateIfNeeded();
            return;
        }
    }

    // Логика Raycast для мыши
    private GameObject PerformRaycast()
    {
        Vector2 mousePosition = playerControls.UI.Point.ReadValue<Vector2>();
        Ray ray = mainCamera.ScreenPointToRay(mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hitInfo, maxRaycastDistance))
        {
            return hitInfo.collider.gameObject;
        }
        return null;
    }

    // Общий метод для Raycast по координатам экрана (ТОЛЬКО ДЛЯ ТАЧА)
    private GameObject PerformRaycastAtScreenPoint(Vector2 screenPosition)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit hitInfo, maxRaycastDistance))
        {
            return hitInfo.collider.gameObject;
        }
        return null;
    }

    // Логика отправки события наведения/очистки
    private void SendHoverInfo(GameObject targetObject, bool isForceCheck = false)
    {
        if (targetObject != null)
        {
            InteractableInfo info = targetObject.GetComponent<InteractableInfo>();
            if (info != null)
            {
                if (targetObject != lastHoveredObject || isForceCheck)
                {
                    bool isNewTargetForPrompt = (targetObject != lastHoveredObject);
                    lastHoveredObject = targetObject;
                    EventManager.Instance?.RaiseEvent(EventType.ShowInteractableInfo,
                        new ShowInteractableInfoEventArgs(this, info.Identifier, targetObject, info.SystemPromptKey, isNewTargetForPrompt));
                }
            }
            else
            {
                ClearHoverStateIfNeeded();
            }
        }
        else
        {
            ClearHoverStateIfNeeded();
        }
    }

    private void HandleHoverDetection()
    {
        if (EventSystem.current.IsPointerOverGameObject())
        {
            ClearHoverStateIfNeeded();
            return;
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.isInProgress) return;

        GameObject currentObjectUnderCursor = PerformRaycast(); // Использует "мышиный" PerformRaycast

        bool isCoreHoverBlocked = isCameraRotating || isDropdownMenuActive;
        if (isCoreHoverBlocked)
        {
            ClearHoverStateIfNeeded();
            return;
        }
        SendHoverInfo(currentObjectUnderCursor, false);
    }

    // Общий метод для обработки кликнутого/тапнутого объекта.
    private void ProcessClickedObject(GameObject targetObject, InteractionType interactionType)
    {
        if (targetObject == null) return;

        InteractableInfo interactableInfo = targetObject.GetComponent<InteractableInfo>();
        if (interactableInfo == null) return;

        string clickedID = interactableInfo.Identifier;
        string systemPromptKey = interactableInfo.SystemPromptKey;
        EventManager.Instance?.RaiseEvent(EventType.ClickedInteractableInfo,
            new ClickedInteractableInfoEventArgs(this, clickedID, targetObject, systemPromptKey, interactionType, true));
        SendHoverInfo(targetObject, true);
    }

    // Только снимает подсветку
    private void ClearHoverStateIfNeeded()
    {
        if (lastHoveredObject != null)
        {
            lastHoveredObject = null;
            if (ToDoManager.Instance != null)
            {
                ToDoManager.Instance.HandleAction(ActionType.UpdateHighlight, new UpdateHighlightArgs(true));
            }
        }
    }

    // Обработчики событий
    private void HandleRotationStarted(EventArgs args)
    {
        isCameraRotating = true;
        ClearHoverStateIfNeeded();
    }

    private void HandleRotationStopped(EventArgs args)
    {
         if (Touchscreen.current != null && Touchscreen.current.wasUpdatedThisFrame)
        {
            isCameraRotating = false;
            return;
        }
        isCameraRotating = false;
        if (!isCameraRotating && !isDropdownMenuActive && enabled) ForceHoverCheck();
    }

    private void SubscribeToEvents()
    {
        if (EventManager.Instance == null) { Debug.LogError("[InteractionDetector] EventManager не найден!", this); return; }
        EventManager.Instance.Subscribe(EventType.CameraRotationStarted, this, HandleRotationStarted);
        EventManager.Instance.Subscribe(EventType.CameraRotationStopped, this, HandleRotationStopped);
        EventManager.Instance.Subscribe(EventType.PromptInteractionBlockStarted, this, HandlePromptInteractionBlockStarted);
        EventManager.Instance.Subscribe(EventType.PromptInteractionBlockFinished, this, HandlePromptInteractionBlockFinished);
        playerControls.UI.PointerMove.performed += OnPointerMove;
        playerControls.UI.Click.performed += OnPrimaryAction;
        playerControls.Camera.PrimaryTouchContact.started += OnHoldStarted;
        playerControls.Camera.SecondaryTouchContact.started += OnMultiFingerGestureStarted;
        playerControls.Camera.TertiaryTouchContact.started += OnMultiFingerGestureStarted;
        if (SystemStateMonitor.Instance != null) SystemStateMonitor.Instance.OnDropdownMenuActivityChanged += HandleDropdownMenuStateChanged;
    }

    private void UnsubscribeFromEvents()
    {
        if (EventManager.Instance != null)
        {
            EventManager.Instance.Unsubscribe(EventType.CameraRotationStarted, this, HandleRotationStarted);
            EventManager.Instance.Unsubscribe(EventType.CameraRotationStopped, this, HandleRotationStopped);
            EventManager.Instance.Unsubscribe(EventType.PromptInteractionBlockStarted, this, HandlePromptInteractionBlockStarted);
            EventManager.Instance.Unsubscribe(EventType.PromptInteractionBlockFinished, this, HandlePromptInteractionBlockFinished);
            playerControls.UI.PointerMove.performed -= OnPointerMove;
            playerControls.UI.Click.performed -= OnPrimaryAction;
            playerControls.Camera.PrimaryTouchContact.started -= OnHoldStarted;
            playerControls.Camera.SecondaryTouchContact.started -= OnMultiFingerGestureStarted;
            playerControls.Camera.TertiaryTouchContact.started -= OnMultiFingerGestureStarted;
            if (SystemStateMonitor.Instance != null) SystemStateMonitor.Instance.OnDropdownMenuActivityChanged -= HandleDropdownMenuStateChanged;
        }
    }

    private void OnPointerMove(InputAction.CallbackContext context)
    {
        // Взводим флаг движения мыши. Определение события происходит в Update
        mouseMovedThisFrame = true;
    }

    private void OnPrimaryAction(InputAction.CallbackContext context)
    {
        // Не сохраняем context! Извлекаем все, что нужно, ЗДЕСЬ И СЕЙЧАС.
        lastUsedDevice = context.control.device;

        // Получаем позицию универсальным способом, который работает и для мыши, и для тача
        lastScreenPosition = UnityEngine.InputSystem.Pointer.current.position.ReadValue();

        // Выставляем флаг в самом конце, чтобы все данные были готовы
        primaryActionTriggered = true;
    }

    // Обработчики событий блокировки
    private void HandlePromptInteractionBlockStarted(EventArgs args)
    {
        isPromptBlockingInteractions = true;
        //ClearHoverStateIfNeeded();
    }

    private void HandlePromptInteractionBlockFinished(EventArgs args)
    {
        isPromptBlockingInteractions = false;
        if (deferredHoverCheckCoroutine != null) StopCoroutine(deferredHoverCheckCoroutine);
        if (!isCameraRotating && !isDropdownMenuActive && enabled) deferredHoverCheckCoroutine = StartCoroutine(DeferredHoverCheck());
    }

    private void HandleDropdownMenuStateChanged(bool isMenuActive)
    {
        if (isMenuActive)
        {
            ClearHoverStateIfNeeded();
        }
        else
        {
            if (!isCameraRotating && !isPromptBlockingInteractions && enabled)
            {
                if (deferredHoverCheckCoroutine != null) StopCoroutine(deferredHoverCheckCoroutine);
                deferredHoverCheckCoroutine = StartCoroutine(DeferredHoverCheck());
            }
        }
    }

    private IEnumerator DeferredHoverCheck()
    {
        yield return new WaitForSeconds(hoverCheckDelayAfterPromptAnim);
        if (!isCameraRotating && !isDropdownMenuActive && enabled) ForceHoverCheck();
        deferredHoverCheckCoroutine = null;
    }

    public void ForceHoverCheck()
    {
        if (EventSystem.current.IsPointerOverGameObject())
        {
            ClearHoverStateIfNeeded(); // Снимаем подсветку, если курсор над UI
            return;
        }

        // Блокировка только если активно меню Dropdown.
        if (isDropdownMenuActive)
        {
            Debug.Log("[InteractionDetector] ForceHoverCheck aborted: Dropdown is currently active.");
            return;
        }

        GameObject currentObjectUnderCursor = PerformRaycast();

        lastHoveredObject = null;
        SendHoverInfo(currentObjectUnderCursor, true);
    }

    private void OnContextMenuPerformed(InputAction.CallbackContext context)
    {
        if (context.control.device is Touchscreen && Touchscreen.current.touches.Count > 1) return;

        Vector2 position;
        if (context.control.device is Mouse)
        {
            // Если это мышь, берем ее текущую позицию
            position = Mouse.current.position.ReadValue();
        }
        else if (context.control.device is Touchscreen)
        {
            // Если это тачскрин, берем позицию основного касания
            position = Touchscreen.current.primaryTouch.position.ReadValue();
        }
        else
        {
            // Неизвестное устройство, выходим
            return;
        }

        // Сохраняем правильную позицию и выставляем флаг
        lastContextMenuPosition = position;
        contextMenuActionRequested = true;
    }

    private void OnHoldStarted(InputAction.CallbackContext context)
    {
        if (holdCheckCoroutine != null)
        {
            StopCoroutine(holdCheckCoroutine);
        }
        // Получаем ID касания. Для primaryTouch это всегда 0.
        holdPointerId = Touchscreen.current.primaryTouch.touchId.ReadValue();

        // Просто запускаем корутину, проверку убираем отсюда
        holdCheckCoroutine = StartCoroutine(HoldCheckRoutine());
    }

    private IEnumerator HoldCheckRoutine()
    {
        // Твое гениальное решение: ждем один кадр, чтобы EventSystem обновился
        yield return null;

        // Теперь делаем правильную проверку, указывая ID пальца
        if (EventSystem.current.IsPointerOverGameObject(holdPointerId))
        {
            holdCheckCoroutine = null;
            yield break; // Если мы над UI, просто выходим из корутины
        }

        /*if (playerControls.Camera.SecondaryTouchContact.IsPressed() || playerControls.Camera.TertiaryTouchContact.IsPressed())
        {
            holdCheckCoroutine = null;
            yield break; // Выходим, потому что начался другой жест
        }*/

        // Если проверка пройдена (мы не над UI), продолжаем как раньше
        yield return new WaitForSeconds(0.7f);

        lastContextMenuPosition = UnityEngine.InputSystem.Pointer.current.position.ReadValue();
        contextMenuActionRequested = true;
        holdCheckCoroutine = null;
    }
    
    private void OnMultiFingerGestureStarted(InputAction.CallbackContext context)
    {
        // Если начался жест несколькими пальцами, это точно не долгое нажатие.
        // Немедленно убиваем таймер, если он был запущен.
        if (holdCheckCoroutine != null)
        {
            StopCoroutine(holdCheckCoroutine);
            holdCheckCoroutine = null;
        }
    }

}