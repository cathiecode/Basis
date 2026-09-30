using HVR.Basis.Comms.Editor;
using UnityEditor;
using UnityEngine;

namespace HVR.Vixxy.Editor
{
    [CustomEditor(typeof(SNVixxyKeyboardInput))]
    public class SNVixxyKeyboardInputEditor : UnityEditor.Editor
    {
        private VKeyboardInput _keyboardInput;

        public static bool _creatorViewFoldout;

        internal const string CreatorView = "Creator View";

        private void OnEnable()
        {
            _keyboardInput = new VKeyboardInput(this);
        }

        public override void OnInspectorGUI()
        {
            var my = (SNVixxyKeyboardInput)target;
            HVRAvatarCommsEditor.EnsureAvatarHasPrefab(my.transform);

            var isPlaying = Application.isPlaying;
            if (isPlaying)
            {
                EditorGUILayout.HelpBox(HVRVixxyLocalizationPhrase.MsgCannotEditInPlayMode, MessageType.Warning);
            }

            var anyChanged = false;
            if (_keyboardInput.LayoutMenu()) return;

            _creatorViewFoldout = HaiEFCommon.LilFoldout(CreatorView, "", _creatorViewFoldout, ref anyChanged);
            if (_creatorViewFoldout)
            {
                if (_keyboardInput.LayoutCreatorView()) return;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
