using com.superneko.basis.masque.native;
using UnityEditor;
using UnityEngine;

namespace com.superneko.basis.masque.editor
{
    [CustomEditor(typeof(MasqueSettings))]
    public class MasqueSettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (target is not MasqueSettings component) return;

            if (GUILayout.Button("Recompile"))
            {
                component.MakeValid();
                component.Setup();
            }

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextArea(component.CompileError);
            EditorGUI.EndDisabledGroup();
        }
    }
}
