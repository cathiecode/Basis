using System;
using System.Collections;
using System.Linq;
using Basis.Scripts.BasisSdk;
using UnityEngine;
using UnityEngine.InputSystem;

namespace com.superneko.basis.masque.native
{
    public class MasqueOwnerRuntime : MonoBehaviour
    {
        public SerializableFaceSet FaceSet;

        bool _initialized = false;
        MasqueCilboxMethod _setExpressionMethod;
        BasisAvatar avatar;

        void Awake()
        {
            avatar = GetComponentInParent<BasisAvatar>(true);
            avatar.OnAvatarReady += OnAvatarReady;

        }

        void Update()
        {
            if (!_initialized) return;

            for (int functionKey = 1; functionKey <= 12; functionKey++)
            {
                var keycode = Key.F1 + functionKey - 1;

                if (Keyboard.current[keycode].isPressed)
                {
                    SetExpressionByTag($"key_f{functionKey}");
                }
            }
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

        public void SetExpressionByTag(string tag, bool forced = false)
        {
            if (!_initialized && !forced) return;

            var matchingExpression = FaceSet.expressions.FirstOrDefault((expression) => expression.TagsSet.TryGetValue(tag, out var _));

            int matchingExpressionIndex = -1;

            for (var i = 0; i < FaceSet.expressions.Length; i++)
            {
                var expression = FaceSet.expressions[i];

                if (expression.TagsSet.TryGetValue(tag, out var _))
                {
                    matchingExpressionIndex = i;
                    break;
                }
            }

            try
            {
                if (matchingExpressionIndex < 0)
                {
                    _setExpressionMethod.Call(new object[] { (byte)0 });
                }
                else
                {
                    _setExpressionMethod.Call(new object[] { (byte)matchingExpressionIndex });
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Masque] Failed to set expression: {e}");
            }
        }
    }
}
