/*

MIT License

Copyright (c) 2026 Raliv and Gator Dragon Games

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using Basis.IK;
using Unity.Collections;
using UnityEngine;
public class RalivIKSpine
{

    // Spine data struct requiring all transforms to represent bones of nonzero length from the previous
    public struct SpineData
    {
        public Vector3 hipTargetPosition;
        public Quaternion hipTargetRotation;
        public Vector3 chestTargetPosition;
        public Quaternion chestTargetRotation;
        public Vector3 headTargetPosition;
        public Quaternion headTargetRotation;
        public FixedList512Bytes<Vector3> positions;
        public FixedList512Bytes<Quaternion> rotations;
        public FixedList512Bytes<Vector3> restPositions;
        public FixedList512Bytes<Quaternion> restRotations;
        // Authored skeleton remains separate from the placed animation reference.
        public bool animationRelative;
        public int neckIndex;
        public FixedList512Bytes<Vector3> referencePositions;
        public FixedList512Bytes<Quaternion> referenceRotations;
        public float length;
        public FixedList512Bytes<float> t;
        public Vector3 chestForward;
        public float chestHintWeight;
        public int chestIndex;
        public FixedList512Bytes<Vector3> targetSpinePositions;
        public FixedList512Bytes<Vector3> hipTargetAlignedSpinePositions;
        public FixedList512Bytes<Vector3> headTargetAlignedSpinePositions;
    }
    public const int iterations = 10;
    public static void SolveSpine(ref SpineData spineData)
    {

        int count = spineData.positions.Length;
        if (count < 2 || spineData.rotations.Length != count || spineData.restPositions.Length != count
            || spineData.restRotations.Length != count || spineData.t.Length != count
            || spineData.targetSpinePositions.Length != count || spineData.hipTargetAlignedSpinePositions.Length != count
            || spineData.headTargetAlignedSpinePositions.Length != count)
        {
            return;
        }
        bool animated = spineData.animationRelative && spineData.referencePositions.Length == count
            && spineData.referenceRotations.Length == count;
        var referencePositions = animated ? spineData.referencePositions : spineData.restPositions;
        var referenceRotations = animated ? spineData.referenceRotations : spineData.restRotations;
        bool hasChestHint = spineData.chestHintWeight > 0f && spineData.chestIndex > 0 && spineData.chestIndex < count - 1;
        int chestIndex = spineData.chestIndex;
        int neckIndex = spineData.neckIndex;
        var weights = spineData.t;
        if (!animated)
        {
            float hipHeadRestDistance = Vector3.Magnitude(referencePositions[^1] - referencePositions[0]);
            Vector3 headToHip = spineData.hipTargetPosition - spineData.headTargetPosition;
            headToHip = headToHip.normalized * hipHeadRestDistance;
            spineData.hipTargetPosition = Vector3.Lerp(spineData.hipTargetPosition, spineData.headTargetPosition + headToHip, 0.5f);
        }

        // World-space deltas multiply on the left, including for tilted reference poses.
        Quaternion hipRotationOffset = spineData.hipTargetRotation * Quaternion.Inverse(referenceRotations[0]);
        Quaternion headRotationOffset = spineData.headTargetRotation * Quaternion.Inverse(referenceRotations[^1]);

        for (int index = 0; index < spineData.headTargetAlignedSpinePositions.Length; index++)
        {
            if (index == spineData.headTargetAlignedSpinePositions.Length - 1)
            {
                spineData.headTargetAlignedSpinePositions[index] = spineData.headTargetPosition;
                continue;
            }
            var headSpacePosition = referencePositions[index] - referencePositions[^1];
            headSpacePosition = Quaternion.Inverse(referenceRotations[^1]) * headSpacePosition;
            spineData.headTargetAlignedSpinePositions[index] = spineData.headTargetRotation * headSpacePosition + spineData.headTargetPosition;
        }

        for (int index = 0; index < spineData.hipTargetAlignedSpinePositions.Length; index++)
        {
            if (index == 0)
            {
                spineData.hipTargetAlignedSpinePositions[index] = spineData.hipTargetPosition;
                continue;
            }
            var hipSpacePosition = referencePositions[index] - referencePositions[0];
            hipSpacePosition = Quaternion.Inverse(referenceRotations[0]) * hipSpacePosition;
            spineData.hipTargetAlignedSpinePositions[index] = spineData.hipTargetRotation * hipSpacePosition + spineData.hipTargetPosition;
            //  Debug.DrawLine(hipTargetAlignedSpinePositions[index - 1], hipTargetAlignedSpinePositions[index], Color.white);
            // Debug.DrawLine(headTargetAlignedSpinePositions[index - 1], headTargetAlignedSpinePositions[index], Color.cyan);
        }


        for (int index = 0; index < spineData.targetSpinePositions.Length; index++)
        {
            spineData.targetSpinePositions[index] = Vector3.Lerp(spineData.hipTargetAlignedSpinePositions[index], spineData.headTargetAlignedSpinePositions[index], ReferenceWeight(index));
        }

        // SOFT FABRIK
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            var lengthConstraint = (float)iteration / iterations;
            var curveConstraint = 1f - lengthConstraint;
            for (int index = 0; index < spineData.positions.Length; index++)
            {
                spineData.positions[index] = Vector3.Lerp(spineData.positions[index], spineData.targetSpinePositions[index], curveConstraint);
            }
            if (hasChestHint)
            {
                var spineOffset = (spineData.chestTargetPosition - spineData.positions[chestIndex])
                    * spineData.chestHintWeight * curveConstraint;
                spineData.positions[chestIndex] += spineOffset;
                spineData.positions[chestIndex + 1] += spineOffset * 0.5f;
            }
            for (int index = spineData.positions.Length - 2; index >= 0; index--)
            {
                var targetIndex = index + 1;
                var targetOffset = spineData.positions[index] - spineData.positions[targetIndex];
                var boneLength = Vector3.Magnitude(referencePositions[targetIndex] - referencePositions[index]);
                targetOffset = targetOffset.normalized * boneLength;
                var targetPosition = spineData.positions[targetIndex] + targetOffset;
                spineData.positions[index] = Vector3.Lerp(spineData.positions[index], targetPosition, lengthConstraint);
            }
            for (int index = 1; index < spineData.positions.Length; index++)
            {
                var targetIndex = index - 1;
                var targetOffset = spineData.positions[index] - spineData.positions[targetIndex];
                var boneLength = Vector3.Magnitude(referencePositions[targetIndex] - referencePositions[index]);
                targetOffset = targetOffset.normalized * boneLength;
                var targetPosition = spineData.positions[targetIndex] + targetOffset;
                spineData.positions[index] = Vector3.Lerp(spineData.positions[index], targetPosition, lengthConstraint);
            }
            // Project head onto target hip to head line to reduce lateral motion artifacts
            //var hipToHead = spineData.positions[^1] - spineData.positions[0];
            //var targetHipToHead = spineData.headTargetPosition - spineData.hipTargetPosition;
            //var projectedHipToHead = Vector3.Project(hipToHead, targetHipToHead);
            //spineData.positions[^1] = spineData.positions[0] + projectedHipToHead;
        }

        // for (int index = 0; index < spineData.positions.Length - 1; index++) {
        //  Debug.DrawLine(spineData.positions[index], spineData.positions[index + 1], Color.green);
        // }

        for (var index = 0; index < spineData.positions.Length - 1; index++)
        {
            var preRotation = Quaternion.Slerp(hipRotationOffset, headRotationOffset, ReferenceWeight(index));
            var restParentOffset = referencePositions[index + 1] - referencePositions[index];
            restParentOffset = preRotation * restParentOffset;
            var parentOffset = spineData.positions[index + 1] - spineData.positions[index];
            var fromTo = SafeFromToRotation(restParentOffset, parentOffset);
            spineData.rotations[index] = fromTo * preRotation * referenceRotations[index];
        }
        spineData.rotations[^1] = spineData.headTargetRotation;

        var chestRotate = 0f;
        if (hasChestHint)
        {
            var worldSpaceChestTargetForwardVector = spineData.chestTargetRotation * spineData.chestForward;
            var worldSpaceChestForwardVector = spineData.rotations[chestIndex] * spineData.chestForward;
            var worldSpaceChestDownBoneVector = spineData.positions[chestIndex + 1] - spineData.positions[chestIndex];
            worldSpaceChestTargetForwardVector = Vector3.ProjectOnPlane(worldSpaceChestTargetForwardVector, worldSpaceChestDownBoneVector.normalized);
            worldSpaceChestForwardVector = Vector3.ProjectOnPlane(worldSpaceChestForwardVector, worldSpaceChestDownBoneVector.normalized);
            // Debug.DrawLine(spineData.chestTargetPosition,
            // spineData.chestTargetPosition + worldSpaceChestTargetForwardVector, Color.red);
            //   Debug.DrawLine(spineData.positions[chestIndex], spineData.positions[chestIndex] + worldSpaceChestForwardVector, Color.blue);
            var chestFromTo = SafeFromToRotation(worldSpaceChestForwardVector.normalized, worldSpaceChestTargetForwardVector.normalized);
            chestFromTo.ToAngleAxis(out var chestFromToAngle, out var chestFromToAxis);
            if (Vector3.Dot(chestFromToAxis.normalized, worldSpaceChestDownBoneVector.normalized) < 0f)
            {
                chestFromToAngle = -chestFromToAngle;
            }

            if (chestFromToAngle > 180f)
            {
                chestFromToAngle -= 360f;
            }

            if (chestFromToAngle < -180f)
            {
                chestFromToAngle += 360f;
            }

            chestRotate = chestFromToAngle;
        }

        if (hasChestHint)
        {
            for (var index = 0; index < spineData.positions.Length - 1; index++)
            {
                if (index < spineData.positions.Length - 1)
                {
                    var w = 0f;
                    if (index == chestIndex - 1)
                    {
                        w = spineData.chestHintWeight * 0.5f;
                    }
                    if (index == chestIndex)
                    {
                        w = spineData.chestHintWeight * 1f;
                    }
                    if (index == chestIndex + 1)
                    {
                        w = spineData.chestHintWeight * 0.5f;
                    }
                    var downBone = spineData.positions[index + 1] - spineData.positions[index];
                    spineData.rotations[index] = Quaternion.AngleAxis(chestRotate * w, downBone.normalized) * spineData.rotations[index];
                }
            }
        }

        var headPinCorrection = spineData.headTargetPosition - spineData.positions[^1];
        for (var index = 0; index < spineData.positions.Length; index++)
        {
            spineData.positions[index] += headPinCorrection;
        }

        float ReferenceWeight(int index)
        {
            if (!animated) return weights[index];
            if (index == count - 1) return 1f;
            // Missing neck: keep the entire authored torso and apply gaze at the head only.
            if (neckIndex <= 0 || neckIndex >= count - 1 || index < neckIndex) return 0f;
            return Mathf.Lerp(0.35f, 1f, (float)(index - neckIndex) / (count - 1 - neckIndex));
        }

        static Quaternion SafeFromToRotation(Vector3 from, Vector3 to)
        {
            return from.sqrMagnitude > BasisEerieMovement.sqrEpsilon && to.sqrMagnitude > BasisEerieMovement.sqrEpsilon ? BasisQuaternionExt.FromToRotation(from, to) : Quaternion.identity;
        }
    }
}
