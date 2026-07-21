using Unity.Burst;
using UnityEngine;

namespace Basis.IK
{
    public struct BasisAnimationRelativeVirtualSpineInput
    {
        public Vector3 AnimatedHeadPosition;
        public Quaternion AnimatedHeadRotation;
        public Vector3 AnimatedHipsPosition;
        public Quaternion AnimatedHipsRotation;
        public Vector3 TrackedHeadPosition;
        public Quaternion TrackedHeadRotation;
        public Vector3 ReferenceUp;
        public Vector3 FallbackForward;
        public bool Locked;
    }

    public struct BasisAnimationRelativeVirtualSpineResult
    {
        public Vector3 HipsPosition;
        public Quaternion HipsRotation;
        /// <summary>The authored hips-to-head axis after applying the tracked heading.</summary>
        public Vector3 BodyUp;
        public bool Valid;
        public bool Frozen;
    }

    public struct BasisAnimationRelativeVirtualSpineState
    {
        public Vector3 HipsPosition;
        public Quaternion HipsRotation;
        public Vector3 BodyUp;
        public int Initialized;
    }

    /// <summary>
    /// Re-expresses the animation's head/hips relationship beneath the tracked head. Only heading around the
    /// play-space up axis follows tracking: tracked look pitch and roll remain head motion rather than tilting
    /// the whole avatar. The authored head-to-hips vector and pelvis tilt are otherwise preserved, including
    /// prone, supine and forward-folded poses.
    /// </summary>
    [BurstCompile]
    public static class BasisAnimationRelativeVirtualSpineCore
    {
        const float k_Epsilon = 1e-5f;
        const float k_SqrEpsilon = 1e-8f;

        public static void Solve(
            ref BasisAnimationRelativeVirtualSpineState state,
            in BasisAnimationRelativeVirtualSpineInput input,
            out BasisAnimationRelativeVirtualSpineResult result)
        {
            result = default;

            if (input.Locked && state.Initialized != 0)
            {
                result.HipsPosition = state.HipsPosition;
                result.HipsRotation = state.HipsRotation;
                result.BodyUp = state.BodyUp;
                result.Valid = true;
                result.Frozen = true;
                return;
            }

            if (!IsFinite(input.AnimatedHeadPosition)
                || !IsFinite(input.AnimatedHipsPosition)
                || !IsFinite(input.TrackedHeadPosition)
                || !TryNormalize(input.AnimatedHeadRotation, out Quaternion animatedHeadRotation)
                || !TryNormalize(input.AnimatedHipsRotation, out Quaternion animatedHipsRotation)
                || !TryNormalize(input.TrackedHeadRotation, out Quaternion trackedHeadRotation))
            {
                return;
            }

            Vector3 animatedHeadToHips = input.AnimatedHipsPosition - input.AnimatedHeadPosition;
            if (animatedHeadToHips.sqrMagnitude < k_SqrEpsilon)
            {
                return;
            }

            Vector3 up = IsFinite(input.ReferenceUp) && input.ReferenceUp.sqrMagnitude >= k_SqrEpsilon
                ? input.ReferenceUp.normalized
                : Vector3.up;
            Vector3 fallback = IsFinite(input.FallbackForward) ? input.FallbackForward : Vector3.forward;

            Quaternion animatedHeading = EstimateHeading(animatedHeadRotation, up, fallback);
            Quaternion trackedHeading = EstimateHeading(
                trackedHeadRotation, up, animatedHeading * Vector3.forward);
            Quaternion headingDelta = trackedHeading * Quaternion.Inverse(animatedHeading);

            Vector3 rotatedHeadToHips = headingDelta * animatedHeadToHips;
            Vector3 hipsPosition = input.TrackedHeadPosition + rotatedHeadToHips;
            Quaternion hipsRotation = BasisQuaternionExt.NormalizeSafe(headingDelta * animatedHipsRotation);
            Vector3 hipsToHead = -rotatedHeadToHips;
            Vector3 bodyUp = hipsToHead.sqrMagnitude >= k_SqrEpsilon
                ? hipsToHead.normalized
                : hipsRotation * Vector3.up;

            if (!IsFinite(hipsPosition) || !IsFinite(hipsRotation) || !IsFinite(bodyUp))
            {
                return;
            }

            state.HipsPosition = hipsPosition;
            state.HipsRotation = hipsRotation;
            state.BodyUp = bodyUp;
            state.Initialized = 1;

            result.HipsPosition = hipsPosition;
            result.HipsRotation = hipsRotation;
            result.BodyUp = bodyUp;
            result.Valid = true;
            result.Frozen = input.Locked;
        }

        public static Quaternion EstimateHeading(Quaternion rotation, Vector3 up, Vector3 fallbackForward)
        {
            Vector3 rightProjected = Vector3.ProjectOnPlane(rotation * Vector3.right, up);
            Vector3 forwardProjected = Vector3.ProjectOnPlane(rotation * Vector3.forward, up);
            float rightWeight = rightProjected.sqrMagnitude;
            float forwardWeight = forwardProjected.sqrMagnitude;
            Vector3 mixedForward = Vector3.zero;

            if (rightWeight > k_SqrEpsilon)
            {
                Vector3 rightBasedForward = Vector3.Cross(rightProjected.normalized, up).normalized;
                mixedForward += rightBasedForward * rightWeight;
            }
            if (forwardWeight > k_SqrEpsilon)
            {
                Vector3 candidate = forwardProjected.normalized * forwardWeight;
                if ((mixedForward + candidate).sqrMagnitude > k_SqrEpsilon)
                {
                    mixedForward += candidate;
                }
            }

            if (mixedForward.sqrMagnitude < k_SqrEpsilon)
            {
                mixedForward = Vector3.ProjectOnPlane(fallbackForward, up);
            }
            if (mixedForward.sqrMagnitude < k_SqrEpsilon)
            {
                mixedForward = Vector3.Cross(up, Mathf.Abs(up.y) < 0.99f ? Vector3.up : Vector3.right);
            }
            return Quaternion.LookRotation(mixedForward.normalized, up);
        }

        static bool TryNormalize(Quaternion value, out Quaternion normalized)
        {
            normalized = Quaternion.identity;
            if (!IsFinite(value))
            {
                return false;
            }
            float lengthSq = Quaternion.Dot(value, value);
            if (!(lengthSq > k_SqrEpsilon))
            {
                return false;
            }
            float inverseLength = 1f / Mathf.Sqrt(lengthSq);
            normalized = new Quaternion(
                value.x * inverseLength, value.y * inverseLength,
                value.z * inverseLength, value.w * inverseLength);
            return true;
        }

        static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        static bool IsFinite(Quaternion value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y)
            && float.IsFinite(value.z) && float.IsFinite(value.w);
    }
}
