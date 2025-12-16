using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Globalization;

public class SetupPanelController : MonoBehaviour
{
    #region Singleton
    private static SetupPanelController _instance;
    public static SetupPanelController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<SetupPanelController>();
                if (_instance == null)
                {
                    GameObject singletonObject = new GameObject("SetupPanelController_Auto");
                    _instance = singletonObject.AddComponent<SetupPanelController>();
                }
            }
            return _instance;
        }
    }
    private void Awake() { if (_instance != null && _instance != this) { Destroy(gameObject); return; } _instance = this; }
    #endregion

    #region UI Elements
    [Header("UI Panel")]
    public GameObject setupPanelUI;
    public GameObject buttonsContainer;

    [Header("Template Selection Panel")]
    public TMP_Dropdown templateNameDropdown;
    public TextMeshProUGUI sampleDisplayNameText;

    [Header("Material Selection Panel")]
    public TMP_Dropdown materialDropdown;

    [Header("Shape Selection")]
    public TMP_Dropdown shapeTypeDropdown;

    [Header("Parameter Input Fields (Legacy - will be managed by RebuildUI)")]
    public TextMeshProUGUI diameterThicknessLabel;
    public TMP_InputField diameterThicknessInputField;
    public TMP_Dropdown diameterThicknessDropdown;
    public TextMeshProUGUI thicknessErrorMessageText;

    public TextMeshProUGUI widthLabel;
    public TMP_InputField widthInputField;
    public TMP_Dropdown widthDropdown;
    public TextMeshProUGUI widthErrorMessageText;

    public TextMeshProUGUI lengthLabel;
    public TMP_InputField lengthInputField;
    public TMP_Dropdown lengthDropdown;
    public TextMeshProUGUI lengthErrorMessageText;

    public TMP_InputField speedInputField;
    public TMP_Dropdown speedModeDropdown;
    public TextMeshProUGUI speedErrorMessageText;

    public TMP_InputField areaInputField;

    [Header("Test Specific UI")]
    public GameObject tensileToleranceFieldsParent;

    private Dictionary<string, TMP_Text> _parameterLabels = new Dictionary<string, TMP_Text>();
    private Dictionary<string, TMP_InputField> _parameterInputFields = new Dictionary<string, TMP_InputField>();
    private Dictionary<string, TMP_Dropdown> _parameterDropdowns = new Dictionary<string, TMP_Dropdown>();
    private Dictionary<string, TextMeshProUGUI> _parameterErrorTexts = new Dictionary<string, TextMeshProUGUI>();


    [Header("Error Message Texts")]
    public TextMeshProUGUI TestSettingMessageText;

    [Header("Sample Header Info (For Table)")]
    public TMP_InputField groupNameInputField;
    public TMP_InputField batchNumberInputField;
    public TMP_InputField markingInputField;
    public TMP_InputField notesInputField;

    [Header("Speed Settings (Validation Limits)")]
    public float minSpeed = 2f;
    public float maxSpeed = 100f;
    #endregion

    #region Private Fields
    private SampleForm currentShapeType = SampleForm.Неопределено;
    private string currentTemplateName;
    private TestConfigurationData currentTestData;

    private ITestLogicHandler _currentTestLogicHandler;
    private SampleData _selectedSampleDataForUIConfig;
    private MaterialPropertiesAsset _selectedMaterialPropertiesAsset;
    private TestConfigurationData _currentTestLogicHandler_AssociatedConfigData;

    private List<MaterialPropertiesAsset> _allLoadedMaterials; // Кэш всех загруженных материалов
    private List<MaterialPropertiesAsset> _currentlyDisplayedMaterials; // Материалы, отображаемые в дропдауне после фильтрации

    private const float SIMULATION_SPEED_MULTIPLIER = 10.0f;
    private bool isTemplateDropdownActive = false;
    private bool isButtonsContainerActive = false;
    private StateSnapshot _stateBeforeEdit; 
    #endregion

    void Start()
    {
        InitializeUI();
        SetupStaticEventListeners();
        LoadInitialData();
        SubscribeToDoManagerActions();
        MapLegacyUIElements();
        LoadAllMaterials();
    }

    private void OnDestroy() { UnsubscribeFromToDoManagerActions(); }

    #region Initialization
    void InitializeUI()
    {
        SetVisibilityForAllParameterFields(false);

        if (shapeTypeDropdown != null) shapeTypeDropdown.interactable = false;
        if (areaInputField != null) areaInputField.interactable = false;
        if (speedModeDropdown != null) speedModeDropdown.gameObject.SetActive(false);

        if (TestSettingMessageText != null) TestSettingMessageText.gameObject.SetActive(false);
        if (setupPanelUI != null) setupPanelUI.SetActive(false);
        if (buttonsContainer != null) buttonsContainer.SetActive(false);
        isButtonsContainerActive = false;

        if (tensileToleranceFieldsParent != null)
        {
            tensileToleranceFieldsParent.SetActive(false);
        }
        // Инициализация дропдауна материалов
        if (materialDropdown != null)
        {
            materialDropdown.interactable = false;
            materialDropdown.ClearOptions();
            if (materialDropdown.captionText != null) materialDropdown.captionText.text = "Материал"; // Начальный текст
        }
    }

    void SetupStaticEventListeners()
    {
        if (templateNameDropdown != null) templateNameDropdown.onValueChanged.AddListener(OnTemplateNameDropdownChanged);
        if (materialDropdown != null) materialDropdown.onValueChanged.AddListener(OnMaterialDropdownChanged);
        if (shapeTypeDropdown != null) shapeTypeDropdown.onValueChanged.AddListener(OnShapeTypeDropdownChanged);
    }

    private void MapLegacyUIElements()
    {
        _parameterLabels["DiameterThickness"] = diameterThicknessLabel;
        _parameterInputFields["DiameterThickness"] = diameterThicknessInputField;
        _parameterDropdowns["DiameterThickness"] = diameterThicknessDropdown;
        _parameterErrorTexts["DiameterThickness"] = thicknessErrorMessageText;

        _parameterLabels["Width"] = widthLabel;
        _parameterInputFields["Width"] = widthInputField;
        _parameterDropdowns["Width"] = widthDropdown;
        _parameterErrorTexts["Width"] = widthErrorMessageText;

        _parameterLabels["Length"] = lengthLabel;
        _parameterInputFields["Length"] = lengthInputField;
        _parameterDropdowns["Length"] = lengthDropdown;
        _parameterErrorTexts["Length"] = lengthErrorMessageText;

        _parameterInputFields["Speed"] = speedInputField;
        _parameterErrorTexts["Speed"] = speedErrorMessageText;
    }

    private void SubscribeToDoManagerActions()
    {
        if (ToDoManager.Instance == null) return;
        ToDoManager.Instance.SubscribeToAction(ActionType.ShowTestSettingsPanelAction, HandleShowTestSettingsPanelCommand);
        ToDoManager.Instance.SubscribeToAction(ActionType.SampleSetupAction, HandleSampleSetupCommand);
        ToDoManager.Instance.SubscribeToAction(ActionType.ApplySampleSetupSettingsAction, HandleApplySampleSetupSettingsCommand);
        ToDoManager.Instance.SubscribeToAction(ActionType.CloseSettingsPanelAction, HandleCloseSettingsPanelCommand);
        ToDoManager.Instance.SubscribeToAction(ActionType.SetSetupActionButtonsVisibilityAction, HandleSetActionButtonsVisibilityCommand);
    }
    private void UnsubscribeFromToDoManagerActions()
    {
        if (ToDoManager.Instance == null) return;
        ToDoManager.Instance.UnsubscribeFromAction(ActionType.ShowTestSettingsPanelAction, HandleShowTestSettingsPanelCommand);
        ToDoManager.Instance.UnsubscribeFromAction(ActionType.SampleSetupAction, HandleSampleSetupCommand);
        ToDoManager.Instance.UnsubscribeFromAction(ActionType.ApplySampleSetupSettingsAction, HandleApplySampleSetupSettingsCommand);
        ToDoManager.Instance.UnsubscribeFromAction(ActionType.CloseSettingsPanelAction, HandleCloseSettingsPanelCommand);
        ToDoManager.Instance.UnsubscribeFromAction(ActionType.SetSetupActionButtonsVisibilityAction, HandleSetActionButtonsVisibilityCommand);
    }

    private class StateSnapshot
    {
        public string TemplateName { get; set; }
        public MaterialPropertiesAsset Material { get; set; }
        public SampleForm Shape { get; set; }
        public Dictionary<string, float> Parameters { get; set; }
        public bool IsValid { get; set; }
        public float Area { get; set; }
        public TestSpeedMode SpeedMode { get; set; }
        public float ClampingLength { get; set; }
        public string GroupName { get; set; }
        public string BatchNumber { get; set; }
        public string Marking { get; set; }
        public string Notes { get; set; }
    }

    void LoadInitialData()
    {
        PopulateTemplateNameDropdown();
        if (templateNameDropdown != null)
        {
            templateNameDropdown.gameObject.SetActive(false);
            isTemplateDropdownActive = false;
        }
    }

    // Метод для загрузки всех MaterialPropertiesAsset
    void LoadAllMaterials()
    {
        _allLoadedMaterials = DataManager.Instance.AllMaterials; 

        if (_allLoadedMaterials == null)
        {
            Debug.LogWarning("[SetupPanelController] Не удалось получить список материалов из DataManager.");
            _allLoadedMaterials = new List<MaterialPropertiesAsset>(); // Создаем пустой список, чтобы избежать ошибок
        }
    }

    // Метод для заполнения/фильтрации дропдауна материалов
    void PopulateMaterialDropdown()
    {
        if (materialDropdown == null || _allLoadedMaterials == null) return;

        materialDropdown.ClearOptions();
        _currentlyDisplayedMaterials = new List<MaterialPropertiesAsset>();        

        if (string.IsNullOrEmpty(currentTemplateName))
        {
            _selectedMaterialPropertiesAsset = null;
            if (materialDropdown.captionText != null) materialDropdown.captionText.text = "Материал";
            materialDropdown.interactable = false;
            materialDropdown.RefreshShownValue();
            // Сбрасываем зависимые от материала UI элементы, если необходимо
            ResetMaterialDependentUI();
            return;
        }

        foreach (var materialAsset in _allLoadedMaterials)
        {
            if (materialAsset.compatibleTestTemplates != null &&
                materialAsset.compatibleTestTemplates.Contains(currentTemplateName))
            {
                _currentlyDisplayedMaterials.Add(materialAsset);
            }
        }

        if (_currentlyDisplayedMaterials.Count > 0)
        {
            // 1. Сначала сортируем САМ список объектов MaterialPropertiesAsset
            _currentlyDisplayedMaterials = _currentlyDisplayedMaterials
                .OrderBy(m => m.materialDisplayName, new NaturalStringComparer())
                .ToList();

            // 2. Теперь создаем список имен из УЖЕ отсортированного списка объектов.
            List<string> materialNames = _currentlyDisplayedMaterials
                .Select(m => m.materialDisplayName)
                .ToList();

            materialDropdown.AddOptions(materialNames);
            materialDropdown.value = -1;
            if (materialDropdown.captionText != null) materialDropdown.captionText.text = "Выберите материал...";
            materialDropdown.interactable = true;
        }
        else
        {
            if (materialDropdown.captionText != null) materialDropdown.captionText.text = "Нет материалов";
            materialDropdown.interactable = false;
            // Сбрасываем зависимые от материала UI элементы
            ResetMaterialDependentUI();
        }
        materialDropdown.RefreshShownValue();
    }

    // Метод для сброса UI, зависящего от материала (например, поля параметров образца)
    private void ResetMaterialDependentUI()
    {
        // Если форма уже выбрана, но материал сброшен/не найден, нужно обновить UI параметров образца без учета свойств материала, или скрыть поля параметров образца.
        if (currentShapeType != SampleForm.Неопределено && _currentTestLogicHandler != null && currentTestData != null && _selectedSampleDataForUIConfig != null)
        {
            // Передаем null в качестве MaterialPropertiesAsset, чтобы UI не зависело от него
            SampleUIConfiguration uiConfig = _currentTestLogicHandler.GetSampleParametersUIConfig(currentTestData, _selectedSampleDataForUIConfig, null);
            RebuildSampleParametersUI(uiConfig);
        }
        else
        {
            // Если и форма не выбрана, то поля и так должны быть неактивны/скрыты
            SetVisibilityForAllParameterFields(false);
            if (areaInputField != null) areaInputField.text = "0.00";
        }
        ValidateAndRefreshUI();
    }

    void PopulateTemplateNameDropdown()
    {
        if (templateNameDropdown == null) return;
        List<string> templateNames = GetUniqueTemplateNames();
        templateNameDropdown.ClearOptions();
        if (templateNames.Count > 0)
        {
            templateNameDropdown.AddOptions(templateNames);
            templateNameDropdown.value = -1;
            if (templateNameDropdown.captionText != null) templateNameDropdown.captionText.text = "Выберите шаблон...";
            templateNameDropdown.interactable = true;
        }
        else
        {
            if (templateNameDropdown.captionText != null) templateNameDropdown.captionText.text = "Нет шаблонов";
            templateNameDropdown.interactable = false;
        }
        templateNameDropdown.RefreshShownValue();
    }
    List<string> GetUniqueTemplateNames()
    {
        var testConfigs = DataManager.Instance?.AllTestConfigs;

        if (testConfigs == null || testConfigs.Count == 0) return new List<string>();
        return testConfigs
            .Where(config => !string.IsNullOrEmpty(config?.templateName))
            .Select(config => config.templateName)
            .Distinct().OrderBy(name => name).ToList();
    }

    void PopulateShapeTypeDropdown()
    {
        if (shapeTypeDropdown == null) { SetVisibilityForAllParameterFields(false); return; }

        shapeTypeDropdown.ClearOptions();
        shapeTypeDropdown.interactable = false;
        if (shapeTypeDropdown.captionText != null) shapeTypeDropdown.captionText.text = "Форма";

        if (string.IsNullOrEmpty(currentTemplateName))
        {
            SetVisibilityForAllParameterFields(false);
            shapeTypeDropdown.RefreshShownValue();
            return;
        }
        HashSet<string> uniqueShapeTypes = GetUniqueSampleFormsForAllConfigsOfTemplate(currentTemplateName);

        if (uniqueShapeTypes.Count > 0)
        {
            List<string> sortedShapeTypes = uniqueShapeTypes.OrderBy(s => s).ToList();
            shapeTypeDropdown.AddOptions(sortedShapeTypes);
            shapeTypeDropdown.value = -1;
            if (shapeTypeDropdown.captionText != null) shapeTypeDropdown.captionText.text = "Выберите форму...";
            shapeTypeDropdown.interactable = true;
        }
        else
        {
            if (shapeTypeDropdown.captionText != null) shapeTypeDropdown.captionText.text = "Формы не найдены";
        }
        shapeTypeDropdown.RefreshShownValue();
        SetVisibilityForAllParameterFields(false);
    }
    HashSet<string> GetUniqueSampleFormsForAllConfigsOfTemplate(string templateName)
    {
        HashSet<string> uniqueShapeTypes = new HashSet<string>();
        var allConfigs = DataManager.Instance?.AllTestConfigs;

        if (allConfigs == null) return uniqueShapeTypes;

        foreach (var config in allConfigs)
        {
            if (config != null && config.templateName == templateName && config.compatibleSampleIDs != null)
            {
                foreach (string sampleId in config.compatibleSampleIDs)
                {
                    if (string.IsNullOrEmpty(sampleId)) continue;
                    SampleData sampleData = SampleManager.Instance?.GetSampleData(sampleId);
                    if (sampleData != null)
                    {
                        uniqueShapeTypes.Add(sampleData.sampleForm.ToString());
                    }
                }
            }
        }
        return uniqueShapeTypes;
    }
    #endregion

    #region UI Updates & Dynamic UI Building
    private void SetVisibilityForAllParameterFields(bool visible)
    {
        if (diameterThicknessLabel) diameterThicknessLabel.gameObject.SetActive(visible);
        if (diameterThicknessInputField) diameterThicknessInputField.gameObject.SetActive(visible);
        if (diameterThicknessDropdown) diameterThicknessDropdown.gameObject.SetActive(visible);
        if (thicknessErrorMessageText && !visible) { thicknessErrorMessageText.text = ""; thicknessErrorMessageText.gameObject.SetActive(false); }

        if (widthLabel) widthLabel.gameObject.SetActive(visible);
        if (widthInputField) widthInputField.gameObject.SetActive(visible);
        if (widthDropdown) widthDropdown.gameObject.SetActive(visible);
        if (widthErrorMessageText && !visible) { widthErrorMessageText.text = ""; widthErrorMessageText.gameObject.SetActive(false); }

        if (lengthLabel) lengthLabel.gameObject.SetActive(visible);
        if (lengthInputField) lengthInputField.gameObject.SetActive(visible);
        if (lengthDropdown) lengthDropdown.gameObject.SetActive(visible);
        if (lengthErrorMessageText && !visible) { lengthErrorMessageText.text = ""; lengthErrorMessageText.gameObject.SetActive(false); }

        if (speedInputField) speedInputField.gameObject.SetActive(visible && (_currentTestLogicHandler != null));
        if (speedErrorMessageText && !visible) { speedErrorMessageText.text = ""; speedErrorMessageText.gameObject.SetActive(false); }

        if (areaInputField && !visible) areaInputField.text = "0.00";
    }

    private void RebuildSampleParametersUI(SampleUIConfiguration uiConfig, Dictionary<string, float> valuesToPreserve = null, TestSpeedMode? speedModeToSet = null)
    {
        if (uiConfig.Fields == null)
        {
            Debug.LogWarning("[SetupPanel] UIConfig.Fields is null. UI не будет перестроено.");
            SetVisibilityForAllParameterFields(false);
            return;
        }

        SetVisibilityForAllParameterFields(false);

        if (speedModeDropdown != null) speedModeDropdown.gameObject.SetActive(false);
        foreach (var fieldConf in uiConfig.Fields)
        {
            _parameterLabels.TryGetValue(fieldConf.ParameterName, out TMP_Text label);
            _parameterInputFields.TryGetValue(fieldConf.ParameterName, out TMP_InputField input);
            _parameterDropdowns.TryGetValue(fieldConf.ParameterName, out TMP_Dropdown dropdown);

            if (!fieldConf.IsVisible) continue;

            if (label != null)
            {
                label.text = fieldConf.LabelText;
                label.gameObject.SetActive(true);
            }

            // Пытаемся восстановить значение
            bool valueWasPreserved = false;
            if (valuesToPreserve != null && valuesToPreserve.TryGetValue(fieldConf.ParameterName, out float preservedValue) && !float.IsNaN(preservedValue))
            {
                if (fieldConf.IsDropdown && dropdown != null)
                {
                    int previousIndex = fieldConf.StandardValues?.IndexOf(preservedValue) ?? -1;
                    if (previousIndex >= 0)
                    {
                        dropdown.value = previousIndex;
                        valueWasPreserved = true;
                    }
                }
                else if (!fieldConf.IsDropdown && input != null)
                {
                    input.text = preservedValue.ToString(CultureInfo.InvariantCulture);
                    valueWasPreserved = true;
                }
            }

            // Если не удалось восстановить, используем значение по умолчанию
            if (!valueWasPreserved)
            {
                if (fieldConf.IsDropdown && dropdown != null)
                {
                    dropdown.ClearOptions();
                    if (fieldConf.StandardValues != null && fieldConf.StandardValues.Any())
                    {
                        dropdown.AddOptions(fieldConf.StandardValues.Select(v => string.Format(CultureInfo.InvariantCulture, fieldConf.StandardDisplayFormat ?? "{0}", v)).ToList());
                        int defaultIndex = fieldConf.StandardValues.IndexOf(fieldConf.DefaultValue);
                        dropdown.value = defaultIndex >= 0 ? defaultIndex : 0;
                    }
                    else
                    {
                        dropdown.AddOptions(new List<string> { "Нет опций" });
                        dropdown.value = 0;
                        dropdown.interactable = false;
                    }
                }
                else if (!fieldConf.IsDropdown && input != null)
                {
                    input.text = fieldConf.DefaultValue.ToString(CultureInfo.InvariantCulture);
                }
            }

            // Настраиваем UI и слушатели в любом случае
            if (fieldConf.IsDropdown && dropdown != null)
            {
                if (input != null) input.gameObject.SetActive(false);
                dropdown.gameObject.SetActive(true);
                dropdown.onValueChanged.RemoveAllListeners();
                dropdown.onValueChanged.AddListener(delegate { ValidateAndRefreshUI(); });
                dropdown.RefreshShownValue();
            }
            else if (!fieldConf.IsDropdown && input != null)
            {
                if (dropdown != null) dropdown.gameObject.SetActive(false);
                input.gameObject.SetActive(true);
                input.onValueChanged.RemoveAllListeners();
                input.onValueChanged.AddListener(delegate { ValidateAndRefreshUI(); });
            }
            // Обработка поля "Speed"
            if (fieldConf.ParameterName == "Speed")
            {
                if (fieldConf.HasSpeedModeSelector && speedModeDropdown != null)
                {
                    speedModeDropdown.gameObject.SetActive(true);

                    // Используем переданное значение, если оно есть. Иначе - дефолтное.
                    speedModeDropdown.value = speedModeToSet.HasValue 
                                        ? (int)speedModeToSet.Value 
                                        : (int)fieldConf.DefaultSpeedMode;

                    // Добавляем слушателя, чтобы при смене режима сразу проходила валидация
                    speedModeDropdown.onValueChanged.RemoveAllListeners();
                    speedModeDropdown.onValueChanged.AddListener(delegate { ValidateAndRefreshUI(); });
                }
                else if (speedModeDropdown != null)
                {
                    speedModeDropdown.gameObject.SetActive(false);
                }
            }
        }

        if (diameterThicknessLabel != null && !string.IsNullOrEmpty(uiConfig.DiameterThicknessLabelOverride))
        {
            var dtFieldConf = uiConfig.Fields.FirstOrDefault(f => f.ParameterName == "DiameterThickness");
            if (dtFieldConf.ParameterName != null && dtFieldConf.IsVisible)
            {
                if (_parameterLabels.TryGetValue("DiameterThickness", out TMP_Text dtLabelFromDict))
                {
                    dtLabelFromDict.text = uiConfig.DiameterThicknessLabelOverride;
                }
                else if (diameterThicknessLabel != null)
                {
                    diameterThicknessLabel.text = uiConfig.DiameterThicknessLabelOverride;
                }
            }
        }

        bool widthFieldManagedByFieldsAndVisible = uiConfig.Fields.Any(f => f.ParameterName == "Width" && f.IsVisible);
        if (!widthFieldManagedByFieldsAndVisible)
        {
            bool showWidthSection = uiConfig.IsWidthFieldRelevant;
            if (widthLabel) widthLabel.gameObject.SetActive(showWidthSection);
            if (widthInputField) widthInputField.gameObject.SetActive(showWidthSection);
            if (widthDropdown) widthDropdown.gameObject.SetActive(showWidthSection);
            if (widthErrorMessageText && !showWidthSection) { widthErrorMessageText.text = ""; widthErrorMessageText.gameObject.SetActive(false); }
        }

        ValidateAndRefreshUI();
    }

    void UpdateSampleDisplayName(string templateName)
    {
        if (sampleDisplayNameText == null) return;
        TestConfigurationData firstMatchConfig = DataManager.Instance?.AllTestConfigs
        .FirstOrDefault(config => config != null && config.templateName == templateName);

        sampleDisplayNameText.text = firstMatchConfig != null ? firstMatchConfig.SampleDisplayName : "Образец";
        }
    #endregion

    #region Dropdown Event Handlers
    public void OnTemplateNameDropdownChanged(int index)
    {
        ClearTemporaryMessage();
        if (TestSettingMessageText != null) TestSettingMessageText.gameObject.SetActive(index < 0);
        currentTestData = null;
        currentShapeType = SampleForm.Неопределено;
        _currentTestLogicHandler = null;
        _selectedSampleDataForUIConfig = null;
        _currentTestLogicHandler_AssociatedConfigData = null;
        _selectedMaterialPropertiesAsset = null;

        if (tensileToleranceFieldsParent != null)
        {
            tensileToleranceFieldsParent.SetActive(false);
        }

        if (index < 0 || templateNameDropdown == null || index >= templateNameDropdown.options.Count)
        {
            currentTemplateName = null;
            if (sampleDisplayNameText != null) sampleDisplayNameText.text = "Образец";
            PopulateMaterialDropdown();
            PopulateShapeTypeDropdown();
        }
        else
        {
            currentTemplateName = templateNameDropdown.options[index].text;

            // БЛОК КОДА ДЛЯ ПОДСКАЗОК
            string promptKey = null;

            switch (currentTemplateName)
            {
                case "ГОСТ 1497 (Растяжение)":
                    promptKey = "SPC_GOST_1497";
                    break;
                case "ГОСТ 10180 (Сжатие)":
                    promptKey = "SPC_GOST_10180";
                    break;
            }

            if (!string.IsNullOrEmpty(promptKey))
            {
                // Используем новый вспомогательный метод!
                SendPromptUpdateCommand(
                    promptKey, 
                    PromptSourceType.SystemAction, 
                    "TemplateDropdownChanged", 
                    true
                );
            }

            PopulateMaterialDropdown();
            PopulateShapeTypeDropdown();
            UpdateSampleDisplayName(currentTemplateName);
        }
        SetVisibilityForAllParameterFields(false);
        if (areaInputField != null) areaInputField.text = "0.00";
        if (shapeTypeDropdown != null) { shapeTypeDropdown.value = -1; if (shapeTypeDropdown.captionText != null) shapeTypeDropdown.captionText.text = "Форма"; shapeTypeDropdown.RefreshShownValue(); } // Сброс формы
        SystemStateMonitor.Instance?.ReportSetupSelection(currentTemplateName, null, SampleForm.Неопределено);
    }

    // Обработчик изменения выбора в дропдауне материалов
    private void OnMaterialDropdownChanged(int index)
    {
        Dictionary<string, float> previousValues = GetCurrentDimensionValuesFromUI();

        if (materialDropdown == null || _currentlyDisplayedMaterials == null || index < 0 || index >= _currentlyDisplayedMaterials.Count)
        {
            ClearTemporaryMessage();
            _selectedMaterialPropertiesAsset = null;
            SystemStateMonitor.Instance?.ReportSelectedMaterial(null);
            // Обновляем UI параметров образца, если материал не выбран
            if (currentShapeType != SampleForm.Неопределено && _currentTestLogicHandler != null && currentTestData != null && _selectedSampleDataForUIConfig != null)
            {
                SampleUIConfiguration uiConfig = _currentTestLogicHandler.GetSampleParametersUIConfig(currentTestData, _selectedSampleDataForUIConfig, null); // Передаем null как материал
                RebuildSampleParametersUI(uiConfig, previousValues);
            }
            return;            
        }

        _selectedMaterialPropertiesAsset = _currentlyDisplayedMaterials[index];
        Debug.Log($"[SetupPanelController] Выбран материал: {_selectedMaterialPropertiesAsset.materialDisplayName}");
        SystemStateMonitor.Instance?.ReportSelectedMaterial(_selectedMaterialPropertiesAsset);

        string materialName = _selectedMaterialPropertiesAsset?.materialDisplayName;
        SystemStateMonitor.Instance?.ReportSetupSelection(currentTemplateName, materialName, currentShapeType);

        // Если форма образца уже выбрана, обновляем UI параметров образца с учетом нового материала
        if (currentShapeType != SampleForm.Неопределено && _currentTestLogicHandler != null && currentTestData != null && _selectedSampleDataForUIConfig != null)
        {
            SampleUIConfiguration uiConfig = _currentTestLogicHandler.GetSampleParametersUIConfig(currentTestData, _selectedSampleDataForUIConfig, _selectedMaterialPropertiesAsset);
            RebuildSampleParametersUI(uiConfig, previousValues);
        }
        // В любом случае, после выбора материала, стоит перепроверить валидацию
        ValidateAndRefreshUI();
    }

    public void OnShapeTypeDropdownChanged(int index)
    {
        ClearTemporaryMessage();
        bool shouldHideToleranceFields = true;

        if (shapeTypeDropdown == null)
        {
            SetVisibilityForAllParameterFields(false);
            if (tensileToleranceFieldsParent != null) tensileToleranceFieldsParent.SetActive(false);
            return;
        }

        if (index < 0 || index >= shapeTypeDropdown.options.Count)
        {
            currentShapeType = SampleForm.Неопределено;
            _selectedSampleDataForUIConfig = null;
            currentTestData = null;
            _currentTestLogicHandler = null;
            _currentTestLogicHandler_AssociatedConfigData = null;
            SetVisibilityForAllParameterFields(false);
            if (areaInputField != null) areaInputField.text = "0.00";
        }
        else
        {
            string selectedShapeName = shapeTypeDropdown.options[index].text;
            if (!System.Enum.TryParse(selectedShapeName, out currentShapeType))
            {
                currentShapeType = SampleForm.Неопределено; _selectedSampleDataForUIConfig = null;
                currentTestData = null; _currentTestLogicHandler = null; _currentTestLogicHandler_AssociatedConfigData = null;
                SetVisibilityForAllParameterFields(false);
                if (areaInputField != null) areaInputField.text = "0.00";
                Debug.LogError($"[SetupPanel] Не удалось распознать форму: {selectedShapeName}");
            }
            else
            {
                currentTestData = TestManager.Instance?.GetTestConfigurationForTemplateAndShape(currentTemplateName, currentShapeType);

                if (currentTestData != null)
                {
                    _currentTestLogicHandler = TestLogicHandlerFactory.Create(currentTestData.testType, currentTestData);
                    Debug.Log($"<color=orange>СОЗДАН ОБРАБОТЧИК: {_currentTestLogicHandler.GetType().Name}</color>");
                    _currentTestLogicHandler_AssociatedConfigData = currentTestData;
                    _selectedSampleDataForUIConfig = SampleManager.Instance?.GetFirstCompatibleSampleData(currentTestData, currentShapeType);

                    if (_currentTestLogicHandler is TensileLogicHandler)
                    {
                        shouldHideToleranceFields = false;
                    }
                }
                else
                {
                    Debug.LogWarning($"[SetupPanel] TestData для '{currentTemplateName}' и '{currentShapeType}' не найден.");
                    _currentTestLogicHandler = null;
                    _currentTestLogicHandler_AssociatedConfigData = null;
                    _selectedSampleDataForUIConfig = null;
                }
            }
        }

        if (tensileToleranceFieldsParent != null)
        {
            tensileToleranceFieldsParent.SetActive(!shouldHideToleranceFields);
        }

        // Передаем _selectedMaterialPropertiesAsset в GetSampleParametersUIConfig
        if (_currentTestLogicHandler != null && currentTestData != null && _selectedSampleDataForUIConfig != null)
        {
            // Используем _selectedMaterialPropertiesAsset при получении конфигурации UI
            SampleUIConfiguration uiConfig = _currentTestLogicHandler.GetSampleParametersUIConfig(currentTestData, _selectedSampleDataForUIConfig, _selectedMaterialPropertiesAsset);
            RebuildSampleParametersUI(uiConfig);
        }
        else
        {
            Debug.LogWarning($"[SetupPanel] Не удалось получить UI Config. Шаблон: {currentTemplateName}, Форма: {currentShapeType}, Материал: {_selectedMaterialPropertiesAsset?.materialDisplayName ?? "не выбран"}");
            SetVisibilityForAllParameterFields(false);
        }
        string materialName = _selectedMaterialPropertiesAsset?.materialDisplayName;
        SystemStateMonitor.Instance?.ReportSetupSelection(currentTemplateName, materialName, currentShapeType);
    }
    #endregion

    #region Validation and Area Calculation
    private void ValidateAndRefreshUI()
    {
        ClearTemporaryMessage();
        if (_currentTestLogicHandler == null || currentTestData == null || _selectedSampleDataForUIConfig == null)
        {
            ClearAllErrorMessages();
            if (areaInputField != null) areaInputField.text = "0.00";
            return;
        }
        Dictionary<string, float> currentValues = GetCurrentDimensionValuesFromUI();
        if (_selectedSampleDataForUIConfig != null) currentValues["ClampingLength"] = _selectedSampleDataForUIConfig.ClampingLength;    

        TestSpeedMode currentSpeedMode = (speedModeDropdown != null && speedModeDropdown.gameObject.activeSelf)
                                     ? (TestSpeedMode)speedModeDropdown.value
                                     : TestSpeedMode.DeformationRate;
        SystemStateMonitor.Instance?.ReportSpeedMode(currentSpeedMode);
        Dictionary<string, string> errors = _currentTestLogicHandler.ValidateSampleParameters
            (currentValues, currentShapeType, currentTestData, _selectedSampleDataForUIConfig, _selectedMaterialPropertiesAsset, minSpeed, maxSpeed, currentSpeedMode);
        DisplayValidationErrors(errors);
        bool isValid = !errors.Any(kvp => !string.IsNullOrEmpty(kvp.Value));
        float area = _currentTestLogicHandler.CalculateCrossSectionalArea(currentValues, currentShapeType, _selectedSampleDataForUIConfig);
        float clampingLength = (_selectedSampleDataForUIConfig != null) ? _selectedSampleDataForUIConfig.ClampingLength : 0f;
        
        SystemStateMonitor.Instance?.ReportClampingLength(clampingLength);
        SystemStateMonitor.Instance?.ReportSampleParameters(currentValues, area);
        SystemStateMonitor.Instance?.ReportSetupValidity(isValid);        
        if (areaInputField != null) areaInputField.text = (area > 0 && !float.IsNaN(area)) ? area.ToString("F2", CultureInfo.InvariantCulture) : "0.00";
    }

    private Dictionary<string, float> GetCurrentDimensionValuesFromUI()
    {
        var values = new Dictionary<string, float>();
        if (_currentTestLogicHandler == null || currentTestData == null || _selectedSampleDataForUIConfig == null) return values;

        // Передаем _selectedMaterialPropertiesAsset для получения UI конфигурации
        SampleUIConfiguration uiConfig = _currentTestLogicHandler.GetSampleParametersUIConfig(currentTestData, _selectedSampleDataForUIConfig, _selectedMaterialPropertiesAsset);
        float diameterThicknessValue = float.NaN;

        foreach (var fieldConf in uiConfig.Fields)
        {
            if (!fieldConf.IsVisible && fieldConf.ParameterName != "DiameterThickness") // DiameterThickness обрабатывается особо ниже, если он скрыт, но нужен для связки
            {
                // Пропускаем полностью невидимые поля, кроме DiameterThickness, если он скрыт, но является основой для других
                bool isDtAndHiddenButLinked = fieldConf.ParameterName == "DiameterThickness" && !fieldConf.IsVisible &&
                                             (_selectedSampleDataForUIConfig?.widthSetting?.linkMode == DimensionLinkMode.FollowDiameterThickness ||
                                              _selectedSampleDataForUIConfig?.lengthSetting?.linkMode == DimensionLinkMode.FollowDiameterThickness);
                if (!isDtAndHiddenButLinked) continue;
            }


            float parsedValue = float.NaN;
            if (fieldConf.IsDropdown && _parameterDropdowns.TryGetValue(fieldConf.ParameterName, out TMP_Dropdown dropdown))
            {
                if (dropdown.gameObject.activeSelf && dropdown.value >= 0 && fieldConf.StandardValues != null && dropdown.value < fieldConf.StandardValues.Count)
                {
                    parsedValue = fieldConf.StandardValues[dropdown.value];
                }
            }
            else if (!fieldConf.IsDropdown && _parameterInputFields.TryGetValue(fieldConf.ParameterName, out TMP_InputField input))
            {
                if (input.gameObject.activeSelf || (fieldConf.ParameterName == "DiameterThickness" && !fieldConf.IsVisible)) // Читаем даже если скрыто, для DiameterThickness, если он основа
                {
                    parsedValue = GetFloatValueFromInputField(input);
                }
            }
            values[fieldConf.ParameterName] = parsedValue;

            if (fieldConf.ParameterName == "DiameterThickness")
            {
                diameterThicknessValue = parsedValue;
            }
        }

        if (_selectedSampleDataForUIConfig != null && !float.IsNaN(diameterThicknessValue))
        {
            if (_selectedSampleDataForUIConfig.widthSetting != null &&
                _selectedSampleDataForUIConfig.widthSetting.linkMode == DimensionLinkMode.FollowDiameterThickness)
            {
                // Если поле ширины не управляется через Fields ИЛИ оно управляется, но не видимо, тогда устанавливаем его значение равным diameterThicknessValue
                var widthFieldConfig = uiConfig.Fields.FirstOrDefault(f => f.ParameterName == "Width");
                if (widthFieldConfig.ParameterName == null || !widthFieldConfig.IsVisible)
                {
                    values["Width"] = diameterThicknessValue;
                }
            }

            if (_selectedSampleDataForUIConfig.lengthSetting != null &&
                _selectedSampleDataForUIConfig.lengthSetting.linkMode == DimensionLinkMode.FollowDiameterThickness)
            {
                var lengthFieldConfig = uiConfig.Fields.FirstOrDefault(f => f.ParameterName == "Length");
                if (lengthFieldConfig.ParameterName == null || !lengthFieldConfig.IsVisible)
                {
                    values["Length"] = diameterThicknessValue;
                }
            }
        }
        if (speedInputField != null && speedInputField.gameObject.activeSelf && !values.ContainsKey("Speed"))
        {
            values["Speed"] = GetFloatValueFromInputField(speedInputField);
        }
        return values;
    }

    private void DisplayValidationErrors(Dictionary<string, string> errors)
    {
        ClearAllErrorMessages();
        foreach (var errorPair in errors)
        {
            if (_parameterErrorTexts.TryGetValue(errorPair.Key, out TextMeshProUGUI errorTextUI))
            {
                SetErrorMessageText(errorTextUI, errorPair.Value);
            }
        }
    }

    private bool IsInputValid()
    {
        if (string.IsNullOrEmpty(currentTemplateName) || _selectedMaterialPropertiesAsset == null || currentShapeType == SampleForm.Неопределено ||
            currentTestData == null || _currentTestLogicHandler == null || _selectedSampleDataForUIConfig == null)
        {
            return false;
        }
        Dictionary<string, float> currentValues = GetCurrentDimensionValuesFromUI();

        TestSpeedMode currentSpeedMode = (speedModeDropdown != null && speedModeDropdown.gameObject.activeSelf)
                                     ? (TestSpeedMode)speedModeDropdown.value
                                     : TestSpeedMode.DeformationRate;

        Dictionary<string, string> errors = _currentTestLogicHandler.ValidateSampleParameters(
            currentValues, currentShapeType, currentTestData, _selectedSampleDataForUIConfig, _selectedMaterialPropertiesAsset, minSpeed, maxSpeed, currentSpeedMode
        );
        return !errors.Any(kvp => !string.IsNullOrEmpty(kvp.Value));
    }

    float GetFloatValueFromInputField(TMP_InputField inputField)
    {
        if (inputField == null || string.IsNullOrWhiteSpace(inputField.text)) return float.NaN;
        string processedValue = inputField.text.Replace(',', '.');
        if (float.TryParse(processedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)) return value;
        return float.NaN;
    }
    void SetErrorMessageText(TextMeshProUGUI errorMessageText, string message)
    {
        if (errorMessageText != null)
        {
            errorMessageText.text = message ?? "";
            errorMessageText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }
    }
    #endregion

    #region Button Click Handlers & ToDoManager Callbacks
    private void HandleShowTestSettingsPanelCommand(BaseActionArgs args)
    {
        InternalToggleTemplateDropdownVisibility();
    }
    private void HandleSampleSetupCommand(BaseActionArgs args)
    {
        // --- ШАГ 1: Проверяем, есть ли в Мониторе уже готовые настройки ---
        var monitor = SystemStateMonitor.Instance;
        if (monitor != null)
        {
            _stateBeforeEdit = new StateSnapshot
            {
                TemplateName = monitor.SelectedTemplateName,
                Material = monitor.SelectedMaterial,
                Shape = monitor.SelectedShape,
                // Создаем КОПИЮ словаря, это критически важно!
                Parameters = new Dictionary<string, float>(monitor.CurrentSampleParameters ?? new Dictionary<string, float>()),
                IsValid = monitor.IsSetupPanelValid,
                Area = monitor.CalculatedArea,
                SpeedMode = monitor.SelectedSpeedMode,
                ClampingLength = monitor.CurrentClampingLength,
                GroupName = monitor.ReportGroupName,
                BatchNumber = monitor.ReportBatchNumber,
                Marking = monitor.ReportMarking,
                Notes = monitor.ReportNotes
            };

            if (!string.IsNullOrEmpty(monitor.SelectedTemplateName))
            {
                // --- ШАГ 2: Восстанавливаем внутреннее состояние SPC из Монитора ---
                currentTemplateName = monitor.SelectedTemplateName;
                currentShapeType = monitor.SelectedShape;
                _selectedMaterialPropertiesAsset = monitor.SelectedMaterial;

                currentTestData = monitor.CurrentTestConfig;
                _currentTestLogicHandler = monitor.CurrentTestLogicHandler;

                if (currentTestData != null)
                {
                    _selectedSampleDataForUIConfig = SampleManager.Instance?.GetFirstCompatibleSampleData(currentTestData, currentShapeType);
                }

                // --- ШАГ 3: Обновляем UI, чтобы он соответствовал восстановленному состоянию ---
                // 1. Устанавливаем дропдауны в правильные позиции без вызова их событий
                templateNameDropdown.SetValueWithoutNotify(templateNameDropdown.options.FindIndex(opt => opt.text == currentTemplateName));
                PopulateMaterialDropdown(); // Перезаполняем список материалов для этого шаблона
                materialDropdown.SetValueWithoutNotify(materialDropdown.options.FindIndex(opt => opt.text == monitor.SelectedMaterialName));
                PopulateShapeTypeDropdown(); // Перезаполняем список форм для этого шаблона
                shapeTypeDropdown.SetValueWithoutNotify(shapeTypeDropdown.options.FindIndex(opt => opt.text == currentShapeType.ToString()));

                // 2. Перестраиваем поля ввода и заполняем их сохраненными значениями из Монитора
                if (_currentTestLogicHandler != null && currentTestData != null && _selectedSampleDataForUIConfig != null)
                {
                    SampleUIConfiguration uiConfig = _currentTestLogicHandler.GetSampleParametersUIConfig(currentTestData, _selectedSampleDataForUIConfig, _selectedMaterialPropertiesAsset);
                    RebuildSampleParametersUI(uiConfig, monitor.CurrentSampleParameters, monitor.SelectedSpeedMode); // Передаем сохраненные параметры
                }

                // 3. Восстанавливаем поля заголовка
                if (groupNameInputField != null) groupNameInputField.text = monitor.ReportGroupName;
                if (batchNumberInputField != null) batchNumberInputField.text = monitor.ReportBatchNumber;
                if (markingInputField != null) markingInputField.text = monitor.ReportMarking;
                if (notesInputField != null) notesInputField.text = monitor.ReportNotes;

                // 4. Восстанавливаем режим скорости
                if (speedModeDropdown != null && speedModeDropdown.gameObject.activeSelf)
                {
                    speedModeDropdown.SetValueWithoutNotify((int)monitor.SelectedSpeedMode);
                    speedModeDropdown.RefreshShownValue();
                }

                InternalShowPanel(); // Просто показываем панель
                return; // Выходим, чтобы не выполнять остальную логику метода
            }
            else
            {
                InternalHideAndResetPanel();
            }
        }

        //Если в Мониторе нет готовых настроек, выполняем стандартную логику открытия панели
        bool shouldHideToleranceFieldsOnOpen = true;

        // Проверяем, выбран ли материал, если шаблон уже выбран
        if (!string.IsNullOrEmpty(currentTemplateName) && _selectedMaterialPropertiesAsset == null)
        {
            Debug.LogWarning("[SetupPanel] Открытие панели настроек: Шаблон выбран, но материал еще не выбран.");
        }

        if (!string.IsNullOrEmpty(currentTemplateName))
        {
            if (currentTestData == null || currentTestData.templateName != currentTemplateName)
            {
                if (currentShapeType != SampleForm.Неопределено)
                {
                    currentTestData = TestManager.Instance?.GetTestConfigurationForTemplateAndShape(currentTemplateName, currentShapeType);
                }
                if (currentTestData == null)
                {
                    if (DataManager.Instance != null)
                    {
                        currentTestData = DataManager.Instance.AllTestConfigs.FirstOrDefault(config => config != null && config.templateName == currentTemplateName);
                    }
                    else
                    {
                        Debug.LogError("[SetupPanel] DataManager не найден! Не могу получить TestConfigurationData.");
                        currentTestData = null;
                    }
                }
            }

            if (currentTestData != null)
            {
                if (_currentTestLogicHandler == null || _currentTestLogicHandler_AssociatedConfigData != currentTestData)
                {
                    _currentTestLogicHandler = TestLogicHandlerFactory.Create(currentTestData.testType, currentTestData);
                    _currentTestLogicHandler_AssociatedConfigData = currentTestData;
                }

                if (_currentTestLogicHandler is TensileLogicHandler)
                {
                    shouldHideToleranceFieldsOnOpen = false;
                }


                if (shapeTypeDropdown != null && (shapeTypeDropdown.options.Count == 0 || (currentTemplateName != templateNameDropdown.captionText.text && templateNameDropdown.value != -1)))
                {
                    PopulateShapeTypeDropdown();
                }
                // Заполняем дропдаун материалов, если он пуст, а шаблон выбран
                if (materialDropdown != null && (materialDropdown.options.Count == 0 || (materialDropdown.interactable && materialDropdown.value == -1)))
                {
                    PopulateMaterialDropdown();
                }

                if (currentShapeType != SampleForm.Неопределено)
                {
                    TestConfigurationData specificConfig = TestManager.Instance?.GetTestConfigurationForTemplateAndShape(currentTemplateName, currentShapeType);
                    if (specificConfig != null && specificConfig != currentTestData)
                    {
                        currentTestData = specificConfig;
                        _currentTestLogicHandler = TestLogicHandlerFactory.Create(currentTestData.testType, currentTestData);
                        _currentTestLogicHandler_AssociatedConfigData = currentTestData;
                        if (_currentTestLogicHandler is TensileLogicHandler) shouldHideToleranceFieldsOnOpen = false; else shouldHideToleranceFieldsOnOpen = true;
                    }

                    if (currentTestData != null)
                    {
                        _selectedSampleDataForUIConfig = SampleManager.Instance?.GetFirstCompatibleSampleData(currentTestData, currentShapeType);

                        if (_currentTestLogicHandler != null && _selectedSampleDataForUIConfig != null)
                        {
                            SampleUIConfiguration uiConfig = _currentTestLogicHandler.GetSampleParametersUIConfig(currentTestData, _selectedSampleDataForUIConfig, _selectedMaterialPropertiesAsset);
                            RebuildSampleParametersUI(uiConfig);
                        }
                        else
                        {
                            SetVisibilityForAllParameterFields(false);
                        }
                    }
                    else
                    {
                        SetVisibilityForAllParameterFields(false);
                    }
                }
                else
                {
                    SetVisibilityForAllParameterFields(false);
                }
            }
            else
            {
                if (shapeTypeDropdown != null)
                {
                    shapeTypeDropdown.ClearOptions();
                    shapeTypeDropdown.interactable = false;
                    if (shapeTypeDropdown.captionText != null) shapeTypeDropdown.captionText.text = "Форма";
                    shapeTypeDropdown.RefreshShownValue();
                }
                if (materialDropdown != null)
                {
                    materialDropdown.ClearOptions();
                    materialDropdown.interactable = false;
                    if (materialDropdown.captionText != null) materialDropdown.captionText.text = "Материал";
                    materialDropdown.RefreshShownValue();
                    _selectedMaterialPropertiesAsset = null;
                }
                SetVisibilityForAllParameterFields(false);
            }
        }
        else
        {
            //if (TestSettingMessageText != null) TestSettingMessageText.gameObject.SetActive(true);
            if (sampleDisplayNameText != null) sampleDisplayNameText.text = "Шаблон не выбран";
            if (shapeTypeDropdown != null)
            {
                shapeTypeDropdown.ClearOptions();
                shapeTypeDropdown.interactable = false;
                if (shapeTypeDropdown.captionText != null) shapeTypeDropdown.captionText.text = "Форма";
                shapeTypeDropdown.RefreshShownValue();
            }
            if (materialDropdown != null)
            {
                materialDropdown.ClearOptions();
                materialDropdown.interactable = false;
                if (materialDropdown.captionText != null) materialDropdown.captionText.text = "Материал";
                materialDropdown.RefreshShownValue();
                _selectedMaterialPropertiesAsset = null;
            }
            SetVisibilityForAllParameterFields(false);
        }

        if (tensileToleranceFieldsParent != null)
        {
            tensileToleranceFieldsParent.SetActive(!shouldHideToleranceFieldsOnOpen);
        }

        InternalShowPanel();
    }

    private void HandleApplySampleSetupSettingsCommand(BaseActionArgs args) { InternalApplySettings(); }

    private void HandleCloseSettingsPanelCommand(BaseActionArgs args)
        {
        // --- Восстанавливаем состояние из "снимка", если он есть ---
        if (_stateBeforeEdit != null && SystemStateMonitor.Instance != null)
        {
            var monitor = SystemStateMonitor.Instance;
            
            // Отправляем все сохраненные значения обратно в монитор
            monitor.ReportSetupSelection(_stateBeforeEdit.TemplateName, _stateBeforeEdit.Material?.materialDisplayName, _stateBeforeEdit.Shape);
            monitor.ReportSelectedMaterial(_stateBeforeEdit.Material);
            monitor.ReportSampleParameters(_stateBeforeEdit.Parameters, _stateBeforeEdit.Area);
            monitor.ReportSetupValidity(_stateBeforeEdit.IsValid);
            monitor.ReportSpeedMode(_stateBeforeEdit.SpeedMode);
            monitor.ReportClampingLength(_stateBeforeEdit.ClampingLength);
            monitor.ReportHeaderInfo(
                _stateBeforeEdit.GroupName, 
                _stateBeforeEdit.BatchNumber, 
                _stateBeforeEdit.Marking, 
                _stateBeforeEdit.Notes
            );

            // Очищаем снимок после использования
            _stateBeforeEdit = null;
        }
        
        if (setupPanelUI != null) setupPanelUI.SetActive(false);
        isButtonsContainerActive = false;
        EventManager.Instance?.RaiseEvent(EventType.RequestViewPlayContainerDisable, null);
    }

    private void HandleSetActionButtonsVisibilityCommand(BaseActionArgs args)
    {
        if (!(args is SetVisibilityArgs visibilityArgs)) return;
        bool shouldBeVisible = visibilityArgs.IsVisible;
        if (buttonsContainer != null && buttonsContainer.activeSelf != shouldBeVisible)
        {
            buttonsContainer.SetActive(shouldBeVisible);
            isButtonsContainerActive = shouldBeVisible;
            if (shouldBeVisible)
            {
                if (setupPanelUI != null && setupPanelUI.activeSelf)
                {
                    setupPanelUI.SetActive(false);
                    SystemStateMonitor.Instance?.ReportSetupValidity(false);
                }
                InternalHideTemplateDropdown();
            }
        }
    }

    private void InternalApplySettings()
    {
        void ShowError(string message) { if (TestSettingMessageText != null) { TestSettingMessageText.text = message; TestSettingMessageText.gameObject.SetActive(true); } }

        if (string.IsNullOrEmpty(currentTemplateName)) { ShowError("Сначала выберите шаблон испытания."); return; }
        if (_selectedMaterialPropertiesAsset == null) { ShowError("Выберите материал образца."); return; }
        if (currentShapeType == SampleForm.Неопределено) { ShowError("Выберите сечение образца."); return; }
        if (!IsInputValid()) { ShowError("Проверьте введенные параметры. Есть ошибки."); return; }

        Dictionary<string, float> finalValues = GetCurrentDimensionValuesFromUI();
        float userEnteredSpeed = finalValues.TryGetValue("Speed", out float s) ? s : float.NaN;

        TestSpeedMode selectedSpeedMode = (speedModeDropdown != null && speedModeDropdown.gameObject.activeSelf)
                                      ? (TestSpeedMode)speedModeDropdown.value
                                      : TestSpeedMode.DeformationRate;

        float actualSpeed = userEnteredSpeed * SIMULATION_SPEED_MULTIPLIER;
        float area = _currentTestLogicHandler.CalculateCrossSectionalArea(finalValues, currentShapeType, _selectedSampleDataForUIConfig);

        if (float.IsNaN(area) || area <= 0) { Debug.LogError("[SetupPanel] Ошибка расчета площади."); return; }
        if (float.IsNaN(userEnteredSpeed)) { Debug.LogError("[SetupPanel] Ошибка: Скорость не определена."); return; }

        finalValues.TryGetValue("DiameterThickness", out float finalDT);
        finalValues.TryGetValue("Width", out float finalW);
        finalValues.TryGetValue("Length", out float finalL);

        if (float.IsNaN(finalDT) || float.IsNaN(finalL) || (uiConfigShowsWidth() && float.IsNaN(finalW)))
        {
            Debug.LogError("[SetupPanel] Ошибка: Один из размеров NaN.");
            return;
        }

        // Передаем _selectedMaterialPropertiesAsset в Монитор
        SystemStateMonitor.Instance?.ReportHeaderInfo(
        groupNameInputField?.text ?? "",
        batchNumberInputField?.text ?? "",
        markingInputField?.text ?? "",
        notesInputField?.text ?? ""
        );

        // 2. Отправляем ПУСТЫЕ сигналы, чтобы запустить остальную логику.
        EventManager.Instance?.RaiseEvent(EventType.SetupHeaderInfoReady, EventArgs.Empty);
        EventManager.Instance?.RaiseEvent(EventType.TestParametersConfirmed, EventArgs.Empty);

        if (setupPanelUI != null) setupPanelUI.SetActive(false);        
        isButtonsContainerActive = false;
        _stateBeforeEdit = null;
        SystemStateMonitor.Instance?.ReportSetupValidity(false);
        EventManager.Instance?.RaiseEvent(EventType.RequestViewPlayContainerDisable, null);
    }

    private bool uiConfigShowsWidth()
    {
        if (_currentTestLogicHandler == null || currentTestData == null || _selectedSampleDataForUIConfig == null) return false;
        // Передаем _selectedMaterialPropertiesAsset для получения UI конфигурации
        var uiConf = _currentTestLogicHandler.GetSampleParametersUIConfig(currentTestData, _selectedSampleDataForUIConfig, _selectedMaterialPropertiesAsset);
        var widthFieldConf = uiConf.Fields.FirstOrDefault(f => f.ParameterName == "Width");
        return uiConf.IsWidthFieldRelevant && !string.IsNullOrEmpty(widthFieldConf.ParameterName) && widthFieldConf.IsVisible;
    }


    private void InternalShowPanel()
    {
        if (setupPanelUI != null)
        {
            bool templateSelected = !string.IsNullOrEmpty(currentTemplateName);
            if (TestSettingMessageText != null && templateNameDropdown != null) TestSettingMessageText.gameObject.SetActive(templateNameDropdown.value < 0);
            if (!templateSelected && sampleDisplayNameText != null) sampleDisplayNameText.text = "Шаблон не выбран";
            else if (templateSelected && sampleDisplayNameText != null) UpdateSampleDisplayName(currentTemplateName);
            setupPanelUI.SetActive(true);
            SystemStateMonitor.Instance?.ReportSetupValidity(true);
            if (buttonsContainer != null) buttonsContainer.SetActive(false);
            InternalHideTemplateDropdown();
        }
    }
    private void InternalHideAndResetPanel()
    {
        if (templateNameDropdown != null) templateNameDropdown.onValueChanged.RemoveAllListeners();
        if (materialDropdown != null) materialDropdown.onValueChanged.RemoveAllListeners(); // Отписка от дропдауна материалов
        if (shapeTypeDropdown != null) shapeTypeDropdown.onValueChanged.RemoveAllListeners();
        foreach (var inputField in _parameterInputFields.Values.Where(i => i != null)) inputField.onValueChanged.RemoveAllListeners();
        foreach (var dropdownField in _parameterDropdowns.Values.Where(d => d != null)) dropdownField.onValueChanged.RemoveAllListeners();
        if (speedInputField != null) speedInputField.onValueChanged.RemoveAllListeners();

        currentTemplateName = null;
        currentShapeType = SampleForm.Неопределено;
        currentTestData = null;
        _currentTestLogicHandler = null;
        _selectedSampleDataForUIConfig = null;
        _selectedMaterialPropertiesAsset = null;
        _currentTestLogicHandler_AssociatedConfigData = null;

        SystemStateMonitor.Instance?.ResetTestSetupState();
        SetVisibilityForAllParameterFields(false);
        if (templateNameDropdown != null) { templateNameDropdown.value = -1; if (templateNameDropdown.captionText != null) templateNameDropdown.captionText.text = "Выберите шаблон..."; templateNameDropdown.RefreshShownValue(); }
        if (materialDropdown != null) { materialDropdown.value = -1; if (materialDropdown.captionText != null) materialDropdown.captionText.text = "Материал"; materialDropdown.ClearOptions(); materialDropdown.interactable = false; materialDropdown.RefreshShownValue(); }
        if (shapeTypeDropdown != null) { shapeTypeDropdown.value = -1; if (shapeTypeDropdown.captionText != null) shapeTypeDropdown.captionText.text = "Форма"; shapeTypeDropdown.ClearOptions(); shapeTypeDropdown.interactable = false; shapeTypeDropdown.RefreshShownValue(); }
        if (sampleDisplayNameText != null) sampleDisplayNameText.text = "Образец";
        if (areaInputField != null) areaInputField.text = "0.00";
        ClearAllErrorMessages();

        if (tensileToleranceFieldsParent != null)
        {
            tensileToleranceFieldsParent.SetActive(false);
        }

        SetupStaticEventListeners(); // Восстанавливаем слушателей, включая нового для materialDropdown

        if (setupPanelUI != null) setupPanelUI.SetActive(false);
        SystemStateMonitor.Instance?.ReportSetupValidity(false);
        isButtonsContainerActive = false;
        EventManager.Instance?.RaiseEvent(EventType.RequestViewPlayContainerDisable, null);
    }
    private void InternalToggleTemplateDropdownVisibility()
    {
        if (templateNameDropdown == null) return;

        // 1. Определяем, каким должен стать новый статус
        bool shouldBecomeActive = !isTemplateDropdownActive;

        if (shouldBecomeActive)
        {
            // 2. Если мы собираемся ОТКРЫТЬ дропдаун, сначала проверяем, нужно ли сбросить состояние
            var monitor = SystemStateMonitor.Instance;
            if (monitor != null && string.IsNullOrEmpty(monitor.SelectedTemplateName))
            {
                // Сбрасываем и внутренние данные, и ВИЗУАЛЬНОЕ отображение
                currentTemplateName = null;
                templateNameDropdown.value = -1;
            }
        }

        // 3. Теперь применяем новый статус
        isTemplateDropdownActive = shouldBecomeActive;
        templateNameDropdown.gameObject.SetActive(isTemplateDropdownActive);

        // 4. Остальная логика остается без изменений
        if (isTemplateDropdownActive && setupPanelUI != null && setupPanelUI.activeSelf)
        {
            // Убрал вызов InternalHideAndResetPanel(), чтобы избежать рекурсии и лишнего сброса
            setupPanelUI.SetActive(false);
        }

        if (isTemplateDropdownActive)
        {
            StartCoroutine(ShowDropdownNextFrame());
        }
    }
    
    private System.Collections.IEnumerator ShowDropdownNextFrame()
    {
        // Ждем один кадр, чтобы UI успел обновиться
        yield return null; 

        // Вызываем Show(), когда дропдаун уже гарантированно активен
        if (templateNameDropdown != null && templateNameDropdown.gameObject.activeInHierarchy)
        {
            templateNameDropdown.Show();
        }
    }

    private void InternalHideTemplateDropdown()
    {
        if (templateNameDropdown != null && templateNameDropdown.gameObject.activeSelf)
        {
            templateNameDropdown.gameObject.SetActive(false);
            isTemplateDropdownActive = false;
        }
    }
    #endregion

    #region Parameter Handling (Helpers)
    void ClearAllErrorMessages()
    {
        SetErrorMessageText(thicknessErrorMessageText, "");
        SetErrorMessageText(widthErrorMessageText, "");
        SetErrorMessageText(lengthErrorMessageText, "");
        SetErrorMessageText(speedErrorMessageText, "");
        foreach (var errorTextUI in _parameterErrorTexts.Values.Where(et => et != null && et != speedErrorMessageText && et != thicknessErrorMessageText && et != widthErrorMessageText && et != lengthErrorMessageText))
        {
            SetErrorMessageText(errorTextUI, "");
        }
    }

    private void ClearTemporaryMessage()
    {
        if (TestSettingMessageText != null && TestSettingMessageText.gameObject.activeSelf)
        {
            // Не очищаем, если это основное сообщение о том, что шаблон не выбран
            if (TestSettingMessageText.text != "Шаблон не выбран")
            {
                TestSettingMessageText.text = "";
                TestSettingMessageText.gameObject.SetActive(false);
            }
        }
    }

    public class NaturalStringComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            // Регулярное выражение для разделения строки на нечисловую и числовую части
            // Например, "B123" будет разделено на "B" и "123"
            var regex = new System.Text.RegularExpressions.Regex("(?<text>\\D*)(?<digit>\\d*)");

            var matchX = regex.Match(x ?? "");
            var matchY = regex.Match(y ?? "");

            var textX = matchX.Groups["text"].Value;
            var textY = matchY.Groups["text"].Value;

            // Сначала сравниваем текстовые части
            int textCompareResult = string.Compare(textX, textY, StringComparison.OrdinalIgnoreCase);
            if (textCompareResult != 0)
            {
                return textCompareResult;
            }

            // Если текстовые части одинаковы, сравниваем числовые
            if (long.TryParse(matchX.Groups["digit"].Value, out long digitX) &&
                long.TryParse(matchY.Groups["digit"].Value, out long digitY))
            {
                return digitX.CompareTo(digitY);
            }

            // Если не удалось распарсить числа, возвращаемся к стандартному сравнению
            return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
        }
    }
    
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
            // Указываем правильный класс в сообщении об ошибке
            Debug.LogError($"[SetupPanelController] ToDoManager.Instance не найден! Не могу отправить Prompt Update Command для ключа '{keyOrId}'.");
        }
    }
    #endregion
}