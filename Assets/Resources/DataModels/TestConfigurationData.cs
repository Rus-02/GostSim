using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "TestConfigurationData", menuName = "Data Models/Test Configuration Data", order = 1)]
public class TestConfigurationData : ScriptableObject
{
    // --- 1. Основная Идентификация Испытания ---
    [Header("1. Основная Идентификация Испытания")]
    [Tooltip("Уникальное имя конфигурации испытания (имя ассета).")]
    public string testName = "Новый тест";

    [Tooltip("Имя шаблона испытания (для группировки и выбора в UI)")]
    public string templateName = "Стандартный шаблон";

    [Tooltip("Тип испытания (Растяжение, Сжатие и т.д.) - Информационно")]
    public TypeOfTest typeOfTest;
    public TestType testType;

    [TextArea(3, 5)] // Увеличим немного поле для удобства
    [Tooltip("Подробное описание теста для отображения в UI.")]
    public string testDescription = "Описание испытания...";

    [Tooltip("Отображаемое имя образца по умолчанию для UI (может быть переопределено из SampleData).")]
    public string SampleDisplayName = "Образец";

    // --- 2. Данные Графика и Стандартизация --- (УБРАНО В МАТЕРИАЛАССЕТ)

    public float testMoveSpeed = 10f;


    // --- 3. Параметры Симуляции и Машины ---
    [Space(10)]
    [Header("3. Параметры Симуляции и Машины")]

    [Tooltip("Начальная Y-позиция (мировая координата), в которую траверса возвращается перед началом испытания.")]
    public float initialTraversePosition = 0f;

    [Tooltip("Максимально допустимая верхняя позиция траверсы (мировая Y-координата).")]
    public float maxUpperTraversePosition = 10f;

    [Tooltip("Минимально допустимая нижняя позиция траверсы (мировая Y-координата).")]
    public float minLowerTraversePosition = -10f;

    [Tooltip("Как образец/симуляция должны вести себя после достижения точки разрушения (например, тип разлома).")]
    public TestType currentTestType = TestType.Tensile; // Убедись, что enum TestType определен и имеет смысл здесь

    // --- 4. Синхронизация с Анимацией ---
    [Space(10)]
    [Header("4. Синхронизация с Анимацией")]
    [Tooltip("Общая длительность основного анимационного клипа Unity (в секундах), который соответствует активной фазе испытания (обычно от начала движения до разрыва). Используется для скраббинга и контроля скорости анимации.")]
    [Min(0.01f)]
    public float testAnimationClipDuration = 3.0f;

    [Tooltip("Время (в секундах) ВНУТРИ основного анимационного клипа (`testAnimationClipDuration`), когда должно произойти событие разрыва (Animation Event). Должно быть синхронизировано с визуальным моментом разрыва в анимации.")]
    [Min(0f)]
    public float ruptureEventTimeInClip = 1.5f;

    // --- 5. Требования к Оснастке и Образцам ---
    [Space(10)]
    [Header("5. Требования к Оснастке и Образцам")]
    [Tooltip("Список ID-шников оснастки (FixtureData), которая ОБЯЗАТЕЛЬНО должна быть установлена для проведения этого испытания.")]
    public List<string> requiredFixtureIDs = new List<string>();

    [Tooltip("Список ID-шников оснастки (FixtureData), которая МОЖЕТ БЫТЬ использована с этим испытанием (например, для разных размеров образцов). Влияет на валидацию совместимости образца и оснастки.")]
    public List<string> potentialFixtureIDs = new List<string>();

    [Tooltip("Список ID-шников образцов (SampleData), которые СОВМЕСТИМЫ с данной конфигурацией испытания. Используется для фильтрации при выборе образца.")]
    public List<string> compatibleSampleIDs = new List<string>();

    [Tooltip("Требуется ли установка верхнего захвата/зажима?")]
    public bool requiresUpperClamp = true;
    [Tooltip("Требуется ли установка нижнего захвата/зажима?")]
    public bool requiresLowerClamp = true;
    [Tooltip("Возможна ли установка экстензометра?")]
    public bool IsExtensometerAllowed = false;

    // --- 6. Размещение Образца в Сцене ---
    [Space(10)]
    [Header("6. Размещение Образца в Сцене")]
    [Tooltip("Тэг GameObject'а (обычно триггер-зоны) на сцене, который определяет место для установки образца.")]
    public string samplePlacementZoneTag = "SamplePlacementZone";

    [Tooltip("Если true, образец будет автоматически размещен в зоне `samplePlacementZoneTag` при завершении настройки испытания. Если false, требуется ручное размещение (например, кнопкой).")]
    public bool placeSampleOnSetup = false;
    
    [Tooltip("ID сборочной последовательности (FixtureSequenceData) для этой конфигурации, если требуется сложная сборка.")]
    public List<string> assemblySequenceIds = new List<string>();

    // --- Валидация (без изменений) ---
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (testMoveSpeed <= 0)
        {
            Debug.LogWarning($"[TestConfigurationData] 'testMoveSpeed' в '{name}' должен быть больше нуля.", this);
            testMoveSpeed = 0.001f;
        }
        if (testAnimationClipDuration <= 0)
        {
            Debug.LogWarning($"[TestConfigurationData] 'testAnimationClipDuration' в '{name}' должен быть больше нуля.", this);
            testAnimationClipDuration = 0.01f;
        }
        if (ruptureEventTimeInClip < 0)
        {
            Debug.LogWarning($"[TestConfigurationData] 'ruptureEventTimeInClip' в '{name}' не может быть отрицательным.", this);
            ruptureEventTimeInClip = 0f;
        }
        if (ruptureEventTimeInClip > testAnimationClipDuration)
        {
            Debug.LogWarning($"[TestConfigurationData] 'ruptureEventTimeInClip' ({ruptureEventTimeInClip}s) в '{name}' не может быть больше общей длительности клипа ('testAnimationClipDuration' = {testAnimationClipDuration}s).", this);
            ruptureEventTimeInClip = testAnimationClipDuration;
        }
        if (minLowerTraversePosition > maxUpperTraversePosition)
        {
             Debug.LogWarning($"[TestConfigurationData] 'minLowerTraversePosition' ({minLowerTraversePosition}) не может быть больше 'maxUpperTraversePosition' ({maxUpperTraversePosition}) в '{name}'. Исправлено.", this);
             minLowerTraversePosition = maxUpperTraversePosition; // Или другое значение по умолчанию
        }
         if (initialTraversePosition < minLowerTraversePosition || initialTraversePosition > maxUpperTraversePosition)
        {
             Debug.LogWarning($"[TestConfigurationData] 'initialTraversePosition' ({initialTraversePosition}) находится вне допустимого диапазона ({minLowerTraversePosition} - {maxUpperTraversePosition}) в '{name}'. Установлено в середину диапазона.", this);
             initialTraversePosition = (minLowerTraversePosition + maxUpperTraversePosition) / 2f; // Как вариант
        }
    }
#endif
}