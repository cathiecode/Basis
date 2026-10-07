using System;
using System.Collections;
using Basis.Scripts.BasisSdk.Players;
using HVR.Basis.Comms;
using UnityEngine;

namespace HVR.Vixxy
{
    public class SNVixxyGestureInput : MonoBehaviour, IHVRInitializable
    {
        [Serializable]
        public enum Fingers
        {
            // Indivisual fingers
            Thumb = 1,
            Index = 2,
            Middle = 4,
            Ring = 8,
            Little = 16,
        }

        [Serializable]
        public class SNGestureInputRule
        {
            public Fingers leftFingerMask;
            public Fingers leftFingerCurled;
            public Fingers rightFingerMask;
            public Fingers rightFingerCurled;
            public float value;
        }

        [SerializeField][Multiline] internal string title;
        [SerializeField] internal HVRVixxyControl control;

        [SerializeField] internal HVRVixxyRememberScope remember = HVRVixxyRememberScope.DoNotRemember;
        [SerializeField] internal string rememberTag = "";
        [SerializeField] internal SNGestureInputRule[] rules = new SNGestureInputRule[] { };

        private bool _isWearerAndReady = false;
        private float _value;
        private HVRAvatarComms _comms;

        public HVRVixxyControl Control => control;

        private bool TryResolveActualControl(out HVRVixxyControl result)
        {
            var controlsOnThis = GetComponents<HVRVixxyControl>(); // This may return 0 elements.
            if (controlsOnThis.Length == 1)
            {
                result = controlsOnThis[0];
                return true;
            }

            if (control != null)
            {
                result = control;
                return true;
            }

            result = null;
            return false;
        }

        public void OnHVRAvatarReady(bool isWearer)
        {
            _comms = HVRCommsUtil.GetComms(this);

            if (!isWearer) return;

            control = TryResolveActualControl(out var actualControl) ? actualControl : null;

            _value = control != null ? control.defaultValue : 0f;

            _isWearerAndReady = true;

            if (control != null && isActiveAndEnabled) StartCoroutine(RestoreNextFrame());
        }

        public void OnHVRReadyBothAvatarAndNetwork(bool isWearer)
        {
            if (!isWearer) return;
        }

        public void Update()
        {
            if (!_isWearerAndReady) return;

            var leftHand = CalcFingerCurl(BasisLocalPlayer.Instance.LocalHandDriver.LeftHand);
            var rightHand = CalcFingerCurl(BasisLocalPlayer.Instance.LocalHandDriver.LeftHand);

            foreach (var rule in rules)
            {
                if (
                    rule.value != _value &&
                    (leftHand & rule.leftFingerMask) == ((Fingers)rule.leftFingerCurled & rule.leftFingerMask) &&
                    (rightHand & rule.rightFingerMask) == ((Fingers)rule.rightFingerCurled & rule.rightFingerMask)
                )
                {
                    ApplyValue(rule.value);
                }
            }
        }

        private IEnumerator RestoreNextFrame()
        {
            yield return null;
            if (control == null) yield break;

            var saved = 0f;
            var found = TryResolvePersistenceKey(out var key) && HVRVixxyPersistentStore.TryGet(key, out saved);
            if (found)
            {
                _value = saved;
                var addressId = control.IsInitialized ? control.AddressId : HVRAddress.AddressToId(control.CalculateAddress());
                _comms.VariableStore.SubmitOrDefineDefaultValue(addressId, saved);
            }
        }

        private bool TryResolvePersistenceKey(out string key)
        {
            key = null;
            if (control == null) return false;

            var address = control.IsInitialized ? control.Address : control.CalculateAddress();

            switch (remember)
            {
                case HVRVixxyRememberScope.RememberInThisAvatar:
                    if (string.IsNullOrEmpty(BasisLocalPlayer.CurrentAvatarUniqueID)) return false;
                    key = $"avatar:{BasisLocalPlayer.CurrentAvatarUniqueID}|{address}";
                    return true;
                case HVRVixxyRememberScope.RememberInThisTag:
                    if (string.IsNullOrEmpty(rememberTag)) return false;
                    key = $"tag:{rememberTag}";
                    return true;
                case HVRVixxyRememberScope.RememberAcrossAvatars:
                    key = $"global:{address}";
                    return true;
                default:
                    return false;
            }
        }

        public void ApplyValue(float value)
        {
            _value = value;
            SubmitValue();
        }

        public float GetValue()
        {
            return _value;
        }

        private void SubmitValue()
        {
            if (control != null && _comms != null)
            {
                var actualAddress = control.IsInitialized ? control.AddressId : HVRAddress.AddressToId(control.CalculateAddress());
                _comms.VariableStore.SubmitOrDefineDefaultValue(actualAddress, _value);

                if (TryResolvePersistenceKey(out var key))
                {
                    HVRVixxyPersistentStore.Set(key, _value, control.defaultValue);
                }
            }
        }

        private static Fingers CalcFingerCurl(BasisFingerPose pose)
        {
            var field = (Fingers)0;

            field |= pose.ThumbPercentage.x > 0 ? Fingers.Thumb : 0;
            field |= pose.IndexPercentage.x > 0 ? Fingers.Index : 0;
            field |= pose.ThumbPercentage.x > 0 ? Fingers.Middle : 0;
            field |= pose.ThumbPercentage.x > 0 ? Fingers.Ring : 0;
            field |= pose.ThumbPercentage.x > 0 ? Fingers.Little : 0;

            return field;
        }
    }
}
