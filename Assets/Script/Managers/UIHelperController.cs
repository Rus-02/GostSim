using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic; // Добавлено для ToDoManager, если будет

public class UIHelperController : MonoBehaviour
{
    #region Singleton
    private static UIHelperController _instance;
    public static UIHelperController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<UIHelperController>();
                if (_instance == null)
                {
                    GameObject singletonObject = new GameObject("UIHelperController_Runtime");
                    _instance = singletonObject.AddComponent<UIHelperController>();
                    Debug.LogWarning("[UIHelperController] Экземпляр создан автоматически.");
                }
            }
            return _instance;
        }
    }
    #endregion

    [Header("UI Элементы для Текстовых Подсказок")]
    [Tooltip("GameObject, содержащий компонент TextMeshProUGUI для подсказок")]
    [SerializeField] private GameObject hintTextContainer;
    [Tooltip("Компонент TextMeshProUGUI для отображения текста подсказки")]
    [SerializeField] private TextMeshProUGUI hintTextMeshProComponent;
    [Tooltip("Стандартная длительность отображения подсказки в секундах")]
    [SerializeField] private float defaultHintDuration = 3.0f;

    private Coroutine _timedTextCoroutine;

    // --- Awake / Start / OnDestroy (Добавлена подписка/отписка) ---
    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        // --- ДОБАВЛЕНО: Подписка на команды ---
        SubscribeToCommands();
    }

    private void OnDestroy()
    {
        // --- ДОБАВЛЕНО: Отписка и очистка ---
        UnsubscribeFromCommands();
        ClearRunningTimer();
        if (_instance == this) { _instance = null; }
    }

    void Start() // Без изменений
    {
        if (hintTextContainer == null) { Debug.LogError("[UIHelper] hintTextContainer не назначен!"); this.enabled = false; return; }
        if (hintTextMeshProComponent == null) { hintTextMeshProComponent = hintTextContainer.GetComponentInChildren<TextMeshProUGUI>(true); if (hintTextMeshProComponent == null) { Debug.LogError("[UIHelper] TextMeshProUGUI не найден!"); this.enabled = false; return; } }
        hintTextContainer.SetActive(false);
    }
    // ---

    // --- ДОБАВЛЕНО: Подписка/Отписка ---
    #region ToDoManager Command Subscription
    private void SubscribeToCommands()
    {
        if (ToDoManager.Instance == null) { Debug.LogError("[UIHelper] ToDoManager null during subscription!"); return; }
        var tm = ToDoManager.Instance;

        tm.SubscribeToAction(ActionType.ShowHintText, HandleShowHintTextCommand);
        tm.SubscribeToAction(ActionType.ClearHints, HandleClearHintsCommand);

        Debug.Log("[UIHelperController] Subscribed to ToDoManager commands.");
    }

    private void UnsubscribeFromCommands()
    {
        var tm = ToDoManager.Instance;
        if (tm != null)
        {
            tm.UnsubscribeFromAction(ActionType.ShowHintText, HandleShowHintTextCommand);
            tm.UnsubscribeFromAction(ActionType.ClearHints, HandleClearHintsCommand);
        }
    }
    #endregion

    // --- ДОБАВЛЕНО: Обработчики команд ---
    #region ToDoManager Command Handlers
    private void HandleShowHintTextCommand(BaseActionArgs baseArgs)
    {
        if (baseArgs is ShowHintArgs args)
        {
            // Вызываем существующий публичный метод
            ShowHintText(args.HintText, args.Duration);
        }
        else
        {
             Debug.LogError($"[UIHelper] HandleShowHintTextCommand received incorrect args type: {baseArgs?.GetType().Name ?? "null"}. Expected ShowHintArgs.");
        }
    }

    private void HandleClearHintsCommand(BaseActionArgs baseArgs) // Параметры не нужны
    {
        // Вызываем существующий публичный метод
        ClearHints();
    }
    #endregion

    // --- Существующие публичные методы (остаются без изменений) ---
    #region Public Methods (Called by Handlers)
    public void ShowHintText(string text, float duration = -1f)
    {
        if (hintTextContainer == null || hintTextMeshProComponent == null) { Debug.LogError("[UIHelper] ShowHintText: UI не инициализирован."); return; }
        ClearRunningTimer();
        float actualDuration = (duration <= 0) ? defaultHintDuration : duration;
        if (actualDuration <= 0) { Debug.LogWarning("[UIHelper] Длительность подсказки <= 0."); actualDuration = 0.1f; }
        hintTextMeshProComponent.text = text;
        hintTextContainer.SetActive(true);
        _timedTextCoroutine = StartCoroutine(HideTextAfterDelayCoroutine(actualDuration));
    }

    public void ClearHints()
    {
        ClearRunningTimer();
        HideContainer();
    }
    #endregion

    // --- Приватные методы (без изменений) ---
    #region Private Helper Methods
    private void ClearRunningTimer() { if (_timedTextCoroutine != null) { StopCoroutine(_timedTextCoroutine); _timedTextCoroutine = null; } }
    private void HideContainer() { if (hintTextContainer != null && hintTextContainer.activeSelf) { hintTextContainer.SetActive(false); if(hintTextMeshProComponent != null) hintTextMeshProComponent.text = ""; } }
    private IEnumerator HideTextAfterDelayCoroutine(float delay) { yield return new WaitForSeconds(delay); HideContainer(); _timedTextCoroutine = null; }
    #endregion
}