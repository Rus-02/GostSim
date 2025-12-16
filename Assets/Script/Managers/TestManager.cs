using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq; 

public class TestManager : MonoBehaviour
{
    #region Singleton
    private static TestManager _instance;
    public static TestManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<TestManager>();
                if (_instance == null)
                {
                    GameObject singletonObject = new GameObject("TestManager_Auto"); // Изменил имя, чтобы не конфликтовать, если уже есть TestManager
                    _instance = singletonObject.AddComponent<TestManager>();
                    Debug.Log("[TestManager] Instance создан автоматически.");
                }
            }
            return _instance;
        }
    }
    #endregion

    private TestConfigurationData _currentTestConfiguration;
    public TestConfigurationData CurrentTestConfiguration => _currentTestConfiguration;

    // Это поле хранит твой специфический TypeOfTest (например, WedgeGrip_Cylinder)
    private TypeOfTest _currentSpecificTestIdentifier;
    public TypeOfTest CurrentSpecificTestIdentifier => _currentSpecificTestIdentifier;

    // Кэш для всех загруженных конфигураций
    private List<TestConfigurationData> _allLoadedConfigurations;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        LoadAllConfigurations(); // Загружаем все конфигурации при старте
    }

    private void LoadAllConfigurations()
    {
        if (DataManager.Instance != null)
        {
            _allLoadedConfigurations = DataManager.Instance.AllTestConfigs;
        }
        else
        {
            Debug.LogError("[TestManager] DataManager.Instance не найден! Не могу получить конфигурации.");
            _allLoadedConfigurations = new List<TestConfigurationData>(); // Создаем пустой список, чтобы избежать ошибок
        }

        if (_allLoadedConfigurations.Count == 0)
        {
            Debug.LogWarning("[TestManager] Список конфигураций пуст.");
        }
        else
        {
            Debug.Log($"[TestManager] Получено {_allLoadedConfigurations.Count} конфигураций из DataManager.");
        }
    }

    // Этот метод устанавливает текущий тест на основе твоего специфического TypeOfTest
    // (например, по выбору пользователя из списка специфичных тестов)
    public void SetCurrentTestType(TypeOfTest specificIdentifier)
    {
        _currentSpecificTestIdentifier = specificIdentifier;
        // Здесь мы ищем конфигурацию, у которой поле typeOfTest (твой enum TypeOfTest)
        // совпадает с переданным specificIdentifier.
        // ПРЕДПОЛАГАЕТСЯ, ЧТО В TestConfigurationData ЕСТЬ ПОЛЕ: public TypeOfTest typeOfTest;
        _currentTestConfiguration = _allLoadedConfigurations.FirstOrDefault(config => config.typeOfTest == specificIdentifier);

        if (_currentTestConfiguration != null)
        {
            Debug.Log($"<color=yellow>[TestManager] Установлена конфигурация: {_currentTestConfiguration.testName} для специфического идентификатора: {specificIdentifier}</color>");
        }
        else
        {
            Debug.LogError($"<color=red>[TestManager] Не найдена конфигурация для специфического идентификатора: {specificIdentifier} среди загруженных.</color>");
        }
    }

    public TestConfigurationData GetCurrentTestConfiguration()
    {
        if (_currentTestConfiguration == null)
        {
            Debug.LogWarning("[TestManager] GetCurrentTestConfiguration: Текущая конфигурация испытания не установлена (null).");
        }
        return _currentTestConfiguration;
    }

    // Этот метод остался от твоего кода, он ищет по имени ресурса.
    // Он может быть полезен, если имя файла TestConfigurationData точно совпадает с testType.ToString().
    // Но более гибкий подход - искать по полю внутри ScriptableObject.
    public TestConfigurationData GetTestConfigurationByNameMatchingSpecificType(TypeOfTest specificTestType)
    {
        var config = _allLoadedConfigurations.FirstOrDefault(c => c.name == specificTestType.ToString());
        if (config == null)
        {
            Debug.LogError($"<color=red>[TestManager] Не удалось найти TestConfigurationData по имени ассета: {specificTestType}.</color>");
        }
        return config;
    }


    /// <summary>
    /// Находит TestConfigurationData, который соответствует указанному имени шаблона
    /// и совместим с указанной формой образца.
    /// </summary>
    /// <param name="templateName">Имя шаблона из TestConfigurationData.</param>
    /// <param name="shapeType">Требуемая форма образца (SampleForm).</param>
    /// <returns>Найденный TestConfigurationData или null, если не найден.</returns>
    public TestConfigurationData GetTestConfigurationForTemplateAndShape(string templateName, SampleForm shapeType)
    {
        if (string.IsNullOrEmpty(templateName))
        {
            Debug.LogWarning("[TestManager] GetTestConfigurationForTemplateAndShape: templateName не может быть null или пустым.");
            return null;
        }
        if (SampleManager.Instance == null)
        {
            Debug.LogError("[TestManager] GetTestConfigurationForTemplateAndShape: SampleManager.Instance не найден. SampleManager необходим для проверки совместимости образцов.");
            return null;
        }
        if (_allLoadedConfigurations == null)
        {
            Debug.LogError("[TestManager] GetTestConfigurationForTemplateAndShape: Список всех конфигураций не загружен (_allLoadedConfigurations is null).");
            return null;
        }

        foreach (TestConfigurationData config in _allLoadedConfigurations)
        {
            if (config == null) continue;

            // 1. Проверяем совпадение имени шаблона
            if (config.templateName == templateName)
            {
                // 2. Проверяем совместимость с формой образца через compatibleSampleIDs
                if (config.compatibleSampleIDs != null && config.compatibleSampleIDs.Count > 0)
                {
                    foreach (string sampleId in config.compatibleSampleIDs)
                    {
                        if (string.IsNullOrEmpty(sampleId)) continue;

                        SampleData sampleData = SampleManager.Instance.GetSampleData(sampleId);
                        if (sampleData != null && sampleData.sampleForm == shapeType)
                        {
                            // Найдена конфигурация, которая явно указывает совместимость с образцом данной формы
                            Debug.Log($"[TestManager] Найдена конфигурация '{config.testName}' для шаблона '{templateName}' и формы '{shapeType}' через compatibleSampleIDs.");
                            return config;
                        }
                    }
                }
            }
        }

        Debug.LogWarning($"[TestManager] Не найдена специфическая конфигурация для шаблона '{templateName}' и формы '{shapeType}'.");
        return null;
    }
}