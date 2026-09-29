using System;
using System.Collections;
using System.Collections.Generic;
using Basis.Scripts.BasisSdk;
using UnityEngine;

namespace com.superneko.basis.masque.native
{
    public class MasqueOwnerRuntime : MonoBehaviour
    {
        [Serializable]
        public struct IdPair
        {
            public int ExpressionId;
            public int SerializedIndex;
        }

        public List<IdPair> ExpressionIdToSerializedIndexPair;

        Dictionary<int, int> _expressionIdToSerializedIndex;

        Dictionary<int, int> ExpressionIdToSerializedIndex
        {
            get
            {
                if (_expressionIdToSerializedIndex is null)
                {
                    _expressionIdToSerializedIndex = new Dictionary<int, int>();

                    foreach (var pair in ExpressionIdToSerializedIndexPair)
                    {
                        _expressionIdToSerializedIndex.Add(pair.ExpressionId, pair.SerializedIndex);
                    }
                }

                return _expressionIdToSerializedIndex;
            }
        }

        bool _initialized = false;
        MasqueCilboxMethod _setExpressionMethod;
        BasisAvatar avatar;

        void Awake()
        {
            avatar = GetComponentInParent<BasisAvatar>(true);
            avatar.OnAvatarReady += OnAvatarReady;
        }

        void OnAvatarReady(bool isOwner)
        {
            if (!isOwner) return;

            StartCoroutine(Setup());
        }

        IEnumerator Setup()
        {
            yield return null; // Wait for 1 frame
            yield return null; // Wait for 1 frame
            yield return null; // Wait for 1 frame

            Debug.Log($"[Masque] Setup.");

            if (!MasqueCilboxMethod.TryFromGameObjectAny(gameObject, "SetExpression", out _setExpressionMethod))
            {
                Debug.LogError("[Masque] Failed to get runtime SetExpression method.");
                yield break;
            }

            SetDefaultExpression(true);

            _initialized = true;

        }

        public void SetDefaultExpression(bool forced = false)
        {
            if (!_initialized && !forced) return;

            try
            {
                _setExpressionMethod.Call(new object[] { (byte)0 });
            }
            catch (Exception e)
            {
                Debug.LogError($"[Masque] Failed to set expression: {e}");
            }
        }

        public void SetExpressionByReferenceId(int expressionId, bool forced = false)
        {
            if (!_initialized && !forced) return;

            if (!ExpressionIdToSerializedIndex.TryGetValue(expressionId, out var index))
            {
                Debug.LogError($"Failed to find expression by id {expressionId}");
                return;
            }

            try
            {
                _setExpressionMethod.Call(new object[] { (byte)index });
            }
            catch (Exception e)
            {
                Debug.LogError($"[Masque] Failed to set expression: {e}");
            }
        }
    }
}
