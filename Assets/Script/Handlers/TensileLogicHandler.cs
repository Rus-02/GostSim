using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

public class TensileLogicHandler : DefaultLogicHandler, IScenarioProvider
{
    public TensileLogicHandler(TestConfigurationData config) : base(config) { }

    public override ApproachGuidanceOutput GetApproachGuidance(
        TestConfigurationData testConfig)
    {
        // Для растяжения обычно устанавливаем расстояние по длине образца
        return new ApproachGuidanceOutput
        {
            DeterminingParameter = ApproachGuidanceParameter.SampleLength,
            CustomDistanceValue_mm = 0,
            ActionType = ApproachActionType.SetDistance
        };
    }

    public override void UpdateSampleVisuals(
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
        if (sampleTransform == null || testConfig == null)
        {
            Debug.LogWarning("[TensileLogicHandler.UpdateSampleVisuals] sampleTransform or testConfig is null.");
            return;
        }

        // Проверяем, не разрушен ли образец визуально и есть ли смысл его масштабировать
        bool shouldStopScaling = isVisuallyRuptured;

        if (actualSampleLength > Mathf.Epsilon && !shouldStopScaling)
        {
            float absoluteDisplacement_mm = (currentGraphRelativeDeformationPercent / 100f) * actualSampleLength;
            float relativeDisplacement = absoluteDisplacement_mm / actualSampleLength * 0.5f; // Относительное удлинение

            // Для растяжения масштаб по Y увеличивается
            float scaleMultiplier = 1.0f + relativeDisplacement;
            float newScaleY = initialSampleScale.y * scaleMultiplier;

            // Ограничение масштаба
            float minSampleScaleFactorY = 0.05f;
            float maxSampleScaleFactorY = 100f;

            float minYScale = initialSampleScale.y * minSampleScaleFactorY;
            float maxYScale = initialSampleScale.y * maxSampleScaleFactorY;

            newScaleY = Mathf.Clamp(newScaleY, minYScale, maxYScale);

            // Масштабирование по остальным осям (Пуассоновское сужение) - опционально
            float poissonRatio = 0.3f; // Примерное значение коэффициента Пуассона
            float transverseScaleMultiplier = 1.0f - (relativeDisplacement * poissonRatio);
            transverseScaleMultiplier = Mathf.Max(transverseScaleMultiplier, 0.01f); // Не даем схлопнуться полностью

            float newScaleX = initialSampleScale.x * transverseScaleMultiplier;
            float newScaleZ = initialSampleScale.z * transverseScaleMultiplier;


            sampleTransform.localScale = new Vector3(newScaleX, newScaleY, newScaleZ);
        }
    }

    public override SampleUIConfiguration GetSampleParametersUIConfig(TestConfigurationData testConfig, SampleData selectedSampleData, MaterialPropertiesAsset selectedMaterialProps)
    {
        if (selectedSampleData == null)
        {
            Debug.LogWarning($"[TensileLogicHandler] selectedSampleData is null for GetSampleParametersUIConfig. TestConfig: {testConfig?.name}");
            return new SampleUIConfiguration { Fields = new List<SampleUIFieldConfig>() };
        }
        if (testConfig == null)
        {
            Debug.LogWarning($"[TensileLogicHandler] testConfig is null. SampleData: {selectedSampleData.sampleId}");
            return new SampleUIConfiguration { Fields = new List<SampleUIFieldConfig>() };
        }

        var uiConfig = new SampleUIConfiguration
        {
            Fields = new List<SampleUIFieldConfig>(),
            DiameterThicknessLabelOverride = selectedSampleData.sampleForm == SampleForm.Круг ? "Диаметр, мм" : "Толщина, мм",
            IsWidthFieldRelevant = selectedSampleData.sampleForm != SampleForm.Круг
        };

        // 1. Параметр: Диаметр/Толщина (DiameterThickness)
        if (selectedSampleData.diameterThicknessSetting != null)
        {
            var dtSetting = selectedSampleData.diameterThicknessSetting;
            List<float> availableStandardDtValues = new List<float>(dtSetting.standardValues ?? new List<float>());
            float currentDefaultDt = dtSetting.defaultValue;

            float minFixtureRange = 0.001f;
            float maxFixtureRange = 10000f;
            bool fixtureRangeApplied = false;

            if (testConfig.potentialFixtureIDs != null && testConfig.potentialFixtureIDs.Count > 0)
            {
                float currentMinOverall = float.MaxValue;
                float currentMaxOverall = float.MinValue;

                foreach (string fixtureId in testConfig.potentialFixtureIDs)
                {
                    FixtureData fixtureData = FixtureManager.Instance?.GetFixtureData(fixtureId);
                    if (fixtureData is IClampRangeProvider rangeProvider)
                    {
                        currentMinOverall = Mathf.Min(currentMinOverall, rangeProvider.MinGripDimension);
                        currentMaxOverall = Mathf.Max(currentMaxOverall, rangeProvider.MaxGripDimension);
                        fixtureRangeApplied = true;
                    }
                }
                if (fixtureRangeApplied)
                {
                    minFixtureRange = currentMinOverall;
                    maxFixtureRange = currentMaxOverall;
                }
            }

            if (dtSetting.inputMode == SingleDimensionInputMode.SelectStandard && availableStandardDtValues.Count > 0)
            {
                var originalCount = availableStandardDtValues.Count;
                availableStandardDtValues = availableStandardDtValues
                                            .Where(val => val >= minFixtureRange && val <= maxFixtureRange)
                                            .ToList();
                if (originalCount > 0 && availableStandardDtValues.Count == 0)
                {
                    Debug.LogWarning($"[TensileLogicHandler] Для D/T не осталось стандартных значений после фильтрации оснасткой [{minFixtureRange}-{maxFixtureRange}].");
                }

                if (!availableStandardDtValues.Contains(currentDefaultDt) && availableStandardDtValues.Count > 0)
                {
                    currentDefaultDt = availableStandardDtValues[0];
                }
            }

            uiConfig.Fields.Add(new SampleUIFieldConfig
            {
                ParameterName = "DiameterThickness",
                LabelText = uiConfig.DiameterThicknessLabelOverride,
                IsVisible = true,
                IsDropdown = dtSetting.inputMode == SingleDimensionInputMode.SelectStandard && availableStandardDtValues.Any(),
                StandardValues = availableStandardDtValues,
                StandardDisplayFormat = dtSetting.standardDisplayFormat,
                DefaultValue = currentDefaultDt,
                MinConstraint = Mathf.Max(dtSetting.minConstraint, fixtureRangeApplied ? minFixtureRange : dtSetting.minConstraint),
                MaxConstraint = Mathf.Min(dtSetting.maxConstraint, fixtureRangeApplied ? maxFixtureRange : dtSetting.maxConstraint)
            });
        }

        // 2. Параметр: Ширина (Width)
        bool showWidthField = selectedSampleData.sampleForm != SampleForm.Круг &&
                              selectedSampleData.widthSetting?.linkMode == DimensionLinkMode.Master;

        if (selectedSampleData.widthSetting != null)
        {
            var wSetting = selectedSampleData.widthSetting;
            uiConfig.Fields.Add(new SampleUIFieldConfig
            {
                ParameterName = "Width",
                LabelText = "Ширина, мм",
                IsVisible = showWidthField,
                IsDropdown = wSetting.inputMode == SingleDimensionInputMode.SelectStandard,
                StandardValues = new List<float>(wSetting.standardValues ?? new List<float>()),
                StandardDisplayFormat = wSetting.standardDisplayFormat,
                DefaultValue = wSetting.defaultValue,
                MinConstraint = wSetting.minConstraint,
                MaxConstraint = wSetting.maxConstraint
            });
        }

        // 3. Параметр: Рабочая длина (Length)
        if (selectedSampleData.lengthSetting != null)
        {
            var lSetting = selectedSampleData.lengthSetting;
            uiConfig.Fields.Add(new SampleUIFieldConfig
            {
                ParameterName = "Length",
                LabelText = "Рабочая длина, мм",
                IsVisible = true,
                IsDropdown = lSetting.inputMode == SingleDimensionInputMode.SelectStandard,
                StandardValues = new List<float>(lSetting.standardValues ?? new List<float>()),
                StandardDisplayFormat = lSetting.standardDisplayFormat,
                DefaultValue = lSetting.defaultValue,
                MinConstraint = lSetting.minConstraint,
                MaxConstraint = lSetting.maxConstraint
            });
        }
        else
        {
            Debug.LogWarning($"[TensileLogicHandler] selectedSampleData.lengthSetting is null для {selectedSampleData.sampleId}. Поле длины не будет сконфигурировано.");
        }

        // 4. Параметр: Скорость (Speed)
        uiConfig.Fields.Add(new SampleUIFieldConfig
        {
            ParameterName = "Speed",
            LabelText = "Скорость",
            IsVisible = true,
            IsDropdown = false,
            DefaultValue = testConfig.testMoveSpeed,
            MinConstraint = 0.001f,
            MaxConstraint = 1000f,
            HasSpeedModeSelector = true,
            DefaultSpeedMode = TestSpeedMode.DeformationRate 
        });

        return uiConfig;
    }

    public override Dictionary<string, string> ValidateSampleParameters(
        Dictionary<string, float> currentDimensionValues,
        SampleForm selectedShape,
        TestConfigurationData testConfig,
        SampleData sampleData,
        MaterialPropertiesAsset materialProps,
        float minAllowedSpeedUser,
        float maxAllowedSpeedUser,
        TestSpeedMode speedMode
    )
    {
        var errors = new Dictionary<string, string>();
        if (sampleData == null) { errors["General"] = "Данные образца не найдены."; return errors; }
        if (testConfig == null) { errors["General"] = "Данные конфигурации теста не найдены."; return errors; }

        // --- Валидация Диаметра/Толщины ---
        float minFixtureDt = 0.001f, maxFixtureDt = 10000f;
        bool fixtureRangeKnown = false;
        if (testConfig.potentialFixtureIDs != null && testConfig.potentialFixtureIDs.Count > 0)
        {
            // (Логика определения minFixtureDt, maxFixtureDt по оснастке - без изменений)
            float currentMinOverall = float.MaxValue; float currentMaxOverall = float.MinValue;
            foreach (string fixtureId in testConfig.potentialFixtureIDs)
            {
                FixtureData fixtureData = FixtureManager.Instance?.GetFixtureData(fixtureId);
                if (fixtureData is IClampRangeProvider rangeProvider)
                {
                    currentMinOverall = Mathf.Min(currentMinOverall, rangeProvider.MinGripDimension);
                    currentMaxOverall = Mathf.Max(currentMaxOverall, rangeProvider.MaxGripDimension);
                    fixtureRangeKnown = true;
                }
            }
            if (fixtureRangeKnown) { minFixtureDt = currentMinOverall; maxFixtureDt = currentMaxOverall; }
        }

        if (currentDimensionValues.TryGetValue("DiameterThickness", out float dtValue))
        {
            if (float.IsNaN(dtValue) || dtValue <= 0) errors["DiameterThickness"] = "Значение должно быть полож. числом.";
            else if (sampleData.diameterThicknessSetting != null)
            {
                float sampleMin = sampleData.diameterThicknessSetting.minConstraint;
                float sampleMax = sampleData.diameterThicknessSetting.maxConstraint;
                float effectiveMin = Mathf.Max(sampleMin, fixtureRangeKnown ? minFixtureDt : sampleMin);
                float effectiveMax = Mathf.Min(sampleMax, fixtureRangeKnown ? maxFixtureDt : sampleMax);
                if (dtValue < effectiveMin || dtValue > effectiveMax)
                {
                    errors["DiameterThickness"] = $"Значение вне доп. диапазона ({effectiveMin:F2} - {effectiveMax:F2} мм).";
                }
            }
        }
        else errors["DiameterThickness"] = "Значение не определено.";

        // --- Валидация Ширины ---
        if (selectedShape != SampleForm.Круг && sampleData.widthSetting?.linkMode == DimensionLinkMode.Master)
        {
            if (currentDimensionValues.TryGetValue("Width", out float wValue))
            {
                if (float.IsNaN(wValue) || wValue <= 0) errors["Width"] = "Значение должно быть полож. числом.";
                else if (sampleData.widthSetting != null && (wValue < sampleData.widthSetting.minConstraint || wValue > sampleData.widthSetting.maxConstraint))
                {
                    errors["Width"] = $"Значение вне доп. диапазона ({sampleData.widthSetting.minConstraint:F2} - {sampleData.widthSetting.maxConstraint:F2} мм).";
                }
            }
            else errors["Width"] = "Значение не определено.";
        }

        // --- Валидация Длины ---
        if (currentDimensionValues.TryGetValue("Length", out float lValue))
        {
            if (float.IsNaN(lValue) || lValue <= 0) errors["Length"] = "Длина должна быть полож. числом.";
            else if (sampleData.lengthSetting != null && (lValue < sampleData.lengthSetting.minConstraint || lValue > sampleData.lengthSetting.maxConstraint))
            {
                // Здесь можно добавить проверку на максимальную длину, которую позволяет оснастка, если это релевантно для растяжения.
                errors["Length"] = $"Длина вне доп. диапазона ({sampleData.lengthSetting.minConstraint:F2} - {sampleData.lengthSetting.maxConstraint:F2} мм).";
            }
        }
        else errors["Length"] = "Длина не определена.";

        // --- Валидация Скорости ---
        if (currentDimensionValues.TryGetValue("Speed", out float speedValue))
        {
            if (float.IsNaN(speedValue) || speedValue <= 0) errors["Speed"] = "Скорость должна быть полож. числом.";
            else if (speedValue < minAllowedSpeedUser || speedValue > maxAllowedSpeedUser)
            {
                string speedUnit = (speedMode == TestSpeedMode.ForceRate) ? "кН/с" : "мм/мин";
                errors["Speed"] = $"Скорость вне доп. диапазона ({minAllowedSpeedUser:F1} - {maxAllowedSpeedUser:F1} мм/мин).";
            }
        }
        else errors["Speed"] = "Скорость не определена.";

        return errors;
    }

    public override FixtureChangePlan CreateFixtureChangePlan(TestConfigurationData targetConfig, SampleForm shape, List<string> currentlyInstalledFixtures)
    {
        var fm = FixtureManager.Instance;
        if (fm == null || _monitor == null) return new FixtureChangePlan();

        var requiredFixtures = new List<string>(targetConfig.potentialFixtureIDs ?? new List<string>());
        bool needsInserts = requiredFixtures.Any(id => fm.GetFixtureData(id) is HydraulicInsertData);

        if (needsInserts)
        {
            _monitor.CurrentSampleParameters.TryGetValue("DiameterThickness", out float sampleDimension);

            var suitableInserts = targetConfig.potentialFixtureIDs
                .Select(id => fm.GetFixtureData(id))
                .OfType<HydraulicInsertData>()
                .Where(data => data != null && sampleDimension >= data.MinGripDimension && sampleDimension <= data.MaxGripDimension)
                .ToList();

            var finalRequiredFixtures = requiredFixtures
                .Where(id => !(fm.GetFixtureData(id) is HydraulicInsertData))
                .ToList();
            
            finalRequiredFixtures.AddRange(suitableInserts.Select(data => data.fixtureId));
            requiredFixtures = finalRequiredFixtures;
        }

        // Создаем временный конфиг, как и раньше
        var tempConfig = ScriptableObject.CreateInstance<TestConfigurationData>();
        tempConfig.potentialFixtureIDs = requiredFixtures;

        // Вызываем базовый метод, который мы уже исправили
        var plan = base.CreateFixtureChangePlan(tempConfig, shape, currentlyInstalledFixtures);

        Object.Destroy(tempConfig);
        return plan;
    }

    #region IScenarioProvider Implementation

    public override List<ScenarioStep> GetOnSampleButtonPress_Scenario(LogicHandlerContext context)
    {
        const float DISTANCE_TOLERANCE = 0.001f;

        // --- Сценарий установки (если образца нет) ---
        if (!context.IsSamplePresent)
        {
            // 1. Пре-проверка на достаточность места
            if (context.CurrentDistance < context.RequiredSampleLength - DISTANCE_TOLERANCE)
            {
                return new List<ScenarioStep> { new ScenarioStep(HandlerAdvisedAction.ShowHint, "Недостаточно места для установки образца. Поднимите траверсу.") };
            }

            bool isDistanceSetExactly = Mathf.Abs(context.CurrentDistance - context.RequiredSampleLength) <= DISTANCE_TOLERANCE;

            // 2. Сценарий в зависимости от расстояния
            if (isDistanceSetExactly)
            {
                // Случай Б: Мгновенная установка
                return new List<ScenarioStep>
                {
                    new ScenarioStep(HandlerAdvisedAction.CreateSample),
                    new ScenarioStep(HandlerAdvisedAction.ClampUpperGrip),
                    new ScenarioStep(HandlerAdvisedAction.ClampLowerGrip),
                    new ScenarioStep(HandlerAdvisedAction.SetState, TestState.ReadyToTest)
                };
            }
            else
            {
                // Случай А: Двухэтапная установка ("наживление")
                return new List<ScenarioStep>
                {
                    new ScenarioStep(HandlerAdvisedAction.CreateSample),
                    new ScenarioStep(HandlerAdvisedAction.ClampUpperGrip),
                    new ScenarioStep(HandlerAdvisedAction.SetState, TestState.ReadyForSetup),
                    new ScenarioStep(HandlerAdvisedAction.UpdateSampleButtonText, "УБРАТЬ\nОБРАЗЕЦ")
                };
            }
        }
        // --- Сценарий снятия (если образец есть) ---
        else
        {
            // Проверка, если тест уже был проведен
            if (context.CurrentState == TestState.TestFinished_SampleUnderLoad || context.CurrentState == TestState.TestResult_SampleSafe)
            {
                // Для растяжения, если тест завершен (Completed), образец разорван и не под нагрузкой.
                // Если тест остановлен (Stopped), он под нагрузкой.
                if (context.CurrentState == TestState.TestFinished_SampleUnderLoad && !context.IsSampleUnloaded)
                {
                    return new List<ScenarioStep> { new ScenarioStep(HandlerAdvisedAction.ShowHint, "Образец под нагрузкой. Выполните разгрузку.") };
                }

                // Снятие ПОСЛЕ теста
                return new List<ScenarioStep>
                {
                    new ScenarioStep(HandlerAdvisedAction.UnclampUpperGrip),
                    new ScenarioStep(HandlerAdvisedAction.UnclampLowerGrip),
                    new ScenarioStep(HandlerAdvisedAction.RemoveSample),
                    new ScenarioStep(HandlerAdvisedAction.SetState, TestState.TestResult_SampleSafe)
                };
            }

            // Снятие ДО теста (или при нажатии "Зажать образец")
            // Логика "Умной кнопки": если мы уже подогнали траверсу, кнопка "Образец" срабатывает как "Зажать низ"
            if (context.CurrentState == TestState.ReadyForSetup)
            {
                bool isDistanceSetExactly = Mathf.Abs(context.CurrentDistance - context.RequiredSampleLength) <= DISTANCE_TOLERANCE;
                // Если расстояние идеальное и низ еще не зажат -> Зажимаем (финализируем установку)
                if (isDistanceSetExactly && !context.IsLowerGripClamped)
                {
                    // Делегируем сценарий зажатия нижнего захвата (он сам переведет в ReadyToTest)
                    return GetOnClampGripPress_Scenario(GripType.Lower, context);
                }
            }

            // Во всех остальных случаях "Убрать образец" означает полное снятие.
            return new List<ScenarioStep>
            {
                new ScenarioStep(HandlerAdvisedAction.UnclampUpperGrip),
                new ScenarioStep(HandlerAdvisedAction.UnclampLowerGrip),
                new ScenarioStep(HandlerAdvisedAction.RemoveSample),
                new ScenarioStep(HandlerAdvisedAction.SetState, TestState.ReadyForSetup)
            };
        }
    }

    public override List<ScenarioStep> GetOnUnloadSamplePress_Scenario(LogicHandlerContext context)
    {
        // Разгрузка нужна только если тест был остановлен
        if (context.CurrentState == TestState.TestFinished_SampleUnderLoad)
        {
            return new List<ScenarioStep>
            {
                new ScenarioStep(HandlerAdvisedAction.SetUnloadedFlag),
                new ScenarioStep(HandlerAdvisedAction.ShowHint, "Разгрузка завершена, можете удалить образец"),
                new ScenarioStep(HandlerAdvisedAction.SetState, TestState.TestResult_SampleSafe)
            };
        }
        return new List<ScenarioStep>();
    }

    public override List<ScenarioStep> GetOnClampGripPress_Scenario(GripType grip, LogicHandlerContext context)
    {
        // --- Логика, если образец ЕСТЬ ---
        if (context.IsSamplePresent)
        {
            // Только для нижнего захвата, когда образец уже "наживлен"
            if (grip == GripType.Lower && context.CurrentState == TestState.ReadyForSetup && context.IsSamplePresent)
            {
                const float DISTANCE_TOLERANCE = 0.001f;
                bool isDistanceCorrect = Mathf.Abs(context.CurrentDistance - context.RequiredSampleLength) <= DISTANCE_TOLERANCE;

                if (isDistanceCorrect)
                {
                    return new List<ScenarioStep>
                    {
                        new ScenarioStep(HandlerAdvisedAction.ClampLowerGrip),
                        new ScenarioStep(HandlerAdvisedAction.SetState, TestState.ReadyToTest)
                    };
                }
                else
                {
                    return new List<ScenarioStep>
                    {
                        new ScenarioStep(HandlerAdvisedAction.ShowHint, "Невозможно зажать образец. Подведите траверсу к образцу кнопкой \"Подвести траверсу\" или вручную.")
                    };
                }
            }
        }
        // --- Логика, если образца НЕТ (свободный режим) ---
        else 
        {
            if (grip == GripType.Upper)
            {
                return new List<ScenarioStep> { new ScenarioStep(HandlerAdvisedAction.ClampUpperGrip) };
            }
            if (grip == GripType.Lower)
            {
                return new List<ScenarioStep> { new ScenarioStep(HandlerAdvisedAction.ClampLowerGrip) };
            }
        }

        // Если ни одно условие не подошло, возвращаем пустой список
        return new List<ScenarioStep>();
    }

    public override List<ScenarioStep> GetOnUnclampGripPress_Scenario(GripType grip, LogicHandlerContext context)
    {
        // Особая логика для разжатия верхнего захвата
        if (context.IsSamplePresent)
        {
            if (grip == GripType.Upper)
            {
                return new List<ScenarioStep>
                {
                    new ScenarioStep(HandlerAdvisedAction.UnclampUpperGrip),
                    new ScenarioStep(HandlerAdvisedAction.RemoveSample),
                    new ScenarioStep(HandlerAdvisedAction.SetState, TestState.ReadyForSetup)
                };
            }
            if (grip == GripType.Lower)
            {
                return new List<ScenarioStep>
                {
                    new ScenarioStep(HandlerAdvisedAction.UnclampLowerGrip),
                    new ScenarioStep(HandlerAdvisedAction.SetState, TestState.ReadyForSetup)
                };
            }
        }
        else
        {
            if (grip == GripType.Upper)
            {
                return new List<ScenarioStep> { new ScenarioStep(HandlerAdvisedAction.UnclampUpperGrip) };
            }
            if (grip == GripType.Lower)
            {
                return new List<ScenarioStep> { new ScenarioStep(HandlerAdvisedAction.UnclampLowerGrip) };
            }
        }
        return new List<ScenarioStep>();
    }

    #endregion
}