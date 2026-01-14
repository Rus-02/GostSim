using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class MenuDropdownData : MonoBehaviour
{
    [Header("Dropdown UI References")]
    [SerializeField] private TMP_Dropdown frameDropdown;
    [SerializeField] private TMP_Dropdown fixtureDropdown;
    [SerializeField] private TMP_Dropdown hydraulicsDropdown;
    [SerializeField] private TMP_Dropdown measurementDropdown;

    // СЛОВАРИ ДЛЯ ХРАНЕНИЯ ДАННЫХ (Вместо списков в инспекторе)
    // Словарь: Дропдаун -> Список объектов в нем
    private Dictionary<TMP_Dropdown, List<GameObject>> _dropdownContentMap = new Dictionary<TMP_Dropdown, List<GameObject>>();

    // Словарь для связи индекса в fixtureDropdown с исходным GameObject'ом
    private Dictionary<int, GameObject> fixtureDropdownIndexToObjectMap = new Dictionary<int, GameObject>();
    
    // Словарь для хранения списка имен GameObject'ов для каждого типа оснастки
    private Dictionary<string, List<string>> fixtureTypeToGameObjectNames = new Dictionary<string, List<string>>();

    void Start()
    {
        // Теперь мы НЕ заполняем дропдауны на старте автоматически.
        // Мы ждем, пока MachineLoader вызовет Initialize().
        ClearAllDropdowns();
    }

    /// <summary>
    /// Главный метод инициализации. Вызывается Загрузчиком после появления машины.
    /// </summary>
    public void Initialize(MachineVisualData visualData)
    {
        ClearAllDropdowns();

        if (visualData == null) return;

        // 1. Собираем объекты по типам (объединяем списки, если категорий одного типа несколько)
        var frameObjs = CollectObjects(visualData, MachineVisualCategory.Frame);
        var fixtureObjs = CollectObjects(visualData, MachineVisualCategory.Fixture);
        var hydroObjs = CollectObjects(visualData, MachineVisualCategory.Hydraulics);
        var measureObjs = CollectObjects(visualData, MachineVisualCategory.Measurement);

        // 2. Заполняем дропдауны (один раз для каждого)
        PopulateDropdown(frameDropdown, frameObjs, "Силовая рама", false);
        
        // Для оснастки включаем спец. логику
        PopulateDropdown(fixtureDropdown, fixtureObjs, "Доступная оснастка", true);
        
        PopulateDropdown(hydraulicsDropdown, hydroObjs, "Компоненты гидростанции", false);
        PopulateDropdown(measurementDropdown, measureObjs, "Измерительные компоненты", false);
    }

    // Вспомогательный метод для сбора всех объектов конкретного типа в один список
    private List<GameObject> CollectObjects(MachineVisualData data, MachineVisualCategory type)
    {
        // Берем все категории этого типа -> Собираем их списки объектов -> Объединяем в один плоский список
        return data.VisualCategories
            .Where(c => c.CategoryType == type)
            .SelectMany(c => c.AssociatedObjects)
            .ToList();
    }

    private void ClearAllDropdowns()
    {
        _dropdownContentMap.Clear();
        if (frameDropdown) frameDropdown.ClearOptions();
        if (fixtureDropdown) fixtureDropdown.ClearOptions();
        if (hydraulicsDropdown) hydraulicsDropdown.ClearOptions();
        if (measurementDropdown) measurementDropdown.ClearOptions();
        
        fixtureDropdownIndexToObjectMap.Clear();
        fixtureTypeToGameObjectNames.Clear();
    }

    private void PopulateDropdown(TMP_Dropdown dropdown, List<GameObject> objects, string defaultOptionText, bool useInteractableInfoForFixture)
    {
        if (dropdown == null) return;
        if (objects == null) objects = new List<GameObject>();

        // Сохраняем список для геттера
        _dropdownContentMap[dropdown] = objects;

        dropdown.ClearOptions();
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();
        options.Add(new TMP_Dropdown.OptionData(defaultOptionText));

        // --- ВЕТКА 1: ОСНАСТКА (Fixture) ---
        // Здесь своя логика группировки по Типам, она остается специфичной
        if (useInteractableInfoForFixture && dropdown == fixtureDropdown)
        {
            HashSet<string> uniqueDisplayNames = new HashSet<string>();
            int currentOptionIndex = 1;

            foreach (GameObject obj in objects)
            {
                if (obj == null) continue;

                InteractableInfo info = obj.GetComponent<InteractableInfo>();
                string typeName = (info != null && info.isFixture && !string.IsNullOrEmpty(info.FixtureTypeDisplayName)) 
                                  ? info.FixtureTypeDisplayName 
                                  : obj.name + " (Тип не указан)";

                // Логика заполнения словарей для поиска
                if (!fixtureTypeToGameObjectNames.ContainsKey(typeName))
                    fixtureTypeToGameObjectNames[typeName] = new List<string>();
                
                if (!fixtureTypeToGameObjectNames[typeName].Contains(obj.name))
                    fixtureTypeToGameObjectNames[typeName].Add(obj.name);

                if (uniqueDisplayNames.Add(typeName))
                {
                    options.Add(new TMP_Dropdown.OptionData(typeName));
                    fixtureDropdownIndexToObjectMap[currentOptionIndex] = obj;
                    currentOptionIndex++;
                }
            }
        }
        // --- ВЕТКА 2: ВСЕ ОСТАЛЬНОЕ (Frame, Hydraulics, Measurement и т.д.) ---
        else 
        {
            foreach (GameObject obj in objects)
            {
                if (obj == null) continue;

                // 1. По умолчанию берем имя объекта (на случай, если скрипта нет)
                string label = obj.name; 

                // 2. Ищем InteractableInfo
                InteractableInfo info = obj.GetComponent<InteractableInfo>();
                
                // 3. Если нашли скрипт И у него заполнено shortDescription - берем его
                if (info != null && !string.IsNullOrWhiteSpace(info.shortDescription))
                {
                    label = info.shortDescription;
                }

                options.Add(new TMP_Dropdown.OptionData(label));
            }
        }

        dropdown.AddOptions(options);
    }

    public GameObject GetGameObjectByIndex(TMP_Dropdown sourceDropdown, int index)
    {
        if (index <= 0) return null;

        if (sourceDropdown == fixtureDropdown)
        {
            if (fixtureDropdownIndexToObjectMap.TryGetValue(index, out GameObject mappedObject))
            {
                return mappedObject;
            }
            return null;
        }
        else
        {
            // Универсальное получение для остальных дропдаунов через словарь
            if (_dropdownContentMap.TryGetValue(sourceDropdown, out List<GameObject> objectList))
            {
                int listIndex = index - 1;
                if (listIndex >= 0 && listIndex < objectList.Count) return objectList[listIndex];
            }
            return null;
        }
    }
}