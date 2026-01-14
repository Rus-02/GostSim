using UnityEngine;
using System.Collections;

public class MachineLoader : MonoBehaviour
{
    [Header("Точка появления")]
    public Transform SpawnPoint;

    [Header("Ссылки на Менеджеры Сцены")]
    [Tooltip("Ссылка на MenuDropdownData в этой сцене")]
    public MenuDropdownData MenuData;
    
    // Остальные менеджеры найдем через синглтоны или можно привязать руками,
    // но для надежности лучше найти или привязать в инспекторе.
    
    void Start()
    {
        StartCoroutine(LoadProcess());
    }

    private IEnumerator LoadProcess()
    {
        // 1. Проверяем SessionManager
        if (SessionManager.Instance == null)
        {
            Debug.LogError("[MachineLoader] SessionManager не найден! Запускаю аварийный режим (без машины).");
            yield break;
        }

        GameObject prefabToLoad = SessionManager.Instance.MachinePrefab;
        if (prefabToLoad == null)
        {
            Debug.LogError("[MachineLoader] В SessionManager нет префаба машины!");
            yield break;
        }

        Debug.Log($"[MachineLoader] Загрузка машины: {prefabToLoad.name}...");

        // 2. Создаем машину
        Vector3 pos = SpawnPoint != null ? SpawnPoint.position : Vector3.zero;
        Quaternion rot = SpawnPoint != null ? SpawnPoint.rotation : Quaternion.identity;
        
        GameObject machineInstance = Instantiate(prefabToLoad, pos, rot);
        
        // 3. Получаем паспорт
        MachineVisualData visualData = machineInstance.GetComponent<MachineVisualData>();
        if (visualData == null)
        {
            Debug.LogError("[MachineLoader] На префабе машины НЕТ компонента MachineVisualData! Система не будет работать.");
            yield break;
        }

        // 4. Инициализация систем (ВАЖЕН ПОРЯДОК!)
        
        // А. Камера (чтобы не дергалась)
        if (CameraController.Instance != null) 
            CameraController.Instance.Initialize(visualData);

        // Б. Меню (заполняем списки)
        if (MenuData != null) 
            MenuData.Initialize(visualData);
        else 
            Debug.LogWarning("[MachineLoader] MenuDropdownData не назначен в инспекторе.");

        // В. Контроллер Оснастки (регистрируем зоны)
        if (FixtureController.Instance != null)
        {
            FixtureController.Instance.InitializeZoneTransforms(visualData);
            // Сразу ставим дефолтную оснастку (плиты и т.д.), если нужно
            FixtureController.Instance.InitializeFixturesAtStartup();
        }

        // Г. VSM (кожухи)
        if (ViewedStateManager.Instance != null)
            ViewedStateManager.Instance.Initialize(visualData);

        // Д. MachineController (Инициализация логики)
        if (MachineController.Instance != null)
        {
            // Ищем конфиг (HydraulicMachineConfig или другой) на корне машины
            MachineConfigBase machineConfig = machineInstance.GetComponent<MachineConfigBase>();
            
            if (machineConfig != null)
            {
                MachineController.Instance.Initialize(machineConfig);
            }
            else
            {
                Debug.LogError("[MachineLoader] На префабе машины НЕТ компонента MachineConfigBase (или наследника)!");
            }
        }
        else
        {
            Debug.LogError("[MachineLoader] MachineController отсутствует в сцене!");
        }
        
        Debug.Log("[MachineLoader] Загрузка и инициализация завершены.");
        
        yield return null;
    }
}