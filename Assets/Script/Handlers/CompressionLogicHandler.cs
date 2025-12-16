using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// Обработчик логики для испытаний на СЖАТИЕ.
public class CompressionLogicHandler : DefaultLogicHandler, IScenarioProvider
{
    public CompressionLogicHandler(TestConfigurationData config) : base(config) { }

    /// Для испытаний на сжатие, траверса должна двигаться НАВСТРЕЧУ неподвижной плите, уменьшая расстояние до тех пор, пока оно не станет равно высоте образца.
    public override ApproachGuidanceOutput GetApproachGuidance(TestConfigurationData testConfig)
    {
        return new ApproachGuidanceOutput
        {
            DeterminingParameter = ApproachGuidanceParameter.SampleLength,
            ActionType = ApproachActionType.ReduceToDistance // Сблизить на расстояние
        };
    }

    /// Обновляет внешний вид образца во время симуляции сжатия. Образец уменьшается по высоте (Y) и "расширяется" в стороны (X, Z) в соответствии с коэффициентом Пуассона.
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
        if (sampleTransform == null || testConfig == null || actualSampleLength <= 0 || isVisuallyRuptured)
        {
            return;
        }

        float relativeDisplacement = currentGraphRelativeDeformationPercent / 100f; // Относительное сжатие (0.0 to 1.0)

        // 1. Уменьшаем масштаб по высоте (ось Y)
        float scaleMultiplierY = 1.0f - relativeDisplacement;
        float newScaleY = initialSampleScale.y * Mathf.Max(0.01f, scaleMultiplierY); // Не даем схлопнуться полностью

        // 2. Увеличиваем масштаб в стороны (X и Z) для сохранения объема (эффект Пуассона)
        float poissonRatio = 0.35f; // Типичное значение для металлов
        float transverseScaleMultiplier = 1.0f + (relativeDisplacement * poissonRatio);
        float newScaleX = initialSampleScale.x * transverseScaleMultiplier;
        float newScaleZ = initialSampleScale.z * transverseScaleMultiplier;

        sampleTransform.localScale = new Vector3(newScaleX, newScaleY, newScaleZ);
    }

    /// Конфигурирует панель ввода параметров образца для теста на сжатие. Учитывает тип образца (куб, цилиндр) и ограничения, накладываемые оснасткой (плитами сжатия).
    public override SampleUIConfiguration GetSampleParametersUIConfig(
        TestConfigurationData testConfig,
        SampleData selectedSampleData,
        MaterialPropertiesAsset selectedMaterialProps)
    {
        if (selectedSampleData == null || testConfig == null)
        {
            return new SampleUIConfiguration { Fields = new List<SampleUIFieldConfig>() };
        }

        var uiConfig = new SampleUIConfiguration { Fields = new List<SampleUIFieldConfig>() };

        // --- Шаг 1: Определяем ключевые характеристики и ограничения ---

        // Эффективный куб - это форма "Квадрат", у которой ширина и высота следуют за "стороной".
        bool isEffectivelyCube = selectedSampleData.sampleForm == SampleForm.Квадрат &&
                                 selectedSampleData.widthSetting?.linkMode == DimensionLinkMode.FollowDiameterThickness &&
                                 selectedSampleData.lengthSetting?.linkMode == DimensionLinkMode.FollowDiameterThickness;

        // Получаем максимально допустимую высоту образца из данных плит сжатия.
        var (minFixtureHeight, maxFixtureHeight, rangeFound) = GetFixtureHeightLimits(testConfig);

        // --- Шаг 2: Конфигурация полей ---

        // Поле 1: Диаметр / Сторона (внутреннее имя "DiameterThickness")
        if (selectedSampleData.diameterThicknessSetting != null)
        {
            var dtSetting = selectedSampleData.diameterThicknessSetting;
            string dtLabel = selectedSampleData.sampleForm == SampleForm.Круг ? "Диаметр, мм" : "Сторона, мм";

            float minConstraint = dtSetting.minConstraint;
            float maxConstraint = dtSetting.maxConstraint;

            // Если это куб, его "сторона" ограничена высотой плит.
            if (isEffectivelyCube && rangeFound)
            {
                minConstraint = Mathf.Max(minConstraint, minFixtureHeight);
                maxConstraint = Mathf.Min(maxConstraint, maxFixtureHeight);
            }

            uiConfig.Fields.Add(new SampleUIFieldConfig
            {
                ParameterName = "DiameterThickness",
                LabelText = dtLabel,
                IsVisible = true,
                DefaultValue = dtSetting.defaultValue,
                MinConstraint = minConstraint,
                MaxConstraint = maxConstraint,
                // Остальные параметры (IsDropdown, StandardValues) берутся из SampleData
                IsDropdown = dtSetting.inputMode == SingleDimensionInputMode.SelectStandard,
                StandardValues = dtSetting.standardValues,
                StandardDisplayFormat = dtSetting.standardDisplayFormat,
            });
            uiConfig.DiameterThicknessLabelOverride = dtLabel;
        }

        // Поле 2: Ширина (только для параллелепипедов)
        if (selectedSampleData.widthSetting != null)
        {
            bool showWidthField = selectedSampleData.sampleForm != SampleForm.Круг && !isEffectivelyCube;
            uiConfig.IsWidthFieldRelevant = showWidthField; // Глобальный флаг для UI

            var wSetting = selectedSampleData.widthSetting;
            uiConfig.Fields.Add(new SampleUIFieldConfig
            {
                ParameterName = "Width",
                LabelText = "Ширина, мм",
                IsVisible = showWidthField && wSetting.linkMode == DimensionLinkMode.Master,
                DefaultValue = wSetting.defaultValue,
                MinConstraint = wSetting.minConstraint,
                MaxConstraint = wSetting.maxConstraint,
                IsDropdown = wSetting.inputMode == SingleDimensionInputMode.SelectStandard,
                StandardValues = wSetting.standardValues,
                StandardDisplayFormat = wSetting.standardDisplayFormat,
            });
        }

        // Поле 3: Высота (внутреннее имя "Length", только для цилиндров и параллелепипедов)
        if (selectedSampleData.lengthSetting != null)
        {
            var lSetting = selectedSampleData.lengthSetting;
            bool showHeightField = !isEffectivelyCube;

            float minConstraint = lSetting.minConstraint;
            float maxConstraint = lSetting.maxConstraint;

            // Высота всегда ограничена плитами сжатия.
            if (rangeFound)
            {
                minConstraint = Mathf.Max(minConstraint, minFixtureHeight);
                maxConstraint = Mathf.Min(maxConstraint, maxFixtureHeight);
            }

            uiConfig.Fields.Add(new SampleUIFieldConfig
            {
                ParameterName = "Length",
                LabelText = "Высота, мм",
                IsVisible = showHeightField && lSetting.linkMode == DimensionLinkMode.Master,
                DefaultValue = lSetting.defaultValue,
                MinConstraint = minConstraint,
                MaxConstraint = maxConstraint,
                IsDropdown = lSetting.inputMode == SingleDimensionInputMode.SelectStandard,
                StandardValues = lSetting.standardValues,
                StandardDisplayFormat = lSetting.standardDisplayFormat,
            });
        }

        // Поле 4: Скорость испытания
        uiConfig.Fields.Add(new SampleUIFieldConfig
        {
            ParameterName = "Speed",
            LabelText = "Скорость",
            IsVisible = true,
            DefaultValue = testConfig.testMoveSpeed,
            MinConstraint = 0.001f,
            MaxConstraint = 1000f,
            HasSpeedModeSelector = true,
            DefaultSpeedMode = TestSpeedMode.ForceRate
        });

        return uiConfig;
    }

    /// Проверяет корректность введенных пользователем размеров образца, учитывая ограничения как самого образца, так и оснастки.
    public override Dictionary<string, string> ValidateSampleParameters(
        Dictionary<string, float> currentDimensionValues,
        SampleForm selectedShape,
        TestConfigurationData testConfig,
        SampleData sampleData,
        MaterialPropertiesAsset materialProps,
        float minAllowedSpeedUser,
        float maxAllowedSpeedUser,
        TestSpeedMode speedMode)
    {
        var errors = base.ValidateSampleParameters(currentDimensionValues, selectedShape, testConfig, sampleData, materialProps, minAllowedSpeedUser, maxAllowedSpeedUser, speedMode);
        if (sampleData == null || testConfig == null) { errors["General"] = "Ошибка конфигурации."; return errors; }

        bool isEffectivelyCube = selectedShape == SampleForm.Квадрат &&
                                 sampleData.widthSetting?.linkMode == DimensionLinkMode.FollowDiameterThickness &&
                                 sampleData.lengthSetting?.linkMode == DimensionLinkMode.FollowDiameterThickness;

        var (minFixtureHeight, maxFixtureHeight, rangeFound) = GetFixtureHeightLimits(testConfig);

        // Валидация Высоты (для куба это "сторона", для остальных - "высота")
        if (isEffectivelyCube)
        {
            // У куба высота = стороне (DiameterThickness)
            if (currentDimensionValues.TryGetValue("DiameterThickness", out float sideValue) && rangeFound)
            {
                if (sideValue < minFixtureHeight || sideValue > maxFixtureHeight)
                    errors["DiameterThickness"] = $"Сторона куба вне доп. высоты плит ({minFixtureHeight:F2} - {maxFixtureHeight:F2} мм).";
            }
        }
        else
        {
            // У цилиндра/параллелепипеда своя высота (Length)
            if (currentDimensionValues.TryGetValue("Length", out float heightValue) && rangeFound)
            {
                if (heightValue < minFixtureHeight || heightValue > maxFixtureHeight)
                    errors["Length"] = $"Высота вне доп. диапазона плит ({minFixtureHeight:F2} - {maxFixtureHeight:F2} мм).";
            }
        }

        return errors;
    }

    /// Вспомогательный метод для получения минимальной и максимальной высоты образца, которую допускает установленная оснастка (плиты сжатия).
    private (float min, float max, bool found) GetFixtureHeightLimits(TestConfigurationData testConfig)
    {
        if (testConfig.potentialFixtureIDs == null || !testConfig.potentialFixtureIDs.Any())
            return (0, 0, false);

        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;
        bool found = false;

        foreach (string fixtureId in testConfig.potentialFixtureIDs)
        {
            var fixtureData = FixtureManager.Instance?.GetFixtureData(fixtureId);
            if (fixtureData is IClampRangeProvider rangeProvider)
            {
                minHeight = Mathf.Min(minHeight, rangeProvider.MinGripDimension);
                maxHeight = Mathf.Max(maxHeight, rangeProvider.MaxGripDimension);
                found = true;
            }
        }

        return found ? (minHeight, maxHeight, true) : (0, 0, false);
    }

    /// Переопределяет логику создания плана смены оснастки, чтобы выбрать правильные плиты сжатия на основе размера образца.
    public override FixtureChangePlan CreateFixtureChangePlan(TestConfigurationData targetConfig, SampleForm shape, List<string> currentlyInstalledFixtures)
    {
        var fm = FixtureManager.Instance;
        
        // Проверяем, что монитор доступен
        if (fm == null || _monitor == null || targetConfig.potentialFixtureIDs == null)
        {
            // Вызываем базовую логику, если что-то пошло не так
            return base.CreateFixtureChangePlan(targetConfig, shape, currentlyInstalledFixtures);
        }

        // --- Логика выбора правильной плиты ---
        
        // Получаем размеры из монитора
        _monitor.CurrentSampleParameters.TryGetValue("DiameterThickness", out float actualDiameterThickness);
        _monitor.CurrentSampleParameters.TryGetValue("Width", out float actualWidth);
        
        float sampleDimension = Mathf.Max(actualDiameterThickness, actualWidth);

        string suitableUpperPlateId = null;
        string suitableLowerPlateId = null;
        float bestUpperFit = float.MaxValue;
        float bestLowerFit = float.MaxValue;

        foreach (var fixtureId in targetConfig.potentialFixtureIDs)
        {
            var data = fm.GetFixtureData(fixtureId);

            // Сначала проверяем, что это IClampRangeProvider
            if (data is IClampRangeProvider rangeProvider)
            {
                if (sampleDimension <= rangeProvider.MaxGripDimension) // Если образец помещается
                {
                    if (data.fixtureZone == FixtureZone.CompressionUpper)
                    {
                        // Ищем самую маленькую плиту, на которую помещается образец
                        if (rangeProvider.MaxGripDimension < bestUpperFit)
                        {
                            bestUpperFit = rangeProvider.MaxGripDimension;
                            suitableUpperPlateId = fixtureId;
                        }
                    }
                    else if (data.fixtureZone == FixtureZone.CompressionLower)
                    {
                        if (rangeProvider.MaxGripDimension < bestLowerFit)
                        {
                            bestLowerFit = rangeProvider.MaxGripDimension;
                            suitableLowerPlateId = fixtureId;
                        }
                    }
                }
            }
    }

        // --- Формируем итоговый список требований (без изменений) ---
        var finalRequiredFixtures = new List<string>();
        if (!string.IsNullOrEmpty(suitableUpperPlateId)) finalRequiredFixtures.Add(suitableUpperPlateId);
        if (!string.IsNullOrEmpty(suitableLowerPlateId)) finalRequiredFixtures.Add(suitableLowerPlateId);

        if (finalRequiredFixtures.Count == 0)
        {
            Debug.LogError($"[CompressionHandler] Не удалось подобрать плиты сжатия для образца размером {sampleDimension} мм!");
        }

        // Создаем временный конфиг только с ПРАВИЛЬНЫМИ ID
        var tempConfig = ScriptableObject.CreateInstance<TestConfigurationData>();
        tempConfig.potentialFixtureIDs = finalRequiredFixtures;

        // Вызываем БАЗОВЫЙ планировщик, передав ему новый `shape`
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
            // Пре-проверка на достаточность места
            if (context.CurrentDistance < context.RequiredSampleLength - DISTANCE_TOLERANCE)
            {
                return new List<ScenarioStep>
                {
                    new ScenarioStep(HandlerAdvisedAction.ShowHint, "Недостаточно места для установки образца. Поднимите траверсу.")
                };
            }
            
            // Основной сценарий установки для сжатия
            bool isDistanceMatching = Mathf.Abs(context.CurrentDistance - context.RequiredSampleLength) <= DISTANCE_TOLERANCE;

            // Основной сценарий установки для сжатия
            var scenario = new List<ScenarioStep>
            {
                new ScenarioStep(HandlerAdvisedAction.CreateSample)
            };

            // Если расстояние совпадает, сразу переходим в ReadyToTest.
            // В противном случае, ждем проверки расстояния (SamplePlaced_AwaitingApproach).
            if (isDistanceMatching)
            {
                scenario.Add(new ScenarioStep(HandlerAdvisedAction.SetState, TestState.ReadyToTest));
            }
            else
            {
                scenario.Add(new ScenarioStep(HandlerAdvisedAction.SetState, TestState.ReadyForSetup));
            }
            
            return scenario;
        }
        // --- Сценарий снятия (если образец есть) ---
        else
        {
            // Проверка, если тест уже был проведен
            if (context.CurrentState == TestState.TestFinished_SampleUnderLoad || context.CurrentState == TestState.TestResult_SampleSafe)
            {
                if (context.CurrentState == TestState.TestFinished_SampleUnderLoad && !context.IsSampleUnloaded)
                {
                    return new List<ScenarioStep> { new ScenarioStep(HandlerAdvisedAction.ShowHint, "Образец под нагрузкой. Выполните разгрузку.") };
                }
                
                // Снятие ПОСЛЕ теста
                return new List<ScenarioStep>
                {
                    new ScenarioStep(HandlerAdvisedAction.RemoveSample),
                    new ScenarioStep(HandlerAdvisedAction.SetState, TestState.TestResult_SampleSafe)
                };
            }
            
            // Снятие ДО теста
            return new List<ScenarioStep>
            {
                new ScenarioStep(HandlerAdvisedAction.RemoveSample),
                new ScenarioStep(HandlerAdvisedAction.SetState, TestState.ReadyForSetup)
            };
        }
    }

    public override List<ScenarioStep> GetOnUnloadSamplePress_Scenario(LogicHandlerContext context)
    {
        // Проверяем только актуальное состояние "Под нагрузкой"
        if (context.CurrentState == TestState.TestFinished_SampleUnderLoad)
        {
            return new List<ScenarioStep>
            {
                new ScenarioStep(HandlerAdvisedAction.SetUnloadedFlag),
                new ScenarioStep(HandlerAdvisedAction.ShowHint, "Разгрузка завершена"),
                new ScenarioStep(HandlerAdvisedAction.SetState, TestState.TestResult_SampleSafe)
            };
        }
        return new List<ScenarioStep>();
    }

    public override List<ScenarioStep> GetOnClampGripPress_Scenario(GripType grip, LogicHandlerContext context)
    {
        if (grip == GripType.Upper)
        {
            return new List<ScenarioStep> { new ScenarioStep(HandlerAdvisedAction.ClampUpperGrip) };
        }
        if (grip == GripType.Lower)
        {
            return new List<ScenarioStep> { new ScenarioStep(HandlerAdvisedAction.ClampLowerGrip) };
        }
        return new List<ScenarioStep>();
    }

    /// <summary>
    /// Переопределяем поведение. Для испытаний на сжатие захваты не задействованы,
    /// но если пользователь нажмет кнопку, действие должно выполниться.
    /// </summary>
    public override List<ScenarioStep> GetOnUnclampGripPress_Scenario(GripType grip, LogicHandlerContext context)
    {
        if (grip == GripType.Upper)
        {
            return new List<ScenarioStep> { new ScenarioStep(HandlerAdvisedAction.UnclampUpperGrip) };
        }
        if (grip == GripType.Lower)
        {
            return new List<ScenarioStep> { new ScenarioStep(HandlerAdvisedAction.UnclampLowerGrip) };
        }
        return new List<ScenarioStep>();
    }

    #endregion
}