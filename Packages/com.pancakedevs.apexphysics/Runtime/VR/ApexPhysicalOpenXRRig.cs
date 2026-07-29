using UnityEngine;
using UnityEngine.XR;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Physical OpenXR body motor. Tracking stays visual and input-only while the
    /// Rigidbody body owns collision, locomotion, jumping, and room-scale recentering.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class ApexPhysicalOpenXRBody : MonoBehaviour
    {
        [Header("Tracking")]
        [SerializeField] private Transform trackingOrigin;
        [SerializeField] private Transform head;

        [Header("Locomotion")]
        [SerializeField, Min(0f)] private float moveSpeed = 3.2f;
        [SerializeField, Min(0f)] private float acceleration = 20f;
        [SerializeField, Min(0f)] private float airControl = 0.25f;
        [SerializeField, Min(0f)] private float jumpVelocity = 4.6f;
        [SerializeField, Range(10f, 90f)] private float snapTurnDegrees = 45f;

        [Header("Room Scale")]
        [SerializeField, Min(0f)] private float recenterDeadzone = 0.08f;
        [SerializeField, Min(0f)] private float maximumRecenterPerStep = 0.2f;

        [Header("Capsule")]
        [SerializeField, Min(0.1f)] private float radius = 0.3f;
        [SerializeField, Min(0.5f)] private float minimumHeight = 0.75f;
        [SerializeField, Min(0.5f)] private float maximumHeight = 2.2f;
        [SerializeField, Min(0f)] private float headClearance = 0.08f;
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField, Min(0.01f)] private float groundProbeDistance = 0.14f;

        private Rigidbody body;
        private CapsuleCollider capsule;
        private Vector2 moveInput;
        private bool jumpQueued;
        private float turnQueued;

        public bool IsGrounded { get; private set; }
        public Rigidbody Body => body;
        public Transform Head => head;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            capsule.radius = radius;
        }

        private void FixedUpdate()
        {
            UpdateCapsule();
            UpdateGrounded();
            ApplyLocomotion();
            ApplyJump();
            ApplySnapTurn();
            RecenterBodyUnderHead();
            jumpQueued = false;
            turnQueued = 0f;
        }

        public void SetMoveInput(Vector2 input)
        {
            moveInput = Vector2.ClampMagnitude(input, 1f);
        }

        public void QueueJump()
        {
            jumpQueued = true;
        }

        public void QueueSnapTurn(float direction)
        {
            if (!Mathf.Approximately(direction, 0f))
            {
                turnQueued = Mathf.Sign(direction) * snapTurnDegrees;
            }
        }

        private void ApplyLocomotion()
        {
            if (head == null)
            {
                return;
            }

            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(head.right, Vector3.up).normalized;
            Vector3 desired = (forward * moveInput.y + right * moveInput.x) * moveSpeed;
            Vector3 planarVelocity = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            float control = IsGrounded ? 1f : airControl;
            Vector3 delta = Vector3.ClampMagnitude(
                desired - planarVelocity,
                acceleration * control * Time.fixedDeltaTime);
            body.AddForce(delta, ForceMode.VelocityChange);
        }

        private void ApplyJump()
        {
            if (!jumpQueued || !IsGrounded)
            {
                return;
            }

            Vector3 velocity = body.linearVelocity;
            velocity.y = jumpVelocity;
            body.linearVelocity = velocity;
        }

        private void ApplySnapTurn()
        {
            if (head == null || trackingOrigin == null || Mathf.Abs(turnQueued) < 0.01f)
            {
                return;
            }

            Vector3 pivot = head.position;
            Quaternion rotation = Quaternion.Euler(0f, turnQueued, 0f);
            Vector3 bodyOffset = body.position - pivot;
            Vector3 originOffset = trackingOrigin.position - pivot;

            body.position = pivot + rotation * bodyOffset;
            body.rotation = rotation * body.rotation;
            trackingOrigin.position = pivot + rotation * originOffset;
            trackingOrigin.rotation = rotation * trackingOrigin.rotation;
        }

        private void RecenterBodyUnderHead()
        {
            if (head == null || trackingOrigin == null)
            {
                return;
            }

            Vector3 horizontalOffset = Vector3.ProjectOnPlane(head.position - body.position, Vector3.up);
            if (horizontalOffset.magnitude <= recenterDeadzone)
            {
                return;
            }

            Vector3 recenter = Vector3.ClampMagnitude(horizontalOffset, maximumRecenterPerStep);
            body.position += recenter;
            trackingOrigin.position -= recenter;
        }

        private void UpdateCapsule()
        {
            if (head == null || capsule == null)
            {
                return;
            }

            float localHeadHeight = transform.InverseTransformPoint(head.position).y + headClearance;
            float height = Mathf.Clamp(localHeadHeight, minimumHeight, maximumHeight);
            capsule.radius = radius;
            capsule.height = Mathf.Max(height, radius * 2f);
            capsule.center = new Vector3(0f, capsule.height * 0.5f, 0f);
        }

        private void UpdateGrounded()
        {
            Vector3 center = transform.TransformPoint(capsule.center);
            float bottom = capsule.height * 0.5f - capsule.radius;
            Vector3 origin = center - transform.up * bottom;
            IsGrounded = Physics.SphereCast(
                origin,
                capsule.radius * 0.9f,
                Vector3.down,
                out _,
                groundProbeDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore);
        }

#if UNITY_EDITOR
        public void EditorConfigure(Transform origin, Transform trackedHead)
        {
            trackingOrigin = origin;
            head = trackedHead;
        }
#endif
    }

    /// <summary>
    /// Dynamic Rigidbody hand driven toward an OpenXR controller target with a
    /// low-latency velocity servo and a shoulder-relative reach limit.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ApexPhysicalOpenXRHand : MonoBehaviour
    {
        [SerializeField] private Transform trackingTarget;
        [SerializeField] private Transform shoulder;
        [SerializeField] private ApexGrabber grabber;
        [SerializeField, Min(0.1f)] private float maximumReach = 0.85f;
        [SerializeField, Min(0f)] private float positionStrength = 55f;
        [SerializeField, Min(0f)] private float velocityCorrection = 0.85f;
        [SerializeField, Min(0f)] private float rotationStrength = 45f;
        [SerializeField, Min(0f)] private float angularCorrection = 0.9f;
        [SerializeField, Min(0f)] private float maximumSpeed = 18f;
        [SerializeField, Min(0f)] private float maximumAngularSpeed = 40f;
        [SerializeField, Min(0f)] private float teleportDistance = 1.5f;

        private Rigidbody handBody;
        private Vector3 sampledPosition;
        private Quaternion sampledRotation = Quaternion.identity;
        private bool hasSampledPose;

        public ApexGrabber Grabber => grabber;
        public Transform TrackingTarget => trackingTarget;

        private void Awake()
        {
            handBody = GetComponent<Rigidbody>();
            handBody.maxAngularVelocity = maximumAngularSpeed;
            handBody.interpolation = RigidbodyInterpolation.None;
            if (grabber == null)
            {
                grabber = GetComponent<ApexGrabber>();
            }

            SampleTrackingPose();
        }

        private void OnEnable()
        {
            Application.onBeforeRender += SampleTrackingPose;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= SampleTrackingPose;
        }

        private void Update()
        {
            SampleTrackingPose();
        }

        private void SampleTrackingPose()
        {
            if (trackingTarget == null)
            {
                return;
            }

            sampledPosition = trackingTarget.position;
            sampledRotation = trackingTarget.rotation;
            hasSampledPose = true;
        }

        private void FixedUpdate()
        {
            if (!hasSampledPose || handBody == null)
            {
                return;
            }

            Vector3 targetPosition = sampledPosition;
            if (shoulder != null)
            {
                Vector3 shoulderToTarget = targetPosition - shoulder.position;
                if (shoulderToTarget.magnitude > maximumReach)
                {
                    targetPosition = shoulder.position + shoulderToTarget.normalized * maximumReach;
                }
            }

            Vector3 positionError = targetPosition - handBody.position;
            if (positionError.magnitude > teleportDistance)
            {
                handBody.position = targetPosition;
                handBody.rotation = sampledRotation;
                handBody.linearVelocity = Vector3.zero;
                handBody.angularVelocity = Vector3.zero;
                return;
            }

            Vector3 desiredVelocity = Vector3.ClampMagnitude(
                positionError * positionStrength,
                maximumSpeed);
            Vector3 velocityChange = (desiredVelocity - handBody.linearVelocity) * velocityCorrection;
            handBody.AddForce(velocityChange, ForceMode.VelocityChange);

            Quaternion rotationError = sampledRotation * Quaternion.Inverse(handBody.rotation);
            rotationError.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f)
            {
                angle -= 360f;
            }

            if (!float.IsNaN(axis.x) && axis.sqrMagnitude > 0.000001f)
            {
                Vector3 desiredAngularVelocity = Vector3.ClampMagnitude(
                    axis.normalized * angle * Mathf.Deg2Rad * rotationStrength,
                    maximumAngularSpeed);
                Vector3 angularChange =
                    (desiredAngularVelocity - handBody.angularVelocity) * angularCorrection;
                handBody.AddTorque(angularChange, ForceMode.VelocityChange);
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(Transform target, Transform shoulderAnchor, ApexGrabber handGrabber)
        {
            trackingTarget = target;
            shoulder = shoulderAnchor;
            grabber = handGrabber;
        }
#endif
    }

    /// <summary>OpenXR input adapter for the physical Apex VR rig.</summary>
    [DisallowMultipleComponent]
    public sealed class ApexPhysicalOpenXRInput : MonoBehaviour
    {
        [SerializeField] private ApexPhysicalOpenXRBody body;
        [SerializeField] private ApexGrabber leftGrabber;
        [SerializeField] private ApexGrabber rightGrabber;
        [SerializeField, Range(0.1f, 1f)] private float turnThreshold = 0.7f;

        private bool leftGrip;
        private bool rightGrip;
        private bool jumpHeld;
        private bool turnLatched;

        private void Update()
        {
            InputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            InputDevice right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

            left.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 move);
            right.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 turn);
            body?.SetMoveInput(move);

            if (!turnLatched && Mathf.Abs(turn.x) >= turnThreshold)
            {
                body?.QueueSnapTurn(turn.x);
                turnLatched = true;
            }
            else if (Mathf.Abs(turn.x) < turnThreshold * 0.5f)
            {
                turnLatched = false;
            }

            UpdateGrab(left, leftGrabber, ref leftGrip);
            UpdateGrab(right, rightGrabber, ref rightGrip);

            left.TryGetFeatureValue(CommonUsages.primaryButton, out bool jump);
            if (jump && !jumpHeld)
            {
                body?.QueueJump();
            }
            jumpHeld = jump;
        }

        private static void UpdateGrab(InputDevice device, ApexGrabber grabber, ref bool previous)
        {
            device.TryGetFeatureValue(CommonUsages.gripButton, out bool held);
            if (held && !previous)
            {
                grabber?.TryGrabClosest();
            }
            else if (!held && previous)
            {
                grabber?.Release();
            }
            previous = held;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            ApexPhysicalOpenXRBody physicalBody,
            ApexGrabber left,
            ApexGrabber right)
        {
            body = physicalBody;
            leftGrabber = left;
            rightGrabber = right;
        }
#endif
    }
}
