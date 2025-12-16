using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Хранит свойства материала и данные эталонного графика деформации.
/// Используется для определения поведения материала при испытаниях.
/// </summary>
[CreateAssetMenu(fileName = "NewMaterialProperties", menuName = "Simulation/Material Properties Asset", order = 1)]
public class MaterialPropertiesAsset : ScriptableObject
{
    [Header("1. Общая информация о материале")]
    [Tooltip("Наименование материала, отображаемое в UI (например, 'Сталь Ст3сп ГОСТ 380-2005')")]
    public string materialDisplayName = "Новый материал";

    [Tooltip("1.1. Список ИМЕН ШАБЛОНОВ тестов (TestConfigurationData.templateName), с которыми совместим данный материал.")]
    public List<string> compatibleTestTemplates = new List<string>();

    [Header("2. Эталонный график и его параметры")]
    [Tooltip("Текстовый файл с данными графика (формат: Напряжение_МПа|ОтносительнаяДеформация, каждая точка на новой строке).")]
    public TextAsset graphDataTextFile;

    [Tooltip("Стандартная начальная РАСЧЕТНАЯ ДЛИНА образца (L₀, в мм), для которой был записан эталонный график (используется для масштабирования оси X, если в файле абсолютная деформация). Если в файле относительная деформация, это значение больше информационное или для обратных расчетов.")]
    [Min(0.001f)]
    public float standardInitialLength_mm = 100f;

    [Tooltip("Стандартная начальная ПЛОЩАДЬ ПОПЕРЕЧНОГО СЕЧЕНИЯ образца (A₀, в мм^2), для которой был записан эталонный график (используется для масштабирования оси Y, если в файле сила, а не напряжение). Если в файле напряжение, это значение больше информационное или для обратных расчетов.")]
    [Min(0.001f)]
    public float standardInitialArea_mm2 = 10f;

    [Header("3. Ключевые механические свойства (МПа, %)")]
    [Tooltip("Модуль упругости (Модуль Юнга, E), в Мегапаскалях (МПа). Определяет наклон начального участка диаграммы.")]
    [Min(0f)]
    public float modulusOfElasticityE_MPa = 200000f;

    [Tooltip("Предел пропорциональности (σпц), в Мегапаскалях (МПа). Напряжение, до которого деформация считается упругой и линейной.")]
    [Min(0f)]
    public float proportionalityLimit_MPa = 200f;

    [Tooltip("Предел текучести (физический σт или условный σ₀.₂), в Мегапаскалях (МПа). Напряжение, при котором начинаются заметные пластические деформации.")]
    [Min(0f)]
    public float yieldStrength_MPa = 240f;

    [Tooltip("Временное сопротивление разрыву (предел прочности, σв), в Мегапаскалях (МПа). Максимальное напряжение, которое выдерживает материал перед разрушением.")]
    [Min(0f)]
    public float ultimateTensileStrength_MPa = 380f;

    [Tooltip("Пороговое значение напряжения (МПа), падение ниже которого ПОСЛЕ предела прочности считается моментом разрыва образца на графике.")]
    [Min(0f)] public float ruptureStressThreshold_MPa = 300f;

    [Tooltip("Относительное удлинение после разрыва (A или δ), в процентах (%). Характеризует пластичность материала.")]
    [Min(0f)]
    public float elongationAtBreak_Percent = 20f;

    [Tooltip("Относительное сужение после разрыва (Z или ψ), в процентах (%). Также характеризует пластичность материала.")]
    [Min(0f)]
    public float reductionOfArea_Percent = 50f;

    [Header("2. Физические свойства")]
    [Tooltip("Средняя плотность материала (ρ), в килограммах на кубический метр (кг/м³). Используется для расчета массы образца.")]
    [Min(0.001f)]
    public float averageDensity_kg_per_m3 = 7850f; // Типичная плотность стали

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (standardInitialLength_mm <= 0)
        {
            Debug.LogWarning($"[MaterialPropertiesAsset] 'standardInitialLength_mm' в '{name}' должен быть больше нуля. Установлено минимальное значение.", this);
            standardInitialLength_mm = 0.001f;
        }
        if (standardInitialArea_mm2 <= 0)
        {
            Debug.LogWarning($"[MaterialPropertiesAsset] 'standardInitialArea_mm2' в '{name}' должен быть больше нуля. Установлено минимальное значение.", this);
            standardInitialArea_mm2 = 0.001f;
        }
        if (modulusOfElasticityE_MPa < 0)
        {
            Debug.LogWarning($"[MaterialPropertiesAsset] 'modulusOfElasticityE_MPa' в '{name}' не может быть отрицательным. Установлено в 0.", this);
            modulusOfElasticityE_MPa = 0f;
        }
        if (proportionalityLimit_MPa < 0)
        {
            Debug.LogWarning($"[MaterialPropertiesAsset] 'proportionalityLimit_MPa' в '{name}' не может быть отрицательным. Установлено в 0.", this);
            proportionalityLimit_MPa = 0f;
        }
        if (yieldStrength_MPa < 0)
        {
            Debug.LogWarning($"[MaterialPropertiesAsset] 'yieldStrength_MPa' в '{name}' не может быть отрицательным. Установлено в 0.", this);
            yieldStrength_MPa = 0f;
        }
        if (ultimateTensileStrength_MPa < 0)
        {
            Debug.LogWarning($"[MaterialPropertiesAsset] 'ultimateTensileStrength_MPa' в '{name}' не может быть отрицательным. Установлено в 0.", this);
            ultimateTensileStrength_MPa = 0f;
        }
        if (elongationAtBreak_Percent < 0)
        {
            Debug.LogWarning($"[MaterialPropertiesAsset] 'elongationAtBreak_Percent' в '{name}' не может быть отрицательным. Установлено в 0.", this);
            elongationAtBreak_Percent = 0f;
        }
        if (reductionOfArea_Percent < 0)
        {
            Debug.LogWarning($"[MaterialPropertiesAsset] 'reductionOfArea_Percent' в '{name}' не может быть отрицательным. Установлено в 0.", this);
            reductionOfArea_Percent = 0f;
        }

        // Логические проверки между значениями
        if (proportionalityLimit_MPa > yieldStrength_MPa && yieldStrength_MPa > 0) // yieldStrength_MPa > 0 чтобы не ругаться, если еще не задано
        {
            Debug.LogWarning($"[MaterialPropertiesAsset] 'proportionalityLimit_MPa' ({proportionalityLimit_MPa} МПа) в '{name}' обычно не должен превышать 'yieldStrength_MPa' ({yieldStrength_MPa} МПа).", this);
        }
        if (yieldStrength_MPa > ultimateTensileStrength_MPa && ultimateTensileStrength_MPa > 0) // ultimateTensileStrength_MPa > 0 чтобы не ругаться, если еще не задано
        {
            Debug.LogWarning($"[MaterialPropertiesAsset] 'yieldStrength_MPa' ({yieldStrength_MPa} МПа) в '{name}' обычно не должен превышать 'ultimateTensileStrength_MPa' ({ultimateTensileStrength_MPa} МПа).", this);
        }
        if (graphDataTextFile == null)
        {
            Debug.LogWarning($"[MaterialPropertiesAsset] 'graphDataTextFile' в '{name}' не назначен. График не будет загружен.", this);
        }
    }
#endif
}