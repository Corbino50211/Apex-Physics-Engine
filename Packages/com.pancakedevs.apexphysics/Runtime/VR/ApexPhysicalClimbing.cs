using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>Marks a collider hierarchy as a surface that Apex VR hands can climb.</summary>
    [DisallowMultipleComponent]
    public sealed class ApexClimbable : MonoBehaviour
    {
        [SerializeField] private bool allowLeftHand = true;
        [SerializeField] private bool allowRightHand = true;

        public bool Allows(ApexPhysicalOpenXRHand hand)
        {
            if (hand == null)
            {
                return false;
            }

            string lowerName = hand.name.ToLowerInvariant();
            if (lowerName.Contains("left"))
            {
                return allowLeftHand;
            }

            if (lowerName.Contains("right"))
            {
                return allowRightHand;
            }

            return allowLeftHand || allowRightHand;
        }
    }

    /// <summary>
    /// Anchors tracked hands to climbable surfaces and moves the physical player body
    /// opposite controller motion. Supports one-hand hanging, two-hand climbing, moving
    /// climbables, reach enforcement, gravity compensation, and release momentum.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ApexPhysicalOpenXRBody), typeof(Rigidbody))]
    [DefaultExecutionOrder(-20)]
    public sealed class ApexPhysicalVRClimber : MonoBehaviour
    {
        private const int OverlapCapacity = 24;

        [Header("References")]
        [SerializeField] private ApexPhysicalOpenXRBody physicalBody;
        [SerializeField] private ApexPhysicalOpenXRHand leftHand;
        [SerializeField] private ApexPhysicalOpenXRHand rightHand;

        [Header("Detection")]
        [SerializeField, Min(0.02f)] private float grabRadius = 0.14f;
        [SerializeField] private LayerMask climbableLayers = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Body Pull")]
        [SerializeField, Min(0f)] private float positionStrength = 32f;
        [SerializeField, Range(0f, 1f)] private float velocityCorrection = 0.9f;
        [SerializeField, Min(0f)] private float maximumClimbSpeed = 8f;
        [SerializeField, Min(0f)] private float maximumAnchorError = 0.9f;
        [SerializeField] private bool compensateGravity = true;

        [Header("Release Momentum")]
        [SerializeField, Min(0f)] private float releaseMomentumMultiplier = 0.85f;
        [SerializeField, Min(0f)] private float maximumReleaseSpeed = 9f;

        private readonly Collider[] overlapResults = new Collider[OverlapCapacity];
        private readonly HoldState leftHold = new HoldState();
        private readonly HoldState rightHold = new HoldState();
        private Rigidbody body;

        public bool IsClimbing => leftHold.Active || rightHold.Active;
        public bool IsLeftClimbing => leftHold.Active;
        public bool IsRightClimbing => rightHold.Active;

        private sealed class HoldState
        {
            public bool Active;
            public ApexPhysicalOpenXRHand Hand;
            public ApexClimbable Climbable;
            public Transform AnchorSpace;
            public Vector3 LocalAnchor;
            public Vector3 PreviousTargetPosition;
            public Vector3 TrackingVelocity;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (physicalBody == null)
            {
                physicalBody = GetComponent<ApexPhysicalOpenXRBody>();
            }

            ResolveHands();
        }

        private void OnDisable()
        {
            ReleaseAll(false);
        }

        private void FixedUpdate()
        {
            if (!IsClimbing || body == null)
            {
                SetBodyClimbing(false);
                return;
            }

            UpdateHoldVelocity(leftHold);
            UpdateHoldVelocity(rightHold);

            Vector3 accumulatedError = Vector3.zero;
            int holdCount = 0;
            AccumulateHoldError(leftHold, ref accumulatedError, ref holdCount);
            AccumulateHoldError(rightHold, ref accumulatedError, ref holdCount);

            if (holdCount == 0)
            {
                SetBodyClimbing(false);
                return;
            }

            Vector3 averageError = accumulatedError / holdCount;
            if (maximumAnchorError > 0f && averageError.magnitude > maximumAnchorError)
            {
                ReleaseAll(true);
                return;
            }

            Vector3 desiredVelocity = Vector3.ClampMagnitude(
                averageError * positionStrength,
                maximumClimbSpeed);
            Vector3 velocityChange = (desiredVelocity - body.linearVelocity) * velocityCorrection;
            body.AddForce(velocityChange, ForceMode.VelocityChange);

            if (compensateGravity && Physics.gravity.sqrMagnitude > 0f)
            {
                body.AddForce(-Physics.gravity, ForceMode.Acceleration);
            }

            SetBodyClimbing(true);
        }

        public bool TryBeginClimb(ApexPhysicalOpenXRHand hand)
        {
            if (hand == null || hand.TrackingTarget == null)
            {
                return false;
            }

            HoldState state = GetState(hand);
            if (state == null || state.Active)
            {
                return state != null && state.Active;
            }

            int hitCount = Physics.OverlapSphereNonAlloc(
                hand.TrackingTarget.position,
                grabRadius,
                overlapResults,
                climbableLayers,
                triggerInteraction);

            ApexClimbable best = null;
            Collider bestCollider = null;
            Vector3 bestPoint = Vector3.zero;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = overlapResults[i];
                overlapResults[i] = null;
                if (hit == null)
                {
                    continue;
                }

                ApexClimbable climbable = hit.GetComponentInParent<ApexClimbable>();
                if (climbable == null || !climbable.isActiveAndEnabled || !climbable.Allows(hand))
                {
                    continue;
                }

                Vector3 point = hit.ClosestPoint(hand.TrackingTarget.position);
                float distance = (point - hand.TrackingTarget.position).sqrMagnitude;
                if (distance >= bestDistance)
                {
                    continue;
                }

                best = climbable;
                bestCollider = hit;
                bestPoint = point;
                bestDistance = distance;
            }

            if (best == null || bestCollider == null)
            {
                return false;
            }

            Transform anchorSpace = best.transform;
            state.Active = true;
            state.Hand = hand;
            state.Climbable = best;
            state.AnchorSpace = anchorSpace;
            state.LocalAnchor = anchorSpace.InverseTransformPoint(bestPoint);
            state.PreviousTargetPosition = hand.TrackingTarget.position;
            state.TrackingVelocity = Vector3.zero;
            SetBodyClimbing(true);
            return true;
        }

        public void Release(ApexPhysicalOpenXRHand hand, bool applyMomentum = true)
        {
            HoldState state = GetState(hand);
            if (state == null || !state.Active)
            {
                return;
            }

            Vector3 releaseVelocity = -state.TrackingVelocity * releaseMomentumMultiplier;
            ClearState(state);

            if (applyMomentum && !IsClimbing && body != null)
            {
                if (maximumReleaseSpeed > 0f)
                {
                    releaseVelocity = Vector3.ClampMagnitude(releaseVelocity, maximumReleaseSpeed);
                }

                body.linearVelocity = Vector3.ClampMagnitude(
                    body.linearVelocity + releaseVelocity,
                    maximumReleaseSpeed > 0f ? maximumReleaseSpeed : float.PositiveInfinity);
                body.WakeUp();
            }

            SetBodyClimbing(IsClimbing);
        }

        public bool IsHandClimbing(ApexPhysicalOpenXRHand hand)
        {
            HoldState state = GetState(hand);
            return state != null && state.Active;
        }

        public void ReleaseAll(bool applyMomentum)
        {
            Vector3 averageRelease = Vector3.zero;
            int count = 0;
            if (leftHold.Active)
            {
                averageRelease -= leftHold.TrackingVelocity;
                count++;
            }
            if (rightHold.Active)
            {
                averageRelease -= rightHold.TrackingVelocity;
                count++;
            }

            ClearState(leftHold);
            ClearState(rightHold);
            SetBodyClimbing(false);

            if (!applyMomentum || count == 0 || body == null)
            {
                return;
            }

            Vector3 releaseVelocity = averageRelease / count * releaseMomentumMultiplier;
            if (maximumReleaseSpeed > 0f)
            {
                releaseVelocity = Vector3.ClampMagnitude(releaseVelocity, maximumReleaseSpeed);
            }

            body.linearVelocity = releaseVelocity;
            body.WakeUp();
        }

        private void UpdateHoldVelocity(HoldState state)
        {
            if (!state.Active || state.Hand == null || state.Hand.TrackingTarget == null)
            {
                return;
            }

            Vector3 current = state.Hand.TrackingTarget.position;
            float deltaTime = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            state.TrackingVelocity = (current - state.PreviousTargetPosition) / deltaTime;
            state.PreviousTargetPosition = current;
        }

        private void AccumulateHoldError(HoldState state, ref Vector3 error, ref int count)
        {
            if (!state.Active || state.Hand == null || state.Hand.TrackingTarget == null ||
                state.Climbable == null || state.AnchorSpace == null)
            {
                ClearState(state);
                return;
            }

            Vector3 worldAnchor = state.AnchorSpace.TransformPoint(state.LocalAnchor);
            error += worldAnchor - state.Hand.TrackingTarget.position;
            count++;
        }

        private HoldState GetState(ApexPhysicalOpenXRHand hand)
        {
            if (hand == null)
            {
                return null;
            }

            if (hand == leftHand || hand.name.ToLowerInvariant().Contains("left"))
            {
                return leftHold;
            }

            if (hand == rightHand || hand.name.ToLowerInvariant().Contains("right"))
            {
                return rightHold;
            }

            return null;
        }

        private void ResolveHands()
        {
            ApexPhysicalOpenXRHand[] hands = GetComponentsInChildren<ApexPhysicalOpenXRHand>(true);
            foreach (ApexPhysicalOpenXRHand hand in hands)
            {
                string lowerName = hand.name.ToLowerInvariant();
                if (leftHand == null && lowerName.Contains("left"))
                {
                    leftHand = hand;
                }
                else if (rightHand == null && lowerName.Contains("right"))
                {
                    rightHand = hand;
                }
            }
        }

        private void SetBodyClimbing(bool climbing)
        {
            if (physicalBody != null)
            {
                physicalBody.SetClimbing(climbing);
            }
        }

        private static void ClearState(HoldState state)
        {
            state.Active = false;
            state.Hand = null;
            state.Climbable = null;
            state.AnchorSpace = null;
            state.LocalAnchor = Vector3.zero;
            state.PreviousTargetPosition = Vector3.zero;
            state.TrackingVelocity = Vector3.zero;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            ApexPhysicalOpenXRBody bodyMotor,
            ApexPhysicalOpenXRHand left,
            ApexPhysicalOpenXRHand right)
        {
            physicalBody = bodyMotor;
            leftHand = left;
            rightHand = right;
        }

        private void OnValidate()
        {
            grabRadius = Mathf.Max(0.02f, grabRadius);
            positionStrength = Mathf.Max(0f, positionStrength);
            maximumClimbSpeed = Mathf.Max(0f, maximumClimbSpeed);
            maximumAnchorError = Mathf.Max(0f, maximumAnchorError);
            releaseMomentumMultiplier = Mathf.Max(0f, releaseMomentumMultiplier);
            maximumReleaseSpeed = Mathf.Max(0f, maximumReleaseSpeed);
        }
#endif
    }
}
