using System;
using System.Collections.Generic;
using System.IO;
using Basis;
using Basis.Network.Core;
using HVR.Vixxy;
using UnityEngine;

namespace com.superneko.basis.masque.cilbox
{
    [Cilboxable]
    public class MasqueFaceDriver : MonoBehaviour
    {
        public SkinnedMeshRenderer FaceMesh;
        public float MinusSpeed = -1;
        public float ConvergenceTime = 10;

        public int[] ControllingBlendShapeIndice;
        public float[] PackedExpressionsBlendShapeWeights;

        public HVRVixxyMenuItem DonorVixxyMenuItem;

        bool initialized = false;
        BasisNetworkShim _networkShim;
        byte _currentExpression = 0;
        float[] _currentExpressionBlendShapeWeights;
        bool _converged = false;
        float _scheduledConvergeTime = 0f;
        Action _periodicAction;

        public void Start()
        {
            Debug.Log($"[Masque] Face driver start for {gameObject.name}");

            _networkShim = SafeUtil.MakeNetworkable(this);

            // We cannot rely on Network message cuz netid of avatar desyncs.
            // _networkShim.NetworkMessageReceived += OnNetworkMessageReceived;

            _currentExpressionBlendShapeWeights = new float[ControllingBlendShapeIndice.Length];

            DeployExpression(0, true);

            _periodicAction = Periodic;

            initialized = true;

            Periodic();
        }

        void Periodic()
        {
            try
            {
                if (initialized)
                {
                    var vixxyValue = DonorVixxyMenuItem.GetValue();

                    if (vixxyValue != _currentExpression)
                    {
                        DeployExpression((byte)vixxyValue, false);
                    }
                }
            }
            finally
            {
                _networkShim.SendCustomEventDelayedSeconds(_periodicAction, 0.25f);
            }
        }

        public void Update()
        {
            // Always initialized i think

            if (_converged) return;

            // Stack is always faster than field
            var controllingBlendShapeIndices = ControllingBlendShapeIndice;
            var controllingBlendShapeIndicesLength = controllingBlendShapeIndices.Length;
            var expressionBlendshapeWeights = _currentExpressionBlendShapeWeights;
            var faceMesh = FaceMesh;

            if (Time.time < _scheduledConvergeTime)
            {
                var t = 1 - Mathf.Exp(Time.deltaTime * MinusSpeed);

                for (var i = 0; i < controllingBlendShapeIndicesLength; i++)
                {
                    var blendShapeIndex = controllingBlendShapeIndices[i];
                    var weightPrev = faceMesh.GetBlendShapeWeight(blendShapeIndex);
                    var weightNext = expressionBlendshapeWeights[i];

                    // Exponential smoothing: https://lisyarus.github.io/blog/posts/exponential-smoothing.html
                    faceMesh.SetBlendShapeWeight(blendShapeIndex, weightPrev + (weightNext - weightPrev) * t);
                }
            }
            else
            {
                for (var i = 0; i < controllingBlendShapeIndicesLength; i++)
                {
                    var blendShapeIndex = controllingBlendShapeIndices[i];
                    var weight = expressionBlendshapeWeights[i];

                    faceMesh.SetBlendShapeWeight(blendShapeIndex, weight);
                }

                _converged = true;
            }
        }

        public void SetExpression(byte expressionIndex)
        {
            if (!initialized) return;

            DeployExpression(expressionIndex, false);

            DonorVixxyMenuItem.ApplyValue((byte)expressionIndex);
        }

        public void OnNetworkMessageReceived(ushort player, byte[] message, DeliveryMethod method)
        {
            if (!initialized) return;
            if (message == null) return;
            if (message.Length == 0) return;

            DeployExpression(message[0], false);
        }

        public void DeployExpression(byte expressionIndex, bool forced)
        {
            if (!forced && expressionIndex == _currentExpression)
            {
                return;
            }

            _currentExpression = expressionIndex;
            _converged = false;
            _scheduledConvergeTime = Time.time + ConvergenceTime;

            Array.Copy(
                PackedExpressionsBlendShapeWeights, ControllingBlendShapeIndice.Length * expressionIndex,
                _currentExpressionBlendShapeWeights, 0, ControllingBlendShapeIndice.Length
            );
        }
    }
}
