using UnityEngine;
using Unity.Collections;

namespace Basis.IK
{
    public partial struct BasisEerieMovement
    {
        void ApplyAnimationRelativeVirtualSpine()
        {
            virtualSpineApplied = false;
            if (!virtualSpineState.IsCreated || virtualSpineState.Length == 0)
            {
                return;
            }

            if (plan.hipsTracked)
            {
                // Do not restore a pose cached before a period of real hip tracking.
                virtualSpineState[0] = default;
                return;
            }
            if (!poseStream.IsValid(handleHead) || !poseStream.IsValid(handleHips))
            {
                return;
            }

            // Capture the fitted animation before any IK writes to the stream.
            FixedList512Bytes<Vector3> animatedPositions = default;
            FixedList512Bytes<Quaternion> animatedRotations = default;
            int count = chainHeadToSpine.IsCreated ? chainHeadToSpine.Length : 0;
            if (count < 2 || count > animatedRotations.Capacity)
            {
                return;
            }
            for (int index = 0; index < count; index++)
            {
                BasisBoneHandle handle = chainHeadToSpine[count - 1 - index];
                if (!poseStream.IsValid(handle))
                {
                    return;
                }
                poseStream.GetPositionAndRotation(handle, out Vector3 position, out Quaternion rotation);
                if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z)
                    || !float.IsFinite(rotation.x) || !float.IsFinite(rotation.y)
                    || !float.IsFinite(rotation.z) || !float.IsFinite(rotation.w)
                    || Quaternion.Dot(rotation, rotation) < sqrEpsilon)
                {
                    return;
                }
                animatedPositions.Add(position);
                animatedRotations.Add(BasisQuaternionExt.NormalizeSafe(rotation));
            }

            poseStream.GetPositionAndRotation(handleHead, out Vector3 animatedHeadPosition, out Quaternion animatedHeadRotation);
            poseStream.GetPositionAndRotation(handleHips, out Vector3 animatedHipsPosition, out Quaternion animatedHipsRotation);
            bool hasAnimatedChest = poseStream.IsValid(handleChest);
            Vector3 animatedChestPosition = Vector3.zero;
            Quaternion animatedChestRotation = Quaternion.identity;
            if (hasAnimatedChest)
            {
                poseStream.GetPositionAndRotation(
                    handleChest, out animatedChestPosition, out animatedChestRotation);
            }

            BasisAnimationRelativeVirtualSpineInput input;
            input.AnimatedHeadPosition = animatedHeadPosition;
            input.AnimatedHeadRotation = animatedHeadRotation;
            input.AnimatedHipsPosition = animatedHipsPosition;
            input.AnimatedHipsRotation = animatedHipsRotation;
            input.AnimatedChestPosition = animatedChestPosition;
            input.AnimatedChestRotation = animatedChestRotation;
            input.HasAnimatedChest = hasAnimatedChest;
            input.TrackedHeadPosition = targetPositionHead;
            input.TrackedHeadRotation = targetRotationHead * offsetRotationHead;
            input.ReferenceUp = playerUp;
            input.FallbackForward = targetRotationHead * Vector3.forward;
            input.DeltaTime = poseStream.deltaTime;
            input.YawDeadzoneDeg = virtualSpineYawDeadzoneDeg;
            input.YawBlendSpeed = virtualSpineYawBlendSpeed;
            input.IsLocomoting = virtualSpineIsLocomoting;
            input.Locked = virtualSpineLocked;

            BasisAnimationRelativeVirtualSpineState state = virtualSpineState[0];
            BasisAnimationRelativeVirtualSpineCore.Solve(
                ref state, in input, out BasisAnimationRelativeVirtualSpineResult result);
            if (!result.Valid)
            {
                return;
            }

            if (!result.Frozen || state.SpinePositions.Length != count || state.SpineRotations.Length != count)
            {
                Quaternion placementRotation = result.HipsRotation * Quaternion.Inverse(animatedHipsRotation);
                state.SpinePositions.Length = count;
                state.SpineRotations.Length = count;
                for (int index = 0; index < count; index++)
                {
                    state.SpinePositions[index] = result.HipsPosition
                        + placementRotation * (animatedPositions[index] - animatedHipsPosition);
                    state.SpineRotations[index] = BasisQuaternionExt.NormalizeSafe(placementRotation * animatedRotations[index]);
                }
            }
            else
            {
                // Preserve the locked shape, but allow runtime body-fit/scale changes.
                Vector3 previous = state.SpinePositions[0];
                for (int index = 1; index < count; index++)
                {
                    Vector3 next = state.SpinePositions[index];
                    float length = (animatedPositions[index] - animatedPositions[index - 1]).magnitude;
                    state.SpinePositions[index] = state.SpinePositions[index - 1] + (next - previous).normalized * length;
                    previous = next;
                }
            }
            virtualSpineState[0] = state;

            targetPositionHips = result.HipsPosition;
            // SolveSpine applies the calibrated target-to-bone offset. The core emits the final animated hips
            // bone rotation, so cancel that multiply before passing the target into the regular spine solve.
            targetRotationHips = result.HipsRotation * Quaternion.Inverse(offsetRotationHips);
            if (!plan.chestTracked && result.ChestValid)
            {
                // The procedural Virtual Spine target is authored in its own upright body model. Replace it
                // with the animation's chest expressed through the same head-anchored heading transform as
                // the hips. A real chest tracker remains authoritative.
                targetPositionChestRaw = result.ChestPosition;
                targetPositionChest = result.ChestPosition;
                targetRotationChest = result.ChestRotation * Quaternion.Inverse(offsetRotationChest);
            }
            virtualSpineApplied = true;
            // Use the ordinary spine path with the placed animation targets.
            plan.prone = false;
            plan.crouchOffset = false;
            // Ground simulation assumes an upright body. Keep the authored legs in lying poses;
            // real leg/foot trackers remain authoritative.
            if (Mathf.Abs(Vector3.Dot(result.BodyUp, playerUp)) < 0.5f)
            {
                if (plan.leftLeg.target == BasisEerieSource.Sim)
                {
                    plan.leftLeg.solve = false;
                    plan.leftToeSurface = false;
                }
                if (plan.rightLeg.target == BasisEerieSource.Sim)
                {
                    plan.rightLeg.solve = false;
                    plan.rightToeSurface = false;
                }
            }
        }

        void PrepareAnimationRelativeSpine(ref RalivIKSpine.SpineData data)
        {
            // Adapt the saved posture to upstream's existing input contract.
            // The caller retains the calibrated data for the normal tracking path.
            BasisAnimationRelativeVirtualSpineState state = virtualSpineState[0];
            data.restPositions = state.SpinePositions;
            data.restRotations = state.SpineRotations;
            data.positions = state.SpinePositions;
            data.rotations = state.SpineRotations;
            float length = 0f;
            for (int index = 0; index < data.positions.Length; index++)
            {
                if (index > 1)
                {
                    length += (data.restPositions[index] - data.restPositions[index - 1]).magnitude;
                }
                data.t[index] = length;
            }
            data.length = length;
            for (int index = 0; index < data.t.Length; index++)
            {
                data.t[index] = length > epsilon ? data.t[index] / length * 0.8f : 0f;
            }
        }
    }
}
