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
        private const int CurrentSettingsVersion = 1;

        private sealed class LegState
        {
            public Transform upperLeg;
            public Transform lowerLeg;
            public Transform foot;
            public float upperLength;
            public float lowerLength;
            public float lateralOffset;
            public float forwardOffset;
            public float colliderSoleOffset;
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
        [SerializeField, Min(0f)] private float additionalSoleClearance = 0.012f;
        [SerializeField, Min(0f)] private float visualSoleLift = 0.012f;
        [SerializeField, Range(0.03f, 0.2f)] private float soleHeightRatio = 0.08f;
        [SerializeField, Min(0f)] private float minimumSoleHeight = 0.025f;
        [SerializeField, Min(0f)] private float maximumSoleHeight = 0.12f;
        [SerializeField, Min(0f)] private float maximumHipsCorrection = 0.45f;
        [SerializeField, Min(0f)] private float hipsHeightFollowSpeed = 8f;
        [SerializeField] private LayerMask groundLayers = ~0;

        [Header("Stepping")]
        [SerializeField, Min(0.05f)] private float stepDistance = 0.18f;
        [SerializeField, Min(0.05f)] private float stepDuration = 0.17f;
        [SerializeField, Min(0f)] private float stepHeight = 0.12f;
        [SerializeField, Min(0f)] private float forwardStepLead = 0.22f;
        [SerializeField, Min(0f)] private float speedLeadMultiplier = 0.06f;
        [SerializeField, Min(0f)] private float maximumForwardLead = 0.45f;
        [SerializeField, Range(0f, 1f)] private float nextStepOverlap = 0.58f;
        [SerializeField, Min(0f)] private float minimumMovingSpeed = 0.08f;
        [SerializeField, Min(0f)] private float teleportResetDistance = 1.2f;
        [SerializeField, HideInInspector] private int settingsVersion;

        private readonly LegState leftLeg = new LegState();
        private readonly LegState rightLeg = new LegState();
        private readonly RaycastHit[] groundHits = new RaycastHit[24];

        private Rigidbody motorBody;
        private Transform hipsAnchor;
        private Transform targetHips;
        private Vector3 baseHipsAnchorLocalPosition;
        private float standingHipsHeight;
        private bool hipsHeightCalibrated;
        private bool initialized;
        private bool subscribed;
        private bool preferLeftStep = true;
        private Vector3 lastMotorPosition;

        public bool IsReady => initialized && leftLeg.valid && rightLeg.valid;

        private void Awake()
        {
            ApplyVersionedDefaults();
            Configure(character != null ? character : GetComponent<ApexPCPhysicalCharacter>());
        }

        private void OnEnable()
        {
            ApplyVersionedDefaults();
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

            GroundHipsToFloor(false);

            Vector3 planarVelocity = Vector3.ProjectOnPlane(motorBody.velocity, Vector3.up);
            float speed = planarVelocity.magnitude;
            Vector3 movementDirection = speed > minimumMovingSpeed
                ? planarVelocity / speed
                : Vector3.ProjectOnPlane(motorBody.transform.forward, Vector3.up).normalized;
            if (movementDirection.sqrMagnitude < 0.0001f)
            {
                movementDirection = Vector3.forward;
            }

            ResolveDesiredGroundPoint(
                leftLeg,
                movementDirection,
                speed,
                out Vector3 leftDesired,
                out Vector3 leftNormal);
            ResolveDesiredGroundPoint(
                rightLeg,
                movementDirection,
                speed,
                out Vector3 rightDesired,
                out Vector3 rightNormal);

            UpdateStep(leftLeg);
            UpdateStep(rightLeg);
            TryBeginNeededStep(
                leftDesired,
                leftNormal,
                rightDesired,
                rightNormal,
                speed);

            Vector3 leftTarget = GetCurrentTarget(leftLeg, out Vector3 leftTargetNormal);
            Vector3 rightTarget = GetCurrentTarget(rightLeg, out Vector3 rightTargetNormal);

            SolveLeg(leftLeg, leftTarget, leftTargetNormal);
            SolveLeg(rightLeg, rightTarget, rightTargetNormal);
        }

        public void Configure(ApexPCPhysicalCharacter owner)
        {
            ApplyVersionedDefaults();
            Unsubscribe();
            character = owner;
            targetAnimator = owner != null && owner.Humanoid != null
                ? owner.Humanoid.TargetAnimator
                : null;
            targetHips = owner != null && owner.Humanoid != null
                ? owner.Humanoid.TargetHips
                : null;
            motorBody = owner != null ? owner.MotorBody : null;
            hipsAnchor = motorBody != null
                ? motorBody.transform.Find("Hips Anchor")
                : null;

            if (hipsAnchor != null)
            {
                baseHipsAnchorLocalPosition = hipsAnchor.localPosition;
            }

            hipsHeightCalibrated = false;
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

            if (targetHips == null)
            {
                targetHips = character.Humanoid.TargetHips;
            }

            if (motorBody == null)
            {
                motorBody = character.MotorBody;
            }

            if (hipsAnchor == null && motorBody != null)
            {
                hipsAnchor = motorBody.transform.Find("Hips Anchor");
                if (hipsAnchor != null)
                {
                    baseHipsAnchorLocalPosition = hipsAnchor.localPosition;
                    hipsHeightCalibrated = false;
                }
            }

            if (!leftLeg.valid || !rightLeg.valid)
            {
                CacheLegs();
            }

            return targetAnimator != null && targetAnimator.isHuman && motorBody != null &&
                   hipsAnchor != null && targetHips != null && leftLeg.valid && rightLeg.valid;
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
            leg.colliderSoleOffset = 0f;
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
            leg.colliderSoleOffset = ResolvePhysicalFootSoleOffset(footRole);
            leg.valid = leg.upperLength > 0.01f && leg.lowerLength > 0.01f;
        }

        private float ResolvePhysicalFootSoleOffset(HumanBodyBones footRole)
        {
            if (character == null || character.Humanoid == null ||
                character.Humanoid.PhysicalCharacter == null)
            {
                return 0f;
            }

            Animator physicalAnimator =
                character.Humanoid.PhysicalCharacter.GetComponentInChildren<Animator>(true);
            if (physicalAnimator == null || !physicalAnimator.isHuman)
            {
                return 0f;
            }

            Transform physicalFoot = physicalAnimator.GetBoneTransform(footRole);
            if (physicalFoot == null)
            {
                return 0f;
            }

            Collider[] footColliders = physicalFoot.GetComponents<Collider>();
            float greatestOffset = 0f;
            for (int i = 0; i < footColliders.Length; i++)
            {
                Collider footCollider = footColliders[i];
                if (footCollider == null || footCollider.isTrigger)
                {
                    continue;
                }

                Bounds bounds = footCollider.bounds;
                if (bounds.size.sqrMagnitude < 0.000001f)
                {
                    continue;
                }

                float offset = physicalFoot.position.y - bounds.min.y;
                greatestOffset = Mathf.Max(greatestOffset, offset);
            }

            return Mathf.Clamp(greatestOffset, 0f, maximumSoleHeight);
        }

        private bool InitializeFeet()
        {
            if (!ResolveReferences())
            {
                return false;
            }

            CalibrateStandingHipsHeight();
            GroundHipsToFloor(true);
            InitializeLeg(leftLeg);
            InitializeLeg(rightLeg);
            initialized = leftLeg.valid && rightLeg.valid;
            preferLeftStep = true;
            lastMotorPosition = motorBody.position;
            return initialized;
        }

        private void CalibrateStandingHipsHeight()
        {
            if (hipsHeightCalibrated || targetHips == null || !leftLeg.valid || !rightLeg.valid)
            {
                return;
            }

            float leftHeight = Vector3.Dot(targetHips.position - leftLeg.foot.position, Vector3.up) +
                               EstimateSoleOffset(leftLeg);
            float rightHeight = Vector3.Dot(targetHips.position - rightLeg.foot.position, Vector3.up) +
                                EstimateSoleOffset(rightLeg);

            standingHipsHeight = Mathf.Max(0.2f, (leftHeight + rightHeight) * 0.5f);
            baseHipsAnchorLocalPosition = hipsAnchor.localPosition;
            hipsHeightCalibrated = true;
        }

        private void GroundHipsToFloor(bool snap)
        {
            if (!hipsHeightCalibrated || hipsAnchor == null || motorBody == null ||
                !TryFindGround(motorBody.position, out Vector3 groundPoint, out _))
            {
                return;
            }

            Vector3 desiredWorldPosition = hipsAnchor.position;
            desiredWorldPosition.y = groundPoint.y + standingHipsHeight;
            Vector3 desiredLocalPosition =
                motorBody.transform.InverseTransformPoint(desiredWorldPosition);

            desiredLocalPosition.x = baseHipsAnchorLocalPosition.x;
            desiredLocalPosition.z = baseHipsAnchorLocalPosition.z;
            desiredLocalPosition.y = Mathf.Clamp(
                desiredLocalPosition.y,
                baseHipsAnchorLocalPosition.y - maximumHipsCorrection,
                baseHipsAnchorLocalPosition.y + maximumHipsCorrection * 0.25f);

            Vector3 localPosition = hipsAnchor.localPosition;
            localPosition.x = baseHipsAnchorLocalPosition.x;
            localPosition.z = baseHipsAnchorLocalPosition.z;
            localPosition.y = snap
                ? desiredLocalPosition.y
                : Mathf.MoveTowards(
                    localPosition.y,
                    desiredLocalPosition.y,
                    hipsHeightFollowSpeed * Time.deltaTime);
            hipsAnchor.localPosition = localPosition;

            ApexHumanoidTargetRootDriver rootDriver = character.Humanoid.TargetRootDriver;
            if (rootDriver != null)
            {
                rootDriver.SnapNow();
            }
        }

        private void InitializeLeg(LegState leg)
        {
            Vector3 localFoot = motorBody.transform.InverseTransformPoint(leg.foot.position);
            leg.lateralOffset = localFoot.x;
            leg.forwardOffset = localFoot.z;
            leg.footRotationOffset = Quaternion.Inverse(motorBody.rotation) * leg.foot.rotation;
            leg.soleOffset = EstimateSoleOffset(leg);

            if (TryFindGround(leg.foot.position, out Vector3 point, out Vector3 normal))
            {
                leg.plantedPosition = point + normal * (leg.soleOffset + additionalSoleClearance);
                leg.plantedNormal = normal;
            }
            else
            {
                leg.plantedPosition = leg.foot.position;
                leg.plantedNormal = Vector3.up;
            }

            leg.stepStart = leg.plantedPosition;
            leg.stepEnd = leg.plantedPosition;
            leg.stepEndNormal = leg.plantedNormal;
            leg.stepping = false;
        }

        private float EstimateSoleOffset(LegState leg)
        {
            float proportionEstimate = leg.lowerLength * soleHeightRatio;
            float estimated = Mathf.Max(proportionEstimate, leg.colliderSoleOffset);
            return Mathf.Clamp(
                estimated + visualSoleLift,
                minimumSoleHeight,
                maximumSoleHeight);
        }

        private void ResolveDesiredGroundPoint(
            LegState leg,
            Vector3 movementDirection,
            float speed,
            out Vector3 desiredPosition,
            out Vector3 desiredNormal)
        {
            Transform motor = motorBody.transform;
            float lead = speed > minimumMovingSpeed
                ? Mathf.Min(
                    maximumForwardLead,
                    forwardStepLead + speed * speedLeadMultiplier)
                : 0f;
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

        private void TryBeginNeededStep(
            Vector3 leftDesired,
            Vector3 leftNormal,
            Vector3 rightDesired,
            Vector3 rightNormal,
            float speed)
        {
            float leftError = PlanarDistance(leftLeg.plantedPosition, leftDesired);
            float rightError = PlanarDistance(rightLeg.plantedPosition, rightDesired);
            float movingThreshold = Mathf.Max(0.08f, stepDistance - speed * 0.015f);
            float threshold = speed > minimumMovingSpeed
                ? movingThreshold
                : stepDistance * 1.35f;

            bool leftCanStep = !leftLeg.stepping &&
                               (!rightLeg.stepping || GetStepProgress(rightLeg) >= nextStepOverlap);
            bool rightCanStep = !rightLeg.stepping &&
                                (!leftLeg.stepping || GetStepProgress(leftLeg) >= nextStepOverlap);
            bool leftNeedsStep = leftCanStep && leftError > threshold;
            bool rightNeedsStep = rightCanStep && rightError > threshold;

            if (!leftNeedsStep && !rightNeedsStep)
            {
                return;
            }

            bool stepLeft;
            if (leftNeedsStep && !rightNeedsStep)
            {
                stepLeft = true;
            }
            else if (rightNeedsStep && !leftNeedsStep)
            {
                stepLeft = false;
            }
            else
            {
                stepLeft = leftError > rightError + 0.025f
                    ? true
                    : rightError > leftError + 0.025f
                        ? false
                        : preferLeftStep;
            }

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

            float progress = GetStepProgress(leg);
            if (progress < 1f)
            {
                return;
            }

            leg.plantedPosition = leg.stepEnd;
            leg.plantedNormal = leg.stepEndNormal;
            leg.stepping = false;
        }

        private float GetStepProgress(LegState leg)
        {
            return leg.stepping
                ? Mathf.Clamp01((Time.time - leg.stepStartedAt) / stepDuration)
                : 1f;
        }

        private Vector3 GetCurrentTarget(LegState leg, out Vector3 normal)
        {
            if (!leg.stepping)
            {
                normal = leg.plantedNormal;
                return leg.plantedPosition;
            }

            float progress = GetStepProgress(leg);
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
            if (state == ApexPCCharacterState.Active && hipsAnchor != null)
            {
                baseHipsAnchorLocalPosition = hipsAnchor.localPosition;
                hipsHeightCalibrated = false;
            }
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

        private void ApplyVersionedDefaults()
        {
            if (settingsVersion >= CurrentSettingsVersion)
            {
                return;
            }

            additionalSoleClearance = Mathf.Max(additionalSoleClearance, 0.012f);
            visualSoleLift = Mathf.Max(visualSoleLift, 0.012f);
            maximumSoleHeight = Mathf.Max(maximumSoleHeight, 0.12f);
            settingsVersion = CurrentSettingsVersion;
        }

        private static float PlanarDistance(Vector3 first, Vector3 second)
        {
            return Vector3.ProjectOnPlane(first - second, Vector3.up).magnitude;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyVersionedDefaults();
            groundProbeHeight = Mathf.Max(0.05f, groundProbeHeight);
            groundProbeDistance = Mathf.Max(0.05f, groundProbeDistance);
            additionalSoleClearance = Mathf.Max(0f, additionalSoleClearance);
            visualSoleLift = Mathf.Max(0f, visualSoleLift);
            soleHeightRatio = Mathf.Clamp(soleHeightRatio, 0.03f, 0.2f);
            minimumSoleHeight = Mathf.Max(0f, minimumSoleHeight);
            maximumSoleHeight = Mathf.Max(minimumSoleHeight, maximumSoleHeight);
            maximumHipsCorrection = Mathf.Max(0f, maximumHipsCorrection);
            hipsHeightFollowSpeed = Mathf.Max(0f, hipsHeightFollowSpeed);
            stepDistance = Mathf.Max(0.05f, stepDistance);
            stepDuration = Mathf.Max(0.05f, stepDuration);
            stepHeight = Mathf.Max(0f, stepHeight);
            forwardStepLead = Mathf.Max(0f, forwardStepLead);
            speedLeadMultiplier = Mathf.Max(0f, speedLeadMultiplier);
            maximumForwardLead = Mathf.Max(forwardStepLead, maximumForwardLead);
            nextStepOverlap = Mathf.Clamp01(nextStepOverlap);
            minimumMovingSpeed = Mathf.Max(0f, minimumMovingSpeed);
            teleportResetDistance = Mathf.Max(0f, teleportResetDistance);
        }
#endif
    }
}
