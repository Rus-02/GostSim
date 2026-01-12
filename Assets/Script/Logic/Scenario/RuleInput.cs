using UnityEngine;

public enum RuleType
{
    Standard,    // Проверить ActionPolicy (ТБ работает)
    ForceBlock,  // ЗАПРЕТИТЬ (Жесткий блок сценарием, например в Обучении)
    ForceAllow   // РАЗРЕШИТЬ (Игнорировать ТБ, например в Экзамене)
}

[CreateAssetMenu(fileName = "NewRule", menuName = "Scenario/Rule Input")]
public class RuleInput : ScriptableObject
{
    [Header("Logic")]
    [Tooltip("Тип поведения правила")]
    public RuleType Type = RuleType.Standard;

    [Tooltip("Условие. Если оно НЕ выполнено — правило превращается в Block. Оставь пустым, если условие не нужно.")]
    public InputCondition Condition; 

    [Header("Feedback")]
    [Tooltip("Текст ошибки, если правило блокирует действие")]
    [TextArea]
    public string BlockHint = "Это действие сейчас недоступно.";

    /// <summary>
    /// Проверяет правило и возвращает текст ошибки (если блок) или null (если можно).
    /// </summary>
    public string CheckBlock()
    {
        // 1. Если есть условие и оно НЕ выполнено — это Блок
        if (Condition != null && !Condition.IsMet())
        {
            return BlockHint;
        }

        // 2. Если тип ForceBlock — это Блок
        if (Type == RuleType.ForceBlock)
        {
            return BlockHint;
        }

        // Иначе — разрешено (null)
        return null;
    }
}