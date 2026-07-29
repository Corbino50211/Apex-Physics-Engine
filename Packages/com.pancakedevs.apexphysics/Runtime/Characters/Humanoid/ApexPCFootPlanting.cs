using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Grounds the hidden Humanoid target's feet while an Apex PC character is Active.
    /// Feet remain planted in world space until the motor moves far enough to request a
    /// step, then travel to a new ground point along a short lift arc. A lightweight
    /// two-bone IK solve keeps the knees and ankles connected to those planted targets.
    /// </summary>
    [DefaultExecutionOrder(-275)]
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class ApexPCFootPlanting : MonoBehaviour
    {
        private sealed class LegState
        {
            public Transform upperLeg;
            public Transform lowerLeg;
            public Transform foot;
            public float upperLength;
            public float lowerLength;
            public float lateralOffset;
            public float forwardOffset;
            public float soleOffset;
            public Quaternion footRotationOffset;
            public Vector3 plantedPosition;
            public Vector3 plantedNormal = Vector3.up;
            public Vector3 stepStart;
            public Vector3 stepEnd;
            public Vector3 stepEndNormal = Vector3.up;
            public float stepStartedAt;
            public bool stepping;
            public bool valid;
        }

        [SerializeField] private ApexPCPhysicalCharacter character;
        [SerializeField] private Animator targetAnimator;

        [Header("Grounding")]
        [SerializeField, Min(0.05f)] private float groundProbeHeight = 0.8f;
        [SerializeField, Min(0.05f)] private float groundProbeDistance = 1.6f;
        [SerializeField, Min(0f)] private float additionalSoleClearance = 0.015f;
        [SerializeField] private LayerMask groundLayers = ~0;

        [Header("Stepping")]
        [SerializeField, Min(0.05f)] private float stepDistance = 0.28f;
        [SerializeField, Min(0.05f)] private float stepDuration = 0.24f;
        [SerializeField, Min(0f)] private float stepHeight = 0.13f;
        [SerializeField, Min(0f)] private float forwardStepLead = 0.18f;
        [SerializeField, Min(0f)] private float minimumMovingSpeed = 0.08f;
        [SerializeField, Min(0f)] private float teleportResetDistance = 1.2f;

        private readonly LegState leftLeg = new LegState();
        private readonly LegState rightLeg = new LegState();
        private readonly RaycastHit[] groundHits = new RaycastHit[24];

        private Rigidbody motorBody;
        private bool initialized;
        private bool subscribed;
        private bool preferLeftStep = true;
        private Vector3 lastMotorPosition;

        public bool IsReady => initialized && leftLeg.valid && rightLeg.valid;

        private void Awake()
        {
            Configure(character != null ? character : GetComponent<ApexPCPhysicalCharacter>());
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            initialized = false;
        }

        private void LateUpdate()
        {
            if (!ResolveReferences() || character.State == ApexPCCharacterState.Ragdoll ||
                character.State == ApexPCCharacterState.GettingUp)
            {
                initialized = false;
                return;
            }

            if (!initialized && !InitializeFeet())
            {
                return;
            }

            Vector3 motorPosition = motorBody.position;
            if ((motorPosition - lastMotorPosition).sqrMagnitude >
                teleportResetDistance * teleportResetDistance)
            {
                InitializeFeet();
            }
            lastMotorPosition = motorPosition;

            Vector3 planarVelocity = Vector3.ProjectOnPlane(motorBody.velocity, Vector3.up);
            float speed = planarVelocity.magnitude;
            Vector3 movementDirection = speed > minimumMovingSpeed
                ? planarVelocity / speed
                : Vector3.ProjectOnPlane(motorBody.transform.forward, Vector3.up).normalized;
            if (movementDirection.sqrMagnitude < 0.0001f)
            {
                movementDirection = Vector3.forward;
            }

            ResolveDesiredGroundPoint(leftLeg, movementDirection, speed, out Vector3 leftDesired, out Vector3 leftNormal);
            ResolveDesiredGroundPoint(rightLeg, movementDirection, speed, out Vector3 rightDesired, out Vector3 rightNormal);

            UpdateStep(leftLeg);
            UpdateStep(rightLeg);

            if (!leftLeg.stepping && !rightLeg.stepping)
            {
                float leftError = PlanarDistance(leftLeg.plantedPosition, leftDesired);
                float rightError = PlanarDistance(rightLeg.plantedPosition, rightDesired);
                float threshold = speed > minimumMovingSpeed ? stepDistance : stepDistance * 1.35f;

                if (leftError > threshold || rightError > threshold)
                {
                    bool stepLeft = leftError > rightError + 0.025f
                        ? true
                        : rightError > leftError + 0.025f
                            ? false
                            : preferLeftStep;

                    if (stepLeft)
                    {
                        BeginStep(leftLeg, leftDesired, leftNormal);
                    }
                    else
                    {
                        BeginStep(rightLeg, rightDesired, rightNormal);
                    }
                    preferLeftStep = !stepLeft;
                }
            }

            Vector3 leftTarget = GetCurrentTarget(leftLeg, out Vector3 leftTargetNormal);
            Vector3 rightTarget = GetCurrentTarget(rightLeg, out Vector3 rightTargetNormal);

            SolveLeg(leftLeg, leftTarget, leftTargetNormal);
            SolveLeg(rightLeg, rightTarget, rightTargetNormal);
        }

        public void Configure(ApexPCPhysicalCharacter owner)
        {
            Unsubscribe();
            character = owner;
            targetAnimator = owner != null && owner.Humanoid != null
                ? owner.Humanoid.TargetAnimator
                : null;
            motorBody = owner != null ? owner.MotorBody : null;
            CacheLegs();
            initialized = false;
            Subscribe();
        }

        public void ReplantNow()
        {
            initialized = false;
            InitializeFeet();
        }

        private bool ResolveReferences()
        {
            if (character == null)
            {
                character = GetComponent<ApexPCPhysicalCharacter>();
            }

            if (character == null || character.Humanoid == null)
            {
                return false;
            }

            if (targetAnimator == null)
            {
                targetAnimator = character.Humanoid.TargetAnimator;
            }

            if (motorBody == null)
            {
                motorBody = character.MotorBody;
            }

            if (!leftLeg.valid || !rightLeg.valid)
            {
                CacheLegs();
            }

            return targetAnimator != null && targetAnimator.isHuman && motorBody != null &&
                   leftLeg.valid && rightLeg.valid;
        }

        private void CacheLegs()
        {
            CacheLeg(
                leftLeg,
                HumanBodyBones.LeftUpperLeg,
                HumanBodyBones.LeftLowerLeg,
                HumanBodyBones.LeftFoot);
            CacheLeg(
                rightLeg,
                HumanBodyBones.RightUpperLeg,
                HumanBodyBones.RightLowerLeg,
                HumanBodyBones.RightFoot);
        }

        private void CacheLeg(
            LegState leg,
            HumanBodyBones upperRole,
            HumanBodyBones lowerRole,
            HumanBodyBones footRole)
        {
            leg.valid = false;
            if (targetAnimator == null || !targetAnimator.isHuman)
            {
                return;
            }

            leg.upperLeg = targetAnimator.GetBoneTransform(upperRole);
            leg.lowerLeg = targetAnimator.GetBoneTransform(lowerRole);
            leg.foot = targetAnimator.GetBoneTransform(footRole);
            if (leg.upperLeg == null || leg.lowerLeg == null || leg.foot == null)
            {
                return;
            }

            leg.upperLength = Vector3.Distance(leg.upperLeg.position, leg.lowerLeg.position);
            leg.lowerLength = Vector3.Distance(leg.lowerLeg.position, leg.foot.position);
            leg.valid = leg.upperLength > 0.01f && leg.lowerLength > 0.01f;
        }

        private bool InitializeFeet()
        {
            if (!ResolveReferences())
            {
                return false;
            }

            InitializeLeg(leftLeg);
            InitializeLeg(rightLeg);
            initialized = leftLeg.valid && rightLeg.valid;
            preferLeftStep = true;
            lastMotorPosition = motorBody.position;
            return initialized;
        }

        private void InitializeLeg(LegState leg)
        {
            Vector3 localFoot = motorBody.transform.InverseTransformPoint(leg.foot.position);
            leg.lateralOffset = localFoot.x;
            leg.forwardOffset = localFoot.z;
            leg.footRotationOffset = Quaternion.Inverse(motorBody.rotation) * leg.foot.rotation;

            if (TryFindGround(leg.foot.position, out Vector3 point, out Vector3 normal))
            {
                leg.soleOffset = Mathf.Clamp(
                    Vector3.Dot(leg.foot.position - point, normal),
                    0.025f,
                    Mathf.Max(0.04f, leg.lowerLength * 0.45f));
                leg.plantedPosition = point + normal * (leg.soleOffset + additionalSoleClearance);
                leg.plantedNormal = normal;
            }
            else
            {
                leg.soleOffset = 0.06f;
                leg.plantedPosition = leg.foot.position;
                leg.plantedNormal = Vector3.up;
            }

            leg.stepStart = leg.plantedPosition;
            leg.stepEnd = leg.plantedPosition;
            leg.stepEndNormal = leg.plantedNormal;
            leg.stepping = false;
        }

        private void ResolveDesiredGroundPoint(
            LegState leg,
            Vector3 movementDirection,
            float speed,
            out Vector3 desiredPosition,
            out Vector3 desiredNormal)
        {
            Transform motor = motorBody.transform;
            float lead = speed > minimumMovingSpeed ? forwardStepLead : 0f;
            Vector3 stancePoint = motor.position +
                                  motor.right * leg.lateralOffset +
                                  motor.forward * leg.forwardOffset +
                                  movementDirection * lead;

            if (TryFindGround(stancePoint, out Vector3 groundPoint, out desiredNormal))
            {
                desiredPosition = groundPoint +
                                  desiredNormal * (leg.soleOffset + additionalSoleClearance);
                return;
            }

            desiredPosition = stancePoint;
            desiredPosition.y = leg.plantedPosition.y;
            desiredNormal = leg.plantedNormal;
        }

        private void BeginStep(
            LegState leg,
            Vector3 destination,
            Vector3 destinationNormal)
        {
            leg.stepStart = leg.plantedPosition;
            leg.stepEnd = destination;
            leg.stepEndNormal = destinationNormal;
            leg.stepStartedAt = Time.time;
            leg.stepping = true;
        }

        private void UpdateStep(LegState leg)
        {
            if (!leg.stepping)
            {
                return;
            }

            float progress = Mathf.Clamp01((Time.time - leg.stepStartedAt) / stepDuration);
            if (progress < 1f)
            {
                return;
            }

            leg.plantedPosition = leg.stepEnd;
            leg.plantedNormal = leg.stepEndNormal;
            leg.stepping = false;
        }

        private Vector3 GetCurrentTarget(LegState leg, out Vector3 normal)
        {
            if (!leg.stepping)
            {
                normal = leg.plantedNormal;
                return leg.plantedPosition;
            }

            float progress = Mathf.Clamp01((Time.time - leg.stepStartedAt) / stepDuration);
            float smooth = progress * progress * (3f - 2f * progress);
            Vector3 target = Vector3.Lerp(leg.stepStart, leg.stepEnd, smooth);
            target += Vector3.up * (Mathf.Sin(progress * Mathf.PI) * stepHeight);
            normal = Vector3.Slerp(leg.plantedNormal, leg.stepEndNormal, smooth).normalized;
            return target;
        }

        private void SolveLeg(LegState leg, Vector3 footTarget, Vector3 groundNormal)
        {
            Vector3 hipPosition = leg.upperLeg.position;
            Vector3 toTarget = footTarget - hipPosition;
            float rawDistance = toTarget.magnitude;
            if (rawDistance < 0.0001f)
            {
                return;
            }

            Vector3 direction = toTarget / rawDistance;
            float minimumReach = Mathf.Abs(leg.upperLength - leg.lowerLength) + 0.001f;
            float maximumReach = leg.upperLength + leg.lowerLength - 0.001f;
            float distance = Mathf.Clamp(rawDistance, minimumReach, maximumReach);

            Vector3 currentKnee = leg.lowerLeg.position;
            Vector3 kneeOffset = Vector3.ProjectOnPlane(currentKnee - hipPosition, direction);
            if (kneeOffset.sqrMagnitude < 0.0001f)
            {
                kneeOffset = Vector3.ProjectOnPlane(motorBody.transform.forward, direction);
            }
            if (kneeOffset.sqrMagnitude < 0.0001f)
            {
                kneeOffset = Vector3.Cross(direction, motorBody.transform.right);
            }
            Vector3 bendDirection = kneeOffset.normalized;

            float along = (leg.upperLength * leg.upperLength + distance * distance -
                           leg.lowerLength * leg.lowerLength) / (2f * distance);
            float heightSquared = Mathf.Max(0f, leg.upperLength * leg.upperLength - along * along);
            Vector3 desiredKnee = hipPosition + direction * along +
                                  bendDirection * Mathf.Sqrt(heightSquared);

            Vector3 currentUpperDirection = leg.lowerLeg.position - leg.upperLeg.position;
            Vector3 desiredUpperDirection = desiredKnee - leg.upperLeg.position;
            if (currentUpperDirection.sqrMagnitude > 0.0001f &&
                desiredUpperDirection.sqrMagnitude > 0.0001f)
            {
                leg.upperLeg.rotation = Quaternion.FromToRotation(
                    currentUpperDirection,
                    desiredUpperDirection) * leg.upperLeg.rotation;
            }

            Vector3 currentLowerDirection = leg.foot.position - leg.lowerLeg.position;
            Vector3 desiredLowerDirection = footTarget - leg.lowerLeg.position;
            if (currentLowerDirection.sqrMagnitude > 0.0001f &&
                desiredLowerDirection.sqrMagnitude > 0.0001f)
            {
                leg.lowerLeg.rotation = Quaternion.FromToRotation(
                    currentLowerDirection,
                    desiredLowerDirection) * leg.lowerLeg.rotation;
            }

            Vector3 normal = groundNormal.sqrMagnitude > 0.0001f
                ? groundNormal.normalized
                : Vector3.up;
            Quaternion slopeRotation = Quaternion.FromToRotation(Vector3.up, normal);
            leg.foot.rotation = slopeRotation * motorBody.rotation * leg.footRotationOffset;
        }

        private bool TryFindGround(
            Vector3 aroundPosition,
            out Vector3 point,
            out Vector3 normal)
        {
            Vector3 origin = aroundPosition + Vector3.up * groundProbeHeight;
            int count = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                groundHits,
                groundProbeHeight + groundProbeDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore);

            float closestDistance = float.PositiveInfinity;
            point = aroundPosition;
            normal = Vector3.up;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = groundHits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    point = hit.point;
                    normal = hit.normal;
                    found = true;
                }
            }

            return found;
        }

        private void HandleCharacterStateChanged(ApexPCCharacterState state)
        {
            initialized = false;
        }

        private void Subscribe()
        {
            if (subscribed || character == null)
            {
                return;
            }

            character.StateChanged += HandleCharacterStateChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || character == null)
            {
                subscribed = false;
                return;
            }

            character.StateChanged -= HandleCharacterStateChanged;
            subscribed = false;
        }

        private static float PlanarDistance(Vector3 first, Vector3 second)
        {
            return Vector3.ProjectOnPlane(first - second, Vector3.up).magnitude;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            groundProbeHeight = Mathf.Max(0.05f, groundProbeHeight);
            groundProbeDistance = Mathf.Max(0.05f, groundProbeDistance);
            additionalSoleClearance = Mathf.Max(0f, additionalSoleClearance);
            stepDistance = Mathf.Max(0.05f, stepDistance);
            stepDuration = Mathf.Max(0.05f, stepDuration);
            stepHeight = Mathf.Max(0f, stepHeight);
            forwardStepLead = Mathf.Max(0f, forwardStepLead);
            minimumMovingSpeed = Mathf.Max(0f, minimumMovingSpeed);
            teleportResetDistance = Mathf.Max(0f, teleportResetDistance);
        }
#endif
    }
}
