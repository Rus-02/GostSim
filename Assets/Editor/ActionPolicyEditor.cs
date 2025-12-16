using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Collections.Generic;

[CustomEditor(typeof(ActionPolicy))]
public class ActionPolicyEditor : Editor
{
    private ActionPolicy policy;
    private SerializedProperty selectedHintProperty;
    private SerializedProperty selectedTestTypesProperty;

    private EventType? selectedActionForEditing;
    private Vector2 scrollPosition;    

    private void OnEnable()
    {
        policy = (ActionPolicy)target;
        ApplicationStateManager.OnStateChanged += Repaint;
    }

    private void OnDisable()
    {
        ApplicationStateManager.OnStateChanged -= Repaint;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Матрица Доступа: [Действие] / [Состояние]", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Двойной клик для смены статуса (✔ / ✖ / ✱).\n✔-Разрешено, ✖-Запрещено для всех, ✱-Запрещено для выбранных.\nОдин клик по ✖ или ✱ для редактирования.", MessageType.Info);

        var eventTypes = Enum.GetValues(typeof(EventType)).Cast<EventType>().ToList();
        var testStates = Enum.GetValues(typeof(TestState)).Cast<TestState>().ToList();

        TestState? currentAppState = null;
        EventType? currentAction = null;
        if (Application.isPlaying && ApplicationStateManager.Instance != null)
        {
            currentAppState = ApplicationStateManager.Instance.currentState;
            currentAction = ApplicationStateManager.Instance.lastAction;
        }
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("", GUILayout.Width(150));
        foreach (var state in testStates)
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
            if (currentAppState.HasValue && currentAppState.Value == state)
            {
                headerStyle.normal.textColor = Color.cyan;
            }
            EditorGUILayout.LabelField(new GUIContent(state.ToString(), "Состояние системы"), headerStyle, GUILayout.Width(100), GUILayout.MinWidth(100));
        }
        EditorGUILayout.EndHorizontal();

        foreach (var action in eventTypes)
        {
            EditorGUILayout.BeginHorizontal();
            
            GUIStyle actionLabelStyle = new GUIStyle(GUI.skin.label);
            if (currentAction.HasValue && currentAction.Value == action)
            {
                actionLabelStyle.fontStyle = FontStyle.Bold;
                actionLabelStyle.normal.textColor = Color.cyan;
            }
            EditorGUILayout.LabelField(new GUIContent(action.ToString(), "Тип действия"), actionLabelStyle, GUILayout.Width(150));

            foreach (var state in testStates)
            {
                DrawCell(action, state, currentAppState, currentAction);
            }
            EditorGUILayout.EndHorizontal();

            if (selectedActionForEditing.HasValue && selectedActionForEditing.Value == action && selectedHintProperty != null)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(155);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.LabelField("Текст подсказки:");
                EditorGUILayout.PropertyField(selectedHintProperty, GUIContent.none, GUILayout.MinHeight(40));

                if (selectedTestTypesProperty != null && selectedTestTypesProperty.isArray && selectedTestTypesProperty.arraySize > 0)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Ограничить для тестов:");
                    EditorGUILayout.PropertyField(selectedTestTypesProperty, true);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.EndScrollView();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawCell(EventType action, TestState state, TestState? currentAppState, EventType? currentAction)
    {
        var rule = policy.rules.FirstOrDefault(r => r.actionType == action);
        var restriction = rule?.stateRestrictions.FirstOrDefault(sr => sr.restrictedState == state);

        string content = "✔";
        GUI.backgroundColor = new Color(0.6f, 1, 0.6f);

        if (restriction != null)
        {
            bool isSpecific = restriction.restrictedTestTypes != null && restriction.restrictedTestTypes.Count > 0;
            if (isSpecific)
            {
                content = "✱";
                GUI.backgroundColor = new Color(1, 0.9f, 0.4f);
            }
            else
            {
                content = "✖";
                GUI.backgroundColor = new Color(1, 0.6f, 0.6f);
            }
        }

        bool isCurrentState = currentAppState.HasValue && currentAppState.Value == state;
        bool isCurrentAction = currentAction.HasValue && currentAction.Value == action;

        Color originalBgColor = GUI.backgroundColor;

        if (isCurrentState && isCurrentAction)
        {
            GUI.backgroundColor = Color.Lerp(originalBgColor, Color.green, 0.95f); 
        }
        else if (isCurrentState || isCurrentAction)
        {
            GUI.backgroundColor = Color.Lerp(originalBgColor, Color.yellow, 0.85f);
        }

        GUILayout.Box(new GUIContent(content), GUILayout.Width(100), GUILayout.Height(25));
        
        GUI.backgroundColor = originalBgColor;

        Rect cellRect = GUILayoutUtility.GetLastRect();

        Event currentEvent = Event.current;
        // ----- ВОТ ИСПРАВЛЕНИЕ -----
        if (cellRect.Contains(currentEvent.mousePosition) && currentEvent.type == UnityEngine.EventType.MouseDown)
        {
            if (currentEvent.clickCount == 2)
            {
                ToggleRestriction(action, state, rule, restriction);
            }
            else if (currentEvent.clickCount == 1)
            {
                if (restriction != null)
                {
                    SelectForEditing(action, state);
                }
                else
                {
                    selectedActionForEditing = null;
                    selectedHintProperty = null;
                    selectedTestTypesProperty = null;
                }
            }
            currentEvent.Use();
        }
    }
    
    private void ToggleRestriction(EventType action, TestState state, ActionPolicy.ActionRule rule, ActionPolicy.StateRestriction restriction)
    {
        selectedActionForEditing = null;
        selectedHintProperty = null;
        selectedTestTypesProperty = null;

        Undo.RecordObject(policy, "Toggle Action Restriction");

        if (restriction == null)
        {
            if (rule == null)
            {
                rule = new ActionPolicy.ActionRule { actionType = action, stateRestrictions = new List<ActionPolicy.StateRestriction>() };
                policy.rules.Add(rule);
            }
            var newRestriction = new ActionPolicy.StateRestriction { restrictedState = state };
            rule.stateRestrictions.Add(newRestriction);
        }
        else
        {
            bool isSpecific = restriction.restrictedTestTypes != null && restriction.restrictedTestTypes.Count > 0;
            if (isSpecific)
            {
                rule.stateRestrictions.Remove(restriction);
                if (rule.stateRestrictions.Count == 0) policy.rules.Remove(rule);
            }
            else
            {
                restriction.restrictedTestTypes.Add(default(TypeOfTest));
            }
        }
        EditorUtility.SetDirty(policy);
    }

    private void SelectForEditing(EventType action, TestState state)
    {
        selectedActionForEditing = null;
        selectedHintProperty = null;
        selectedTestTypesProperty = null;

        for (int i = 0; i < policy.rules.Count; i++)
        {
            if (policy.rules[i].actionType == action)
            {
                var restrictions = policy.rules[i].stateRestrictions;
                for (int j = 0; j < restrictions.Count; j++)
                {
                    if (restrictions[j].restrictedState == state)
                    {
                        var ruleProp = serializedObject.FindProperty("rules").GetArrayElementAtIndex(i);
                        var restrictionProp = ruleProp.FindPropertyRelative("stateRestrictions").GetArrayElementAtIndex(j);

                        selectedHintProperty = restrictionProp.FindPropertyRelative("hintMessage");
                        selectedTestTypesProperty = restrictionProp.FindPropertyRelative("restrictedTestTypes");

                        selectedActionForEditing = action;
                        return;
                    }
                }
            }
        }
    }
}