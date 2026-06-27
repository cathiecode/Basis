using Unity.Collections;
using UnityEngine;

namespace UnityEngine.Animations.Rigging
{
    [System.Serializable]
    public struct BasisVirtualSpinePreSolveData : IAnimationJobData
    {
        [SerializeField] BasisFullBodyIK m_Source;
        [SyncSceneToStream, SerializeField] bool m_Enabled;
        [SyncSceneToStream, SerializeField] bool m_FreezeHipsToTPose;
        [SyncSceneToStream, SerializeField] bool m_RotationLocked;
        [SyncSceneToStream, SerializeField] bool m_IsLocomoting;
        [SyncSceneToStream, SerializeField] bool m_LeftFootTracked;
        [SyncSceneToStream, SerializeField] bool m_RightFootTracked;
        [SyncSceneToStream, SerializeField] Vector3 m_NeckPosition;
        [SyncSceneToStream, SerializeField] Vector3 m_TposeHips;
        [SyncSceneToStream, SerializeField] Vector3 m_PlayerPosition;
        [SyncSceneToStream, SerializeField] Quaternion m_PlayerRotation;
        [SyncSceneToStream, SerializeField] float m_RestLength;
        [SyncSceneToStream, SerializeField] float m_HipsForwardBias;
        [SyncSceneToStream, SerializeField] float m_YawDeadzoneDeg;
        [SyncSceneToStream, SerializeField] float m_YawBlendSpeed;
        [SyncSceneToStream, SerializeField] float m_HipsRotationSpeed;
        [SyncSceneToStream, SerializeField] float m_CompressionStrength;
        [SyncSceneToStream, SerializeField] float m_MaxDrop;

        public BasisFullBodyIK Source { get => m_Source; set => m_Source = value; }
        public bool Enabled { get => m_Enabled; set => m_Enabled = value; }
        public bool FreezeHipsToTPose { get => m_FreezeHipsToTPose; set => m_FreezeHipsToTPose = value; }
        public bool RotationLocked { get => m_RotationLocked; set => m_RotationLocked = value; }
        public bool IsLocomoting { get => m_IsLocomoting; set => m_IsLocomoting = value; }
        public bool LeftFootTracked { get => m_LeftFootTracked; set => m_LeftFootTracked = value; }
        public bool RightFootTracked { get => m_RightFootTracked; set => m_RightFootTracked = value; }
        public Vector3 NeckPosition { get => m_NeckPosition; set => m_NeckPosition = value; }
        public Vector3 TposeHips { get => m_TposeHips; set => m_TposeHips = value; }
        public Vector3 PlayerPosition { get => m_PlayerPosition; set => m_PlayerPosition = value; }
        public Quaternion PlayerRotation { get => m_PlayerRotation; set => m_PlayerRotation = value; }
        public float RestLength { get => m_RestLength; set => m_RestLength = value; }
        public float HipsForwardBias { get => m_HipsForwardBias; set => m_HipsForwardBias = value; }
        public float YawDeadzoneDeg { get => m_YawDeadzoneDeg; set => m_YawDeadzoneDeg = value; }
        public float YawBlendSpeed { get => m_YawBlendSpeed; set => m_YawBlendSpeed = value; }
        public float HipsRotationSpeed { get => m_HipsRotationSpeed; set => m_HipsRotationSpeed = value; }
        public float CompressionStrength { get => m_CompressionStrength; set => m_CompressionStrength = value; }
        public float MaxDrop { get => m_MaxDrop; set => m_MaxDrop = value; }

        public string EnabledProperty => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(m_Enabled));
        public string FreezeHipsToTPoseProperty => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(m_FreezeHipsToTPose));
        public string RotationLockedProperty => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(m_RotationLocked));
        public string IsLocomotingProperty => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(m_IsLocomoting));
        public string LeftFootTrackedProperty => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(m_LeftFootTracked));
        public string RightFootTrackedProperty => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(m_RightFootTracked));
        public string NeckPositionProperty => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(m_NeckPosition));
        public string TposeHipsProperty => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(m_TposeHips));
        public string PlayerPositionProperty => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(m_PlayerPosition));
        public string PlayerRotationProperty => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(m_PlayerRotation));
        public string RestLengthProperty => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(m_RestLength));
        public string HipsForwardBiasProperty => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(m_HipsForwardBias));
        public string YawDeadzoneDegProperty => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(m_YawDeadzoneDeg));
        public string YawBlendSpeedProperty => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(m_YawBlendSpeed));
        public string HipsRotationSpeedProperty => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(m_HipsRotationSpeed));
        public string CompressionStrengthProperty => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(m_CompressionStrength));
        public string MaxDropProperty => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(m_MaxDrop));

        bool IAnimationJobData.IsValid() => m_Source != null;
        void IAnimationJobData.SetDefaultValues()
        {
            m_Source = null;
            m_Enabled = true;
            m_PlayerRotation = Quaternion.identity;
        }
    }

    [AddComponentMenu("Animation Rigging/Basis Virtual Spine Pre-Solve")]
    public class BasisVirtualSpinePreSolve : RigConstraint<BasisVirtualSpinePreSolveJob, BasisVirtualSpinePreSolveData, BasisVirtualSpinePreSolveBinder> { }

    public struct BasisVirtualSpinePreSolveJob : IWeightedAnimationJob
    {
        public BoolProperty enabled, freezeHipsToTPose, isLocomoting, leftFootTracked, rightFootTracked, rotationLocked;
        public BoolProperty hasHipsTracker;
        public Vector3Property neckPosition, tposeHips, playerPosition;
        public Vector4Property playerRotation;
        public FloatProperty restLength, hipsForwardBias, yawDeadzoneDeg;
        public FloatProperty yawBlendSpeed, hipsRotationSpeed, compressionStrength, maxDrop;
        public Vector3Property headPosition, leftFootPosition, rightFootPosition, hipsPosition;
        public Vector4Property headRotation, hipsRotation;
        public NativeArray<BasisLocalVirtualSpineDriver.SpineSolveState> state;
        public FloatProperty jobWeight { get; set; }
        public ReadOnlyTransformHandle animatedHips, animatedHead;

        public void ProcessRootMotion(AnimationStream stream) { }
        public void ProcessAnimation(AnimationStream stream)
        {
            if (jobWeight.Get(stream) <= 0f || hasHipsTracker.Get(stream) || !state.IsCreated)
                return;

            if (!enabled.Get(stream))
                return;

            Vector3 origin = playerPosition.Get(stream);
            Quaternion playerRot = ToQuaternion(playerRotation.Get(stream));
            Quaternion invPlayerRot = Quaternion.Inverse(playerRot);
            BasisLocalVirtualSpineDriver.VirtualHipsInput input = default;
            input.DeltaTime = stream.deltaTime;
            input.HeadPosition = invPlayerRot * (headPosition.Get(stream) - origin);
            input.NeckPosition = neckPosition.Get(stream);
            input.HeadRotation = invPlayerRot * ToQuaternion(headRotation.Get(stream));
            input.AnimatedHeadPosition = invPlayerRot * (animatedHead.GetPosition(stream) - origin);
            input.AnimatedHeadRotation = invPlayerRot * animatedHead.GetRotation(stream);
            input.AnimatedHipsPosition = invPlayerRot * (animatedHips.GetPosition(stream) - origin);
            input.AnimatedHipsRotation = invPlayerRot * animatedHips.GetRotation(stream);
            input.PlayerUp = Vector3.up;
            input.LeftFootPosition = invPlayerRot * (leftFootPosition.Get(stream) - origin);
            input.RightFootPosition = invPlayerRot * (rightFootPosition.Get(stream) - origin);
            input.LeftFootTracked = leftFootTracked.Get(stream);
            input.RightFootTracked = rightFootTracked.Get(stream);
            input.Scale = 1f;
            input.RestLength = restLength.Get(stream);
            input.HipsForwardBias = hipsForwardBias.Get(stream);
            input.YawDeadzoneDeg = yawDeadzoneDeg.Get(stream);
            input.YawBlendSpeed = yawBlendSpeed.Get(stream);
            input.HipsRotationSpeed = hipsRotationSpeed.Get(stream);
            input.CompressionStrength = compressionStrength.Get(stream);
            input.MaxDrop = maxDrop.Get(stream);
            input.FreezeHipsToTPose = freezeHipsToTPose.Get(stream);
            input.RotationLocked = rotationLocked.Get(stream);
            input.IsLocomoting = isLocomoting.Get(stream);
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
                enabled = BoolProperty.Bind(animator, component, data.EnabledProperty),
                freezeHipsToTPose = BoolProperty.Bind(animator, component, data.FreezeHipsToTPoseProperty),
                rotationLocked = BoolProperty.Bind(animator, component, data.RotationLockedProperty),
                isLocomoting = BoolProperty.Bind(animator, component, data.IsLocomotingProperty),
                leftFootTracked = BoolProperty.Bind(animator, component, data.LeftFootTrackedProperty),
                rightFootTracked = BoolProperty.Bind(animator, component, data.RightFootTrackedProperty),
                neckPosition = Vector3Property.Bind(animator, component, data.NeckPositionProperty),
                tposeHips = Vector3Property.Bind(animator, component, data.TposeHipsProperty),
                playerPosition = Vector3Property.Bind(animator, component, data.PlayerPositionProperty),
                playerRotation = Vector4Property.Bind(animator, component, data.PlayerRotationProperty),
                restLength = FloatProperty.Bind(animator, component, data.RestLengthProperty),
                hipsForwardBias = FloatProperty.Bind(animator, component, data.HipsForwardBiasProperty),
                yawDeadzoneDeg = FloatProperty.Bind(animator, component, data.YawDeadzoneDegProperty),
                yawBlendSpeed = FloatProperty.Bind(animator, component, data.YawBlendSpeedProperty),
                hipsRotationSpeed = FloatProperty.Bind(animator, component, data.HipsRotationSpeedProperty),
                compressionStrength = FloatProperty.Bind(animator, component, data.CompressionStrengthProperty),
                maxDrop = FloatProperty.Bind(animator, component, data.MaxDropProperty),
                hasHipsTracker = BoolProperty.Bind(animator, source, sourceData.HasHipsTrackerBoolProperty),
                headPosition = Vector3Property.Bind(animator, source, sourceData.TargetPositionPropertyHead),
                headRotation = Vector4Property.Bind(animator, source, sourceData.TargetRotationPropertyHead),
                animatedHips = BindHandle(animator, sourceData.hips),
                animatedHead = BindHandle(animator, sourceData.head),
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

        static ReadOnlyTransformHandle BindHandle(Animator animator, Transform t) => (t != null) ? ReadOnlyTransformHandle.Bind(animator, t) : default;
    }
}
