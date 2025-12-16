using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Обработчик логики для испытания на растяжение с использованием Пропорционального Захвата.
/// </summary>
public class TensileProportionalLogicHandler : TensileLogicHandler
{
    public TensileProportionalLogicHandler(TestConfigurationData config) : base(config) { }

    public override SampleUIConfiguration GetSampleParametersUIConfig(
        TestConfigurationData testConfig,
        SampleData selectedSampleData,
        MaterialPropertiesAsset selectedMaterialProps)
    {

        // Мы НЕ вызываем базовый метод, чтобы не наследовать его ошибку.
        // Вся логика теперь находится здесь.

        if (selectedSampleData == null || testConfig == null)
        {
            Debug.LogWarning($"[TensileProportionalLogicHandler] Недостаточно данных для построения UI.");
            return new SampleUIConfiguration { Fields = new List<SampleUIFieldConfig>() };
        }

        var uiConfig = new SampleUIConfiguration
        {
            Fields = new List<SampleUIFieldConfig>()
        };

        // --- 1. Параметр: Диаметр/Толщина (DiameterThickness) ---
        if (selectedSampleData.diameterThicknessSetting != null)
        {
            var dtSetting = selectedSampleData.diameterThicknessSetting;
            
            // --- КЛЮЧЕВОЕ ИСПРАВЛЕНИЕ: Расчет диапазона ТОЛЬКО по нужной оснастке ---
            float minFixtureRange = 0.001f;
            float maxFixtureRange = 10000f;
            bool fixtureRangeApplied = false;

            if (testConfig.potentialFixtureIDs != null)
            {
                // Ищем в списке оснастку, ID которой начинается с "Pr_Ins_", как в логе.
                var proportionalInserts = testConfig.potentialFixtureIDs
                .Select(id => FixtureManager.Instance?.GetFixtureData(id))
                .OfType<ProportionalInsertData>() // Фильтруем по правильному типу.
                .ToList();

                if (proportionalInserts.Any())
                {
                    minFixtureRange = proportionalInserts.Min(p => p.MinGripDimension);
                    maxFixtureRange = proportionalInserts.Max(p => p.MaxGripDimension); // Теперь здесь гарантированно будет 21.
                    fixtureRangeApplied = true;
                }
            }

            Debug.Log($"<color=cyan><b>[ProportionalHandler] Итоговый диапазон для UI: Min = {minFixtureRange}, Max = {maxFixtureRange}</b></color>");

            // --- Создание поля UI с ПРАВИЛЬНЫМИ данными ---
            var availableStandardDtValues = (dtSetting.standardValues ?? new List<float>())
                .Where(val => val >= minFixtureRange && val <= maxFixtureRange)
                .ToList();

            float currentDefaultDt = dtSetting.defaultValue;
            if (!availableStandardDtValues.Contains(currentDefaultDt) && availableStandardDtValues.Any())
            {
                currentDefaultDt = availableStandardDtValues[0];
            }

            uiConfig.Fields.Add(new SampleUIFieldConfig
            {
                ParameterName = "DiameterThickness",
                LabelText = "Диаметр, мм",
                IsVisible = true,
                IsDropdown = dtSetting.inputMode == SingleDimensionInputMode.SelectStandard && availableStandardDtValues.Any(),
                StandardValues = availableStandardDtValues,
                StandardDisplayFormat = dtSetting.standardDisplayFormat,
                DefaultValue = currentDefaultDt,
                MinConstraint = Mathf.Max(dtSetting.minConstraint, fixtureRangeApplied ? minFixtureRange : dtSetting.minConstraint),
                MaxConstraint = Mathf.Min(dtSetting.maxConstraint, fixtureRangeApplied ? maxFixtureRange : dtSetting.maxConstraint)
            });
        }

        // --- 2. Параметр: Рабочая длина (Length) ---
        // (Логика полностью скопирована из TensileLogicHandler)
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

        // --- 3. Параметр: Скорость (Speed) ---
        // (Логика полностью скопирована из TensileLogicHandler)
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

        // --- 4. Финальные настройки для UI ---
        uiConfig.DiameterThicknessLabelOverride = "Диаметр, мм";
        uiConfig.IsWidthFieldRelevant = false; // Поле "Ширина" для этого теста не нужно.

        return uiConfig;
    }

    public override IEnumerator SetupTestSpecificFixtures(
        TestConfigurationData testConfig,
        FixtureManager fixtureManager,
        ToDoManager toDoManager)
    {
        yield break;
    }

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
        // Сначала вызываем базовую валидацию для скорости, длины и т.д.
        var errors = base.ValidateSampleParameters(currentDimensionValues, selectedShape, testConfig, sampleData, materialProps, minAllowedSpeedUser, maxAllowedSpeedUser, speedMode);

        // А теперь ПЕРЕПИСЫВАЕМ логику валидации для диаметра, используя ПРАВИЛЬНЫЙ фильтр.
        
        // 1. Вычисляем правильный диапазон ТОЛЬКО по пропорциональным вкладышам.
        float minFixtureDt = 0.001f, maxFixtureDt = 10000f;
        bool fixtureRangeKnown = false;
        if (testConfig.potentialFixtureIDs != null)
        {
            var proportionalInserts = testConfig.potentialFixtureIDs
                .Select(id => FixtureManager.Instance?.GetFixtureData(id))
                .OfType<ProportionalInsertData>() // <-- Правильный фильтр
                .ToList();

            if (proportionalInserts.Any())
            {
                minFixtureDt = proportionalInserts.Min(p => p.MinGripDimension);
                maxFixtureDt = proportionalInserts.Max(p => p.MaxGripDimension); // <-- Здесь будет 21
                fixtureRangeKnown = true;
            }
        }

        // 2. Проверяем введенное значение по этому правильному диапазону.
        if (currentDimensionValues.TryGetValue("DiameterThickness", out float dtValue))
        {
            if (float.IsNaN(dtValue) || dtValue <= 0)
            {
                errors["DiameterThickness"] = "Значение должно быть полож. числом.";
            }
            else if (sampleData.diameterThicknessSetting != null)
            {
                float sampleMin = sampleData.diameterThicknessSetting.minConstraint;
                float sampleMax = sampleData.diameterThicknessSetting.maxConstraint;
                
                float effectiveMin = Mathf.Max(sampleMin, fixtureRangeKnown ? minFixtureDt : sampleMin);
                float effectiveMax = Mathf.Min(sampleMax, fixtureRangeKnown ? maxFixtureDt : sampleMax);
                
                if (dtValue < effectiveMin || dtValue > effectiveMax)
                {
                    // Теперь сообщение об ошибке будет использовать правильный диапазон 4-21.
                    errors["DiameterThickness"] = $"Значение вне доп. диапазона ({effectiveMin:F2} - {effectiveMax:F2} мм).";
                }
                else
                {
                    // Если ошибки нет, удаляем ее из словаря (на случай, если она там была от базового метода)
                    errors.Remove("DiameterThickness");
                }
            }
        }
        
        return errors;
    }

    /// <summary>
    /// Переопределенный метод, который теперь корректно собирает полный план
    /// и выставляет флаги для анимации.
    /// </summary>
    public override FixtureChangePlan CreateFixtureChangePlan(TestConfigurationData targetConfig, SampleForm shape, List<string> currentlyInstalledFixtures)
    {
        var fm = FixtureManager.Instance;
        // Было: if (fm == null || confirmedParams == null) return new FixtureChangePlan();
        // Стало: (проверяем _monitor, который у нас есть из базового класса)
        if (fm == null || _monitor == null) return new FixtureChangePlan();

        // Получаем размер из монитора
        _monitor.CurrentSampleParameters.TryGetValue("DiameterThickness", out float actualDiameterThickness);

        // ... (код отладочного блока, если он вам нужен)

        var plan = new FixtureChangePlan();

        // --- Предварительный шаг: Определяем, происходит ли замена вкладышей ---
        var currentInserts = currentlyInstalledFixtures
            .Select(fm.GetFixtureData)
            .OfType<ProportionalInsertData>()
            .Select(d => d.fixtureId)
            .ToHashSet();

        var requiredInserts = targetConfig.potentialFixtureIDs
            .Select(fm.GetFixtureData)
            .OfType<ProportionalInsertData>()
            // Было: .Where(data => data != null && confirmedParams.ActualDiameterThickness >= data.MinGripDimension && confirmedParams.ActualDiameterThickness <= data.MaxGripDimension)
            // Стало:
            .Where(data => data != null && actualDiameterThickness >= data.MinGripDimension && actualDiameterThickness <= data.MaxGripDimension)
            .Select(d => d.fixtureId)
            .ToHashSet();

        
        bool insertsAreChanging = !currentInserts.SetEquals(requiredInserts);
        if(insertsAreChanging) Debug.Log("<color=cyan>[ProportionalHandler] Обнаружена замена вкладышей. Кольца будут принудительно анимированы.</color>");


        // --- Шаг 1: Определяем ПОЛНЫЙ "идеальный" набор оснастки для этого теста ---
        var idealSetupIds = new HashSet<string>();
        
        // Сначала собираем ПОЛНЫЙ список всего, что нужно для теста
        idealSetupIds.Add("PropFixUp");
        idealSetupIds.Add("PropFixDown");
        idealSetupIds.UnionWith(requiredInserts);
        
        var adapterData = FindFixtureData<ProportionalAdapterData>(targetConfig, fm);
        if (adapterData != null)
        {
            float tailDiameter = adapterData.adapterTailDiameter_mm;
            var suitableHydraulicInserts = targetConfig.potentialFixtureIDs
                .Select(fm.GetFixtureData)
                .OfType<HydraulicInsertData>()
                .Where(data => data != null && tailDiameter >= data.MinGripDimension && tailDiameter <= data.MaxGripDimension)
                .Select(data => data.fixtureId);
            idealSetupIds.UnionWith(suitableHydraulicInserts);
        }

        var internalParts = targetConfig.potentialFixtureIDs
                .Select(fm.GetFixtureData)
                .OfType<SimplePartData>()
                .Where(data => data != null && data.placementSource == SamplePlacementSource.OnFixtureType)
                .Select(data => data.fixtureId);
        idealSetupIds.UnionWith(internalParts);

        // Теперь, когда есть полный список, формируем отдельный список для установки без анимации
        var preInitializeList = new List<string>();
        preInitializeList.Add("PropFixUp");
        preInitializeList.Add("PropFixDown");
        preInitializeList.AddRange(requiredInserts);

        // Добавляем кольца, так как они - часть базовой установки.
        preInitializeList.Add("Prop_RingUp");
        preInitializeList.Add("Prop_RingDown");

        plan.FixturesToPreInitialize = preInitializeList;

        // --- Шаг 2: Проверяем, будет ли запущен PreInitialize, и корректируем флаг ---
        // Это предотвращает ненужную анимацию колец при первой установке.
        if (!currentlyInstalledFixtures.Contains("PropFixUp"))
        {
            // Если основного захвата нет, это точно первая установка.
            // Отключаем принудительную анимацию колец.
            insertsAreChanging = false;
        }


        // --- Шаг 2: Формируем план ЗАМЕНЫ ---
        var idealFixturesByZone = idealSetupIds
            .Select(fm.GetFixtureData)
            .Where(d => d != null && d.placementSource == SamplePlacementSource.FixedOnMachine)
            .GroupBy(d => d.fixtureZone)
            .ToDictionary(g => g.Key, g => g.First().fixtureId);

        foreach (var installedId in currentlyInstalledFixtures)
        {
            var installedData = fm.GetFixtureData(installedId);
            if (installedData == null) continue;

            if (!idealSetupIds.Contains(installedId))
            {
                if (idealFixturesByZone.ContainsKey(installedData.fixtureZone) || installedData.isSpecializedEquipment)
                {
                    plan.MainFixturesToRemove.Add(installedId);
                }
            }
        }
        
        foreach (var idealId in idealSetupIds)
        {
            if (currentlyInstalledFixtures.Contains(idealId)) continue;

            var dataToInstall = fm.GetFixtureData(idealId);
            if (dataToInstall == null) continue;

            if (dataToInstall.placementSource == SamplePlacementSource.FixedOnMachine)
            {
                bool useAnimation = plan.MainFixturesToRemove.Any(removedId =>
                {
                    var removedData = fm.GetFixtureData(removedId);
                    return removedData != null && removedData.fixtureZone == dataToInstall.fixtureZone;
                });

                plan.MainFixturesToInstall.Add(new FixtureChangePlan.FixtureInstallationInfo
                {
                    FixtureId = idealId,
                    UseAnimation = useAnimation
                });
            }
            else
            {
                plan.InternalFixturesToInstall.Add(new InternalFixturePlanItem
                {
                    FixtureId = idealId,
                    ParentFixtureId = dataToInstall.parentFixtureId,
                    AttachmentPointName = dataToInstall.parentAttachmentPointName
                });
            }
        }
        
        // --- Шаг 3: Принудительно добавляем кольца в план, если меняются вкладыши ---
        if (insertsAreChanging)
        {
            string[] ringIds = { "Prop_RingUp", "Prop_RingDown" };
            foreach (var ringId in ringIds)
            {
                // Добавляем в список на удаление, если еще не там.
                if (!plan.MainFixturesToRemove.Contains(ringId))
                {
                    plan.MainFixturesToRemove.Add(ringId);
                }

                // Добавляем в список на установку, если еще не там.
                if (!plan.InternalFixturesToInstall.Any(item => item.FixtureId == ringId))
                {
                    var ringData = fm.GetFixtureData(ringId);
                    if(ringData != null)
                    {
                        plan.InternalFixturesToInstall.Add(new InternalFixturePlanItem 
                        { 
                            FixtureId = ringId, 
                            ParentFixtureId = ringData.parentFixtureId, 
                            AttachmentPointName = ringData.parentAttachmentPointName 
                        });
                    }
                }
            }
        }

        // --- Шаг 4: Финальная сортировка списков для правильного порядка анимаций ---
        plan.MainFixturesToRemove.Sort((a, b) => GetRemovalPriority(a).CompareTo(GetRemovalPriority(b)));
        plan.InternalFixturesToInstall.Sort((a, b) => GetInstallationPriority(a.FixtureId).CompareTo(GetInstallationPriority(b.FixtureId)));
        
        bool installsProportionalRig = plan.MainFixturesToInstall.Any(info => info.FixtureId.StartsWith("PropFix")) ||
                                   plan.FixturesToPreInitialize.Any(id => id.StartsWith("PropFix"));

        if (installsProportionalRig)
        {
            Debug.Log("<color=cyan>[ProportionalHandler]</color> Добавление команды ReinitializeFixtureZones в план.");
            plan.InterstitialCommands.Add(new ToDoManagerCommand(ActionType.ReinitializeFixtureZones, null));
        }
        return plan;
    }
    
    public override List<ScenarioStep> GetOnSampleButtonPress_Scenario(LogicHandlerContext context)
    {
        // --- Шаг 1: Запрашиваем ID установленных деталей по их ЗОНАМ ---
        context.InstalledFixtures.TryGetValue(FixtureZone.PropRingUp, out string upperRingId);
        context.InstalledFixtures.TryGetValue(FixtureZone.PropRingDown, out string lowerRingId);
        context.InstalledFixtures.TryGetValue(FixtureZone.PropFixturePoint_UpLeft, out string upperLeftInsertId);
        context.InstalledFixtures.TryGetValue(FixtureZone.PropFixturePoint_DownLeft, out string lowerLeftInsertId);

        // Если какая-то из ключевых деталей не установлена, возвращаем пустой сценарий.
        if (string.IsNullOrEmpty(upperRingId) || string.IsNullOrEmpty(lowerRingId) ||
            string.IsNullOrEmpty(upperLeftInsertId) || string.IsNullOrEmpty(lowerLeftInsertId))
        {
            Debug.LogError("[ProportionalHandler] Не удалось построить сценарий: ключевые детали не установлены.");
            return new List<ScenarioStep>();
        }

        // --- Шаг 2: Создаем пустой список для будущего сценария ---
        var scenarioSteps = new List<ScenarioStep>();

        // --- Шаг 3: Наполняем список в зависимости от наличия образца ---
        if (!context.IsSamplePresent)
        {
            // Проверка на корректное расстояние перед установкой
            const float DISTANCE_TOLERANCE = 0.001f;
            bool isDistanceCorrect = Mathf.Abs(context.CurrentDistance - context.RequiredSampleLength) <= DISTANCE_TOLERANCE;

            if (!isDistanceCorrect)
            {
                // Если расстояние неверное, возвращаем сценарий из одного шага - показать подсказку.
                // Дверь в этом случае открывать не нужно.
                return new List<ScenarioStep>
            {
                new ScenarioStep(HandlerAdvisedAction.ShowHint, "Для установки пропорционального образца необходимо сначала подвести траверсу")
            };
            }

            // Если все хорошо, добавляем шаги установки в наш список
            scenarioSteps.AddRange(new List<ScenarioStep>
        {
            new ScenarioStep(HandlerAdvisedAction.Play_SampleRemove_Animation, upperRingId),
            new ScenarioStep(HandlerAdvisedAction.Play_SampleRemove_Animation, lowerRingId),
            new ScenarioStep(HandlerAdvisedAction.Play_SampleRemove_Animation, upperLeftInsertId),
            new ScenarioStep(HandlerAdvisedAction.Play_SampleRemove_Animation, lowerLeftInsertId),
            new ScenarioStep(HandlerAdvisedAction.CreateSample),
            new ScenarioStep(HandlerAdvisedAction.Play_SampleInstall_Animation, upperLeftInsertId),
            new ScenarioStep(HandlerAdvisedAction.Play_SampleInstall_Animation, lowerLeftInsertId),
            new ScenarioStep(HandlerAdvisedAction.Play_SampleInstall_Animation, upperRingId),
            new ScenarioStep(HandlerAdvisedAction.Play_SampleInstall_Animation, lowerRingId),
            new ScenarioStep(HandlerAdvisedAction.ClampUpperGrip),
            new ScenarioStep(HandlerAdvisedAction.ClampLowerGrip),
            new ScenarioStep(HandlerAdvisedAction.SetState, TestState.ReadyToTest)
        });
        }
        else // Если образец присутствует, создаем сценарий снятия
        {
            scenarioSteps.AddRange(new List<ScenarioStep>
        {
            new ScenarioStep(HandlerAdvisedAction.Play_SampleRemove_Animation, upperRingId),
            new ScenarioStep(HandlerAdvisedAction.Play_SampleRemove_Animation, lowerRingId),
            new ScenarioStep(HandlerAdvisedAction.Play_SampleRemove_Animation, upperLeftInsertId),
            new ScenarioStep(HandlerAdvisedAction.Play_SampleRemove_Animation, lowerLeftInsertId),
            new ScenarioStep(HandlerAdvisedAction.RemoveSample),
            new ScenarioStep(HandlerAdvisedAction.Play_SampleInstall_Animation, upperLeftInsertId),
            new ScenarioStep(HandlerAdvisedAction.Play_SampleInstall_Animation, lowerLeftInsertId),
            new ScenarioStep(HandlerAdvisedAction.Play_SampleInstall_Animation, upperRingId),
            new ScenarioStep(HandlerAdvisedAction.Play_SampleInstall_Animation, lowerRingId),
            new ScenarioStep(HandlerAdvisedAction.SetState, TestState.ReadyForSetup)
        });
        }

        // --- Шаг 4: "Оборачиваем" готовый сценарий командами для двери ---
        // Если в списке есть хоть один шаг, значит, нужно открыть и закрыть дверь.
        if (scenarioSteps.Count > 0)
        {
            // Вставляем команду "открыть дверь" в самое начало списка.
            scenarioSteps.Insert(0, new ScenarioStep(HandlerAdvisedAction.SetDoorState, true));

            // Добавляем команду "закрыть дверь" в самый конец списка.
            scenarioSteps.Add(new ScenarioStep(HandlerAdvisedAction.SetDoorState, false));
        }

        // --- Шаг 5: Возвращаем полностью готовый сценарий ---
        return scenarioSteps;
    }

    public override List<ScenarioStep> GetOnClampGripPress_Scenario(GripType grip, LogicHandlerContext context)
    {
        // Возвращаем пустой список, так как ручное управление запрещено.
        return new List<ScenarioStep>();
    }

    /// <summary>
    /// Переопределяем поведение родительского класса.
    /// Для пропорционального захвата ручное разжатие НЕ предусмотрено.
    /// Метод всегда возвращает пустой сценарий.
    /// </summary>
    public override List<ScenarioStep> GetOnUnclampGripPress_Scenario(GripType grip, LogicHandlerContext context)
    {
        // Возвращаем сценарий из одного шага - показать подсказку.
        return new List<ScenarioStep>
        {
            new ScenarioStep(HandlerAdvisedAction.ShowHint, "Невозможно разжать захваты: установлена оснастка для пропорциональных образцов.")
        };
    }

    #region Вспомогательные методы

    private T FindFixtureData<T>(TestConfigurationData testConfig, FixtureManager fixtureManager, string nameHint = "") where T : FixtureData
    {
        if (testConfig.potentialFixtureIDs == null) return null;
        return testConfig.potentialFixtureIDs
            .Select(id => fixtureManager.GetFixtureData(id))
            .OfType<T>()
            .FirstOrDefault(data => data != null && (string.IsNullOrEmpty(nameHint) || data.fixtureId.Contains(nameHint)));
    }
    
    // Вспомогательный метод для определения приоритета удаления деталей.
    private int GetRemovalPriority(string fixtureId)
    {
        if (fixtureId.Contains("Ring")) return 0; // Кольца удаляются первыми.
        if (fixtureId.Contains("Insert")) return 1; // Вкладыши - вторыми.
        return 2; // Всё остальное - в последнюю очередь.
    }
    
    // Вспомогательный метод для определения приоритета установки деталей.
    private int GetInstallationPriority(string fixtureId)
    {
        if (fixtureId.Contains("Insert")) return 0; // Вкладыши ставятся первыми.
        if (fixtureId.Contains("Ring")) return 1; // Кольца - вторыми.
        return 2; // Всё остальное - в последнюю очередь.
    }

    public override List<string> CreateTeardownPlan(List<string> fixturesScheduledForRemoval)
    {
        Debug.Log("<color=cyan>[ProportionalHandler]</color> Создание специального плана удаления с правильным порядком.");

        // Создаем копию списка, чтобы безопасно работать с ней
        var sortedList = new List<string>(fixturesScheduledForRemoval);
        
        // Сортируем список, используя нашу специальную логику приоритетов
        sortedList.Sort((a, b) => GetRemovalPriority(a).CompareTo(GetRemovalPriority(b)));
        
        return sortedList;
    }

    #endregion

    #region Команды Pre/Post (без изменений, они верны)

    public override List<ToDoManagerCommand> GetPreChangePreparationCommands(List<string> fixturesToRemove)
    {
        var commands = new List<ToDoManagerCommand>();
        bool needsUnclamping = fixturesToRemove.Any(id =>
            id.Contains("PropFixUp") || id.Contains("PropFixDown"));

        if (needsUnclamping)
        {
            Debug.Log("<color=cyan>[ProportionalHandler]</color> Пропорциональный захват будет удален. Формирование команд на разжатие вкладышей.");
            commands.Add(new ToDoManagerCommand(ActionType.UnclampUpperGrip, null));
            commands.Add(new ToDoManagerCommand(ActionType.UnclampLowerGrip, null));
        }
        return commands;
    }

    public override List<ToDoManagerCommand> GetPostChangeFinalizationCommands()
    {
        Debug.Log("<color=cyan>[ProportionalHandler]</color> Формирование команд финализации: зажим клиновых вкладышей.");
        var commands = new List<ToDoManagerCommand>
        {
            new ToDoManagerCommand(ActionType.ClampUpperGrip, null),
            new ToDoManagerCommand(ActionType.ClampLowerGrip, null)
        };
        return commands;
    }
    #endregion
}