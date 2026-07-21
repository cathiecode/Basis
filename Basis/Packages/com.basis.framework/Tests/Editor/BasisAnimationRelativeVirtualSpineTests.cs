using Basis.IK;
using NUnit.Framework;
using UnityEngine;

namespace Basis.Tests.IK
{
    public class BasisAnimationRelativeVirtualSpineTests
    {
        static BasisAnimationRelativeVirtualSpineResult Solve(
            Vector3 animatedHeadPosition,
            Quaternion animatedHeadRotation,
            Vector3 animatedHipsPosition,
            Quaternion animatedHipsRotation,
            Vector3 trackedHeadPosition,
            Quaternion trackedHeadRotation,
            bool locked,
            ref BasisAnimationRelativeVirtualSpineState state)
        {
            BasisAnimationRelativeVirtualSpineInput input;
            input.AnimatedHeadPosition = animatedHeadPosition;
            input.AnimatedHeadRotation = animatedHeadRotation;
            input.AnimatedHipsPosition = animatedHipsPosition;
            input.AnimatedHipsRotation = animatedHipsRotation;
            input.TrackedHeadPosition = trackedHeadPosition;
            input.TrackedHeadRotation = trackedHeadRotation;
            input.ReferenceUp = Vector3.up;
            input.FallbackForward = Vector3.forward;
            input.Locked = locked;
            BasisAnimationRelativeVirtualSpineCore.Solve(ref state, in input, out BasisAnimationRelativeVirtualSpineResult result);
            return result;
        }

        [Test]
        public void UprightPose_IsReanchoredBelowTrackedHeadAndHeading()
        {
            BasisAnimationRelativeVirtualSpineState state = default;
            Quaternion yaw = Quaternion.AngleAxis(90f, Vector3.up);
            var result = Solve(
                Vector3.up, Quaternion.identity, Vector3.zero, Quaternion.identity,
                new Vector3(3f, 2f, 4f), yaw, false, ref state);

            Assert.That(result.Valid, Is.True);
            Assert.That(Vector3.Distance(result.HipsPosition, new Vector3(3f, 1f, 4f)), Is.LessThan(1e-4f));
            Assert.That(Quaternion.Angle(result.HipsRotation, yaw), Is.LessThan(1e-3f));
            Assert.That(Vector3.Angle(result.BodyUp, Vector3.up), Is.LessThan(1e-3f));
        }

        [Test]
        public void ForwardLean_PreservesTheAuthoredHeadToHipsVector()
        {
            BasisAnimationRelativeVirtualSpineState state = default;
            Quaternion yaw = Quaternion.AngleAxis(63f, Vector3.up);
            Vector3 animatedHead = new Vector3(0f, 1f, 0.45f);
            var result = Solve(
                animatedHead, Quaternion.identity, Vector3.zero, Quaternion.Euler(18f, 0f, 0f),
                new Vector3(-2f, 1.7f, 5f), yaw, false, ref state);

            Vector3 expectedHeadToHips = yaw * -animatedHead;
            Assert.That(result.Valid, Is.True);
            Assert.That(Vector3.Distance(result.HipsPosition - new Vector3(-2f, 1.7f, 5f), expectedHeadToHips),
                Is.LessThan(1e-4f));
            Assert.That(Quaternion.Angle(result.HipsRotation, yaw * Quaternion.Euler(18f, 0f, 0f)),
                Is.LessThan(1e-3f));
        }

        [Test]
        public void LyingPose_RemainsHorizontalAndLockBothGuardsArePoseRelative()
        {
            BasisAnimationRelativeVirtualSpineState state = default;
            Quaternion yaw = Quaternion.AngleAxis(90f, Vector3.up);
            Vector3 trackedHead = new Vector3(0f, 0.8f, 0f);
            var result = Solve(
                Vector3.forward, Quaternion.identity, Vector3.zero, Quaternion.Euler(90f, 0f, 0f),
                trackedHead, yaw, false, ref state);

            Assert.That(result.Valid, Is.True);
            Assert.That(Mathf.Abs(Vector3.Dot(result.BodyUp, Vector3.up)), Is.LessThan(1e-4f),
                "a lying animation was pulled back toward world-up");

            Vector3 bendLimited = BasisFullIKConstraintJob.EnforceSpineBendLimit(
                trackedHead, result.HipsPosition, 60f, result.BodyUp);
            Vector3 distanceLimited = BasisFullIKConstraintJob.ClampHipsAroundHead(
                trackedHead, bendLimited, 1f, 0.5f, 1.5f, result.BodyUp);
            Assert.That(Vector3.Distance(distanceLimited, result.HipsPosition), Is.LessThan(1e-4f),
                "LockBoth treated a valid lying torso as an inverted upright torso");
        }

        [Test]
        public void HeadLookPitch_DoesNotPitchThePelvis()
        {
            BasisAnimationRelativeVirtualSpineState yawOnlyState = default;
            BasisAnimationRelativeVirtualSpineState pitchedState = default;
            Quaternion yaw = Quaternion.AngleAxis(40f, Vector3.up);
            Quaternion pitchedHead = yaw * Quaternion.AngleAxis(72f, Vector3.right);

            var yawOnly = Solve(Vector3.up, Quaternion.identity, Vector3.zero, Quaternion.identity,
                Vector3.up, yaw, false, ref yawOnlyState);
            var pitched = Solve(Vector3.up, Quaternion.identity, Vector3.zero, Quaternion.identity,
                Vector3.up, pitchedHead, false, ref pitchedState);

            Assert.That(Vector3.Distance(yawOnly.HipsPosition, pitched.HipsPosition), Is.LessThan(1e-4f));
            Assert.That(Quaternion.Angle(yawOnly.HipsRotation, pitched.HipsRotation), Is.LessThan(1e-3f));
        }

        [Test]
        public void VirtualSpineLock_FreezesLastValidPoseUntilReleased()
        {
            BasisAnimationRelativeVirtualSpineState state = default;
            var initial = Solve(Vector3.up, Quaternion.identity, Vector3.zero, Quaternion.identity,
                Vector3.up, Quaternion.identity, false, ref state);
            var frozen = Solve(new Vector3(0.4f, 0.8f, 0.2f), Quaternion.Euler(0f, 30f, 0f), Vector3.zero,
                Quaternion.Euler(15f, 30f, 0f), new Vector3(5f, 3f, -2f), Quaternion.Euler(0f, 120f, 0f),
                true, ref state);

            Assert.That(frozen.Valid, Is.True);
            Assert.That(frozen.Frozen, Is.True);
            Assert.That(Vector3.Distance(frozen.HipsPosition, initial.HipsPosition), Is.LessThan(1e-5f));
            Assert.That(Quaternion.Angle(frozen.HipsRotation, initial.HipsRotation), Is.LessThan(1e-4f));

            var released = Solve(new Vector3(0.4f, 0.8f, 0.2f), Quaternion.Euler(0f, 30f, 0f), Vector3.zero,
                Quaternion.Euler(15f, 30f, 0f), new Vector3(5f, 3f, -2f), Quaternion.Euler(0f, 120f, 0f),
                false, ref state);
            Assert.That(Vector3.Distance(released.HipsPosition, initial.HipsPosition), Is.GreaterThan(1f));
        }
    }
}
