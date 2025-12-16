using System;
using UnityEngine;

/// <summary>
/// Главный интерфейс-фасад, описывающий всю специфичную логику конкретной модели машины.
/// Агрегирует и предоставляет доступ ко всем системам машины.
/// </summary>
public interface IMachineLogic
{
    // --- Свойства состояния (для доступа из MC/Monitor) ---

    /// <summary>
    /// Ссылка на Transform траверсы (или основного подвижного узла).
    /// </summary>
    Transform TraverseTransform { get; }

    /// <summary>
    /// Состояние верхнего захвата (если есть).
    /// </summary>
    bool IsUpperClamped { get; }

    /// <summary>
    /// Состояние нижнего захвата (если есть).
    /// </summary>
    bool IsLowerClamped { get; }

    // --- Компоненты ---

    IMoveableComponent Positioner { get; }
    IMoveableComponent Loader { get; }
    IDoorController Doors { get; }
    IGripController Grips { get; } // Может быть null

    // --- Жизненный цикл и События ---

    /// <summary>
    /// Событие изменения статуса занятости любой из систем машины.
    /// </summary>
    event Action<bool> OnBusyStateChanged;

    /// <summary>
    /// Событие отказа выполнения действия. 
    /// Передает строку с причиной отказа (для отображения в UI).
    /// </summary>
    event Action<string> OnActionRejected;

    /// <summary>
    /// Метод обновления (вызывается каждый кадр из MC).
    /// </summary>
    void OnUpdate();

    /// <summary>
    /// Метод очистки (вызывается при уничтожении MC).
    /// </summary>
    void OnDestroy();

    // --- Команды ---

    // 1. Ручное управление ПОЗИЦИОНЕРОМ
    void StartManualPositioning(Direction direction, SpeedType speed);
    void AdjustManualPositioningSpeed(bool increase);
    void StopManualPositioning();

    // 2. Автоматический подвод ПОЗИЦИОНЕРА
    void StartAutomaticApproach(float targetPosition);

    // 3. Ручное управление НАГРУЖАТЕЛЕМ
    void StartManualLoading(Direction direction, SpeedType speed);
    void StopManualLoading();

    // 4. Движение по программе
    void ApplyProgrammaticDisplacement(float displacement);

    // 5. Вспомогательные системы
    void SetSupportSystemState(bool isActive); 
    void SetPowerUnitState(bool isOn);

    // 6. Сложная логика (делегированная из MC)
    /// <summary>
    /// Обеспечивает необходимый зазор для установки оснастки.
    /// </summary>
    void EnsureClearance(TestType testType, float? targetLocalZ, ActionRequester requester);

    /// <summary>
    /// Рассчитывает точку подвода траверсы и сообщает её прямо в SystemStateMonitor.
    /// </summary>
    /// <param name="driveWorld">Мировая точка драйв</param>
    /// <param name="undriveWorld">Мировая точка андрайв</param>
    /// <param name="effectiveLen">Эффективная длина образца (мм)</param>
    /// <param name="type">Тип действия</param>
    void CalculateAndReportApproach(Vector3 driveWorld, Vector3 undriveWorld, float effectiveLen, ApproachActionType type);
    
    // Методы управления захватами (прокси для удобства MC)
    void ClampUpper();
    void UnclampUpper();
    void ClampLower();
    void UnclampLower();

    // --- Статус готовности машины ---

    /// <summary>
    /// Готова ли машина к процедурам настройки (установка образца, подвод траверсы).
    /// </summary>
    bool IsReadyForSetup { get; }

    /// <summary>
    /// Текст ошибки/подсказки, если машина не готова.
    /// </summary>
    string NotReadyReason { get; }

    /// <summary>
    /// Событие, вызываемое при изменении статуса готовности.
    /// </summary>
    event Action OnReadyStateChanged;

    // Событие изменения состояния питания (насоса)
    event Action<bool> OnPowerUnitStateChanged; 
}