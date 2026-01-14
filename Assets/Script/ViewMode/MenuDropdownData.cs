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

        foreach (var category in visualData.VisualCategories)
        {
            switch (category.CategoryType)
            {
                case MachineVisualCategory.Frame:
                    PopulateDropdown(frameDropdown, category.AssociatedObjects, "Силовая рама", false);
                    break;

                case MachineVisualCategory.Fixture:
                    // Для оснастки включаем спец. логику (InteractableInfo)
                    PopulateDropdown(fixtureDropdown, category.AssociatedObjects, "Доступная оснастка", true);
                    break;

                case MachineVisualCategory.Hydraulics:
                    PopulateDropdown(hydraulicsDropdown, category.AssociatedObjects, "Компоненты гидростанции", false);
                    break;

                case MachineVisualCategory.Measurement:
                    PopulateDropdown(measurementDropdown, category.AssociatedObjects, "Измерительные компоненты", false);
                    break;
                    
                // Если появятся новые типы (Protection, Electronics), добавь кейсы сюда
                // и привяжи к новым дропдаунам (если они будут в UI)
            }
        }
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

        // Сохраняем список объектов для этого дропдауна (для геттера GetGameObjectByIndex)
        _dropdownContentMap[dropdown] = objects;

        dropdown.ClearOptions();
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();
        options.Add(new TMP_Dropdown.OptionData(defaultOptionText));

        // --- ЛОГИКА ДЛЯ ОСНАСТКИ (Сложная) ---
        if (useInteractableInfoForFixture && dropdown == fixtureDropdown)
        {
            HashSet<string> uniqueDisplayNames = new HashSet<string>();
            int currentOptionIndex = 1;

            foreach (GameObject obj in objects)
            {
                if (obj == null) continue;

                InteractableInfo interactableInfo = obj.GetComponent<InteractableInfo>();
                string fixtureTypeDisplayName = null;
                string gameObjectName = obj.name;

                if (interactableInfo != null && interactableInfo.isFixture)
                {
                    fixtureTypeDisplayName = interactableInfo.FixtureTypeDisplayName;
                }

                if (string.IsNullOrEmpty(fixtureTypeDisplayName))
                {
                    fixtureTypeDisplayName = gameObjectName + " (Тип не указан)";
                }

                if (!fixtureTypeToGameObjectNames.ContainsKey(fixtureTypeDisplayName))
                {
                    fixtureTypeToGameObjectNames[fixtureTypeDisplayName] = new List<string>();
                }
                
                if (!fixtureTypeToGameObjectNames[fixtureTypeDisplayName].Contains(gameObjectName))
                {
                    fixtureTypeToGameObjectNames[fixtureTypeDisplayName].Add(gameObjectName);
                }

                if (uniqueDisplayNames.Add(fixtureTypeDisplayName))
                {
                    options.Add(new TMP_Dropdown.OptionData(fixtureTypeDisplayName));
                    fixtureDropdownIndexToObjectMap[currentOptionIndex] = obj;
                    currentOptionIndex++;
                }
            }
        }
        // --- ЛОГИКА ДЛЯ ОБЫЧНЫХ ОБЪЕКТОВ ---
        else 
        {
            foreach (GameObject obj in objects)
            {
                if (obj != null) options.Add(new TMP_Dropdown.OptionData(obj.name));
                else options.Add(new TMP_Dropdown.OptionData("INVALID_ENTRY"));
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