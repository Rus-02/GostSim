using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

public class FixtureManager : MonoBehaviour
{
    #region Singleton
    private static FixtureManager _instance;
    public static FixtureManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<FixtureManager>();
                if (_instance == null)
                {
                    GameObject managerObject = new GameObject("FixtureManager");
                    _instance = managerObject.AddComponent<FixtureManager>();
                    Debug.Log("[FixtureManager] Instance created automatically.");
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
    #endregion

    [Header("Databases (Назначьте в Инспекторе)")]
    [Tooltip("Список ScriptableObjects Плит Сжатия")]
    public List<CompressionPlateData> compressionPlateDatabase;
    [Tooltip("Список ScriptableObjects Гидравлических Вкладышей")]
    public List<HydraulicInsertData> hydraulicInsertDatabase;

    // Внутренний словарь для быстрого доступа по ID
    private Dictionary<string, FixtureData> _fixtureLookup;

    void Start()
    {
        InitializeFixtureDatabase();
    }

    private void InitializeFixtureDatabase()
    {
        // 1. Проверяем, существует ли DataManager
        if (DataManager.Instance == null)
        {
            Debug.LogError("[FixtureManager] DataManager.Instance is null! Невозможно инициализировать базу оснастки.");
            _fixtureLookup = new Dictionary<string, FixtureData>();
            return;
        }

        _fixtureLookup = new Dictionary<string, FixtureData>();

        // 2. Получаем готовый список всех ассетов оснастки из DataManager
        var allFixtures = DataManager.Instance.AllFixtureData;
        if (allFixtures == null)
        {
            Debug.LogError("[FixtureManager] DataManager.Instance.AllFixtureData is null!");
            return;
        }

        // 3. Обрабатываем полученный список (логика остается той же)
        foreach (var fixtureData in allFixtures)
        {
            if (fixtureData == null || string.IsNullOrEmpty(fixtureData.fixtureId))
            {
                Debug.LogWarning($"[FixtureManager] Обнаружен ассет FixtureData с null данными или пустым ID. Пропускается.");
                continue;
            }

            if (!_fixtureLookup.ContainsKey(fixtureData.fixtureId))
            {
                _fixtureLookup.Add(fixtureData.fixtureId, fixtureData);
            }
            else
            {
                Debug.LogError($"[FixtureManager] Обнаружен дубликат Fixture ID: {fixtureData.fixtureId}. Имя ассета: {fixtureData.name}.");
            }
        }

        Debug.Log($"[FixtureManager] Инициализация завершена. Загружено оснасток из DataManager: {_fixtureLookup.Count}");

        // 4. Опциональное заполнение списков в инспекторе (логика остается той же)
        if (compressionPlateDatabase == null) compressionPlateDatabase = new List<CompressionPlateData>();
        if (hydraulicInsertDatabase == null) hydraulicInsertDatabase = new List<HydraulicInsertData>();

        // Очищаем списки перед заполнением, чтобы избежать дублирования при перезагрузке
        compressionPlateDatabase.Clear();
        hydraulicInsertDatabase.Clear();

        foreach (var pair in _fixtureLookup)
        {
            if (pair.Value is CompressionPlateData cpData)
            {
                compressionPlateDatabase.Add(cpData);
            }
            else if (pair.Value is HydraulicInsertData hiData)
            {
                hydraulicInsertDatabase.Add(hiData);
            }
        }
    }

    /// Получает FixtureData для указанного ID оснастки. Использует внутренний словарь для эффективности.
    public FixtureData GetFixtureData(string fixtureId)
    {
        if (string.IsNullOrEmpty(fixtureId))
        {
            Debug.LogWarning($"[FixtureManager] GetFixtureData вызван с null или пустым fixtureId.");
            return null;
        }

        // Убедимся, что словарь инициализирован
        if (_fixtureLookup == null)
        {
            Debug.LogWarning("[FixtureManager] Словарь оснастки не инициализирован. Инициализация...");
            InitializeFixtureDatabase(); // Попытка инициализации, если вызвано до Start
        }

        if (_fixtureLookup.TryGetValue(fixtureId, out FixtureData fixtureData))
        {
            return fixtureData;
        }
        else
        {
            // Логируем как предупреждение, так как запрос несуществующих данных может быть намеренным
            Debug.LogWarning($"[FixtureManager] Данные оснастки не найдены для ID: {fixtureId}");
            return null;
        }
    }

    // --- Методы для получения специфичных типов (опционально, GetFixtureData более общий) ---

    /// Получаем CompressionPlateData по ID.
    public CompressionPlateData GetCompressionPlateData(string plateId)
    {
        FixtureData data = GetFixtureData(plateId);
        if (data is CompressionPlateData compressionPlateData)
        {
            return compressionPlateData;
        }
        return null;
    }

    /// Получаем HydraulicInsertData по ID.
    public HydraulicInsertData GetHydraulicInsertData(string insertId)
    {
        FixtureData data = GetFixtureData(insertId);
        if (data is HydraulicInsertData hydraulicInsertData)
        {
            return hydraulicInsertData;
        }
        return null;
    }

    /// Определяет список целевых ID оснастки, необходимых на основе конфигурации теста
    public List<string> ResolveTargetFixtureIDs(TestConfigurationData config, float sampleDimension)
    {
        if (config == null)
        {
            Debug.LogError("[FixtureManager] ResolveTargetFixtureIDs вызван с null TestConfigurationData.");
            return new List<string>(); // Возвращаем пустой список при ошибке
        }

        List<string> targetFixtureIDs = new List<string>();
        // Ключевой размер для захвата (предполагаем, что это DiameterThickness)
        if (config.potentialFixtureIDs == null)
        {
            Debug.LogWarning($"[FixtureManager] У TestConfigurationData '{config.name}' список potentialFixtureIDs равен null.");
            return targetFixtureIDs;
        }
            
        // Проверка валидности списка potentialFixtureIDs
        if (config.potentialFixtureIDs == null)
        {
            Debug.LogWarning($"[FixtureManager] У TestConfigurationData '{config.name}' список potentialFixtureIDs равен null.");
            return targetFixtureIDs; // Возвращаем пустой список
        }

        // 1. Итерация по всей потенциальной оснастке для этого типа теста
        foreach (string potentialID in config.potentialFixtureIDs)
        {
            FixtureData fixtureData = GetFixtureData(potentialID); // Используем существующий метод для получения данных

            if (fixtureData == null)
            {
                Debug.LogWarning($"[FixtureManager] Не удалось найти FixtureData для потенциального ID: {potentialID}, указанного в TestConfig: {config.name}. Пропускается.");
                continue; // Пропускаем этот ID, если данные отсутствуют
            }

            bool isCompatible = false;

            // 2. Проверка совместимости:
            // Проверяем, важен ли для этой оснастки диапазон захвата
            if (fixtureData is IClampRangeProvider rangeProvider)
            {
                // Это оснастка с определенным диапазоном захвата (например, HydraulicInsertData)
                // Проверяем, попадает ли размер образца в диапазон
                if (sampleDimension >= rangeProvider.MinGripDimension && sampleDimension <= rangeProvider.MaxGripDimension)
                {
                    isCompatible = true;
                }
                else { }
            }
            else
            {
                // Это оснастка без специфического диапазона захвата (например, CompressionPlateData или основной корпус захвата)
                // Считаем ее всегда совместимой с точки зрения размера для целей этого выбора.
                isCompatible = true;
                Debug.Log($"[FixtureManager] Оснастка '{fixtureData.displayName}' (ID: {potentialID}) считается совместимой (нет IClampRangeProvider).");
            }

            // 3. Добавляем совместимый ID в целевой список
            if (isCompatible)
            {
                // Избегаем дубликатов, если ID указан несколько раз в potentialFixtureIDs
                if (!targetFixtureIDs.Contains(potentialID))
                {
                    targetFixtureIDs.Add(potentialID);
                }
            }
        }
        return targetFixtureIDs;
    }    
}