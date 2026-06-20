using Unity.Collections;
using UnityEngine;

namespace UnityEngine.Animations.Rigging
{
    [System.Serializable]
    public struct BasisVirtualSpinePreSolveData : IAnimationJobData
    {
        [SerializeField] BasisFullBodyIK m_Source;
        [SyncSceneToStream, SerializeField] float m_Flags;
        [SyncSceneToStream, SerializeField] Vector3 m_NeckPosition;
        [SyncSceneToStream, SerializeField] Vector3 m_TposeHips;
        [SyncSceneToStream, SerializeField] Vector3 m_PlayerPosition;
        [SyncSceneToStream, SerializeField] Quaternion m_PlayerRotation;
        [SyncSceneToStream, SerializeField] Vector4 m_Config0;
        [SyncSceneToStream, SerializeField] Vector4 m_Config1;

        public BasisFullBodyIK Source { get => m_Source; set => m_Source = value; }
        public float Flags { get => m_Flags; set => m_Flags = value; }
        public Vector3 NeckPosition { get => m_NeckPosition; set => m_NeckPosition = value; }
        public Vector3 TposeHips { get => m_TposeHips; set => m_TposeHips = value; }
        public Vector3 PlayerPosition { get => m_PlayerPosition; set => m_PlayerPosition = value; }
        public Quaternion PlayerRotation { get => m_PlayerRotation; set => m_PlayerRotation = value; }
        public Vector4 Config0 { get => m_Config0; set => m_Config0 = value; }
        public Vector4 Config1 { get => m_Config1; set => m_Config1 = value; }

        public string FlagsProperty => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(m_Flags));
        public string NeckPositionProperty => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(m_NeckPosition));
        public string TposeHipsProperty => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(m_TposeHips));
        public string PlayerPositionProperty => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(m_PlayerPosition));
        public string PlayerRotationProperty => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(m_PlayerRotation));
        public string Config0Property => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(m_Config0));
        public string Config1Property => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(m_Config1));

        bool IAnimationJobData.IsValid() => m_Source != null;
        void IAnimationJobData.SetDefaultValues()
        {
            m_Source = null;
            m_Flags = 1f;
            m_PlayerRotation = Quaternion.identity;
        }
    }

    [AddComponentMenu("Animation Rigging/Basis Virtual Spine Pre-Solve")]
    public class BasisVirtualSpinePreSolve : RigConstraint<BasisVirtualSpinePreSolveJob, BasisVirtualSpinePreSolveData, BasisVirtualSpinePreSolveBinder> { }

    public struct BasisVirtualSpinePreSolveJob : IWeightedAnimationJob
    {
        public FloatProperty flags;
        public BoolProperty hasHipsTracker;
        public Vector3Property neckPosition, tposeHips, playerPosition;
        public Vector4Property playerRotation, config0, config1;
        public Vector3Property headPosition, leftFootPosition, rightFootPosition, hipsPosition;
        public Vector4Property headRotation, hipsRotation;
        public NativeArray<BasisLocalVirtualSpineDriver.SpineSolveState> state;
        public FloatProperty jobWeight { get; set; }

        public void ProcessRootMotion(AnimationStream stream) { }
        public void ProcessAnimation(AnimationStream stream)
        {
            if (jobWeight.Get(stream) <= 0f || hasHipsTracker.Get(stream) || !state.IsCreated)
                return;

            int inputFlags = (int)flags.Get(stream);
            if ((inputFlags & 1) == 0)
                return;

            Vector3 origin = playerPosition.Get(stream);
            Quaternion playerRot = ToQuaternion(playerRotation.Get(stream));
            Quaternion invPlayerRot = Quaternion.Inverse(playerRot);
            Vector4 c0 = config0.Get(stream);
            Vector4 c1 = config1.Get(stream);
            BasisLocalVirtualSpineDriver.VirtualHipsInput input = default;
            input.DeltaTime = stream.deltaTime;
            input.HeadPosition = invPlayerRot * (headPosition.Get(stream) - origin);
            input.NeckPosition = neckPosition.Get(stream);
            input.HeadRotation = invPlayerRot * ToQuaternion(headRotation.Get(stream));
            input.PlayerUp = Vector3.up;
            input.LeftFootPosition = invPlayerRot * (leftFootPosition.Get(stream) - origin);
            input.RightFootPosition = invPlayerRot * (rightFootPosition.Get(stream) - origin);
            input.LeftFootTracked = (inputFlags & 8) != 0;
            input.RightFootTracked = (inputFlags & 16) != 0;
            input.Scale = 1f;
            input.RestLength = c0.x;
            input.StandingHipsY = c0.y;
            input.HipsForwardBias = c0.z;
            input.YawDeadzoneDeg = c0.w;
            input.YawBlendSpeed = c1.x;
            input.HipsRotationSpeed = c1.y;
            input.CompressionStrength = c1.z;
            input.MaxDrop = c1.w;
            input.FreezeHips = (inputFlags & 2) != 0;
            input.IsLocomoting = (inputFlags & 4) != 0;
            input.TposeHips = tposeHips.Get(stream);

            BasisLocalVirtualSpineDriver.SpineSolveState solveState = state[0];
            BasisLocalVirtualSpineDriver.SolveHips(ref solveState, in input, out Vector3 localHipsPos, out Quaternion localHipsRot);
            state[0] = solveState;

            Vector3 worldHipsPos = origin + playerRot * localHipsPos;
            Quaternion worldHipsRot = playerRot * localHipsRot;
            hipsPosition.Set(stream, worldHipsPos);
            hipsRotation.Set(stream, new Vector4(worldHipsRot.x, worldHipsRot.y, worldHipsRot.z, worldHipsRot.w));
        }

        static Quaternion ToQuaternion(Vector4 value) => new Quaternion(value.x, value.y, value.z, value.w);
    }

    public class BasisVirtualSpinePreSolveBinder : AnimationJobBinder<BasisVirtualSpinePreSolveJob, BasisVirtualSpinePreSolveData>
    {
        public override BasisVirtualSpinePreSolveJob Create(Animator animator, ref BasisVirtualSpinePreSolveData data, Component component)
        {
            BasisFullBodyIK source = data.Source;
            BasisFullBodyData sourceData = source.data;
            var job = new BasisVirtualSpinePreSolveJob
            {
                flags = FloatProperty.Bind(animator, component, data.FlagsProperty),
                neckPosition = Vector3Property.Bind(animator, component, data.NeckPositionProperty),
                tposeHips = Vector3Property.Bind(animator, component, data.TposeHipsProperty),
                playerPosition = Vector3Property.Bind(animator, component, data.PlayerPositionProperty),
                playerRotation = Vector4Property.Bind(animator, component, data.PlayerRotationProperty),
                config0 = Vector4Property.Bind(animator, component, data.Config0Property),
                config1 = Vector4Property.Bind(animator, component, data.Config1Property),
                hasHipsTracker = BoolProperty.Bind(animator, source, sourceData.HasHipsTrackerBoolProperty),
                headPosition = Vector3Property.Bind(animator, source, sourceData.TargetPositionPropertyHead),
                headRotation = Vector4Property.Bind(animator, source, sourceData.TargetRotationPropertyHead),
                leftFootPosition = Vector3Property.Bind(animator, source, sourceData.TargetPositionPropertyLeftLowerLeg),
                rightFootPosition = Vector3Property.Bind(animator, source, sourceData.TargetPositionPropertyRightLowerLeg),
                hipsPosition = Vector3Property.Bind(animator, source, sourceData.TargetPositionPropertyHips),
                hipsRotation = Vector4Property.Bind(animator, source, sourceData.TargetRotationPropertyHips),
                state = new NativeArray<BasisLocalVirtualSpineDriver.SpineSolveState>(1, Allocator.Persistent)
            };
            return job;
        }

        public override void Destroy(BasisVirtualSpinePreSolveJob job)
        {
            if (job.state.IsCreated) job.state.Dispose();
        }
    }
}
