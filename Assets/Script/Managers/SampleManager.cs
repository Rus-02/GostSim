using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

public class SampleManager : MonoBehaviour
{
    private static SampleManager _instance;
    public static SampleManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<SampleManager>();
                if (_instance == null)
                {
                    GameObject managerObject = new GameObject("SampleManager");
                    _instance = managerObject.AddComponent<SampleManager>();
                    Debug.Log("[SampleManager] Instance создан автоматически.");
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    [Header("Список данных об образцах")]
    private Dictionary<string, SampleData> _sampleLookup; // Словарь для быстрого поиска образцов по ID
    private EventManager _eventManager;

    void Start()
    {
        _eventManager = EventManager.Instance;
        if (_eventManager == null)
        {
            Debug.LogError("[SampleManager] EventManager не найден!");
        }
        InitializeSampleDatabase();
    }

    private void InitializeSampleDatabase()
    {
        _sampleLookup = new Dictionary<string, SampleData>();
        
        // [ИЗМЕНЕНИЕ] Берем данные из DataManager, а не из инспектора
        if (DataManager.Instance == null)
        {
            Debug.LogError("[SampleManager] DataManager.Instance не найден! База данных образцов не будет инициализирована.");
            return;
        }

        var allSamplesFromDataManager = DataManager.Instance.AllSampleData;

        // Заполняем словарь из данных, полученных от DataManager
        foreach (var sampleData in allSamplesFromDataManager)
        {
            if (sampleData != null && !string.IsNullOrEmpty(sampleData.sampleId))
            {
                if (!_sampleLookup.ContainsKey(sampleData.sampleId))
                {
                    _sampleLookup.Add(sampleData.sampleId, sampleData);
                }
                else
                {
                    Debug.LogError($"[SampleManager] Обнаружен дубликат Sample ID: {sampleData.sampleId}.");
                }
            }
        }
    }


    // Метод для получения данных об образце по ID
    public SampleData GetSampleData(string sampleId)
    {
        if (string.IsNullOrEmpty(sampleId)) return null;
        if (_sampleLookup.TryGetValue(sampleId, out SampleData sampleData))
        {
            return sampleData;
        }
        else
        {
            Debug.LogWarning($"[SampleManager] Данные об образце не найдены для ID: {sampleId}");
            return null;
        }
    }

    public GameObject CreateAndSetupSample(string sampleId, Transform placementPoint, float targetDiameterThickness, float targetWidth, float targetLength)
    {
        // 1. Получить SampleData (Валидация входных данных)
        SampleData sampleData = GetSampleData(sampleId);
        if (sampleData == null)
        {
            Debug.LogError($"[SampleManager] CreateAndSetupSample - Не удалось найти SampleData для ID: {sampleId}");
            return null;
        }
        if (sampleData.prefabModel == null)
        {
            Debug.LogError($"[SampleManager] CreateAndSetupSample - PrefabModel равен null для SampleData ID: {sampleId}");
            return null;
        }
        if (placementPoint == null)
        {
            Debug.LogError($"[SampleManager] CreateAndSetupSample - placementPoint не может быть null!");
            return null;
        }

        // 2. Инстанцировать префаб
        GameObject sampleInstance = null;
        try
        {
            sampleInstance = Instantiate(sampleData.prefabModel, placementPoint.position, placementPoint.rotation);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SampleManager] Error instantiating prefab for ID {sampleId}: {ex.Message}");
            return null;
        }

        // 3. Установить родителя и локальные координаты
        sampleInstance.transform.SetParent(placementPoint, true);
        sampleInstance.transform.localPosition = Vector3.zero;
        sampleInstance.transform.localRotation = Quaternion.identity;

        // 4. Найти SampleController
        SampleController sampleController = sampleInstance.GetComponent<SampleController>();
        if (sampleController == null)
        {
            Debug.LogError($"[SampleManager] CreateAndSetupSample - Компонент SampleController не найден на экземпляре: {sampleInstance.name}. Destroying instance.");
            Destroy(sampleInstance);
            return null;
        }

        // 5. Вызвать метод настройки и масштабирования
        try
        {
            sampleController.SetupAndScaleSample(sampleData, targetDiameterThickness, targetWidth, targetLength);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SampleManager] Error calling SetupAndScaleSample on {sampleController.gameObject.name}: {ex.Message}");
            Destroy(sampleInstance); // Уничтожаем, если настройка не удалась
            return null;
        }

        // 6. Вернуть созданный экземпляр
        return sampleInstance;
    }
    
    public SampleData GetFirstCompatibleSampleData(TestConfigurationData testConfig, SampleForm selectedShape)
    {
        if (DataManager.Instance == null)
        {
            Debug.LogError("[SampleManager] GetFirstCompatibleSampleData: DataManager.Instance не найден!");
            return null;
        }
        var allSamples = DataManager.Instance.AllSampleData; // Получаем полный список всех образцов из DataManager

        if (testConfig == null)
        {
            Debug.LogWarning("[SampleManager] GetFirstCompatibleSampleData: testConfig is null. Поиск по всей базе.");
            return allSamples.FirstOrDefault(sd => sd != null && sd.sampleForm == selectedShape);
        }

        // Используем 'compatibleSampleIDs', так как оно предназначено для образцов
        if (testConfig.compatibleSampleIDs == null || testConfig.compatibleSampleIDs.Count == 0)
        {
            Debug.LogWarning($"[SampleManager] GetFirstCompatibleSampleData: Список 'compatibleSampleIDs' в конфигурации '{testConfig.name}' пуст. Поиск по всей базе для формы '{selectedShape}'.");
            return allSamples.FirstOrDefault(sd => sd != null && sd.sampleForm == selectedShape);
        }

        foreach (string sampleId in testConfig.compatibleSampleIDs) // Итерация по compatibleSampleIDs
        {
            if (string.IsNullOrEmpty(sampleId)) continue;

            SampleData sampleData = GetSampleData(sampleId); // Используем ваш существующий метод
            if (sampleData != null && sampleData.sampleForm == selectedShape)
            {
                return sampleData; // Нашли первый совместимый
            }
        }

        Debug.LogWarning($"[SampleManager] Не найден совместимый SampleData в списке 'compatibleSampleIDs' для TestConfig '{testConfig.name}' и формы '{selectedShape}'.");
        return null; // Не нашли совместимых
    }
}