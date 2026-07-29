using System;
using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Generic, input-agnostic grabber for VR hands, desktop hands, NPCs, and tools.
    /// It finds nearby ApexGrabbables and drives their grip point toward a target
    /// using forces and torque instead of parenting or teleporting the held object.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApexGrabber : MonoBehaviour, IApexGrabber
    {
        private const int OverlapCapacity = 32;

        [Header("Identity")]
        [SerializeField] private ApexHandedness handedness = ApexHandedness.Either;

        [Header("References")]
        [Tooltip("The point the held object's grip pose will follow. Defaults to this transform.")]
        [SerializeField] private Transform gripTarget;
        [SerializeField] private ApexGrabProfile grabProfile;

        [Header("Detection")]
        [SerializeField, Min(0.01f)] private float grabRadius = 0.16f;
        [SerializeField] private LayerMask grabbableLayers = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Target Velocity")]
        [SerializeField, Range(0f, 1f)] private float velocitySmoothing = 0.35f;

        private readonly Collider[] overlapResults = new Collider[OverlapCapacity];

        private ApexGrabbable heldObject;
        private ApexGrabPose heldPose;
        private Vector3 sampledLinearVelocity;
        private Vector3 sampledAngularVelocity;
        private Vector3 previousTargetPosition;
        private Quaternion previousTargetRotation;
        private bool hasVelocitySample;

        public event Action<ApexGrabbable> Grabbed;
        public event Action<ApexGrabbable> Released;

        public int GrabberId => GetInstanceID();
        public Transform GripTarget => gripTarget != null ? gripTarget : transform;
        public ApexHandedness Handedness => handedness;
        public bool IsHolding => heldObject != null;
        public ApexGrabbable HeldObject => heldObject;
        public ApexGrabProfile GrabProfile => grabProfile;
        public Vector3 SampledLinearVelocity => sampledLinearVelocity;
        public Vector3 SampledAngularVelocity => sampledAngularVelocity;

        private void Reset()
        {
            gripTarget = transform;
        }

        private void Awake()
        {
            if (gripTarget == null)
            {
                gripTarget = transform;
            }

            ResetVelocitySampling();
        }

        private void OnEnable()
        {
            ResetVelocitySampling();
        }

        private void OnDisable()
        {
            ForceRelease();
        }

        private void FixedUpdate()
        {
            SampleTargetVelocity();

            if (heldObject == null)
            {
                return;
            }

            ApexBody apexBody = heldObject.Body;
            Rigidbody body = apexBody != null ? apexBody.Rigidbody : null;

            if (body == null || body.isKinematic || !body.gameObject.activeInHierarchy)
            {
                ForceRelease();
                return;
            }

            Vector3 currentGripPosition = heldObject.GetWorldGripPosition(heldPose);
            float distance = Vector3.Distance(currentGripPosition, GripTarget.position);

            if (distance > GetBreakDistance())
            {
                ForceRelease();
                return;
            }

            if (!heldObject.CanMove)
            {
                return;
            }

            ApplyPositionDrive(body, currentGripPosition);

            if (heldPose.FollowRotation)
            {
                ApplyRotationDrive(body);
            }
        }

        /// <summary>Finds and grabs the nearest valid ApexGrabbable.</summary>
        public bool TryGrabClosest()
        {
            if (heldObject != null)
            {
                return true;
            }

            int hitCount = Physics.OverlapSphereNonAlloc(
                GripTarget.position,
                grabRadius,
                overlapResults,
                grabbableLayers,
                triggerInteraction);

            ApexGrabbable bestCandidate = null;
            ApexGrabPose bestPose = default;
            float bestSqrDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = overlapResults[i];
                overlapResults[i] = null;

                if (hit == null)
                {
                    continue;
                }

                ApexGrabbable candidate = hit.GetComponentInParent<ApexGrabbable>();
                if (candidate == null || !candidate.isActiveAndEnabled)
                {
                    continue;
                }

                if (!CanLift(candidate))
                {
                    continue;
                }

                if (!candidate.TryGetBestGrabPose(
                        GripTarget.position,
                        GripTarget.rotation,
                        handedness,
                        out ApexGrabPose pose))
                {
                    continue;
                }

                Vector3 worldGripPosition = candidate.GetWorldGripPosition(pose);
                float sqrDistance = (worldGripPosition - GripTarget.position).sqrMagnitude;

                if (sqrDistance >= bestSqrDistance)
                {
                    continue;
                }

                bestCandidate = candidate;
                bestPose = pose;
                bestSqrDistance = sqrDistance;
            }

            return bestCandidate != null && TryGrab(bestCandidate, bestPose);
        }

        /// <summary>Attempts to grab a specific object using its best available pose.</summary>
        public bool TryGrab(ApexGrabbable candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            if (!candidate.TryGetBestGrabPose(
                    GripTarget.position,
                    GripTarget.rotation,
                    handedness,
                    out ApexGrabPose pose))
            {
                return false;
            }

            return TryGrab(candidate, pose);
        }

        public void Release()
        {
            ReleaseInternal(true);
        }

        public void ForceRelease()
        {
            ReleaseInternal(false);
        }

        private bool TryGrab(ApexGrabbable candidate, ApexGrabPose pose)
        {
            if (candidate == null)
            {
                return false;
            }

            if (heldObject == candidate)
            {
                return true;
            }

            if (heldObject != null || !CanLift(candidate))
            {
                return false;
            }

            if (!candidate.TryBeginGrab(this, pose))
            {
                return false;
            }

            heldObject = candidate;
            heldPose = pose;
            heldObject.Body?.Wake();
            Grabbed?.Invoke(heldObject);
            return true;
        }

        private void ReleaseInternal(bool applyThrowVelocity)
        {
            if (heldObject == null)
            {
                return;
            }

            ApexGrabbable releasedObject = heldObject;
            Rigidbody releasedBody = releasedObject.Body != null
                ? releasedObject.Body.Rigidbody
                : null;

            heldObject = null;
            heldPose = default;
            releasedObject.EndGrab(this);

            if (applyThrowVelocity && releasedBody != null && !releasedBody.isKinematic)
            {
                ApplyReleaseVelocity(releasedBody);
            }

            Released?.Invoke(releasedObject);
        }

        private bool CanLift(ApexGrabbable candidate)
        {
            if (candidate == null || candidate.Body == null || candidate.Body.Rigidbody == null)
            {
                return false;
            }

            Rigidbody body = candidate.Body.Rigidbody;
            if (body.isKinematic)
            {
                return false;
            }

            float maximumMass = grabProfile != null ? grabProfile.MaximumHeldMass : 150f;
            return maximumMass <= 0f || body.mass <= maximumMass;
        }

        private void ApplyPositionDrive(Rigidbody body, Vector3 currentGripPosition)
        {
            Vector3 positionError = GripTarget.position - currentGripPosition;
            Vector3 pointVelocity = body.GetPointVelocity(currentGripPosition);
            Vector3 velocityError = sampledLinearVelocity - pointVelocity;

            float spring = grabProfile != null ? grabProfile.PositionSpring : 1200f;
            float damping = grabProfile != null ? grabProfile.PositionDamping : 90f;
            float maximumForce = grabProfile != null ? grabProfile.MaximumForce : 1800f;

            Vector3 force = positionError * spring + velocityError * damping;
            if (maximumForce > 0f)
            {
                force = Vector3.ClampMagnitude(force, maximumForce);
            }

            body.AddForceAtPosition(force, currentGripPosition, ForceMode.Force);
        }

        private void ApplyRotationDrive(Rigidbody body)
        {
            Quaternion currentGripRotation = heldObject.GetWorldGripRotation(heldPose);
            Quaternion rotationError = GripTarget.rotation * Quaternion.Inverse(currentGripRotation);
            rotationError.ToAngleAxis(out float angleDegrees, out Vector3 axis);

            if (angleDegrees > 180f)
            {
                angleDegrees -= 360f;
            }

            Vector3 angularError = sampledAngularVelocity - body.angularVelocity;
            float spring = grabProfile != null ? grabProfile.RotationSpring : 700f;
            float damping = grabProfile != null ? grabProfile.RotationDamping : 45f;
            float maximumTorque = grabProfile != null ? grabProfile.MaximumTorque : 900f;

            Vector3 torque = angularError * damping;
            if (Mathf.Abs(angleDegrees) > 0.001f && IsFinite(axis))
            {
                torque += axis.normalized * (angleDegrees * Mathf.Deg2Rad * spring);
            }

            if (maximumTorque > 0f)
            {
                torque = Vector3.ClampMagnitude(torque, maximumTorque);
            }

            body.AddTorque(torque, ForceMode.Force);
        }

        private void ApplyReleaseVelocity(Rigidbody body)
        {
            float influence = grabProfile != null ? grabProfile.ReleaseVelocityInfluence : 0.75f;
            float multiplier = grabProfile != null ? grabProfile.ThrowVelocityMultiplier : 1f;
            float maximumSpeed = grabProfile != null ? grabProfile.MaximumThrowSpeed : 25f;

            Vector3 throwVelocity = sampledLinearVelocity * multiplier;
            if (maximumSpeed > 0f)
            {
                throwVelocity = Vector3.ClampMagnitude(throwVelocity, maximumSpeed);
            }

            body.velocity = Vector3.Lerp(body.velocity, throwVelocity, influence);
            body.angularVelocity = Vector3.Lerp(body.angularVelocity, sampledAngularVelocity, influence);
            body.WakeUp();
        }

        private float GetBreakDistance()
        {
            return grabProfile != null ? grabProfile.BreakDistance : 0.75f;
        }

        private void SampleTargetVelocity()
        {
            Transform target = GripTarget;
            float deltaTime = Time.fixedDeltaTime;

            if (!hasVelocitySample || deltaTime <= Mathf.Epsilon)
            {
                previousTargetPosition = target.position;
                previousTargetRotation = target.rotation;
                sampledLinearVelocity = Vector3.zero;
                sampledAngularVelocity = Vector3.zero;
                hasVelocitySample = true;
                return;
            }

            Vector3 rawLinearVelocity = (target.position - previousTargetPosition) / deltaTime;

            Quaternion deltaRotation = target.rotation * Quaternion.Inverse(previousTargetRotation);
            deltaRotation.ToAngleAxis(out float angleDegrees, out Vector3 axis);
            if (angleDegrees > 180f)
            {
                angleDegrees -= 360f;
            }

            Vector3 rawAngularVelocity = Vector3.zero;
            if (Mathf.Abs(angleDegrees) > 0.001f && IsFinite(axis))
            {
                rawAngularVelocity = axis.normalized * (angleDegrees * Mathf.Deg2Rad / deltaTime);
            }

            float blend = 1f - velocitySmoothing;
            sampledLinearVelocity = Vector3.Lerp(sampledLinearVelocity, rawLinearVelocity, blend);
            sampledAngularVelocity = Vector3.Lerp(sampledAngularVelocity, rawAngularVelocity, blend);

            previousTargetPosition = target.position;
            previousTargetRotation = target.rotation;
        }

        private void ResetVelocitySampling()
        {
            Transform target = GripTarget;
            previousTargetPosition = target.position;
            previousTargetRotation = target.rotation;
            sampledLinearVelocity = Vector3.zero;
            sampledAngularVelocity = Vector3.zero;
            hasVelocitySample = false;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            grabRadius = Mathf.Max(0.01f, grabRadius);
            velocitySmoothing = Mathf.Clamp01(velocitySmoothing);

            if (gripTarget == null)
            {
                gripTarget = transform;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireSphere(GripTarget.position, grabRadius);
        }
#endif
    }
}