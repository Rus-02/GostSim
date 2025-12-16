using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public class GraphController : MonoBehaviour
{
    #region Singleton
    public static GraphController Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Debug.LogWarning($"[GraphController] Duplicate instance. Destroying '{gameObject.name}'."); Destroy(gameObject); return; }
        Instance = this;
        SubscribeToCommands();
    }
    #endregion

    #region Enums and State
    public enum GraphState
    {
        Idle, Preparing, Ready, Plotting, Paused, Finished, Error
    }
    [Header("State (Readonly)")]
    [SerializeField] private GraphState _currentState = GraphState.Idle;
    public GraphState CurrentState => _currentState;
    private bool isPaused = false;
    private Coroutine plotCoroutine = null;
    private int lastDrawnIndex = 0;

    // lastScaledX хранит текущую ОТНОСИТЕЛЬНУЮ ДЕФОРМАЦИЮ (%) с графика
    // lastScaledY хранит текущую СИЛУ (кН), рассчитанную для АКТУАЛЬНОЙ площади
    private float lastPlotPoint_RelativeStrain_Percent = 0f; // Хранит X последней отрисованной точки (% деформации)
    private float lastPlotPoint_Force_kN_actual = 0f;    // Хранит Y последней отрисованной точки (Сила в кН для актуальной площади)

    // Данные для событий по ГОСТ (установка/снятие экстензометра)
    private float _proportionalityLimit_kN_ForEvents = float.NaN; // Предел пропорциональности в кН для событий
    private bool _extensometerAttachEventSent = false;
    private bool _extensometerRemoveEventSent = false;
    private const float EXTENSOMETER_ATTACH_THRESHOLD_PERCENT = 0.25f; // Установка на 25% от макс. силы
    private const float EXTENSOMETER_REMOVE_THRESHOLD_PERCENT = 0.75f; // Снятие на 75% от макс. силы

    public float X_UltimateStrength_Percent { get; private set; } = -1f; // X-координата (%) для пика UTS
    public float X_Rupture_Percent { get; private set; } = -1f;         // X-координата (%) для разрыва по порогу
    private float _ruptureStressThreshold_MPa_from_material = -1f; // Для поиска точки разрыва

    #endregion

    #region UI References
    [Header("UI Elements")]
    [SerializeField] private RawImage graphRawImage = null;
    [SerializeField] private RawImage gridRawImage = null;
    [SerializeField] private Text[] horizontalScaleLabels = new Text[0];
    [SerializeField] private Text[] verticalScaleLabels = new Text[0];
    #endregion

    #region Configuration & Data (Internal)
    [Header("Plotting Parameters")]
    [SerializeField] private Color plotColor = Color.red;
    [SerializeField][Range(1, 5)] private int plotPointSize = 2;
    [SerializeField] private Color gridLineColor = new Color(0.84f, 0.84f, 0.84f, 1f);



    private TextAsset _currentGraphDataFile;
    private float standardInitialLengthFromMaterial = 1f; // Эталонная длина из MaterialPropertiesAsset
    private float standardInitialAreaFromMaterial = 1f;   // Эталонная площадь из MaterialPropertiesAsset
    private float actualLength = 1f, actualArea = 1f;
    private float testSpeed = 1f;
    private TestSpeedMode currentSpeedMode;
    

    private List<Vector2> originalGraphPoints_StrainPercent_StressMPa = new List<Vector2>(); // X: ОтносительнаяДеформация_%, Y: Напряжение_МПа
    private float maxOriginal_RelativeStrain_Percent = 1f; // Макс. относительная деформация (%) из файла
    private float maxOriginal_Stress_MPa = 1f;             // Макс. напряжение (МПа) из файла

    private float maxScaled_RelativeStrain_Percent = 1f;
    private float maxScaled_Force_kN_actual = 1f;
    private float graphDrawDelay = 0.05f;

    private Texture2D graphTexture = null;
    private Texture2D gridTexture = null;
    private float _machineForceLimit_kN = float.MaxValue;
    private bool _isLimitExceededEventSent = false;
    #endregion

    #region Visual Scaling & RectTransform Data
    private RectTransform graphRectTransform = null;
    private Vector2 originalGraphSizeDelta;
    private Vector3 originalGraphScale;
    private Vector2 originalGraphPivot;
    private Vector2 originalGraphAnchorMin;
    private Vector2 originalGraphAnchorMax;
    private Vector2 originalGraphAnchoredPosition;

    private float currentGraphScaleX = 1f;
    private float currentGraphScaleY = 1f;
    private float previousMaxNormX = 0.1f;
    private float previousMaxNormY = 0.1f;
    private const float GraphPadding = 0.11f;
    #endregion

    /*#region Events (Outgoing)
    public event Action OnPlotComplete;
    #endregion*/

    #region MonoBehaviour Lifecycle
    void Start()
    {
        if (SessionManager.Instance != null) { _machineForceLimit_kN = SessionManager.Instance.MaxMachineForce_kN; Debug.Log($"[GraphController] Лимит прочности машины установлен: {_machineForceLimit_kN} кН."); }
        if (graphRawImage != null) { graphRectTransform = graphRawImage.GetComponent<RectTransform>(); if (graphRectTransform == null) { Debug.LogError("[GC] RawImage has no RectTransform!", this); SetState(GraphState.Error); return; } SaveOriginalGraphParameters(); }
        else { Debug.LogError("[GC] graphRawImage not assigned!", this); SetState(GraphState.Error); return; }
        if (gridRawImage == null) Debug.LogWarning("[GC] gridRawImage not assigned.", this);
        if (horizontalScaleLabels == null || horizontalScaleLabels.Length == 0) Debug.LogWarning("[GC] horizontalScaleLabels not assigned.", this);
        if (verticalScaleLabels == null || verticalScaleLabels.Length == 0) Debug.LogWarning("[GC] verticalScaleLabels not assigned.", this);

        ResetGraphAndSimulation();
        if (_currentState != GraphState.Error) { SetState(GraphState.Idle); }
    }

    void OnDestroy()
    {
        UnsubscribeFromCommands();
        if (graphTexture != null) Destroy(graphTexture);
        if (gridTexture != null) Destroy(gridTexture);
        if (Instance == this) Instance = null;
    }

    private void SaveOriginalGraphParameters()
    {
        if (graphRectTransform == null) return;
        originalGraphSizeDelta = graphRectTransform.sizeDelta; originalGraphScale = graphRectTransform.localScale; originalGraphAnchoredPosition = graphRectTransform.anchoredPosition;
        originalGraphAnchorMin = graphRectTransform.anchorMin; originalGraphAnchorMax = graphRectTransform.anchorMax; originalGraphPivot = graphRectTransform.pivot;
    }
    #endregion

    #region ToDoManager Command Subscription
    private void SubscribeToCommands()
    {
        if (ToDoManager.Instance == null) { Debug.LogError("[GC] ToDoManager null during subscription!"); return; }
        var tm = ToDoManager.Instance;
        tm.SubscribeToAction(ActionType.PrepareGraph, HandlePrepareGraphCommand);
        tm.SubscribeToAction(ActionType.StartGraphAndSimulation, HandleStartGraphCommand);
        tm.SubscribeToAction(ActionType.PauseGraphAndSimulation, HandlePauseGraphCommand);
        tm.SubscribeToAction(ActionType.ResumeGraphAndSimulation, HandleResumeGraphCommand);
        tm.SubscribeToAction(ActionType.ResetGraphAndSimulation, HandleResetGraphCommand);

        Debug.Log("[GraphController] Subscribed to ToDoManager commands.");
    }

    private void UnsubscribeFromCommands()
    {
        var tm = ToDoManager.Instance;
        if (tm != null)
        {
            tm.UnsubscribeFromAction(ActionType.PrepareGraph, HandlePrepareGraphCommand);
            tm.UnsubscribeFromAction(ActionType.StartGraphAndSimulation, HandleStartGraphCommand);
            tm.UnsubscribeFromAction(ActionType.PauseGraphAndSimulation, HandlePauseGraphCommand);
            tm.UnsubscribeFromAction(ActionType.ResumeGraphAndSimulation, HandleResumeGraphCommand);
            tm.UnsubscribeFromAction(ActionType.ResetGraphAndSimulation, HandleResetGraphCommand);
        }
    }
    #endregion

    #region ToDoManager Command Handlers
    private void HandleStartGraphCommand(BaseActionArgs baseArgs) { StartPlotting(); }
    private void HandlePauseGraphCommand(BaseActionArgs baseArgs) { PausePlotting(); }
    private void HandleResumeGraphCommand(BaseActionArgs baseArgs) { ResumePlotting(); }
    private void HandleResetGraphCommand(BaseActionArgs args) { ResetGraphAndSimulation(); }

    #endregion

    #region Event Handlers (Incoming)
    private void HandlePrepareGraphCommand(BaseActionArgs args)
    {
        PrepareGraphForTest();
    }
    #endregion

    #region Preparation Logic

    public void PrepareGraphForTest()
    {
        if (_currentState != GraphState.Idle && _currentState != GraphState.Finished && _currentState != GraphState.Error)
        {
            Debug.LogWarning($"[GC] PrepareGraph called in state {_currentState}. Ignored.");
            return;
        }

        var monitor = SystemStateMonitor.Instance;
        if (monitor == null)
        {
            Debug.LogError("[GC] SystemStateMonitor не найден! Подготовка невозможна.");
            SetState(GraphState.Error); return;
        }

        // --- ШАГ 1: ПОЛУЧАЕМ ВСЕ ДАННЫЕ ИЗ МОНИТОРА ---

        MaterialPropertiesAsset material = monitor.SelectedMaterial;


if (material.graphDataTextFile == null)
{
    Debug.LogError($"[GC ДИАГНОСТИКА] Ассет материала '{material.name}' ({material.GetInstanceID()}) получен, НО его поле 'graphDataTextFile' ПУСТОЕ!", material);
}
else
{
    Debug.Log($"[GC ДИАГНОСТИКА] Ассет материала '{material.name}' ({material.GetInstanceID()}) получен. Его файл графика: '{material.graphDataTextFile.name}'", material);
}

        if (material == null)
        {
            Debug.LogError("[GC] SelectedMaterial из SystemStateMonitor is null.", this);
            SetState(GraphState.Error); return;
        }

        // Получаем параметры образца из словаря в мониторе
        monitor.CurrentSampleParameters.TryGetValue("Length", out actualLength);
        monitor.CurrentSampleParameters.TryGetValue("Speed", out testSpeed);
        
        // Площадь берем уже рассчитанную
        actualArea = monitor.CalculatedArea;

        // Получаем остальные данные из монитора и ассета материала
        currentSpeedMode = monitor.SelectedSpeedMode;
        _currentGraphDataFile = material.graphDataTextFile;
        standardInitialLengthFromMaterial = material.standardInitialLength_mm;
        standardInitialAreaFromMaterial = material.standardInitialArea_mm2;
        _ruptureStressThreshold_MPa_from_material = material.ruptureStressThreshold_MPa;
        float proportionalityLimit_MPa = material.proportionalityLimit_MPa;


        // --- ШАГ 2: ВАЛИДАЦИЯ И ПОДГОТОВКА (логика без изменений) ---

        _isLimitExceededEventSent = false;
        SetState(GraphState.Preparing);
        StopPlottingCoroutine();

        if (proportionalityLimit_MPa >= 0 && actualArea > 0)
        {
            _proportionalityLimit_kN_ForEvents = (proportionalityLimit_MPa * actualArea) / 1000.0f;
        }
        else
        {
            _proportionalityLimit_kN_ForEvents = float.NaN;
        }
        _extensometerAttachEventSent = false;
        _extensometerRemoveEventSent = false;

        if (_currentGraphDataFile == null)
        {
            Debug.LogError("[GC] GraphDataTextFile from MaterialAsset is null.", this);
            SetState(GraphState.Error); return;
        }
        if (actualLength <= 0 || actualArea <= 0 || testSpeed <= 0)
        {
            Debug.LogError($"[GC] Invalid actual parameters from Monitor: Length={actualLength}, Area={actualArea}, Speed={testSpeed}.", this);
            SetState(GraphState.Error); return;
        }
        if (standardInitialLengthFromMaterial <= 0 || standardInitialAreaFromMaterial <= 0)
        {
            Debug.LogError($"[GC] Invalid standard material parameters: StandardLength={standardInitialLengthFromMaterial}, StandardArea={standardInitialAreaFromMaterial}.", this);
            SetState(GraphState.Error); return;
        }
        if (float.IsNaN(_proportionalityLimit_kN_ForEvents) && proportionalityLimit_MPa >= 0)
        {
            Debug.LogWarning($"[GC] ProportionalityLimit_kN is NaN. Original MPa was {proportionalityLimit_MPa}. ActualArea might be zero or invalid.", this);
        }

        if (!LoadAndProcessGraphData()) return;

        maxScaled_RelativeStrain_Percent = maxOriginal_RelativeStrain_Percent;

        if (actualArea > 0)
        {
            maxScaled_Force_kN_actual = (maxOriginal_Stress_MPa * actualArea) / 1000.0f;
            SystemStateMonitor.Instance?.ReportMaxForce(maxScaled_Force_kN_actual);
            EventManager.Instance?.RaiseEvent(EventType.MaxForceCalculated, EventArgs.Empty);
        }
        else
        {
            Debug.LogError($"[GC] ActualArea ({actualArea}) is not positive. Cannot calculate maxScaled_Force_kN_actual.", this);
            SetState(GraphState.Error); return;
        }
        
        // --- ШАГ 3: ФИНАЛИЗАЦИЯ И РЕПОРТЫ (логика без изменений) ---

        // Сообщаем рассчитанные ключевые точки в монитор
        SystemStateMonitor.Instance?.ReportExtensometerEvent(false, false);

        // Отправляем старое событие (пока не уберем его)
        /*try
        {
            if (maxScaled_Force_kN_actual > 0)
            {
                MaxForceCalculatedEventArgs forceArgs = new MaxForceCalculatedEventArgs(this, maxScaled_Force_kN_actual);
                EventManager.Instance?.RaiseEvent(EventType.MaxForceCalculated, forceArgs);
            }
        }
        catch (Exception ex) { Debug.LogError($"[GC] Ошибка при отправке события MaxForceCalculated: {ex.Message}"); }*/

        if (maxScaled_RelativeStrain_Percent < 0 || (maxScaled_Force_kN_actual <= 0 && originalGraphPoints_StrainPercent_StressMPa.Any(p => p.y > 0)))
        {
            Debug.LogError($"[GC] Invalid calculated maxScaled values: MaxRelativeStrain_%={maxScaled_RelativeStrain_Percent}, MaxForce_kN_Actual={maxScaled_Force_kN_actual}.", this);
            SetState(GraphState.Error); return;
        }
        
        // testSpeed здесь уже в мм/мин, если режим DeformationRate.
        // Если ForceRate, то он в кН/с. CalculateEffectiveSpeedMmPerMin это учтет.
        this.testSpeed = CalculateEffectiveSpeedMmPerMin(testSpeed, currentSpeedMode);

        if (!CalculateDrawDelay()) return;
        if (!InitializeGraphVisuals()) return;
        lastDrawnIndex = 0;
        isPaused = false;
        previousMaxNormX = 0.1f;
        previousMaxNormY = 0.1f;
        lastPlotPoint_RelativeStrain_Percent = 0f;
        lastPlotPoint_Force_kN_actual = 0f;
        InitializeScaleLabels();
        SetState(GraphState.Ready);
    }

    private bool LoadAndProcessGraphData()
    {
        originalGraphPoints_StrainPercent_StressMPa.Clear();
        maxOriginal_RelativeStrain_Percent = 0f;
        maxOriginal_Stress_MPa = 0f;

        if (_currentGraphDataFile == null)
        {
            Debug.LogError("[GC] _currentGraphDataFile is null in LoadAndProcessGraphData.", this);
            SetState(GraphState.Error);
            return false;
        }

        try
        {
            string fileContent = _currentGraphDataFile.text;
            string[] lines = fileContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length < 2)
            {
                Debug.LogError($"[GC] Graph data in '{_currentGraphDataFile.name}' contains less than 2 points.", this);
                SetState(GraphState.Error);
                return false;
            }

            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine)) continue;

                string[] parts = trimmedLine.Split('|');
                // Формат файла: "Напряжение_МПа|ОтносительнаяДеформация_%"
                // parts[0] = Напряжение (Y-ось исходная)
                // parts[1] = Относительная деформация (X-ось исходная)
                if (parts.Length == 2 &&
                    float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float stress_MPa) &&
                    float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float relativeStrain_Percent))
                {
                    // X: Относительная деформация (%) напрямую из файла
                    // Y: Напряжение (МПа) напрямую из файла
                    originalGraphPoints_StrainPercent_StressMPa.Add(new Vector2(relativeStrain_Percent, stress_MPa));
                    maxOriginal_RelativeStrain_Percent = Mathf.Max(maxOriginal_RelativeStrain_Percent, relativeStrain_Percent);
                    maxOriginal_Stress_MPa = Mathf.Max(maxOriginal_Stress_MPa, stress_MPa);
                }
                else
                {
                    Debug.LogError($"[GC] Error parsing line in '{_currentGraphDataFile.name}': '{line}'. Expected format: Stress_MPa|RelativeStrain_%.", this);
                    SetState(GraphState.Error);
                    return false;
                }
            }

            if (originalGraphPoints_StrainPercent_StressMPa.Count < 2)
            {
                Debug.LogError($"[GC] Less than 2 valid points parsed from '{_currentGraphDataFile.name}' after processing all lines.", this);
                SetState(GraphState.Error);
                return false;
            }

            if (maxOriginal_Stress_MPa <= 0 && originalGraphPoints_StrainPercent_StressMPa.Any(p => p.y > 0))
            {
                Debug.LogWarning($"[GC] Max stress (maxOriginal_Stress_MPa: {maxOriginal_Stress_MPa} MPa) from '{_currentGraphDataFile.name}' is zero or negative, but some points have positive stress. Check graph data.", this);
            }
            if (maxOriginal_RelativeStrain_Percent < 0)
            {
                Debug.LogError($"[GC] Max relative strain (maxOriginal_RelativeStrain_Percent: {maxOriginal_RelativeStrain_Percent} %) from '{_currentGraphDataFile.name}' is negative. Check graph data.", this);
                SetState(GraphState.Error);
                return false;
            }

            FindKeyXPointsOnGraph(); // Вызываем поиск ключевых X-точек (теперь он будет работать с МПа)

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GC] Exception loading graph data from '{_currentGraphDataFile?.name ?? "Unknown File"}': {ex.Message}\nStackTrace: {ex.StackTrace}", this);
            SetState(GraphState.Error);
            return false;
        }
    }

    private void FindKeyXPointsOnGraph()
    {
        // Теперь поиск идет по originalGraphPoints_StrainPercent_StressMPa, где Y это Напряжение_МПа
        if (originalGraphPoints_StrainPercent_StressMPa == null || originalGraphPoints_StrainPercent_StressMPa.Count < 2)
        {
            X_UltimateStrength_Percent = -1f;
            X_Rupture_Percent = -1f;
            Debug.LogWarning("[GC] FindKeyXPointsOnGraph: originalGraphPoints_StrainPercent_StressMPa не готов.");
            return;
        }

        X_UltimateStrength_Percent = -1f;
        X_Rupture_Percent = -1f;
        int utsIndex = -1;
        float maxStress_MPa_Observed = float.MinValue; // Максимальное наблюденное напряжение на графике

        // 1. Находим X для Ultimate Tensile Strength (UTS) - пик графика по Y (напряжению)
        int observedUtsPointIndex = -1;
        for (int i = 0; i < originalGraphPoints_StrainPercent_StressMPa.Count; i++)
        {
            if (originalGraphPoints_StrainPercent_StressMPa[i].y > maxStress_MPa_Observed)
            {
                maxStress_MPa_Observed = originalGraphPoints_StrainPercent_StressMPa[i].y;
                observedUtsPointIndex = i;
            }
        }

        if (observedUtsPointIndex != -1)
        {
            X_UltimateStrength_Percent = originalGraphPoints_StrainPercent_StressMPa[observedUtsPointIndex].x;
            utsIndex = observedUtsPointIndex;
            Debug.Log($"[GC] Найден X для наблюдаемого UTS (пик): {X_UltimateStrength_Percent}% (напряжение {maxStress_MPa_Observed} МПа)");

            // 2. Находим X для Rupture (когда напряжение падает ниже порога ПОСЛЕ UTS)
            // _ruptureStressThreshold_MPa_from_material уже в МПа.
            if (_ruptureStressThreshold_MPa_from_material >= 0 && utsIndex < originalGraphPoints_StrainPercent_StressMPa.Count - 1)
            {
                for (int i = utsIndex; i < originalGraphPoints_StrainPercent_StressMPa.Count - 1; i++)
                {
                    Vector2 p1 = originalGraphPoints_StrainPercent_StressMPa[i];     // Текущая точка (X: %, Y: МПа)
                    Vector2 p2 = originalGraphPoints_StrainPercent_StressMPa[i + 1]; // Следующая точка (X: %, Y: МПа)

                    if (p1.y >= _ruptureStressThreshold_MPa_from_material && p2.y < _ruptureStressThreshold_MPa_from_material)
                    {
                        if (Mathf.Approximately(p1.y, p2.y))
                        {
                            X_Rupture_Percent = p2.x;
                        }
                        else
                        {
                            X_Rupture_Percent = p1.x + (p2.x - p1.x) * (_ruptureStressThreshold_MPa_from_material - p1.y) / (p2.y - p1.y);
                        }
                        Debug.Log($"[GC] Интерполирован X для разрыва (напряжение <= {_ruptureStressThreshold_MPa_from_material:F2} МПа): {X_Rupture_Percent}% между точками ({p1.x}%, {p1.y:F2}МПа) и ({p2.x}%, {p2.y:F2}МПа)");
                        break;
                    }
                    else if (i == utsIndex && p1.y < _ruptureStressThreshold_MPa_from_material)
                    {
                        X_Rupture_Percent = p1.x;
                        Debug.LogWarning($"[GC] Первая точка после/на пике UTS ({p1.x}%, {p1.y:F2}МПа) уже ниже порога разрыва {_ruptureStressThreshold_MPa_from_material:F2}МПа. Используется ее X.");
                        break;
                    }
                }

                if (X_Rupture_Percent < 0 && originalGraphPoints_StrainPercent_StressMPa.Last().x > X_UltimateStrength_Percent)
                {
                    X_Rupture_Percent = originalGraphPoints_StrainPercent_StressMPa.Last().x;
                    Debug.LogWarning($"[GC] Точка разрыва по порогу (пересечение) не найдена. Используется X последней точки графика: {X_Rupture_Percent}%");
                }
            }
            else if (_ruptureStressThreshold_MPa_from_material < 0)
            {
                Debug.LogWarning("[GC] ruptureStressThreshold_MPa не задан, X для разрыва не будет определен по порогу. Используется X последней точки.");
                if (originalGraphPoints_StrainPercent_StressMPa.Count > 0 && (utsIndex == -1 || originalGraphPoints_StrainPercent_StressMPa.Last().x > X_UltimateStrength_Percent))
                {
                    X_Rupture_Percent = originalGraphPoints_StrainPercent_StressMPa.Last().x;
                }
                else if (X_UltimateStrength_Percent >= 0)
                {
                    X_Rupture_Percent = X_UltimateStrength_Percent;
                }
            }
            else if (utsIndex == originalGraphPoints_StrainPercent_StressMPa.Count - 1 && originalGraphPoints_StrainPercent_StressMPa[utsIndex].y < _ruptureStressThreshold_MPa_from_material)
            {
                X_Rupture_Percent = originalGraphPoints_StrainPercent_StressMPa[utsIndex].x;
                Debug.LogWarning($"[GC] Пик UTS является последней точкой и ниже порога разрыва. X разрыва = X UTS: {X_Rupture_Percent}%");
            }
        }
        else
        {
            Debug.LogWarning("[GC] Не удалось найти пик UTS на графике. Ключевые X-точки не установлены.");
        }

        if (X_Rupture_Percent >= 0 && X_UltimateStrength_Percent >= 0 && X_Rupture_Percent < X_UltimateStrength_Percent)
        {
            Debug.LogWarning($"[GC] Скорректированный X разрыва ({X_Rupture_Percent}%) оказался раньше X UTS ({X_UltimateStrength_Percent}%). Устанавливаем X разрыва равным X последней точки или UTS.");
            if (utsIndex != -1 && utsIndex < originalGraphPoints_StrainPercent_StressMPa.Count - 1)
            {
                X_Rupture_Percent = originalGraphPoints_StrainPercent_StressMPa.Last().x;
            }
            else if (X_UltimateStrength_Percent >= 0)
            {
                X_Rupture_Percent = X_UltimateStrength_Percent;
            }
        }
        if (X_Rupture_Percent < 0 && X_UltimateStrength_Percent >= 0)
        {
            X_Rupture_Percent = originalGraphPoints_StrainPercent_StressMPa.Last().x;
            Debug.LogWarning($"[GC] X_Rupture_Percent не был определен, установлен в X последней точки: {X_Rupture_Percent}%");
        }

        EventManager.Instance.RaiseEvent(EventType.GraphKeyPointsCalculated, EventArgs.Empty);
        SystemStateMonitor.Instance?.ReportGraphKeyPoints(
            X_UltimateStrength_Percent, 
            X_Rupture_Percent, 
            _proportionalityLimit_kN_ForEvents
        );
    }

    private bool CalculateDrawDelay()
    {
        // originalGraphPoints_StrainPercent_StressMPa.x хранит относительную деформацию (%).
        // actualLength используется для пересчета среднего шага в абсолютные мм.

        if (originalGraphPoints_StrainPercent_StressMPa.Count < 2 || testSpeed <= 0 || actualLength <= 0)
        {
            Debug.LogError($"[GC] Invalid data for draw delay calculation: Points={originalGraphPoints_StrainPercent_StressMPa.Count}, Speed={testSpeed}, ActualLength={actualLength}.", this);
            SetState(GraphState.Error); return false;
        }

        float totalRelativeDeltaX_Percent = 0;
        int validSteps = 0;
        for (int i = 1; i < originalGraphPoints_StrainPercent_StressMPa.Count; i++)
        {
            float deltaX_Percent = originalGraphPoints_StrainPercent_StressMPa[i].x - originalGraphPoints_StrainPercent_StressMPa[i - 1].x;
            if (deltaX_Percent >= 0) // Учитываем только положительные или нулевые приращения деформации
            {
                totalRelativeDeltaX_Percent += deltaX_Percent;
                validSteps++;
            }
        }

        if (validSteps == 0)
        {
            Debug.LogWarning("[GC] No valid positive X (relative strain %) steps found for draw delay calculation. Using min delay.", this);
            graphDrawDelay = 0.001f; // Минимальная задержка
            return true;
        }

        float averageRelativeDeltaX_Percent = totalRelativeDeltaX_Percent / validSteps;
        if (averageRelativeDeltaX_Percent <= Mathf.Epsilon) // Если средний шаг очень мал или нулевой
        {
            Debug.LogWarning($"[GC] Average relative delta X (%) is very small ({averageRelativeDeltaX_Percent}). Using min delay.", this);
            graphDrawDelay = 0.001f;
            return true;
        }

        float averageAbsoluteDeltaX_mm_actual = (averageRelativeDeltaX_Percent / 100.0f) * actualLength;
        float speedMmPerSec = testSpeed / 60.0f;

        if (speedMmPerSec <= Mathf.Epsilon)
        {
            Debug.LogError("[GC] Test speed (mm/sec) is near zero, cannot calculate draw delay.", this);
            SetState(GraphState.Error); return false;
        }

        graphDrawDelay = averageAbsoluteDeltaX_mm_actual / speedMmPerSec;

        const float minDelay = 0.001f;
        if (graphDrawDelay < minDelay)
        {
            graphDrawDelay = minDelay;
        }
        return true;
    }

    /// Рассчитывает эффективную скорость в мм/мин.
    /// Если режим ForceRate, пересчитывает кН/с в эквивалентную скорость мм/мин до пика прочности.
    private float CalculateEffectiveSpeedMmPerMin(float actualSpeedFromMonitor, TestSpeedMode speedModeFromMonitor)
    {
        if (speedModeFromMonitor == TestSpeedMode.DeformationRate)
        {
            return actualSpeedFromMonitor; // Просто возвращаем скорость в мм/мин
        }

        // Логика пересчета из кН/с в мм/мин
        if (maxScaled_Force_kN_actual <= 0 || X_UltimateStrength_Percent < 0 || actualSpeedFromMonitor <= 0)
        {
            Debug.LogWarning("[GC] Невозможно рассчитать эффективную скорость из кН/с. Недостаточно данных. Используется скорость по умолчанию.");
            return actualSpeedFromMonitor;
        }

        float timeToPeak_sec = maxScaled_Force_kN_actual / actualSpeedFromMonitor;
        if (timeToPeak_sec <= 0)
        {
            Debug.LogWarning("[GC] Расчетное время до пика <= 0. Используется скорость по умолчанию.");
            return actualSpeedFromMonitor;
        }

        float deformationAtPeak_mm = (X_UltimateStrength_Percent / 100.0f) * actualLength;
        float effectiveSpeed_mm_per_sec = deformationAtPeak_mm / timeToPeak_sec;
        float effectiveSpeed_mm_per_min = effectiveSpeed_mm_per_sec * 60.0f;
        Debug.Log($"[GC] Пересчет скорости: {actualSpeedFromMonitor:F2} кН/с эквивалентно {effectiveSpeed_mm_per_min:F2} мм/мин (до пика).");
        return effectiveSpeed_mm_per_min;
    }

    #endregion

    #region Visual Initialization and Drawing
    private bool InitializeGraphVisuals()
    {
        if (graphRawImage == null || graphRectTransform == null) { Debug.LogError("[GC] graphRawImage null.", this); SetState(GraphState.Error); return false; }
        int texWidth = Mathf.RoundToInt(graphRectTransform.rect.width); int texHeight = Mathf.RoundToInt(graphRectTransform.rect.height);
        if (!InitializeTexture(graphRawImage, ref graphTexture, texWidth, texHeight)) return false; ClearTexture(graphTexture, Color.clear);
        if (gridRawImage != null)
        {
            RectTransform gridRect = gridRawImage.GetComponent<RectTransform>(); if (gridRect != null)
            {
                int gridW = Mathf.RoundToInt(gridRect.rect.width); int gridH = Mathf.RoundToInt(gridRect.rect.height);
                if (InitializeTexture(gridRawImage, ref gridTexture, gridW, gridH)) { ClearTexture(gridTexture, Color.white); DrawGrid(gridTexture); } else { Debug.LogWarning("[GC] Failed init grid texture.", this); }
            }
            else { Debug.LogWarning("[GC] gridRawImage no RectTransform.", this); }
        }
        ResetGraphVisuals(); return true;
    }
    private bool InitializeTexture(RawImage targetImage, ref Texture2D textureRef, int width, int height)
    { if (targetImage == null) { Debug.LogError("[GC] Target RawImage null.", this); return false; } if (width <= 0 || height <= 0) { Debug.LogError($"[GC] Invalid texture dims {width}x{height}.", this); return false; } if (textureRef != null && (textureRef.width != width || textureRef.height != height)) { Destroy(textureRef); textureRef = null; } if (textureRef == null) { textureRef = new Texture2D(width, height, TextureFormat.RGBA32, false); textureRef.wrapMode = TextureWrapMode.Clamp; textureRef.filterMode = FilterMode.Bilinear; textureRef.name = $"{targetImage.name}_Texture"; } if (targetImage.texture != textureRef) { targetImage.texture = textureRef; } return true; }
    private void ClearTexture(Texture2D texture, Color color)
    { if (texture == null) return; Color[] pixels = texture.GetPixels(); for (int i = 0; i < pixels.Length; i++) { pixels[i] = color; } texture.SetPixels(pixels); texture.Apply(); }
    private void DrawGrid(Texture2D texture)
    { if (texture == null) return; int vLines = 11; int hLines = 11; float lPad = 58f; float bPad = 31f; float rPad = 10f; float tPad = 10f; float gridW = texture.width - lPad - rPad; float gridH = texture.height - bPad - tPad; if (gridW <= 0 || gridH <= 0) { return; } float vSpacing = (vLines > 1) ? gridW / (vLines - 1) : gridW; float hSpacing = (hLines > 1) ? gridH / (hLines - 1) : gridH; Color[] pixels = texture.GetPixels(); for (int i = 0; i < vLines; i++) { int x = Mathf.RoundToInt(lPad + i * vSpacing); DrawLinePixels(pixels, texture.width, texture.height, x, Mathf.RoundToInt(bPad), x, Mathf.RoundToInt(bPad + gridH), gridLineColor); } for (int i = 0; i < hLines; i++) { int y = Mathf.RoundToInt(bPad + i * hSpacing); DrawLinePixels(pixels, texture.width, texture.height, Mathf.RoundToInt(lPad), y, Mathf.RoundToInt(lPad + gridW), y, gridLineColor); } texture.SetPixels(pixels); texture.Apply(); }
    private void DrawLinePixels(Color[] pixels, int texWidth, int texHeight, int x0, int y0, int x1, int y1, Color color)
    { int dx = Mathf.Abs(x1 - x0); int dy = -Mathf.Abs(y1 - y0); int sx = x0 < x1 ? 1 : -1; int sy = y0 < y1 ? 1 : -1; int err = dx + dy; while (true) { if (x0 >= 0 && x0 < texWidth && y0 >= 0 && y0 < texHeight) { pixels[y0 * texWidth + x0] = color; } if (x0 == x1 && y0 == y1) break; int e2 = 2 * err; if (e2 >= dy) { if (x0 == x1) break; err += dy; x0 += sx; } if (e2 <= dx) { if (y0 == y1) break; err += dx; y0 += sy; } } }
    private void DrawPointPixels(Color[] pixels, int texWidth, int texHeight, int centerX, int centerY, Color color, int size)
    { if (pixels == null || size <= 0) return; int halfSize = size / 2; int startX = Mathf.Max(0, centerX - halfSize); int startY = Mathf.Max(0, centerY - halfSize); int endX = Mathf.Min(texWidth - 1, centerX + halfSize + (size % 2 == 0 ? -1 : 0)); int endY = Mathf.Min(texHeight - 1, centerY + halfSize + (size % 2 == 0 ? -1 : 0)); for (int px = startX; px <= endX; px++) { for (int py = startY; py <= endY; py++) { pixels[py * texWidth + px] = color; } } }
    private void ResetGraphVisuals()
    { if (graphRectTransform == null) return; graphRectTransform.sizeDelta = originalGraphSizeDelta; graphRectTransform.localScale = originalGraphScale; graphRectTransform.anchoredPosition = originalGraphAnchoredPosition; graphRectTransform.anchorMin = originalGraphAnchorMin; graphRectTransform.anchorMax = originalGraphAnchorMax; graphRectTransform.pivot = originalGraphPivot; currentGraphScaleX = 1f; currentGraphScaleY = 1f; previousMaxNormX = 0.1f; previousMaxNormY = 0.1f; if (graphTexture != null) { ClearTexture(graphTexture, Color.clear); } if (_currentState != GraphState.Preparing && _currentState != GraphState.Idle) { InitializeScaleLabels(); } }
    #endregion

    #region Scale Label Management
    private void InitializeScaleLabels()
    { UpdateScaleLabels(1.0f, 1.0f); }

    private void UpdateScaleLabels(float scaleFactorX, float scaleFactorY)
    {
        // maxScaled_RelativeStrain_Percent - макс. относительная деформация (%)
        // maxScaled_Force_kN_actual - макс. сила (кН) для актуальной площади
        // actualLength - фактическая длина образца в мм
        if (maxScaled_RelativeStrain_Percent < 0 || maxScaled_Force_kN_actual <= 0 || actualLength <= 0) return;

        if (horizontalScaleLabels != null && horizontalScaleLabels.Length > 0)
        {
            float visX_percent = (maxScaled_RelativeStrain_Percent > Mathf.Epsilon ? maxScaled_RelativeStrain_Percent : 0.1f) / scaleFactorX;
            float valX_percent_step = visX_percent / horizontalScaleLabels.Length;

            for (int i = 0; i < horizontalScaleLabels.Length; i++)
            {
                if (horizontalScaleLabels[i] != null)
                {
                    float currentLabelValue_percent = valX_percent_step * (i + 1);
                    float currentLabelValue_mm = (currentLabelValue_percent / 100.0f) * actualLength;
                    horizontalScaleLabels[i].text = FormatScaleValue(currentLabelValue_mm);
                }
            }
        }
        if (verticalScaleLabels != null && verticalScaleLabels.Length > 0)
        {
            float visY_kN = (maxScaled_Force_kN_actual > Mathf.Epsilon ? maxScaled_Force_kN_actual : 0.1f) / scaleFactorY;
            float valY_kN_step = visY_kN / verticalScaleLabels.Length;
            for (int i = 0; i < verticalScaleLabels.Length; i++)
            {
                if (verticalScaleLabels[i] != null) verticalScaleLabels[i].text = FormatScaleValue(valY_kN_step * (i + 1));
            }
        }
    }

    private string FormatScaleValue(float value)
    {
        CultureInfo culture = CultureInfo.InvariantCulture;
        string formattedValue;
        if (value == 0) formattedValue = "0";
        else if (Mathf.Abs(value) < 0.01f && value != 0) formattedValue = value.ToString("G3", culture);
        else if (Mathf.Abs(value) < 1f) formattedValue = value.ToString("F2", culture);
        else if (Mathf.Abs(value) < 100f) formattedValue = value.ToString("F1", culture);
        else formattedValue = value.ToString("F0", culture);
        return formattedValue;
    }
    #endregion

    #region Plotting Logic
    private IEnumerator PlotGraphCoroutine()
    {
        if (_currentState != GraphState.Ready) { Debug.LogError($"[GC] Plotting failed. State: {_currentState}", this); yield break; }
        SetState(GraphState.Plotting);
        Debug.Log("[GC] Plotting started...");
        Color[] graphPixels = graphTexture.GetPixels();
        int texWidth = graphTexture.width; int texHeight = graphTexture.height;
        bool pixelsChanged = false;
        CenterRawImageOnPoint(0, 0); // Центрируем на начальной точке

        float relativeStrainRate_PercentPerSec = 0;
        if (actualLength > 0 && testSpeed > 0)
        {
            float speedMmPerSec = testSpeed / 60.0f;
            relativeStrainRate_PercentPerSec = (speedMmPerSec / actualLength) * 100.0f;
        }
        else
        {
            Debug.LogError($"[GC] actualLength ({actualLength}) or testSpeed ({testSpeed}) is invalid for calculating strain rate. Plotting will be instant per point.");
        }

        float previous_X_RelativeStrainPercent_for_delay_calc = 0f;
        if (lastDrawnIndex > 0 && lastDrawnIndex <= originalGraphPoints_StrainPercent_StressMPa.Count)
        {
            previous_X_RelativeStrainPercent_for_delay_calc = originalGraphPoints_StrainPercent_StressMPa[lastDrawnIndex - 1].x;
        }

        // Константа для количества точек в отрисовке за один кадр
        const int POINTS_PER_BATCH = 1; // Количество точек в одной "пачке" для отрисовки
        float accumulatedDelay = 0f;    // Накопленная задержка для пачки

        for (int i = lastDrawnIndex; i < originalGraphPoints_StrainPercent_StressMPa.Count; i++)
        {
            while (isPaused)
            {
                if (pixelsChanged) { graphTexture.SetPixels(graphPixels); graphTexture.Apply(); pixelsChanged = false; }
                if (_currentState != GraphState.Paused) SetState(GraphState.Paused);
                yield return null;
            }
            if (_currentState != GraphState.Plotting) { Debug.Log($"[GC] Plotting interrupted. State: {_currentState}"); yield break; }

            float current_X_RelativeStrainPercent = originalGraphPoints_StrainPercent_StressMPa[i].x;
            float current_Y_Stress_MPa = originalGraphPoints_StrainPercent_StressMPa[i].y;

            // Рассчитываем текущую силу (кН) для АКТУАЛЬНОЙ площади
            float current_Y_Force_kN_actual = (current_Y_Stress_MPa * actualArea) / 1000.0f;
            SystemStateMonitor.Instance?.ReportGraphPlotPoint(current_X_RelativeStrainPercent, current_Y_Force_kN_actual);

            if (current_Y_Force_kN_actual > _machineForceLimit_kN && !_isLimitExceededEventSent)
            {
                // 1. Ставим флаг, чтобы событие отправилось только один раз
                _isLimitExceededEventSent = true;
                
                // 2. Просто сообщаем CSM о проблеме. Больше ничего не делаем.
                Debug.LogWarning($"[GraphController] Превышен лимит машины. Отправка события CSM.");
                EventManager.Instance?.RaiseEvent(EventType.MachineForceLimitReached, new EventArgs(this));
            }

            lastPlotPoint_RelativeStrain_Percent = current_X_RelativeStrainPercent;
            lastPlotPoint_Force_kN_actual = current_Y_Force_kN_actual;

            // Нормализация для отрисовки
            // Ось X нормализуется по максимальной относительной деформации
            // Ось Y нормализуется по максимальной силе (для актуальной площади)
            float normX = maxScaled_RelativeStrain_Percent > Mathf.Epsilon ? current_X_RelativeStrainPercent / maxScaled_RelativeStrain_Percent : 0f;
            float normY = maxScaled_Force_kN_actual > Mathf.Epsilon ? current_Y_Force_kN_actual / maxScaled_Force_kN_actual : 0f;
            normX = Mathf.Clamp01(normX);
            normY = Mathf.Clamp01(normY);

            float deltaX_Percent_step = current_X_RelativeStrainPercent - previous_X_RelativeStrainPercent_for_delay_calc;
            float currentStepDrawDelay = 0.001f;

            if (relativeStrainRate_PercentPerSec > Mathf.Epsilon && deltaX_Percent_step > 0)
            {
                currentStepDrawDelay = deltaX_Percent_step / relativeStrainRate_PercentPerSec;
            }
            else if (deltaX_Percent_step <= 0 && i > 0)
            {
                // Если деформация не увеличилась или уменьшилась, отрисовываем быстро
                currentStepDrawDelay = 0.001f;
            }

            const float minPracticalDelay = 0.001f;
            if (currentStepDrawDelay < minPracticalDelay)
            {
                currentStepDrawDelay = minPracticalDelay;
            }

            float availableWidthFactor = 1.0f - GraphPadding;
            int xPixel = Mathf.RoundToInt(normX * (texWidth * availableWidthFactor - 1));
            xPixel = Mathf.Clamp(xPixel, 0, texWidth - 1);
            float availableHeight = texHeight * (1.0f - GraphPadding);
            int yPixel = Mathf.RoundToInt(normY * (availableHeight - 1));
            yPixel = Mathf.Clamp(yPixel, 0, texHeight - 1);

            DrawPointPixels(graphPixels, texWidth, texHeight, xPixel, yPixel, plotColor, plotPointSize);
            pixelsChanged = true;
            CenterRawImageOnPoint(normX, normY);
            lastDrawnIndex = i + 1;
            previous_X_RelativeStrainPercent_for_delay_calc = current_X_RelativeStrainPercent;

            if (maxScaled_Force_kN_actual > 0) // Проверяем, что максимальная сила рассчитана
            {
                // Рассчитываем порог для установки от МАКСИМАЛЬНОЙ СИЛЫ
                float attachThresholdValue_kN = maxScaled_Force_kN_actual * EXTENSOMETER_ATTACH_THRESHOLD_PERCENT;

                // Рассчитываем порог для снятия от МАКСИМАЛЬНОЙ СИЛЫ
                float removeThresholdValue_kN = maxScaled_Force_kN_actual * EXTENSOMETER_REMOVE_THRESHOLD_PERCENT;

                // Условие установки
                if (!_extensometerAttachEventSent && current_Y_Force_kN_actual >= attachThresholdValue_kN)
                {                   
                    _extensometerAttachEventSent = true; // Ставим флаг, чтобы не вызывать событие снова
                    SystemStateMonitor.Instance?.ReportExtensometerEvent(true, _extensometerRemoveEventSent);
                }

                // Условие снятия
                if (_extensometerAttachEventSent && !_extensometerRemoveEventSent && current_Y_Force_kN_actual >= removeThresholdValue_kN)
                {                    
                    _extensometerRemoveEventSent = true; // Сначала ставим флаг
                    SystemStateMonitor.Instance?.ReportExtensometerEvent(_extensometerAttachEventSent, true); // Потом сообщаем
                }
            }

            /*if (EventManager.Instance != null)
            {
                TestProgressState currentProgressState = ConvertGraphStateToProgressState(GraphState.Plotting);
                // Передаем текущую относительную деформацию (%) и текущую силу (кН для актуальной площади)
                GraphStepUpdatedEventArgs stepArgs = new GraphStepUpdatedEventArgs(this, current_X_RelativeStrainPercent, current_Y_Force_kN_actual, currentProgressState);
                EventManager.Instance.RaiseEvent(EventType.GraphStepUpdated, stepArgs);
            }
            else { Debug.LogError("[GC] EventManager null! Cannot raise GraphStepUpdated."); }*/

            // Накапливаем задержку для текущей точки
            accumulatedDelay += currentStepDrawDelay;

            // Проверяем, пора ли обновить текстуру на GPU
            bool isLastPoint = (i == originalGraphPoints_StrainPercent_StressMPa.Count - 1);
            if ((i + 1) % POINTS_PER_BATCH == 0 || isLastPoint)
            {
                if (pixelsChanged)
                {
                    graphTexture.SetPixels(graphPixels);
                    graphTexture.Apply(); // Вызываем дорогую операцию только здесь
                    pixelsChanged = false;
                }

                // Ждем всё накопленное время
                if (accumulatedDelay > 0.001f)
                {
                    yield return new WaitForSeconds(accumulatedDelay);
                }
                else
                {
                    // Если задержка очень мала, просто ждем следующий кадр
                    yield return null;
                }
                accumulatedDelay = 0f; // Сбрасываем счетчик задержки
                EventManager.Instance?.RaiseEvent(EventType.GraphStepUpdated, EventArgs.Empty); // Отправляем событие для обновления TC и CSM
            }
        }

        Debug.Log("[GC] Plotting finished.");
        SetState(GraphState.Finished);
        plotCoroutine = null;
    }

    private void CenterRawImageOnPoint(float normX, float normY)
    { if (graphRectTransform == null) return; previousMaxNormX = Mathf.Max(previousMaxNormX, normX); previousMaxNormY = Mathf.Max(previousMaxNormY, normY); float targetNormX = Mathf.Clamp01(previousMaxNormX * 1.05f); float targetNormY = Mathf.Clamp01(previousMaxNormY * 1.05f); targetNormX = Mathf.Max(targetNormX, 0.01f); targetNormY = Mathf.Max(targetNormY, 0.01f); currentGraphScaleX = 1.0f / targetNormX; currentGraphScaleY = 1.0f / targetNormY; graphRectTransform.localScale = new Vector3(currentGraphScaleX, currentGraphScaleY, 1f); if (graphRectTransform.anchorMin != Vector2.zero || graphRectTransform.anchorMax != Vector2.zero || graphRectTransform.pivot != Vector2.zero) { graphRectTransform.anchorMin = Vector2.zero; graphRectTransform.anchorMax = Vector2.zero; graphRectTransform.pivot = Vector2.zero; } if (graphRectTransform.anchoredPosition != Vector2.zero) { graphRectTransform.anchoredPosition = Vector2.zero; } UpdateScaleLabels(currentGraphScaleX, currentGraphScaleY); }
    #endregion

    #region Public Control Methods
    public bool StartPlotting()
    {
        if (_currentState == GraphState.Ready)
        {
            if (plotCoroutine != null) { StopPlottingCoroutine(); }
            lastDrawnIndex = 0;
            lastPlotPoint_RelativeStrain_Percent = 0f;
            lastPlotPoint_Force_kN_actual = 0f;
            plotCoroutine = StartCoroutine(PlotGraphCoroutine()); return true;
        }
        else { Debug.LogWarning($"[GC] Cannot StartPlotting. State: {_currentState}", this); return false; }
    }
    public void PausePlotting()
    { if (_currentState == GraphState.Plotting) { isPaused = true; Debug.Log("[GC] Pausing plot."); } else { Debug.LogWarning($"[GC] Cannot PausePlotting. State: {_currentState}", this); } }
    public void ResumePlotting()
    { if (_currentState == GraphState.Paused) { isPaused = false; SetState(GraphState.Plotting); Debug.Log("[GC] Resuming plot."); } else { Debug.LogWarning($"[GC] Cannot ResumePlotting. State: {_currentState}", this); } }

    public void ResetGraphAndSimulation()
    {
        StopPlottingCoroutine();
        lastDrawnIndex = 0;
        isPaused = false;

        _currentGraphDataFile = null;
        _proportionalityLimit_kN_ForEvents = float.NaN;
        standardInitialLengthFromMaterial = 1f;
        standardInitialAreaFromMaterial = 1f; // Оставляем на случай, если понадобится для чего-то еще
        actualLength = 1f; // Сброс актуальных размеров
        actualArea = 1f;
        testSpeed = 1f;

        _extensometerAttachEventSent = false;
        _extensometerRemoveEventSent = false;

        originalGraphPoints_StrainPercent_StressMPa.Clear();
        maxOriginal_RelativeStrain_Percent = 1f;
        maxOriginal_Stress_MPa = 1f;
        maxScaled_RelativeStrain_Percent = 1f;
        maxScaled_Force_kN_actual = 1f;

        lastPlotPoint_RelativeStrain_Percent = 0f;
        lastPlotPoint_Force_kN_actual = 0f;

        X_UltimateStrength_Percent = -1f; // Сброс найденных ключевых точек
        X_Rupture_Percent = -1f;
        _ruptureStressThreshold_MPa_from_material = -1f;

        ResetGraphVisuals();
        /*if (EventManager.Instance != null)
        {
            TestProgressState resetProgressState = ConvertGraphStateToProgressState(GraphState.Idle);
            // Передаем 0 для % деформации и 0 для силы кН при сбросе
            GraphStepUpdatedEventArgs resetArgs = new GraphStepUpdatedEventArgs(this, 0f, 0f, resetProgressState);
            EventManager.Instance.RaiseEvent(EventType.GraphStepUpdated, resetArgs);
        }
        else { Debug.LogError("[GC] EventManager null! Cannot raise Idle event."); }*/
        SystemStateMonitor.Instance?.ReportGraphPlotPoint(0f, 0f);
        EventManager.Instance.RaiseEvent(EventType.GraphStepUpdated, EventArgs.Empty);
        SetState(GraphState.Idle);
    }
    #endregion

    #region State Management
    private void SetState(GraphState newState)
    {
        if (_currentState == newState) return;
        GraphState previousState = _currentState; _currentState = newState;
        SystemStateMonitor.Instance?.ReportGraphState(_currentState);

        // При изменении состояния на Paused, Finished, Error отправляем последние актуальные значения
        if (newState == GraphState.Paused || newState == GraphState.Finished || newState == GraphState.Error)
        {
            /*if (EventManager.Instance != null)
            {
                TestProgressState currentProgressState = ConvertGraphStateToProgressState(newState);
                // Используем lastPlotPoint_RelativeStrain_Percent и lastPlotPoint_Force_kN_actual
                GraphStepUpdatedEventArgs eventArgs = new GraphStepUpdatedEventArgs(this, lastPlotPoint_RelativeStrain_Percent, lastPlotPoint_Force_kN_actual, currentProgressState);
                EventManager.Instance.RaiseEvent(EventType.GraphStepUpdated, eventArgs);
            }
            else { Debug.LogError($"[GC] EventManager null! Cannot raise state change event for {newState}."); }*/
            EventManager.Instance.RaiseEvent(EventType.GraphStepUpdated, EventArgs.Empty);
            //if (newState == GraphState.Finished) { OnPlotComplete?.Invoke(); }
        }
    }

    private TestProgressState ConvertGraphStateToProgressState(GraphController.GraphState graphState)
    {
        switch (graphState)
        {
            case GraphController.GraphState.Plotting: return TestProgressState.Running;
            case GraphController.GraphState.Paused: return TestProgressState.Paused;
            case GraphController.GraphState.Finished: return TestProgressState.Finished;
            case GraphController.GraphState.Error: return TestProgressState.Error;
            case GraphController.GraphState.Idle:
            case GraphController.GraphState.Preparing:
            case GraphController.GraphState.Ready:
            default: return TestProgressState.Idle;
        }
    }
    #endregion

    #region Helper Methods
    private void StopPlottingCoroutine()
    { if (plotCoroutine != null) { StopCoroutine(plotCoroutine); plotCoroutine = null; } }
    #endregion
    public Texture2D GetGraphTexture()
    {
        return graphTexture;
    }
}