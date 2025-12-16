using System;

/// <summary>
/// Абстракция для системы защитного ограждения (дверей) машины.
/// </summary>
public interface IDoorController
{
    /// <summary>
    /// Запускает процесс открытия дверей.
    /// </summary>
    /// <param name="onComplete">Действие, которое выполнится по завершении открытия.</param>
    void OpenDoors(Action onComplete = null);

    /// <summary>
    /// Запускает процесс закрытия дверей.
    /// </summary>
    /// <param name="onComplete">Действие, которое выполнится по завершении закрытия.</param>
    void CloseDoors(Action onComplete = null);
}