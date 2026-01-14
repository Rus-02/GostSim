using UnityEngine;
using System.Collections;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class MachineLoader : MonoBehaviour
{
    [Header("Точка появления")]
    public Transform SpawnPoint;

    [Header("Ссылки на Менеджеры Сцены")]
    public MenuDropdownData MenuData;

    // Храним хендл операции, чтобы потом (при выходе) можно было выгрузить машину из памяти
    private AsyncOperationHandle<GameObject> _machineLoadHandle;

    void Start()
    {
        StartCoroutine(LoadProcess());
    }

    private void OnDestroy()
    {
        // ВАЖНО: Если мы выходим из сцены, нужно освободить память
        if (_machineLoadHandle.IsValid())
        {
            Addressables.ReleaseInstance(_machineLoadHandle);
        }
    }

    private IEnumerator LoadProcess()
    {
        if (SessionManager.Instance == null)
        {
            Debug.LogError("[MachineLoader] SessionManager NULL!");
            yield break;
        }

        var machineRef = SessionManager.Instance.MachineReference;
        if (machineRef == null || !machineRef.RuntimeKeyIsValid())
        {
            Debug.LogError("[MachineLoader] Ссылка на машину в SessionManager пустая или невалидная!");
            yield break;
        }

        Debug.Log($"[MachineLoader] Начало асинхронной загрузки...");

        Vector3 pos = SpawnPoint != null ? SpawnPoint.position : Vector3.zero;
        Quaternion rot = SpawnPoint != null ? SpawnPoint.rotation : Quaternion.identity;

        // 1. АСИНХРОННАЯ ЗАГРУЗКА И СОЗДАНИЕ
        // Мы используем InstantiateAsync. Это загрузит ассет в память И создаст его копию на сцене.
        _machineLoadHandle = Addressables.InstantiateAsync(machineRef, pos, rot);

        // Ждем завершения
        while (!_machineLoadHandle.IsDone)
        {
            yield return null;
        }

        if (_machineLoadHandle.Status == AsyncOperationStatus.Succeeded)
        {
            GameObject machineInstance = _machineLoadHandle.Result;
            Debug.Log($"[MachineLoader] Машина загружена: {machineInstance.name}");

            // Дальше всё как и было раньше (настройка зависимостей)
            InitializeMachineDependencies(machineInstance);
        }
        else
        {
            Debug.LogError($"[MachineLoader] Ошибка загрузки машины: {_machineLoadHandle.OperationException}");
        }
    }

    private void InitializeMachineDependencies(GameObject machineInstance)
    {
        // 2. Получаем паспорт
        MachineVisualData visualData = machineInstance.GetComponent<MachineVisualData>();
        if (visualData == null)
        {
            Debug.LogError("[MachineLoader] Нет MachineVisualData на загруженной машине!");
            return;
        }

        // 3. Инициализация систем
        if (CameraController.Instance != null) 
            CameraController.Instance.Initialize(visualData);

        if (MenuData != null) 
            MenuData.Initialize(visualData);

        if (FixtureController.Instance != null)
        {
            FixtureController.Instance.InitializeZoneTransforms(visualData);
            FixtureController.Instance.InitializeFixturesAtStartup();
        }

        if (ViewedStateManager.Instance != null)
            ViewedStateManager.Instance.Initialize(visualData);

        // Промпты (нужен синглтон или поиск)
        if (PromptController.Instance != null)
            PromptController.Instance.RegisterMachineInteractables(machineInstance);

        // 4. MachineController
        if (MachineController.Instance != null)
        {
            MachineConfigBase machineConfig = machineInstance.GetComponent<MachineConfigBase>();
            if (machineConfig != null)
            {
                MachineController.Instance.Initialize(machineConfig);
            }
            else
            {
                Debug.LogError("[MachineLoader] Нет MachineConfigBase на машине!");
            }
        }

        Debug.Log("[MachineLoader] Полная инициализация завершена.");
    }
}