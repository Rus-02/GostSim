using UnityEditor;
using UnityEngine;
using System.Text;
using System.Collections.Generic;
using System.Linq;

public class SystemStateMonitorWindow : EditorWindow
{
    // Переменная для хранения позиции скролла
    private Vector2 _scrollPosition;

    [MenuItem("Tools/System State Monitor")]
    public static void ShowWindow() => GetWindow<SystemStateMonitorWindow>("State Monitor");

    private void OnGUI()
    {
        if (!Application.isPlaying || SystemStateMonitor.Instance == null)
        {
            EditorGUILayout.LabelField("Запустите игру для мониторинга состояния.");
            return;
        }

        var instance = SystemStateMonitor.Instance;

        // --- НАЧАЛО SCROLL VIEW ---
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        float originalLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 220f;

        try 
        {
            DrawGlobalState(instance);
            DrawCSMState(instance);
            
            DrawMachineReadiness(instance);

            DrawVSMState(instance);
            DrawMachineState(instance);
            DrawSetupPanelState(instance);
            DrawGraphControllerState(instance);
            DrawProcessesState(instance);
            DrawFixtureStatusSection(instance);
        }
        finally
        {
            EditorGUIUtility.labelWidth = originalLabelWidth;
        }

        // --- КОНЕЦ SCROLL VIEW ---
        EditorGUILayout.EndScrollView();

        Repaint();
    }

    private void OnInspectorUpdate()
    {
        // Принудительная перерисовка, чтобы видеть изменения в реальном времени
        Repaint();
    }

    private void DrawMachineReadiness(SystemStateMonitor instance)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("MACHINE READINESS", EditorStyles.boldLabel);
        
        GUI.enabled = false; 
        EditorGUILayout.Toggle("Is Machine Ready", instance.IsMachineReadyForSetup);
        GUI.enabled = true;

        if (!instance.IsMachineReadyForSetup)
        {
            EditorGUILayout.HelpBox($"Reason: {instance.MachineNotReadyReason}", MessageType.Warning);
        }
    }

    private void DrawGlobalState(SystemStateMonitor instance)
    {
        EditorGUILayout.LabelField("GLOBAL STATE", EditorStyles.boldLabel);
        EditorGUILayout.EnumPopup("Режим работы", instance.CurrentApplicationMode);
    }

    private void DrawCSMState(SystemStateMonitor instance)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("CSM State", EditorStyles.boldLabel);
        EditorGUILayout.TextField("Состояние системы испытаний", instance.CurrentTestState.ToString());
        EditorGUILayout.TextField("Тип образца", instance.CurrentTestConfig?.name ?? "None");
        EditorGUILayout.TextField("Тип теста", instance.CurrentGeneralTestType.ToString());
        EditorGUILayout.TextField("Логика теста (Handler)", instance.CurrentTestLogicHandler?.GetType().Name ?? "None");
        EditorGUILayout.Toggle("Образец установлен", instance.IsSampleInPlace);
        EditorGUILayout.Toggle("Верхний захват сжат", instance.IsUpperGripClamped);
        EditorGUILayout.Toggle("Нижний захват сжат", instance.IsLowerGripClamped);
    }

    private void DrawVSMState(SystemStateMonitor instance)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("VSM State", EditorStyles.boldLabel);
        EditorGUILayout.Toggle("Открыто меню категорий", instance.IsDropdownMenuActive);
        EditorGUILayout.Toggle("Дверь закрыта", instance.AreDoorsClosed);
        EditorGUILayout.Toggle("Экстензометр добавлен", instance.IsExtensometerEnabledByUser);
    }

    private void DrawMachineState(SystemStateMonitor instance)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Machine State", EditorStyles.boldLabel);
        EditorGUILayout.TextField("Положение траверсы", instance.CurrentTraverseY.ToString("F4"));
        EditorGUILayout.TextField("Точка подвода траверсы", instance.LastApproachTargetZ.ToString("F4"));
        EditorGUILayout.Toggle("Траверса двигается вручную", instance.IsTraverseMovingManually);

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("ЛИМИТЫ", EditorStyles.miniBoldLabel);
        EditorGUILayout.Toggle("Динамические лимиты активны", instance.IsDynamicLimitsActive);
        EditorGUILayout.TextField("Текущие лимиты (Мин/Макс)", $"{instance.CurrentMinTraverseLimitY:F4} / {instance.CurrentMaxTraverseLimitY:F4}");
        EditorGUILayout.TextField("Стандартные лимиты (Мин/Макс)", $"{instance.OriginMinLimitY:F4} / {instance.OriginMaxLimitY:F4}");
    }

    private void DrawSetupPanelState(SystemStateMonitor instance)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Setup Panel State", EditorStyles.boldLabel);
        EditorGUILayout.TextField("Выбраный шаблон", instance.SelectedTemplateName ?? "None");
        EditorGUILayout.TextField("Материал", instance.SelectedMaterialName ?? "None");
        EditorGUILayout.TextField("Длина захвата оснастки", instance.CurrentClampingLength.ToString("F2"));
        EditorGUILayout.TextField("Форма сечения образца", instance.SelectedShape.ToString());
        EditorGUILayout.TextField("Выбранный тип скорости", instance.SelectedSpeedMode.ToString());
        EditorGUILayout.TextField("Площадь сечения", instance.CalculatedArea.ToString("F2"));
        EditorGUILayout.Toggle("Панель настройки образца", instance.IsSetupPanelValid);

        if (instance.CurrentSampleParameters != null && instance.CurrentSampleParameters.Count > 0)
        {
            var sb = new StringBuilder();
            foreach (var kvp in instance.CurrentSampleParameters)
            {
                sb.Append($"{kvp.Key}: {kvp.Value:F2}; ");
            }
            EditorGUILayout.LabelField("Параметры образца", sb.ToString());
        }
        else
        {
            EditorGUILayout.LabelField("Параметры образца", "Не установлены");
        }

        string materialAssetName = instance.SelectedMaterial != null ? instance.SelectedMaterial.name : "None";
        EditorGUILayout.TextField("Source Material Asset", materialAssetName);
    }

    private void DrawGraphControllerState(SystemStateMonitor instance)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Graph Controller State", EditorStyles.boldLabel);
        EditorGUILayout.TextField("Состояние теста", instance.CurrentGraphState.ToString());

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Live Values", EditorStyles.miniBoldLabel);
        EditorGUILayout.TextField("Удлиннение (%)", instance.CurrentRelativeStrain_Percent.ToString("F3"));
        EditorGUILayout.TextField("Приложенное усилие (kN)", instance.CurrentForce_kN.ToString("F3"));

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Key Points (Test Constants)", EditorStyles.miniBoldLabel);
        EditorGUILayout.TextField("Предел прочности (%)", instance.UTS_RelativeStrain_Percent.ToString("F3"));
        EditorGUILayout.TextField("Точка разрыва (%)", instance.Rupture_RelativeStrain_Percent.ToString("F3"));
        EditorGUILayout.TextField("Лимит пропорциональности (kN)", instance.ProportionalityLimit_kN.ToString("F3"));
        EditorGUILayout.TextField("Максимальная нагрузка (kN)", instance.MaxForceInTest_kN.ToString("F3"));

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Events", EditorStyles.miniBoldLabel);
        EditorGUILayout.Toggle("Установка экстензометра", instance.WasExtensometerAttachRequested);
        EditorGUILayout.Toggle("Удаление экстензометра", instance.WasExtensometerRemoveRequested);
    }

    private void DrawProcessesState(SystemStateMonitor instance)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Prompt Panel State (Подсказки)", EditorStyles.boldLabel);
        GUI.enabled = false; // Делаем все тогглы ниже read-only
        EditorGUILayout.Toggle("Панель свернута", instance.IsPromptPanelCollapsed);
        EditorGUILayout.TextField("Текущий ключ подсказки", instance.CurrentPromptKey ?? "None");

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Процессы", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.ToggleLeft("Смена оснастки", instance.IsFixtureChangeInProgress);
        EditorGUILayout.ToggleLeft("Подвод траверсы", instance.IsApproachInProgress);
        EditorGUILayout.ToggleLeft("Scenario", instance.IsScenarioExecuting);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.ToggleLeft("Работа захватов", instance.IsClampAnimating);
        EditorGUILayout.ToggleLeft("Масляная подушка", instance.IsHydraulicBufferReady);
        EditorGUILayout.ToggleLeft("Масляный насос", instance.IsPowerUnitActive);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.ToggleLeft("Образец разгружен", instance.IsSampleUnloaded);
        EditorGUILayout.ToggleLeft("Траверса в позиции", instance.IsTraverseAtTarget);
        
        EditorGUILayout.EndHorizontal();
        
        GUI.enabled = true; // Возвращаем интерактивность
    }

    private void DrawFixtureStatusSection(SystemStateMonitor instance)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Fixture State (Установленные/Необходимые)", EditorStyles.boldLabel);
        DrawFixtureStatusList(instance);
    }

    private void DrawFixtureStatusList(SystemStateMonitor monitor)
    {
        var required = monitor.FixturesRequiredForTest ?? new List<string>();
        var installed = monitor.AllInstalledFixtureIDs ?? new List<string>();

        var allUniqueIds = new HashSet<string>(installed);
        allUniqueIds.UnionWith(required);

        var sortedIds = allUniqueIds.ToList();
        sortedIds.Sort();

        if (sortedIds.Count == 0)
        {
            EditorGUILayout.LabelField("(No fixtures installed or required)");
            return;
        }

        EditorGUI.indentLevel++;

        foreach (string id in sortedIds)
        {
            bool isInstalled = installed.Contains(id);
            bool isRequired = required.Contains(id);

            string prefix;
            string suffix = "";
            Color color;

            if (isInstalled && isRequired)
            {
                prefix = "[✓]";
                suffix = " (Установлен, используется)";
                color = new Color(0.6f, 1.0f, 0.6f);
            }
            else if (isInstalled && !isRequired)
            {
                prefix = "[ ]";
                suffix = " (Установлен, не требуется)";
                color = new Color(1.0f, 1.0f, 0.6f);
            }
            else
            {
                prefix = "[ ]";
                suffix = " (Необходимо установить)";
                color = new Color(1.0f, 0.6f, 0.6f);
            }

            var style = new GUIStyle(EditorStyles.label);
            style.normal.textColor = color;

            EditorGUILayout.LabelField(prefix + " " + id + suffix, style);
        }

        EditorGUI.indentLevel--;
    }
}