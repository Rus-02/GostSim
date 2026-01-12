using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewMap", menuName = "Scenario/Map Input")]
public class MapInput : ScriptableObject
{
    [Header("Global Settings")]
    [Tooltip("Что делать с кнопками, которых нет в списке ниже?")]
    public RuleType DefaultBehavior = RuleType.Standard;

    [Tooltip("Текст подсказки для DefaultBehavior = ForceBlock")]
    public string DefaultBlockHint = "Действие недоступно в этом режиме.";

    [Header("Overrides")]
    public List<ButtonRuleEntry> Rules = new List<ButtonRuleEntry>();

    // Внутренний словарь для быстрого поиска
    private Dictionary<string, RuleInput> _lookup;

    public void Initialize()
    {
        _lookup = new Dictionary<string, RuleInput>();
        foreach (var entry in Rules)
        {
            if (!string.IsNullOrEmpty(entry.ButtonID) && entry.Rule != null)
            {
                _lookup[entry.ButtonID] = entry.Rule;
            }
        }
    }

    public RuleInput GetRule(string buttonId)
    {
        if (_lookup == null) Initialize();

        if (_lookup.TryGetValue(buttonId, out var rule))
        {
            return rule;
        }
        return null;
    }

    [System.Serializable]
    public struct ButtonRuleEntry
    {
        public string ButtonID; // ID кнопки (совпадает с UIBinding)
        public RuleInput Rule;  // Ссылка на ассет правила
    }
}