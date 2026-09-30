using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HVR.Vixxy.Editor
{
    internal class VKeyboardInput
    {
        private readonly SNVixxyKeyboardInput my;
        private readonly SerializedObject serializedObject;

        internal VKeyboardInput(SNVixxyKeyboardInputEditor editor)
        {
            my = (SNVixxyKeyboardInput)editor.target;
            serializedObject = editor.serializedObject;
        }

        public bool LayoutCreatorView()
        {
            var controlsOnThis = my.GetComponents<HVRVixxyControl>();

            EditorGUILayout.Separator();


            if (controlsOnThis.Length == 1)
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField(new GUIContent(HVRVixxyLocalizationPhrase.ControlLabel), controlsOnThis[0], typeof(HVRVixxyControl), true);
                EditorGUI.EndDisabledGroup();
            }
            else
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(SNVixxyKeyboardInput.control)));
            }

            EditorGUILayout.Separator();

            return false;
        }

        public bool LayoutMenu()
        {
            EditorGUILayout.Separator();

            for (var i = 0; i < my.rules.Length; i++)
            {
                var rule = my.rules[i];

                EditorGUILayout.BeginHorizontal();

                rule.key = (Key)EditorGUILayout.EnumPopup(rule.key);
                rule.value = EditorGUILayout.FloatField(rule.value);

                if (GUILayout.Button("↑"))
                {
                    MoveRuleUp(i);
                }

                if (GUILayout.Button("↓"))
                {
                    MoveRuleDown(i);
                }

                if (GUILayout.Button("×"))
                {
                    RemoveRule(i);
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add Rule"))
            {
                var menu = new GenericMenu();

                menu.AddItem(new GUIContent("Add single rule"), false, AddRule);
                menu.AddItem(new GUIContent("Add F1-F12"), false, AddFunctionRuleSet);

                menu.ShowAsContext();
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(SNVixxyKeyboardInput.remember)));

            if (my.remember == HVRVixxyRememberScope.RememberInThisTag)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(SNVixxyKeyboardInput.rememberTag)));
            }

            EditorGUILayout.Separator();

            return false;
        }

        public void AddFunctionRuleSet()
        {
            var functionRules = new SNVixxyKeyboardInput.SNKeyboardInputRule[12];

            for (var i = Key.F1; i <= Key.F12; i++)
            {
                functionRules[i - Key.F1] = new SNVixxyKeyboardInput.SNKeyboardInputRule
                {
                    key = i,
                    value = my.rules.Length + (i - Key.F1)
                };
            }

            my.rules = my.rules.Concat(functionRules).ToArray();

            Undo.RecordObject(my, "Add ruleset");
        }

        public void AddRule()
        {
            my.rules = my.rules.Concat(new[]
            {
                new SNVixxyKeyboardInput.SNKeyboardInputRule
                {
                    value = my.rules.Length
                }
            }).ToArray();

            Undo.RecordObject(my, "Add rule");
        }

        public void RemoveRule(int ruleIndex)
        {
            my.rules = my.rules.Where((_, i) => i != ruleIndex).ToArray();

            Undo.RecordObject(my, "Remove rule");
        }

        public void MoveRuleUp(int fromIndex)
        {
            MoveRule(fromIndex, fromIndex - 1);
        }

        public void MoveRuleDown(int fromIndex)
        {
            MoveRule(fromIndex, fromIndex + 1);
        }

        public void MoveRule(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || toIndex < 0) return;

            if (fromIndex >= my.rules.Length || toIndex >= my.rules.Length) return;

            (my.rules[fromIndex], my.rules[toIndex]) = (my.rules[toIndex], my.rules[fromIndex]);

            Undo.RecordObject(my, "Move rule");
        }
    }
}
