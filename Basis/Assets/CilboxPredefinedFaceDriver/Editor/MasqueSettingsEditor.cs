using System;
using System.Collections.Generic;
using com.superneko.basis.masque.cilbox;
using com.superneko.basis.masque.native;
using UnityEditor;
using UnityEngine;

namespace com.superneko.basis.masque.editor
{
    [CustomEditor(typeof(MasqueSettings))]
    public class MasqueSettingsEditor : Editor
    {
        const float BLENDSHAPE_EPSILON = 0.01f;
        string CompileError = "";

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            base.OnInspectorGUI();

            if (EditorGUI.EndChangeCheck()) OnValidate();

            if (GUILayout.Button("Recompile"))
            {
                OnValidate();
            }

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextArea(CompileError);
            EditorGUI.EndDisabledGroup();
        }

        void OnValidate()
        {
            if (target is not MasqueSettings component) return;

            MakeValid(component);
            Setup(component);
        }

        public void MakeValid(MasqueSettings component)
        {
            var changed = false;

            if (component.FaceSet == null)
            {
                component.FaceSet = new FaceSet();
                changed = true;
            }

            if (component.FaceSet.Expressions == null)
            {
                component.FaceSet.Expressions = new();
                changed = true;
            }

            // ID Duplication check
            var idSet = new HashSet<int>();

            foreach (var expression in component.FaceSet.Expressions)
            {
                if (idSet.TryGetValue(expression.ReferenceId, out var _))
                {
                    // duplication. reroll.

                    // TODO: Better id generation
                    int newId;
                    while (true)
                    {
                        newId = UnityEngine.Random.Range(0, int.MaxValue);
                        if (!idSet.TryGetValue(newId, out var _)) break;
                    }

                    expression.ReferenceId = newId;
                    changed = true;

                    idSet.Add(newId);
                }
                else
                {
                    idSet.Add(expression.ReferenceId);
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(this);
            }
        }

        public void Setup(MasqueSettings component)
        {
            var faceDriver = component.GetComponent<MasqueFaceDriver>();
            var ownerRuntime = component.GetComponent<MasqueOwnerRuntime>();

            CompileError = "No setup";

            try
            {
                var serializableSet = MasqueFaceSetCompiler.Compile(component.transform, component.FaceSkinnedMeshRenderer, component.FaceSet);

                faceDriver.FaceMesh = component.FaceSkinnedMeshRenderer;
                faceDriver.ControllingBlendShapeIndice = serializableSet.ControllingBlendShapes;
                faceDriver.MinusSpeed = -component.speed;
                faceDriver.ConvergenceTime = Mathf.Log(BLENDSHAPE_EPSILON) / -component.speed;

                var packedWeights = new float[faceDriver.ControllingBlendShapeIndice.Length * (serializableSet.Expressions.Length + 1)];

                var defaultExpression = serializableSet.DefaultExpression;

                Array.Copy(
                    defaultExpression.BlendshapeWeights, 0,
                    packedWeights, 0, faceDriver.ControllingBlendShapeIndice.Length
                );

                for (var i = 0; i < serializableSet.Expressions.Length; i++)
                {
                    var expression = serializableSet.Expressions[i];

                    Array.Copy(
                        expression.BlendshapeWeights, 0,
                        packedWeights, faceDriver.ControllingBlendShapeIndice.Length * (i + 1), faceDriver.ControllingBlendShapeIndice.Length
                    );
                }

                faceDriver.PackedExpressionsBlendShapeWeights = packedWeights;

                ownerRuntime.FaceSet = serializableSet;

                EditorUtility.SetDirty(faceDriver);
                EditorUtility.SetDirty(ownerRuntime);

                CompileError = "No error.";
            }
            catch (Exception e)
            {
                CompileError = e.ToString();
            }
        }
    }
}
