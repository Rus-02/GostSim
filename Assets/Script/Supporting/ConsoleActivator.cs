using UnityEngine;
using UnityEngine.UI;
using IngameDebugConsole;
using System.Collections;

public class ConsoleActivator : MonoBehaviour
{
    [SerializeField] private DebugLogManager consoleManager;
    public Button exitButton;

    private int _pressCount = 0;
    private float _lastPressTime = 0f;
    private const float ResetTime = 2f;
    private const int ClicksToOpen = 8;

    void Start()
    {
        if (consoleManager == null || exitButton == null)
        {
            Debug.LogError("Не все поля назначены в ConsoleActivator!");
            this.enabled = false;
            return;
        }

        exitButton.onClick.AddListener(HandleExitPress);
        
        // Теперь этот вызов сработает корректно, т.к. popup отключен
        consoleManager.HideLogWindow();
    }

    private void HandleExitPress()
    {
        if (Time.time - _lastPressTime > ResetTime)
        {
            _pressCount = 0;
        }

        _pressCount++;
        _lastPressTime = Time.time;

        if (_pressCount >= ClicksToOpen)
        {
            if (consoleManager.IsLogWindowVisible)
            {
                consoleManager.HideLogWindow();
            }
            else
            {
                consoleManager.ShowLogWindow();
            }
            _pressCount = 0;
        }
    }

    void OnDestroy()
    {
        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(HandleExitPress);
        }
    }
}