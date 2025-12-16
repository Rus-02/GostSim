
using UnityEngine;
using System;
using System.Collections.Generic;

/// Базовый абстрактный класс для всех аргументов команд (действий), передаваемых через ToDoManager
public abstract class BaseActionArgs {}

// Аргументы для MachineController
public class MoveTraverseArgs : BaseActionArgs { public float Direction { get; }
    public SpeedType Speed { get; }
    public MoveTraverseArgs(float direction, SpeedType speed) { Direction = direction; Speed = speed; } }

public enum SpeedType { Fast, Slow }

//public class SetDoorStateArgs : BaseActionArgs {public bool OpenDoor { get; } public SetDoorStateArgs(bool openDoor) { OpenDoor = openDoor; }}

/*public class ApproachTraverseArgs : BaseActionArgs
{
    public float TargetZLocal { get; }
    public ApproachTraverseArgs(float targetZLocal) { TargetZLocal = targetZLocal; }
}*/

public class AdjustSpeedArgs : BaseActionArgs { public float Change { get; }
    public AdjustSpeedArgs(float change) { Change = change; } }

/*public class UpdateMachineVisualsArgs : BaseActionArgs { public float CurrentGraphRelativeDeformationPercent { get; } public float ActualSampleLength { get; } public TestProgressState ProgressState { get; }
    public UpdateMachineVisualsArgs(float currentGraphRelativeDeformationPercent, float actualSampleLength, TestProgressState progressState)
    { CurrentGraphRelativeDeformationPercent = currentGraphRelativeDeformationPercent; ActualSampleLength = actualSampleLength; ProgressState = progressState; } }*/

/*public class RequestApproachCalculationArgs : BaseActionArgs // Убедитесь, что базовый класс BaseActionArgs тот же
{
    public Vector3 DrivePosWorld { get; }
    public Vector3 UndrivePosWorld { get; }
    public float EffectiveDimension_mm { get; }
    public ApproachActionType ActionType { get; } 

    public RequestApproachCalculationArgs(Vector3 drivePosWorld, Vector3 undrivePosWorld, float effectiveDimension_mm, ApproachActionType actionType)
    {
        DrivePosWorld = drivePosWorld;
        UndrivePosWorld = undrivePosWorld;
        EffectiveDimension_mm = effectiveDimension_mm;
        ActionType = actionType;                     // Используем новый тип
    }
}*/
        
/*public class SetDynamicLimitsArgs : BaseActionArgs
{
    public bool Enable { get; }
    public float SampleLength { get; }
    public float DriveY { get; }
    public float UndriveY { get; }
    public float TraverseY { get; }
    public TypeOfTest SpecificTestType { get; }
    public TestType GeneralTestType { get; }
    public float ClampingLength { get; }

    public SetDynamicLimitsArgs(bool enable, float sampleLengthMm, float driveY, float undriveY, float traverseY, TypeOfTest specificTestType,
                                TestType generalTestType, float clampingLengthMm)
    {
        if (!enable)
        {
            throw new ArgumentException("This constructor is for enabling limits only. Use the constructor with only 'enable=false' for disabling.", nameof(enable));
        }
        if (sampleLengthMm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleLengthMm), "Sample length must be positive.");
        }

        Enable = true;
        SampleLength = sampleLengthMm;
        DriveY = driveY;
        UndriveY = undriveY;
        TraverseY = traverseY;
        SpecificTestType = specificTestType;
        GeneralTestType = generalTestType;
        ClampingLength = clampingLengthMm;
    }

    public SetDynamicLimitsArgs(bool enable)
    {
        if (enable)
        {
            throw new ArgumentException("This constructor is for disabling limits only. Use the constructor with all parameters for enabling.", nameof(enable));
        }
        Enable = false;
        SampleLength = float.NaN;
        DriveY = float.NaN;
        UndriveY = float.NaN;
        TraverseY = float.NaN;
        SpecificTestType = default;
        GeneralTestType = TestType.None;
        ClampingLength = float.NaN;
    }
}*/

/*public class UpdateMinLimitPostTensionArgs : BaseActionArgs
{
    public TypeOfTest CompletedTestType { get; private set; }
    public UpdateMinLimitPostTensionArgs(TypeOfTest testType) { CompletedTestType = testType; }
}*/

//public class SetOriginMachineLimitsArgs : BaseActionArgs { public float OriginMinLimit { get; } public float OriginMaxLimit { get; } public SetOriginMachineLimitsArgs(float originMinLimit, float originMaxLimit) { OriginMinLimit = originMinLimit; OriginMaxLimit = originMaxLimit; } }

/* Аргументы для TestController
public class InitializeTestControllerArgs : BaseActionArgs
{
    public TestParametersConfirmedEventArgs TestConfirmedArgs { get; }
    public GameObject SampleInstance { get; }

    ///НОВОЕ///
    public float KeyPoint_UTS_X_Percent { get; }      // X-координата UTS в % деформации
    public float KeyPoint_Rupture_X_Percent { get; }  // X-координата разрыва в % деформации
    public bool AreKeyPointsAvailable { get; }       // Флаг, указывающий, доступны ли эти точки

    public InitializeTestControllerArgs(
        TestParametersConfirmedEventArgs testConfirmedArgs,
        GameObject sampleInstance,
        float keyPointUtsXPercent,      // Добавляем параметр для UTS X
        float keyPointRuptureXPercent,  // Добавляем параметр для Rupture X
        bool areKeyPointsAvailable      // Добавляем флаг доступности
        )
    {
        if (testConfirmedArgs == null) throw new ArgumentNullException(nameof(testConfirmedArgs));
        if (sampleInstance == null) throw new ArgumentNullException(nameof(sampleInstance));

        TestConfirmedArgs = testConfirmedArgs;
        SampleInstance = sampleInstance;

        KeyPoint_UTS_X_Percent = keyPointUtsXPercent;
        KeyPoint_Rupture_X_Percent = keyPointRuptureXPercent;
        AreKeyPointsAvailable = areKeyPointsAvailable;
    }
}*/

/*public class SetCurrentLogicHandlerArgs : BaseActionArgs
{
    public ITestLogicHandler Handler { get; }
    public SetCurrentLogicHandlerArgs(ITestLogicHandler handler) { Handler = handler; }
}*/


/*public class UpdateSampleVisualsArgs : BaseActionArgs
{
    public float ScaledX { get; }
    public TestProgressState ProgressState { get; }
    public UpdateSampleVisualsArgs(float scaledX, TestProgressState progressState) { ScaledX = scaledX; ProgressState = progressState; }
}*/

public class NotifyTestControllerAnimationEventArgs : BaseActionArgs { public GameObject SourceAnimatorObject { get; }
    public NotifyTestControllerAnimationEventArgs(GameObject sourceAnimatorObject) { SourceAnimatorObject = sourceAnimatorObject; } }

// Аргументы для UIController
public class SetUIContainerActiveArgs : BaseActionArgs { public string ContainerId { get; } public bool Activate { get; }
    public SetUIContainerActiveArgs(string containerId, bool activate) { if (string.IsNullOrEmpty(containerId)) throw new ArgumentNullException(nameof(containerId)); ContainerId = containerId; Activate = activate; }}

public class UpdateUIStateArgs : BaseActionArgs { public string ButtonId { get; } public bool Enable { get; }
    public UpdateUIStateArgs(string buttonId, bool enable) { if (string.IsNullOrEmpty(buttonId)) throw new ArgumentNullException(nameof(buttonId)); ButtonId = buttonId; Enable = enable; } }

// Аргументы для FixtureController
public class PlayFixtureAnimationArgs : BaseActionArgs
{
    public string FixtureId { get; }
    public AnimationDirection Direction { get; }
    public ActionRequester Requester { get; }

    // Конструктор тоже с необязательным параметром
    public PlayFixtureAnimationArgs(string fixtureId, AnimationDirection direction, ActionRequester requester = ActionRequester.None)
    {
        if (string.IsNullOrEmpty(fixtureId))
        {
            throw new ArgumentNullException(nameof(fixtureId));
        }
        FixtureId = fixtureId;
        Direction = direction;
        Requester = requester;
    }
}

// Аргументы для UIHelperController
public class ShowHintArgs : BaseActionArgs { public string HintText { get; } public float Duration { get; }
    public ShowHintArgs(string hintText, float duration = -1f) { if (string.IsNullOrEmpty(hintText)) throw new ArgumentNullException(nameof(hintText)); HintText = hintText; Duration = duration; } }

// Аргументы для PromptController

public enum PromptSourceType { None, SystemAction, HoverInteraction, ClickInteraction, DropdownSelection }

///НОВОЕ///
public class UpdatePromptArgs : BaseActionArgs { public string TargetKeyOrIdentifier { get; } public PromptSourceType SourceType { get; } public string SourceSenderInfo { get; } public bool IsNewTargetForPrompt { get; }
    public UpdatePromptArgs(string targetKeyOrIdentifier, PromptSourceType sourceType, string sourceSenderInfo = null, bool isNewTargetForPrompt = true) {
        if (string.IsNullOrEmpty(targetKeyOrIdentifier)) { throw new ArgumentNullException(nameof(targetKeyOrIdentifier), "Целевой ключ или идентификатор не может быть пустым."); }
        TargetKeyOrIdentifier = targetKeyOrIdentifier; SourceType = sourceType; SourceSenderInfo = sourceSenderInfo; IsNewTargetForPrompt = isNewTargetForPrompt; } }

public enum HighlightType { SingleObject, FixtureType, ClearAll }
public class UpdateHighlightArgs : BaseActionArgs { public HighlightType Type { get; } public GameObject TargetObject { get; } public string FixtureTypeName { get; }
    public UpdateHighlightArgs(GameObject targetObject) { if (targetObject == null) throw new ArgumentNullException(nameof(targetObject), "Целевой объект для подсветки не может быть null."); Type = HighlightType.SingleObject; TargetObject = targetObject; FixtureTypeName = null; }
    public UpdateHighlightArgs(string fixtureTypeName) { if (string.IsNullOrEmpty(fixtureTypeName)) throw new ArgumentNullException(nameof(fixtureTypeName), "Имя типа оснастки не может быть null или пустым."); Type = HighlightType.FixtureType; TargetObject = null; FixtureTypeName = fixtureTypeName; }
    public UpdateHighlightArgs(bool clear) { if (!clear) throw new ArgumentException("Для создания команды очистки используйте конструктор UpdateHighlightArgs(true).", nameof(clear)); Type = HighlightType.ClearAll; TargetObject = null; FixtureTypeName = null; }
    private UpdateHighlightArgs() { } }



public class EmptyArgs : BaseActionArgs {public static readonly EmptyArgs Instance = new EmptyArgs(); private EmptyArgs() { } }

public class ControlTestArgs : BaseActionArgs {public enum ControlType { Start, Pause, Stop }
    public ControlType Command { get; } public ControlTestArgs(ControlType command) { Command = command; } }

/// Аргументы для команды размещения оснастки
public class PlaceFixtureArgs : BaseActionArgs { public string FixtureId { get; } public GameObject ParentObject { get; } public string InternalPointName { get; }
    public PlaceFixtureArgs(string fixtureId, GameObject parentObject, string internalPointName) { if (string.IsNullOrEmpty(fixtureId)) throw new ArgumentNullException(nameof(fixtureId)); FixtureId = fixtureId; ParentObject = parentObject; InternalPointName = internalPointName; } }

/// Аргументы для команды удаления оснастки
public class RemoveFixtureArgs : BaseActionArgs
{
    public string FixtureId { get; }
    public GameObject ParentObject { get; }
    public string InternalPointName { get; }

    public RemoveFixtureArgs(string fixtureId, GameObject parentObject, string internalPointName)
    {
        if (string.IsNullOrEmpty(fixtureId)) throw new ArgumentNullException(nameof(fixtureId));
        FixtureId = fixtureId;
        ParentObject = parentObject;
        InternalPointName = internalPointName;
    }
}

/// Аргументы для команды активации вкладки UI
public class ActivateUITabArgs : BaseActionArgs
{
    /// Строковый идентификатор вкладки для активации (например, "ControlTab", "TestTab", "ResultsTab").
    public string TabId { get; }

    public ActivateUITabArgs(string tabId)
    {
        if (string.IsNullOrEmpty(tabId))
        {
            throw new ArgumentNullException(nameof(tabId), "TabId не может быть null или пустым.");
        }
        TabId = tabId;
    }
}

public class EnsureFixtureInstallationClearanceArgs : BaseActionArgs
{
    public float? TargetLocalZ { get; }
    public TestType GeneralTestType { get; }
    public ActionRequester Requester { get; }

    public EnsureFixtureInstallationClearanceArgs(float? targetLocalZ, TestType generalTestType, ActionRequester requester)
    {
        TargetLocalZ = targetLocalZ;
        GeneralTestType = generalTestType;
        Requester = requester;
    }
}


public class UpdateUIButtonVisualsArgs : BaseActionArgs
{
    public string ButtonId { get; }
    public ButtonVisualStateType? VisualState { get; }
    public string ButtonText { get; }
    public EventType? NewEventType { get; }

    // Добавлен новый опциональный параметр newEventType со значением null по умолчанию.
    public UpdateUIButtonVisualsArgs(string buttonId, ButtonVisualStateType? visualState, string buttonText = null, EventType? newEventType = null)
    {
        if (string.IsNullOrEmpty(buttonId))
        {
            throw new ArgumentNullException(nameof(buttonId), "ButtonId не может быть null или пустым.");
        }

        ButtonId = buttonId;
        VisualState = visualState;
        ButtonText = buttonText;
        NewEventType = newEventType;
    }
}

public class ExtensometerControlArgs : BaseActionArgs
{
    /// Обязательное действие, которое необходимо выполнить.
    public ExtensometerAction Action { get; }

    // --- Опциональные параметры, используемые в зависимости от действия ---
    public Transform DrivePoint { get; }
    public Transform UndrivePoint { get; }
    public float? Elongation_mm { get; }
    public ExtensometerControlArgs(ExtensometerAction action, Transform drivePoint = null, Transform undrivePoint = null, float? elongation_mm = null)
    {
        Action = action;
        DrivePoint = drivePoint;
        UndrivePoint = undrivePoint;
        Elongation_mm = elongation_mm;
    }
}

/*public class NotifyReportExtensometerUsageArgs : BaseActionArgs { public bool WasUsed { get; }
    public NotifyReportExtensometerUsageArgs(bool wasUsed) { WasUsed = wasUsed; } }*/



/*public class PrepareGraphArgs : BaseActionArgs
{
    // Данные, специфичные для актуального запуска (от пользователя/SPC)
    public string TemplateName { get; }
    public SampleForm ShapeType { get; }
    public float ActualDiameterThickness { get; }
    public float ActualLength { get; }            // Фактическая длина образца (мм)
    public float ActualSpeed { get; }             // Фактическая скорость испытания (мм/мин)
    public TestSpeedMode SpeedMode { get; } // Режим скорости испытания (мм/мин или кН/сек)
    public float ActualWidth { get; }       // Фактическая ширина образца (мм), если применимо
    public float ActualArea { get; }              // Фактическая площадь сечения (мм^2)

    // Данные эталонного графика и материала (ИЗ MaterialPropertiesAsset)
    public TextAsset GraphDataTextFile { get; }       // Файл графика ("МПа / %")
    public float StandardInitialLengthMm { get; }   // Стандартная длина, для которой записан график (информационно)
    public float StandardInitialAreaMm2 { get; }    // Стандартная площадь, для которой записан график (информационно)
    public float ProportionalityLimitMPa { get; } // Предел пропорциональности (МПа) для событий ГОСТ
    public float RuptureStressThresholdMPa { get; } // Пороговое значение для события "Разрыв" (МПа)

    // Общая конфигурация теста (ИЗ TestConfigurationData)
    // Может понадобиться GraphController'у для каких-то общих настроек, не связанных с кривой материала.
    // Если не нужен GraphController'у, можно удалить отсюда.
    public TestConfigurationData GeneralTestConfig { get; }

    public PrepareGraphArgs(
        // Параметры от пользователя / SPC
        string templateName,
        SampleForm shapeType,
        float actualDiameterThickness,
        float actualLength,
        float actualSpeed,
        TestSpeedMode speedMode,
        float actualWidth,
        float actualArea,
        // Параметры, извлеченные из MaterialPropertiesAsset
        TextAsset graphDataTextFile,
        float standardInitialLengthMm,   // Из MaterialPropertiesAsset.standardInitialLength_mm
        float standardInitialAreaMm2,    // Из MaterialPropertiesAsset.standardInitialArea_mm2
        float proportionalityLimitMPa,   // Из MaterialPropertiesAsset.proportionalityLimit_MPa
        float ruptureStressThresholdMPa, // Из MaterialPropertiesAsset.ruptureStressThreshold_MPa
                                         // Общая конфигурация теста
        TestConfigurationData generalTestConfig)
    {
        // Валидация
        if (string.IsNullOrEmpty(templateName)) throw new ArgumentNullException(nameof(templateName));
        if (actualLength <= 0) throw new ArgumentOutOfRangeException(nameof(actualLength));
        if (actualSpeed <= 0) throw new ArgumentOutOfRangeException(nameof(actualSpeed));
        if (actualArea <= 0) throw new ArgumentOutOfRangeException(nameof(actualArea));
        if (graphDataTextFile == null) throw new ArgumentNullException(nameof(graphDataTextFile));
        if (standardInitialLengthMm <= 0) throw new ArgumentOutOfRangeException(nameof(standardInitialLengthMm));
        if (standardInitialAreaMm2 <= 0) throw new ArgumentOutOfRangeException(nameof(standardInitialAreaMm2));
        if (proportionalityLimitMPa < 0) throw new ArgumentOutOfRangeException(nameof(proportionalityLimitMPa)); // Может быть 0
        if (generalTestConfig == null) throw new ArgumentNullException(nameof(generalTestConfig)); // Если он всегда должен быть

        // Присвоение
        TemplateName = templateName;
        ShapeType = shapeType;
        ActualDiameterThickness = actualDiameterThickness;
        ActualLength = actualLength;
        ActualSpeed = actualSpeed;
        SpeedMode = speedMode;
        ActualWidth = actualWidth;
        ActualArea = actualArea;

        GraphDataTextFile = graphDataTextFile;
        StandardInitialLengthMm = standardInitialLengthMm;
        StandardInitialAreaMm2 = standardInitialAreaMm2;
        ProportionalityLimitMPa = proportionalityLimitMPa;
        RuptureStressThresholdMPa = ruptureStressThresholdMPa;

        GeneralTestConfig = generalTestConfig;
    }
}*/

public class SetDisplayModeArgs : BaseActionArgs {public string ModeText { get; }
    public SetDisplayModeArgs(string modeText)  { if (string.IsNullOrEmpty(modeText))  { throw new ArgumentNullException(nameof(modeText), "Текст режима не может быть пустым."); } ModeText = modeText; } }

public class SetVisibilityArgs : BaseActionArgs
{
    public bool IsVisible { get; } // Доступно только для чтения после создания

    public SetVisibilityArgs(bool isVisible)
    {
        IsVisible = isVisible;
    }
}

public class SetGlobalModeButtonsVisibilityArgs : BaseActionArgs { public bool ShowMenuButton { get; set; } public bool ShowHomeButton { get; set; }
    public SetGlobalModeButtonsVisibilityArgs(bool showMenuButton = false, bool showHomeButton = false) { ShowMenuButton = showMenuButton; ShowHomeButton = showHomeButton; } }

    public class ControlLoaderArgs : BaseActionArgs 
{ 
    public Direction MoveDirection { get; }
    public SpeedType MoveSpeed { get; }
    public bool IsStopCommand { get; }

    // Конструктор для движения
    public ControlLoaderArgs(Direction direction, SpeedType speed) 
    { 
        MoveDirection = direction; 
        MoveSpeed = speed; 
        IsStopCommand = false;
    }
    
    // Конструктор для остановки
    public ControlLoaderArgs(bool stop)
    {
        IsStopCommand = true;
        MoveDirection = default; // не важно
        MoveSpeed = default;     // не важно
    }
}

public class SetSupportSystemStateArgs : BaseActionArgs
{
    public bool Activate { get; }
    public SetSupportSystemStateArgs(bool activate)
    {
        Activate = activate;
    }
}

