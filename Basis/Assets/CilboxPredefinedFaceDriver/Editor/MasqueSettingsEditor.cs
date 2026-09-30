using System;
using System.Collections.Generic;
using Basis.Scripts.BasisSdk;
using com.superneko.basis.masque.cilbox;
using com.superneko.basis.masque.native;
using HVR.Vixxy;
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

            if (component.AnimationRoot == null)
            {
                var basisAvatar = component.GetComponentInParent<BasisAvatar>();

                if (basisAvatar != null)
                {
                    component.AnimationRoot = basisAvatar.transform;
                }
            }

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

            if (changed)
            {
                EditorUtility.SetDirty(this);
            }
        }

        public void Setup(MasqueSettings component)
        {
            var faceDriver = component.GetComponent<MasqueFaceDriver>();
            var ownerRuntime = component.GetComponent<MasqueOwnerRuntime>();
            var donorMenuItem = component.GetComponent<HVRVixxyMenuItem>();
            var donorControl = component.GetComponent<HVRVixxyControl>();

            CompileError = "No setup";

            try
            {
                var serializableSet = MasqueFaceSetCompiler.Compile(component.AnimationRoot, component.FaceSkinnedMeshRenderer, component.FaceSet);

                faceDriver.FaceMesh = component.FaceSkinnedMeshRenderer;
                faceDriver.ControllingBlendShapeIndice = serializableSet.ControllingBlendShapes;
                faceDriver.MinusSpeed = -component.speed;
                faceDriver.ConvergenceTime = Mathf.Log(BLENDSHAPE_EPSILON) / -component.speed;
                faceDriver.DonorVixxyMenuItem = donorMenuItem;

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

                faceDriver.DonorVixxyMenuItem = donorMenuItem;

                var donorChoices = new HVRVixxyChoiceControl[serializableSet.Expressions.Length + 1];

                donorChoices[0] = new HVRVixxyChoiceControl()
                {
                    title = "Default",
                    value = 0
                };

                for (var i = 0; i < serializableSet.Expressions.Length; i++)
                {
                    donorChoices[i + 1] = new HVRVixxyChoiceControl()
                    {
                        title = serializableSet.Expressions[i].Title,
                        value = i + 1
                    };
                }

                donorControl.defaultValue = 0;
                donorControl.choices = donorChoices;

                EditorUtility.SetDirty(faceDriver);
                EditorUtility.SetDirty(ownerRuntime);
                EditorUtility.SetDirty(donorControl);

                CompileError = "No error.";
            }
            catch (Exception e)
            {
                CompileError = e.ToString();
            }
        }
    }
}
