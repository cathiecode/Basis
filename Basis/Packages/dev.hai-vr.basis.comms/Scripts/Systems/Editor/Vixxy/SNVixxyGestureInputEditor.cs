using HVR.Basis.Comms.Editor;
using UnityEditor;
using UnityEngine;

namespace HVR.Vixxy.Editor
{
    [CustomEditor(typeof(SNVixxyGestureInput))]
    public class SNVixxyGestureInputEditor : UnityEditor.Editor
    {
        private VGestureInput _gestureInput;

        public static bool _creatorViewFoldout;

        internal const string CreatorView = "Creator View";

        private void OnEnable()
        {
            _gestureInput = new VGestureInput(this);
        }

        public override void OnInspectorGUI()
        {
            var my = (SNVixxyGestureInput)target;
            HVRAvatarCommsEditor.EnsureAvatarHasPrefab(my.transform);

            var isPlaying = Application.isPlaying;
            if (isPlaying)
            {
                EditorGUILayout.HelpBox(HVRVixxyLocalizationPhrase.MsgCannotEditInPlayMode, MessageType.Warning);
            }

            var anyChanged = false;
            if (_gestureInput.LayoutMenu()) return;

            _creatorViewFoldout = HaiEFCommon.LilFoldout(CreatorView, "", _creatorViewFoldout, ref anyChanged);
            if (_creatorViewFoldout)
            {
                if (_gestureInput.LayoutCreatorView()) return;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
