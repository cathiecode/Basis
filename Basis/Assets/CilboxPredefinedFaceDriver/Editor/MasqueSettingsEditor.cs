using com.superneko.basis.masque.native;
using UnityEditor;

namespace com.superneko.basis.masque.editor
{
    [CustomEditor(typeof(MasqueSettings))]
    public class MasqueSettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (target is not MasqueSettings component) return;

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextArea(component.CompileError);
            EditorGUI.EndDisabledGroup();
        }
    }
}
