using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;

public class TestController : MonoBehaviour
{
    #region Singleton
    private static TestController _instance;
    public static TestController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<TestController>();
                if (_instance == null)
                {
                    GameObject singletonObject = new GameObject("TestController_Auto");
                    _instance = singletonObject.AddComponent<TestController>();
                    Debug.LogWarning("[TestController] Instance created automatically. Ensure it exists in the scene.");
                }
            }
            return _instance;
        }
    }
    #endregion

    private EventManager _eventManager;
    
    private const string CONVENTIONAL_TARGET_STATE_NAME = "DeformationPhase";
    private int _conventionalTargetStateHash;

    [Header("Animator Settings")]
    [Tooltip("Имя ТРИГГЕРА в Animator Controller для запуска основной анимации испытания.")]
    [SerializeField] private string animationTriggerName = "Test";
    [Tooltip("Индекс слоя аниматора, на котором находится основная анимация. Обычно 0.")]
    [SerializeField] private int baseLayerIndex = 0;

    private float _totalClipDuration;
    private float _ruptureEventTimeInClip;

    [Header("Sample Visual State (Readonly)")]
    [SerializeField] private Transform sampleParentTransform = null;
    [SerializeField] private List<Animator> sampleAnimators = new List<Animator>();
    [SerializeField] private Vector3 initialSampleScale = Vector3.one;
    
    [SerializeField] private float actualLength_mm = 1.0f; 
    
    [SerializeField] private float adaptedYieldPointX_graph = -1f;
    [SerializeField] private float adaptedRupturePointX_graph = -1f;
    [SerializeField] private bool areKeyPointsReceived = false; 


    [Header("Internal State (Readonly)")]
    [SerializeField] private bool isTestInitialized = false;
    [SerializeField] private bool isVisuallyRuptured = false; 
    [SerializeField] private bool isYieldReachedForAnimation = false; 
    [SerializeField] private bool isAnimationTriggered = false;    
    [SerializeField] private bool hasReachedRupturePointOnGraph = false;
    [SerializeField] private bool isRupturedNotifiedByAnimationEvent = false; 
    [SerializeField] private bool isMainAnimationLoopFinished = false; 
    [SerializeField] private bool isGraphFinished = false;
    [SerializeField] private int totalAnimatorsCount = 0;
    [SerializeField] private int finishedAnimatorsCount = 0;
    [SerializeField] private bool isPostRuptureTimerRunning = false;
    [SerializeField] private float postRuptureTimer = 0f;
    [SerializeField] private float postRuptureDuration = 0f; 

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning($"[TestController] Duplicate instance found. Destroying '{gameObject.name}'.");
            Destroy(gameObject);
            return;
        }
        _instance = this;
        _conventionalTargetStateHash = Animator.StringToHash(CONVENTIONAL_TARGET_STATE_NAME);
        SubscribeToToDoManagerCommands();
    }

    void Start()
    {
        _eventManager = EventManager.Instance;
        if (_eventManager == null) { Debug.LogError("[TestController] EventManager not found!"); }
        ResetTestState();
    }

    private void OnDestroy()
    {
        UnsubscribeFromToDoManagerCommands();
    }

    #region ToDoManager Command Subscription
    private void SubscribeToToDoManagerCommands()
    {
        if (ToDoManager.Instance == null)
        {
             Debug.LogError("[TestController] ToDoManager.Instance is null during subscription!");
             return;
        }
        var tm = ToDoManager.Instance;
        tm.SubscribeToAction(ActionType.InitializeTestController, HandleInitializeTestControllerCommand);
        tm.SubscribeToAction(ActionType.UpdateSampleVisuals, HandleUpdateSampleVisualsCommand);
        tm.SubscribeToAction(ActionType.NotifyTestControllerRupture, HandleNotifyRuptureCommand); 
        tm.SubscribeToAction(ActionType.NotifyTestControllerAnimationEnd, HandleNotifyAnimationEndCommand); 
        tm.SubscribeToAction(ActionType.ResetTestController, HandleResetTestControllerCommand);
        tm.SubscribeToAction(ActionType.PauseGraphAndSimulation, HandlePauseAnimationCommand);
        tm.SubscribeToAction(ActionType.ResumeGraphAndSimulation, HandleResumeAnimationCommand);
        tm.SubscribeToAction(ActionType.ResetSampleVisuals, HandleResetSampleVisualsCommand);
    }

    private void UnsubscribeFromToDoManagerCommands()
    {
        var tm = ToDoManager.Instance;
        if (tm != null)
        {
            tm.UnsubscribeFromAction(ActionType.InitializeTestController, HandleInitializeTestControllerCommand);
            tm.UnsubscribeFromAction(ActionType.UpdateSampleVisuals, HandleUpdateSampleVisualsCommand);
            tm.UnsubscribeFromAction(ActionType.NotifyTestControllerRupture, HandleNotifyRuptureCommand);
            tm.UnsubscribeFromAction(ActionType.NotifyTestControllerAnimationEnd, HandleNotifyAnimationEndCommand);
            tm.UnsubscribeFromAction(ActionType.ResetTestController, HandleResetTestControllerCommand);
            tm.UnsubscribeFromAction(ActionType.PauseGraphAndSimulation, HandlePauseAnimationCommand);
            tm.UnsubscribeFromAction(ActionType.ResumeGraphAndSimulation, HandleResumeAnimationCommand);
            tm.UnsubscribeFromAction(ActionType.ResetSampleVisuals, HandleResetSampleVisualsCommand);
        }
    }
    #endregion

    #region ToDoManager Command Handlers
    // Обработчик теперь просто вызывает метод без аргсов
    private void HandleInitializeTestControllerCommand(BaseActionArgs baseArgs)
    {
        InitializeForTest();
    }

    private void HandleUpdateSampleVisualsCommand(BaseActionArgs baseArgs)
    {
        var monitor = SystemStateMonitor.Instance;
        if (!isTestInitialized || monitor == null || monitor.CurrentSampleInstance == null) return;
        
        // Получаем все необходимые данные из Монитора
        var testLogicHandler = monitor.CurrentTestLogicHandler;
        if (testLogicHandler == null) return;

        float currentGraphRelativeDeformationPercent = monitor.CurrentRelativeStrain_Percent;
        var graphState = monitor.CurrentGraphState;
        var materialProps = monitor.SelectedMaterial;
        var testConfig = monitor.CurrentTestConfig;
        var sampleBehaviorHandler = monitor.CurrentSampleInstance?.GetComponentInChildren<SampleBehaviorHandler>();

        // Логика switch теперь работает с состоянием из Монитора
        switch (graphState)
        {
            case GraphController.GraphState.Idle:
                ResetTestState(); 
                return;
            case GraphController.GraphState.Paused:
                if (isPostRuptureTimerRunning) isPostRuptureTimerRunning = false;
                SetAnimatorsSpeed(0f);
                return;
            case GraphController.GraphState.Finished:
                isGraphFinished = true;
                if (hasReachedRupturePointOnGraph) { SetAnimatorsSpeed(1f); }
                CheckForTestCompletion();
                return;
            case GraphController.GraphState.Plotting:
                isGraphFinished = false;
                SetAnimatorsSpeed(1f); 
                break;
            default: 
                return;
        }
        
        // --- Основная логика обновления визуала (когда Plotting) ---

        // Длину берем из поля класса, которое было установлено при инициализации
        float currentActualLength = actualLength_mm; 

        testLogicHandler.UpdateSampleVisuals(
            sampleParentTransform,
            currentGraphRelativeDeformationPercent,
            materialProps,
            initialSampleScale,
            sampleAnimators.FirstOrDefault(), 
            sampleBehaviorHandler,
            testConfig,
            currentActualLength, 
            ref isVisuallyRuptured, 
            ref isYieldReachedForAnimation 
        );
        if (areKeyPointsReceived && totalAnimatorsCount > 0 && !isAnimationTriggered) 
        {
            bool shouldTrigger = false; 
            if (testLogicHandler != null && materialProps != null)
            {
                shouldTrigger = testLogicHandler.ShouldTriggerNeckingAnimation(currentGraphRelativeDeformationPercent, materialProps, adaptedYieldPointX_graph, isAnimationTriggered);
            }
            else
            {
                 Debug.LogError($"[TC UpdateVisuals] Cannot check ShouldTrigger: testLogicHandler or materialProps is null");
            }

            if (shouldTrigger)
            {
                isYieldReachedForAnimation = true; 
                Debug.Log($"<color=yellow>[TC] Main Animation Trigger Condition Met (by Handler). Triggering '{animationTriggerName}'. YieldPointX_Graph={adaptedYieldPointX_graph:F3}%</color>");
                EventManager.Instance?.RaiseEvent(EventType.NeckingStarted, EventArgs.Empty);
                isAnimationTriggered = true;
                isMainAnimationLoopFinished = false;
                isRupturedNotifiedByAnimationEvent = false;
                hasReachedRupturePointOnGraph = false; 
                finishedAnimatorsCount = 0;

                foreach (var animator in sampleAnimators)
                {
                    if (animator != null && animator.isInitialized)
                    {
                        animator.speed = 1f; 
                        animator.SetTrigger(animationTriggerName);
                    }
                }
            }
        }

        if (areKeyPointsReceived && isAnimationTriggered && !hasReachedRupturePointOnGraph && totalAnimatorsCount > 0 && 
            adaptedYieldPointX_graph >= 0 && adaptedRupturePointX_graph > adaptedYieldPointX_graph) 
        {
            float deltaGraphX_YieldToRupture = adaptedRupturePointX_graph - adaptedYieldPointX_graph;
            float progressRelativeToYield = currentGraphRelativeDeformationPercent - adaptedYieldPointX_graph;

            float animationProgressNorm = 0f;
            if (deltaGraphX_YieldToRupture > Mathf.Epsilon)
            {
                animationProgressNorm = Mathf.Clamp01(progressRelativeToYield / deltaGraphX_YieldToRupture);
            }
            else if (progressRelativeToYield >= 0) 
            {
                animationProgressNorm = 1f;
            }

            float targetNormalizedAnimTime = 0f;
            if (_totalClipDuration > Mathf.Epsilon && _ruptureEventTimeInClip >= 0)
            {
                float normalizedRuptureEventTimeInClip = Mathf.Clamp01(_ruptureEventTimeInClip / _totalClipDuration);
                targetNormalizedAnimTime = Mathf.Clamp01(animationProgressNorm * normalizedRuptureEventTimeInClip);
            }
            else if (_totalClipDuration > Mathf.Epsilon) 
            {
                targetNormalizedAnimTime = Mathf.Clamp01(animationProgressNorm);
            }

            foreach (var animator in sampleAnimators)
            {
                if (animator == null || !animator.isInitialized || !animator.gameObject.activeInHierarchy) continue;

                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(baseLayerIndex);
                AnimatorStateInfo nextStateInfo = animator.GetNextAnimatorStateInfo(baseLayerIndex);

                bool isInTargetState = stateInfo.shortNameHash == _conventionalTargetStateHash;
                bool isTransitioningToTarget = animator.IsInTransition(baseLayerIndex) && nextStateInfo.shortNameHash == _conventionalTargetStateHash;

                if (isInTargetState || isTransitioningToTarget)
                {
                    if (animator.speed != 0f || Mathf.Abs(stateInfo.normalizedTime - targetNormalizedAnimTime) > 0.005f || isTransitioningToTarget)
                    {
                        animator.speed = 0;
                        animator.Play(_conventionalTargetStateHash, baseLayerIndex, targetNormalizedAnimTime);
                    }
                }
            }

            if (areKeyPointsReceived && adaptedRupturePointX_graph >=0 && currentGraphRelativeDeformationPercent >= adaptedRupturePointX_graph)
            {
                Debug.Log($"<color=orange>[TC] Rupture Point Reached on Graph (X={currentGraphRelativeDeformationPercent:F3} >= RP_Adapted={adaptedRupturePointX_graph:F3}). Switching animators to speed 1.</color>");
                hasReachedRupturePointOnGraph = true;
                if(sampleBehaviorHandler != null) sampleBehaviorHandler.HandleFailure();

                SetAnimatorsSpeed(1f); 

                if (_totalClipDuration > _ruptureEventTimeInClip)
                {
                    float currentNormalizedTimeAtRuptureOnGraph = 0f;
                    if (_totalClipDuration > Mathf.Epsilon && _ruptureEventTimeInClip >=0) {
                        currentNormalizedTimeAtRuptureOnGraph = Mathf.Clamp01(_ruptureEventTimeInClip / _totalClipDuration);
                    } else if (_totalClipDuration > Mathf.Epsilon) { 
                        currentNormalizedTimeAtRuptureOnGraph = 1f; 
                    }

                    postRuptureDuration = _totalClipDuration * (1.0f - currentNormalizedTimeAtRuptureOnGraph);
                } else {
                    postRuptureDuration = 0.1f; 
                }

                if (postRuptureDuration < 0) postRuptureDuration = 0f;

                if (postRuptureDuration > Mathf.Epsilon)
                {
                    postRuptureTimer = 0f;
                    isPostRuptureTimerRunning = true;
                }
                else
                {
                    isMainAnimationLoopFinished = true; 
                    isPostRuptureTimerRunning = false;
                    CheckForTestCompletion();
                }
            }
        }
        else if (hasReachedRupturePointOnGraph && isPostRuptureTimerRunning)
        {
            postRuptureTimer += Time.deltaTime;
            SetAnimatorsSpeed(1f); 
            if (postRuptureTimer >= postRuptureDuration)
            {
                Debug.Log($"<color=lime>[TC Timer] Post-rupture animation timer finished. Setting isMainAnimationLoopFinished = true.</color>");
                isMainAnimationLoopFinished = true;
                isPostRuptureTimerRunning = false;
                SetAnimatorsSpeed(0f); 
                CheckForTestCompletion();
            }
        }
        else if (isAnimationTriggered && hasReachedRupturePointOnGraph && !isPostRuptureTimerRunning && !isMainAnimationLoopFinished)
        {
            SetAnimatorsSpeed(1f); 
        }
    }

    private void HandleNotifyRuptureCommand(BaseActionArgs baseArgs)
    {
        if (baseArgs is NotifyTestControllerAnimationEventArgs args)
        {
            NotifyVisualRuptureFromAnimation(args.SourceAnimatorObject);
        }
        else
        {
            Debug.LogWarning($"[TC] HandleNotifyRuptureCommand received incorrect args type: {baseArgs?.GetType().Name ?? "null"}. Expected NotifyTestControllerAnimationEventArgs.");
        }
    }

    private void HandleNotifyAnimationEndCommand(BaseActionArgs baseArgs) 
    {
        if (baseArgs is NotifyTestControllerAnimationEventArgs args)
        {
            NotifyMainAnimationLoopFinished(args.SourceAnimatorObject);
        }
        else
        {
             Debug.LogWarning($"[TC] HandleNotifyAnimationEndCommand received incorrect args type: {baseArgs?.GetType().Name ?? "null"}. Expected NotifyTestControllerAnimationEventArgs.");
        }
    }

    private void HandleResetTestControllerCommand(BaseActionArgs baseArgs) { ResetTestState(); }
    private void HandlePauseAnimationCommand(BaseActionArgs baseArgs)
    {
        if (!isTestInitialized) return;
        SetAnimatorsSpeed(0f);
        if (isPostRuptureTimerRunning) isPostRuptureTimerRunning = false;
    }
    private void HandleResumeAnimationCommand(BaseActionArgs baseArgs)
    {
        if (!isTestInitialized) return;
        SetAnimatorsSpeed(1f);
        if (hasReachedRupturePointOnGraph && !isMainAnimationLoopFinished && postRuptureDuration > Mathf.Epsilon)
        {
            isPostRuptureTimerRunning = true;
        }
    }
    private void HandleResetSampleVisualsCommand(BaseActionArgs baseArgs)
    {
        var sampleBehaviorHandler = SystemStateMonitor.Instance?.CurrentSampleInstance?.GetComponentInChildren<SampleBehaviorHandler>();
        if (sampleBehaviorHandler != null) sampleBehaviorHandler.ResetBehavior();
        if (sampleParentTransform != null && initialSampleScale != Vector3.zero) 
        {
            sampleParentTransform.localScale = initialSampleScale;
        }
    }
    #endregion

    private void ValidateKeyPoints()
    {
        if (!areKeyPointsReceived)
        {
            Debug.LogWarning("[TC ValidateKeyPoints] Ключевые X-точки графика не были предоставлены или не валидны. Валидация отложена/невозможна.");
            return;
        }

        bool pointsValid = adaptedYieldPointX_graph >= 0 && 
                           adaptedRupturePointX_graph >= 0 && 
                           adaptedRupturePointX_graph > adaptedYieldPointX_graph;
                           
        if (!pointsValid && totalAnimatorsCount > 0) 
        {
            Debug.LogError($"[TC ValidateKeyPoints] Rupture point on graph ({adaptedRupturePointX_graph:F3}%) <= Yield/UTS point ({adaptedYieldPointX_graph:F3}%). Animation logic might be compromised. Points were provided: {areKeyPointsReceived}");
        }
        else if (pointsValid)
        {
            Debug.Log($"[TC ValidateKeyPoints] Ключевые X-точки графика валидны: Yield/UTS_X={adaptedYieldPointX_graph:F3}%, Rupture_X={adaptedRupturePointX_graph:F3}%.");
        }
    }

    // Метод инициализации теперь не принимает аргументов и берет все из Монитора
    public void InitializeForTest()
    {
        var monitor = SystemStateMonitor.Instance;
        if (monitor == null || monitor.CurrentTestConfig == null || monitor.CurrentSampleInstance == null)
        {
            Debug.LogError("[TC] InitializeForTest: Ключевые данные в SystemStateMonitor отсутствуют (Config или SampleInstance).");
            isTestInitialized = false;
            return;
        }
        ResetTestState();

        var testConfig = monitor.CurrentTestConfig;
        var currentSampleInstance = monitor.CurrentSampleInstance;

        var sampleBehaviorHandler = currentSampleInstance.GetComponentInChildren<SampleBehaviorHandler>();
        if (sampleBehaviorHandler == null) Debug.Log($"[TC] SampleBehaviorHandler not found on '{currentSampleInstance.name}'.");

        sampleParentTransform = currentSampleInstance.transform;
        initialSampleScale = sampleParentTransform.localScale;

        sampleAnimators.Clear();
        sampleAnimators.AddRange(currentSampleInstance.GetComponentsInChildren<Animator>(true));
        totalAnimatorsCount = sampleAnimators.Count;

        _totalClipDuration = testConfig.testAnimationClipDuration;
        _ruptureEventTimeInClip = testConfig.ruptureEventTimeInClip;
        bool animationConfigValid = _totalClipDuration > 0 && _ruptureEventTimeInClip >= 0 && _ruptureEventTimeInClip <= _totalClipDuration;
        if (!animationConfigValid) Debug.LogWarning($"[TC] Invalid animation timing in config '{testConfig.name}'.");

        if (totalAnimatorsCount > 0)
        {
            if (string.IsNullOrEmpty(animationTriggerName)) Debug.LogError("[TC] Animation Trigger Name not set!");
        }
        else Debug.LogWarning($"[TC] No Animators found for sample '{currentSampleInstance.name}'. Animation features will be disabled.");

        if (monitor.CurrentSampleParameters.TryGetValue("Length", out float length))
        {
            actualLength_mm = length;
        }

        // Получаем ключевые точки из Монитора
        adaptedYieldPointX_graph = monitor.UTS_RelativeStrain_Percent;
        adaptedRupturePointX_graph = monitor.Rupture_RelativeStrain_Percent;
        areKeyPointsReceived = (adaptedYieldPointX_graph >= 0 && adaptedRupturePointX_graph >= 0);

        if (areKeyPointsReceived)
        {
            Debug.Log($"[TC InitializeForTest] Key points received from Monitor: UTS_X={adaptedYieldPointX_graph:F3}%, Rupture_X={adaptedRupturePointX_graph:F3}%");
            ValidateKeyPoints();
        }
        else
        {
            Debug.LogWarning("[TC InitializeForTest] Key points were NOT available in SystemStateMonitor.");
        }

        isTestInitialized = true;
        Debug.Log($"<color=cyan>[TestController] Initialized from Monitor. Sample: '{currentSampleInstance.name}'. Animators: {totalAnimatorsCount}.</color>");
    }
    
    public void NotifyVisualRuptureFromAnimation(GameObject sourceAnimatorObject)
    {
        if (!isTestInitialized) return;
        if (!isRupturedNotifiedByAnimationEvent) 
        {
            isRupturedNotifiedByAnimationEvent = true;
            isVisuallyRuptured = true; 

            if (!isMainAnimationLoopFinished)
            {
                SetAnimatorsSpeed(1f);
            }
        }
    }

    public void NotifyMainAnimationLoopFinished(GameObject sourceAnimatorObject)
    {
        if (!isTestInitialized) return;

        if (isAnimationTriggered)
        {
            finishedAnimatorsCount++;

            if (finishedAnimatorsCount >= totalAnimatorsCount)
            {
                Debug.Log("<color=green>[TC] All animators reported Main Animation Loop Finished.</color>");
                isMainAnimationLoopFinished = true;
                SetAnimatorsSpeed(0f);
            }
        }
    }

    public void ResetTestState()
    {
        isTestInitialized = false;
        isYieldReachedForAnimation = false;
        isAnimationTriggered = false;
        hasReachedRupturePointOnGraph = false;
        isRupturedNotifiedByAnimationEvent = false;
        isVisuallyRuptured = false;
        isMainAnimationLoopFinished = false;
        isGraphFinished = false;
        finishedAnimatorsCount = 0;
        isPostRuptureTimerRunning = false;
        postRuptureTimer = 0f;
        postRuptureDuration = 0f;
        
        areKeyPointsReceived = false; 
        adaptedYieldPointX_graph = -1f; 
        adaptedRupturePointX_graph = -1f;

        var monitor = SystemStateMonitor.Instance;
        if (monitor != null && monitor.CurrentSampleInstance != null)
        {
            var sampleBehaviorHandler = monitor.CurrentSampleInstance.GetComponentInChildren<SampleBehaviorHandler>();
            if (sampleBehaviorHandler != null)
            {
                sampleBehaviorHandler.ResetBehavior();
            }
        }

        if (sampleAnimators != null && sampleAnimators.Count > 0)
        {
             foreach (var animator in sampleAnimators)
             {
                 if (animator != null && animator.gameObject.activeInHierarchy && animator.isInitialized)
                 {
                     try
                     {
                         animator.speed = 1f; 
                         if (animator.HasState(baseLayerIndex, _conventionalTargetStateHash))  { }
                         animator.Rebind(); 
                         animator.Update(0f); 
                     }
                     catch (Exception e) { Debug.LogWarning($"[TC] Animator reset exception: {e.Message} on {animator.gameObject.name}", animator.gameObject); }
                 }
             }
        }

        if (sampleParentTransform != null && initialSampleScale != Vector3.zero)
        {
            sampleParentTransform.localScale = initialSampleScale;
        }
        initialSampleScale = Vector3.zero; 
    }

    #region Internal Logic
    private void SetAnimatorsSpeed(float speed)
    {
         if (sampleAnimators == null) return;
         foreach(var animator in sampleAnimators)
         {
             if(animator != null && animator.isInitialized && animator.speed != speed) 
             {
                 animator.speed = speed;
             }
         }
     }

    private void CheckForTestCompletion()
    {
        bool animationRelatedCompletion = false;
        if (isAnimationTriggered)
        {
            animationRelatedCompletion = isMainAnimationLoopFinished ||
                                         (hasReachedRupturePointOnGraph && !isPostRuptureTimerRunning && postRuptureDuration <= Mathf.Epsilon);
        }
        else 
        {
            animationRelatedCompletion = true; 
        }

        if (isGraphFinished && isTestInitialized)
        {
            string reason = $"Graph Finished. Animation Triggered: {isAnimationTriggered}, Main Anim Loop Finished: {isMainAnimationLoopFinished}, Reached Rupture on Graph: {hasReachedRupturePointOnGraph}, Post Rupture Timer Running: {isPostRuptureTimerRunning}";
            Debug.Log($"<color=#00FF00>[TestController] TEST SEQUENCE COMPLETED. Reason: {reason}</color>");
            isTestInitialized = false; 
            _eventManager?.RaiseEvent(EventType.TestSequenceCompleted, EventArgs.Empty);
        }
    }
    #endregion
}