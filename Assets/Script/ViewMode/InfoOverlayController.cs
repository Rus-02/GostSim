using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

// Этот контроллер управляет режимом подсказок к объектам UI
public class InfoOverlayController : MonoBehaviour
{
    #region Singleton
    private static InfoOverlayController _instance;
    private static bool _applicationIsQuitting = false;

    public static InfoOverlayController Instance
    {
        get
        {
            if (_applicationIsQuitting) { return null; }

            if (_instance == null)
            {
                _instance = FindFirstObjectByType<InfoOverlayController>();
                if (_instance == null)
                {
                    GameObject singletonObject = new GameObject("InfoOverlayController");
                    _instance = singletonObject.AddComponent<InfoOverlayController>();
                }
                if (_instance != null && _instance._registeredTargets == null)
                {
                    _instance._registeredTargets = new List<InfoTarget>();
                }
            }
            return _instance;
        }
    }
    #endregion

    [Header("UI References")]
    [Tooltip("Canvas, который будет отображаться поверх всего UI для инфографики.")]
    [SerializeField] private Canvas overlayCanvas;

    [Tooltip("CanvasGroup или Image на Overlay Canvas, который используется для затенения и блокировки кликов.")]
    [SerializeField] private CanvasGroup dimmingAndBlockerGroup;

    [Header("Annotation Settings")]
    [Tooltip("Префаб 2D аннотации (с компонентом AnnotationView).")]
    [SerializeField] private GameObject annotationPrefab;

    [Tooltip("Начальное количество объектов в пуле аннотаций.")]
    [SerializeField] private int poolInitialSize = 10;

    [Tooltip("Время ожидания между кадрами при поиске места для аннотаций в корутине (для распределения нагрузки).")]
    [SerializeField] private float placementCalculationDelay = 0.01f;

    [Header("Dimming Settings")]
    [Tooltip("Альфа (прозрачность) затенения фона.")]
    [SerializeField] private float dimmingAlpha = 0.6f;

    [Header("Placement Settings")]
    [Tooltip("Список приоритетных позиций для попытки размещения текстового блока аннотации в пределах AllowedPlacementArea. Координаты относительны центру AllowedPlacementArea в диапазоне [-0.5, 0.5] для каждой оси. Начинается с центра (0,0).")]
    [SerializeField] private Vector2[] placementCandidatePoints = new Vector2[]
    {
        new Vector2(0f, 0f),
        new Vector2(-0.5f, 0.5f),
        new Vector2(0.5f, 0.5f),
        new Vector2(-0.5f, -0.5f),
        new Vector2(0.5f, -0.5f),
        new Vector2(0f, 0.5f),
        new Vector2(0f, -0.5f),
        new Vector2(-0.5f, 0f),
        new Vector2(0.5f, 0f)
    };

    private List<GameObject> _annotationPool;
    private List<AnnotationView> _activeAnnotations;
    private List<RectTransform> _activeAnnotationTextBlockRects;

    private List<InfoTarget> _registeredTargets;
    private bool _isOverlayActive = false;

    private EventManager _eventManager;
    private RectTransform _prefabTextBlockRect;

    private List<LayoutGroup> _disabledLayoutGroups; // Список Layout Group'ов, которые мы отключили

    private List<Rect> _placedAnnotationAllowedRects;


    private void Awake()
    {
        _applicationIsQuitting = false;

        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        InitializePool();
    }

    void Start()
    {
        _eventManager = EventManager.Instance;
        if (_eventManager == null) Debug.LogError("[InfoOverlayController] EventManager не найден!", this);
        SetOverlayVisibility(false);
    }

    void OnApplicationQuit()
    {
        _applicationIsQuitting = true;
    }

    private void OnDestroy()
    {
        _applicationIsQuitting = true;
        StopAllCoroutines();
    }

    /// Инициализирует пул объектов аннотаций.
    private void InitializePool()
    {
        if (annotationPrefab == null)
        {
            Debug.LogError("[InfoOverlayController] Префаб аннотации не назначен в инспекторе!", this);
            _annotationPool = new List<GameObject>();
            _activeAnnotations = new List<AnnotationView>();
            _activeAnnotationTextBlockRects = new List<RectTransform>();
            _placedAnnotationAllowedRects = new List<Rect>();
            return;
        }

        _annotationPool = new List<GameObject>();
        _activeAnnotations = new List<AnnotationView>();
        _activeAnnotationTextBlockRects = new List<RectTransform>();
        _placedAnnotationAllowedRects = new List<Rect>();


        AnnotationView prefabView = annotationPrefab.GetComponent<AnnotationView>();
        if (prefabView != null && prefabView.TextBlockRect != null)
        {
             _prefabTextBlockRect = prefabView.TextBlockRect;
        }
        else Debug.LogError("[InfoOverlayController] Префаб аннотации или его TextBlockRect не настроен!", this);

        // Создаем начальное количество объектов в пуле
        for (int i = 0; i < poolInitialSize; i++)
        {
            GameObject annotationObject = Instantiate(annotationPrefab, overlayCanvas.transform);
            annotationObject.SetActive(false);
            _annotationPool.Add(annotationObject);
        }
    }

    /// Получает объект аннотации из пула.
    private GameObject GetAnnotationFromPool()
    {
        GameObject annotationObject = null;

        // Ищем неактивный объект в пуле
        for (int i = 0; i < _annotationPool.Count; i++)
        {
            if (!_annotationPool[i].activeSelf)
            {
                annotationObject = _annotationPool[i];
                break;
            }
        }

        // Если не нашли, создаем новый (пул расширяется)
        if (annotationObject == null)
        {
            annotationObject = Instantiate(annotationPrefab, overlayCanvas.transform);
            _annotationPool.Add(annotationObject);
        }

        // Объект будет активирован в AnnotationView.Setup()
        annotationObject.SetActive(true); // Активируем объект перед использованием

        return annotationObject;
    }

    /// Возвращает объект аннотации обратно в пул.
    private void ReturnAnnotationToPool(GameObject annotationObject)
    {
        if (annotationObject != null)
        {
            AnnotationView view = annotationObject.GetComponent<AnnotationView>();
            if (view != null)
            {
                 view.Hide(); // Скрываем и очищаем данные
            }
            else
            {
                // Если нет AnnotationView, просто скрываем
                annotationObject.SetActive(false);
            }
        }
    }

    /// Регистрирует InfoTarget. Вызывается InfoTarget.OnEnable.
    public void RegisterTarget(InfoTarget target)
    {
        if (target != null && _registeredTargets != null && !_registeredTargets.Contains(target))
        {
            _registeredTargets.Add(target);
        }
    }

    /// Отменяет регистрацию InfoTarget. Вызывается InfoTarget.OnDisable.
    public void UnregisterTarget(InfoTarget target)
    {
        if (target != null && _registeredTargets != null)
        {
            _registeredTargets.Remove(target);
        }
    }

    /// Переключает состояние инфо-оверлея
    public void ToggleOverlay()
    {
        if (_isOverlayActive)
        {
            DeactivateOverlay();
        }
        else
        {
            ActivateOverlay();
        }
    }

    /// Активирует режим инфо-оверлея.
    public void ActivateOverlay() // Сделали публичным, чтобы его мог вызвать VSM
    {
        if (_isOverlayActive) return;
        _isOverlayActive = true;

        // Устанавливаем видимость оверлей Canvas и затемнение/блокировку
        SetOverlayVisibility(true);

        // Отключаем Layout Group'ы на родителях InfoTarget'ов, чтобы они не мешали позиционированию
        _disabledLayoutGroups = new List<LayoutGroup>(); // Инициализируем список отключенных LayoutGroup'ов
        List<RectTransform> parentsToRebuild = new List<RectTransform>(); // Список родителей, которые могут нуждаться в принудительном ребилде Layout'а

        // Проходим по всем зарегистрированным InfoTarget'ам
        foreach (var target in _registeredTargets)
        {
            if (target == null || target.TargetRectTransform == null) continue;

            // Идем вверх по иерархии от целевого объекта до Canvas'а
            Transform currentParent = target.TargetRectTransform.parent;
            while(currentParent != null && currentParent != overlayCanvas.transform) // Идем до корневого Canvas или пока не кончится иерархия
            {
                 LayoutGroup layoutGroup = currentParent.GetComponent<LayoutGroup>();
                 if (layoutGroup != null && layoutGroup.enabled) // Если нашли активный LayoutGroup
                 {
                     // Если это новый LayoutGroup, который мы еще не отключали
                     if (!_disabledLayoutGroups.Contains(layoutGroup))
                     {
                         _disabledLayoutGroups.Add(layoutGroup); // Добавляем в список
                         layoutGroup.enabled = false; // Отключаем его
                         // Debug.Log($"[InfoOverlayController] Отключен LayoutGroup на объекте '{currentParent.name}' для InfoTarget '{target.name}'.", this); // Отладочный лог удален

                         // Добавляем родителя в список для возможного ребилда после отключения Layout Group'а
                         if (currentParent.GetComponent<RectTransform>() != null && !parentsToRebuild.Contains(currentParent.GetComponent<RectTransform>()))
                         {
                              parentsToRebuild.Add(currentParent.GetComponent<RectTransform>());
                         }
                     }
                 }
                 currentParent = currentParent.parent; // Переходим к следующему родителю
            }
        }

        // Принудительно перестраиваем Layout на объектах, чьи Layout Group'ы были отключены.
        foreach(var parentRect in parentsToRebuild)
        {
             // Проверяем, что объект еще существует
             if (parentRect != null)
             {
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
             }
        }

        // 1. Фильтруем: только активные в иерархии и с назначенной AllowedPlacementArea
        List<InfoTarget> validTargets = _registeredTargets
            .Where(target => target != null && target.gameObject.activeInHierarchy && target.AllowedPlacementArea != null)
            .ToList();

        // 2. Применяем специальное правило для приоритета 0
        List<InfoTarget> targetsToPlace;
        bool hasPriorityZero = validTargets.Any(target => target.Priority == 0);

        if (hasPriorityZero)
        {
            // Если есть хотя бы один таргет с приоритетом 0, обрабатываем только их
            targetsToPlace = validTargets.Where(target => target.Priority == 0).ToList();
        }
        else
        {
            // Иначе обрабатываем все валидные таргеты (с приоритетом > 0)
            targetsToPlace = validTargets.Where(target => target.Priority > 0).ToList();
        }

        // Очищаем списки перед новым размещением
        _activeAnnotations.Clear();
        _activeAnnotationTextBlockRects.Clear();
        _placedAnnotationAllowedRects.Clear(); // Очищаем список занятых зон AllowedPlacementArea

        // Запускаем корутину для размещения аннотаций с отфильтрованным списком
        StartCoroutine(PlaceAnnotationsCoroutine(targetsToPlace));
    }

    /// Публичный метод для запроса деактивации оверлея извне (например, по клику на затемняющей панели).
    public void RequestDeactivateOverlay()
    {
         DeactivateOverlay(); // Вызываем приватный метод деактивации
    }


    /// Деактивирует режим инфо-оверлея.
    private void DeactivateOverlay() // Этот метод остается приватным, вызывается из RequestDeactivateOverlay или ToggleOverlay
    {
        if (!_isOverlayActive) return;
        _isOverlayActive = false;

        // Останавливаем корутину размещения, если она еще работает
        StopAllCoroutines();

        // Скрываем все активные аннотации и возвращаем их в пул
        foreach (var annotationView in _activeAnnotations)
        {
            if (annotationView != null)
            {
                ReturnAnnotationToPool(annotationView.gameObject);
            }
        }
        _activeAnnotations.Clear();
        _activeAnnotationTextBlockRects.Clear(); // Очищаем список занятых мест TextBlockRect
        _placedAnnotationAllowedRects.Clear(); // Очищаем список занятых зон AllowedPlacementArea


        // Восстанавливаем ранее отключенные Layout Group'ы
        if (_disabledLayoutGroups != null)
        {
            foreach (var layoutGroup in _disabledLayoutGroups)
            {
                if (layoutGroup != null) // Проверяем, что объект LayoutGroup не уничтожен
                {
                    layoutGroup.enabled = true; // Включаем LayoutGroup обратно
                }
            }
            _disabledLayoutGroups.Clear(); // Очищаем список
        }

        // Скрываем оверлей Canvas и затемнение/блокировку
        SetOverlayVisibility(false);
    }

    /// Устанавливает видимость оверлей Canvas и его блокирующего слоя.
    private void SetOverlayVisibility(bool isVisible)
    {
        if (overlayCanvas != null)
        {
            overlayCanvas.enabled = isVisible;
        }
        else Debug.LogError("[InfoOverlayController] Overlay Canvas не назначен!", this);


        if (dimmingAndBlockerGroup != null)
        {
            dimmingAndBlockerGroup.alpha = isVisible ? dimmingAlpha : 0f;
            dimmingAndBlockerGroup.blocksRaycasts = isVisible;
            dimmingAndBlockerGroup.interactable = false; // Этот элемент блокирует Raycast'ы, но сам не интерактивен
        }
         else Debug.LogError("[InfoOverlayController] Dimming and Blocker CanvasGroup не назначен!", this);
    }

    /// Корутина для поиска InfoTarget'ов и размещения аннотаций с учетом приоритетов и зон.
    private IEnumerator PlaceAnnotationsCoroutine(List<InfoTarget> targetsToProcess)
    {
        yield return null;

        // Очищаем списки перед новым размещением (уже сделано в ActivateOverlay, но оставим на всякий случай)
        _activeAnnotations.Clear();
        _activeAnnotationTextBlockRects.Clear();
        _placedAnnotationAllowedRects.Clear(); // Очищаем список занятых зон AllowedPlacementArea


        if (_prefabTextBlockRect == null)
        {
            Debug.LogError("[InfoOverlayController] Не удалось получить RectTransform текстового блока из префаба. Размещение невозможно.");
            yield break;
        }
        // Получаем стандартный размер текстового блока аннотации из префаба
        Vector2 annotationTextBlockSize = _prefabTextBlockRect.rect.size;
        // Получаем пивот текстового блока из префаба для корректного расчета позиции
        Vector2 annotationTextBlockPivot = _prefabTextBlockRect.pivot;

        // Сортируем по приоритету (от самого высокого к самому низкому).
        List<InfoTarget> sortedTargetsToPlace = targetsToProcess.OrderBy(target => target.Priority).ToList();

        // При работе в Screen Space - Overlay, камера для пересчета координат не нужна,
        RectTransform overlayCanvasRect = overlayCanvas.GetComponent<RectTransform>();

        // Пробуем разместить аннотации в порядке приоритета
        int placedCount = 0;
        foreach (var target in sortedTargetsToPlace)
        {
            if (!_isOverlayActive) yield break; // Прерываем, если оверлей деактивирован
            if (target == null || target.AllowedPlacementArea == null) continue; // Дополнительная проверка на всякий случай

            // Получаем RectTransform зоны размещения для этого InfoTarget
            RectTransform allowedAreaRect = target.AllowedPlacementArea;

            // Пересчитываем границы AllowedPlacementArea в локальные координаты Overlay Canvas'а
            Vector3[] worldCorners = new Vector3[4];
            allowedAreaRect.GetWorldCorners(worldCorners); // Получаем мировые координаты углов (работает для UI)

            Vector2[] overlayLocalCorners = new Vector2[4];

            for (int i = 0; i < 4; i++)
            {
                Vector3 screenPos = UnityEngine.RectTransformUtility.WorldToScreenPoint(null, worldCorners[i]);
                UnityEngine.RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    overlayCanvasRect,
                    screenPos,
                    null, // Камера: null для Screen Space - Overlay канвасов
                    out overlayLocalCorners[i]
                );
            }

            // Создаем Rect в локальных координатах Overlay Canvas'а из этих углов
            Rect overlayAllowedRect = GetRectFromCorners(overlayLocalCorners);

            ///НОВОЕ///
            // 5. Проверяем перекрытие с уже размещенными зонами AllowedPlacementArea ТОЛЬКО для приоритетов > 0.
            bool intersectsExistingAllowedArea = false;
            if (target.Priority != 0) // Проверка перекрытий выполняется только для приоритетов > 0
            {
                 foreach (var placedRect in _placedAnnotationAllowedRects)
                 {
                     if (overlayAllowedRect.Overlaps(placedRect))
                     {
                         intersectsExistingAllowedArea = true;
                         break;
                     }
                 }
            }


            if (!intersectsExistingAllowedArea) // Этот блок выполняется, если проверка перекрытия не проводилась (P=0) или она не выявила перекрытий (P>0)
            {
                // 6. Пробуем найти позицию для аннотации внутри overlayAllowedRect, проверяя кандидатные точки
                Vector2 bestPlacementLocalPos = Vector2.zero;
                bool positionFound = false;

                // Итерируем по предопределенным кандидатным позициям (относительно центра AllowedPlacementArea)
                foreach (var candidateRelativePos in placementCandidatePoints)
                {
                    // Рассчитываем потенциальную позицию в локальных координатах Overlay Canvas
                    Vector2 potentialLocalPos = overlayAllowedRect.center + new Vector2(overlayAllowedRect.width * candidateRelativePos.x, overlayAllowedRect.height * candidateRelativePos.y);

                    // Рассчитываем Rect, который займет текстовый блок при размещении его пивота в potentialLocalPos
                    Rect testRect = new Rect(
                        potentialLocalPos.x - annotationTextBlockSize.x * annotationTextBlockPivot.x,
                        potentialLocalPos.y - annotationTextBlockSize.y * annotationTextBlockPivot.y,
                        annotationTextBlockSize.x,
                        annotationTextBlockSize.y
                    );

                    // Проверяем, находится ли testRect полностью внутри overlayAllowedRect (TextBlock должен поместиться в зону)
                    bool containsCheck = overlayAllowedRect.Contains(testRect.min) && overlayAllowedRect.Contains(testRect.max);

                    if (!containsCheck)
                    {
                        continue; // Пробуем следующую точку, эта позиция не подходит
                    }

                    // Нашли подходящую позицию! (хотя бы одну, где TextBlock помещается в AllowedPlacementArea)
                    bestPlacementLocalPos = potentialLocalPos; // Сохраняем позицию, куда хотим поставить пивот аннотации
                    positionFound = true;
                    break; // Выходим из цикла по кандидатным точкам, достаточно первой подходящей
                }

                // --- Если позиция для TextBlock найдена внутри AllowedPlacementArea ---
                if (positionFound)
                {
                    GameObject annotationObject = GetAnnotationFromPool();
                    if (annotationObject != null)
                    {
                        AnnotationView annotationView = annotationObject.GetComponent<AnnotationView>();
                        if (annotationView != null)
                        {
                            // Размещаем текстовый блок аннотации в найденной позиции
                            annotationView.TextBlockRect.anchoredPosition = bestPlacementLocalPos;

                            // Настраиваем и активируем аннотацию
                            annotationView.Setup(target.HintText, target.TargetRectTransform, overlayCanvas.GetComponent<RectTransform>());

                            // Добавляем в списки активных
                            _activeAnnotations.Add(annotationView);
                            // Добавляем RectTransform текстового блока в список занятых областей TextBlock (для рисования линии)
                            _activeAnnotationTextBlockRects.Add(annotationView.TextBlockRect);

                            _placedAnnotationAllowedRects.Add(overlayAllowedRect);

                            placedCount++;
                        }
                        else
                        {
                            Debug.LogError($"[InfoOverlayController] Префаб аннотации '{annotationPrefab.name}' не имеет компонента AnnotationView!", this);
                            ReturnAnnotationToPool(annotationObject); // Вернуть в пул, т.к. не можем использовать
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[InfoOverlayController] Не удалось получить объект аннотации из пула.", this);
                    }
                }
                 else
                 {
                     // Этот случай сработает, если TextBlock не поместился ни в одну из кандидатных точек внутри AllowedPlacementArea
                     Debug.LogWarning($"ТАРГЕТ: {target.name}: Позиция для текстового блока не найдена внутри AllowedPlacementArea! (TextBlock не поместился)", target);
                 }
            }
             else { }


            if (placementCalculationDelay > 0)
                yield return new WaitForSeconds(placementCalculationDelay);
            else
                yield return null; // Ждем хотя бы один кадр
        }
    }

    /// Вспомогательный метод для получения Rect из массива 4-х углов в Vector2.
    private Rect GetRectFromCorners(Vector2[] corners)
    {
        float minX = Mathf.Min(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
        float minY = Mathf.Min(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
        float maxX = Mathf.Max(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
        float maxY = Mathf.Max(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    /// Обновление позиций линий для всех активных аннотаций.
    void Update()
    {
        if (_isOverlayActive)
        {
            foreach (var annotationView in _activeAnnotations)
            {
                if (annotationView != null && annotationView.gameObject.activeSelf)
                {
                    annotationView.UpdateLinePosition();
                }
            }
        }
    }
}