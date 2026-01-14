using UnityEngine;
using UnityEngine.EventSystems;
using Cinemachine;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;

public class FocusCameraEventArgs : EventArgs { public CameraFocusTarget Target { get; private set; } public FocusCameraEventArgs(object sender, CameraFocusTarget target) : base(sender) { Target = target; } }

public class CameraController : MonoBehaviour
{
    #region Singleton
    private static CameraController _instance;
    public static CameraController Instance
    {
        get
        {
            if (_instance == null) _instance = FindFirstObjectByType<CameraController>();
            return _instance;
        }
    }
    #endregion

    [Header("Cinemachine FreeLook Cameras")]
    [SerializeField] private CinemachineFreeLook vcamFrame;
    [SerializeField] private CinemachineFreeLook vcamFixture;
    [SerializeField] private CinemachineFreeLook vcamHydraulics;
    [SerializeField] private CinemachineFreeLook vcamMeasurement;
    [SerializeField] private CinemachineFreeLook vcamOverview;

    [Header("Camera Control Settings")]
    [SerializeField] private int highPriority = 15;
    [SerializeField] private int lowPriority = 10;

    [Header("Rotation (RMB + Mouse Move)")]
    [Tooltip("Чувствительность горизонтального вращения. Возможно, потребует подстройки под New Input System.")]
    [SerializeField] private float rotationSensitivityX = 0.1f;
    [SerializeField] private float rotationSensitivityY = 0.1f;

    [Header("Zoom (Mouse Wheel)")]
    [Tooltip("Чувствительность зума колесом мыши.")]
    [SerializeField] private float zoomSensitivity = 0.01f;
    [Tooltip("Минимальное расстояние до объекта (радиус орбит)")]
    [SerializeField] private float minZoomDistance = 1.0f;
    [Tooltip("Максимальное расстояние до объекта (радиус орбит)")]
    [SerializeField] private float maxZoomDistance = 20.0f;

    [Header("Panning (MMB + Mouse Move)")]
    [Tooltip("Чувствительность панорамирования. Возможно, потребует подстройки.")]
    [SerializeField] private float panSensitivity = 0.05f;

    [Header("Touch Control Settings")]
    [Header("Touch Control Settings")] // Если этого заголовка еще нет над этими полями
    [Tooltip("Чувствительность ГОРИЗОНТАЛЬНОГО вращения камеры двумя пальцами (влево/вправо).")]
    [SerializeField] private float touchRotationSensitivityX = 0.25f;
    [Tooltip("Чувствительность ВЕРТИКАЛЬНОГО вращения камеры двумя пальцами (вверх/вниз).")]
    [SerializeField] private float touchRotationSensitivityY = 0.04f;
    [Tooltip("Чувствительность зума камеры (pinch).")]
    [SerializeField] private float touchZoomSensitivity = 0.005f;
    [Tooltip("Чувствительность панорамирования камеры тремя пальцами.")]
    [SerializeField] private float touchPanSensitivity = 0.03f;
    [Tooltip("Минимальное изменение (в пикселях) для начала жеста перетаскивания/вращения/панорамирования.")]
    [SerializeField] private float touchDragThreshold = 10f; // Порог для начала жеста

    // Состояние для отслеживания активных жестов
    private bool _isOneFingerTouchActiveForCamera = false; // Если решим использовать один палец для камеры
    private bool _isTwoFingerGestureActive = false;
    private bool _isThreeFingerGestureActive = false;

    // Начальные значения для жестов
    private Vector2 _initialTouch0ScreenPos, _initialTouch1ScreenPos, _initialTouch2ScreenPos;
    private float _initialPinchDistance;
    private Vector2 _initialTwoFingerMidpointScreen;
    private Vector2 _initialThreeFingerMidpointScreen;

    // Флаги для отслеживания, что жест действительно начался (превышен порог)
    // и для отслеживания событий начала/конца вращения от тача
    private bool _twoFingerDragStarted = false;
    private bool _threeFingerDragStarted = false;
    private bool _wasRotatingByTouchLastFrame = false;
    private bool isRotationHeld = false;

    [Header("ContextMenu")]
    [SerializeField] private ContextMenuManager contextMenuManager;

    // --- Внутреннее состояние ---
    private List<CinemachineFreeLook> allVcams;
    private Dictionary<CameraFocusTarget, CinemachineFreeLook> vcamMap;
    private CinemachineFreeLook activeVcam;

    // Словарь для хранения начальных радиусов орбит каждой камеры для сброса зума
    private Dictionary<CinemachineFreeLook, float[]> defaultOrbitRadii;
    // Словарь для хранения начальных позиций Follow/LookAt таргетов для сброса панорамирования
    private Dictionary<CinemachineFreeLook, Vector3> initialTargetPositions;

    // --- New Input System ---
    private PlayerControls playerControls; // Экземпляр нашего сгенерированного класса

    // Флаг для отслеживания состояния вращения в предыдущем кадре
    private bool _wasRotatingLastFrame = false;


    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;

        playerControls = new PlayerControls();

        InitializeVcamListAndMap();

        if (allVcams.Count == 0)
        {
            Debug.LogError("[CameraController] Не найдены или не назначены CinemachineFreeLook камеры!");
            enabled = false;
            return;
        }
    }

    private void OnEnable()
    {
        playerControls?.Camera.Enable();
        if (playerControls != null)
        {
            //playerControls.Camera.PrimaryTouchContact.performed += HandlePrimaryTap;
            playerControls.Camera.RotationModifier.performed += _ => isRotationHeld = true;
            playerControls.Camera.RotationModifier.canceled += _ => isRotationHeld = false;
        }
    }

    private void OnDisable()
    {
        playerControls?.Camera.Disable();
        if (playerControls != null)
        {
            //playerControls.Camera.PrimaryTouchContact.performed -= HandlePrimaryTap;
            playerControls.Camera.RotationModifier.performed -= _ => isRotationHeld = true;
            playerControls.Camera.RotationModifier.canceled -= _ => isRotationHeld = false;
        }
    }
    // -----------------------------------------

    private void InitializeVcamListAndMap()
    {
        allVcams = new List<CinemachineFreeLook>();
        vcamMap = new Dictionary<CameraFocusTarget, CinemachineFreeLook>();
        // Инициализируем словари для хранения дефолтных значений
        defaultOrbitRadii = new Dictionary<CinemachineFreeLook, float[]>();
        initialTargetPositions = new Dictionary<CinemachineFreeLook, Vector3>(); // Инициализируем словарь для позиций таргетов

        Action<CameraFocusTarget, CinemachineFreeLook> addVcam = (target, vcam) =>
        {
            if (vcam != null)
            {
                allVcams.Add(vcam);
                vcamMap[target] = vcam;
                // Напоминание об очистке осей в инспекторе (если управляем из скрипта)
                if (!string.IsNullOrEmpty(vcam.m_XAxis.m_InputAxisName) /* || !string.IsNullOrEmpty(vcam.m_YAxis.m_InputAxisName) */ ) // Проверяем только X, т.к. Y лучше через инспектор
                {
                    Debug.LogWarning($"[CameraController] У камеры '{vcam.name}' ЗАПОЛНЕНО поле Input Axis Name для X Axis. ОЧИСТИТЕ ЕГО для управления из скрипта!", vcam);
                }
                // Рекомендуется настроить Y Axis в инспекторе: Input Axis Name = "Mouse Y", Value Range ~ [0.38, 0.62]

                // Сохраняем начальные радиусы орбит
                float[] initialRadii = new float[3];
                for (int i = 0; i < 3; i++)
                {
                    initialRadii[i] = vcam.m_Orbits[i].m_Radius;
                }
                defaultOrbitRadii[vcam] = initialRadii;

                // Сохраняем начальную позицию общего таргета (Follow/LookAt)
                Transform commonTarget = vcam.Follow; // Предполагаем, что Follow и LookAt - один и тот же объект
                if (commonTarget == null) 
                {
                    commonTarget = vcam.LookAt;
                }

                if (commonTarget != null)
                {
                    // Если цель есть (например, Overview камера уже смотрит на точку в сцене), запоминаем позицию
                    initialTargetPositions[vcam] = commonTarget.position;
                }
                else { }
            }
        };
        addVcam(CameraFocusTarget.Frame, vcamFrame);
        addVcam(CameraFocusTarget.Fixture, vcamFixture);
        addVcam(CameraFocusTarget.Hydraulics, vcamHydraulics);
        addVcam(CameraFocusTarget.Measurement, vcamMeasurement);
        addVcam(CameraFocusTarget.Overview, vcamOverview);
    }

    void Start()
    {
        // Управление курсором 
        SubscribeToEvents();
        /*if (vcamMap.ContainsKey(CameraFocusTarget.Overview))
            SwitchToCamera(CameraFocusTarget.Overview);
        else if (allVcams.Count > 0)
            SwitchToCamera(vcamMap.First().Key);
            */
    }

    /// <summary>
    /// Настраивает камеры на новую машину.
    /// </summary>
    public void Initialize(MachineVisualData visualData)
    {
        if (visualData == null) return;

        // 1. Настраиваем Overview (Глобальную)
        Transform overviewTarget = visualData.GlobalOverviewFocusPoint;
        UpdateVcamTarget(vcamOverview, overviewTarget);

        // 2. Настраиваем остальные по категориям
        // Frame
        UpdateVcamTarget(vcamFrame, visualData.GetFocusPointSafe(MachineVisualCategory.Frame));
        
        // Fixture
        UpdateVcamTarget(vcamFixture, visualData.GetFocusPointSafe(MachineVisualCategory.Fixture));
        
        // Hydraulics
        // Если категории нет, GetFocusPointSafe вернет Overview или Раму (безопасно)
        UpdateVcamTarget(vcamHydraulics, visualData.GetFocusPointSafe(MachineVisualCategory.Hydraulics));
        
        // Measurement
        UpdateVcamTarget(vcamMeasurement, visualData.GetFocusPointSafe(MachineVisualCategory.Measurement));

        // Сбрасываем позицию камеры в Overview
        SwitchToCamera(CameraFocusTarget.Overview);
    }

    private void UpdateVcamTarget(CinemachineFreeLook vcam, Transform target)
    {
        if (vcam != null && target != null)
        {
            vcam.Follow = target;
            vcam.LookAt = target;
            
            // Обновляем "домашнюю" позицию для сброса (из словаря)
            if (initialTargetPositions.ContainsKey(vcam))
            {
                initialTargetPositions[vcam] = target.position;
            }
            else
            {
                initialTargetPositions.Add(vcam, target.position);
            }
        }
    }    

    void Update()
    {
        bool isMouseOverUI = EventSystem.current.IsPointerOverGameObject(); // Для мыши

        // --- ЛОГИКА ДЛЯ ТАЧ-ВВОДА ---
        bool touchHandled = HandleTouchInput(); // Новый метод для обработки тач-ввода

        // --- ЛОГИКА ДЛЯ МЫШИ (выполняется, если тач не активен для камеры) ---
        if (!touchHandled) // Если тач не управлял камерой, проверяем мышь
        {
            // Определяем, может ли камера вращаться в данный момент (ПКМ нажата, не над UI, есть активная vcam)
            bool couldBeRotatingByMouse = !isMouseOverUI && activeVcam != null && isRotationHeld;

            // Проверяем изменение состояния вращения для генерации событий
            if (couldBeRotatingByMouse && !_wasRotatingLastFrame)
            {
                EventManager.Instance?.RaiseEvent(EventType.CameraRotationStarted, new EventArgs(this));
                _wasRotatingLastFrame = true;
            }
            else if (!couldBeRotatingByMouse && _wasRotatingLastFrame)
            {
                EventManager.Instance?.RaiseEvent(EventType.CameraRotationStopped, new EventArgs(this));
                _wasRotatingLastFrame = false;
            }

            // Прерываем дальнейшую обработку ввода камеры, если курсор над UI
            if (isMouseOverUI)
            {
                // Если мышь была над UI, но вращение было активно (например, началось вне UI и курсор зашел на UI)
                // нужно остановить событие вращения
                if (_wasRotatingLastFrame)
                {
                    EventManager.Instance?.RaiseEvent(EventType.CameraRotationStopped, new EventArgs(this));
                    _wasRotatingLastFrame = false;
                }
                return;
            }

            if (activeVcam == null) return;

            HandleFreeLookZoom();   // Зум мышью
            HandleFreeLookRotation(); // Вращение мышью
            HandleFreeLookPan();      // Панорамирование мышью
        }
        else // Если тач обработал ввод, то сбрасываем флаги вращения мышью
        {
            if (_wasRotatingLastFrame) // Если мышь вращала, но теперь тач взял управление
            {
                EventManager.Instance?.RaiseEvent(EventType.CameraRotationStopped, new EventArgs(this));
                _wasRotatingLastFrame = false;
            }
        }
    }

    private bool HandleTouchInput()
    {
        if (activeVcam == null) return false; // Нет активной камеры для управления

        int activeTouchCount = 0;
        // Используем IsPressed для определения количества активных касаний для жестов
        // PrimaryTouchContact.IsPressed() будет true, когда палец на экране, даже если Tap Interaction еще не завершился
        bool primaryFingerDown = playerControls.Camera.PrimaryTouchContact.IsPressed();
        bool secondaryFingerDown = playerControls.Camera.SecondaryTouchContact.IsPressed();
        bool tertiaryFingerDown = playerControls.Camera.TertiaryTouchContact.IsPressed();

        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            // Определяем количество активных пальцев на экране для жестов
            if (tertiaryFingerDown && Touchscreen.current.touches.Count >= 3) activeTouchCount = 3;
            else if (secondaryFingerDown && Touchscreen.current.touches.Count >= 2) activeTouchCount = 2;
            else if (primaryFingerDown && Touchscreen.current.touches.Count >= 1) activeTouchCount = 1;
        }

        // Проверка, находится ли какой-либо из АКТИВНЫХ пальцев (которые мы будем использовать для жестов) над UI
        bool isAnyActiveTouchOverUI = false;
        if (activeTouchCount > 0 && Touchscreen.current != null) // Touchscreen.current.touches.Count уже проверен выше
        {
            for (int i = 0; i < Mathf.Min(activeTouchCount, Touchscreen.current.touches.Count); i++)
            {
                if (EventSystem.current.IsPointerOverGameObject(Touchscreen.current.touches[i].touchId.ReadValue()))
                {
                    isAnyActiveTouchOverUI = true;
                    break;
                }
            }
        }

        // Если активный жест камеры (который мог бы начаться) перекрывает UI, сбрасываем состояния жестов и не обрабатываем их как управление камерой.
        // Это важно, чтобы не начинать жесты камеры, если пальцы изначально над UI.
        if (isAnyActiveTouchOverUI && ((activeTouchCount >= 1 && _isOneFingerTouchActiveForCamera) || (activeTouchCount >= 2 && _isTwoFingerGestureActive) || (activeTouchCount >= 3 && _isThreeFingerGestureActive)))
        {
            ResetTouchGestureStates();
            return false; // Жест над UI, камера не управляется тачем
        }
        // Если просто пальцы над UI, но жесты камеры еще не активны, мы не должны возвращать true из HandleTouchInput
        // чтобы позволить UI обработать эти касания. Камера не должна их "перехватывать".
        if (isAnyActiveTouchOverUI && activeTouchCount > 0 && !(_isOneFingerTouchActiveForCamera || _isTwoFingerGestureActive || _isThreeFingerGestureActive))
        {
            // Пальцы над UI, но жесты камеры не активны. Не мешаем UI.
            // Если был активный жест в прошлом кадре, но теперь пальцы над UI - сбрасываем.
            if (_wasRotatingByTouchLastFrame) ResetTouchGestureStates();
            return false;
        }


        bool touchIsControllingCamera = false;

        // --- Обработка Трех Пальцев (Панорамирование) ---
        if (activeTouchCount >= 3 && tertiaryFingerDown && !isAnyActiveTouchOverUI)
        {
            touchIsControllingCamera = true; // Тач управляет камерой

            // 1. Читаем готовое смещение (delta). Оно уже ускорено в 2 раза в PlayerControls.
            Vector2 panDelta = playerControls.Camera.TertiaryTouchPosition.ReadValue<Vector2>();

            // 2. Проверяем, начался ли жест (превышен порог)
            if (!_isThreeFingerGestureActive) // Начало жеста
            {
                _isThreeFingerGestureActive = true;
                _threeFingerDragStarted = false; // Сбрасываем флаг начала движения
                HandleTouchRotationEventStart();
            }
            
            // Если порог еще не превышен, проверяем его
            if (!_threeFingerDragStarted && panDelta.magnitude > touchDragThreshold)
            {
                _threeFingerDragStarted = true;
            }

            // 3. Если жест активен, применяем панорамирование
            if (_threeFingerDragStarted && activeVcam.Follow != null && Camera.main != null)
            {
                // Используем panDelta напрямую.
                // touchPanSensitivity остается для дополнительной подстройки скорости в инспекторе.
                Vector3 move = (Camera.main.transform.right * -panDelta.x + Camera.main.transform.up * -panDelta.y) * touchPanSensitivity * Time.deltaTime;
                activeVcam.Follow.position += move;
            }
        }
        else if (_isThreeFingerGestureActive) // Если жест тремя пальцами был активен, но условие перестало выполняться
        {
            ResetTouchGestureStates(); // Сбрасываем все состояния тач-жестов
        }


        // --- Обработка Двух Пальцев (Зум и Вращение) ---
        // Выполняется, только если нет активного жеста тремя пальцами
        // Используем secondaryFingerDown как индикатор
        if (!_isThreeFingerGestureActive && activeTouchCount == 2 && secondaryFingerDown && !isAnyActiveTouchOverUI)
        {
            touchIsControllingCamera = true; // Тач управляет камерой
            Vector2 touch0Pos = playerControls.Camera.PrimaryTouchPosition.ReadValue<Vector2>();
            Vector2 touch1Pos = playerControls.Camera.SecondaryTouchPosition.ReadValue<Vector2>();

            if (!_isTwoFingerGestureActive) // Начало жеста двумя пальцами
            {
                _isTwoFingerGestureActive = true;
                _isOneFingerTouchActiveForCamera = false; // Сбрасываем другие типы жестов
                _twoFingerDragStarted = false; // Жест еще не "начался"

                _initialTouch0ScreenPos = touch0Pos;
                _initialTouch1ScreenPos = touch1Pos;
                _initialPinchDistance = Vector2.Distance(_initialTouch0ScreenPos, _initialTouch1ScreenPos);
                _initialTwoFingerMidpointScreen = (_initialTouch0ScreenPos + _initialTouch1ScreenPos) / 2f;

                HandleTouchRotationEventStart(); // Сообщаем о начале управления камерой тачем
            }

            float currentPinchDistance = Vector2.Distance(touch0Pos, touch1Pos);
            Vector2 currentTwoFingerMidpoint = (touch0Pos + touch1Pos) / 2f;

            float pinchDeltaAmount = currentPinchDistance - _initialPinchDistance;
            Vector2 rotationDeltaAmount = currentTwoFingerMidpoint - _initialTwoFingerMidpointScreen;

            if (!_twoFingerDragStarted && (Mathf.Abs(pinchDeltaAmount) > touchDragThreshold || rotationDeltaAmount.magnitude > touchDragThreshold))
            {
                _twoFingerDragStarted = true; // Порог превышен
                // Корректируем начальные значения, чтобы избежать скачка
                _initialPinchDistance = currentPinchDistance - (Mathf.Sign(pinchDeltaAmount) * Mathf.Min(Mathf.Abs(pinchDeltaAmount), touchDragThreshold));
                _initialTwoFingerMidpointScreen = currentTwoFingerMidpoint - (rotationDeltaAmount.normalized * Mathf.Min(rotationDeltaAmount.magnitude, touchDragThreshold));

                // Пересчитываем дельты с новыми начальными значениями
                pinchDeltaAmount = currentPinchDistance - _initialPinchDistance;
                rotationDeltaAmount = currentTwoFingerMidpoint - _initialTwoFingerMidpointScreen;
            }

            if (_twoFingerDragStarted)
            {
                // Эвристика для определения основного действия: зум или вращение
                bool isPrimarilyZooming = Mathf.Abs(pinchDeltaAmount) > Mathf.Max(touchDragThreshold * 0.25f, rotationDeltaAmount.magnitude * 0.5f);
                bool isPrimarilyRotating = rotationDeltaAmount.magnitude > Mathf.Max(touchDragThreshold * 0.25f, Mathf.Abs(pinchDeltaAmount) * 0.5f);

                if (isPrimarilyZooming) // Зум
                {
                    for (int i = 0; i < 3; i++)
                    {
                        // Умножаем pinchDeltaAmount на 1.5 для ускорения
                        activeVcam.m_Orbits[i].m_Radius -= (pinchDeltaAmount * 1.5f) * touchZoomSensitivity;
                        activeVcam.m_Orbits[i].m_Radius = Mathf.Clamp(activeVcam.m_Orbits[i].m_Radius, minZoomDistance, maxZoomDistance);
                    }
                }
                else if (isPrimarilyRotating) // Вращение
                {
                    // Умножаем смещение на 1.5 для ускорения
                    Vector2 finalRotationDelta = rotationDeltaAmount * 1.5f;
                    activeVcam.m_XAxis.Value += finalRotationDelta.x * touchRotationSensitivityX * Time.deltaTime;
                    activeVcam.m_YAxis.Value -= finalRotationDelta.y * touchRotationSensitivityY * Time.deltaTime;
                    activeVcam.m_YAxis.Value = Mathf.Clamp01(activeVcam.m_YAxis.Value);
                }
            }
            // Обновляем начальные значения для следующего кадра
            _initialPinchDistance = currentPinchDistance;
            _initialTwoFingerMidpointScreen = currentTwoFingerMidpoint;
        }
        else if (_isTwoFingerGestureActive) // Если жест двумя пальцами был активен, но условие перестало выполняться
        {
            ResetTouchGestureStates(); // Сбрасываем все состояния тач-жестов
        }

        // Если все пальцы убраны (activeTouchCount == 0), но какой-то флаг жеста остался активным (например, из-за бага или резкого изменения числа касаний)
        // или если условия для активных жестов больше не выполняются (например, осталось меньше пальцев, чем нужно для текущего жеста)
        if (activeTouchCount == 0 && (_isThreeFingerGestureActive || _isTwoFingerGestureActive || _isOneFingerTouchActiveForCamera))
        {
            ResetTouchGestureStates();
        }
        // Также, если количество пальцев уменьшилось и не соответствует текущему активному жесту
        else if (_isThreeFingerGestureActive && activeTouchCount < 3) ResetTouchGestureStates();
        else if (_isTwoFingerGestureActive && activeTouchCount < 2 && !_isThreeFingerGestureActive) ResetTouchGestureStates();

        // Если никакой тач-жест не управляет камерой, но ранее управлял, нужно вызвать Stop
        if (!touchIsControllingCamera && _wasRotatingByTouchLastFrame)
        {
            HandleTouchRotationEventStop();
        }

        return touchIsControllingCamera; // Возвращает true, если какой-либо тач-жест управлял камерой в этом кадре
    }

    /*private void HandlePrimaryTap(InputAction.CallbackContext context)
    {
        // Убеждаемся, что это действительно тач
        if (!(context.control.device is Touchscreen)) return;

        // Получаем конкретный контрол касания, чтобы извлечь его ID
        var touchControl = context.control as UnityEngine.InputSystem.Controls.TouchControl;
        if (touchControl == null) return; // Доп. проверка на всякий случай

        // Если активны другие жесты, тап можно игнорировать
        if (_isTwoFingerGestureActive || _isThreeFingerGestureActive || _isOneFingerTouchActiveForCamera) return;

        Vector2 tapPosition = playerControls.Camera.PrimaryTouchPosition.ReadValue<Vector2>();
        
        // Получаем реальный ID касания из системы ввода, а не используем 0
        int touchId = touchControl.touchId.ReadValue();

        // Генерируем событие, на которое подпишется система выбора объектов
        EventManager.Instance?.RaiseEvent(EventType.ScreenTapWithValue, new ScreenTapEventArgs(this, tapPosition, touchId));
    }*/

    private void ResetTouchGestureStates()
    {
        // Сбрасываем флаги активных жестов
        bool gestureWasActive = _isOneFingerTouchActiveForCamera || _isTwoFingerGestureActive || _isThreeFingerGestureActive;

        _isOneFingerTouchActiveForCamera = false;
        _isTwoFingerGestureActive = false;
        _twoFingerDragStarted = false;
        _isThreeFingerGestureActive = false;
        _threeFingerDragStarted = false;

        // Если какой-либо жест был активен и мы его сбрасываем, сообщаем об окончании управления камерой тачем
        if (gestureWasActive && _wasRotatingByTouchLastFrame) HandleTouchRotationEventStop();
    }

    private void HandleTouchRotationEventStart()
    {
        if (!_wasRotatingByTouchLastFrame)
        {
            EventManager.Instance?.RaiseEvent(EventType.CameraRotationStarted, new EventArgs(this));
            _wasRotatingByTouchLastFrame = true;
        }
    }

    private void HandleTouchRotationEventStop()
    {
        if (_wasRotatingByTouchLastFrame)
        {
            EventManager.Instance?.RaiseEvent(EventType.CameraRotationStopped, new EventArgs(this));
            _wasRotatingByTouchLastFrame = false;
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
        playerControls?.Dispose();
    }

    // --- Обработка Ввода для FreeLook с New Input System ---

    private void HandleFreeLookZoom()
    {
        // Читаем значение из action "Zoom"
        float scrollY = playerControls.Camera.Zoom.ReadValue<float>();

        // Используем Mathf.Sign для определения направления и умножаем на фиксированное значение,
        // т.к. scroll.y дает большое значение при одном щелчке колеса (обычно +/- 120)
        if (Mathf.Abs(scrollY) > 0.1f) // Небольшой порог для срабатывания
        {
            float zoomAmount = Mathf.Sign(scrollY) * zoomSensitivity; // Определяем направление и применяем чувствительность

            if (activeVcam != null)
            {
                // Изменяем радиусы для ВСЕХ трех орбит
                for (int i = 0; i < 3; i++)
                {
                    // УМЕНЬШАЕМ радиус при прокрутке ВПЕРЕД (scrollY > 0 -> zoomAmount > 0)
                    // Но нам нужно вычитать положительный zoomAmount
                    activeVcam.m_Orbits[i].m_Radius -= zoomAmount;
                    // Ограничиваем радиус
                    activeVcam.m_Orbits[i].m_Radius = Mathf.Clamp(activeVcam.m_Orbits[i].m_Radius, minZoomDistance, maxZoomDistance);
                }
            }
        }
    }

    private void HandleFreeLookRotation()
    {
        // Проверяем, зажат ли модификатор вращения (теперь это ПКМ)
        if (playerControls.Camera.RotationModifier.IsPressed() && activeVcam != null)
        {
            // Читаем дельту мыши из action "Look"
            Vector2 lookDelta = playerControls.Camera.Look.ReadValue<Vector2>();

            // Применяем горизонтальное вращение
            // Чувствительность, возможно, придется сильно подстроить!
            activeVcam.m_XAxis.Value += lookDelta.x * rotationSensitivityX * Time.deltaTime; // Добавляем Time.deltaTime для независимости от FPS

            // Вертикальное вращение (Y Axis)
            activeVcam.m_YAxis.Value -= lookDelta.y * rotationSensitivityY * Time.deltaTime;
            activeVcam.m_YAxis.Value = Mathf.Clamp01(activeVcam.m_YAxis.Value);
        }
    }


    private void HandleFreeLookPan()
    {
        // Проверяем, зажат ли модификатор панорамирования (MMB)
        if (playerControls.Camera.PanModifier.IsPressed() && activeVcam != null)
        {
            Transform followTarget = activeVcam.Follow;
            if (followTarget == null) return;

            // Читаем дельту мыши из action "Look"
            Vector2 lookDelta = playerControls.Camera.Look.ReadValue<Vector2>();

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                Vector3 right = mainCam.transform.right;
                Vector3 up = mainCam.transform.up;
                // Чувствительность, возможно, придется подстроить!
                // Используем lookDelta.x и lookDelta.y
                Vector3 move = (right * -lookDelta.x + up * -lookDelta.y) * panSensitivity * Time.deltaTime; // Добавляем Time.deltaTime
                followTarget.position += move;
            }
        }
    }


    // --- Переключение камер ---
    public void SwitchToCamera(CameraFocusTarget targetFocus)
    {
        if (!vcamMap.TryGetValue(targetFocus, out CinemachineFreeLook targetVcam))
        {
            if (targetFocus != CameraFocusTarget.None && vcamMap.TryGetValue(CameraFocusTarget.Overview, out var overviewCam))
            {
                targetVcam = overviewCam;
                Debug.LogWarning($"[CameraController] Не найдена vcam для '{targetFocus}'. Переключаюсь на Overview.");
            }
            else
            {
                Debug.LogError($"[CameraController] Не найдена vcam для '{targetFocus}' и нет Overview.");
                return;
            }
        }

        // Сброс состояния камеры перед активацией
        ResetCameraState(targetVcam);

        targetVcam.Priority = highPriority;
        activeVcam = targetVcam;

        foreach (var vcam in allVcams)
        {
            if (vcam != targetVcam)
                vcam.Priority = lowPriority;
        }

        if (activeVcam.LookAt == null) Debug.LogWarning($"[CameraController] Активная камера {activeVcam.name} не имеет LookAt.", activeVcam);
        if (activeVcam.Follow == null) Debug.LogWarning($"[CameraController] Активная камера {activeVcam.name} не имеет Follow.", activeVcam);

        // Отправляем команду на очистку подсветки через ToDoManager при смене камеры
        if (ToDoManager.Instance != null)
        {
            ToDoManager.Instance.HandleAction(ActionType.UpdateHighlight, new UpdateHighlightArgs(true)); // true для команды очистки
        }
        else
        {
            Debug.LogWarning("[CameraController] ToDoManager не найден! Не могу отправить команду ClearHighlight.");
        }
        Debug.Log($"[CameraController] Активная камера: {activeVcam.name} (Цель: {targetFocus})");
    }

    // Метод для сброса вращения, зума и панорамирования камеры к значениям по умолчанию
    private void ResetCameraState(CinemachineFreeLook vcamToReset)
    {
        if (vcamToReset == null) return;

        // 1. Сброс вращения (осей виртуальной камеры)
        vcamToReset.m_XAxis.Value = 0f; // Сбрасываем горизонтальную ось (даже если значение не отображается, это инициирует сброс)
        vcamToReset.m_YAxis.Value = 0.5f; // Сбрасываем вертикальную ось на середину


        // 2. Сброс зума (радиусов орбит виртуальной камеры)
        if (defaultOrbitRadii.TryGetValue(vcamToReset, out float[] initialRadii))
        {
            if (initialRadii.Length == vcamToReset.m_Orbits.Length)
            {
                for (int i = 0; i < vcamToReset.m_Orbits.Length; i++)
                {
                    vcamToReset.m_Orbits[i].m_Radius = initialRadii[i];
                }
            }
            else
            {
                Debug.LogWarning($"[CameraController] Не совпадает количество орбит при сбросе зума для {vcamToReset.name}. Ожидалось {initialRadii.Length}, получено {vcamToReset.m_Orbits.Length}");
            }
        }
        else
        {
            Debug.LogWarning($"[CameraController] Не найдены дефолтные радиусы для сброса зума камеры {vcamToReset.name}");
        }


        // 3. Сброс панорамирования (возврат Follow/LookAt таргета на начальную позицию)
        Transform targetToReset = vcamToReset.Follow; // Получаем таргет (он же LookAt)
        if (targetToReset != null && initialTargetPositions.TryGetValue(vcamToReset, out Vector3 initialPos))
        {
            targetToReset.position = initialPos; // Устанавливаем сохраненную начальную позицию
        }
        else
        {
            if (targetToReset == null) Debug.LogWarning($"[CameraController] Не удалось сбросить панорамирование для {vcamToReset.name}: Follow/LookAt таргет не назначен.");
            else Debug.LogWarning($"[CameraController] Не найдены дефолтные позиции для сброса панорамирования камеры {vcamToReset.name}");
        }
    }

    // --- Подписка/Отписка от событий ---
    private void SubscribeToEvents()
    {
        if (EventManager.Instance == null) { Debug.LogError("[CameraController] EventManager не доступен!"); return; }
        EventManager.Instance.Subscribe(EventType.FocusCameraRequested, this, HandleFocusRequest);
        EventManager.Instance.Subscribe(EventType.ResetCameraStateRequested, this, HandleCameraResetRequest);
    }
    private void UnsubscribeFromEvents()
    {
        if (EventManager.Instance != null)
        {
            EventManager.Instance.Unsubscribe(EventType.FocusCameraRequested, this, HandleFocusRequest);
            EventManager.Instance.Unsubscribe(EventType.ResetCameraStateRequested, this, HandleCameraResetRequest);
        }
    }
    private void HandleFocusRequest(EventArgs args)
    {
        if (!(args is FocusCameraEventArgs focusArgs)) return;
        SwitchToCamera(focusArgs.Target);
    }

    private void HandleCameraResetRequest(EventArgs args)
    {
        ResetActiveCameraState();
    }

    public void ResetActiveCameraState()
    {
        if (activeVcam != null)
        {
            Debug.Log($"[CameraController] Сброс состояния для активной камеры: {activeVcam.name}");
            ResetCameraState(activeVcam);
        }
        else
        {
            Debug.LogWarning("[CameraController] Попытка сброса, но нет активной камеры.");
        }
    }
    
    public void CancelRotationState()
    {
        _wasRotatingLastFrame = false;
        isRotationHeld = false;
        Debug.Log("[CameraController] Состояние вращения принудительно сброшено.");
    }
}