using System;
using System.Collections.Generic;
using com.superneko.basis.masque.cilbox;
using UnityEditor;
using UnityEngine;

namespace com.superneko.basis.masque.native
{
    public class MasqueSettings : MonoBehaviour
    {
        public SkinnedMeshRenderer FaceSkinnedMeshRenderer;
        public FaceSet FaceSet;
        public float speed = 1;

        [NonSerialized] public string CompileError;

        void OnValidate()
        {
            MakeValid();
            Setup();
        }

        public void MakeValid()
        {
            var changed = false;

            if (FaceSet == null)
            {
                FaceSet = new FaceSet();
                changed = true;
            }

            if (FaceSet.Expressions == null)
            {
                FaceSet.Expressions = new();
                changed = true;
            }

            // ID Duplication check
            var idSet = new HashSet<int>();

            foreach (var expression in FaceSet.Expressions)
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

        public void Setup()
        {
            var faceDriver = GetComponent<MasqueFaceDriver>();
            var ownerRuntime = GetComponent<MasqueOwnerRuntime>();

            CompileError = "";

            try
            {
                var serializableSet = MasqueFaceSetCompiler.Compile(transform, FaceSkinnedMeshRenderer, FaceSet);

                faceDriver.FaceMesh = FaceSkinnedMeshRenderer;
                faceDriver.ControllingBlendShapeIndice = serializableSet.controllingBlendShapes;
                faceDriver.minusSpeed = -speed;

                var packedWeights = new float[faceDriver.ControllingBlendShapeIndice.Length * (serializableSet.expressions.Length + 1)];

                var defaultExpression = serializableSet.defaultExpression;

                Array.Copy(
                    defaultExpression.blendshapeWeights, 0,
                    packedWeights, 0, faceDriver.ControllingBlendShapeIndice.Length
                );

                for (var i = 0; i < serializableSet.expressions.Length; i++)
                {
                    var expression = serializableSet.expressions[i];

                    Array.Copy(
                        expression.blendshapeWeights, 0,
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
