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

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.EndHorizontal();

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

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(SNVixxyKeyboardInput.remember)));
            if (my.remember == HVRVixxyRememberScope.RememberInThisTag)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(SNVixxyKeyboardInput.rememberTag)));
            }
            EditorGUILayout.Separator();

            return false;
        }
    }
}
