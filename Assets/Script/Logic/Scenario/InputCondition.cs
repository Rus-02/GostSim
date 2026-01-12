using UnityEngine;

/// <summary>
/// Базовый класс для любых условий (Есть образец? Давление в норме? и т.д.)
/// </summary>
public abstract class InputCondition : ScriptableObject
{
    public abstract bool IsMet();
}