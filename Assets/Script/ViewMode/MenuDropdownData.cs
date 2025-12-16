using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq; // Потребуется для LINQ

public class MenuDropdownData : MonoBehaviour
{
    [Header("Dropdown UI References")]
    [SerializeField] private TMP_Dropdown frameDropdown;
    [SerializeField] private TMP_Dropdown fixtureDropdown;
    [SerializeField] private TMP_Dropdown hydraulicsDropdown;
    [SerializeField] private TMP_Dropdown measurementDropdown;

    [Header("Associated GameObjects (For Highlighting)")]
    [SerializeField] private List<GameObject> frameObjects;
    [SerializeField] private List<GameObject> fixtureObjects;
    [SerializeField] private List<GameObject> hydraulicsObjects;
    [SerializeField] private List<GameObject> measurementObjects;

    [Header("Category Focus Targets")]
    [Tooltip("Объект, на котором центрироваться при выборе категории 'Силовая рама'")]
    [SerializeField] private Transform frameFocusTarget;
    [Tooltip("Объект, на котором центрироваться при выборе категории 'Оснастка' (может быть общий узел крепления)")]
    [SerializeField] private Transform fixtureFocusTarget;
    [Tooltip("Объект, на котором центрироваться при выборе категории 'Гидростанция'")]
    [SerializeField] private Transform hydraulicsFocusTarget;
    [Tooltip("Объект, на котором центрироваться при выборе категории 'Измерительная система' (может быть стол с ПК)")]
    [SerializeField] private Transform measurementFocusTarget;


    // Словарь для связи индекса в fixtureDropdown с исходным GameObject'ом (нужен из-за дедупликации)
    private Dictionary<int, GameObject> fixtureDropdownIndexToObjectMap = new Dictionary<int, GameObject>();
    // Словарь для хранения списка имен GameObject'ов (используемых как ID) для каждого типа оснастки
    private Dictionary<string, List<string>> fixtureTypeToGameObjectNames = new Dictionary<string, List<string>>();


    void Start()
    {
        PopulateAllDropdowns();
    }

    public void PopulateAllDropdowns()
    {
        PopulateDropdown(frameDropdown, frameObjects, "Силовая рама", false); // Обычное заполнение
///НОВОЕ///
        // Вызываем PopulateDropdown для оснастки, используя InteractableInfo вместо XrayFixtureInfo
        PopulateDropdown(fixtureDropdown, fixtureObjects, "Доступная оснастка", true); // true теперь означает использование InteractableInfo для оснастки
///КОНЕЦ НОВОЕ///
        PopulateDropdown(hydraulicsDropdown, hydraulicsObjects, "Компоненты гидростанции", false); // Обычное заполнение
        PopulateDropdown(measurementDropdown, measurementObjects, "Измерительные компоненты", false); // Обычное заполнение
    }

///НОВОЕ///
    // Обновленный метод PopulateDropdown с возможностью использовать InteractableInfo для оснастки
    private void PopulateDropdown(TMP_Dropdown dropdown, List<GameObject> objects, string defaultOptionText = "--- Выберите ---", bool useInteractableInfoForFixture = false)
    {
        if (dropdown == null) return;
        if (objects == null) objects = new List<GameObject>();

        dropdown.ClearOptions();
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();
        options.Add(new TMP_Dropdown.OptionData(defaultOptionText)); // Добавляем опцию по умолчанию

        // Если это fixtureDropdown и мы должны использовать InteractableInfo для получения имени типа оснастки
        if (useInteractableInfoForFixture && dropdown == fixtureDropdown)
        {
            fixtureDropdownIndexToObjectMap.Clear(); // Очищаем старый маппинг индекса к объекту
            fixtureTypeToGameObjectNames.Clear(); // Очищаем старый маппинг типа к списку имен/ID
            HashSet<string> uniqueDisplayNames = new HashSet<string>(); // Для отслеживания уникальных имен для опций dropdown
            int currentOptionIndex = 1; // Начинаем с 1, так как 0 - это "-- Выберите --"

            foreach (GameObject obj in objects)
            {
                if (obj == null) continue; // Пропускаем null объекты

                InteractableInfo interactableInfo = obj.GetComponent<InteractableInfo>(); // Получаем компонент InteractableInfo
                string fixtureTypeDisplayName = null; // Инициализируем имя типа оснастки
                string gameObjectName = obj.name; // Получаем имя объекта (используется как ID)

                // Проверяем, является ли объект оснасткой и имеет ли он корректные данные в InteractableInfo
                if (interactableInfo != null && interactableInfo.isFixture)
                {
                    fixtureTypeDisplayName = interactableInfo.FixtureTypeDisplayName; // Берем имя типа из InteractableInfo
                }

                if (string.IsNullOrEmpty(fixtureTypeDisplayName))
                {
                    // Если имя типа не задано (объект не оснастка, или InteractableInfo отсутствует, или FixtureTypeDisplayName пустое)
                    fixtureTypeDisplayName = gameObjectName + " (Тип не указан)"; // Используем имя объекта с пометкой
                    Debug.LogWarning($"[MenuDropdownData] GameObject '{gameObjectName}' не имеет корректного FixtureTypeDisplayName в InteractableInfo или не помечен как isFixture. Используется имя объекта как тип.");
                }

                // Добавляем имя объекта (ID) в список для соответствующего типа
                if (!fixtureTypeToGameObjectNames.ContainsKey(fixtureTypeDisplayName))
                {
                    // Если такого типа еще нет в словаре, создаем новый список
                    fixtureTypeToGameObjectNames[fixtureTypeDisplayName] = new List<string>();
                }
                // Добавляем имя текущего объекта в список для этого типа
                if (!fixtureTypeToGameObjectNames[fixtureTypeDisplayName].Contains(gameObjectName)) // Проверка на случай дубликатов в исходном списке objects
                {
                    fixtureTypeToGameObjectNames[fixtureTypeDisplayName].Add(gameObjectName);
                }

                // Добавляем уникальное имя типа в опции dropdown, если его еще нет
                if (uniqueDisplayNames.Add(fixtureTypeDisplayName))
                {
                    options.Add(new TMP_Dropdown.OptionData(fixtureTypeDisplayName)); // Добавляем уникальное имя типа в опции
                    // Сохраняем связь: индекс этой НОВОЙ опции -> этот GameObject (как представитель типа)
                    fixtureDropdownIndexToObjectMap[currentOptionIndex] = obj;
                    currentOptionIndex++; // Увеличиваем индекс для следующей уникальной опции
                }
                // Если fixtureTypeDisplayName не уникальный, опцию в dropdown не добавляем, но имя объекта уже добавлено в словарь fixtureTypeToGameObjectNames
            }
        }
        else // Обычное заполнение для других dropdown'ов
        {
            foreach (GameObject obj in objects)
            {
                if (obj != null) options.Add(new TMP_Dropdown.OptionData(obj.name));
                else options.Add(new TMP_Dropdown.OptionData("INVALID_ENTRY"));
            }
        }

        dropdown.AddOptions(options); // Добавляем собранные опции
    }
///КОНЕЦ НОВОЕ///

    // Обновленный метод GetGameObjectByIndex, учитывающий маппинг для fixtureDropdown
    public GameObject GetGameObjectByIndex(TMP_Dropdown sourceDropdown, int index)
    {
        if (index <= 0) return null; // Индекс 0 - это "-- Выберите --"

        // Если это fixtureDropdown, используем наш словарь для поиска объекта-представителя
        if (sourceDropdown == fixtureDropdown)
        {
            if (fixtureDropdownIndexToObjectMap.TryGetValue(index, out GameObject mappedObject))
            {
                return mappedObject; // Возвращаем объект, связанный с этим уникальным пунктом меню
            }
            else
            {
                 Debug.LogError($"[MenuDropdownData] Не удалось найти GameObject для индекса {index} в fixtureDropdownIndexToObjectMap!");
                 return null; // Не смогли найти соответствие
            }
        }
        else // Для других dropdown'ов используем старую логику
        {
            List<GameObject> targetList = null;
            if (sourceDropdown == frameDropdown) targetList = frameObjects;
            // else if (sourceDropdown == fixtureDropdown) targetList = fixtureObjects; // Эта ветка больше не нужна здесь
            else if (sourceDropdown == hydraulicsDropdown) targetList = hydraulicsObjects;
            else if (sourceDropdown == measurementDropdown) targetList = measurementObjects;

            if (targetList == null) return null;
            int listIndex = index - 1; // Корректируем индекс для списка (т.к. в списке нет "-- Выберите --")
            if (listIndex >= 0 && listIndex < targetList.Count) return targetList[listIndex];
            else return null;
        }
    }
}