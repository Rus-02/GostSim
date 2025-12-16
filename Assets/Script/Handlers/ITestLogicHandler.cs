using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

// --- НОВАЯ СИСТЕМА СЦЕНАРИЕВ ---

// "Словарь" высокоуровневых советов, которые хендлер может дать CSM.
public enum HandlerAdvisedAction
{
    CreateSample, RemoveSample,
    ClampUpperGrip, UnclampUpperGrip, ClampLowerGrip, UnclampLowerGrip,
    SetState, SetUnloadedFlag,
    ShowHint, UpdateSampleButtonText,
    Play_In_Animation,
    Play_Out_Animation,
    Play_SampleInstall_Animation,
    Play_SampleRemove_Animation,
    ReinitializeFixtureZones,
    SetDoorState,
}

// Вспомогательный enum для указания захвата
public enum GripType { Upper, Lower }

// "Бланк" для одного шага сценария.
public class ScenarioStep
{
    public HandlerAdvisedAction Action { get; }
    public object Argument { get; }

    public ScenarioStep(HandlerAdvisedAction action, object argument = null)
    {
        Action = action;
        Argument = argument;
    }
}

// Контекст, который CSM передает хендлеру.
public struct LogicHandlerContext
{
    public TestState CurrentState;
    public bool IsUpperGripClamped;
    public bool IsLowerGripClamped;
    public bool IsSampleUnloaded;
    public bool IsSamplePresent;
    public float CurrentDistance;
    public float RequiredSampleLength;
    public IReadOnlyDictionary<FixtureZone, string> InstalledFixtures; 
}

// Новый, чистый интерфейс "Советника".
public interface IScenarioProvider
{
    List<ScenarioStep> GetOnSampleButtonPress_Scenario(LogicHandlerContext context);
    List<ScenarioStep> GetOnClampGripPress_Scenario(GripType grip, LogicHandlerContext context);
    List<ScenarioStep> GetOnUnclampGripPress_Scenario(GripType grip, LogicHandlerContext context);
    List<ScenarioStep> GetOnUnloadSamplePress_Scenario(LogicHandlerContext context);
}

// --- ВСПОМОГАТЕЛЬНЫЕ КЛАССЫ И СТРУКТУРЫ (которые были случайно удалены) ---

public class InternalFixturePlanItem
{
    public string FixtureId { get; set; }
    public string ParentFixtureId { get; set; }
    public string AttachmentPointName { get; set; }
}

public class FixtureChangePlan
{
    public List<string> MainFixturesToRemove { get; set; } = new List<string>();

    public class FixtureInstallationInfo
    {
        public string FixtureId { get; set; }
        public bool UseAnimation { get; set; }
    }
    public List<FixtureInstallationInfo> MainFixturesToInstall { get; set; } = new List<FixtureInstallationInfo>();
    public List<ToDoManagerCommand> InterstitialCommands { get; set; } = new List<ToDoManagerCommand>();

    public List<InternalFixturePlanItem> InternalFixturesToInstall { get; set; } = new List<InternalFixturePlanItem>();
    
    public List<string> FixturesToPreInitialize { get; set; } = new List<string>(); 
}



// --- СТАРЫЕ СТРУКТУРЫ, КОТОРЫЕ ВСЕ ЕЩЕ НУЖНЫ ---

public enum ApproachGuidanceParameter { SampleLength, SampleWidth, SampleDiameterThickness, CustomValue }
public enum ApproachActionType { SetDistance, ReduceToDistance }

public struct ApproachGuidanceOutput
{
    public ApproachGuidanceParameter DeterminingParameter;
    public float CustomDistanceValue_mm;
    public ApproachActionType ActionType;
}

public struct SampleUIFieldConfig
{
    public string ParameterName;
    public string LabelText;
    public bool IsVisible;
    public bool IsDropdown;
    public List<float> StandardValues;
    public string StandardDisplayFormat;
    public float DefaultValue;
    public float MinConstraint;
    public float MaxConstraint;
    public bool HasSpeedModeSelector; // Показывать ли выбор мм/мин | кН/с
    public TestSpeedMode DefaultSpeedMode;  // Режим скорости по умолчанию
}

public struct SampleUIConfiguration
{
    public List<SampleUIFieldConfig> Fields;
    public string DiameterThicknessLabelOverride;
    public bool IsWidthFieldRelevant;
}

public interface ITestLogicHandler : IScenarioProvider
{
    ApproachGuidanceOutput GetApproachGuidance(TestConfigurationData testConfig);
    void UpdateSampleVisuals(Transform sampleTransform, float currentGraphRelativeDeformationPercent, MaterialPropertiesAsset materialProps, Vector3 initialSampleScale, Animator sampleAnimator, SampleBehaviorHandler sampleBehaviorHandler, TestConfigurationData testConfig, float actualSampleLength, ref bool isVisuallyRuptured, ref bool isYieldPointReachedForNeckingAnim);
    bool ShouldTriggerNeckingAnimation(float currentGraphRelativeDeformationPercent, MaterialPropertiesAsset materialProps, float ultimateStrength_X_Percent_FromGraph, bool isNeckingAnimationAlreadyStarted);
    IEnumerator SetupTestSpecificFixtures(TestConfigurationData testConfig, FixtureManager fm, ToDoManager tm);
    SampleUIConfiguration GetSampleParametersUIConfig(TestConfigurationData testConfig, SampleData selectedSampleData, MaterialPropertiesAsset selectedMaterialProps);
    Dictionary<string, string> ValidateSampleParameters(Dictionary<string, float> currentDimensionValues, SampleForm selectedShape, TestConfigurationData testConfig, SampleData sampleData, MaterialPropertiesAsset materialProps, float minAllowedSpeed, float maxAllowedSpeed, TestSpeedMode speedMode);
    float CalculateCrossSectionalArea(Dictionary<string, float> currentDimensionValues, SampleForm selectedShape, SampleData sampleData);
    float GetPistonDisplacementFromGraphValue(float currentGraphRelativeDeformationPercent, float actualSampleLength, TestConfigurationData testConfig);
    (float x_proportionalityLimit_percent, float x_ultimateStrength_percent, float x_rupture_percent) GetKeyPointsX_FromMaterialGraph(MaterialPropertiesAsset materialProps, TestConfigurationData testConfig, float ruptureThreshold_MPa);
    FixtureChangePlan CreateFixtureChangePlan(TestConfigurationData targetConfig, SampleForm shape, List<string> currentlyInstalledFixtures);    List<ActionType> GetClampActions();
    List<ActionType> GetUnclampActions();
    List<ToDoManagerCommand> GetPostChangeFinalizationCommands();
    List<ToDoManagerCommand> GetPreChangePreparationCommands(List<string> fixturesToRemove);
    List<string> CreateTeardownPlan(List<string> fixturesScheduledForRemoval);

}