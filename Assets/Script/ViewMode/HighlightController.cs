using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// Управляет визуальной подсветкой объектов в сцене.
/// Получает команды через ToDoManager и активирует/деактивирует
/// дочерние объекты-рендеры для создания эффекта подсветки.
/// </summary>
public class HighlightController : MonoBehaviour
{
    #region Singleton
    private static HighlightController _instance;
    public static HighlightController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<HighlightController>();
                if (_instance == null)
                {
                    GameObject singletonObject = new GameObject("HighlightController");
                    _instance = singletonObject.AddComponent<HighlightController>();
                }
            }
            return _instance;
        }
    }
    #endregion

    [Header("Настройки")]
    [Tooltip("Имя дочернего объекта, который используется для визуализации подсветки (XRay).")]
    public string outlineObjectName = "XRay";

    [Header("Отладка")]
    [Tooltip("Включает подробный вывод в консоль каждого шага поиска и состояния подсветки.")]
    [SerializeField] private bool enableVerboseLogging = false;

    /// <summary>
    /// Список для отслеживания всех активных рендереров подсветки.
    /// Используется для быстрой очистки.
    /// </summary>
    private List<Renderer> highlightedRenderers = new List<Renderer>();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void Start()
    {
        SubscribeToActions();
    }

    private void OnDestroy()
    {
        UnsubscribeFromActions();
    }

    /// <summary>
    /// Подписывается на команду UpdateHighlight в ToDoManager.
    /// </summary>
    private void SubscribeToActions()
    {
        if (ToDoManager.Instance == null)
        {
            Debug.LogError("[HighlightController] ToDoManager не найден! Не удалось подписаться на действия.", this);
            return;
        }
        ToDoManager.Instance.SubscribeToAction(ActionType.UpdateHighlight, HandleUpdateHighlightAction);
    }

    /// <summary>
    /// Отписывается от команды UpdateHighlight в ToDoManager.
    /// </summary>
    private void UnsubscribeFromActions()
    {
        if (ToDoManager.Instance != null)
        {
            ToDoManager.Instance.UnsubscribeFromAction(ActionType.UpdateHighlight, HandleUpdateHighlightAction);
        }
    }

    /// <summary>
    /// Обработчик команды ActionType.UpdateHighlight.
    /// </summary>
    private void HandleUpdateHighlightAction(BaseActionArgs baseArgs)
    {
        if (!(baseArgs is UpdateHighlightArgs args))
        {
            Debug.LogWarning("[HighlightController] Получены некорректные аргументы для UpdateHighlightAction.", this);
            return;
        }

        switch (args.Type)
        {
            case HighlightType.SingleObject:
                HighlightSingleObject(args.TargetObject);
                break;
            case HighlightType.FixtureType:
                HighlightFixturesByType(args.FixtureTypeName);
                break;
            case HighlightType.ClearAll:
                ClearAllHighlights();
                break;
            default:
                Debug.LogWarning($"[HighlightController] Неизвестный HighlightType: {args.Type}", this);
                break;
        }
    }

    /// <summary>
    /// Подсвечивает один указанный GameObject.
    /// </summary>
    /// <param name="objectToHighlight">Объект для подсветки.</param>
    public void HighlightSingleObject(GameObject objectToHighlight)
    {
        ClearAllHighlights();

        if (objectToHighlight == null) return;

        Transform outlineTransform = objectToHighlight.transform.Find(outlineObjectName);
        if (outlineTransform != null)
        {
            Renderer rend = outlineTransform.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.enabled = true;
                highlightedRenderers.Add(rend);
            }
            else
            {
                Debug.LogWarning($"'{outlineObjectName}' у '{objectToHighlight.name}' не содержит Renderer.", objectToHighlight);
            }
        }
        else
        {
            Debug.LogWarning($"Не найден дочерний объект '{outlineObjectName}' у '{objectToHighlight.name}' для подсветки.", objectToHighlight);
        }
    }

    /// <summary>
    /// Подсвечивает все объекты оснастки с заданным типом.
    /// </summary>
    /// <param name="fixtureTypeName">Имя типа оснастки из InteractableInfo.FixtureTypeDisplayName.</param>
    public void HighlightFixturesByType(string fixtureTypeName)
    {
        ClearAllHighlights();

        if (string.IsNullOrEmpty(fixtureTypeName))
        {
            Debug.LogWarning("[HighlightController] HighlightFixturesByType вызван с пустым именем типа.");
            return;
        }

        List<string> foundMatchingNames = new List<string>();
        
        Debug.Log($"[HighlightController] --- STARTING SEARCH FOR TYPE: {fixtureTypeName} ---");
        InteractableInfo[] allInteractables = FindObjectsByType<InteractableInfo>(FindObjectsSortMode.None);
        Debug.Log($"[HighlightController] Total InteractableInfo found in scene: {allInteractables.Length}");

        foreach (InteractableInfo info in allInteractables)
        {
            if (info == null) continue;
            
            // Выводим детальную информацию о каждом проверяемом объекте, только если включена отладка.
            if (enableVerboseLogging)
            {
                Debug.Log($"[HighlightController] Checking OBJ: {info.gameObject.name}, " +
                          $"IS_FIXTURE: {info.isFixture}, " +
                          $"TYPE_NAME: '{info.FixtureTypeDisplayName}', " +
                          $"IS_ACTIVE_GO: {info.gameObject.activeInHierarchy}");
            }

            // Основное условие поиска: объект является оснасткой, активен, и его тип совпадает.
            if (info.isFixture && info.FixtureTypeDisplayName == fixtureTypeName && info.gameObject.activeInHierarchy)
            {
                foundMatchingNames.Add(info.gameObject.name);
                Transform outlineTransform = info.transform.Find(outlineObjectName);
                if (outlineTransform != null)
                {
                    Renderer rend = outlineTransform.GetComponent<Renderer>();
                    if (rend != null)
                    {
                        rend.enabled = true;
                        highlightedRenderers.Add(rend);
                    }
                }
            }
        }

        // Выводим итоговый результат поиска.
        string resultMessage = foundMatchingNames.Count > 0 ? string.Join(", ", foundMatchingNames) : "None";
        Debug.Log($"[HighlightController] Matched objects for type '{fixtureTypeName}': {resultMessage}");
    }

    /// <summary>
    /// Снимает всю текущую подсветку с объектов.
    /// </summary>
    public void ClearAllHighlights()
    {
        foreach (var rend in highlightedRenderers)
        {
            if (rend != null)
            {
                rend.enabled = false;
            }
        }
        highlightedRenderers.Clear();
    }
}