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
        public float DeltaTime;
        public float YawDeadzoneDeg;
        public float YawBlendSpeed;
        public bool IsLocomoting;
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
        public Quaternion TorsoHeadingAnchor;
        public Quaternion PreviousHeadHeading;
        public float TorsoFollow;
        public int TorsoHeadingInitialized;
        public int TorsoHeadingBroken;
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
        const float k_TorsoYawRelockSpeedDeg = 6f;
        const float k_SmoothingReferenceFps = 90f;

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

            // The old VSP reduced its yaw play as the authored torso approached horizontal. Preserve that
            // behaviour: upright animation gets the configured cone; prone/supine animation starts following
            // immediately because a large delayed horizontal head/hips separation looks especially wrong.
            Vector3 authoredBodyUp = -animatedHeadToHips.normalized;
            float verticality = Mathf.Abs(Vector3.Dot(authoredBodyUp, up));
            float effectiveDeadzone = Mathf.Max(0f, input.YawDeadzoneDeg) * verticality;
            Quaternion torsoHeading = ComputeTorsoHeading(
                ref state, trackedHeading, effectiveDeadzone,
                input.YawBlendSpeed, input.IsLocomoting, input.DeltaTime);
            Quaternion headingDelta = torsoHeading * Quaternion.Inverse(animatedHeading);

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

        static Quaternion ComputeTorsoHeading(
            ref BasisAnimationRelativeVirtualSpineState state,
            Quaternion headHeading,
            float deadzoneDeg,
            float blendSpeed,
            bool moving,
            float deltaTime)
        {
            float dt = Mathf.Max(deltaTime, 1e-5f);
            if (state.TorsoHeadingInitialized == 0)
            {
                state.TorsoHeadingAnchor = headHeading;
                state.PreviousHeadHeading = headHeading;
                state.TorsoHeadingBroken = 0;
                state.TorsoFollow = 0f;
                state.TorsoHeadingInitialized = 1;
            }

            float headSpeedDeg = Quaternion.Angle(state.PreviousHeadHeading, headHeading) / dt;
            state.PreviousHeadHeading = headHeading;

            // Keyboard/stick locomotion deliberately breaks the cone so the torso eases toward the movement /
            // look direction. Once the head stops, the cone is re-centred at the new heading.
            if (moving || (state.TorsoHeadingBroken == 0
                && Quaternion.Angle(state.TorsoHeadingAnchor, headHeading) > deadzoneDeg))
            {
                state.TorsoHeadingBroken = 1;
            }

            float targetFollow = state.TorsoHeadingBroken != 0 ? 1f : 0f;
            state.TorsoFollow = Mathf.Lerp(
                state.TorsoFollow, targetFollow, FramerateIndependentAlpha(blendSpeed, dt));

            // Wait for the entrance blend before moving the anchor; otherwise crossing the cone edge produces
            // the exact one-frame click the blend is intended to remove.
            if (state.TorsoHeadingBroken != 0
                && state.TorsoFollow >= 0.999f
                && headSpeedDeg <= k_TorsoYawRelockSpeedDeg)
            {
                state.TorsoHeadingBroken = 0;
                state.TorsoHeadingAnchor = headHeading;
            }

            return Quaternion.Slerp(state.TorsoHeadingAnchor, headHeading, state.TorsoFollow);
        }

        static float FramerateIndependentAlpha(float speed, float deltaTime)
        {
            float alphaAtReference = Mathf.Clamp01(Mathf.Max(0f, speed) / k_SmoothingReferenceFps);
            if (alphaAtReference >= 0.999f) return 1f;
            if (alphaAtReference <= 0f) return 0f;
            float rate = -k_SmoothingReferenceFps * Mathf.Log(1f - alphaAtReference);
            return 1f - Mathf.Exp(-rate * Mathf.Max(0f, deltaTime));
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
