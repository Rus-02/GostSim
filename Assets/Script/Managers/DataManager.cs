using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Централизованный менеджер для загрузки и хранения всех ScriptableObject ассетов.
/// Является единственным источником "сырых" данных для всех остальных систем.
/// Загружает все данные один раз при старте и предоставляет к ним доступ через Singleton.
/// </summary>
public class DataManager : MonoBehaviour
{
    #region Singleton
    public static DataManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // Гарантирует, что данные будут доступны между сценами

        LoadAllData();
    }
    #endregion

    #region Хранилища данных (Кэши ассетов)

    public List<TestConfigurationData> AllTestConfigs { get; private set; }
    public List<SampleData> AllSampleData { get; private set; }
    public List<FixtureData> AllFixtureData { get; private set; } // Включает все типы наследников (CompressionPlate, HydraulicInsert и т.д.)
    public List<MaterialPropertiesAsset> AllMaterials { get; private set; }
    public List<ReportConfiguration> AllReportConfigs { get; private set; }
    public List<MachineData> AllMachineData { get; private set; }
    
    #endregion

    #region Логика загрузки

    private void LoadAllData()
    {
        // Укажите здесь пути к вашим папкам с ассетами внутри папки Resources
        AllTestConfigs = Resources.LoadAll<TestConfigurationData>("DataModels/Test").ToList();
        AllSampleData = Resources.LoadAll<SampleData>("DataModels/Samples").ToList();
        AllFixtureData = Resources.LoadAll<FixtureData>("DataModels/Fixtures").ToList();
        AllMaterials = Resources.LoadAll<MaterialPropertiesAsset>("DataModels/Materials").ToList();
        AllReportConfigs = Resources.LoadAll<ReportConfiguration>("DataModels/Reports").ToList();
        AllMachineData = Resources.LoadAll<MachineData>("DataModels/MachineData").ToList();

        // Логирование результатов для отладки
        Debug.Log($"[DataManager] Загрузка данных завершена:");
        Debug.Log($"-- Конфигурации тестов: {AllTestConfigs.Count}");
        Debug.Log($"-- Данные образцов: {AllSampleData.Count}");
        Debug.Log($"-- Данные оснастки (все типы): {AllFixtureData.Count}");
        Debug.Log($"-- Материалы: {AllMaterials.Count}");
        Debug.Log($"-- Конфигурации отчетов: {AllReportConfigs.Count}");
        Debug.Log($"-- Данные машин: {AllMachineData.Count}");
    }

    #endregion

    #region Публичный API (методы для поиска)

    /// <summary>
    /// Находит FixtureData по уникальному ID.
    /// </summary>
    public FixtureData GetFixtureDataByID(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        var data = AllFixtureData.FirstOrDefault(f => f.fixtureId == id);
        if (data == null) Debug.LogWarning($"[DataManager] FixtureData с ID '{id}' не найден.");
        return data;
    }

    /// <summary>
    /// Находит SampleData по уникальному ID.
    /// </summary>
    public SampleData GetSampleDataByID(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        var data = AllSampleData.FirstOrDefault(s => s.sampleId == id);
        if (data == null) Debug.LogWarning($"[DataManager] SampleData с ID '{id}' не найден.");
        return data;
    }

    /// <summary>
    /// Находит TestConfigurationData по имени ассета.
    /// </summary>
    public TestConfigurationData GetTestConfigByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        var data = AllTestConfigs.FirstOrDefault(t => t.name == name);
        if (data == null) Debug.LogWarning($"[DataManager] TestConfigurationData с именем '{name}' не найден.");
        return data;
    }

    /// <summary>
    /// Находит MaterialPropertiesAsset по отображаемому имени.
    /// </summary>
    public MaterialPropertiesAsset GetMaterialByDisplayName(string displayName)
    {
        if (string.IsNullOrEmpty(displayName)) return null;
        var data = AllMaterials.FirstOrDefault(m => m.materialDisplayName == displayName);
        if (data == null) Debug.LogWarning($"[DataManager] MaterialPropertiesAsset с именем '{displayName}' не найден.");
        return data;
    }

    #endregion
}