using UnityEngine;

namespace Basis.IK
{
    public partial struct BasisEerieMovement
    {
        void ApplyAnimationRelativeVirtualSpine(BasisPoseStream stream)
        {
            virtualSpineApplied = false;
            if (!virtualSpineState.IsCreated || virtualSpineState.Length == 0)
            {
                return;
            }

            if (hasHipsTracker)
            {
                // Do not restore a pose cached before a period of real hip tracking.
                virtualSpineState[0] = default;
                return;
            }
            if (!handleHead.IsValid(stream) || !handleHips.IsValid(stream))
            {
                return;
            }

            handleHead.GetPositionAndRotation(stream, out Vector3 animatedHeadPosition, out Quaternion animatedHeadRotation);
            handleHips.GetPositionAndRotation(stream, out Vector3 animatedHipsPosition, out Quaternion animatedHipsRotation);

            BasisAnimationRelativeVirtualSpineInput input;
            input.AnimatedHeadPosition = animatedHeadPosition;
            input.AnimatedHeadRotation = animatedHeadRotation;
            input.AnimatedHipsPosition = animatedHipsPosition;
            input.AnimatedHipsRotation = animatedHipsRotation;
            input.TrackedHeadPosition = targetPositionHead;
            input.TrackedHeadRotation = targetRotationHead * targetOffsetHead;
            input.ReferenceUp = playerUp;
            input.FallbackForward = targetRotationHead * Vector3.forward;
            input.DeltaTime = stream.deltaTime;
            input.YawDeadzoneDeg = virtualSpineYawDeadzoneDeg;
            input.YawBlendSpeed = virtualSpineYawBlendSpeed;
            input.IsLocomoting = virtualSpineIsLocomoting;
            input.Locked = virtualSpineLocked;

            BasisAnimationRelativeVirtualSpineState state = virtualSpineState[0];
            BasisAnimationRelativeVirtualSpineCore.Solve(
                ref state, in input, out BasisAnimationRelativeVirtualSpineResult result);
            virtualSpineState[0] = state;
            if (!result.Valid)
            {
                return;
            }

            targetPositionHips = result.HipsPosition;
            // SolveSpine applies the calibrated target-to-bone offset. The core emits the final animated hips
            // bone rotation, so cancel that multiply before passing the target into the regular spine solve.
            targetRotationHips = result.HipsRotation * Quaternion.Inverse(offsetRotationHips);
            virtualSpineApplied = true;
        }

        bool TryGetVirtualSpineBodyUp(out Vector3 bodyUp)
        {
            bodyUp = playerUp;
            if (!virtualSpineApplied || !virtualSpineState.IsCreated || virtualSpineState.Length == 0)
            {
                return false;
            }

            Vector3 candidate = virtualSpineState[0].BodyUp;
            if (candidate.sqrMagnitude < k_SqrEpsilon)
            {
                return false;
            }

            bodyUp = candidate.normalized;
            return true;
        }
    }
}
