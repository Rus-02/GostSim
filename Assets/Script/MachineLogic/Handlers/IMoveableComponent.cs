using System;

/// <summary>
/// Абстракция для любого движущегося узла испытательной машины.
/// Описывает "что" узел должен уметь делать, но не "как" он это делает.
/// </summary>
public interface IMoveableComponent
{
    /// <summary>
    /// Мгновенно устанавливает позицию компонента.
    /// Используется для синхронизации с данными графика во время теста.
    /// </summary>
    /// <param name="absoluteDisplacement">Абсолютное смещение от начальной точки в метрах.</param>
    void SetPositionByDisplacement(float absoluteDisplacement);

    /// <summary>
    /// Запускает плавное движение к целевой точке с заданной скоростью.
    /// Используется для автоматического подвода.
    /// </summary>
    /// <param name="targetPosition">Целевая мировая координата по оси движения.</param>
    /// <param name="speed">Скорость движения в м/с.</param>
    /// <param name="onCompleteCallback">Действие, которое выполнится по завершении движения.</param>
    void MoveTo(float targetPosition, float speed, Action onCompleteCallback);

    /// <summary>
    /// Запускает непрерывное движение в заданном направлении.
    /// Используется для ручного управления кнопками.
    /// </summary>
    /// <param name="direction">Направление движения.</param>
    /// <param name="speed">Скорость движения в м/с.</param>
    void MoveContinuously(Direction direction, float speed);

    /// <summary>
    /// Корректирует скорость непрерывного движения.
    /// </summary>
    /// <param name="increase">True - увеличить, false - уменьшить.</param>
    void AdjustContinuousSpeed(bool increase);

    /// <summary>
    /// Останавливает любое текущее движение компонента.
    /// </summary>
    void Stop();
}