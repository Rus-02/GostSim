using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;
using DG.Tweening;

/// <summary>
/// Управляет жизненным циклом объектов оснастки (Fixtures) в сцене.
/// Отвечает за их создание, удаление, анимацию и отслеживание состояния.
/// Является центральным исполнителем для всех команд, связанных с оснасткой.
/// </summary>
public class FixtureController : MonoBehaviour
{
    #region Singleton
    //================================================================================================================//
    // РЕГИОН 1: СИНГЛТОН И КЛЮЧЕВЫЕ ДАННЫЕ
    // Все, что скрипт "знает": его внутреннее состояние, словари с установленной оснасткой и ссылки на менеджеры.
    //================================================================================================================//

    private static FixtureController _instance;
    public static FixtureController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<FixtureController>();
                if (_instance == null)
                {
                    GameObject singletonObject = new GameObject("FixtureController");
                    _instance = singletonObject.AddComponent<FixtureController>();
                    Debug.Log("[FixtureController] Instance created automatically.");
                }
            }
            return _instance;
        }
    }

    [Header("Debugging")]
    [Tooltip("Включает подробное логирование каждого шага установки, удаления и анимации оснастки.")]
    [SerializeField] private bool enableVerboseLogging = false;

    private EventManager _eventManager;
    private MachineVisualData _currentVisualData;
    private const string FixtureTag = "FixtureInstance";

    // --- Словари для отслеживания состояния ---
    private Dictionary<GameObject, (Vector3, Quaternion)> _savedObjectTransforms = new Dictionary<GameObject, (Vector3, Quaternion)>();
    private Dictionary<FixtureZone, GameObject> _installedFixtures = new Dictionary<FixtureZone, GameObject>();
    private Dictionary<FixtureZone, FixtureData> _installedFixtureData = new Dictionary<FixtureZone, FixtureData>();
    private Dictionary<int, Dictionary<string, GameObject>> _installedInternalFixtures = new Dictionary<int, Dictionary<string, GameObject>>();
    private Dictionary<int, Dictionary<string, FixtureData>> _installedInternalFixtureData = new Dictionary<int, Dictionary<string, FixtureData>>();
    private Dictionary<FixtureZone, Transform> _zoneTransforms = new Dictionary<FixtureZone, Transform>();
    private ITestLogicHandler _activeLogicHandler; 

    #endregion

    #region Unity Lifecycle & Subscriptions
    //================================================================================================================//
    // РЕГИОН 2: ЖИЗНЕННЫЙ ЦИКЛ И ПОДПИСКИ
    // Стандартные методы Unity (Awake, Start, OnDestroy) и управление подписками на команды от ToDoManager.
    //================================================================================================================//

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        SubscribeToCommands();
    }

    void Start()
    {
        _eventManager = EventManager.Instance;
        if (_eventManager == null) { Debug.LogError("[FC] EventManager not found!"); }
        try { var go = GameObject.FindWithTag(FixtureTag); }
        catch (UnityException) { Debug.LogError($"[FC] Тег '{FixtureTag}' не найден!"); }
    }

    private void OnDestroy()
    {
        UnsubscribeFromCommands();
    }

    private void SubscribeToCommands()
    {
        if (ToDoManager.Instance == null) { Debug.LogError("[FC] ToDoManager null during subscription!"); return; }
        var tm = ToDoManager.Instance;
        tm.SubscribeToAction(ActionType.PlaceFixtureByIdentifier, HandlePlaceFixtureCommand);
        tm.SubscribeToAction(ActionType.RemoveFixtureByIdentifier, HandleRemoveFixtureCommand);
        tm.SubscribeToAction(ActionType.InitializeFixturesAtStartup, HandleInitializeFixturesCommand);
        tm.SubscribeToAction(ActionType.PlayFixtureAnimationAction, HandlePlayFixtureAnimationCommand);
        tm.SubscribeToAction(ActionType.PlaceFixtureWithoutAnimation, HandlePlaceFixtureWithoutAnimationCommand);
        tm.SubscribeToAction(ActionType.ReinitializeFixtureZones, HandleReinitializeFixtureZonesCommand);
        tm.SubscribeToAction(ActionType.SetCurrentLogicHandler, HandleSetCurrentLogicHandler);
    }

    private void UnsubscribeFromCommands()
    {
        var tm = ToDoManager.Instance;
        if (tm != null)
        {
            tm.UnsubscribeFromAction(ActionType.PlaceFixtureByIdentifier, HandlePlaceFixtureCommand);
            tm.UnsubscribeFromAction(ActionType.RemoveFixtureByIdentifier, HandleRemoveFixtureCommand);
            tm.UnsubscribeFromAction(ActionType.InitializeFixturesAtStartup, HandleInitializeFixturesCommand);
            tm.UnsubscribeFromAction(ActionType.PlayFixtureAnimationAction, HandlePlayFixtureAnimationCommand);
            tm.UnsubscribeFromAction(ActionType.PlaceFixtureWithoutAnimation, HandlePlaceFixtureWithoutAnimationCommand);
            tm.UnsubscribeFromAction(ActionType.ReinitializeFixtureZones, HandleReinitializeFixtureZonesCommand);
            tm.UnsubscribeFromAction(ActionType.SetCurrentLogicHandler, HandleSetCurrentLogicHandler);
        }
    }

    #endregion

    #region ToDoManager Command Handlers (Input Layer)
    //================================================================================================================//
    // РЕГИОН 3: ОБРАБОТЧИКИ КОМАНД (ВХОДНАЯ ДВЕРЬ)
    // Методы, которые напрямую вызываются ToDoManager-ом. Их задача - принять команду, проверить аргументы
    // и передать управление основному методу-исполнителю.
    //================================================================================================================//

    private void HandlePlaceFixtureCommand(BaseActionArgs baseArgs)
    {
        if (baseArgs is PlaceFixtureArgs args) { PlaceFixtureById(args.FixtureId, args.ParentObject, args.InternalPointName); }
        else { Debug.LogError($"[FC] HandlePlaceFixtureCommand incorrect args: {baseArgs?.GetType().Name ?? "null"}. Expected PlaceFixtureArgs."); }
    }

    private void HandleRemoveFixtureCommand(BaseActionArgs baseArgs)
    {
        if (baseArgs is RemoveFixtureArgs args)
        {
            FixtureData data = FixtureManager.Instance?.GetFixtureData(args.FixtureId);
            if (data != null && data.OutAnimation != null) { RemoveFixtureWithAnimationById(args.FixtureId, args.ParentObject, args.InternalPointName); }
            else { RemoveFixtureById(args.FixtureId, args.ParentObject, args.InternalPointName); }
        }
        else { Debug.LogError($"[FC] HandleRemoveFixtureCommand incorrect args: {baseArgs?.GetType().Name ?? "null"}. Expected RemoveFixtureArgs."); }
    }

    private void HandleInitializeFixturesCommand(BaseActionArgs baseArgs)
    {
        InitializeFixturesAtStartup();
    }

    private void HandlePlayFixtureAnimationCommand(BaseActionArgs baseArgs)
    {
        if (baseArgs is PlayFixtureAnimationArgs args)
        {
            FixtureData fixtureData = FixtureManager.Instance?.GetFixtureData(args.FixtureId);
            if (fixtureData == null) { Debug.LogError($"[FC PlayAnim] FixtureData not found: {args.FixtureId}"); return; }

            FixtureAnimationData animData = null;
            switch (args.Direction)
            {
                case AnimationDirection.In: animData = fixtureData.InAnimation; break;
                case AnimationDirection.Out: animData = fixtureData.OutAnimation; break;
                case AnimationDirection.SampleInstall: animData = fixtureData.SampleInstallAnimation; break;
                case AnimationDirection.SampleRemove: animData = fixtureData.SampleRemoveAnimation; break;
                default: Debug.LogError($"[FC PlayAnim] Неизвестный AnimationDirection: {args.Direction}"); return;
            }

            if (animData == null) { Debug.LogError($"[FC PlayAnim] AnimationData для '{args.Direction}' не найдена для: {args.FixtureId}"); return; }

            Transform zoneTransform = GetZoneTransform(fixtureData.fixtureZone);
            if (zoneTransform == null) { Debug.LogError($"[FC PlayAnim] Zone transform not found: {fixtureData.fixtureZone}"); return; }

            GameObject targetObject = zoneTransform.gameObject;
            PlayFixtureAnimation(animData, targetObject, fixtureData, args.Requester, () => { });
        }
        else { Debug.LogError($"[FC] HandlePlayFixtureAnimationCommand incorrect args: {baseArgs?.GetType().Name ?? "null"}. Expected PlayFixtureAnimationArgs."); }
    }

    private void HandlePlaceFixtureWithoutAnimationCommand(BaseActionArgs baseArgs)
    {
        if (baseArgs is PlaceFixtureArgs args)
        {
            FixtureData dataToPlace = FixtureManager.Instance?.GetFixtureData(args.FixtureId);
            if (dataToPlace == null) { Debug.LogError($"[FC] Не удалось найти FixtureData для ID: {args.FixtureId} при мгновенной установке."); return; }
            PlaceFixtureWithoutAnimationInternal(dataToPlace, null, args.ParentObject, args.InternalPointName);
        }
        else { Debug.LogError($"[FC] HandlePlaceFixtureWithoutAnimationCommand incorrect args: {baseArgs?.GetType().Name ?? "null"}. Expected PlaceFixtureArgs."); }
    }

    private void HandleReinitializeFixtureZonesCommand(BaseActionArgs baseArgs)
    {
        if (_currentVisualData != null)
        {
            Debug.LogWarning("[FC] Re-initializing zones from cached data...");
            InitializeZoneTransforms(_currentVisualData);
        }
        else
        {
            Debug.LogError("[FC] Cannot reinitialize: No MachineVisualData cached.");
        }
    }

    private void HandleSetCurrentLogicHandler(BaseActionArgs baseArgs)
    {
        var monitor = SystemStateMonitor.Instance;
        if (monitor == null)
        {
            Debug.LogError("[FixtureController] SystemStateMonitor не найден! Не могу получить Logic Handler.");
            _activeLogicHandler = null;
            return;
        }

        // 2. Просто забираем ссылку из Монитора и сохраняем у себя
        _activeLogicHandler = monitor.CurrentTestLogicHandler;

        if (_activeLogicHandler != null)
        {
            Debug.LogWarning($"<color=purple>[FixtureController] Активный хендлер обновлен на: {_activeLogicHandler.GetType().Name} (из Монитора)</color>");
        }
        else
        {
            Debug.LogWarning("<color=purple>[FixtureController] Активный хендлер сброшен на null (из Монитора).</color>");
        }
    }

    #endregion

    #region Public API (External Queries)
    //================================================================================================================//
    // РЕГИОН 4: ПУБЛИЧНЫЙ API (СПРАВОЧНОЕ БЮРО)
    // Методы, которые другие скрипты могут вызывать, чтобы ПОЛУЧИТЬ ИНФОРМАЦИЮ о состоянии оснастки.
    // Они ничего не меняют, только отвечают на вопросы.
    //================================================================================================================//

    public List<string> GetInstalledMainFixtureIDs() { return _installedFixtureData.Values.Select(data => data.fixtureId).ToList(); }

    public GameObject GetInstalledFixtureObjectById(string fixtureId)
    {
        if (string.IsNullOrEmpty(fixtureId)) return null;
        foreach (var pair in _installedFixtureData)
        {
            if (pair.Value.fixtureId == fixtureId)
            {
                if (_installedFixtures.TryGetValue(pair.Key, out GameObject fixtureObject)) { return fixtureObject; }
            }
        }
        return null;
    }

    public FixtureData GetInstalledFixtureInZone(FixtureZone zone)
    {
        _installedFixtureData.TryGetValue(zone, out FixtureData data);
        return data;
    }

    public string GetInstalledFixtureIdInZone(FixtureZone zone)
    {
        if (_installedFixtureData.TryGetValue(zone, out FixtureData data))
        {
            return data.fixtureId;
        }
        return null;
    }

    public FixtureData GetInstalledInternalFixture(GameObject parentFixture, string internalPointName)
    {
        if (parentFixture == null || string.IsNullOrEmpty(internalPointName)) return null;
        int parentId = parentFixture.GetInstanceID();
        if (_installedInternalFixtureData.TryGetValue(parentId, out var internalDict)) { internalDict.TryGetValue(internalPointName, out FixtureData data); return data; }
        return null;
    }

    public ITestLogicHandler GetActiveLogicHandler()
    {
        return _activeLogicHandler;
    }

    #endregion

    #region Core Logic: Fixture Manipulation
    //================================================================================================================//
    // РЕГИОН 5: ОСНОВНАЯ ЛОГИКА (ГЛАВНЫЙ ЦЕХ)
    // Здесь происходит вся работа: создание, удаление, анимация GameObjects.
    // Эти методы вызываются из обработчиков команд.
    //================================================================================================================//

    public void PlaceFixtureById(string fixtureId, GameObject parentObject = null, string internalPointName = null)
    {
        FixtureData fixtureData = FixtureManager.Instance?.GetFixtureData(fixtureId);
        if (fixtureData != null)
        {
            if (parentObject == null || string.IsNullOrEmpty(internalPointName)) { PlaceFixtureInternal(fixtureData); }
            else { Transform internalPointTransform = FindInternalPointTransform(parentObject, internalPointName); if (internalPointTransform != null) { PlaceFixtureInternal(fixtureData, internalPointTransform, parentObject, internalPointName); } else { Debug.LogError($"[FC PlaceById] Internal point '{internalPointName}' not found in '{parentObject.name}'."); } }
        }
        else { Debug.LogError($"[FC PlaceById] FixtureData not found: {fixtureId}"); }
    }

    public void RemoveFixtureById(string fixtureId, GameObject parentObject = null, string internalPointName = null)
    {
        FixtureData dataToRemove = FixtureManager.Instance?.GetFixtureData(fixtureId);
        if (dataToRemove == null) { Debug.LogError($"[FC RemoveById] FixtureData not found: {fixtureId}"); return; }
        GameObject fixtureToRemove = null;
        if (parentObject == null || string.IsNullOrEmpty(internalPointName))
        {
            FixtureZone zoneToRemove = dataToRemove.fixtureZone;
            if (_installedFixtures.TryGetValue(zoneToRemove, out fixtureToRemove) && _installedFixtureData.TryGetValue(zoneToRemove, out var installedData) && installedData.fixtureId == fixtureId)
            {
                _installedFixtures.Remove(zoneToRemove); _installedFixtureData.Remove(zoneToRemove);
                if (fixtureToRemove != null) Destroy(fixtureToRemove);
                ReportCurrentStateToMonitor(); // Этот метод уже сообщает актуальное состояние
                Debug.Log($"<color=orange>[FC RemoveById] Removed Event (INSTANT - Main) - ID: {fixtureId}</color>");
            }
        }
        else
        {
            int parentId = parentObject.GetInstanceID();
            if (_installedInternalFixtures.TryGetValue(parentId, out var internalDict) && internalDict.TryGetValue(internalPointName, out fixtureToRemove) && _installedInternalFixtureData.TryGetValue(parentId, out var internalDataDict) && internalDataDict.TryGetValue(internalPointName, out var installedInternalData) && installedInternalData.fixtureId == fixtureId) 
            {
                 internalDict.Remove(internalPointName);
                 internalDataDict.Remove(internalPointName);
                 if (internalDict.Count == 0) _installedInternalFixtures.Remove(parentId);
                 if (internalDataDict.Count == 0) _installedInternalFixtureData.Remove(parentId);
                 if (fixtureToRemove != null) Destroy(fixtureToRemove);
                ReportCurrentStateToMonitor();
                 Debug.Log($"<color=orange>[FC RemoveById] Removed Event (INSTANT - Internal) - ID: {fixtureId}</color>");
            }
        }
    }

    public void RemoveFixtureWithAnimationById(string fixtureId, GameObject parentObject = null, string internalPointName = null)
    {
        FixtureData fixtureData = FixtureManager.Instance?.GetFixtureData(fixtureId);
        if (fixtureData == null) { Debug.LogError($"[FC RemoveAnim] No FixtureData: {fixtureId}"); return; }
        if (fixtureData.OutAnimation == null) { Debug.LogWarning($"[FC RemoveAnim] No OUT anim data: {fixtureId}. Instant remove."); RemoveFixtureById(fixtureId, parentObject, internalPointName); return; }
        Transform targetPlacementTransform = null; FixtureZone zone = fixtureData.fixtureZone; bool isInstalled = false;
        if (parentObject == null || string.IsNullOrEmpty(internalPointName)) { targetPlacementTransform = GetZoneTransform(zone); if (_installedFixtures.ContainsKey(zone) && _installedFixtureData.ContainsKey(zone) && _installedFixtureData[zone].fixtureId == fixtureId) isInstalled = true; }
        else { targetPlacementTransform = FindInternalPointTransform(parentObject, internalPointName); if (targetPlacementTransform != null) { int parentId = parentObject.GetInstanceID(); if (_installedInternalFixtures.TryGetValue(parentId, out var iDict) && iDict.ContainsKey(internalPointName) && _installedInternalFixtureData.TryGetValue(parentId, out var iDataDict) && iDataDict.TryGetValue(internalPointName, out var iData) && iData.fixtureId == fixtureId) isInstalled = true; } }
        if (!isInstalled) { Debug.LogError($"[FC RemoveAnim] Fixture '{fixtureId}' not found at location."); return; }
        if (targetPlacementTransform == null) { Debug.LogError($"[FC RemoveAnim] No target transform: {fixtureId}."); return; }
        PlayFixtureAnimation(fixtureData.OutAnimation, targetPlacementTransform.gameObject, fixtureData, ActionRequester.None, null);
    }

    private GameObject PlaceFixtureInternal(FixtureData fixtureData, Transform parentTransformOverride = null, GameObject parentObject = null, string internalPointName = null)
    {
        if (fixtureData == null) { Debug.LogError("<color=red>[FC PlaceInternal] Invalid fixture data.</color>"); return null; }
        FixtureZone placementZone = fixtureData.fixtureZone;
        Transform parentTransform = parentTransformOverride ?? GetZoneTransform(placementZone);
        if (parentTransform == null) { Debug.LogError($"<color=red>[FC PlaceInternal] Parent Transform не найден для зоны: {placementZone}</color>"); return null; }
        if (fixtureData.prefabModel == null) { Debug.LogError($"<color=red>[FC PlaceInternal] prefabModel NULL for {fixtureData.fixtureId}</color>"); return null; }
        if (!fixtureData.prefabModel.CompareTag(FixtureTag)) Debug.LogWarning($"<color=orange>[FC PlaceInternal] Prefab '{fixtureData.prefabModel.name}' lacks tag '{FixtureTag}'.</color>");
        if (fixtureData.InAnimation != null && fixtureData.InAnimation.animationSteps != null && fixtureData.InAnimation.animationSteps.Count > 0)
        {
            Debug.Log($"[FC PlaceInternal] Запуск IN анимации для {fixtureData.displayName} на {parentTransform.name}");
            PlayFixtureAnimation(fixtureData.InAnimation, parentTransform.gameObject, fixtureData, ActionRequester.None, () => { GameObject fixtureInstance = null; int childCount = parentTransform.childCount; if (childCount > 0) { for (int i = 0; i < childCount; i++) { Transform child = parentTransform.GetChild(i); if (child != null && child.CompareTag(FixtureTag)) { fixtureInstance = child.gameObject; break; } } } if (fixtureInstance != null) { FinalizeFixturePlacement(fixtureData, fixtureInstance, placementZone, parentObject, internalPointName); } else { Debug.LogError($"[FC PlaceInternal] Не найден инстанс по тегу '{FixtureTag}' после анимации."); } });
            return null;
        }
        else
        {
            Debug.Log($"[FC PlaceInternal] Мгновенное размещение для {fixtureData.displayName} на {parentTransform.name}");
            GameObject fixtureInstance = Instantiate(fixtureData.prefabModel, parentTransform.position, parentTransform.rotation);
            fixtureInstance.transform.SetParent(parentTransform, false); fixtureInstance.transform.localPosition = Vector3.zero; fixtureInstance.transform.localRotation = Quaternion.identity;
            FinalizeFixturePlacement(fixtureData, fixtureInstance, placementZone, parentObject, internalPointName);
            return fixtureInstance;
        }
    }

    private GameObject PlaceFixtureWithoutAnimationInternal(FixtureData fixtureData, Transform parentTransformOverride = null, GameObject parentObject = null, string internalPointName = null)
    {
        if (fixtureData == null) { Debug.LogError("<color=red>PlaceNoAnimInternal - Invalid data.</color>"); return null; }
        FixtureZone placementZone = fixtureData.fixtureZone;
        Transform parentTransform = null;
        if (parentObject != null && !string.IsNullOrEmpty(internalPointName))
        {
            parentTransform = FindInternalPointTransform(parentObject, internalPointName);
            if (parentTransform == null) { Debug.LogError($"[FC PlaceNoAnimInternal] Не найдена точка '{internalPointName}' в '{parentObject.name}'."); return null; }
        }
        else
        {
            parentTransform = parentTransformOverride ?? GetZoneTransform(placementZone);
            if (parentTransform == null) { Debug.LogError($"<color=red>PlaceNoAnimInternal - Parent Transform не найден для зоны: {placementZone}</color>"); return null; }
        }
        if (fixtureData.prefabModel == null) { Debug.LogError($"<color=red>PlaceNoAnimInternal - Prefab null для {fixtureData.fixtureId}</color>"); return null; }
        if (!fixtureData.prefabModel.CompareTag(FixtureTag)) Debug.LogWarning($"<color=orange>PlaceNoAnimInternal: Prefab '{fixtureData.prefabModel.name}' lacks tag '{FixtureTag}'.</color>");

        GameObject fixtureInstance = Instantiate(fixtureData.prefabModel, parentTransform.position, parentTransform.rotation);
        fixtureInstance.transform.SetParent(parentTransform, false); fixtureInstance.transform.localPosition = Vector3.zero; fixtureInstance.transform.localRotation = Quaternion.identity;
        FinalizeFixturePlacement(fixtureData, fixtureInstance, placementZone, parentObject, internalPointName);
        return fixtureInstance;
    }

    private void PlayFixtureAnimation(FixtureAnimationData animationData, GameObject targetObject, FixtureData fixtureData, ActionRequester requester, Action onComplete = null)
    {
        Sequence sequence = DOTween.Sequence(); GameObject createdInstance = null;
        if (targetObject == null || animationData == null || fixtureData == null) { Debug.LogError($"[FC PlayAnim] Invalid params."); onComplete?.Invoke(); return; }
        bool usesInstantiate = animationData.animationSteps != null && animationData.animationSteps.Exists(step => step.stepType == AnimationStepType.InstantiatePrefab);
        if (usesInstantiate) { if (fixtureData.prefabModel == null) Debug.LogError($"<color=red>[FC PlayAnim] Prefab null for {fixtureData.fixtureId}</color>"); else if (!fixtureData.prefabModel.CompareTag(FixtureTag)) Debug.LogWarning($"<color=orange>[FC PlayAnim] Prefab '{fixtureData.prefabModel.name}' lacks tag '{FixtureTag}'.</color>"); }
        // Сообщаем в Монитор о начале анимации
        SystemStateMonitor.Instance?.ReportFixtureChangeStatus(true);
        if (animationData.animationSteps != null)
        {
            foreach (var step in animationData.animationSteps)
            {
                switch (step.stepType)
                {
                    case AnimationStepType.Move: sequence.Append(targetObject.transform.DOLocalMove(targetObject.transform.localPosition + step.moveDirection, step.moveDuration).SetEase(step.moveEase)); break;
                    case AnimationStepType.Rotate: sequence.Append(targetObject.transform.DOLocalRotate(step.rotationAngle, step.rotationDuration, RotateMode.FastBeyond360).SetEase(step.rotationEase).SetRelative()); break;
                    case AnimationStepType.Wait: sequence.AppendInterval(step.waitTime); break;
                    case AnimationStepType.InstantiatePrefab: if (fixtureData.prefabModel != null) { sequence.AppendCallback(() => { GameObject instance = Instantiate(fixtureData.prefabModel, targetObject.transform.position, targetObject.transform.rotation); instance.transform.SetParent(targetObject.transform, false); instance.transform.localPosition = Vector3.zero; instance.transform.localRotation = Quaternion.identity; createdInstance = instance; }); } else Debug.LogError($"[FC AnimStep {animationData.animationDirection}] InstantiatePrefab FAILED - prefab null for {fixtureData.fixtureId}"); break;
                    case AnimationStepType.DestroyPrefab:
                        sequence.AppendCallback(() =>
                        {
                            GameObject childToDestroy = null;
                            if (targetObject.transform.childCount > 0)
                            {
                                for (int i = 0; i < targetObject.transform.childCount; i++)
                                {
                                    Transform child = targetObject.transform.GetChild(i);
                                    if (child.CompareTag(FixtureTag)) { childToDestroy = child.gameObject; break; }
                                }
                                if (childToDestroy == null && targetObject.transform.childCount > 0) childToDestroy = targetObject.transform.GetChild(0).gameObject;
                            }
                            if (childToDestroy != null)
                            {
                                Destroy(childToDestroy);
                            }
                            else { Debug.LogWarning($"[FC DestroyCB {animationData.animationDirection}] Child not found in {targetObject.name}."); }
                        }); break;
                    case AnimationStepType.SavePosition: _savedObjectTransforms[targetObject] = (targetObject.transform.localPosition, targetObject.transform.localRotation); break;
                    case AnimationStepType.RestorePosition: if (_savedObjectTransforms.TryGetValue(targetObject, out var savedTransform)) { sequence.Append(targetObject.transform.DOLocalMove(savedTransform.Item1, 0.1f).SetEase(Ease.Linear)); sequence.Join(targetObject.transform.DOLocalRotateQuaternion(savedTransform.Item2, 0.1f).SetEase(Ease.Linear)); } else Debug.LogWarning($"[FC AnimStep {animationData.animationDirection}] RestorePosition - No saved transform for {targetObject.name}."); break;
                }
            }
        }
        sequence.AppendCallback(() =>
        {
            _eventManager?.RaiseEvent(EventType.FixtureAnimationFinished, new FixtureEventArguments(this, fixtureData.fixtureId, fixtureData.fixtureZone, requester));
            if (animationData.animationDirection == AnimationDirection.In)
            {
                if (createdInstance != null) { FinalizeFixturePlacement(fixtureData, createdInstance, fixtureData.fixtureZone, null, null); }
                else
                {
                    GameObject childInstance = null;
                    if (targetObject.transform.childCount > 0)
                    {
                        for (int i = 0; i < targetObject.transform.childCount; i++)
                        { var child = targetObject.transform.GetChild(i); if (child.CompareTag(FixtureTag)) { childInstance = child.gameObject; break; } }
                    }
                    if (childInstance != null) { FinalizeFixturePlacement(fixtureData, childInstance, fixtureData.fixtureZone, null, null); }
                    else { Debug.LogWarning($"<color=orange>FC FinalCB (IN):</color> Cannot finalize {fixtureData.fixtureId}, instance missing or not found."); }
                }
            }
            else if (animationData.animationDirection == AnimationDirection.Out)
            {
                FinalizeFixtureRemoval(fixtureData);
            }

            onComplete?.Invoke();
        });
        sequence.Play();
    }

    private void FinalizeFixturePlacement(FixtureData fixtureData, GameObject fixtureInstance, FixtureZone placementZone, GameObject parentObject = null, string internalPointName = null)
    {
        if (fixtureInstance == null) { Debug.LogError($"[FC Finalize] NULL instance for {fixtureData?.fixtureId}."); return; }
        if (fixtureData == null) { Debug.LogError("[FC Finalize] NULL data."); return; }

        if (parentObject == null || string.IsNullOrEmpty(internalPointName))
        {
            _installedFixtures[placementZone] = fixtureInstance;
            _installedFixtureData[placementZone] = fixtureData;
            if (enableVerboseLogging) Debug.Log($"[FC Finalize] Основная '{fixtureData.fixtureId}' установлена в {placementZone}.");
        }
        else
        {
            int parentId = parentObject.GetInstanceID();
            if (!_installedInternalFixtures.ContainsKey(parentId)) { _installedInternalFixtures[parentId] = new Dictionary<string, GameObject>(); _installedInternalFixtureData[parentId] = new Dictionary<string, FixtureData>(); }
            _installedInternalFixtures[parentId][internalPointName] = fixtureInstance;
            _installedInternalFixtureData[parentId][internalPointName] = fixtureData;

            _installedFixtures[placementZone] = fixtureInstance;
            _installedFixtureData[placementZone] = fixtureData;

            if (enableVerboseLogging) Debug.Log($"[FC Finalize] Вложенная '{fixtureData.fixtureId}' установлена в '{parentObject.name}' / '{internalPointName}'.");
        }
        ReportCurrentStateToMonitor();
    }

    private void FinalizeFixtureRemoval(FixtureData fixtureData)
    {
        if (fixtureData == null) return;
        FixtureZone zone = fixtureData.fixtureZone;
        if (_installedFixtureData.ContainsKey(zone) && _installedFixtureData[zone].fixtureId == fixtureData.fixtureId)
        {
            _installedFixtures.Remove(zone);
            _installedFixtureData.Remove(zone);
            if (enableVerboseLogging) Debug.Log($"<color=orange>[FixtureController]</color> Данные для оснастки '{fixtureData.fixtureId}' в зоне '{zone}' полностью очищены после анимации удаления.");
            ReportCurrentStateToMonitor();
        }
    }

    public void InitializeFixturesAtStartup()
    {
        FixtureManager fm = FixtureManager.Instance;
        if (fm == null) { Debug.LogError("[FC InitStartup] FixtureManager null!"); return; }
        List<string> fixtureIdsToPlaceAtStartup = new List<string>() {
        "DownComressionPlate", "UpComressionPlate",
        "HydraulicInsertCylindrical_LowerLeft 6-13", "HydraulicInsertCylindrical_LowerRight 6-13",
        "HydraulicInsertCylindrical_UpperLeft 6-13", "HydraulicInsertCylindrical_UpperRight 6-13"};

        Dictionary<FixtureZone, GameObject> placedMainFixtures = new Dictionary<FixtureZone, GameObject>();

        if (enableVerboseLogging) Debug.Log("[FC InitStartup] --- Проход 1: Установка Основной Оснастки ---");
        foreach (string fixtureId in fixtureIdsToPlaceAtStartup)
        {
            FixtureData data = fm.GetFixtureData(fixtureId);
            if (data == null) { Debug.LogError($"[FC InitStartup] FixtureData не найдена: {fixtureId}"); continue; }

            GameObject placedInstance = PlaceFixtureWithoutAnimationInternal(data, null, null, null);
            if (placedInstance != null)
            {
                if (!placedMainFixtures.ContainsKey(data.fixtureZone)) { placedMainFixtures.Add(data.fixtureZone, placedInstance); }
            }
        }
        if (enableVerboseLogging) Debug.Log("[FC InitStartup] Инициализация стартовой оснастки завершена.");
        ReportCurrentStateToMonitor();
    }

    #endregion

    #region Helper Methods & Utilities
    //================================================================================================================//
    // РЕГИОН 6: ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ (ЯЩИК С ИНСТРУМЕНТАМИ)
    // Функции для поиска объектов, инициализации и другой рутинной работы.
    // Здесь же лежит старый закомментированный код как артефакт.
    //================================================================================================================//

    /// <summary>
    /// Инициализация зон из Паспорта Машины.
    /// Вызывается внешним загрузчиком.
    /// </summary>
    public void InitializeZoneTransforms(MachineVisualData visualData)
    {
        if (visualData == null)
        {
            Debug.LogError("[FC] Initialize failed: MachineVisualData is null");
            return;
        }
        _currentVisualData = visualData;
        _zoneTransforms.Clear();

        // Карта: Enum Зоны -> Имя объекта в префабе
        var zoneMapping = new Dictionary<FixtureZone, string>
        {
            { FixtureZone.GripperUpper_Left, "GripperUpper_LeftPlacement" },
            { FixtureZone.GripperUpper_Right, "GripperUpper_RightPlacement" },
            { FixtureZone.GripperLower_Left, "GripperLower_LeftPlacement" },
            { FixtureZone.GripperLower_Right, "GripperLower_RightPlacement" },
            { FixtureZone.CompressionUpper, "UpperCompressionPlatePlacement" },
            { FixtureZone.CompressionLower, "LowerCompressionPlatePlacement" },
            { FixtureZone.ProportionalUpperZone, "ProportionalUpperZone" },
            { FixtureZone.ProportionalLowerZone, "ProportionalLowerZone" },
            // Пропорциональные
            { FixtureZone.PropFixturePoint_UpLeft, "PropFixturePoint_UpLeft" },
            { FixtureZone.PropFixturePoint_UpRight, "PropFixturePoint_UpRight" },
            { FixtureZone.PropFixturePoint_DownLeft, "PropFixturePoint_DownLeft" },
            { FixtureZone.PropFixturePoint_DownRight, "PropFixturePoint_DownRight" },
            { FixtureZone.PropRingUp, "PropRingUp" },
            { FixtureZone.PropRingDown, "PropRingDown" }
        };

        foreach (var kvp in zoneMapping)
        {
            // Запрашиваем точку у машины по имени
            Transform t = visualData.GetPoint(kvp.Value);
            
            if (t != null)
            {
                _zoneTransforms[kvp.Key] = t;
            }
        }
        
        Debug.Log($"[FC] Zones initialized. Found {_zoneTransforms.Count} zones.");
    }

    private void AddZoneTransform(FixtureZone zone, string objectName)
    {
        GameObject foundObj = GameObject.Find(objectName);
        if (foundObj != null) { _zoneTransforms[zone] = foundObj.transform; }
        else { Debug.LogWarning($"[FC] Зона крепления не найдена: '{objectName}' для зоны {zone}."); }
    }

    public Transform GetZoneTransform(FixtureZone zone)
    {
        if (_zoneTransforms.TryGetValue(zone, out Transform transform)) 
        { 
            return transform; 
        }
        else 
        { 
            // Пытаемся восстановить, только если есть данные
            if (_currentVisualData != null)
            {
                Debug.LogWarning($"[FC] Zone {zone} missing. Retrying init..."); 
                InitializeZoneTransforms(_currentVisualData); 
                
                if (_zoneTransforms.TryGetValue(zone, out transform)) return transform;
            }
            
            // Если данных нет или зона так и не нашлась
            return null; 
        }
    }

    private Transform FindInternalPointTransform(GameObject parentObject, string internalPointName)
    {
        if (parentObject == null || string.IsNullOrEmpty(internalPointName)) { Debug.LogError($"[FC FindInternal] Invalid params: Parent={parentObject?.name}, Point={internalPointName}"); return null; }
        Transform internalPointTransform = FindDeepChild(parentObject.transform, internalPointName);
        if (internalPointTransform == null) Debug.LogError($"[FC FindInternal] Point '{internalPointName}' not found in '{parentObject.name}'.");
        return internalPointTransform;
    }

    private Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent) { if (child.name == name) return child; Transform result = FindDeepChild(child, name); if (result != null) return result; }
        return null;
    }

    #endregion
    public List<string> GetAllInstalledFixtureIDs()
    {
        var allIds = new HashSet<string>();

        foreach (var data in _installedFixtureData.Values)
        {
            if (data != null && !string.IsNullOrEmpty(data.fixtureId))
            {
                allIds.Add(data.fixtureId);
            }
        }

        foreach (var parentDict in _installedInternalFixtureData.Values)
        {
            foreach (var data in parentDict.Values)
            {
                if (data != null && !string.IsNullOrEmpty(data.fixtureId))
                {
                    allIds.Add(data.fixtureId);
                }
            }
        }

        return allIds.ToList();
    }
    
    private void ReportCurrentStateToMonitor()
    {
        SystemStateMonitor.Instance?.ReportAllInstalledFixtures(GetAllInstalledFixtureIDs());
    }
}