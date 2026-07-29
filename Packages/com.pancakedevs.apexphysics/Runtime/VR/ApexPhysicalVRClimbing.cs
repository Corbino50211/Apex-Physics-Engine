using UnityEngine;
using UnityEngine.XR;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>Marks a collider hierarchy as a surface that Apex VR hands may climb.</summary>
    [DisallowMultipleComponent]
    public sealed class ApexClimbable : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float gripRadiusMultiplier = 1f;
        [SerializeField] private bool allowLeftHand = true;
        [SerializeField] private bool allowRightHand = true;

        public float GripRadiusMultiplier => Mathf.Max(0f, gripRadiusMultiplier);

        public bool AllowsHand(bool leftHand)
        {
            return leftHand ? allowLeftHand : allowRightHand;
        }
    }

    /// <summary>
    /// Pulls the physical OpenXR body opposite tracked-controller movement while one or
    /// both hands are anchored to an ApexClimbable surface.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(ApexPhysicalOpenXRBody), typeof(Rigidbody))]
    public sealed class ApexPhysicalVRClimber : MonoBehaviour
    {
        private const int OverlapCapacity = 24;

        [Header("Climbing")]
        [SerializeField, Min(0.02f)] private float gripRadius = 0.16f;
        [SerializeField] private LayerMask climbableLayers = ~0;
        [SerializeField, Min(0f)] private float pullStrength = 28f;
        [SerializeField, Min(0f)] private float pullDamping = 0.9f;
        [SerializeField, Min(0f)] private float maximumClimbSpeed = 7f;
        [SerializeField, Min(0f)] private float releaseVelocityMultiplier = 1f;
        [SerializeField, Min(0f)] private float maximumReleaseSpeed = 8f;
        [SerializeField] private bool cancelLocomotionWhileClimbing = true;
        [SerializeField] private bool counterGravityWhileClimbing = true;

        private readonly Collider[] overlapResults = new Collider[OverlapCapacity];

        private ApexPhysicalOpenXRBody physicalBody;
        private Rigidbody body;
        private ApexPhysicalOpenXRHand leftHand;
        private ApexPhysicalOpenXRHand rightHand;
        private HandAnchor leftAnchor;
        private HandAnchor rightAnchor;
        private bool previousLeftGrip;
        private bool previousRightGrip;
        private Vector3 previousLeftTrackedPosition;
        private Vector3 previousRightTrackedPosition;
        private Vector3 leftTrackedVelocity;
        private Vector3 rightTrackedVelocity;
        private bool hasVelocitySamples;

        public bool IsClimbing => leftAnchor.Active || rightAnchor.Active;
        public bool IsLeftHandClimbing => leftAnchor.Active;
        public bool IsRightHandClimbing => rightAnchor.Active;

        private struct HandAnchor
        {
            public bool Active;
            public ApexClimbable Climbable;
            public Transform SurfaceTransform;
            public Vector3 LocalSurfacePoint;
            public Vector3 ControllerStartPosition;
            public Vector3 BodyStartPosition;
        }

        private void Awake()
        {
            ResolveReferences();
            ResetVelocitySamples();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ResetVelocitySamples();
        }

        private void OnDisable()
        {
            leftAnchor = default;
            rightAnchor = default;
        }

        private void Update()
        {
            ResolveReferences();
            SampleTrackedVelocities();

            InputDevice leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            InputDevice rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            leftDevice.TryGetFeatureValue(CommonUsages.gripButton, out bool leftGrip);
            rightDevice.TryGetFeatureValue(CommonUsages.gripButton, out bool rightGrip);

            UpdateGrip(true, leftGrip, ref previousLeftGrip, leftHand, ref leftAnchor);
            UpdateGrip(false, rightGrip, ref previousRightGrip, rightHand, ref rightAnchor);
        }

        private void FixedUpdate()
        {
            if (!IsClimbing || body == null)
            {
                return;
            }

            Vector3 desiredBodyPosition = Vector3.zero;
            int activeHands = 0;

            AccumulateDesiredBodyPosition(leftHand, ref leftAnchor, ref desiredBodyPosition, ref activeHands);
            AccumulateDesiredBodyPosition(rightHand, ref rightAnchor, ref desiredBodyPosition, ref activeHands);

            if (activeHands == 0)
            {
                return;
            }

            desiredBodyPosition /= activeHands;
            Vector3 positionError = desiredBodyPosition - body.position;
            Vector3 desiredVelocity = Vector3.ClampMagnitude(positionError * pullStrength, maximumClimbSpeed);
            Vector3 velocityChange = (desiredVelocity - body.linearVelocity) * pullDamping;
            body.AddForce(velocityChange, ForceMode.VelocityChange);

            if (counterGravityWhileClimbing && body.useGravity)
            {
                body.AddForce(-Physics.gravity, ForceMode.Acceleration);
            }

            if (cancelLocomotionWhileClimbing)
            {
                physicalBody?.SetMoveInput(Vector2.zero);
            }
        }

        private void UpdateGrip(
            bool isLeft,
            bool held,
            ref bool previous,
            ApexPhysicalOpenXRHand hand,
            ref HandAnchor anchor)
        {
            if (held && !previous)
            {
                bool holdingObject = hand != null && hand.Grabber != null && hand.Grabber.IsHolding;
                if (!holdingObject)
                {
                    TryBeginClimb(isLeft, hand, ref anchor);
                }
            }
            else if (!held && previous && anchor.Active)
            {
                EndClimb(isLeft, ref anchor, true);
            }

            if (anchor.Active && hand != null && hand.Grabber != null && hand.Grabber.IsHolding)
            {
                EndClimb(isLeft, ref anchor, false);
            }

            previous = held;
        }

        private bool TryBeginClimb(bool isLeft, ApexPhysicalOpenXRHand hand, ref HandAnchor anchor)
        {
            if (hand == null || hand.TrackingTarget == null || body == null)
            {
                return false;
            }

            Vector3 handPosition = hand.transform.position;
            int hitCount = Physics.OverlapSphereNonAlloc(
                handPosition,
                gripRadius,
                overlapResults,
                climbableLayers,
                QueryTriggerInteraction.Collide);

            ApexClimbable bestClimbable = null;
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
                if (climbable == null || !climbable.isActiveAndEnabled || !climbable.AllowsHand(isLeft))
                {
                    continue;
                }

                Vector3 point = hit.ClosestPoint(handPosition);
                float allowedRadius = gripRadius * Mathf.Max(0.01f, climbable.GripRadiusMultiplier);
                float distance = Vector3.Distance(handPosition, point);
                if (distance > allowedRadius || distance >= bestDistance)
                {
                    continue;
                }

                bestClimbable = climbable;
                bestCollider = hit;
                bestPoint = point;
                bestDistance = distance;
            }

            if (bestClimbable == null || bestCollider == null)
            {
                return false;
            }

            Transform surface = bestCollider.transform;
            anchor = new HandAnchor
            {
                Active = true,
                Climbable = bestClimbable,
                SurfaceTransform = surface,
                LocalSurfacePoint = surface.InverseTransformPoint(bestPoint),
                ControllerStartPosition = hand.TrackingTarget.position,
                BodyStartPosition = body.position
            };

            body.WakeUp();
            return true;
        }

        private void AccumulateDesiredBodyPosition(
            ApexPhysicalOpenXRHand hand,
            ref HandAnchor anchor,
            ref Vector3 total,
            ref int count)
        {
            if (!anchor.Active)
            {
                return;
            }

            if (anchor.Climbable == null || anchor.SurfaceTransform == null ||
                !anchor.Climbable.isActiveAndEnabled || hand == null || hand.TrackingTarget == null)
            {
                anchor = default;
                return;
            }

            Vector3 surfacePoint = anchor.SurfaceTransform.TransformPoint(anchor.LocalSurfacePoint);
            Vector3 surfaceDelta = surfacePoint - anchor.SurfaceTransform.TransformPoint(anchor.LocalSurfacePoint);
            Vector3 controllerDelta = anchor.ControllerStartPosition - hand.TrackingTarget.position;
            total += anchor.BodyStartPosition + controllerDelta + surfaceDelta;
            count++;
        }

        private void EndClimb(bool isLeft, ref HandAnchor anchor, bool applyReleaseVelocity)
        {
            if (!anchor.Active)
            {
                return;
            }

            anchor = default;

            if (applyReleaseVelocity && body != null)
            {
                Vector3 trackedVelocity = isLeft ? leftTrackedVelocity : rightTrackedVelocity;
                Vector3 releaseVelocity = -trackedVelocity * releaseVelocityMultiplier;
                releaseVelocity = Vector3.ClampMagnitude(releaseVelocity, maximumReleaseSpeed);
                body.linearVelocity = Vector3.Lerp(body.linearVelocity, releaseVelocity, 0.75f);
            }
        }

        private void ResolveReferences()
        {
            if (physicalBody == null)
            {
                physicalBody = GetComponent<ApexPhysicalOpenXRBody>();
            }
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }

            ApexPhysicalOpenXRHand[] hands = GetComponentsInChildren<ApexPhysicalOpenXRHand>(true);
            foreach (ApexPhysicalOpenXRHand hand in hands)
            {
                string lowerName = hand.name.ToLowerInvariant();
                if (lowerName.Contains("left"))
                {
                    leftHand = hand;
                }
                else if (lowerName.Contains("right"))
                {
                    rightHand = hand;
                }
            }
        }

        private void SampleTrackedVelocities()
        {
            if (leftHand == null || rightHand == null ||
                leftHand.TrackingTarget == null || rightHand.TrackingTarget == null)
            {
                return;
            }

            float deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            Vector3 leftPosition = leftHand.TrackingTarget.position;
            Vector3 rightPosition = rightHand.TrackingTarget.position;

            if (!hasVelocitySamples)
            {
                previousLeftTrackedPosition = leftPosition;
                previousRightTrackedPosition = rightPosition;
                hasVelocitySamples = true;
                return;
            }

            leftTrackedVelocity = Vector3.Lerp(
                leftTrackedVelocity,
                (leftPosition - previousLeftTrackedPosition) / deltaTime,
                0.5f);
            rightTrackedVelocity = Vector3.Lerp(
                rightTrackedVelocity,
                (rightPosition - previousRightTrackedPosition) / deltaTime,
                0.5f);
            previousLeftTrackedPosition = leftPosition;
            previousRightTrackedPosition = rightPosition;
        }

        private void ResetVelocitySamples()
        {
            hasVelocitySamples = false;
            leftTrackedVelocity = Vector3.zero;
            rightTrackedVelocity = Vector3.zero;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            ApexPhysicalOpenXRHand[] hands = GetComponentsInChildren<ApexPhysicalOpenXRHand>(true);
            Gizmos.color = Color.cyan;
            foreach (ApexPhysicalOpenXRHand hand in hands)
            {
                Gizmos.DrawWireSphere(hand.transform.position, gripRadius);
            }
        }
#endif
    }

    /// <summary>Adds climbing support to existing physical OpenXR rigs at runtime.</summary>
    internal static class ApexPhysicalVRClimbingBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallClimbers()
        {
            ApexPhysicalOpenXRBody[] bodies = Object.FindObjectsByType<ApexPhysicalOpenXRBody>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (ApexPhysicalOpenXRBody body in bodies)
            {
                if (body != null && body.GetComponent<ApexPhysicalVRClimber>() == null)
                {
                    body.gameObject.AddComponent<ApexPhysicalVRClimber>();
                }
            }
        }
    }
}
