using UnityEngine;
using TMPro;
using System;
using System.Globalization;

public class InfoTableController : MonoBehaviour
{
    [Header("UI Value Fields")]
    [SerializeField] private TextMeshProUGUI groupNameText;
    [SerializeField] private TextMeshProUGUI materialText;
    [SerializeField] private TextMeshProUGUI batchNumberText;
    [SerializeField] private TextMeshProUGUI markingText;
    [SerializeField] private TextMeshProUGUI sectionAreaText;
    [SerializeField] private TextMeshProUGUI testSpeedText;
    [SerializeField] private TextMeshProUGUI maxForceText;

    private const string DefaultText = "---";
    private const string ForceUnit = "кН";

    private void OnEnable()
    {
        if (EventManager.Instance != null)
        {
            EventManager.Instance.Subscribe(EventType.SetupHeaderInfoReady, this, HandleSetupHeaderInfo);
            EventManager.Instance.Subscribe(EventType.MaxForceCalculated, this, HandleMaxForceCalculated);
            Debug.Log("[InfoTableController] Subscribed to events.");
        }
        else
        {
            Debug.LogError("[InfoTableController] EventManager.Instance is null! Cannot subscribe.", this);
        }
        ClearTable();
    }

    private void OnDisable()
    {
        if (EventManager.Instance != null)
        {
            EventManager.Instance.Unsubscribe(EventType.SetupHeaderInfoReady, this, HandleSetupHeaderInfo);
            EventManager.Instance.Unsubscribe(EventType.MaxForceCalculated, this, HandleMaxForceCalculated);
            Debug.Log("[InfoTableController] Unsubscribed from events.");
        }
    }

    // Метод берет все данные из SystemStateMonitor
    private void HandleSetupHeaderInfo(EventArgs e)
    {
        var monitor = SystemStateMonitor.Instance;
        if (monitor == null)
        {
            Debug.LogError("[InfoTableController] SystemStateMonitor не найден! Невозможно обновить таблицу.");
            return;
        }

        Debug.Log($"[InfoTableController] Received SetupHeaderInfoReady signal. Updating from Monitor.");

        // Обновляем текстовые поля, читая данные из Монитора
        UpdateText(groupNameText, monitor.ReportGroupName);
        UpdateText(batchNumberText, monitor.ReportBatchNumber);
        UpdateText(markingText, monitor.ReportMarking);
        UpdateText(materialText, monitor.SelectedMaterialName);

        // Форматируем СЕЧЕНИЕ
        string formattedSection = DefaultText;
        if (monitor.CalculatedArea > 0 && monitor.SelectedShape != SampleForm.Неопределено)
        {
            string areaFormatted = monitor.CalculatedArea.ToString("F0", CultureInfo.InvariantCulture);
            string shapeName = monitor.SelectedShape.ToString();
            formattedSection = $"S={areaFormatted}мм<sup>2</sup>, {shapeName}";
        }
        else if (monitor.CalculatedArea > 0)
        {
             string areaFormatted = monitor.CalculatedArea.ToString("F0", CultureInfo.InvariantCulture);
             formattedSection = $"S={areaFormatted}мм<sup>2</sup>";
        }
        UpdateText(sectionAreaText, formattedSection);

        // Форматируем скорость
        string formattedSpeed = DefaultText;
        if (monitor.CurrentSampleParameters.TryGetValue("Speed", out float testSpeed) && testSpeed > 0)
        {
            string speedValueStr = testSpeed.ToString("F1", CultureInfo.InvariantCulture);
            switch (monitor.SelectedSpeedMode)
            {
                case TestSpeedMode.ForceRate:
                    formattedSpeed = $"V(F) = {speedValueStr} кН/с";
                    break;
                case TestSpeedMode.DeformationRate:
                default:
                    formattedSpeed = $"V(L) = {speedValueStr} мм/мин";
                    break;
            }
        }
        UpdateText(testSpeedText, formattedSpeed);
    }

    private void HandleMaxForceCalculated(EventArgs e)
    {
        var monitor = SystemStateMonitor.Instance;
        if (monitor == null)
        {
            Debug.LogError("[InfoTableController] SystemStateMonitor не найден! Невозможно обновить макс. силу.");
            return;
        }

        // Берем уже рассчитанное и сохраненное значение из Монитора
        float maxForce = monitor.MaxForceInTest_kN;
        
        Debug.Log($"[InfoTableController] Received MaxForceCalculated signal. Force from Monitor = {maxForce}");

        if (maxForce > 0)
        {
            float roundedForceValue;
            float roundingStep;

            if (maxForce < 50f)
            {
                roundingStep = 10.0f;
            }
            else if (maxForce <= 500f)
            {
                roundingStep = 50.0f;
            }
            else
            {
                roundingStep = 100.0f;
            }

            roundedForceValue = Mathf.Ceil(maxForce / roundingStep) * roundingStep;
            
            string formattedForce = $"F = {(int)roundedForceValue}{ForceUnit}";
            UpdateText(maxForceText, formattedForce);
        }
        else
        {
            UpdateText(maxForceText, DefaultText);
        }
    }

    private void UpdateText(TextMeshProUGUI textElement, string value)
    {
        if (textElement != null)
        {
            textElement.text = !string.IsNullOrEmpty(value) ? value : DefaultText;
        }
        else
        {
            Debug.LogWarning($"[InfoTableController] TextMeshProUGUI element '{gameObject.name}' is not assigned in the inspector.", this);
        }
    }

    public void ClearTable()
    {
        Debug.Log("[InfoTableController] Clearing table.");
        UpdateText(groupNameText, DefaultText);
        UpdateText(materialText, DefaultText);
        UpdateText(batchNumberText, DefaultText);
        UpdateText(markingText, DefaultText);
        UpdateText(sectionAreaText, DefaultText);
        UpdateText(testSpeedText, DefaultText);
        UpdateText(maxForceText, DefaultText);
    }
}