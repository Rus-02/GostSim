using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using System;
using System.Linq; // Добавлен для использования LINQ методов типа Distinct() и Where()

public class UIController : MonoBehaviour
{
    #region Singleton
    private static UIController _instance;
    public static UIController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<UIController>();
                if (_instance == null) { Debug.LogError("UIController Instance is null and couldn't be found."); }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(this.gameObject); return; }
        _instance = this;
        SubscribeToCommands();
    }

    private void OnDestroy()
    {
        UnsubscribeFromCommands();
        if (_instance == this) _instance = null;
    }
    #endregion

    [Header("Ссылки на ProgramPanelController")]
    public ProgramPanelController programPanelController;

    private Dictionary<string, Button> _buttonsById = new Dictionary<string, Button>();
    private Dictionary<string, GameObject> _tabContentsByName = new Dictionary<string, GameObject>();

    void Start()
    {
        if (this == null || _instance != this) return;
        InitializeUIElements();
    }

    private void InitializeUIElements()
    {
        _tabContentsByName.Clear(); // Очищаем словарь для контента вкладок
        _buttonsById.Clear();       // Очищаем словарь для кнопок

        // 1. Проверяем наличие ProgramPanelController
        if (programPanelController == null)
        {
            Debug.LogError("[UIController] ProgramPanelController не назначен. Невозможно инициализировать UI элементы.", this);
            return; // Не можем продолжать без ссылки на PPC
        }

        // 2. Получаем ссылку на главный корневой контейнер UI из PPC
        GameObject rootContainerForSearch = programPanelController.mainButtonContainer;

        // 3. Если главный контейнер не назначен или null, выходим
        if (rootContainerForSearch == null)
        {
            Debug.LogError("[UIController] mainButtonContainer не назначен в ProgramPanelController или сам null. Невозможно найти кнопки.", this);
            return; // Не можем найти кнопки без основного корня
        }

        // 4. Находим ВСЕ компоненты ButtonScript в пределах главного корневого контейнера и всех его потомков (рекурсивно)
        // Параметр 'true' означает поиск даже на неактивных объектах
        ButtonScript[] allButtonScripts = rootContainerForSearch.GetComponentsInChildren<ButtonScript>(true);

        // 5. Наполняем словарь _buttonsById найденными кнопками
        foreach (ButtonScript buttonScript in allButtonScripts)
        {
            string id = buttonScript.GetButtonId();
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning($"[UIController] Кнопка '{buttonScript.gameObject.name}' не имеет buttonId. Управление ею будет невозможно.", buttonScript);
                continue;
            }

            if (_buttonsById.ContainsKey(id))
            {
                Debug.LogError($"[UIController] Дублирующийся buttonId '{id}' обнаружен на кнопке '{buttonScript.gameObject.name}'. ID кнопок должны быть уникальными!", buttonScript);
            }
            else
            {
                Button btnComponent = buttonScript.GetComponent<Button>();
                if (btnComponent != null)
                {
                    _buttonsById.Add(id, btnComponent);
                }
                else
                {
                     Debug.LogWarning($"[UIController] На объекте '{buttonScript.gameObject.name}' с ButtonScript не найден компонент Button.", buttonScript);
                }
            }
        }
        Debug.Log($"[UIController] Initialized. Found {_buttonsById.Count} buttons with unique IDs.");

        // 6. Наполняем словарь _tabContentsByName, используя явные ссылки из ProgramPanelController
        // Этот словарь нужен для команды ActivateUITabCommand, чтобы напрямую активировать нужный контент вкладки
        if (programPanelController.controlTabContent != null) _tabContentsByName.Add("ControlTab", programPanelController.controlTabContent);
        if (programPanelController.testTabContent != null) _tabContentsByName.Add("TestTab", programPanelController.testTabContent);
        if (programPanelController.resultsTabContent != null) _tabContentsByName.Add("ResultsTab", programPanelController.resultsTabContent);
    }

    #region ToDoManager Command Subscription
    private void SubscribeToCommands()
    {
        if (ToDoManager.Instance == null) { Debug.LogError("[UIController] ToDoManager null!"); return; }
        var tm = ToDoManager.Instance;
        tm.SubscribeToAction(ActionType.UpdateUIState, HandleUpdateUIStateCommand);
        tm.SubscribeToAction(ActionType.UpdateUIButtonVisuals, HandleUpdateVisualsCommand);
        Debug.Log("[UIController] Subscribed to ToDoManager commands.");
    }

    private void UnsubscribeFromCommands()
    {
        var tm = ToDoManager.Instance;
        if (tm != null)
        {
            tm.UnsubscribeFromAction(ActionType.UpdateUIState, HandleUpdateUIStateCommand);
            tm.UnsubscribeFromAction(ActionType.UpdateUIButtonVisuals, HandleUpdateVisualsCommand);
        }
    }

#endregion
#region ToDoManager Command Handlers

    private void HandleUpdateUIStateCommand(BaseActionArgs baseArgs)
    {
        if (baseArgs is UpdateUIStateArgs args)
        {
            string targetButtonId = args.ButtonId;
            bool shouldEnable = args.Enable;

            if (shouldEnable) { EnableButton(targetButtonId); }
            else { DisableButton(targetButtonId); }
        }
        else { Debug.LogError($"[UIController] HandleUpdateUIStateCommand incorrect args: {baseArgs?.GetType().Name}. Expected UpdateUIStateArgs."); }
    }

    /// Обрабатывает команду на смену визуального состояния кнопки (спрайта) ИЛИ текста.
private void HandleUpdateVisualsCommand(BaseActionArgs baseArgs)
{
    // 1. Преобразуем аргументы (без изменений)
    if (!(baseArgs is UpdateUIButtonVisualsArgs args)) 
    {
        Debug.LogError($"[UIController] HandleUpdateVisualsCommand received incorrect args type: {baseArgs?.GetType().Name}. Expected UpdateUIButtonVisualsArgs."); 
        return;
    }

    if (string.IsNullOrEmpty(args.ButtonId)) 
    {
        Debug.LogWarning("[UIController] HandleUpdateVisualsCommand received empty ButtonId."); 
        return;
    }

    // 2. Находим кнопку (без изменений)
    if (_buttonsById.TryGetValue(args.ButtonId, out Button button))
    {
        if (button != null)
        {
            ButtonScript bs = button.GetComponent<ButtonScript>();
            if (bs != null)
            {
                // А. Проверяем, нужно ли менять текст
                if (!string.IsNullOrEmpty(args.ButtonText)) bs.SetButtonText(args.ButtonText);

                // Б. Проверяем, нужно ли менять визуал (спрайт)
                if (args.VisualState.HasValue) bs.SetVisualState(args.VisualState.Value); 

                // В. Проверяем, нужно ли менять событие
                if (args.NewEventType.HasValue) bs.SetEventType(args.NewEventType.Value);
            }
            else  { Debug.LogWarning($"[UIController] ButtonScript component not found on button with ID: '{args.ButtonId}'. Cannot update.", button); }
        }
    }
    else  { Debug.LogWarning($"[UIController] Button for HandleUpdateVisualsCommand not found by ID: '{args.ButtonId}'."); }
}

#endregion
#region Public UI Control Methods

    public void EnableButton(string buttonId)
    {
        if (string.IsNullOrEmpty(buttonId)) return;
        if (_buttonsById.TryGetValue(buttonId, out Button button)) { if (button != null) button.interactable = true; }
        else { Debug.LogWarning($"[UIController] Кнопка EnableButton не найдена по ID: '{buttonId}'."); }
    }

    public void DisableButton(string buttonId)
    {
        if (string.IsNullOrEmpty(buttonId)) return;
        if (_buttonsById.TryGetValue(buttonId, out Button button)) { if (button != null) button.interactable = false; }
        else { Debug.LogWarning($"[UIController] Кнопка DisableButton не найдена по ID: '{buttonId}'."); }
    }

    public void SetButtonImage(string buttonId, Sprite image)
    {
        if (string.IsNullOrEmpty(buttonId)) return;
        if (_buttonsById.TryGetValue(buttonId, out Button button)) { if (button != null) { ButtonScript bs = button.GetComponent<ButtonScript>(); if (bs != null) bs.SetButtonImage(image); else Debug.LogWarning($"[UIController] ButtonScript не найден на кнопке ID: '{buttonId}'."); } }
        else { Debug.LogWarning($"[UIController] Кнопка SetButtonImage не найдена по ID: '{buttonId}'."); }
    }

     public void SetButtonText(string buttonId, string text)
    {
        if (string.IsNullOrEmpty(buttonId)) return;
        if (_buttonsById.TryGetValue(buttonId, out Button button)) { if (button != null) { ButtonScript bs = button.GetComponent<ButtonScript>(); if (bs != null) bs.SetButtonText(text); else Debug.LogWarning($"[UIController] ButtonScript не найден на кнопке ID: '{buttonId}'."); } }
        else { Debug.LogWarning($"[UIController] Кнопка SetButtonText не найдена по ID: '{buttonId}'."); }
    }

    public void ShowButton(string buttonId)
    {
        if (string.IsNullOrEmpty(buttonId)) return;
        if (_buttonsById.TryGetValue(buttonId, out Button button)) { if (button != null) button.gameObject.SetActive(true); }
        else { Debug.LogWarning($"[UIController] Кнопка ShowButton не найдена по ID: '{buttonId}'."); }
    }

    public void HideButton(string buttonId)
    {
        if (string.IsNullOrEmpty(buttonId)) return;
        if (_buttonsById.TryGetValue(buttonId, out Button button)) { if (button != null) button.gameObject.SetActive(false); }
        else { Debug.LogWarning($"[UIController] Кнопка HideButton не найдена по ID: '{buttonId}'."); }
    }

    public void SetButtonsInactiveExcept(string buttonIdToActivate)
    {
        if (string.IsNullOrEmpty(buttonIdToActivate)) return;
        foreach (var buttonPair in _buttonsById) { if (buttonPair.Value != null) { buttonPair.Value.interactable = (buttonPair.Key == buttonIdToActivate); } }
    }

    private Coroutine _pulseCoroutine;
    private Button _pulsingButton;

    public void PulseButton(string buttonId)
    {
        if (string.IsNullOrEmpty(buttonId)) return;
        if (_buttonsById.TryGetValue(buttonId, out Button button)) {
            if (button == null) return;
            if (_pulseCoroutine != null && _pulsingButton == button) { StopCoroutine(_pulseCoroutine); ResetButtonScale(_pulsingButton); }
            _pulsingButton = button; _pulseCoroutine = StartCoroutine(PulseButtonCoroutine(button));
        } else { Debug.LogWarning($"[UIController] Кнопка PulseButton не найдена по ID: '{buttonId}'."); }
    }

    public void StopPulseButton(string buttonId)
    {
        if (string.IsNullOrEmpty(buttonId)) return;
        if (_buttonsById.TryGetValue(buttonId, out Button button) && _pulsingButton == button && _pulseCoroutine != null) {
            StopCoroutine(_pulseCoroutine); ResetButtonScale(_pulsingButton); _pulseCoroutine = null; _pulsingButton = null;
        }
    }

    private IEnumerator PulseButtonCoroutine(Button button) { Vector3 originalScale = button.transform.localScale; while (true) { float scaleFactor = Mathf.PingPong(Time.time * 0.5f, 0.1f) + 1f; button.transform.localScale = originalScale * scaleFactor; yield return null; } }
    private void ResetButtonScale(Button button) { if(button != null) button.transform.localScale = Vector3.one; }

    public void SetButtonsInteractable(bool interactable)
    {
        foreach (var buttonPair in _buttonsById) { if (buttonPair.Value != null) buttonPair.Value.interactable = interactable; }
    }
    #endregion
}