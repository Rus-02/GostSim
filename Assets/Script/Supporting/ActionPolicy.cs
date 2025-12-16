using UnityEngine;
using System.Collections.Generic;
using System;

[CreateAssetMenu(fileName = "NewActionPolicy", menuName = "Policies/Action Policy")]
public class ActionPolicy : ScriptableObject
{
    [Serializable]
    public class StateRestriction
    {
        public TestState restrictedState;
        public List<TypeOfTest> restrictedTestTypes = new List<TypeOfTest>();
        [TextArea(2, 4)]
        public string hintMessage = "Действие временно недоступно.";
    }

    [Serializable]
    public class ActionRule
    {
        public EventType actionType;
        public List<StateRestriction> stateRestrictions;
    }

    public List<ActionRule> rules = new List<ActionRule>();

    public string GetHintForAction(EventType action, TestState currentState, TypeOfTest currentTestType)
    {
        foreach (var rule in rules)
        {
            if (rule.actionType == action)
            {
                foreach (var restriction in rule.stateRestrictions)
                {
                    if (restriction.restrictedState == currentState)
                    {
                        // Проверяем, применяется ли ограничение к текущему типу теста
                        bool appliesToAllTypes = restriction.restrictedTestTypes == null || restriction.restrictedTestTypes.Count == 0;
                        bool appliesToCurrentType = !appliesToAllTypes && restriction.restrictedTestTypes.Contains(currentTestType);

                        // Если ограничение для всех типов или для текущего типа, возвращаем подсказку
                        if (appliesToAllTypes || appliesToCurrentType)
                        {
                            return restriction.hintMessage;
                        }
                        // Иначе, если ограничение НЕ для всех и НЕ для текущего типа, пропускаем его
                    }
                }
                // Если дошли сюда, значит для данного action не найдено запретов для currentState И currentTestType
                return null; // Действие разрешено для этой комбинации состояния и типа теста
            }
        }
        // Если не найдено правила для данного action
        return null; // Действие разрешено (нет правила - нет запрета)
    }
}