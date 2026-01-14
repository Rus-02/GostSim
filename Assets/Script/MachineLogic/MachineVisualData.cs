using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;

public enum MachineVisualCategory
{
    Frame, Fixture, Hydraulics, Measurement, Protection, Electronics, DriveSystem
}

[Serializable]
public class VisualCategoryEntry
{
    public string DisplayName;
    public MachineVisualCategory CategoryType;
    public List<GameObject> AssociatedObjects = new List<GameObject>();
    public Transform FocusPoint;
}

public class MachineVisualData : MonoBehaviour
{
    [Header("0. Общие настройки")]
    public Transform GlobalOverviewFocusPoint;

    [Header("1. Визуальные категории (Меню)")]
    public List<VisualCategoryEntry> VisualCategories = new List<VisualCategoryEntry>();

    [Header("2. Ключевые Точки (Имена объектов важны!)")]
    [Tooltip("Перетащи сюда все важные точки (места крепления, зоны). Система будет искать их по ИМЕНИ ОБЪЕКТА (Name).")]
    public List<Transform> MachinePoints = new List<Transform>();

    [Header("3. Объекты для скрытия (Кожухи)")]
    public List<GameObject> ObjectsToHide = new List<GameObject>();

    // Кэш для быстрого поиска
    private Dictionary<string, Transform> _pointsCache;

    // Инициализация кэша (вызывается при старте или при первом запросе)
    private void EnsureCache()
    {
        if (_pointsCache != null) return;
        
        _pointsCache = new Dictionary<string, Transform>();
        foreach (var t in MachinePoints)
        {
            if (t != null)
            {
                if (!_pointsCache.ContainsKey(t.name))
                {
                    _pointsCache.Add(t.name, t);
                }
                else
                {
                    Debug.LogWarning($"[MachineVisualData] Дубликат имени точки: '{t.name}'. Используется первая найденная.");
                }
            }
        }
    }

    // --- Публичный API ---

    public VisualCategoryEntry GetCategory(MachineVisualCategory type)
    {
        return VisualCategories.FirstOrDefault(c => c.CategoryType == type);
    }

    /// <summary>
    /// Находит точку по имени объекта.
    /// Заменяет GameObject.Find(name).
    /// </summary>
    public Transform GetPoint(string pointName)
    {
        EnsureCache();
        if (_pointsCache.TryGetValue(pointName, out Transform t))
        {
            return t;
        }
        return null;
    }

    public Transform GetFocusPointSafe(MachineVisualCategory? type)
    {
        if (type == null) return GlobalOverviewFocusPoint != null ? GlobalOverviewFocusPoint : transform;
        var cat = GetCategory(type.Value);
        if (cat != null && cat.FocusPoint != null) return cat.FocusPoint;
        if (GlobalOverviewFocusPoint != null) return GlobalOverviewFocusPoint;
        return transform;
    }
}