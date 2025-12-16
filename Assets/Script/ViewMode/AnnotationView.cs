using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class AnnotationView : MonoBehaviour
{
    [Header("Ссылки на элементы префаба")]
    [Tooltip("RectTransform самого текстового блока (с фоном и текстом).")]
    public RectTransform TextBlockRect;

    [Tooltip("Компонент TextMeshProUGUI для отображения текста подсказки.")]
    public TextMeshProUGUI HintTextMeshPro;

    [Tooltip("RectTransform или компонент для визуализации линии/стрелки.")]
    public RectTransform LineRect;

    private RectTransform _targetRectTransform;
    private RectTransform _overlayCanvasRectTransform;

    /// Настраивает аннотацию, задавая текст и целевой элемент.
    public void Setup(string hintText, RectTransform targetElement, RectTransform overlayCanvas)
    {
        if (HintTextMeshPro != null)
        {
            HintTextMeshPro.text = hintText;
            HintTextMeshPro.ForceMeshUpdate();
        }

        _targetRectTransform = targetElement;
        _overlayCanvasRectTransform = overlayCanvas;
    }

    /// Обновляет позицию линии/стрелки в каждом кадре чтобы она всегда указывала на целевой UI элемент
    public void UpdateLinePosition()
    {        
        if (_targetRectTransform == null || LineRect == null || _overlayCanvasRectTransform == null || TextBlockRect == null) // Проверяем наличие всех необходимых ссылок
        {
             // Если хотя бы одной ссылки нет, скрываем линию (если она была видна) и выходим.
             if (LineRect != null && LineRect.gameObject.activeSelf)
             {
                 LineRect.gameObject.SetActive(false);
             }
             return;
        }

        // 1. Получаем мировую позицию центра текстового блока.
        Vector3 textBlockWorldCenter = TextBlockRect.TransformPoint(TextBlockRect.rect.center);

        // 2. Получаем мировую позицию центра целевого UI элемента.
        Vector3 targetWorldCenter = _targetRectTransform.TransformPoint(_targetRectTransform.rect.center);

        // Рассчитываем мировую позицию точки, где должен находиться пивот линии (середина между центрами)
        Vector3 midpointWorldPosition = (textBlockWorldCenter + targetWorldCenter) / 2f;

        // Получаем RectTransform родителя линии. LineRect.anchoredPosition задается относительно якорей этого родителя.
        RectTransform lineParentRectTransform = LineRect.parent.GetComponent<RectTransform>();
        if (lineParentRectTransform == null)
        {
             // Оставляем критическую ошибку, так как это проблема с настройкой префаба
             Debug.LogError($"[AnnotationView] {gameObject.name}: Родительский RectTransform для LineRect не найден!", this);
             if (LineRect.gameObject.activeSelf) LineRect.gameObject.SetActive(false);
             return;
        }

        // Конвертируем желаемую мировую позицию пивота линии в локальные координаты
        Vector2 desiredLineAnchoredPosition;
        bool success = UnityEngine.RectTransformUtility.ScreenPointToLocalPointInRectangle(
            lineParentRectTransform,    // Целевой RectTransform (родитель линии)
            UnityEngine.RectTransformUtility.WorldToScreenPoint(null, midpointWorldPosition), // Экранная позиция желаемого пивота линии
            null,                       // Камера: null для Screen Space - Overlay канвасов
            out desiredLineAnchoredPosition // Выходная локальная позиция относительно якорей родителя линии
        );

        if (!success) // Если пересчет не удался, скрываем линию.
        {
             if (LineRect.gameObject.activeSelf)
             {
                 LineRect.gameObject.SetActive(false);
             }
             // Оставляем предупреждение, так как это может указывать на проблему с UI или размещением
             Debug.LogWarning($"[AnnotationView] {gameObject.name}: Не удалось конвертировать мировую позицию середины линии в локальные координаты родителя линии.", this);
             return;
        }
        else
        {
            if (!LineRect.gameObject.activeSelf) LineRect.gameObject.SetActive(true); // Показываем линию
        }

        // 3. Вычисляем вектор и расстояние между мировыми центрами текстового блока и цели.
        Vector3 directionWorld = targetWorldCenter - textBlockWorldCenter;
        float distanceWorld = directionWorld.magnitude; // Расстояние между центрами в мировых единицах (для Overlay Canvas соответствует Screen/Canvas единицам)

        float angle = Mathf.Atan2(directionWorld.y, directionWorld.x) * Mathf.Rad2Deg; // Вычисляем угол в градусах по вектору направления (используем 2D проекцию)

        // 4. Позиционируем, масштабируем и поворачиваем линию.
        LineRect.pivot = new Vector2(0.5f, 0.5f);

        LineRect.anchoredPosition = desiredLineAnchoredPosition;       // Устанавливаем позицию линии

        // Получаем scaleFactor канваса, на котором находится LineRect (обычно это overlayCanvas)
        float currentCanvasScaleFactor = 1f;
        Canvas parentCanvas = LineRect.GetComponentInParent<Canvas>();
        if (parentCanvas != null && parentCanvas.scaleFactor > 0)
        {
            currentCanvasScaleFactor = parentCanvas.scaleFactor;
        }

        float logicalDistance = distanceWorld / currentCanvasScaleFactor;

        float targetTotalPhysicalOffset = 100f; // Целевой общий отступ в физических пикселях
        
        float offsetToSubtractInLogicalUnits = targetTotalPhysicalOffset / currentCanvasScaleFactor;         // Переводим этот физический отступ в логические единицы для ТЕКУЩЕГО разрешения        
        float finalAdjustedDistance = Mathf.Max(0f, logicalDistance - offsetToSubtractInLogicalUnits);        // Вычитаем отступ из полной логической длины        
        LineRect.sizeDelta = new Vector2(finalAdjustedDistance, LineRect.sizeDelta.y);         // Устанавливаем длину линии равной скорректированному расстоянию. Высота остается неизменной.
        LineRect.localEulerAngles = new Vector3(0, 0, angle);        // Устанавливаем поворот.
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        _targetRectTransform = null;
        _overlayCanvasRectTransform = null;
        if (HintTextMeshPro != null) HintTextMeshPro.text = "";         // Сбрасываем текст, чтобы не держать старые данные в пуле

    }
}