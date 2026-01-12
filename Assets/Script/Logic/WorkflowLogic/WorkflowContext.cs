using System.Collections.Generic;
using UnityEngine;

public class WorkflowContext
{
    /// <summary>
    /// Ссылка на центральный менеджер, чтобы шаги могли дергать методы CSM (или через него ToDoManager).
    /// </summary>
    public CentralizedStateManager CSM { get; private set; }

    // "Blackboard" - словарь для передачи любых данных между шагами.
    // Например: Шаг 1 (Расчет) кладет сюда список ID, Шаг 2 (Установка) забирает его.
    private Dictionary<string, object> _data = new Dictionary<string, object>();

    public WorkflowContext(CentralizedStateManager csm)
    {
        CSM = csm;
    }

    /// <summary>
    /// Сохранить данные в контекст (например, "PlanToInstall").
    /// </summary>
    public void SetData<T>(string key, T value)
    {
        if (_data.ContainsKey(key))
        {
            _data[key] = value;
        }
        else
        {
            _data.Add(key, value);
        }
    }

    /// <summary>
    /// Получить данные из контекста. Возвращает default(T), если ключ не найден.
    /// </summary>
    public T GetData<T>(string key)
    {
        if (_data.TryGetValue(key, out object val))
        {
            if (val is T typedVal) return typedVal;
        }
        return default(T);
    }

    /// <summary>
    /// Проверка наличия ключа (чтобы не получить null).
    /// </summary>
    public bool HasData(string key)
    {
        return _data.ContainsKey(key);
    }

    public ActionRequester Requester { get; private set; } = ActionRequester.CSM;
    public WorkflowContext(CentralizedStateManager csm, ActionRequester requester = ActionRequester.CSM)
    {
        CSM = csm;
        Requester = requester;
    }

}