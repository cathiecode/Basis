using System;
using System.Collections.Generic;
using System.IO;
using Basis;
using Basis.Network.Core;
using UnityEngine;

namespace com.superneko.basis.masque.cilbox
{
    [Cilboxable]
    public class MasqueFaceDriver : MonoBehaviour
    {
        public SkinnedMeshRenderer FaceMesh;
        public float minusSpeed = -1;

        public int[] ControllingBlendShapeIndice;
        public float[] PackedExpressionsBlendShapeWeights;

        bool initialized = false;
        BasisNetworkShim _networkShim;
        float[] _currentExpressionBlendShapeWeights;

        public void Start()
        {
            Debug.Log($"[Masque] Face driver start for {gameObject.name}");

            _networkShim = SafeUtil.MakeNetworkable(this);
            _networkShim.NetworkMessageReceived += OnNetworkMessageReceived;

            _currentExpressionBlendShapeWeights = new float[ControllingBlendShapeIndice.Length];
            DeployExpression(0);

            initialized = true;
        }

        public void Update()
        {
            // Always initialized i think

            // Stack is always faster than field
            var controllingBlendShapeIndices = ControllingBlendShapeIndice;
            var controllingBlendShapeIndicesLength = controllingBlendShapeIndices.Length;
            var expressionBlendshapeWeights = _currentExpressionBlendShapeWeights;
            var faceMesh = FaceMesh;
            
            var t = 1 - Mathf.Exp(Time.deltaTime * minusSpeed);

            for (var i = 0; i < controllingBlendShapeIndicesLength; i++)
            {
                var blendShapeIndex = controllingBlendShapeIndices[i];
                var weightPrev = faceMesh.GetBlendShapeWeight(blendShapeIndex);
                var weightNext = expressionBlendshapeWeights[i];

                // Exponential smoothing: https://lisyarus.github.io/blog/posts/exponential-smoothing.html
                faceMesh.SetBlendShapeWeight(blendShapeIndex, weightPrev + (weightNext - weightPrev) * t);
            }
        }

        public void SetExpression(byte expressionIndex)
        {
            if (!initialized) return;

            DeployExpression(expressionIndex);

            if (_networkShim.HasNetworkID)
            {
                _networkShim.SendCustomNetworkEvent(new byte[] { expressionIndex });
            }
        }

        public void OnNetworkMessageReceived(ushort player, byte[] message, DeliveryMethod method)
        {
            if (!initialized) return;

            if (message.Length == 0) return;

            DeployExpression(message[0]);
        }

        public void DeployExpression(byte expressionIndex)
        {
            Array.Copy(
                PackedExpressionsBlendShapeWeights, ControllingBlendShapeIndice.Length * expressionIndex,
                _currentExpressionBlendShapeWeights, 0, ControllingBlendShapeIndice.Length
            );
        }
    }
}
