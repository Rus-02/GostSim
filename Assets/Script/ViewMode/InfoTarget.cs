using UnityEngine;
using UnityEngine.SceneManagement;
using System;

// Этот компонент помечает UI элемент, который может быть аннотирован.
[RequireComponent(typeof(RectTransform))]
public class InfoTarget : MonoBehaviour
{
    [Header("Настройки аннотации")]

    [Tooltip("Текст, который будет отображаться в подсказке.")]
    [TextArea(3, 5)]
    public string HintText = "Подсказка";

    [Tooltip("Приоритет отображения аннотации (меньше число = выше приоритет). Аннотации с низким приоритетом могут быть скрыты, если места не хватает.")]
    [Range(0, 10)]
        public int Priority = 1;

    [Header("Область размещения")]

    [Tooltip("Дочерний RectTransform (пустой GameObject), определяющий область на Canvas, где МОЖНО разместить текстовый блок аннотации для этого элемента. Если не задан, аннотация не будет показана.")]
    public RectTransform AllowedPlacementArea;

    [Header("Внутренние ссылки (для менеджера)")]
    [HideInInspector]
    public RectTransform TargetRectTransform;

    private void Awake()
    {
        TargetRectTransform = GetComponent<RectTransform>();
        if (AllowedPlacementArea == null)
        {
             Debug.LogWarning($"[InfoTarget] На объекте '{gameObject.name}' не назначена AllowedPlacementArea. Этот элемент не будет аннотирован.", this);
        }
         else if (AllowedPlacementArea.transform.parent != transform) { }
    }

    /// Регистрирует этот InfoTarget в InfoOverlayController при активации объекта.
    private void OnEnable()
    {
        if (InfoOverlayController.Instance != null)
        {
            InfoOverlayController.Instance.RegisterTarget(this);
        }
    }

    /// Отменяет регистрацию этого InfoTarget в InfoOverlayController при деактивации или уничтожении объекта.
    private void OnDisable()
    {
        if (InfoOverlayController.Instance != null)
        {
            InfoOverlayController.Instance.UnregisterTarget(this);
        }
    }
}