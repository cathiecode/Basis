using System;
using System.Collections.Generic;
using System.Linq;
using com.superneko.basis.masque.cilbox;
using Mono.Cecil.Cil;
using UnityEditor;
using UnityEngine;

namespace com.superneko.basis.masque.native
{
    public static class MasqueFaceSetCompiler
    {
        public static SerializableFaceSet Compile(Transform animationRoot, SkinnedMeshRenderer smr, FaceSet faceSet)
        {
            var facePath = AnimationUtility.CalculateTransformPath(smr.transform, animationRoot);
            var faceMesh = smr.sharedMesh;

            var animatedBlendShapeIndiceSet = new HashSet<int>();

            // Collect animated blendshape indice
            if (faceSet.DefaultExpressionAnimationClip != null)
            {
                var weights = GetClipBlendshapeWeights(facePath, faceMesh, faceSet.DefaultExpressionAnimationClip);

                foreach (var weight in weights)
                {
                    animatedBlendShapeIndiceSet.Add(weight.Key);
                }
            }

            foreach (var expression in faceSet.Expressions)
            {
                if (expression.AnimationClip == null) continue;

                var weights = GetClipBlendshapeWeights(facePath, faceMesh, expression.AnimationClip);

                foreach (var weight in weights)
                {
                    animatedBlendShapeIndiceSet.Add(weight.Key);
                }
            }

            int[] animatedBlendShapeIndice = animatedBlendShapeIndiceSet.ToArray();
            Array.Sort(animatedBlendShapeIndice);

            var serializedExpressions = new List<SerializableExpression>();

            // Default face expression
            SerializableExpression defaultFaceExpression;
            if (faceSet.DefaultExpressionAnimationClip != null)
            {
                var weights = GetClipBlendshapeWeights(facePath, faceMesh, faceSet.DefaultExpressionAnimationClip);

                var serializedWeights = new float[animatedBlendShapeIndice.Length];

                for (var i = 0; i < serializedWeights.Length; i++)
                {
                    if (weights.TryGetValue(animatedBlendShapeIndice[i], out var weight))
                    {
                        serializedWeights[i] = weight;
                    }
                    else
                    {
                        serializedWeights[i] = 0;
                    }
                }

                defaultFaceExpression = new SerializableExpression() { Tags = new string[] { }, ReferenceId = 999999, blendshapeWeights = serializedWeights };
            }
            else
            {
                // No clip specified. collect initial weights.
                var serializedWeights = new float[animatedBlendShapeIndice.Length];

                for (var i = 0; i < serializedWeights.Length; i++)
                {
                    var weight = smr.GetBlendShapeWeight(animatedBlendShapeIndice[i]);

                    serializedWeights[i] = weight;
                }

                defaultFaceExpression = new SerializableExpression() { Tags = new string[] { }, ReferenceId = 999999, blendshapeWeights = serializedWeights };
            }

            // Actually serialize expressions
            foreach (var expression in faceSet.Expressions)
            {
                var tags = expression.Tags ?? new string[] { };

                if (expression.AnimationClip == null)
                {
                    // Animation clip is null. add empty.
                    serializedExpressions.Add(new SerializableExpression() { Tags = tags, blendshapeWeights = CopiedArray(defaultFaceExpression.blendshapeWeights) });
                    continue;
                }

                var weights = GetClipBlendshapeWeights(facePath, faceMesh, expression.AnimationClip);

                var serializedWeights = CopiedArray(defaultFaceExpression.blendshapeWeights);

                for (var i = 0; i < animatedBlendShapeIndice.Length; i++)
                {
                    if (weights.TryGetValue(animatedBlendShapeIndice[i], out var weight))
                    {
                        serializedWeights[i] = weight;
                    }
                    else
                    {
                        serializedWeights[i] = 0;
                    }
                }

                serializedExpressions.Add(new SerializableExpression() { Tags = tags, ReferenceId = expression.ReferenceId, blendshapeWeights = serializedWeights });
            }

            return new SerializableFaceSet()
            {
                controllingBlendShapes = animatedBlendShapeIndice,
                defaultExpression = defaultFaceExpression,
                expressions = serializedExpressions.ToArray(),
            };
        }

        static Dictionary<int, float> GetClipBlendshapeWeights(string facePath, Mesh faceMesh, AnimationClip animationClip)
        {
            var dic = new Dictionary<int, float>();

            var curveBindings = AnimationUtility.GetCurveBindings(animationClip);

            foreach (var curveBinding in curveBindings)
            {
                if (curveBinding.path != facePath) continue;
                if (curveBinding.type != typeof(SkinnedMeshRenderer)) continue;
                if (!curveBinding.propertyName.StartsWith("blendShape.")) continue;

                var animationCurve = AnimationUtility.GetEditorCurve(animationClip, curveBinding);

                var blendShapeName = curveBinding.propertyName["blendShape.".Length..];
                var blendShapeIndex = faceMesh.GetBlendShapeIndex(blendShapeName);

                var value = animationCurve.Evaluate(0);

                dic.Add(blendShapeIndex, value);
            }

            // TODO: Warn empty

            return dic;
        }

        static T[] CopiedArray<T>(T[] src)
        {
            var newArray = new T[src.Length];

            return newArray;
        }
    }
}
