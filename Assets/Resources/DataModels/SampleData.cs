using UnityEngine;
using System.Collections.Generic;

#region Перечисления (Enums)

// Определяет режим ввода для ОДНОГО конкретного размера образца (длины, ширины или толщины/диаметра).
public enum SingleDimensionInputMode
{
    [Tooltip("Ручной ввод. Ограничения могут накладываться только внешними факторами (например, оснасткой), но не настройками SampleData.")]
    ManualUnlimited,
    [Tooltip("Ручной ввод с проверкой на Min/Max значения, заданные ниже в настройках этого размера.")]
    ManualConstrained,
    [Tooltip("Выбор значения из предопределенного списка стандартных значений для этого размера. Поле ручного ввода будет заменено на выпадающий список.")]
    SelectStandard
}

// Определяет, является ли настройка размера главной (Master) или следует за другой.
public enum DimensionLinkMode
{
    [Tooltip("Этот размер настраивается независимо.")]
    Master,
    [Tooltip("Этот размер автоматически принимает значение 'Толщина/Диаметр'. Его собственные настройки ввода/значений игнорируются.")]
    FollowDiameterThickness,
    [Tooltip("Этот размер автоматически принимает значение 'Ширина'. Его собственные настройки ввода/значений игнорируются.")]
    FollowWidth,
    [Tooltip("Этот размер автоматически принимает значение 'Длина'. Его собственные настройки ввода/значений игнорируются.")]
    FollowLength
}

#endregion

#region Вспомогательный Класс Настроек Размера

// Содержит все настройки, касающиеся одного измерения образца 
[System.Serializable]
public class DimensionSetting
{
    [Tooltip("Название этого измерения (например, 'Длина', 'Ширина', 'Толщина'). Используется для ясности в инспекторе.")]
    [HideInInspector]
    public string dimensionName = "Dimension";

    [Header("Базовый Размер и Значение по Умолчанию")]
    [Tooltip("Базовый размер префаба по оси, соответствующей этому измерению, при масштабе (1,1,1). Например, если длина модели 100мм при scale.z=1, введите 100. Используется для расчета масштабирования модели.")]
    public float prefabBaseSize = 1.0f;

    [Tooltip("Значение по умолчанию для этого размера (мм). Используется как стартовое значение в поле ввода или как фиксированное значение, если ввод не предусмотрен.")]
    public float defaultValue = 100f;

    [Header("Режим Ввода и Зависимые Настройки")]
    [Tooltip("Режим ввода для этого конкретного размера:\n" +
             "- ManualUnlimited: Ручной ввод без ограничений Min/Max от образца.\n" +
             "- ManualConstrained: Ручной ввод с проверкой на Min/Max.\n" +
             "- SelectStandard: Выбор из списка стандартных значений.")]
    public SingleDimensionInputMode inputMode = SingleDimensionInputMode.ManualUnlimited;

    // --- Поля для режима ManualConstrained ---
    [Header("Ограничения (для режима 'ManualConstrained')")]
    [Tooltip("Минимально допустимое значение для этого размера (мм).")]
    public float minConstraint = 1f;
    [Tooltip("Максимально допустимое значение для этого размера (мм).")]
    public float maxConstraint = 1000f;

    // --- Поля для режима SelectStandard ---
    [Header("Стандартные значения (для режима 'SelectStandard')")]
    [Tooltip("Список стандартных значений (мм) для выбора в выпадающем списке.")]
    public List<float> standardValues = new List<float>();
    [Tooltip("Формат отображения стандартного значения в выпадающем списке UI. Используйте '{0}' для подстановки значения (например, '{0} мм').")]
    public string standardDisplayFormat = "{0} мм";

    [Header("Связь с другими размерами")]
    [Tooltip("Определяет, является ли этот размер главным (Master) или его значение берется из другого размера (Follow...).")]
    public DimensionLinkMode linkMode = DimensionLinkMode.Master; // По умолчанию - главный

    // Конструктор для удобного создания экземпляра с базовыми параметрами.
    public DimensionSetting(string name, float baseSize = 1.0f, float defValue = 100f)
    {
        dimensionName = name;
        prefabBaseSize = baseSize;
        defaultValue = defValue;
        minConstraint = 1f;
        maxConstraint = 1000f;
        standardValues = new List<float>();
        standardDisplayFormat = "{0} мм";
        inputMode = SingleDimensionInputMode.ManualUnlimited;
    }
}

#endregion

#region Основной Класс SampleData

// ScriptableObject для хранения полных данных об образце для испытаний.
[CreateAssetMenu(fileName = "SampleData", menuName = "Data Models/Samples/Sample Data (Flexible Dimensions)", order = 2)]
public class SampleData : ScriptableObject
{
    [Header("Идентификация")]
    [Tooltip("Уникальный ID образца")]
    public string sampleId;
    [Tooltip("Отображаемое имя образца в UI")]
    public string displayName;
    [Tooltip("Описание образца, ГОСТ и т.д.")]
    [TextArea] public string description;
    [Tooltip("Префаб 3D-модели образца")]
    public GameObject prefabModel;
    
    [Header("Геометрия для Визуализации")]
    [Tooltip("Длина части образца, которая зажимается в захвате с ОДНОЙ стороны, в мм. Для сжатия оставьте 0.")]
    public float ClampingLength = 0f;

    [Header("Сечение и форма образца")]
    [Tooltip("Основная форма сечения. Влияет на то, используется ли настройка Ширины.")]
    public SampleForm sampleForm;

    [Header("Настройки Размеров Образца")]

    [Space(10)]
    [Tooltip("Настройки для измерения 'Толщина' (для прямоугольных/квадратных) или 'Диаметр' (для круглых) образца.")]
    public DimensionSetting diameterThicknessSetting = new DimensionSetting("Толщина/Диаметр", 1.0f, 100f);

    [Space(10)]
    [Tooltip("Настройки для измерения 'Ширина' образца. Эти настройки ИГНОРИРУЮТСЯ, если форма образца (SampleForm) установлена как 'Круг'.")]
    public DimensionSetting widthSetting = new DimensionSetting("Ширина", 1.0f, 100f);

    [Space(10)]
    [Tooltip("Настройки для измерения 'Длина' образца (обычно вдоль основной оси нагружения).")]
    public DimensionSetting lengthSetting = new DimensionSetting("Длина", 1.0f, 100f);


    // Вспомогательный метод для получения базовых размеров префаба
    public Vector3 GetPrefabBaseDimensions()
{
    float xBase, yBase, zBase;

    // Ось Y всегда соответствует Длине
    yBase = lengthSetting?.prefabBaseSize ?? 1.0f;

    // Оси X и Z зависят от формы
    if (sampleForm == SampleForm.Круг)
    {
        // Для круга обе оси сечения (X и Z) используют размер Диаметра
        xBase = diameterThicknessSetting?.prefabBaseSize ?? 1.0f;
        zBase = diameterThicknessSetting?.prefabBaseSize ?? 1.0f;
    }
    else // Квадрат, Прямоугольник, Куб
    {
        xBase = widthSetting?.prefabBaseSize ?? 1.0f;
        zBase = diameterThicknessSetting?.prefabBaseSize ?? 1.0f;
    }

    return new Vector3(xBase, yBase, zBase);
}


    #if UNITY_EDITOR
    private void OnValidate()
    {
        if (diameterThicknessSetting != null) diameterThicknessSetting.dimensionName = "Толщина/Диаметр";
        if (widthSetting != null) widthSetting.dimensionName = "Ширина";
        if (lengthSetting != null) lengthSetting.dimensionName = "Длина";
    }
    #endif

}
#endregion