public enum StepType
{
    Command,        // Шаг, содержащий команду для ToDoManager
    Animation,      // Шаг, описывающий анимацию оснастки
    CreateSample,    // Специальная команда для CSM на создание образца
    RemoveSample ,    // Специальная команда для CSM на удаление образца
    ShowHint,    // Шаг, содержащий подсказку для пользователя
}

// Класс, описывающий один шаг сценария
public class InstallationStep
{
    public StepType Type { get; set; }

    // Это поле используется, только если Type == Command.
    public ToDoManagerCommand Command { get; set; }

    // Эти поля используются, только если Type == Animation
    public FixtureZone ZoneToAnimate { get; set; }
    public AnimationDirection AnimationDirection { get; set; }
    // Используется, только если Type == ShowHint
    public string HintText { get; set; }    
}