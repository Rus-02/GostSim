using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Класс-обертка для инкапсуляции данных команды, 
/// передаваемой команд от хендлеров к CSM.
/// </summary>
public class ToDoManagerCommand
{
    public ActionType Action { get; }
    public BaseActionArgs Args { get; }

    public bool WaitForCompletion { get; }
    public ToDoManagerCommand(ActionType action, BaseActionArgs args, bool waitForCompletion = false)
    {
        Action = action;
        Args = args;
        WaitForCompletion = waitForCompletion;
    }
}

/// <summary>
/// Реализует паттерн "Издатель-подписчик" (Pub/Sub) для централизованной 
/// обработки и рассылки команд (действий) в приложении.
/// Выступает в роли единой точки для отправки и получения системных событий.
/// </summary>
public class ToDoManager : MonoBehaviour
{
    #region Singleton Implementation
    private static ToDoManager _instance;
    private static bool isShuttingDown = false;
    public static ToDoManager Instance
    {
        get
        {
            if (isShuttingDown) return null;
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<ToDoManager>();
                if (_instance == null)
                {
                    Debug.LogWarning("[ToDoManager] Instance is null and couldn't be found. Ensure a ToDoManager exists in the scene.");
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        isShuttingDown = false;
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning($"[ToDoManager] Duplicate instance detected. Destroying {this.gameObject.name}.");
            Destroy(this.gameObject);
            return;
        }
        _instance = this;

        // Настраиваем список действий, которые будут исключены из логирования.
        InitializeLogIgnoreList();
    }

    private void OnDestroy() { isShuttingDown = true; }
    #endregion

    /// <summary>
    /// Словарь для хранения всех подписчиков, сгруппированных по типу действия.
    /// Ключ: ActionType, Значение: Список делегатов (слушателей).
    /// </summary>
    private Dictionary<ActionType, List<Action<BaseActionArgs>>> _actionSubscribers =
        new Dictionary<ActionType, List<Action<BaseActionArgs>>>();

    /// <summary>
    /// Коллекция ActionType для исключения из детального логирования.
    /// Позволяет скрыть "шумные" или часто вызываемые события, чтобы не засорять консоль.
    /// Используется HashSet для максимально быстрых проверок (O(1)).
    /// </summary>
    private HashSet<ActionType> _actionsToIgnoreInLog = new HashSet<ActionType>();

    /// <summary>
    /// Централизованно определяет, какие типы действий не следует выводить в лог.
    /// </summary>
    private void InitializeLogIgnoreList()
    {
        // Сюда добавляются ActionType, которые вызываются очень часто (например, каждый кадр) 
        // и не несут критической информации для общего анализа потока событий.
        _actionsToIgnoreInLog.Add(ActionType.UpdateHighlight);
        _actionsToIgnoreInLog.Add(ActionType.UpdatePromptDisplay);
        _actionsToIgnoreInLog.Add(ActionType.UpdateSampleVisuals);
        _actionsToIgnoreInLog.Add(ActionType.UpdateMachineVisuals);
        _actionsToIgnoreInLog.Add(ActionType.UpdateUIButtonVisuals);
        _actionsToIgnoreInLog.Add(ActionType.PlayFixtureAnimationAction);
        _actionsToIgnoreInLog.Add(ActionType.PlaceFixtureWithoutAnimation);
        _actionsToIgnoreInLog.Add(ActionType.SetDoorStateAction);
        _actionsToIgnoreInLog.Add(ActionType.ReinitializeFixtureZones);
    }

    /// <summary>
    /// Подписывает слушателя (listener) на определенный тип действия (actionType).
    /// </summary>
    /// <param name="actionType">Тип действия, на которое происходит подписка.</param>
    /// <param name="listener">Метод, который будет вызван при возникновении действия.</param>
    public void SubscribeToAction(ActionType actionType, Action<BaseActionArgs> listener)
    {
        if (listener == null)
        {
            Debug.LogWarning($"[ToDoManager] Attempted to subscribe with a null listener for action: {actionType}");
            return;
        }

        if (!_actionSubscribers.ContainsKey(actionType))
        {
            _actionSubscribers[actionType] = new List<Action<BaseActionArgs>>();
        }

        // Предотвращаем повторную подписку одного и того же слушателя.
        if (!_actionSubscribers[actionType].Contains(listener))
        {
            _actionSubscribers[actionType].Add(listener);
        }
        else
        {
            Debug.LogWarning($"[ToDoManager] Listener '{listener.Method.Name}' from '{listener.Target?.GetType().Name ?? "Static"}' already subscribed to action: {actionType}");
        }
    }

    /// <summary>
    /// Отписывает слушателя от определенного типа действия.
    /// </summary>
    /// <param name="actionType">Тип действия, от которого происходит отписка.</param>
    /// <param name="listener">Метод-слушатель для удаления.</param>
    public void UnsubscribeFromAction(ActionType actionType, Action<BaseActionArgs> listener)
    {
        if (listener == null) return;

        if (_actionSubscribers.TryGetValue(actionType, out List<Action<BaseActionArgs>> listeners))
        {
            if (listeners.Contains(listener))
            {
                listeners.Remove(listener);
                if (listeners.Count == 0)
                {
                    _actionSubscribers.Remove(actionType);
                }
            }
        }
    }

    /// <summary>
    /// Обрабатывает входящее действие, уведомляя всех его подписчиков.
    /// </summary>
    /// <param name="action">Тип выполняемого действия.</param>
    /// <param name="args">Аргументы, связанные с действием (могут быть null).</param>
    public void HandleAction(ActionType action, BaseActionArgs args = null)
    {
        // Перед выводом в консоль, проверяем, не находится ли действие в списке игнорируемых.
        if (!_actionsToIgnoreInLog.Contains(action))
        {
            Debug.Log($"[ToDoManager] Handling Action: {action} with Args: {args?.GetType().Name ?? "null"}");
        }

        // Если для данного действия есть подписчики, уведомляем их.
        if (_actionSubscribers.TryGetValue(action, out List<Action<BaseActionArgs>> listeners))
        {
            if (listeners.Count > 0)
            {
                // Создаем копию списка, чтобы избежать ошибок, если подписчик
                // решит отписаться прямо во время вызова его метода.
                List<Action<BaseActionArgs>> listenersCopy = new List<Action<BaseActionArgs>>(listeners);

                foreach (var listener in listenersCopy)
                {
                    try
                    {
                        listener?.Invoke(args);
                    }
                    catch (Exception ex)
                    {
                        // Оборачиваем вызов в try-catch, чтобы ошибка в одном подписчике
                        // не сломала всю цепочку уведомлений для остальных.
                        Debug.LogError($"[ToDoManager] Error invoking listener ({listener?.Target?.GetType().Name}.{listener?.Method.Name}) for action {action}: {ex.Message}\n{ex.StackTrace}");
                    }
                }
            }
        }
    }
}