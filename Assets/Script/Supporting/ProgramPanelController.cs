using UnityEngine;
using UnityEngine.UI;
using System;

public class ProgramPanelController : MonoBehaviour
{
    #region Синглтон
    private static ProgramPanelController _instance;
    public static ProgramPanelController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<ProgramPanelController>();
                if (_instance == null)
                {
                    Debug.LogError("[ProgramPanelController] Instance is null and no existing instance was found in the scene! Ensure it exists and is active.");
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        _instance = this;
        SubscribeToCommands();
    }

    private void OnDestroy()
    {
        UnsubscribeFromCommands();
        if (_instance == this) _instance = null;
    }
    #endregion

    [Header("Общий контейнер кнопок")]
    public GameObject mainButtonContainer;
    public GameObject ScreenContainer;

    ///НОВОЕ///
    // Добавляем ссылку на контейнер пульта
    [Header("Контейнер пульта управления")]
    public GameObject pultContainer;
    ///КОНЕЦ НОВОЕ///

    [Header("Кнопки вкладок")]
    public Button controlTabButton;
    public Button testTabButton;
    public Button resultsTabButton;

    [Header("Контейнеры кнопок вкладок")]
    public GameObject controlTabContent;
    public GameObject testTabContent;
    public GameObject resultsTabContent;

    private void Start()
    {
        if (this == null || _instance != this) return;

        ActivateDefaultTab();
    }

    #region ToDoManager Command Subscription
    private void SubscribeToCommands()
    {
        var tm = ToDoManager.Instance; if (tm != null)
        {
            tm.SubscribeToAction(ActionType.SetUIContainerActive, HandleSetContainerActiveCommand);
            tm.SubscribeToAction(ActionType.ActivateUITab, HandleActivateUITabCommand);
            Debug.Log("[ProgramPanelController] Subscribed to ToDoManager commands.");
        }
    }

    private void UnsubscribeFromCommands()
    {
        var tm = ToDoManager.Instance; if (tm != null)
        {
            tm.UnsubscribeFromAction(ActionType.SetUIContainerActive, HandleSetContainerActiveCommand);
            tm.UnsubscribeFromAction(ActionType.ActivateUITab, HandleActivateUITabCommand);
        }
    }
    #endregion

    #region ToDoManager Command Handlers
    private void HandleSetContainerActiveCommand(BaseActionArgs baseArgs)
    {
        if (baseArgs is SetUIContainerActiveArgs args)
        {
            SetContainerActiveState(args.ContainerId, args.Activate);
        }
        else
        {
             Debug.LogError($"[PPC] HandleSetContainerActiveCommand incorrect args: {baseArgs?.GetType().Name ?? "null"}. Expected SetUIContainerActiveArgs.");
        }
    }

    private void HandleActivateUITabCommand(BaseActionArgs baseArgs)
    {
        if (baseArgs is ActivateUITabArgs args)
        {
            GameObject tabToActivate = null;
            switch (args.TabId)
            {
                case "ControlTab":
                    tabToActivate = controlTabContent;
                    break;
                case "TestTab":
                    tabToActivate = testTabContent;
                    break;
                case "ResultsTab":
                    tabToActivate = resultsTabContent;
                    break;
                default:
                    Debug.LogWarning($"[PPC] Received ActivateUITab command with unknown TabId: '{args.TabId}'");
                    break;
            }

            if (tabToActivate != null)
            {
                ActivateTabContent(tabToActivate);
            }
        }
        else
        {
            Debug.LogError($"[PPC] HandleActivateUITabCommand incorrect args: {baseArgs?.GetType().Name ?? "null"}. Expected ActivateUITabArgs.");
        }
    }

    #endregion

    public void SetContainerActiveState(string containerId, bool activate)
    {
        switch (containerId)
        {
            case "MainButtonContainer":
                if (mainButtonContainer != null) { mainButtonContainer.SetActive(activate); }
                else Debug.LogError("[PPC] mainButtonContainer null!");
                break;
            case "ScreenContainer":
                if (ScreenContainer != null) { ScreenContainer.SetActive(activate); }
                else Debug.LogError("[PPC] ScreenContainer null!");
                break;
            ///НОВОЕ///
            // Добавляем кейс для управления активностью контейнера пульта
            case "Pult":
                if (pultContainer != null) { pultContainer.SetActive(activate); }
                else Debug.LogError("[PPC] pultContainer null!");
                break;
            ///КОНЕЦ НОВОЕ///
            default:
                Debug.LogWarning($"[PPC] Unknown containerId: '{containerId}'.");
                break;
        }
    }

    public void ActivateTabContent(GameObject tabContentToActivate)
    {
        if (controlTabContent != null) controlTabContent.SetActive(controlTabContent == tabContentToActivate);
        if (testTabContent != null) testTabContent.SetActive(testTabContent == tabContentToActivate);
        if (resultsTabContent != null) resultsTabContent.SetActive(resultsTabContent == tabContentToActivate);
    }

    private void ActivateDefaultTab()
    {
        if (controlTabContent != null) { ActivateTabContent(controlTabContent); return; }
        if (testTabContent != null) { ActivateTabContent(testTabContent); return; }
        if (resultsTabContent != null) { ActivateTabContent(resultsTabContent); return; }
        Debug.LogError("[ProgramPanelController] Ни один контейнер вкладки не назначен! Невозможно активировать вкладку по умолчанию.");
    }
}