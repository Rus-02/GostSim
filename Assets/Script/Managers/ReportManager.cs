using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

// ===================================================================================
// Перечисление для внутренних флагов, чтобы код был читаемым.
// ===================================================================================
public enum ReportableField
{
    YieldStrength,
    UltimateTensileStrength,
    ProportionalityLimit,
    ReductionOfArea,
    Elongation,
}

// ===================================================================================
// Класс-контейнер для хранения всех "замороженных" данных одного готового отчета.
// ===================================================================================
public class ReportData
{
    // --- Поля для БОЛЬШОГО отчета (таблица) ---
    public float InitialWorkingLength_mm { get; set; }
    public float InitialSectionArea_mm2 { get; set; }
    public float? BaseLength_mm { get; set; }
    public float? FinalLength_mm { get; set; }
    public float? MaxForce_kN { get; set; }
    public float? YieldStrength_MPa { get; set; }
    public float? UltimateTensileStrength_MPa { get; set; }
    public float? ProportionalityLimit_MPa { get; set; }
    public float? ModulusOfElasticity_MPa { get; set; }

    // --- Поля, нужные для ОБОИХ отчетов ---
    public string MaterialDisplayName { get; set; }
    public string TestStandard { get; set; } // Методика испытания
    public float? ElongationAtBreak_Percent { get; set; } // Относительное удлинение
    public float? ReductionOfArea_Percent { get; set; } // Относительное сужение
    public string InitialSize_mm { get; set; } // Рабочий размер образца (мм)
    public float? Average_Density { get; set; } // Средняя плотность образца (кг/м3)
    public float? Weight_g { get; set; } // Масса образца (г)

    // --- Поля для дополнительной информации (Большой отчет) ---
    public string Notes { get; set; }

    // --- Визуальные данные (Большой отчет) ---
    public Texture2D GraphTexture { get; set; }
    public bool WasExtensometerUsed;
}


// ===================================================================================
// Основной класс. Собирает, обрабатывает и отображает данные отчета.
// ===================================================================================
public class ReportManager : MonoBehaviour
{
    #region Singleton & UI Fields
    public static ReportManager Instance { get; private set; }

    [Header("Report Configurations")]
    [Tooltip("Список всех доступных конфигураций отчетов.")]
    public List<ReportConfiguration> _allReportConfigurations; // <-- Список всех конфигураций
    [HideInInspector]
    public ReportConfiguration CurrentReportConfiguration; // Выбранная конфигурация

    [Header("UI Panels")]
    [SerializeField] private GameObject smallReportPanel;
    [SerializeField] private GameObject bigReportPanel;

    [Header("Small Report Fields (Общие)")]
    [SerializeField] private TextMeshProUGUI small_TestStandardText;
    [SerializeField] private TextMeshProUGUI small_MaterialNameText;

    [Header("Big Report Table Structure")]
    [Tooltip("Transform первой строки (заголовки) таблицы большого отчета.")]
    [SerializeField] private Transform headerRow; // Ссылка на Transform первой строки (заголовки)
    [Tooltip("Transform второй строки (данные) таблицы большого отчета.")]
    [SerializeField] private Transform dataRow;    // Ссылка на Transform второй строки (данные)

    [Header("Small Report Table Structure")]
    [Tooltip("Transform первой колонки таблицы маленького отчета.")]
    [SerializeField] private Transform smallColumn1; // Ссылка на Transform первой колонки
    [Tooltip("Transform второй колонки таблицы маленького отчета.")]
    [SerializeField] private Transform smallColumn2; // Ссылка на Transform второй колонки

    [Header("Big Report Additional Info")]
    [SerializeField] private TextMeshProUGUI big_TestStandardText;
    [SerializeField] private TextMeshProUGUI big_MaterialNameText;
    [SerializeField] private TextMeshProUGUI big_NotesText;

    [Header("Big Report Graph & Controls")]
    [SerializeField] private RectTransform graphAreaToCapture;
    [SerializeField] private RawImage big_GraphImage;
    [SerializeField] private Button printButton;
    [SerializeField] private Button hideBigReportButton;

    [SerializeField] private Button showBigReportButton; // Добавлено, так как использовалось
    #endregion

    #region Internal State
    private ReportData _lastFinalizedReport; // Готовый отчет
    #endregion

    #region Unity Lifecycle & Subscriptions
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        SubscribeToEventsAndCommands();
        
        if (DataManager.Instance != null) { _allReportConfigurations = DataManager.Instance.AllReportConfigs; }
        else { Debug.LogError("[ReportManager] DataManager не найден!"); _allReportConfigurations = new List<ReportConfiguration>(); }

        if (showBigReportButton != null) showBigReportButton.onClick.AddListener(OnShowBigReportClicked);
        if (hideBigReportButton != null) hideBigReportButton.onClick.AddListener(OnShowSmallReportClicked);
        if (printButton != null) printButton.onClick.AddListener(OnPrintButtonClick);
        smallReportPanel.SetActive(false);
        bigReportPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        UnsubscribeFromEventsAndCommands();
    }

    private void SubscribeToEventsAndCommands()
    {
        var tm = ToDoManager.Instance;
        
        // Подписываемся только на команды управления от CSM
        if (tm != null)
        {
            tm.SubscribeToAction(ActionType.FinalizeTestData, HandleFinalizeTestData); // Главная команда
            tm.SubscribeToAction(ActionType.ClearLastReport, HandleClearLastReport);
            tm.SubscribeToAction(ActionType.ShowSmallReport, HandleShowSmallReport);
            tm.SubscribeToAction(ActionType.ShowBigReport, HandleShowBigReport);
        }
    }

    private void UnsubscribeFromEventsAndCommands()
    {
        var tm = ToDoManager.Instance;

        if (tm != null)
        {
            tm.UnsubscribeFromAction(ActionType.FinalizeTestData, HandleFinalizeTestData);
            tm.UnsubscribeFromAction(ActionType.ClearLastReport, HandleClearLastReport);
            tm.UnsubscribeFromAction(ActionType.ShowSmallReport, HandleShowSmallReport);
            tm.UnsubscribeFromAction(ActionType.ShowBigReport, HandleShowBigReport);
        }
    }
    #endregion

    #region Data Gathering Handlers
private void HandleFinalizeTestData(BaseActionArgs args)
    {
        if (_lastFinalizedReport != null) return;

        var monitor = SystemStateMonitor.Instance;
        if (monitor == null || monitor.CurrentTestConfig == null || monitor.SelectedMaterial == null) return;

        CurrentReportConfiguration = _allReportConfigurations.FirstOrDefault(rc => rc.LinkedTest == monitor.CurrentTestConfig);
        if (CurrentReportConfiguration == null) return;

        var finalReport = CurrentReportConfiguration.CreateReportData();
        var materialProps = monitor.SelectedMaterial;
        
        // --- 1. БАЗА (Всегда) ---
        finalReport.TestStandard = monitor.SelectedTemplateName;
        finalReport.MaterialDisplayName = monitor.SelectedMaterialName;
        finalReport.InitialSectionArea_mm2 = monitor.CalculatedArea;
        
        // Размеры
        float effectiveLength = 0f;
        if (monitor.CurrentSampleParameters.TryGetValue("Length", out float len)) effectiveLength = len;
        else if (monitor.CurrentSampleParameters.TryGetValue("DiameterThickness", out float dt)) effectiveLength = dt;
        finalReport.InitialWorkingLength_mm = effectiveLength;

        if (monitor.CurrentSampleParameters.ContainsKey("Width") && monitor.CurrentSampleParameters.ContainsKey("DiameterThickness"))
             finalReport.InitialSize_mm = $"{effectiveLength:0.##} x {monitor.CurrentSampleParameters["Width"]:0.##} x {monitor.CurrentSampleParameters["DiameterThickness"]:0.##}";
        else
             finalReport.InitialSize_mm = effectiveLength.ToString("F2");

        // Макс. сила машины (Всегда факт)
        finalReport.MaxForce_kN = monitor.MaxForceInTest_kN;
        
        // Константы материала (Всегда)
        finalReport.ModulusOfElasticity_MPa = ApplyRandomScatter(materialProps.modulusOfElasticityE_MPa);
        finalReport.Average_Density = materialProps.averageDensity_kg_per_m3;

        // Вес
        if (monitor.CalculatedArea > 0 && effectiveLength > 0 && materialProps.averageDensity_kg_per_m3 > 0)
        {
            float volume_mm3 = monitor.CalculatedArea * effectiveLength;
            float density_g_per_mm3 = materialProps.averageDensity_kg_per_m3 * 1e-6f;
            finalReport.Weight_g = ApplyRandomScatter(volume_mm3 * density_g_per_mm3);
        }

        // Экстензометр (база)
        finalReport.WasExtensometerUsed = monitor.IsExtensometerEnabledByUser;
        finalReport.BaseLength_mm = finalReport.WasExtensometerUsed ? (float?)CurrentReportConfiguration.ExtensometerBaseLength_mm : null;


        // --- 2. РАСЧЕТНЫЕ СВОЙСТВА (По факту достижения) ---

        // Флаг полного успеха (разрыв)
        bool isFullSuccess = (monitor.CurrentTestState == TestState.TestResult_SampleSafe);

        // А. Предел Пропорциональности / Текучести
        // Проверяем: вычислил ли Монитор эти точки до остановки?
        // (Предполагаем, что monitor.ProportionalityLimit_kN > 0, если точка пройдена)
        if (monitor.ProportionalityLimit_kN > 0 && finalReport.InitialSectionArea_mm2 > 0)
        {
            // Считаем по факту
            float propForce_N = monitor.ProportionalityLimit_kN * 1000f;
            finalReport.ProportionalityLimit_MPa = propForce_N / finalReport.InitialSectionArea_mm2;
            
            // Если прошли пропорциональность, обычно считаем и текучесть (или берем из материала)
            finalReport.YieldStrength_MPa = ApplyRandomScatter(materialProps.yieldStrength_MPa);
        }
        else
        {
            // Не дошли до зоны упругости
            finalReport.ProportionalityLimit_MPa = null;
            finalReport.YieldStrength_MPa = null;
        }

        // Валидация экстензометра (если требовался, но не использовали - сбрасываем точность)
        if (!monitor.WasExtensometerAttachRequested) finalReport.YieldStrength_MPa = null;
        if (!monitor.WasExtensometerRemoveRequested) finalReport.ProportionalityLimit_MPa = null;


        // Б. Предел Прочности (UTS)
        // 1. Либо тест завершен полностью (разрыв).
        // 2. Либо мы преодолели точку пика (текущая деформация > деформации при UTS).
        bool hasPassedUTS = (monitor.UTS_RelativeStrain_Percent > 0 && 
                             monitor.CurrentRelativeStrain_Percent >= monitor.UTS_RelativeStrain_Percent);

        if (isFullSuccess || hasPassedUTS)
        {
            // Мы знаем реальный предел прочности.
            // Считаем его как Макс.Сила / Площадь. 
            // (MaxForceInTest_kN уже зафиксировал этот пик).
            
            // Ноль защиты, если площадь вдруг 0
            if (finalReport.InitialSectionArea_mm2 > 0 && finalReport.MaxForce_kN.HasValue)
            {
                float maxForce_N = finalReport.MaxForce_kN.Value * 1000f;
                finalReport.UltimateTensileStrength_MPa = maxForce_N / finalReport.InitialSectionArea_mm2;
            }
            else
            {
                // Фолбэк на материал, если что-то с данными не так, но лучше null
                finalReport.UltimateTensileStrength_MPa = ApplyRandomScatter(materialProps.ultimateTensileStrength_MPa); 
            }
        }
        else
        {
            // Не дошли до пика -> Прочерк
            finalReport.UltimateTensileStrength_MPa = null;
        }


        // В. Пластичность (Разрыв)
        // Только если порвали
        if (isFullSuccess)
        {
            finalReport.ElongationAtBreak_Percent = ApplyRandomScatter(materialProps.elongationAtBreak_Percent);
            finalReport.ReductionOfArea_Percent = ApplyRandomScatter(materialProps.reductionOfArea_Percent);

            if (finalReport.ElongationAtBreak_Percent.HasValue && effectiveLength > 0)
            {
                finalReport.FinalLength_mm = effectiveLength * (1 + finalReport.ElongationAtBreak_Percent.Value / 100f);
            }
        }
        else
        {
            finalReport.ElongationAtBreak_Percent = null;
            finalReport.ReductionOfArea_Percent = null;
            finalReport.FinalLength_mm = null;
        }

        _lastFinalizedReport = finalReport;
        Debug.Log($"[ReportManager] Отчет сформирован. Успех: {isFullSuccess}");
    }

    private void SetPropertyIfAvailable(object obj, string propertyName, object value)
    {
        PropertyInfo prop = obj.GetType().GetProperty(propertyName);
        if (prop != null && prop.CanWrite)
        {
            if (value != null && !prop.PropertyType.IsAssignableFrom(value.GetType()))
            {
                Debug.LogWarning($"Тип значения для '{propertyName}' не совместим. Ожидался: {prop.PropertyType}, Получен: {value.GetType()}");
                return;
            }
            prop.SetValue(obj, value);
        }
    }
    #endregion

    #region Report Finalization & UI Control
    private void HandleClearLastReport(BaseActionArgs args)
    {
        _lastFinalizedReport = null;
        smallReportPanel?.SetActive(false);
        bigReportPanel?.SetActive(false);
    }

    private float? ApplyRandomScatter(float? value, float scatterPercent = 0.005f) // 0.005f = 0.5%
    {
        if (!value.HasValue || value.Value == 0)
        {
            return value;
        }

        float randomMultiplier = UnityEngine.Random.Range(1f - scatterPercent, 1f + scatterPercent);

        return value.Value * randomMultiplier;
    }

    private void OnShowBigReportClicked() => ToDoManager.Instance?.HandleAction(ActionType.ShowBigReport, null);
    private void OnShowSmallReportClicked() => ToDoManager.Instance?.HandleAction(ActionType.ShowSmallReport, null);

    private void HandleShowSmallReport(BaseActionArgs args)
    {
        Debug.Log($"ReportManager.HandleShowSmallReport: Action received. _lastFinalizedReport is {(_lastFinalizedReport != null ? "NOT NULL" : "NULL")}");
        if (_lastFinalizedReport != null)
        {
            Debug.Log($"ReportManager.HandleShowSmallReport: Filling UI with report type {_lastFinalizedReport.GetType().Name}");
            FillSmallReportUI(_lastFinalizedReport);
            smallReportPanel?.SetActive(true);
            bigReportPanel?.SetActive(false);
            Debug.Log("ReportManager.HandleShowSmallReport: Small report panel activated, big report panel deactivated.");
        }
        else
        {
            Debug.LogWarning("ReportManager.HandleShowSmallReport: _lastFinalizedReport is null, cannot show report.");
        }
    }

    private void HandleShowBigReport(BaseActionArgs args)
    {
        if (_lastFinalizedReport != null)
        {
            StartCoroutine(CaptureAndShowReportCoroutine());
        }
        else
        {
            Debug.LogWarning("ReportManager.HandleShowBigReport: _lastFinalizedReport is null, cannot show report.");
        }
    }

    private IEnumerator CaptureAndShowReportCoroutine()
    {
        if (ToDoManager.Instance != null) { ToDoManager.Instance.HandleAction(ActionType.ClearHints, null); }

        yield return new WaitForEndOfFrame();

        if (graphAreaToCapture == null)
        {
            Debug.LogError("Не указан RectTransform для захвата графика (graphAreaToCapture)!");
            yield break;
        }

        Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();

        if (screenshot == null)
        {
            Debug.LogError("Не удалось создать скриншот. Возможно, проблема с платформой или графическим API.");
            yield break;
        }

        Vector3[] corners = new Vector3[4];
        graphAreaToCapture.GetWorldCorners(corners);

        int x = (int)corners[0].x;
        int y = (int)corners[0].y;
        int width = (int)(corners[2].x - corners[0].x);
        int height = (int)(corners[2].y - corners[0].y);

        if (x < 0) x = 0;
        if (y < 0) y = 0;
        if (x + width > screenshot.width) width = screenshot.width - x;
        if (y + height > screenshot.height) height = screenshot.height - y;

        Texture2D croppedTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
        croppedTexture.SetPixels(screenshot.GetPixels(x, y, width, height));
        croppedTexture.Apply();
        Destroy(screenshot);

        _lastFinalizedReport.GraphTexture = croppedTexture;
        FillBigReportUI(_lastFinalizedReport);

        smallReportPanel?.SetActive(false);
        bigReportPanel?.SetActive(true);
        Debug.Log("ReportManager: Отчет с захваченным графиком показан.");
    }

    private void OnPrintButtonClick() { Debug.Log("Печать отчета..."); }
    #endregion

    #region UI Filling Methods (Dynamic with Show/Hide)

    private void FillSmallReportUI(ReportData data)
    {
        const string notAvailable = "---";
        small_TestStandardText.text = data.TestStandard ?? notAvailable;
        small_MaterialNameText.text = data.MaterialDisplayName ?? notAvailable;

        if (CurrentReportConfiguration == null)
        {
            Debug.LogError("CurrentReportConfiguration не задана при заполнении краткого отчета.");
            return;
        }

        var shortFields = CurrentReportConfiguration.ShortReportFields;
        int totalFields = shortFields.Count;

        if (totalFields > 6)
        {
            Debug.LogWarning($"В ShortReportFields указано {totalFields} полей, но таблица рассчитана максимум на 6. Лишние поля будут проигнорированы.");
            totalFields = 6;
        }

        for (int i = 0; i < 6; i++)
        {
            if (smallColumn1.childCount > i)
                smallColumn1.GetChild(i).gameObject.SetActive(false);
            if (smallColumn2.childCount > i)
                smallColumn2.GetChild(i).gameObject.SetActive(false);
        }

        for (int i = 0; i < totalFields; i++)
        {
            var fieldConfig = shortFields[i];

            if (smallColumn1.childCount > i && smallColumn2.childCount > i)
            {
                Transform labelCellTransform = smallColumn1.GetChild(i);
                Transform valueCellTransform = smallColumn2.GetChild(i);

                TextMeshProUGUI labelTextUI = labelCellTransform.GetComponentInChildren<TextMeshProUGUI>(true);
                TextMeshProUGUI valueTextUI = valueCellTransform.GetComponentInChildren<TextMeshProUGUI>(true);

                if (labelTextUI != null)
                {
                    labelTextUI.text = fieldConfig.Label ?? notAvailable;
                    labelCellTransform.gameObject.SetActive(true);
                }
                else
                {
                    Debug.LogWarning($"TextMeshProUGUI (дочерний) не найден в ячейке метки строки {i} (имя: {labelCellTransform.name}).");
                    labelCellTransform.gameObject.SetActive(false);
                }

                if (valueTextUI != null)
                {
                    PropertyInfo dataProperty = data.GetType().GetProperty(fieldConfig.DataKey);
                    if (dataProperty != null)
                    {
                        object value = dataProperty.GetValue(data);
                        if (value != null)
                        {
                            try
                            {
                                valueTextUI.text = string.Format($"{{0:{fieldConfig.Format}}}", value);
                            }
                            catch (FormatException)
                            {
                                valueTextUI.text = value.ToString();
                                Debug.LogWarning($"Неверный формат '{fieldConfig.Format}' для поля '{fieldConfig.DataKey}' со значением '{value}'.");
                            }
                        }
                        else
                        {
                            valueTextUI.text = notAvailable;
                        }
                    }
                    else
                    {
                        valueTextUI.text = notAvailable;
                        Debug.LogWarning($"Свойство '{fieldConfig.DataKey}' не найдено в ReportData для отображения значения в кратком отчете.");
                    }
                    valueCellTransform.gameObject.SetActive(true);
                }
                else
                {
                    Debug.LogWarning($"TextMeshProUGUI (дочерний) не найден в ячейке значения строки {i} (имя: {valueCellTransform.name}).");
                    valueCellTransform.gameObject.SetActive(false);
                }
            }
            else
            {
                Debug.LogWarning($"Недостаточно дочерних элементов в одной из колонок для отображения строки {i}.");
            }
        }
    }

    private void FillBigReportUI(ReportData data)
    {
        const string notAvailable = "---";

        if (CurrentReportConfiguration == null)
        {
            Debug.LogError("CurrentReportConfiguration не задана при заполнении большого отчета.");
            return;
        }

        var tableColumns = CurrentReportConfiguration.TableColumns;
        int totalColumns = tableColumns.Count;

        for (int i = 0; i < 11; i++)
        {
            if (headerRow.childCount > i)
                headerRow.GetChild(i).gameObject.SetActive(false);
            if (dataRow.childCount > i)
                dataRow.GetChild(i).gameObject.SetActive(false);
        }

        for (int i = 0; i < totalColumns && i < 11; i++)
        {
            var columnConfig = tableColumns[i];

            if (columnConfig.DataKey == "BaseLength_mm")
            {
                var monitor = SystemStateMonitor.Instance;
                if (monitor != null && (!monitor.WasExtensometerRemoveRequested))
                {
                    continue; 
                }
            }

            if (headerRow.childCount > i && dataRow.childCount > i)
            {
                Transform headerCell = headerRow.GetChild(i);
                Transform dataCell = dataRow.GetChild(i);

                TextMeshProUGUI headerTextUI = headerCell.GetComponentInChildren<TextMeshProUGUI>(true);
                TextMeshProUGUI dataTextUI = dataCell.GetComponentInChildren<TextMeshProUGUI>(true);

                if (headerTextUI != null)
                {
                    headerTextUI.text = columnConfig.HeaderText;
                    headerCell.gameObject.SetActive(true);
                }
                else
                {
                    Debug.LogWarning($"TextMeshProUGUI не найден в заголовке столбца {i}.");
                    headerCell.gameObject.SetActive(false);
                }

                if (dataTextUI != null)
                {
                    PropertyInfo dataProperty = data.GetType().GetProperty(columnConfig.DataKey);
                    if (dataProperty != null)
                    {
                        object value = dataProperty.GetValue(data);
                        if (value != null)
                        {
                            try
                            {
                                dataTextUI.text = string.Format($"{{0:{columnConfig.Format}}}", value);
                            }
                            catch (FormatException)
                            {
                                dataTextUI.text = value.ToString();
                                Debug.LogWarning($"Неверный формат '{columnConfig.Format}' для поля '{columnConfig.DataKey}' со значением '{value}'.");
                            }
                        }
                        else
                        {
                            dataTextUI.text = notAvailable;
                        }
                    }
                    else
                    {
                        dataTextUI.text = "---";
                        Debug.LogError($"Property with key '{columnConfig.DataKey}' not found in ReportData.");
                    }
                    dataCell.gameObject.SetActive(true);
                }
                else
                {
                    Debug.LogWarning($"TextMeshProUGUI не найден в данных столбца {i}.");
                    dataCell.gameObject.SetActive(false);
                }
            }
            else
            {
                Debug.LogWarning($"Недостаточно дочерних элементов в строках для отображения столбца {i}.");
            }
        }

        big_TestStandardText.text = data.TestStandard ?? notAvailable;
        big_MaterialNameText.text = data.MaterialDisplayName ?? notAvailable;
        big_NotesText.text = !string.IsNullOrEmpty(data.Notes) ? data.Notes : notAvailable;

        if (big_GraphImage != null)
        {
            big_GraphImage.texture = data.GraphTexture;
            big_GraphImage.color = data.GraphTexture != null ? Color.white : Color.clear;
        }

        bigReportPanel.SetActive(true);
    }
    #endregion
}