public enum AnimationDirection
{
    In,                 // Установка оснастки
    Out,                // Снятие оснастки
    SampleInstall,      // Установка образца
    SampleRemove,       // Снятие образца
    Custom,             // Любая другая кастомная анимация
    None
    }
    
public enum AnimationStepType
{
    Move,
    Rotate,
    Wait,
    SavePosition,       // Сохранить текущую позицию объекта
    RestorePosition,    // Восстановить сохраненную позицию объекта
    InstantiatePrefab,  // Создать копию префаба
    DestroyPrefab     // Удалить копию префаба
}
