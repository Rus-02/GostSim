using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class DefaultLogicHandler : ITestLogicHandler
{
    protected TestConfigurationData _testConfig;
    protected readonly SystemStateMonitor _monitor;

    public DefaultLogicHandler(TestConfigurationData config)
    {
        _testConfig = config;
        _monitor = SystemStateMonitor.Instance;
        if (_monitor == null) { Debug.LogError("[DefaultLogicHandler] SystemStateMonitor.Instance is null! Логика может работать некорректно."); }
    }

    public virtual ApproachGuidanceOutput GetApproachGuidance( TestConfigurationData testConfig)
    {
        Debug.LogWarning($"[DefaultLogicHandler] GetApproachGuidance не реализован для {testConfig?.name ?? "NULL Config"}. Возвращено значение по умолчанию (длина, установка расстояния).");
        return new ApproachGuidanceOutput
        {
            DeterminingParameter = ApproachGuidanceParameter.SampleLength,
            CustomDistanceValue_mm = 0,
            ActionType = ApproachActionType.SetDistance
        };
    }
        
    public virtual void UpdateSampleVisuals(
        Transform sampleTransform,
        float currentGraphRelativeDeformationPercent,
        MaterialPropertiesAsset materialProps,
        Vector3 initialSampleScale,
        Animator sampleAnimator,
        SampleBehaviorHandler sampleBehaviorHandler,
        TestConfigurationData testConfig,
        float actualSampleLength,
        ref bool isVisuallyRuptured,
        ref bool isYieldPointReachedForNeckingAnim)
    {
        if (sampleTransform == null || testConfig == null || materialProps == null)
        {
            Debug.LogWarning("[DefaultLogicHandler.UpdateSampleVisuals] sampleTransform, testConfig, or materialProps is null.");
            return;
        }

        bool shouldStopScaling = isVisuallyRuptured;

        if (actualSampleLength > Mathf.Epsilon && !shouldStopScaling)
        {
            float absoluteDisplacement_mm = (currentGraphRelativeDeformationPercent / 100f) * actualSampleLength;

            float scaleMultiplierY = 1.0f;
            if (testConfig.testType == TestType.Tensile)
            {
                scaleMultiplierY = (actualSampleLength + absoluteDisplacement_mm) / actualSampleLength;
            }
            else if (testConfig.testType == TestType.Compression)
            {
                scaleMultiplierY = (actualSampleLength - absoluteDisplacement_mm) / actualSampleLength;
            }

            float newScaleY = initialSampleScale.y * scaleMultiplierY;

            float minSampleScaleFactorY = 0.05f;
            float maxSampleScaleFactorY = (testConfig.testType == TestType.Tensile) ? (1.0f + (materialProps.elongationAtBreak_Percent / 100f) * 1.5f) : 2.0f;

            newScaleY = Mathf.Clamp(newScaleY, initialSampleScale.y * minSampleScaleFactorY, initialSampleScale.y * maxSampleScaleFactorY);
            sampleTransform.localScale = new Vector3(initialSampleScale.x, newScaleY, initialSampleScale.z);
        }
    }

    public virtual bool ShouldTriggerNeckingAnimation(
        float currentGraphRelativeDeformationPercent,
        MaterialPropertiesAsset materialProps,
        float ultimateStrength_X_Percent_FromGraph,
        bool isNeckingAnimationAlreadyStarted)
    {
        if (materialProps == null)
        {
            Debug.LogError("[DefaultLogicHandler] materialProps is null in ShouldTriggerNeckingAnimation.");
            return false;
        }
        return currentGraphRelativeDeformationPercent >= ultimateStrength_X_Percent_FromGraph && !isNeckingAnimationAlreadyStarted;
    }

    public virtual IEnumerator SetupTestSpecificFixtures(
        TestConfigurationData testConfig,
        FixtureManager fixtureManager,
        ToDoManager toDoManager)
    {
        yield break;
    }

    public virtual SampleUIConfiguration GetSampleParametersUIConfig(
        TestConfigurationData testConfig,
        SampleData selectedSampleData,
        MaterialPropertiesAsset selectedMaterialProps
    )
    {
        if (selectedSampleData == null)
        {
            Debug.LogWarning($"[DefaultLogicHandler] selectedSampleData is null for GetSampleParametersUIConfig. TestConfig: {testConfig?.name}");
            return new SampleUIConfiguration { Fields = new List<SampleUIFieldConfig>() };
        }
        var fields = new List<SampleUIFieldConfig>();

        if (testConfig != null)
        {
            fields.Add(new SampleUIFieldConfig
        {
            ParameterName = "Speed",
            LabelText = "Скорость", // <-- Теперь универсальный текст
            IsVisible = true,
            IsDropdown = false,
            DefaultValue = testConfig.testMoveSpeed,
            MinConstraint = 0.001f,
            MaxConstraint = 1000f,
            HasSpeedModeSelector = true, // <-- Сообщаем UI, что нужен селектор
            DefaultSpeedMode = TestSpeedMode.DeformationRate // <-- Режим по умолчанию (мм/мин)
        });

        }

        return new SampleUIConfiguration
        {
            Fields = fields,
            DiameterThicknessLabelOverride = selectedSampleData.sampleForm == SampleForm.Круг ? "Диаметр, мм" : "Толщина, мм",
            IsWidthFieldRelevant = selectedSampleData.sampleForm != SampleForm.Круг
        };
    }

    public virtual Dictionary<string, string> ValidateSampleParameters(
        Dictionary<string, float> currentDimensionValues,
        SampleForm selectedShape,
        TestConfigurationData testConfig,
        SampleData sampleData,
        MaterialPropertiesAsset materialProps,
        float minAllowedSpeed,
        float maxAllowedSpeed,
        TestSpeedMode speedMode)
    {
        var errors = new Dictionary<string, string>();
        if (materialProps == null)
        {
            errors["General"] = "Ошибка: Данные материала не загружены.";
            return errors;
        }

        if (currentDimensionValues.TryGetValue("Speed", out float speedValue))
        {
            if (float.IsNaN(speedValue) || speedValue <= 0) errors["Speed"] = "Скорость должна быть > 0.";
            float effectiveMaxSpeed = (testConfig != null) ? Mathf.Min(maxAllowedSpeed, testConfig.testMoveSpeed * 2f) : maxAllowedSpeed;
            float effectiveMinSpeed = (testConfig != null) ? Mathf.Max(minAllowedSpeed, testConfig.testMoveSpeed * 0.5f) : minAllowedSpeed;

            if (speedValue < effectiveMinSpeed || speedValue > effectiveMaxSpeed)
            {
                string speedUnit = (speedMode == TestSpeedMode.ForceRate) ? "кН/с" : "мм/мин";
                errors["Speed"] = $"Скорость ({speedValue:F1}) вне доп. диапазона ({effectiveMinSpeed:F1} - {effectiveMaxSpeed:F1} {speedUnit}).";
            }
        }
        else
        {
            errors["Speed"] = "Скорость не указана.";
        }
        return errors;
    }

    public virtual float CalculateCrossSectionalArea(
        Dictionary<string, float> currentDimensionValues,
        SampleForm selectedShape,
        SampleData sampleData)
    {
        currentDimensionValues.TryGetValue("DiameterThickness", out float dtValue);
        if (float.IsNaN(dtValue) || dtValue <= 0) return 0f;

        if (selectedShape == SampleForm.Круг)
        {
            float radius = dtValue / 2f;
            return Mathf.PI * radius * radius;
        }
        else
        {
            float widthValueToUse;
            if (sampleData != null &&
                (selectedShape == SampleForm.Квадрат ||
                 (sampleData.widthSetting?.linkMode == DimensionLinkMode.FollowDiameterThickness && selectedShape != SampleForm.Круг)))
            {
                widthValueToUse = dtValue;
            }
            else if (sampleData?.widthSetting?.linkMode == DimensionLinkMode.Master)
            {
                if (!currentDimensionValues.TryGetValue("Width", out widthValueToUse) || float.IsNaN(widthValueToUse) || widthValueToUse <= 0)
                {
                    return 0f;
                }
            }
            else if (selectedShape == SampleForm.Круг) { return 0f; }
            else
            {
                return 0f;
            }
            if (widthValueToUse <= 0) return 0f;
            return dtValue * widthValueToUse;
        }
    }

    public virtual float GetPistonDisplacementFromGraphValue(
        float currentGraphRelativeDeformationPercent,
        float actualSampleLength,
        TestConfigurationData testConfig)
    {
        if (actualSampleLength <= 0) return 0f;
        float absoluteDisplacement_mm = (currentGraphRelativeDeformationPercent / 100f) * actualSampleLength;
        return absoluteDisplacement_mm / 1000.0f;
    }

    public virtual (
        float x_proportionalityLimit_percent,
        float x_ultimateStrength_percent,
        float x_rupture_percent
    ) GetKeyPointsX_FromMaterialGraph(
        MaterialPropertiesAsset materialProps,
        TestConfigurationData testConfig,
        float ruptureThreshold_MPa)
    {
        if (materialProps == null)
        {
            Debug.LogError("[DefaultLogicHandler] materialProps is null in GetKeyPointsX_FromMaterialGraph.");
            return (0f, 0f, 0f);
        }

        float placeholder_X_for_proportionality = 0f;
        float placeholder_X_for_ultimate = 0f;
        float placeholder_X_for_rupture = 0f;

        if (materialProps.modulusOfElasticityE_MPa > 0)
        {
            placeholder_X_for_proportionality = (materialProps.proportionalityLimit_MPa / materialProps.modulusOfElasticityE_MPa) * 100f;
        }
        placeholder_X_for_rupture = materialProps.elongationAtBreak_Percent;
        placeholder_X_for_ultimate = Mathf.Max(0, materialProps.elongationAtBreak_Percent - 5.0f);

        return (placeholder_X_for_proportionality, placeholder_X_for_ultimate, placeholder_X_for_rupture);
    }

    public virtual FixtureChangePlan CreateFixtureChangePlan(TestConfigurationData targetConfig, SampleForm shape, List<string> currentlyInstalledFixtures)
    {
        var plan = new FixtureChangePlan();
        
        // --- ВАША ОРИГИНАЛЬНАЯ ЛОГИКА ОСТАЕТСЯ БЕЗ ИЗМЕНЕНИЙ ---
        
        if (targetConfig == null || targetConfig.potentialFixtureIDs == null)
        {
            // Если нет информации о требуемой оснастке, планируем снять всё.
            plan.MainFixturesToRemove.AddRange(currentlyInstalledFixtures);
            return plan;
        }

        var fm = FixtureManager.Instance;
        if (fm == null) return plan;

        // Список requiredFixtures берется напрямую из конфига, без фильтрации по размеру.
        List<string> requiredFixtures = new List<string>(targetConfig.potentialFixtureIDs);
        List<string> mainTargetIDs = new List<string>();
        List<InternalFixturePlanItem> internalInstallPlan = new List<InternalFixturePlanItem>();

        foreach (var id in requiredFixtures)
        {
            var data = fm.GetFixtureData(id);
            if (data == null) continue;
            if (data.placementSource == SamplePlacementSource.FixedOnMachine)
            {
                mainTargetIDs.Add(id);
            }
            else
            {
                internalInstallPlan.Add(new InternalFixturePlanItem
                {
                    FixtureId = data.fixtureId,
                    ParentFixtureId = data.parentFixtureId,
                    AttachmentPointName = data.parentAttachmentPointName
                });
            }
        }
        plan.InternalFixturesToInstall = internalInstallPlan;

        var targetFixturesByZone = new Dictionary<FixtureZone, string>();
        foreach (string targetId in mainTargetIDs)
        {
            var data = fm.GetFixtureData(targetId);
            if (data != null && !targetFixturesByZone.ContainsKey(data.fixtureZone))
            {
                targetFixturesByZone.Add(data.fixtureZone, targetId);
            }
        }

        foreach (var installedId in currentlyInstalledFixtures)
        {
            var installedData = fm.GetFixtureData(installedId);
            if (installedData != null && installedData.isSpecializedEquipment && !requiredFixtures.Contains(installedId))
            {
                plan.MainFixturesToRemove.Add(installedId);
            }
        }

        foreach (var installedId in currentlyInstalledFixtures)
        {
            if (plan.MainFixturesToRemove.Contains(installedId)) continue;

            var installedData = fm.GetFixtureData(installedId);
            if (installedData == null) continue;

            if (targetFixturesByZone.TryGetValue(installedData.fixtureZone, out string requiredIdInThisZone))
            {
                if (installedId != requiredIdInThisZone)
                {
                    plan.MainFixturesToRemove.Add(installedId);
                }
            }
        }

        foreach (var requiredPair in targetFixturesByZone)
        {
            if (!currentlyInstalledFixtures.Contains(requiredPair.Value))
            {
                plan.MainFixturesToInstall.Add(new FixtureChangePlan.FixtureInstallationInfo
                {
                    FixtureId = requiredPair.Value,
                    UseAnimation = true
                });
            }
        }
        
        return plan;
    }

    public virtual List<ActionType> GetClampActions()
    {
        var actions = new List<ActionType>();
        if (_testConfig != null)
        {
            if (_testConfig.requiresUpperClamp) actions.Add(ActionType.ClampUpperGrip);
            if (_testConfig.requiresLowerClamp) actions.Add(ActionType.ClampLowerGrip);
        }
        return actions;
    }

    public virtual List<ActionType> GetUnclampActions()
    {
        var actions = new List<ActionType>();
        if (_testConfig != null)
        {
            if (_testConfig.requiresUpperClamp) actions.Add(ActionType.UnclampUpperGrip);
            if (_testConfig.requiresLowerClamp) actions.Add(ActionType.UnclampLowerGrip);
        }
        return actions;
    }

    public virtual List<ToDoManagerCommand> GetPostChangeFinalizationCommands() { return new List<ToDoManagerCommand>(); }
    public virtual List<ToDoManagerCommand> GetPreChangePreparationCommands(List<string> fixturesToRemove) { return new List<ToDoManagerCommand>(); }

    // Добавляем пустые виртуальные реализации методов из IScenarioProvider.
    #region IScenarioProvider default implementation

    public virtual List<ScenarioStep> GetOnSampleButtonPress_Scenario(LogicHandlerContext context) { return new List<ScenarioStep>(); }
    public virtual List<ScenarioStep> GetOnClampGripPress_Scenario(GripType grip, LogicHandlerContext context) { return new List<ScenarioStep>(); }
    public virtual List<ScenarioStep> GetOnUnclampGripPress_Scenario(GripType grip, LogicHandlerContext context) { return new List<ScenarioStep>(); }
    public virtual List<ScenarioStep> GetOnUnloadSamplePress_Scenario(LogicHandlerContext context) { return new List<ScenarioStep>(); }
    public virtual List<string> CreateTeardownPlan(List<string> fixturesScheduledForRemoval) { return fixturesScheduledForRemoval; }

    #endregion
}