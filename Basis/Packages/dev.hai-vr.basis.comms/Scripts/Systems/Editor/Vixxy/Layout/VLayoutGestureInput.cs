using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HVR.Vixxy.Editor
{
    internal class VGestureInput
    {
        private readonly SNVixxyGestureInput my;
        private readonly SerializedObject serializedObject;

        internal VGestureInput(SNVixxyGestureInputEditor editor)
        {
            my = (SNVixxyGestureInput)editor.target;
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

                EditorGUILayout.LabelField("LeftMask / LeftCurled / RightMask / RightCurled");

                EditorGUILayout.BeginHorizontal();

                rule.leftFingerMask = GestureField(rule.leftFingerMask);
                rule.leftFingerCurled = GestureField(rule.leftFingerCurled);
                rule.rightFingerMask = GestureField(rule.rightFingerMask);
                rule.rightFingerCurled = GestureField(rule.rightFingerCurled);

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
                menu.AddItem(new GUIContent("Add single rule"), false, AddLeftHandBasicRuleSet);

                menu.ShowAsContext();
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(SNVixxyGestureInput.remember)));

            if (my.remember == HVRVixxyRememberScope.RememberInThisTag)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(SNVixxyGestureInput.rememberTag)));
            }

            EditorGUILayout.Separator();

            return false;
        }

        public void AddLeftHandBasicRuleSet()
        {
            var mask = SNVixxyGestureInput.Fingers.Middle | SNVixxyGestureInput.Fingers.Index | SNVixxyGestureInput.Fingers.Thumb;

            my.rules = my.rules.Concat(new[]
            {
                new SNVixxyGestureInput.SNGestureInputRule() { leftFingerMask = mask, rightFingerMask = 0 },
                new SNVixxyGestureInput.SNGestureInputRule() { leftFingerMask = mask, rightFingerMask = 0 },
                new SNVixxyGestureInput.SNGestureInputRule() { leftFingerMask = mask, rightFingerMask = 0 }
            }).ToArray();

            Undo.RecordObject(my, "Add ruleset");
        }

        public void AddRule()
        {
            my.rules = my.rules.Concat(new[]
            {
                new SNVixxyGestureInput.SNGestureInputRule
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

        static Dictionary<int, SNVixxyGestureInput.Fingers> NextGestureFieldValue = new();

        static KeyValuePair<SNVixxyGestureInput.Fingers, string>[] PredefinedGesturesPair = new[] {
            KeyValuePair.Create((SNVixxyGestureInput.Fingers)0b00000, "Open"),
            KeyValuePair.Create((SNVixxyGestureInput.Fingers)0b00001, "Stop"),
            KeyValuePair.Create((SNVixxyGestureInput.Fingers)0b00011, "Ok"),
            KeyValuePair.Create((SNVixxyGestureInput.Fingers)0b11100, "Gun"),
            KeyValuePair.Create((SNVixxyGestureInput.Fingers)0b01101, "Fox"),
            KeyValuePair.Create((SNVixxyGestureInput.Fingers)0b11110, "ThumbsUp"),
            KeyValuePair.Create((SNVixxyGestureInput.Fingers)0b11111, "Fist"),
        };

        static Dictionary<SNVixxyGestureInput.Fingers, string> PredefinedGestures = new(PredefinedGesturesPair);

        private SNVixxyGestureInput.Fingers GestureField(SNVixxyGestureInput.Fingers value)
        {
            var id = GUIUtility.GetControlID(FocusType.Passive);

            if (!PredefinedGestures.TryGetValue(value, out var title))
            {
                var titleBuilder = new StringBuilder();

                foreach (var finger in Enum.GetValues(typeof(SNVixxyGestureInput.Fingers)))
                {
                    if ((value & (SNVixxyGestureInput.Fingers)finger) > 0)
                    {
                        if (titleBuilder.Length == 0)
                        {
                            titleBuilder.Append(Enum.GetName(typeof(SNVixxyGestureInput.Fingers), (SNVixxyGestureInput.Fingers)finger));
                        } else
                        {
                            titleBuilder.Append($",{Enum.GetName(typeof(SNVixxyGestureInput.Fingers), (SNVixxyGestureInput.Fingers)finger)}");
                        }
                    }
                }

                title = titleBuilder.ToString();
            }

            if (EditorGUILayout.DropdownButton(new GUIContent(title), FocusType.Keyboard))
            {
                var menu = new GenericMenu();

                foreach (var gesture in PredefinedGesturesPair)
                {
                    menu.AddItem(new GUIContent(gesture.Value), value == gesture.Key, () => { NextGestureFieldValue[id] = gesture.Key; });
                }

                menu.AddSeparator("");

                menu.AddItem(new GUIContent("Thumb"), (value & SNVixxyGestureInput.Fingers.Thumb) > 0, () => { NextGestureFieldValue[id] = value ^ SNVixxyGestureInput.Fingers.Thumb; });
                menu.AddItem(new GUIContent("Index"), (value & SNVixxyGestureInput.Fingers.Index) > 0, () => { NextGestureFieldValue[id] = value ^ SNVixxyGestureInput.Fingers.Index; });
                menu.AddItem(new GUIContent("Middle"), (value & SNVixxyGestureInput.Fingers.Middle) > 0, () => { NextGestureFieldValue[id] = value ^ SNVixxyGestureInput.Fingers.Middle; });
                menu.AddItem(new GUIContent("Ring"), (value & SNVixxyGestureInput.Fingers.Ring) > 0, () => { NextGestureFieldValue[id] = value ^ SNVixxyGestureInput.Fingers.Ring; });
                menu.AddItem(new GUIContent("Little"), (value & SNVixxyGestureInput.Fingers.Little) > 0, () => { NextGestureFieldValue[id] = value ^ SNVixxyGestureInput.Fingers.Little; });

                menu.ShowAsContext();
            }

            if (NextGestureFieldValue.TryGetValue(id, out var nextValue))
            {
                NextGestureFieldValue.Remove(id);
                return nextValue;
            }

            return value;
        }
    }
}
